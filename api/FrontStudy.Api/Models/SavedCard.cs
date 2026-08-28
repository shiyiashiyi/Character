/**
 * SavedCard.cs — 已保存的角色卡（落库），映射 CharacterCards 表。
 *
 * 注意：与输出契约 CharacterCard（record，生成结果）不同，
 * 本实体是持久化的"已保存角色卡"。当前为单用户本地应用，暂不做用户归属；
 * 待 M1 认证补齐后再加 UserId 作用域。
 */
namespace FrontStudy.Api.Models;

public class SavedCard
{
    public long Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public string WorkTitle { get; set; } = string.Empty;
    public string CardJson { get; set; } = string.Empty;
    public string SkillMarkdown { get; set; } = string.Empty;
    public string EvidenceMarkdown { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
