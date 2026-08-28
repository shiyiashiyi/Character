/**
 * CardDtos.cs — 已保存角色卡的请求/响应。
 */
namespace FrontStudy.Api.DTOs;

public record SaveCardRequest(
    string? Slug,
    string? CharacterName,
    string? WorkTitle,
    string? CardJson,
    string? SkillMarkdown,
    string? EvidenceMarkdown);

public record CardSummaryDto(long Id, string Slug, string CharacterName, string WorkTitle, DateTime CreatedAtUtc);

public record CardDetailDto(
    long Id,
    string Slug,
    string CharacterName,
    string WorkTitle,
    string CardJson,
    string SkillMarkdown,
    string EvidenceMarkdown,
    DateTime CreatedAtUtc);
