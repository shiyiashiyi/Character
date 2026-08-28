/**
 * GenerationJob.cs — 生成任务落库实体（映射 GenerationJobs 表）。
 *
 * 让任务在服务重启后仍可查询；ResultJson 存序列化后的 PersonaForgeResponse。
 */
namespace FrontStudy.Api.Models;

public class GenerationJob
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = "Queued";
    public int Percent { get; set; }
    public string CurrentStageKey { get; set; } = "preprocess";
    public string? Message { get; set; }
    public string? ResultJson { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
