/**
 * 007_create_sql_login.sql
 * 创建供 API 使用的 SQL 登录（用户名 + 密码），替代 Windows 身份验证
 *
 * 前置：SQL Server 已启用【混合模式】身份验证
 *   SSMS → 右键服务器 → 属性 → 安全性 → SQL Server 和 Windows 身份验证模式 → 重启 SQL 服务
 *
 * 执行后，把密码填入 Character/api/FrontStudy.Api/appsettings.Development.json
 */

USE master;
GO

-- 按需修改登录名与密码（本地开发用，勿用于生产）
DECLARE @LoginName sysname = N'frontstudy_app';
DECLARE @Password  nvarchar(128) = N'FrontStudy@2026';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @LoginName)
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@LoginName)
        + N' WITH PASSWORD = ''' + REPLACE(@Password, '''', '''''') + N''', '
        + N'DEFAULT_DATABASE = CharacterSkills, CHECK_POLICY = OFF;';
    EXEC sp_executesql @sql;
    PRINT N'已创建服务器登录: ' + @LoginName;
END
ELSE
    PRINT N'登录已存在: ' + @LoginName;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'CharacterSkills')
    CREATE DATABASE CharacterSkills;
GO

USE CharacterSkills;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'frontstudy_app')
    CREATE USER [frontstudy_app] FOR LOGIN [frontstudy_app];
GO

ALTER ROLE db_owner ADD MEMBER [frontstudy_app];
GO

PRINT N'frontstudy_app 已拥有 CharacterSkills 的 db_owner 权限。';
GO
