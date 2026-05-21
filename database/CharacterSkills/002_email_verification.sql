/**
 * 002_email_verification.sql
 * 数据库：CharacterSkills
 * 说明：邮箱注册验证码表；Users 增加 EmailConfirmed（verify-first 注册即已确认）
 *
 * 在 DBeaver / SSMS 中连接 SQL Server 后执行本脚本（可重复执行）
 */

USE CharacterSkills;
GO

IF OBJECT_ID(N'dbo.EmailVerificationCodes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailVerificationCodes
    (
        Id              BIGINT          NOT NULL IDENTITY(1, 1),
        Email           NVARCHAR(256)   NOT NULL,
        CodeHash        NVARCHAR(128)   NOT NULL,
        ExpiresAtUtc    DATETIME2(7)    NOT NULL,
        CreatedAtUtc    DATETIME2(7)    NOT NULL
            CONSTRAINT DF_EmailVerificationCodes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        AttemptCount    INT             NOT NULL
            CONSTRAINT DF_EmailVerificationCodes_AttemptCount DEFAULT (0),
        IsConsumed      BIT             NOT NULL
            CONSTRAINT DF_EmailVerificationCodes_IsConsumed DEFAULT (0),

        CONSTRAINT PK_EmailVerificationCodes PRIMARY KEY CLUSTERED (Id)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_EmailVerificationCodes_Email_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'dbo.EmailVerificationCodes')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_EmailVerificationCodes_Email_CreatedAtUtc
        ON dbo.EmailVerificationCodes (Email, CreatedAtUtc DESC)
        INCLUDE (CodeHash, ExpiresAtUtc, AttemptCount, IsConsumed);
END
GO

IF COL_LENGTH(N'dbo.Users', N'EmailConfirmed') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD EmailConfirmed BIT NOT NULL
            CONSTRAINT DF_Users_EmailConfirmed DEFAULT (1);
END
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'邮箱注册验证码（仅存哈希，一次性消费）',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'EmailVerificationCodes';
GO

PRINT N'EmailVerificationCodes 表与 Users.EmailConfirmed 已就绪。';
GO
