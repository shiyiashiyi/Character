/**
 * 002_seed_demo_user.sql
 * 插入与 Vue 登录页一致的演示账号（邮箱 demo@front.study）
 *
 * 注意：PasswordHash 为占位符。正式环境应由 .NET 的 PasswordHasher 写入哈希。
 * 执行本脚本后，请通过后续 Register API 设置密码，或运行 003 说明中的方式更新哈希。
 */

USE CharacterSkills;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'demo@front.study')
BEGIN
    INSERT INTO dbo.Users (Email, PasswordHash, DisplayName, IsActive)
    VALUES (
        N'demo@front.study',
        N'__REPLACE_VIA_DOTNET_PASSWORD_HASHER__',
        N'Demo',
        1
    );
    PRINT N'已插入演示用户 demo@front.study（密码哈希待 .NET 写入）。';
END
ELSE
BEGIN
    PRINT N'演示用户已存在，跳过插入。';
END
GO
