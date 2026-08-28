/**
 * ForgeJobStore.cs — 生成任务存储（内存 + 落库）。
 *
 * 内存态用于运行中的实时进度；同时落库（GenerationJobs）使任务在服务
 * 重启后仍可查询。重启后从 DB 恢复，未完成的任务标记为失败（避免前端无限等待）。
 */
using System.Collections.Concurrent;
using System.Text.Json;
using FrontStudy.Api.Data;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FrontStudy.Api.Services;

public class ForgeJobStore(IServiceScopeFactory scopeFactory, ILogger<ForgeJobStore> logger)
{
    private readonly ConcurrentDictionary<string, ForgeJob> _jobs = new();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    public ForgeJob Create()
    {
        var job = new ForgeJob { JobId = Guid.NewGuid().ToString("N") };
        _jobs[job.JobId] = job;
        PersistNew(job);
        return job;
    }

    public ForgeJob? Get(string jobId)
    {
        Cleanup();
        if (_jobs.TryGetValue(jobId, out var job)) return job;
        return LoadFromDb(jobId);
    }

    /// <summary>任务结束时持久化最终状态与结果。</summary>
    public void Complete(ForgeJob job) => Persist(job);

    private void Cleanup()
    {
        var cutoff = DateTime.UtcNow - Ttl;
        foreach (var kv in _jobs)
            if (kv.Value.CompletedAtUtc is { } completed && completed < cutoff)
                _jobs.TryRemove(kv.Key, out _);
    }

    private void PersistNew(ForgeJob job)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.GenerationJobs.Add(new GenerationJob
            {
                JobId = job.JobId,
                Status = job.Status.ToString(),
                Percent = job.Percent,
                CurrentStageKey = job.CurrentStage.Key,
                Message = job.Message,
                CreatedAtUtc = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "任务落库失败（不影响内存态运行）：{JobId}", job.JobId);
        }
    }

    private void Persist(ForgeJob job)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.GenerationJobs.Find(job.JobId);
            if (row is null)
            {
                row = new GenerationJob { JobId = job.JobId, CreatedAtUtc = job.CreatedAtUtc };
                db.GenerationJobs.Add(row);
            }

            row.Status = job.Status.ToString();
            row.Percent = job.Percent;
            row.CurrentStageKey = job.CurrentStage.Key;
            row.Message = job.Message;
            row.ResultJson = job.Result is null ? null : JsonSerializer.Serialize(job.Result);
            row.CompletedAtUtc = job.CompletedAtUtc;
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "任务状态落库失败：{JobId}", job.JobId);
        }
    }

    private ForgeJob? LoadFromDb(string jobId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = db.GenerationJobs.Find(jobId);
        if (row is null) return null;

        var status = Enum.TryParse<ForgeJobStatus>(row.Status, out var parsed) ? parsed : ForgeJobStatus.Failed;

        // 服务重启导致中断的未完成任务 → 标记失败，避免前端无限等待
        if (status is ForgeJobStatus.Queued or ForgeJobStatus.Running)
        {
            status = ForgeJobStatus.Failed;
            row.Status = status.ToString();
            row.Message = "任务因服务重启而中断，请重新提交";
            row.CompletedAtUtc = DateTime.UtcNow;
            db.SaveChanges();
        }

        var job = new ForgeJob
        {
            JobId = row.JobId,
            Status = status,
            Percent = row.Percent,
            CurrentStage = ForgeJob.Stages.FirstOrDefault(s => s.Key == row.CurrentStageKey)
                           ?? new ForgeStage(row.CurrentStageKey, row.CurrentStageKey, null),
            Message = row.Message,
            CreatedAtUtc = row.CreatedAtUtc,
            CompletedAtUtc = row.CompletedAtUtc,
        };

        if (!string.IsNullOrWhiteSpace(row.ResultJson))
        {
            try
            {
                job.Result = JsonSerializer.Deserialize<PersonaForgeResponse>(row.ResultJson);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "任务结果反序列化失败：{JobId}", jobId);
            }
        }

        _jobs[jobId] = job;
        return job;
    }
}
