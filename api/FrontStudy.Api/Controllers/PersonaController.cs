/**
 * PersonaController.cs — 上传小说文本并异步生成角色人格 Skill。
 *
 * 流程：POST /forge 提交任务（立即返回 jobId）→
 *       GET /forge/{jobId}        轮询进度/结果
 *       GET /forge/{jobId}/events SSE 实时进度
 * AI 未配置或失败时回退规则模式（在后台任务内处理）。
 */
using System.Text;
using System.Text.Json;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;
using FrontStudy.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonaController(
    ForgeJobStore jobs,
    ForgeJobRunner runner,
    ILogger<PersonaController> logger) : ControllerBase
{
    private static readonly string[] AllowedExt = [".txt", ".md", ".text"];

    [HttpPost("forge")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<PersonaForgeSubmitResponse>> Forge(
        [FromForm] string characterName,
        [FromForm] string? workTitle,
        [FromForm] string? chapterRange,
        [FromForm] string? mode,
        [FromForm] IFormFile? file,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return BadRequest(new PersonaForgeResponse(false, "请填写人物姓名", null, null, null, null, null, null, null));

        string text;
        if (file is { Length: > 0 })
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext))
                return BadRequest(new PersonaForgeResponse(false, "仅支持 .txt / .md 文本文件", null, null, null, null, null, null, null));

            text = await ReadTextFileAsync(file, ct);
        }
        else
        {
            return BadRequest(new PersonaForgeResponse(false, "请上传文本文件", null, null, null, null, null, null, null));
        }

        var job = runner.Start(text, characterName, workTitle, chapterRange, mode);
        logger.LogInformation("Persona 任务已提交：{JobId} mode={Mode}", job.JobId, mode);

        return Accepted(new PersonaForgeSubmitResponse(
            job.JobId,
            job.Status.ToString(),
            $"/api/persona/forge/{job.JobId}",
            $"/api/persona/forge/{job.JobId}/events"));
    }

    [HttpGet("forge/{jobId}")]
    public ActionResult<ForgeProgressEvent> GetJob(string jobId)
    {
        var job = jobs.Get(jobId);
        if (job is null) return NotFound();
        return Ok(ToEvent(job));
    }

    [HttpGet("forge/{jobId}/events")]
    public async Task ForgeEvents(string jobId, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        logger.LogInformation("SSE 订阅开始：{JobId}", jobId);
        await Response.Body.FlushAsync(ct);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var job = jobs.Get(jobId);
                if (job is null)
                {
                    // 任务不存在（后端重启或已过期）：发送错误事件后结束，避免前端无限等待
                    await WriteSseAsync(Response, new { error = "job_not_found", message = "生成任务不存在或已失效，请重新提交" }, ct);
                    logger.LogInformation("SSE 任务不存在：{JobId}", jobId);
                    break;
                }

                var evt = ToEvent(job);
                await WriteSseAsync(Response, evt, ct);
                if (evt.Done)
                {
                    logger.LogInformation("SSE 完成：{JobId} status={Status}", jobId, job.Status);
                    break;
                }

                await Task.Delay(500, ct);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SSE 客户端断开：{JobId}", jobId);
        }
    }

    private static ForgeProgressEvent ToEvent(ForgeJob job) =>
        new(
            job.JobId,
            job.Status.ToString(),
            job.Percent,
            job.CurrentStage,
            ForgeJob.Stages,
            job.Message,
            job.IsDone,
            job.Result);

    private static readonly JsonSerializerOptions SseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static async Task WriteSseAsync(HttpResponse response, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, SseJson);
        await response.WriteAsync($"data: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }

    private static async Task<string> ReadTextFileAsync(IFormFile file, CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes[3..]);

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Many Chinese web-novel text files are saved as GBK/GB18030.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }
}
