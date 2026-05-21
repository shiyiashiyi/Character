/**
 * PersonaForgeService.cs — 从小说文本抽取台词并生成 persona skill 草稿
 * 流程对齐 novel-character-persona-forge：证据抽取 → 模板填充（启发式，非 LLM）
 */
using System.Text;
using System.Text.RegularExpressions;
using FrontStudy.Api.DTOs;

namespace FrontStudy.Api.Services;

public class PersonaForgeService
{
    private const int MaxQuotes = 24;
    private const int MaxFileChars = 800_000;

    public PersonaForgeResponse Forge(string text, string characterName, string? workTitle, string? chapterRange)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Fail("请上传文本文件或提供正文内容");

        if (string.IsNullOrWhiteSpace(characterName))
            return Fail("请填写人物姓名");

        if (text.Length > MaxFileChars)
            return Fail($"文本过长，请控制在 {MaxFileChars / 10000} 万字以内或截取章节");

        var name = characterName.Trim();
        var work = string.IsNullOrWhiteSpace(workTitle) ? "（未指定作品名）" : workTitle.Trim();
        var range = string.IsNullOrWhiteSpace(chapterRange) ? "用户上传全文" : chapterRange.Trim();
        var slug = ToSlug(name);

        var quotes = ExtractQuotes(text, name);
        var mentions = ExtractMentionSnippets(text, name, 8);
        var languageRules = InferLanguageRules(quotes, text, name);
        var constraints = InferConstraints(name);
        var oneLiner = BuildOneLiner(name, quotes, mentions);
        var sampleReply = BuildSampleReply(name, quotes);

        var skillMd = BuildSkillMarkdown(name, slug, work, range, oneLiner, languageRules, constraints, quotes, sampleReply);
        var evidenceMd = BuildEvidenceMarkdown(name, work, range, quotes, mentions);

