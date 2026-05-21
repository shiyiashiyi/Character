/**
 * EmailVerificationService.cs — 生成/校验邮箱验证码，防刷与哈希存储
 */
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FrontStudy.Api.Data;
using FrontStudy.Api.Models;
using FrontStudy.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FrontStudy.Api.Services;

public partial class EmailVerificationService(
    AppDbContext db,
    EmailSenderService emailSender,
    IOptions<EmailVerificationOptions> options,
    ILogger<EmailVerificationService> logger)
{
    private static readonly Regex EmailRegex = EmailPattern();
    private readonly EmailVerificationOptions _opts = options.Value;

    public async Task<SendCodeResult> SendCodeAsync(string rawEmail, CancellationToken ct = default)
    {
        var email = NormalizeEmail(rawEmail);
        if (!EmailRegex.IsMatch(email))
            return SendCodeResult.Fail("请输入有效的邮箱地址");

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return SendCodeResult.Fail("该邮箱已被注册");

        var latest = await db.EmailVerificationCodes
            .Where(c => c.Email == email)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (latest is not null)
        {
            var elapsed = DateTime.UtcNow - latest.CreatedAtUtc;
            if (elapsed.TotalSeconds < _opts.ResendCooldownSeconds)
            {
                var wait = (int)Math.Ceiling(_opts.ResendCooldownSeconds - elapsed.TotalSeconds);
                return SendCodeResult.RateLimited($"请 {wait} 秒后再试");
            }
        }

        var code = GenerateCode();
        var record = new EmailVerificationCode
        {
            Email = email,
            CodeHash = HashCode(email, code),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_opts.CodeExpiryMinutes),
            CreatedAtUtc = DateTime.UtcNow,
            AttemptCount = 0,
            IsConsumed = false,
        };

        db.EmailVerificationCodes.Add(record);
        await db.SaveChangesAsync(ct);

        try
        {
            await emailSender.SendVerificationCodeAsync(email, code, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "发送验证码邮件失败：{Email}", email);
            return SendCodeResult.Fail("验证码发送失败，请稍后重试");
        }

        return SendCodeResult.Ok("验证码已发送，请查收邮件（可能在垃圾箱）");
    }

    public async Task<ValidateCodeResult> ValidateCodeAsync(
        string rawEmail,
        string code,
        CancellationToken ct = default)
    {
        var email = NormalizeEmail(rawEmail);
        var normalizedCode = code.Trim();

        if (!EmailRegex.IsMatch(email) || normalizedCode.Length != 6 || !normalizedCode.All(char.IsDigit))
            return ValidateCodeResult.Invalid("验证码错误");

        var record = await db.EmailVerificationCodes
            .Where(c => c.Email == email && !c.IsConsumed)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (record is null)
            return ValidateCodeResult.Invalid("验证码错误或已过期");

        if (record.ExpiresAtUtc < DateTime.UtcNow)
            return ValidateCodeResult.Invalid("验证码已过期");

        if (record.AttemptCount >= _opts.MaxAttempts)
            return ValidateCodeResult.Invalid("验证码尝试次数过多，请重新获取");

        var expectedHash = HashCode(email, normalizedCode);
        if (!FixedTimeEquals(record.CodeHash, expectedHash))
        {
            record.AttemptCount++;
            await db.SaveChangesAsync(ct);
            return ValidateCodeResult.Invalid("验证码错误");
        }

        record.IsConsumed = true;
        await db.SaveChangesAsync(ct);
        return ValidateCodeResult.Valid();
    }

    private static string NormalizeEmail(string rawEmail) =>
        rawEmail.Trim().ToLowerInvariant();

    private static string GenerateCode()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    private static string HashCode(string email, string code)
    {
        var payload = $"{email}:{code}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b));

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailPattern();
}

public readonly record struct SendCodeResult(bool Success, string Message, bool IsRateLimited = false)
{
    public static SendCodeResult Ok(string message) => new(true, message);
    public static SendCodeResult Fail(string message) => new(false, message);
    public static SendCodeResult RateLimited(string message) => new(false, message, true);
}

public readonly record struct ValidateCodeResult(bool Success, string Message)
{
    public static ValidateCodeResult Valid() => new(true, string.Empty);
    public static ValidateCodeResult Invalid(string message) => new(false, message);
}
