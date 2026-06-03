/**
 * AiPersonaRefinementService.cs - Refines rule-based persona drafts with a
 * configurable AI provider.
 */
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Options;
using Microsoft.Extensions.Options;

namespace FrontStudy.Api.Services;

public class AiPersonaRefinementService(
    HttpClient http,
    IOptions<AiProviderOptions> options,
    ILogger<AiPersonaRefinementService> logger)
{
    private readonly AiProviderOptions _opts = options.Value;

    public async Task<PersonaForgeResponse> RefineAsync(
        PersonaForgeResponse draft,
        CancellationToken ct = default)
    {
        var validation = ValidateOptions();
        if (validation is not null)
            return draft with { Success = false, Message = validation };

        if (string.IsNullOrWhiteSpace(draft.SkillMarkdown)
            || string.IsNullOrWhiteSpace(draft.EvidenceMarkdown))
        {
            return draft with
            {
                Success = false,
                Message = "AI refinement failed: rule draft or evidence is empty."
            };
        }

        var result = _opts.ApiKind.Trim().ToLowerInvariant() switch
        {
            "chatcompletions" or "chat-completions" or "openai-compatible" =>
                await RefineWithChatCompletionsAsync(draft, ct),
            _ => AiCallResult.Fail(
                $"Unsupported AiProvider:ApiKind '{_opts.ApiKind}'. Use 'ChatCompletions'.")
        };

        if (!result.Success)
            return draft with { Success = false, Message = result.Message };

        var refined = ParseRefinement(result.OutputText);
        if (refined is null)
        {
            logger.LogWarning(
                "{Provider} refinement output could not be parsed. Output={Output}",
                _opts.Provider,
                result.OutputText);
            return draft with
            {
                Success = false,
                Message = "AI refinement failed: model output was not valid JSON."
            };
        }

        return draft with
        {
            Message = $"AI refinement completed ({_opts.Provider})",
            Summary = draft.Summary is null
                ? null
                : draft.Summary with { OneLiner = refined.Summary },
            SkillMarkdown = refined.SkillMarkdown,
            EvidenceMarkdown = refined.EvidenceMarkdown,
        };
    }

    private string? ValidateOptions()
    {
        if (_opts.HasPlaceholderApiKey())
            return $"AI refinement is not configured: paste your {_opts.Provider} API Key into appsettings.Development.json -> {AiProviderOptions.SectionName}:ApiKey.";

        if (string.IsNullOrWhiteSpace(_opts.EffectiveBaseUrl()))
            return $"AI refinement is not configured: set {AiProviderOptions.SectionName}:BaseUrl or use Provider 'DeepSeek'/'OpenAI'.";

        if (string.IsNullOrWhiteSpace(_opts.EffectiveModel()))
            return $"AI refinement is not configured: set {AiProviderOptions.SectionName}:Model.";

        return null;
    }

    private async Task<AiCallResult> RefineWithChatCompletionsAsync(
        PersonaForgeResponse draft,
        CancellationToken ct)
    {
        var baseUrl = _opts.EffectiveBaseUrl().TrimEnd('/');
        var model = _opts.EffectiveModel();
        var prompt = BuildPrompt(draft);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You refine character persona skill files. Return only valid JSON."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    },
                },
                temperature = _opts.Temperature,
                max_tokens = _opts.MaxOutputTokens,
                response_format = new { type = "json_object" },
                stream = false,
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "{Provider} chat completions call failed. Status={Status}, Body={Body}",
                _opts.Provider,
                (int)response.StatusCode,
                payload);
            return AiCallResult.Fail(
                $"AI refinement failed: {_opts.Provider} API returned {(int)response.StatusCode}.");
        }

        var outputText = ExtractChatContent(payload);
        return string.IsNullOrWhiteSpace(outputText)
            ? AiCallResult.Fail("AI refinement failed: provider response did not contain message content.")
            : AiCallResult.Ok(outputText);
    }

    private static string BuildPrompt(PersonaForgeResponse draft)
    {
        var summary = draft.Summary is null
            ? string.Empty
            : JsonSerializer.Serialize(draft.Summary);

        return $$"""
        Refine the rule-generated character persona into higher-quality Chinese Markdown.

        Hard rules:
        - Use only the provided draft and evidence.
        - Do not invent major plot facts, relationships, deaths, marriages, betrayals, or identity settings.
        - If evidence is insufficient, explicitly write "证据不足，需要人工补充".
        - Original quotes must come only from the evidence file below.
        - Return only one JSON object with these exact fields:
          {
            "summary": "one-sentence Chinese summary",
            "skillMarkdown": "complete SKILL.md content",
            "evidenceMarkdown": "complete source-evidence.md content"
          }

        The skillMarkdown must include:
        - 身份摘要
        - 说话风格
        - 价值观与动机
        - 关键经历
        - 代表性原话
        - 扮演模式
        - 关键约束
        - 示例对话

        Character: {{draft.CharacterName}}
        Work: {{draft.WorkTitle}}
        Slug: {{draft.Slug}}
        Rule summary JSON: {{summary}}

        # Rule Draft
        {{draft.SkillMarkdown}}

        # Evidence
        {{draft.EvidenceMarkdown}}
        """;
    }

    private static string ExtractChatContent(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (root.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static RefinementResult? ParseRefinement(string output)
    {
        var json = ExtractJsonObject(output);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<RefinementResult>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private readonly record struct AiCallResult(
        bool Success,
        string OutputText,
        string Message)
    {
        public static AiCallResult Ok(string outputText) => new(true, outputText, string.Empty);
        public static AiCallResult Fail(string message) => new(false, string.Empty, message);
    }

    private sealed record RefinementResult(
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("skillMarkdown")] string SkillMarkdown,
        [property: JsonPropertyName("evidenceMarkdown")] string EvidenceMarkdown);
}
