/**
 * LlmChatClient.cs — 通用 ChatCompletions 客户端（OpenAI-compatible）。
 *
 * 封装：配置校验、超时、简单重试、JSON 提取与反序列化。
 * 流水线各阶段（证据抽取 / 人格综合 / 语气样本）复用本客户端。
 */
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrontStudy.Api.Options;
using Microsoft.Extensions.Options;

namespace FrontStudy.Api.Services;

public class LlmChatClient(
    HttpClient http,
    IOptions<AiProviderOptions> options,
    ILogger<LlmChatClient> logger)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);
    private const int MaxAttempts = 3;
    private readonly AiProviderOptions _opts = options.Value;

    /// <summary>返回配置问题说明；为 null 表示已就绪。</summary>
    public string? Validate()
    {
        if (_opts.HasPlaceholderApiKey())
            return $"AI 未配置：请在 appsettings.Development.json -> {AiProviderOptions.SectionName}:ApiKey 填入 API Key。";
        if (string.IsNullOrWhiteSpace(_opts.EffectiveBaseUrl()))
            return $"AI 未配置：请设置 {AiProviderOptions.SectionName}:BaseUrl，或使用 Provider 'DeepSeek'/'OpenAI'。";
        if (string.IsNullOrWhiteSpace(_opts.EffectiveModel()))
            return $"AI 未配置：请设置 {AiProviderOptions.SectionName}:Model。";
        return null;
    }

    /// <summary>调用一次（带重试），返回原始 content 文本；失败返回 null。</summary>
    public async Task<string?> ChatAsync(string system, string user, CancellationToken ct = default)
    {
        var lastError = "未知错误";
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                return await CallOnceAsync(system, user, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                if (attempt < MaxAttempts)
                {
                    logger.LogWarning("LLM 调用失败（第 {Attempt}/{Max} 次）：{Error}", attempt, MaxAttempts, ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
                }
            }
        }

        logger.LogWarning("LLM 调用 {Max} 次均失败：{Error}", MaxAttempts, lastError);
        return null;
    }

    /// <summary>调用并反序列化为 T；失败返回 default。</summary>
    public async Task<T?> ChatJsonAsync<T>(string system, string user, CancellationToken ct = default)
    {
        var content = await ChatAsync(system, user, ct);
        if (string.IsNullOrWhiteSpace(content)) return default;

        var json = ExtractJsonObject(content);
        try
        {
            return JsonSerializer.Deserialize<T>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            logger.LogWarning("LLM 输出无法解析为 {Type}：{Error}。输出={Output}", typeof(T).Name, ex.Message, content);
            return default;
        }
    }

    private async Task<string?> CallOnceAsync(string system, string user, CancellationToken ct)
    {
        var baseUrl = _opts.EffectiveBaseUrl().TrimEnd('/');
        var model = _opts.EffectiveModel();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(Timeout);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opts.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = system },
                    new { role = "user", content = user },
                },
                temperature = _opts.Temperature,
                max_tokens = _opts.MaxOutputTokens,
                response_format = new { type = "json_object" },
                stream = false,
            }),
            Encoding.UTF8,
            "application/json");

        using var response = await http.SendAsync(request, cts.Token);
        var payload = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Provider 返回 {(int)response.StatusCode}：{Truncate(payload, 200)}");

        var content = ExtractChatContent(payload);
        if (string.IsNullOrWhiteSpace(content))
            throw new HttpRequestException("Provider 响应缺少 message.content。");
        return content;
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

    /// <summary>从模型输出中提取 JSON 对象：去代码围栏 + 截取首尾花括号。</summary>
    public static string ExtractJsonObject(string text)
    {
        var cleaned = Regex.Replace(text, @"```(?:json)?\s*", string.Empty).Trim('`', ' ', '\r', '\n');
        var start = cleaned.IndexOf('{');
        var end = cleaned.LastIndexOf('}');
        return start >= 0 && end > start ? cleaned[start..(end + 1)] : cleaned;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
