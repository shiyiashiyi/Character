/**
 * EmailSenderService.cs — 基于 MailKit 的 SMTP 发信（支持 DryRun）
 */
using FrontStudy.Api.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace FrontStudy.Api.Services;

public class EmailSenderService(
    IOptions<SmtpOptions> smtpOptions,
    IHostEnvironment env,
    ILogger<EmailSenderService> logger)
{
    private readonly SmtpOptions _smtp = smtpOptions.Value;

    public async Task SendVerificationCodeAsync(
        string toEmail,
        string code,
        CancellationToken ct = default)
    {
        var subject = "Character 注册验证码";
        var body = $"您的 Character 注册验证码为：{code}\n\n验证码 10 分钟内有效，请勿泄露给他人。";

        if (ShouldDryRun())
        {
            logger.LogWarning(
                "未真实发信（DryRun 或未配置授权码）。收件人={Email}，验证码={Code}",
                toEmail,
                code);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(
            _smtp.Host,
            _smtp.Port,
            _smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
            ct);
        await client.AuthenticateAsync(_smtp.User, _smtp.Password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        logger.LogInformation("已向 {Email} 发送注册验证码邮件", toEmail);
    }

    private bool ShouldDryRun() =>
        _smtp.DryRun
        || string.IsNullOrWhiteSpace(_smtp.Password)
        || (string.IsNullOrWhiteSpace(_smtp.User) && env.IsDevelopment());
}
