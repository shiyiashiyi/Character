/**
 * 001_create_users.sql
 * 数据库：CharacterSkills
 * 说明：创建登录/注册用的用户表（与 Vue 登录页、后续 .NET Core API 对齐）
 *
 * 在 DBeaver 中：连接你的 SQL Server 实例 → 打开 SQL 编辑器 → 执行本脚本
 */

USE CharacterSkills;
GO

-- 若重复执行，先删除旧表（仅开发环境；生产勿随意 DROP）
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Users;
END
GO

CREATE TABLE dbo.Users
(
    UserId          BIGINT          NOT NULL IDENTITY(1, 1),
    Email           NVARCHAR(256)   NOT NULL,
    PasswordHash    NVARCHAR(512)   NOT NULL,
    DisplayName     NVARCHAR(100)   NULL,
    IsActive        BIT             NOT NULL
        CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAtUtc    DATETIME2(7)    NOT NULL
        CONSTRAINT DF_Users_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc    DATETIME2(7)    NULL,
    LastLoginAtUtc  DATETIME2(7)    NULL,

    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

-- 登录时按邮箱查询
CREATE NONCLUSTERED INDEX IX_Users_Email
    ON dbo.Users (Email)
    WHERE IsActive = 1;
GO

EXEC sys.sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'用户表：邮箱登录，密码仅存哈希',
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'TABLE',  @level1name = N'Users';
GO

PRINT N'Users 表创建完成。';
GO
