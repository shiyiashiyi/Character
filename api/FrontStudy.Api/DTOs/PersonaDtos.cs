/**
 * PersonaDtos.cs — 人物 Skill 生成请求与响应
 */
using FrontStudy.Api.Models;

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

/// <summary>异步提交的响应：任务已入队，前端据此轮询或订阅 SSE。</summary>
public record PersonaForgeSubmitResponse(
    string JobId,
    string Status,
    string StatusUrl,
    string EventsUrl);

/// <summary>任务进度快照（轮询与 SSE 共用）。</summary>
public record ForgeProgressEvent(
    string JobId,
    string Status,
    int Percent,
    ForgeStage CurrentStage,
    IReadOnlyList<ForgeStage> Stages,
    string? Message,
    bool Done,
    PersonaForgeResponse? Result);
