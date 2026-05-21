/**
 * 004_grant_api_windows_user.sql
 * 为【运行 dotnet run 的 Windows 账户】授予 CharacterSkills 权限
 *
 * 执行前：把 @WindowsLogin 改成你的登录名（whoami 或 /api/health/db 的 authHint）
 */

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'CharacterSkills')
    CREATE DATABASE CharacterSkills;
GO

USE master;
GO

DECLARE @WindowsLogin sysname = N'你的计算机名\你的用户名';
DECLARE @sql nvarchar(max);

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @WindowsLogin)
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@WindowsLogin) + N' FROM WINDOWS';
    EXEC sp_executesql @sql;
END
GO

USE CharacterSkills;
GO

DECLARE @WindowsLogin sysname = N'你的计算机名\你的用户名';
DECLARE @User sysname;
DECLARE @sql nvarchar(max);

SELECT @User = name FROM sys.database_principals WHERE sid = SUSER_SID(@WindowsLogin);

IF @User IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@WindowsLogin) + N' FOR LOGIN ' + QUOTENAME(@WindowsLogin);
    EXEC sp_executesql @sql;
    SET @User = @WindowsLogin;
END

IF ISNULL(IS_ROLEMEMBER(N'db_owner', @User), 0) <> 1
BEGIN
    SET @sql = N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@User);
    EXEC sp_executesql @sql;
END

PRINT N'Windows 授权完成，用户: ' + @User;
GO
