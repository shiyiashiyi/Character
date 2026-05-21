/**
 * SmtpOptions.cs — 163 等 SMTP 发信配置（密钥仅放本地 appsettings.Development.json）
 */
namespace FrontStudy.Api.Options;

public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.163.com";
    public int Port { get; set; } = 465;
    public bool UseSsl { get; set; } = true;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Character";
    /// <summary>为 true 时不发真实邮件，仅在日志输出验证码（本地调试）。</summary>
    public bool DryRun { get; set; }
}
