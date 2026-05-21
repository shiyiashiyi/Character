/**
 * EmailVerificationOptions.cs — 验证码有效期、防刷与尝试次数上限
 */
namespace FrontStudy.Api.Options;

public class EmailVerificationOptions
{
    public const string SectionName = "EmailVerification";

    public int CodeExpiryMinutes { get; set; } = 10;
    public int ResendCooldownSeconds { get; set; } = 60;
    public int MaxAttempts { get; set; } = 5;
}
