/**
 * ForgeJobRunner.cs — 异步执行 Persona 生成任务（单例）。
 *
 * Start() 创建任务并后台执行，填充 ForgeJob 的状态/进度/结果。
 * AI 未配置或失败时回退规则模式（保持与同步版一致的降级策略）。
 */
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;

namespace FrontStudy.Api.Services;

public class ForgeJobRunner(
    ForgeJobStore store,
    PersonaForgeService forge,
    PersonaPipelineService pipeline,
    ILogger<ForgeJobRunner> logger)
{
    public ForgeJob Start(string text, string characterName, string? workTitle, string? chapterRange, string? mode)
    {
        var job = store.Create();
        _ = Task.Run(() => ProcessAsync(job, text, characterName, workTitle, chapterRange, mode));
        return job;
    }

    private async Task ProcessAsync(
        ForgeJob job,
        string text,
        string characterName,
        string? workTitle,
        string? chapterRange,
        string? mode)
    {
        try
        {
            job.Status = ForgeJobStatus.Running;
            PersonaForgeResponse result;

            if (string.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase))
            {
                var validation = pipeline.Validate();
                if (validation is null)
                {
                    result = await pipeline.ForgeAsync(
                        text, characterName, workTitle, chapterRange, job, CancellationToken.None);
                    if (!result.Success)
                    {
                        var rule = forge.Forge(text, characterName, workTitle, chapterRange);
                        result = rule with { Message = $"{rule.Message}（AI 生成失败，已回退规则模式）" };
                        logger.LogWarning("AI 流水线失败，回退规则模式：{Reason}", result.Message);
                    }
                }
                else
                {
                    var rule = forge.Forge(text, characterName, workTitle, chapterRange);
                    result = rule with { Message = $"{rule.Message}（AI 未配置，已回退规则模式）" };
                    logger.LogWarning("AI 未就绪，回退规则模式：{Reason}", validation);
                }
            }
            else
            {
                result = forge.Forge(text, characterName, workTitle, chapterRange);
            }

            job.Result = result;
            job.Status = ForgeJobStatus.Succeeded;
            job.Percent = 100;
            job.Message = result.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Persona 生成任务异常：{JobId}", job.JobId);
            job.Status = ForgeJobStatus.Failed;
            job.Percent = 100;
            job.Message = ex.Message;
            job.Result = new PersonaForgeResponse(false, ex.Message, null, null, null, null, null, null, null);
        }
        finally
        {
            job.CompletedAtUtc = DateTime.UtcNow;
            store.Complete(job);
        }
    }
}
