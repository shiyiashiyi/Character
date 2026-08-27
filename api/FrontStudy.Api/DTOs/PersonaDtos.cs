/**
 * PersonaDtos.cs — 人物 Skill 生成请求与响应
 */
namespace FrontStudy.Api.DTOs;

public record PersonaForgeResponse(
    bool Success,
    string? Message,
    string? CharacterName,
    string? Slug,
    string? WorkTitle,
    PersonaSummaryDto? Summary,
    string? SkillMarkdown,
    string? EvidenceMarkdown,
    string? CardJson);

public record PersonaSummaryDto(
    string OneLiner,
    int QuoteCount,
    IReadOnlyList<string> LanguageRules,
    IReadOnlyList<string> HardConstraints);
