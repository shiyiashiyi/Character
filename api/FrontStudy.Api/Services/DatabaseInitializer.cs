/**
 * DatabaseInitializer.cs — 启动时幂等建表。
 *
 * dotnet-ef CLI 迁移在沙箱内因无法访问 nuget.org 暂不可用，这里用
 * 「IF NOT EXISTS」的幂等 DDL 在启动时确保 CharacterCards 表存在。
 * 在能访问 NuGet 的环境可换回 `dotnet ef migrations`（设计文档 M3）。
 */
using FrontStudy.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Services;

public class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializer> logger) : IHostedService
{
    private const string CreateCharacterCardsSql = """
        IF OBJECT_ID(N'dbo.CharacterCards', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.CharacterCards (
                Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CharacterCards PRIMARY KEY,
                Slug NVARCHAR(80) NOT NULL,
                CharacterName NVARCHAR(100) NOT NULL,
                WorkTitle NVARCHAR(200) NOT NULL,
                CardJson NVARCHAR(MAX) NOT NULL,
                SkillMarkdown NVARCHAR(MAX) NOT NULL,
                EvidenceMarkdown NVARCHAR(MAX) NOT NULL,
                CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CharacterCards_CreatedAtUtc DEFAULT SYSUTCDATETIME()
            );
            CREATE INDEX IX_CharacterCards_Slug ON dbo.CharacterCards(Slug);
        END
        IF OBJECT_ID(N'dbo.GenerationJobs', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.GenerationJobs (
                JobId NVARCHAR(32) NOT NULL CONSTRAINT PK_GenerationJobs PRIMARY KEY,
                Status NVARCHAR(16) NOT NULL,
                [Percent] INT NOT NULL,
                CurrentStageKey NVARCHAR(32) NOT NULL,
                Message NVARCHAR(MAX) NULL,
                ResultJson NVARCHAR(MAX) NULL,
                CreatedAtUtc DATETIME2 NOT NULL,
                CompletedAtUtc DATETIME2 NULL
            );
        END
        """;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.ExecuteSqlRawAsync(CreateCharacterCardsSql, cancellationToken);
            logger.LogInformation("数据库初始化完成：CharacterCards / GenerationJobs 表已就绪");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "数据库初始化失败（CharacterCards 建表）");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
