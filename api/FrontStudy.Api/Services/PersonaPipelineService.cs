/**
 * PersonaPipelineService.cs — 多阶段角色人格生成流水线（M2 重写）。
 *
 * Stage 0 预处理：分章 + 分块
 * Stage 1 检索：按角色名关键词排序，取相关块（不嵌向量，RAG-lite）
 * Stage 2 证据抽取（map）：逐块让 LLM 提取原话/旁白，带章节引用
 * Stage 3 人格综合（reduce）：LLM 显式产出性格/动机/价值观/关系
 * Stage 4 语气样本：LLM 生成多条示例对话（few-shot 锁定口吻）
 * Stage 5 组装：角色卡 JSON + SKILL.md + source-evidence.md
 */
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;

namespace FrontStudy.Api.Services;

public class PersonaPipelineService(
    LlmChatClient llm,
    ILogger<PersonaPipelineService> logger)
{
    private const int ChunkSize = 24_000;       // 单块字符数（控制单次 LLM 上下文）
    private const int ChunkOverlap = 800;        // 块间重叠，避免边界切断句子
    private const int MaxRelevantChunks = 6;     // 参与 LLM 抽取的最大块数
    private const int MaxEvidence = 60;          // 证据上限，控制后续综合的 token

    private const string ExtractSystem =
        "你是小说人物分析助手。只返回合法 JSON，不要输出任何解释或额外文字。";

    public string? Validate() => llm.Validate();

    public async Task<PersonaForgeResponse> ForgeAsync(
        string text,
        string characterName,
        string? workTitle,
        string? chapterRange,
        ForgeJob job,
        CancellationToken ct)
    {
        var name = characterName.Trim();
        var work = string.IsNullOrWhiteSpace(workTitle) ? "（未指定作品名）" : workTitle.Trim();
        var range = string.IsNullOrWhiteSpace(chapterRange) ? "用户上传全文" : chapterRange.Trim();

        // Stage 0：预处理
        SetStage(job, "preprocess", 5);
        var chunks = Preprocess(text);
        if (chunks.Count == 0)
            return Fail("文本为空或无法分块");

        // Stage 1：检索相关块
        SetStage(job, "upload", 10);
        var relevant = Retrieve(chunks, name);

        // Stage 2：证据抽取（map，按块上报进度）
        var evidence = await ExtractEvidenceAsync(relevant, name, work, job, ct);
        if (evidence is null || evidence.Count == 0)
            return Fail("证据抽取失败：LLM 未返回有效证据，可重试或换用规则模式");

        // Stage 3：人格综合（reduce）
        SetStage(job, "synthesize", 80);
        var persona = await llm.ChatJsonAsync<PersonaSynthesis>(
            ExtractSystem, BuildSynthesisPrompt(name, work, evidence), ct);
        if (persona is null)
            return Fail("人格综合失败：LLM 未返回有效结果");

        // Stage 4：语气样本
        SetStage(job, "examples", 90);
        var examplesResult = await llm.ChatJsonAsync<ExamplesResult>(
            ExtractSystem, BuildExamplesPrompt(name, persona, evidence), ct);
        var examples = examplesResult?.Examples?.Where(e => !string.IsNullOrWhiteSpace(e.Char)).ToList() ?? [];

        // Stage 5：组装
        SetStage(job, "assemble", 98);
        var card = AssembleCard(name, work, range, persona, examples);
        var skillMd = RenderSkillMarkdown(card);
        var evidenceMd = RenderEvidenceMarkdown(name, work, range, evidence);
        var quoteCount = evidence.Count(e => string.Equals(e.Type, "quote", StringComparison.OrdinalIgnoreCase));

        var summary = new PersonaSummaryDto(
            card.Identity.Summary,
            quoteCount,
            BuildLanguageRules(card.SpeechStyle),
            card.Constraints.MustNot);

        return new PersonaForgeResponse(
            true,
            $"已生成角色卡（证据 {evidence.Count} 条 · 示例对话 {examples.Count} 条）",
            name,
            card.Name,
            work,
            summary,
            skillMd,
            evidenceMd,
            JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ---------------------------------------------------------------- Stage 0

    private sealed record TextChunk(string Chapter, int Index, string Text);

    private static List<TextChunk> Preprocess(string text)
    {
        var chunks = new List<TextChunk>();
        var chapterPattern = new Regex(
            @"^(第[一二三四五六七八九十百千0-9]+[章节回][^\r\n]*)",
            RegexOptions.Multiline);
        var matches = chapterPattern.Matches(text);

        // 无章节标记：整篇按大小分块
        if (matches.Count == 0)
        {
            ChunkBody("全文", 0, text, chunks);
            return chunks;
        }

        // 章节标题之前的引言
        if (matches[0].Index > 0)
            ChunkBody("前言", -1, text[..matches[0].Index], chunks);

        // 逐章切分
        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            ChunkBody(matches[i].Value.Trim(), i, text[start..end], chunks);
        }

        return chunks;
    }

    private static void ChunkBody(string chapter, int index, string body, List<TextChunk> chunks)
    {
        var t = body.Trim();
        if (t.Length == 0) return;

        if (t.Length <= ChunkSize)
        {
            chunks.Add(new TextChunk(chapter, index, t));
            return;
        }

        for (var pos = 0; pos < t.Length; pos += ChunkSize - ChunkOverlap)
        {
            var len = Math.Min(ChunkSize, t.Length - pos);
            chunks.Add(new TextChunk(chapter, index, t.Substring(pos, len)));
        }
    }

    // ---------------------------------------------------------------- Stage 1

    private static List<TextChunk> Retrieve(List<TextChunk> chunks, string name)
    {
        var ranked = chunks
            .Select(c => new { Chunk = c, Hits = CountOccurrences(c.Text, name) })
            .OrderByDescending(x => x.Hits)
            .ThenBy(x => x.Chunk.Index)
            .ToList();

        var selected = ranked
            .Where(x => x.Hits > 0)
            .Select(x => x.Chunk)
            .Take(MaxRelevantChunks)
            .ToList();

        // 第一章常含人物介绍，若未被选中则优先补入
        var intro = ranked.FirstOrDefault();
        if (intro is not null && !selected.Any(c => c.Chapter == intro.Chunk.Chapter && c.Index == intro.Chunk.Index))
            selected.Insert(0, intro.Chunk);

        if (selected.Count == 0)
            selected = ranked.Take(Math.Min(MaxRelevantChunks, ranked.Count)).Select(x => x.Chunk).ToList();

        return selected;
    }

    private static int CountOccurrences(string text, string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(name, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += name.Length;
        }
        return count;
    }

    // ---------------------------------------------------------------- Stage 2

    private sealed record EvidenceItem(string Type, string Text, string Chapter, string Speaker, double Confidence);
    private sealed record EvidenceChunkResult(List<LlmEvidence>? Evidence);
    private sealed record LlmEvidence(string? Type, string? Text, string? Chapter, string? Speaker, double? Confidence);

    private async Task<List<EvidenceItem>?> ExtractEvidenceAsync(
        List<TextChunk> chunks, string name, string work, ForgeJob job, CancellationToken ct)
    {
        var all = new List<EvidenceItem>();
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            SetStage(job, "evidence", 20 + (int)((i + 1) * 50.0 / chunks.Count), $"块 {i + 1}/{chunks.Count}");
            var result = await llm.ChatJsonAsync<EvidenceChunkResult>(
                ExtractSystem, BuildEvidencePrompt(name, work, chunk), ct);
            if (result?.Evidence is null) continue;

            foreach (var e in result.Evidence)
            {
                if (string.IsNullOrWhiteSpace(e.Text)) continue;
                all.Add(new EvidenceItem(
                    string.IsNullOrWhiteSpace(e.Type) ? "quote" : e.Type.Trim(),
                    e.Text.Trim(),
                    string.IsNullOrWhiteSpace(e.Chapter) ? chunk.Chapter : e.Chapter.Trim(),
                    string.IsNullOrWhiteSpace(e.Speaker) ? name : e.Speaker.Trim(),
                    e.Confidence ?? 0.5));
            }
        }

        if (all.Count == 0)
        {
            logger.LogWarning("证据抽取：{Chunks} 个块均未返回证据", chunks.Count);
            return null;
        }

        // 去重（按原文文本）+ 截断
        return all
            .GroupBy(e => e.Text)
            .Select(g => g.First())
            .Take(MaxEvidence)
            .ToList();
    }

    private static string BuildEvidencePrompt(string name, string work, TextChunk chunk)
    {
        return $$"""
        作品：《{{work}}》
        目标角色：{{name}}
        章节：{{chunk.Chapter}}

        请从下面的小说片段中，抽取与角色「{{name}}」相关的【原话台词】和【旁白提及】，作为角色扮演证据。
        要求：
        1. 原话必须逐字摘自原文，不得改写、不得概括；每条 text 不超过 100 字；
        2. 只抽取「{{name}}」本人说的话，以及直接描写「{{name}}」的旁白；不要把其它角色的台词算进来；
        3. **最多返回 12 条**：优先台词、其次旁白，宁可少而精，不要凑数；
        4. 每条给出 type（quote=台词，mention=旁白提及）、chapter、speaker、confidence（0~1 的数字）；
        5. 返回 JSON 对象，格式：
        { "evidence": [ { "type": "quote", "text": "……", "chapter": "……", "speaker": "……", "confidence": 0.9 } ] }

        小说片段：
        {{chunk.Text}}
        """;
    }

    // ---------------------------------------------------------------- Stage 3

    private sealed record PersonaSynthesis(
        IdentitySynthesis? Identity,
        List<PersonalitySynthesis>? Personality,
        MotivationSynthesis? Motivation,
        List<BackstorySynthesis>? Backstory,
        List<RelationshipSynthesis>? Relationships,
        SpeechStyleSynthesis? SpeechStyle);

    private sealed record IdentitySynthesis(List<string>? Aliases, string? Role, string? Appearance, string? Summary);
    private sealed record PersonalitySynthesis(string? Trait, double? Intensity, string? Evidence);
    private sealed record MotivationSynthesis(List<string>? Goals, List<string>? Values, List<string>? BottomLines);
    private sealed record BackstorySynthesis(string? Event, string? Effect, string? Evidence);
    private sealed record RelationshipSynthesis(string? Person, string? Relation, string? Evidence);
    private sealed record SpeechStyleSynthesis(List<string>? Catchphrases, string? SentenceLength, string? Register, string? Notes);

    private static string BuildSynthesisPrompt(string name, string work, List<EvidenceItem> evidence)
    {
        var evidenceJson = JsonSerializer.Serialize(evidence);
        return $$"""
        作品：《{{work}}》
        目标角色：{{name}}

        下面是已抽取的原文证据（台词 + 旁白）。请通读证据，综合出该角色的人格画像。
        严格约束：
        1. 只能依据下方证据，不得编造证据中不存在的情节、关系、死亡、背叛、身份设定；
        2. 证据不足的维度，明确写「证据不足」而不是猜测；
        3. personality 的每一项都要附 evidence（引用证据原文，或写「证据不足」）；
        4. 返回 JSON 对象，格式：
        {
          "identity": { "aliases": [], "role": "", "appearance": "", "summary": "" },
          "personality": [ { "trait": "", "intensity": 0.8, "evidence": "" } ],
          "motivation": { "goals": [], "values": [], "bottomLines": [] },
          "backstory": [ { "event": "", "effect": "", "evidence": "" } ],
          "relationships": [ { "person": "", "relation": "", "evidence": "" } ],
          "speechStyle": { "catchphrases": [], "sentenceLength": "", "register": "", "notes": "" }
        }

        证据：
        {{evidenceJson}}
        """;
    }

    // ---------------------------------------------------------------- Stage 4

    private sealed record ExamplesResult(List<DialogueExample>? Examples);

    private static string BuildExamplesPrompt(string name, PersonaSynthesis persona, List<EvidenceItem> evidence)
    {
        var personaJson = JsonSerializer.Serialize(persona);
        var quotesJson = JsonSerializer.Serialize(
            evidence.Where(e => string.Equals(e.Type, "quote", StringComparison.OrdinalIgnoreCase))
                    .Select(e => e.Text)
                    .Take(20));
        return $$"""
        目标角色：{{name}}
        人格画像：{{personaJson}}
        代表性原话：{{quotesJson}}

        请为「{{name}}」生成 5 条示例对话（user 是用户说的话，char 是「{{name}}」的回复）。
        要求：
        1. char 的回复必须符合人格画像，并模仿代表性原话的语气、用词、句长；
        2. 不得使用「作为一个 AI」「根据训练数据」等打破第四墙的表述；
        3. 返回 JSON：{ "examples": [ { "user": "", "char": "" } ] }
        """;
    }

    // ---------------------------------------------------------------- Stage 5

    private static CharacterCard AssembleCard(
        string name, string work, string range, PersonaSynthesis p, List<DialogueExample> examples)
    {
        var identity = p.Identity;
        var motivation = p.Motivation;
        var speech = p.SpeechStyle;

        return new CharacterCard(
            ToSlug(name),
            name,
            new WorkInfo(work, range),
            new IdentityInfo(
                OrEmpty(identity?.Aliases),
                Empty(identity?.Role),
                Empty(identity?.Appearance),
                string.IsNullOrWhiteSpace(identity?.Summary) ? "证据不足，需要人工补充" : identity!.Summary!),
            OrEmpty(p.Personality).Select(x => new TraitInfo(
                Empty(x.Trait), x.Intensity ?? 0.5, Empty(x.Evidence))).ToList(),
            new MotivationInfo(
                OrEmpty(motivation?.Goals), OrEmpty(motivation?.Values), OrEmpty(motivation?.BottomLines)),
            OrEmpty(p.Backstory).Select(x => new BackstoryEvent(
                Empty(x.Event), Empty(x.Effect), Empty(x.Evidence))).ToList(),
            OrEmpty(p.Relationships).Select(x => new RelationshipInfo(
                Empty(x.Person), Empty(x.Relation), Empty(x.Evidence))).ToList(),
            new SpeechStyleInfo(
                OrEmpty(speech?.Catchphrases), Empty(speech?.SentenceLength), Empty(speech?.Register), Empty(speech?.Notes)),
            new ConstraintsInfo(
                ["安全与隐私", "事实与真实性", "用户意图", "核心任务", "人设一致性", "语言风格"],
                ["全程第一人称，不跳出角色", "无原文证据的设定必须标注为推测"],
                [
                    "不得使用「作为一个 AI」「根据训练数据」等打破第四墙的表述",
                    "不得编造原文不存在的情节、关系、身份设定",
                    $"禁止混入与 {name} 原文气质不符的网络流行语"
                ]),
            examples);
    }

    private static string RenderSkillMarkdown(CharacterCard card)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {card.Name}");
        sb.AppendLine("description: >-");
        sb.AppendLine($"  以《{card.Work.Title}》中的{card.CharacterName}第一人称对话，复现其语气、用词与态度。");
        sb.AppendLine($"  当用户要求扮演{card.CharacterName}、用{card.CharacterName}的语气回答时触发。");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {card.CharacterName} · 角色扮演");
        sb.AppendLine();
        sb.AppendLine("## 身份摘要");
        sb.AppendLine();
        sb.AppendLine(card.Identity.Summary);
        sb.AppendLine($"- 别名：{string.Join("、", card.Identity.Aliases.Count > 0 ? card.Identity.Aliases : ["（无）"])}");
        sb.AppendLine($"- 定位：{card.Identity.Role}");
        sb.AppendLine($"- 外貌：{card.Identity.Appearance}");
        sb.AppendLine();
        sb.AppendLine("## 性格特质");
        sb.AppendLine();
        if (card.Personality.Count == 0)
        {
            sb.AppendLine("- （证据不足）");
        }
        else
        {
            foreach (var t in card.Personality)
                sb.AppendLine($"- **{t.Trait}**（{t.Intensity:P0}）— 依据：{t.Evidence}");
        }
        sb.AppendLine();
        sb.AppendLine("## 价值观与动机");
        sb.AppendLine();
        sb.AppendLine($"- 目标：{JoinOrEmpty(card.Motivation.Goals)}");
        sb.AppendLine($"- 价值观：{JoinOrEmpty(card.Motivation.Values)}");
        sb.AppendLine($"- 底线：{JoinOrEmpty(card.Motivation.BottomLines)}");
        sb.AppendLine();
        sb.AppendLine("## 关键经历");
        sb.AppendLine();
        sb.AppendLine("| 经历 | 对说话方式的影响 | 依据 |");
        sb.AppendLine("|------|------------------|------|");
        if (card.Backstory.Count == 0) sb.AppendLine("| （证据不足） | — | — |");
        foreach (var b in card.Backstory)
            sb.AppendLine($"| {EscapeCell(b.Event)} | {EscapeCell(b.Effect)} | {EscapeCell(b.Evidence)} |");
        sb.AppendLine();
        sb.AppendLine("## 关系网");
        sb.AppendLine();
        if (card.Relationships.Count == 0) sb.AppendLine("- （证据不足）");
        foreach (var r in card.Relationships)
            sb.AppendLine($"- {r.Person}：{r.Relation}（依据：{r.Evidence}）");
        sb.AppendLine();
        sb.AppendLine("## 说话风格");
        sb.AppendLine();
        sb.AppendLine($"- 口头禅：{JoinOrEmpty(card.SpeechStyle.Catchphrases)}");
        sb.AppendLine($"- 句法：{card.SpeechStyle.SentenceLength}");
        sb.AppendLine($"- 语体：{card.SpeechStyle.Register}");
        sb.AppendLine($"- 备注：{card.SpeechStyle.Notes}");
        sb.AppendLine();
        sb.AppendLine("## 扮演模式");
        sb.AppendLine();
        sb.AppendLine("1. **沉浸**：全程第一人称，不跳出角色。");
        sb.AppendLine("2. **未知问题**：可说「书上没写到，但我会先按自己的判断来……」。");
        sb.AppendLine("3. **长度**：日常 2-6 句，复杂局势可分点分析。");
        sb.AppendLine();
        sb.AppendLine("## 关键约束");
        sb.AppendLine();
        sb.AppendLine("**优先级（冲突时从上到下裁决）**");
        sb.AppendLine();
        foreach (var p in card.Constraints.PriorityOrder)
            sb.AppendLine($"1. {p}");
        sb.AppendLine();
        sb.AppendLine("**MUST**");
        sb.AppendLine();
        foreach (var m in card.Constraints.Must) sb.AppendLine($"- {m}");
        sb.AppendLine();
        sb.AppendLine("**MUST NOT**");
        sb.AppendLine();
        foreach (var m in card.Constraints.MustNot) sb.AppendLine($"- {m}");
        sb.AppendLine();
        sb.AppendLine("## 示例对话");
        sb.AppendLine();
        if (card.Examples.Count == 0)
        {
            sb.AppendLine("（无示例）");
        }
        else
        {
            foreach (var ex in card.Examples)
            {
                sb.AppendLine($"**用户**：{ex.User}");
                sb.AppendLine($"**{card.CharacterName}**：{ex.Char}");
                sb.AppendLine();
            }
        }
        sb.AppendLine("## 证据索引");
        sb.AppendLine();
        sb.AppendLine("详见 [references/source-evidence.md](references/source-evidence.md)。");
        return sb.ToString();
    }

    private static string RenderEvidenceMarkdown(
        string name, string work, string range, List<EvidenceItem> evidence)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {name} · 原文证据库");
        sb.AppendLine();
        sb.AppendLine("## 作品与范围");
        sb.AppendLine();
        sb.AppendLine($"- 作品：《{work}》");
        sb.AppendLine($"- 分析范围：{range}");
        sb.AppendLine("- 生成方式：LLM 通读原文抽取（请人工校对）");
        sb.AppendLine();
        sb.AppendLine("## 台词 / 旁白全集");
        sb.AppendLine();
        sb.AppendLine("| # | 类型 | 原文 | 说话人 | 章节 | 置信度 |");
        sb.AppendLine("|---|------|------|--------|------|--------|");
        for (var i = 0; i < evidence.Count; i++)
        {
            var e = evidence[i];
            sb.AppendLine($"| {i + 1} | {EscapeCell(e.Type)} | {EscapeCell(e.Text)} | {EscapeCell(e.Speaker)} | {EscapeCell(e.Chapter)} | {e.Confidence:P0} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 推测项（低置信）");
        sb.AppendLine();
        sb.AppendLine("- 性格强度、关系网等综合维度由模型依据证据归纳，可能存在偏差，请结合全书人工复核。");
        return sb.ToString();
    }

    private static List<string> BuildLanguageRules(SpeechStyleInfo speech)
    {
        var rules = new List<string>
        {
            $"语体：{speech.Register}",
            $"句法：{speech.SentenceLength}",
        };
        if (speech.Catchphrases.Count > 0)
            rules.Add($"口头禅：{string.Join("、", speech.Catchphrases)}");
        if (!string.IsNullOrWhiteSpace(speech.Notes))
            rules.Add($"备注：{speech.Notes}");
        return rules;
    }

    private static string ToSlug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '_' or '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length < 2) slug = "character-" + Math.Abs(name.GetHashCode());
        return "persona-" + slug[..Math.Min(slug.Length, 40)].TrimEnd('-');
    }

    private static string EscapeCell(string text) => text.Replace("|", "\\|");

    private static string JoinOrEmpty(List<string> list) =>
        list.Count == 0 ? "（证据不足）" : string.Join("、", list);

    private static List<T> OrEmpty<T>(List<T>? list) => list ?? [];

    private static string Empty(string? s) => string.IsNullOrWhiteSpace(s) ? "（证据不足）" : s;

    private static PersonaForgeResponse Fail(string msg) =>
        new(false, msg, null, null, null, null, null, null, null);

    /// <summary>上报当前阶段与进度到任务（SSE/轮询据此渲染）。</summary>
    private static void SetStage(ForgeJob job, string key, int percent, string? message = null)
    {
        job.CurrentStage = ForgeJob.Stages.FirstOrDefault(s => s.Key == key) ?? new ForgeStage(key, key, message);
        job.Percent = Math.Clamp(percent, 0, 99);
        if (message is not null) job.Message = message;
    }
}
