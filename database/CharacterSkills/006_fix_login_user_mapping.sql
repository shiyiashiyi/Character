/**
 * 006_fix_login_user_mapping.sql
 * 修复 15063 / 137：单批次执行，不要用 GO 拆开（避免 @User 未声明）
 *
 * 在 SSMS：打开本文件 → 全选 → 执行（F5）
 */

-- ① 确保库存在（master）
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'CharacterSkills')
    CREATE DATABASE CharacterSkills;
GO

-- ② 确保服务器登录存在（master）
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'YEBBYHUANG-PC\77374')
    CREATE LOGIN [YEBBYHUANG-PC\77374] FROM WINDOWS;
GO

-- ③ 以下【整段一起执行】，中间不要有 GO
USE CharacterSkills;

DECLARE @Login sysname = N'YEBBYHUANG-PC\77374';
DECLARE @User  sysname;
DECLARE @sql   nvarchar(max);

-- 按 SID 查找该 Windows 登录在库内对应的用户名（可能不是同名）
SELECT @User = dp.name
FROM sys.database_principals AS dp
WHERE dp.sid = SUSER_SID(@Login);

IF @User IS NULL
BEGIN
    BEGIN TRY
        CREATE USER [YEBBYHUANG-PC\77374] FOR LOGIN [YEBBYHUANG-PC\77374];
        SET @User = N'YEBBYHUANG-PC\77374';
        PRINT N'已新建数据库用户: ' + @User;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 15063
        BEGIN
            -- 登录已映射到其他用户名，再查一次
            SELECT @User = dp.name
            FROM sys.database_principals AS dp
            WHERE dp.sid = SUSER_SID(@Login);

            IF @User IS NULL
                THROW 50001, N'存在映射冲突但未找到数据库用户，请联系管理员手动检查 sys.database_principals。', 1;
            PRINT N'登录已映射到现有用户: ' + @User;
        END
        ELSE
            THROW;
    END CATCH
END
ELSE
    PRINT N'库内已有用户: ' + @User;

IF ISNULL(IS_ROLEMEMBER(N'db_owner', @User), 0) <> 1
BEGIN
    SET @sql = N'ALTER ROLE db_owner ADD MEMBER ' + QUOTENAME(@User);
    EXEC sp_executesql @sql;
    PRINT N'已授予 db_owner → ' + @User;
END
ELSE
    PRINT N'已是 db_owner: ' + @User;

-- 自检
SELECT
    dp.name AS DatabaseUser,
    sp.name AS ServerLogin,
    r.name  AS DatabaseRole
FROM sys.database_principals AS dp
LEFT JOIN sys.server_principals AS sp ON dp.sid = sp.sid
LEFT JOIN sys.database_role_members AS rm ON dp.principal_id = rm.member_principal_id
LEFT JOIN sys.database_principals AS r ON rm.role_principal_id = r.principal_id
WHERE dp.sid = SUSER_SID(@Login);
GO
