/**
 * ForgeJob.cs — 异步生成任务模型（M4：真实进度）。
 *
 * 内存态任务：POST /api/persona/forge 创建后由后台任务填充，
 * GET /{jobId}（轮询）与 /{jobId}/events（SSE）读取。
 */
using FrontStudy.Api.DTOs;

namespace FrontStudy.Api.Models;

public enum ForgeJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
}

public sealed record ForgeStage(string Key, string Title, string? Description);

public sealed class ForgeJob
{
    public required string JobId { get; init; }
    public ForgeJobStatus Status { get; set; } = ForgeJobStatus.Queued;
    public ForgeStage CurrentStage { get; set; } = new("preprocess", "准备中", null);
    public int Percent { get; set; }
    public string? Message { get; set; }
    public PersonaForgeResponse? Result { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    public bool IsDone => Status is ForgeJobStatus.Succeeded or ForgeJobStatus.Failed;

    /// <summary>全量阶段清单（rule / ai 共用），供前端渲染步骤列表。</summary>
    public static readonly IReadOnlyList<ForgeStage> Stages =
    [
        new("preprocess", "读取上传文本", "检查文件并准备分块"),
        new("upload", "上传到生成服务", "提交生成任务"),
        new("evidence", "抽取台词与证据", "定位角色相关对话与旁白"),
        new("synthesize", "综合人格", "归纳性格、动机、价值观与关系网"),
        new("examples", "生成示例对话", "模仿原话语气生成角色回复样本"),
        new("assemble", "整理生成结果", "组装角色卡与证据文件"),
    ];
}