        return new PersonaForgeResponse(
            true,
            quotes.Count >= 8
                ? "已生成 Skill 草稿（台词充足）"
                : $"已生成 Skill 草稿（仅抽到 {quotes.Count} 条原话，建议补充文本后重试）",
            name,
            slug,
            work,
            new PersonaSummaryDto(oneLiner, quotes.Count, languageRules, constraints),
            skillMd,
            evidenceMd);
    }

    private static PersonaForgeResponse Fail(string msg) =>
        new(false, msg, null, null, null, null, null, null);

    private static string ToSlug(string name)
    {
        var lower = name.ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var c in lower)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '_' or '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length < 2) slug = "character-" + Math.Abs(name.GetHashCode());
        return "persona-" + slug[..Math.Min(slug.Length, 40)].TrimEnd('-');
    }

    private static List<QuoteEntry> ExtractQuotes(string text, string name)
    {
        var results = new List<QuoteEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        // 「对白」前带说话人
        var speakerPattern = new Regex(
            $@"(?<speaker>{Regex.Escape(name)})\s*(?:说|说道|道|问|问道|答|答道|笑道|沉声道|冷声道|怒道|叹道|喊|低声道|轻声道|缓缓道|淡淡道)[：:,，]?\s*[「""](?<line>[^」""]+)[」""]",
            RegexOptions.Multiline);

        foreach (Match m in speakerPattern.Matches(text))
        {
            AddQuote(results, seen, m.Groups["line"].Value.Trim(), m.Groups["speaker"].Value, "对话", GuessChapter(text, m.Index));
        }

        // 行内「」且同段含人名
        var quotePattern = new Regex("[「\"](?<line>[^」\"]{2,120})[」\"]", RegexOptions.Multiline);
        foreach (Match m in quotePattern.Matches(text))
        {
            var start = Math.Max(0, m.Index - 80);
            var ctx = text.Substring(start, Math.Min(160, text.Length - start));
            if (!ctx.Contains(name, StringComparison.Ordinal)) continue;
            AddQuote(results, seen, m.Groups["line"].Value.Trim(), name, "语境含角色名", GuessChapter(text, m.Index));
        }

        return results.Take(MaxQuotes).ToList();
    }

    private static void AddQuote(List<QuoteEntry> list, HashSet<string> seen, string line, string speaker, string scene, string chapter)
    {
        if (line.Length < 2 || seen.Contains(line)) return;
        seen.Add(line);
        list.Add(new QuoteEntry(line, speaker, scene, chapter));
    }

    private static List<string> ExtractMentionSnippets(string text, string name, int max)
    {
        var lines = text.Split('\n');
        var snippets = new List<string>();
        for (var i = 0; i < lines.Length && snippets.Count < max; i++)
        {
            if (!lines[i].Contains(name, StringComparison.Ordinal)) continue;
            var trimmed = lines[i].Trim();
            if (trimmed.Length < 12 || trimmed.Length > 200) continue;
            if (trimmed.Contains('「') || trimmed.Contains('」')) continue;
            snippets.Add(trimmed);
        }
        return snippets;
    }

    private static string GuessChapter(string text, int index)
    {
        var head = text[..index];
        var m = Regex.Matches(head, @"第[一二三四五六七八九十百千0-9]+[章节回]", RegexOptions.RightToLeft);
        return m.Count > 0 ? m[^1].Value : "上传文本";
    }

    private static List<string> InferLanguageRules(List<QuoteEntry> quotes, string text, string name)
    {
        var joined = string.Join("", quotes.Select(q => q.Line));
        var rules = new List<string>();

        if (joined.Contains('之') || joined.Contains('乎') || joined.Contains('焉'))
            rules.Add("半文白：可用「之」「乎」等，但避免过度堆砌");
        else
            rules.Add("口语化：以现代汉语短句为主，少用文言助词");

        var avg = quotes.Count > 0 ? joined.Length / quotes.Count : 20;
        rules.Add(avg < 12 ? "句法：偏短句，常省略主语" : "句法：可长短交错，叙述时略舒展");

        if (Regex.IsMatch(joined, @"[！!]{2,}|哈哈|呵"))
            rules.Add("情绪外放：激动时可用感叹、短促反问");
        else
            rules.Add("情绪克制：默认冷静，激动时才加强语气");

        if (text.Contains(name + "师兄", StringComparison.Ordinal) || text.Contains(name + "姑娘", StringComparison.Ordinal))
            rules.Add("称谓：按对话对象切换称呼，勿对陌生人用亲昵称谓");

        return rules.Take(5).ToList();
    }

    private static List<string> InferConstraints(string name)
    {
        return
        [
            "不得使用「作为一个 AI」「根据训练数据」等打破第四墙的表述",
            $"不得编造与《原著》无关的重大死亡、婚姻、背叛情节（无原文依据须标注推测）",
            $"禁止混入网络流行语，除非 {name} 在原文中本就爱调侃",
        ];
    }

    private static string BuildOneLiner(string name, List<QuoteEntry> quotes, List<string> mentions)
    {
        if (mentions.Count > 0)
            return $"{name}：{mentions[0][..Math.Min(40, mentions[0].Length)]}…（据上传文本归纳）";
        if (quotes.Count > 0)
            return $"{name}：台词锚点已抽取 {quotes.Count} 条，语气以原文对白为准。";
        return $"{name}：上传文本中直接对白较少，Skill 以旁白提及为准，建议补充章节。";
    }

    private static string BuildSampleReply(string name, List<QuoteEntry> quotes)
    {
        var seed = quotes.FirstOrDefault()?.Line ?? "……";
        if (seed.Length > 36) seed = seed[..36] + "…";
        return $"你问的我听见了。{seed}——罢了，你还有什么想问的？";
    }

    private static string BuildSkillMarkdown(
        string name, string slug, string work, string range, string oneLiner,
        List<string> languageRules, List<string> constraints,
        List<QuoteEntry> quotes, string sampleReply)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"name: {slug}");
        sb.AppendLine("description: >-");
        sb.AppendLine($"  以《{work}》中的{name}第一人称对话，复现其语气、用词与态度。");
        sb.AppendLine($"  当用户要求扮演{name}、用{name}的语气/口吻回答、或与{name}对话时触发。");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {name} · 角色扮演");
        sb.AppendLine();
        sb.AppendLine("## 身份摘要");
        sb.AppendLine();
        sb.AppendLine(oneLiner);
        sb.AppendLine();
        sb.AppendLine("> 本文件由 Character Persona Forge 根据上传文本自动生成，请人工核对后再放入 `~/.cursor/skills/`。");
        sb.AppendLine();
        sb.AppendLine("## 说话风格（执行规则）");
        sb.AppendLine();
        for (var i = 0; i < languageRules.Count; i++)
            sb.AppendLine($"{i + 1}. {languageRules[i]}");
        sb.AppendLine();
        sb.AppendLine("## 价值观与动机");
        sb.AppendLine();
        sb.AppendLine("- [无原文支撑] 请根据下方原话与 evidence 文件手工补全信念与目标。");
        sb.AppendLine();
        sb.AppendLine("## 关键经历（影响语气）");
        sb.AppendLine();
        sb.AppendLine("| 阶段 | 经历 | 对说话方式的影响 |");
        sb.AppendLine("|------|------|------------------|");
        sb.AppendLine("| 上传文本 | 见 source-evidence | 依原文提及归纳 |");
        sb.AppendLine();
        sb.AppendLine("## 代表性原话（语气锚点）");
        sb.AppendLine();
        sb.AppendLine("> 以下引文来自上传文本自动抽取，扮演时模仿节奏与用词。");
        sb.AppendLine();
        if (quotes.Count == 0)
            sb.AppendLine("1. （未抽到对白，请手工从原著补 8 条以上）");
        else
        {
            for (var i = 0; i < quotes.Count; i++)
            {
                var q = quotes[i];
                sb.AppendLine($"{i + 1}. 「{q.Line}」— {q.Scene}（{q.Chapter}）");
            }
            if (quotes.Count < 8)
                sb.AppendLine($"\n（原文台词仅 {quotes.Count} 条，不足 8 条）");
        }
        sb.AppendLine();
        sb.AppendLine("## 扮演模式");
        sb.AppendLine();
        sb.AppendLine("1. **沉浸**：全程第一人称，不跳出角色。");
        sb.AppendLine("2. **未知问题**：前缀「书上没写到，但我觉着……」");
        sb.AppendLine("3. **长度**：日常 2～6 句。");
        sb.AppendLine();
        sb.AppendLine("## 关键约束");
        sb.AppendLine();
        sb.AppendLine("**MUST**");
        sb.AppendLine();
        sb.AppendLine("- 语气与上表原话锚点一致");
        sb.AppendLine();
        sb.AppendLine("**MUST NOT**");
        sb.AppendLine();
        foreach (var c in constraints)
            sb.AppendLine($"- {c}");
        sb.AppendLine();
        sb.AppendLine("## 示例对话");
        sb.AppendLine();
        sb.AppendLine("**用户**：你怎么看待眼前这件事？");
        sb.AppendLine($"**{name}**：{sampleReply}");
        sb.AppendLine();
        sb.AppendLine("## 证据索引");
        sb.AppendLine();
        sb.AppendLine("详见 [references/source-evidence.md](references/source-evidence.md)。");
        return sb.ToString();
    }

    private static string BuildEvidenceMarkdown(string name, string work, string range, List<QuoteEntry> quotes, List<string> mentions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {name} · 原文证据库");
        sb.AppendLine();
        sb.AppendLine("## 作品与范围");
        sb.AppendLine();
        sb.AppendLine($"- 作品：《{work}》");
        sb.AppendLine($"- 分析范围：{range}");
        sb.AppendLine($"- 生成方式：Persona Forge 自动抽取（请人工校对）");
        sb.AppendLine();
        sb.AppendLine("## 台词全集（自动抽取）");
        sb.AppendLine();
        sb.AppendLine("| # | 原文 | 说话对象 | 场景 | 章节 |");
        sb.AppendLine("|---|------|----------|------|------|");
        for (var i = 0; i < quotes.Count; i++)
        {
            var q = quotes[i];
            sb.AppendLine($"| {i + 1} | {q.Line} | {q.Speaker} | {q.Scene} | {q.Chapter} |");
        }
        sb.AppendLine();
        sb.AppendLine("## 旁白/提及片段");
        sb.AppendLine();
        foreach (var m in mentions)
            sb.AppendLine($"- {m}");
        sb.AppendLine();
        sb.AppendLine("## 推测项（低置信）");
        sb.AppendLine();
        sb.AppendLine("- [推测·低置信] 价值观与关系网需结合全书人工补全。");
        return sb.ToString();
    }

    private sealed record QuoteEntry(string Line, string Speaker, string Scene, string Chapter);
}
