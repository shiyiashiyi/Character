/**
 * 006_fix_login_user_mapping.sql
 * 修复 15063 / 137：为 Windows 登录授权（单批执行变量段）
 *
 * 执行前：把 @Login 改成你的 Windows 登录名（whoami 输出）
 * SSMS：全选 → F5
 */

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'CharacterSkills')
    CREATE DATABASE CharacterSkills;
GO

USE master;
GO

DECLARE @Login sysname = N'你的计算机名\你的用户名';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = @Login)
BEGIN
    DECLARE @sqlLogin nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME(@Login) + N' FROM WINDOWS';
    EXEC sp_executesql @sqlLogin;
END
GO

USE CharacterSkills;

DECLARE @Login sysname = N'你的计算机名\你的用户名';
DECLARE @User  sysname;
DECLARE @sql   nvarchar(max);

SELECT @User = dp.name
FROM sys.database_principals AS dp
WHERE dp.sid = SUSER_SID(@Login);

IF @User IS NULL
BEGIN
    BEGIN TRY
        SET @sql = N'CREATE USER ' + QUOTENAME(@Login) + N' FOR LOGIN ' + QUOTENAME(@Login);
        EXEC sp_executesql @sql;
        SET @User = @Login;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 15063
            SELECT @User = dp.name FROM sys.database_principals AS dp WHERE dp.sid = SUSER_SID(@Login);
        ELSE
            THROW;
    END CATCH
END

IF @User IS NULL
    RAISERROR(N'未找到数据库用户，请检查 @Login 是否填写正确。', 16, 1);
ELSE IF ISNULL(IS_ROLEMEMBER(N'db_owner', @User), 0) <> 1
BEGIN
    SET @sql = N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@User);
    EXEC sp_executesql @sql;
    PRINT N'已授予 db_owner → ' + @User;
END
ELSE
    PRINT N'已是 db_owner: ' + @User;

SELECT dp.name AS DatabaseUser, sp.name AS ServerLogin, r.name AS DatabaseRole
FROM sys.database_principals AS dp
LEFT JOIN sys.server_principals AS sp ON dp.sid = sp.sid
LEFT JOIN sys.database_role_members AS rm ON dp.principal_id = rm.member_principal_id
LEFT JOIN sys.database_principals AS r ON rm.role_principal_id = r.principal_id
WHERE dp.sid = SUSER_SID(@Login);
GO
