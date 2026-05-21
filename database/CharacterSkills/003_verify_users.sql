/**
 * 003_verify_users.sql — 建表后自检
 */
USE CharacterSkills;
GO

-- 表结构
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS AS c
WHERE c.TABLE_SCHEMA = N'dbo'
  AND c.TABLE_NAME = N'Users'
ORDER BY c.ORDINAL_POSITION;
GO

-- 当前数据
SELECT
    UserId,
    Email,
    DisplayName,
    IsActive,
    CreatedAtUtc,
    LastLoginAtUtc,
    LEFT(PasswordHash, 20) + N'...' AS PasswordHashPreview
FROM dbo.Users;
GO
