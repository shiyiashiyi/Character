/**
 * EmailVerificationCode.cs — 邮箱注册验证码记录，映射 dbo.EmailVerificationCodes
 */
namespace FrontStudy.Api.Models;

public class EmailVerificationCode
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public bool IsConsumed { get; set; }
}
