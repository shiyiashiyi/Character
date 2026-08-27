/**
 * PersonaForgeService.cs - Extracts character evidence from uploaded fiction
 * and builds the first-pass persona skill draft.
 */
using System.Text;
using System.Text.RegularExpressions;
using FrontStudy.Api.DTOs;

namespace FrontStudy.Api.Services;

public class PersonaForgeService
{
    private const int MaxQuotes = 32;
    private const int MaxMentions = 16;
    private const int MaxFileChars = 800_000;
    private const int MaxQuoteLength = 160;

    private static readonly string[] FirstPersonMarkers =
    [
        "我说", "我问", "我答", "我回答", "我道", "我说道", "我喊", "我叫",
        "我低声", "我轻声", "我喃喃", "我嘀咕", "我心想", "我想"
    ];

    public PersonaForgeResponse Forge(
        string text,
        string characterName,
        string? workTitle,
        string? chapterRange)
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
        var mentions = ExtractMentionSnippets(text, name, MaxMentions);
        var languageRules = InferLanguageRules(quotes, text, name);
        var constraints = InferConstraints(name);
        var oneLiner = BuildOneLiner(name, quotes, mentions);
        var sampleReply = BuildSampleReply(name, quotes);

        var skillMd = BuildSkillMarkdown(
            name,
            slug,
            work,
            range,
            oneLiner,
            languageRules,
            constraints,
            quotes,
            sampleReply);
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
            evidenceMd,
            null);
    }

    private static PersonaForgeResponse Fail(string msg) =>
        new(false, msg, null, null, null, null, null, null, null);

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
        var results = new List<IndexedQuote>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AddExplicitSpeakerQuotes(text, name, results, seen);
        AddTrailingSpeakerQuotes(text, name, results, seen);
        AddNameColonQuotes(text, name, results, seen);
        AddFirstPersonNarrationQuotes(text, name, results, seen);
        AddContextualQuotes(text, name, results, seen);

        return results
            .OrderBy(q => q.Index)
            .Take(MaxQuotes)
            .Select(q => q.Entry)
            .ToList();
    }

    // Handles: 隗辛说：“……” / 隗辛问：“……”
    private static void AddExplicitSpeakerQuotes(
        string text,
        string name,
        List<IndexedQuote> results,
        HashSet<string> seen)
    {
        var pattern = new Regex(
            $@"(?<speaker>{Regex.Escape(name)})\s*(?:说|说道|道|问|问道|答|答道|回答|笑道|沉声道|冷声道|怒道|叹道|喊|低声道|轻声道|缓缓道|淡淡道)[：:，,]?\s*[“「『""'](?<line>[^”」』""']{{2,{MaxQuoteLength}}})[”」』""']",
            RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(text))
            AddQuote(results, seen, m.Index, m.Groups["line"].Value, name, "角色名引导对白", GuessChapter(text, m.Index));
    }

    // Handles: “……”隗辛说 / “……”她说. For the target protagonist, nearby
    // third-person pronouns often refer back to the same character.
    private static void AddTrailingSpeakerQuotes(
        string text,
        string name,
        List<IndexedQuote> results,
        HashSet<string> seen)
    {
        var pattern = new Regex(
            $@"[“「『""'](?<line>[^”」』""']{{2,{MaxQuoteLength}}})[”」』""']\s*(?<speaker>{Regex.Escape(name)}|她|他|我)?\s*(?:说|说道|道|问|问道|答|答道|回答|喊|低声说|轻声说|心想)",
            RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(text))
        {
            if (m.Groups["speaker"].Value == name || ContextMentionsName(text, m.Index, name, 180))
                AddQuote(results, seen, m.Index, m.Groups["line"].Value, name, "后置说话人对白", GuessChapter(text, m.Index));
        }
    }

    // Handles web-novel script-like lines: 隗辛：好耶！
    private static void AddNameColonQuotes(
        string text,
        string name,
        List<IndexedQuote> results,
        HashSet<string> seen)
    {
        var pattern = new Regex(
            $@"(?:^|\n)\s*(?:{Regex.Escape(name)}|我)\s*[：:]\s*(?<line>[^\r\n]{{2,{MaxQuoteLength}}})",
            RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(text))
            AddQuote(results, seen, m.Index, m.Groups["line"].Value, name, "冒号对白", GuessChapter(text, m.Index));
    }

    // Handles first-person protagonist narration: 我说：“……” / 我心想：“……”.
    private static void AddFirstPersonNarrationQuotes(
        string text,
        string name,
        List<IndexedQuote> results,
        HashSet<string> seen)
    {
        if (!LooksLikeProtagonist(text, name)) return;

        var markerPattern = string.Join("|", FirstPersonMarkers.Select(Regex.Escape));
        var pattern = new Regex(
            $@"(?:{markerPattern})[：:，,]?\s*[“「『""'](?<line>[^”」』""']{{2,{MaxQuoteLength}}})[”」』""']",
            RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(text))
            AddQuote(results, seen, m.Index, m.Groups["line"].Value, name, "第一人称叙事对白", GuessChapter(text, m.Index));
    }

    // Fallback: any quote near the target name. This is lower confidence, so it
    // runs after stronger speaker patterns and still requires local context.
    private static void AddContextualQuotes(
        string text,
        string name,
        List<IndexedQuote> results,
        HashSet<string> seen)
    {
        var quotePattern = new Regex(
            $@"[“「『""'](?<line>[^”」』""']{{2,{MaxQuoteLength}}})[”」』""']",
            RegexOptions.Multiline);

        foreach (Match m in quotePattern.Matches(text))
        {
            if (!ContextMentionsName(text, m.Index, name, 220)) continue;
            AddQuote(results, seen, m.Index, m.Groups["line"].Value, name, "上下文含角色名", GuessChapter(text, m.Index));
        }
    }

    private static bool LooksLikeProtagonist(string text, string name)
    {
        var firstNameIndex = text.IndexOf(name, StringComparison.Ordinal);
        if (firstNameIndex < 0) return false;

        var head = text[..Math.Min(text.Length, 6000)];
        return head.Contains($"主角：{name}", StringComparison.Ordinal)
            || head.Contains($"姓名：{name}", StringComparison.Ordinal)
            || head.Contains($"{name}是", StringComparison.Ordinal)
            || Regex.Matches(head, Regex.Escape(name)).Count >= 3;
    }

    private static bool ContextMentionsName(string text, int index, string name, int radius)
    {
        var start = Math.Max(0, index - radius);
        var len = Math.Min(radius * 2, text.Length - start);
        return text.Substring(start, len).Contains(name, StringComparison.Ordinal);
    }

    private static void AddQuote(
        List<IndexedQuote> list,
        HashSet<string> seen,
        int index,
        string rawLine,
        string speaker,
        string scene,
        string chapter)
    {
        var line = CleanQuote(rawLine);
        if (line.Length < 2 || line.Length > MaxQuoteLength) return;
        if (IsLikelyNonCharacterLine(line)) return;
        if (!seen.Add(line)) return;

        list.Add(new IndexedQuote(index, new QuoteEntry(line, speaker, scene, chapter)));
    }

    private static string CleanQuote(string line) =>
        Regex.Replace(line.Trim(), @"\s+", " ")
            .Trim('　', ' ', '。', '，', ',', '“', '”', '「', '」', '『', '』', '"', '\'');

    private static bool IsLikelyNonCharacterLine(string line)
    {
        var systemLike = new[]
        {
            "第", "作者", "文案", "内容标签", "搜索关键字", "一句话简介", "立意",
            "vip", "科普", "百度百科"
        };

        return line.Length < 2
            || systemLike.Any(x => line.StartsWith(x, StringComparison.OrdinalIgnoreCase))
            || line.Count(c => c == '：' || c == ':') >= 3;
    }

    private static List<string> ExtractMentionSnippets(string text, string name, int max)
    {
        var lines = text.Split('\n');
        var snippets = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var raw in lines)
        {
            if (snippets.Count >= max) break;
            if (!raw.Contains(name, StringComparison.Ordinal)) continue;

            var trimmed = raw.Trim();
            if (trimmed.Length < 12 || trimmed.Length > 220) continue;
            if (trimmed.Contains('“') || trimmed.Contains('”') || trimmed.Contains('「') || trimmed.Contains('」')) continue;
            if (!seen.Add(trimmed)) continue;

            snippets.Add(trimmed);
        }

        return snippets;
    }

    private static string GuessChapter(string text, int index)
    {
        var head = text[..index];
        var m = Regex.Matches(head, @"第[一二三四五六七八九十百千0-9]+[章节回][^\r\n]*", RegexOptions.RightToLeft);
        return m.Count > 0 ? m[^1].Value.Trim() : "上传文本";
    }

    private static List<string> InferLanguageRules(List<QuoteEntry> quotes, string text, string name)
    {
        var joined = string.Join("", quotes.Select(q => q.Line));
        var rules = new List<string>();

        if (joined.Contains('之') || joined.Contains('乎') || joined.Contains('焉'))
            rules.Add("半文白：可少量使用古典助词，但不要堆砌。");
        else
            rules.Add("现代口语：以清晰、直接的现代汉语为主。");

        var avg = quotes.Count > 0 ? joined.Length / quotes.Count : 20;
        rules.Add(avg < 14 ? "句法：偏短句，反应快，常省略铺垫。" : "句法：长短交错，分析问题时会略微展开。");

        if (Regex.IsMatch(joined, @"[！!]{2,}|哈哈|好耶|发财|靠"))
            rules.Add("情绪：能吐槽、能短促感叹，但遇到危险会迅速转向冷静判断。");
        else
            rules.Add("情绪：默认克制冷静，紧张时也优先分析局势。");

        if (text.Contains("无神论", StringComparison.Ordinal))
            rules.Add("立场：倾向理性怀疑，不轻易接受神秘解释。");

        if (text.Contains("保住小命", StringComparison.Ordinal) || text.Contains("苟住", StringComparison.Ordinal))
            rules.Add("行动倾向：重视生存与风险评估，必要时会选择隐忍和伪装。");

        return rules.Take(5).ToList();
    }

    private static List<string> InferConstraints(string name)
    {
        return
        [
            "不得使用「作为一个 AI」「根据训练数据」等打破第四墙的表述。",
            "不得编造与原著无关的重大死亡、婚姻、背叛情节；无原文依据必须标注推测。",
            $"禁止混入与 {name} 原文气质不符的网络流行语；只有证据支持时才使用调侃。"
        ];
    }

    private static string BuildOneLiner(string name, List<QuoteEntry> quotes, List<string> mentions)
    {
        if (mentions.Count > 0)
            return $"{name}：{mentions[0][..Math.Min(60, mentions[0].Length)]}…（据上传文本归纳）";

        if (quotes.Count > 0)
            return $"{name}：已抽取 {quotes.Count} 条语气锚点，优先依据原文对白和第一人称反应。";

        return $"{name}：上传文本中直接对白较少，Skill 以旁白提及为准，建议补充更高相关章节。";
    }

    private static string BuildSampleReply(string name, List<QuoteEntry> quotes)
    {
        var seed = quotes.FirstOrDefault()?.Line ?? "我先确认风险，再决定下一步。";
        if (seed.Length > 36) seed = seed[..36] + "…";
        return $"{seed} 先别急着下结论，把能活下来的路找出来。";
    }

    private static string BuildSkillMarkdown(
        string name,
        string slug,
        string work,
        string range,
        string oneLiner,
        List<string> languageRules,
        List<string> constraints,
        List<QuoteEntry> quotes,
        string sampleReply)
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
        sb.AppendLine("> 本文件由 Character Persona Forge 根据上传文本自动生成，请人工核对。");
        sb.AppendLine();
        sb.AppendLine("## 说话风格（执行规则）");
        sb.AppendLine();
        for (var i = 0; i < languageRules.Count; i++)
            sb.AppendLine($"{i + 1}. {languageRules[i]}");
        sb.AppendLine();
        sb.AppendLine("## 价值观与动机");
        sb.AppendLine();
        sb.AppendLine("- [待核对] 根据下方原话与 evidence 文件补全信念、目标和底线。");
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
        {
            sb.AppendLine("1. （未抽到对白，请手工从原著补 8 条以上）");
        }
        else
        {
            for (var i = 0; i < quotes.Count; i++)
            {
                var q = quotes[i];
                sb.AppendLine($"{i + 1}. 「{q.Line}」-- {q.Scene}（{q.Chapter}）");
            }

            if (quotes.Count < 8)
                sb.AppendLine($"\n（原文台词仅 {quotes.Count} 条，不足 8 条）");
        }
        sb.AppendLine();
        sb.AppendLine("## 扮演模式");
        sb.AppendLine();
        sb.AppendLine("1. **沉浸**：全程第一人称，不跳出角色。");
        sb.AppendLine("2. **未知问题**：可说「书上没写到，但我会先按风险来判断……」。");
        sb.AppendLine("3. **长度**：日常 2-6 句，复杂局势可分点分析。");
        sb.AppendLine();
        sb.AppendLine("## 关键约束");
        sb.AppendLine();
        sb.AppendLine("**MUST**");
        sb.AppendLine();
        sb.AppendLine("- 语气与上表原话锚点一致。");
        sb.AppendLine("- 对没有证据的设定保持保守。");
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

    private static string BuildEvidenceMarkdown(
        string name,
        string work,
        string range,
        List<QuoteEntry> quotes,
        List<string> mentions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {name} · 原文证据库");
        sb.AppendLine();
        sb.AppendLine("## 作品与范围");
        sb.AppendLine();
        sb.AppendLine($"- 作品：《{work}》");
        sb.AppendLine($"- 分析范围：{range}");
        sb.AppendLine("- 生成方式：Persona Forge 自动抽取（请人工校对）");
        sb.AppendLine();
        sb.AppendLine("## 台词全集（自动抽取）");
        sb.AppendLine();
        sb.AppendLine("| # | 原文 | 说话对象 | 场景 | 章节 |");
        sb.AppendLine("|---|------|----------|------|------|");
        for (var i = 0; i < quotes.Count; i++)
        {
            var q = quotes[i];
            sb.AppendLine($"| {i + 1} | {EscapeTableCell(q.Line)} | {EscapeTableCell(q.Speaker)} | {EscapeTableCell(q.Scene)} | {EscapeTableCell(q.Chapter)} |");
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

    private static string EscapeTableCell(string text) => text.Replace("|", "\\|");

    private sealed record QuoteEntry(string Line, string Speaker, string Scene, string Chapter);
    private sealed record IndexedQuote(int Index, QuoteEntry Entry);
}
