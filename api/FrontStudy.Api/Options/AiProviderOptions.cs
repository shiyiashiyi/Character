/**
 * AiProviderOptions.cs - Configurable AI provider settings.
 *
 * Provider presets let local development switch between DeepSeek, OpenAI, and
 * other OpenAI-compatible vendors by editing configuration instead of code.
 */
namespace FrontStudy.Api.Options;

public class AiProviderOptions
{
    public const string SectionName = "AiProvider";

    public string Provider { get; set; } = "DeepSeek";
    public string ApiKind { get; set; } = "ChatCompletions";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public int MaxOutputTokens { get; set; } = 8000;
    public double Temperature { get; set; } = 0.3;

    public string EffectiveBaseUrl() =>
        !string.IsNullOrWhiteSpace(BaseUrl)
            ? BaseUrl
            : Provider.Trim().ToLowerInvariant() switch
            {
                "openai" => "https://api.openai.com/v1",
                "deepseek" => "https://api.deepseek.com",
                _ => string.Empty,
            };

    public string EffectiveModel() =>
        !string.IsNullOrWhiteSpace(Model)
            ? Model
            : Provider.Trim().ToLowerInvariant() switch
            {
                "deepseek" => "deepseek-v4-flash",
                _ => string.Empty,
            };

    public bool HasPlaceholderApiKey() =>
        string.IsNullOrWhiteSpace(ApiKey)
        || ApiKey.Contains("API_KEY_HERE", StringComparison.OrdinalIgnoreCase)
        || ApiKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);
}
