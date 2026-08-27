/**
 * CharacterCard.cs — 结构化角色卡（输出契约，对齐 SillyTavern character card V3）。
 *
 * 权威格式为 JSON，SKILL.md / source-evidence.md 是其可读呈现。
 * personality / backstory / relationships 的每一项都挂 evidence，满足「可追溯」。
 */
namespace FrontStudy.Api.Models;

public sealed record CharacterCard(
    string Name,
    string CharacterName,
    WorkInfo Work,
    IdentityInfo Identity,
    List<TraitInfo> Personality,
    MotivationInfo Motivation,
    List<BackstoryEvent> Backstory,
    List<RelationshipInfo> Relationships,
    SpeechStyleInfo SpeechStyle,
    ConstraintsInfo Constraints,
    List<DialogueExample> Examples);

public sealed record WorkInfo(string Title, string Chapters);

public sealed record IdentityInfo(
    List<string> Aliases,
    string Role,
    string Appearance,
    string Summary);

/// <summary>性格特质 + 强度 + 证据原文（可追溯）。</summary>
public sealed record TraitInfo(string Trait, double Intensity, string Evidence);

public sealed record MotivationInfo(
    List<string> Goals,
    List<string> Values,
    List<string> BottomLines);

public sealed record BackstoryEvent(string Event, string Effect, string Evidence);

public sealed record RelationshipInfo(string Person, string Relation, string Evidence);

public sealed record SpeechStyleInfo(
    List<string> Catchphrases,
    string SentenceLength,
    string Register,
    string Notes);

public sealed record ConstraintsInfo(
    List<string> PriorityOrder,
    List<string> Must,
    List<string> MustNot);

public sealed record DialogueExample(string User, string Char);
