/**
 * 004_grant_api_windows_user.sql
 * 修复：dotnet run 使用的 Windows 账户无法打开 CharacterSkills
 *
 * 在 SSMS 或 DBeaver 中用【有管理员权限的账户】连接实例后执行
 * （例如你平时能打开 SSMS 的那个 Windows 登录）
 *
 * 若 API 报错里的用户名与下面不一致，把脚本中的登录名改成报错中的完整名称
 */

-- 1. 确保数据库存在
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'CharacterSkills')
BEGIN
    CREATE DATABASE CharacterSkills;
    PRINT N'已创建数据库 CharacterSkills';
END
ELSE
    PRINT N'数据库 CharacterSkills 已存在';
GO

USE CharacterSkills;
GO

-- 2. 为运行 API 的 Windows 账户创建服务器登录（若尚未存在）
IF NOT EXISTS (
    SELECT 1 FROM sys.server_principals
    WHERE name = N'MicrosoftAccount\773749724@qq.com'
)
BEGIN
    CREATE LOGIN [MicrosoftAccount\773749724@qq.com] FROM WINDOWS;
    PRINT N'已创建服务器登录 MicrosoftAccount\773749724@qq.com';
END
ELSE
    PRINT N'服务器登录已存在';
GO

USE CharacterSkills;
GO

-- 3. 映射数据库用户并授权
IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name = N'MicrosoftAccount\773749724@qq.com'
)
BEGIN
    CREATE USER [MicrosoftAccount\773749724@qq.com]
        FOR LOGIN [MicrosoftAccount\773749724@qq.com];
    PRINT N'已创建数据库用户';
END
GO

ALTER ROLE db_owner ADD MEMBER [MicrosoftAccount\773749724@qq.com];
GO

PRINT N'已为 API 运行账户授予 CharacterSkills 的 db_owner 权限。';
GO
