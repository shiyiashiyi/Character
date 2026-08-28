-- 008_create_character_cards.sql
-- 已保存的角色卡表（应用启动时也会幂等创建，见 DatabaseInitializer.cs）

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
