/**
 * EmailVerificationCleanupService.cs — 定期清理过期/已消费的验证码，防止表无限膨胀。
 */
using FrontStudy.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Services;

public class EmailVerificationCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<EmailVerificationCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "验证码清理失败");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 过期超 1 天，或已消费超 1 小时 的记录
        var deleted = await db.EmailVerificationCodes
            .Where(c => c.ExpiresAtUtc < DateTime.UtcNow.AddDays(-1)
                     || (c.IsConsumed && c.CreatedAtUtc < DateTime.UtcNow.AddHours(-1)))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
            logger.LogInformation("已清理 {Count} 条过期验证码", deleted);
    }
}
