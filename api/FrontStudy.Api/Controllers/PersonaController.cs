/**
 * PersonaController.cs — 上传小说文本并生成角色人格 Skill。
 *
 * mode=rule：规则抽取（证据索引，无 LLM，作为回退 / 无 Key 场景）。
 * mode=ai  ：多阶段 LLM 流水线（主路径），失败或未配置时回退到规则模式。
 */
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonaController(
    PersonaForgeService forge,
    PersonaPipelineService pipeline,
    ILogger<PersonaController> logger) : ControllerBase
{
    private static readonly string[] AllowedExt = [".txt", ".md", ".text"];

    [HttpPost("forge")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<PersonaForgeResponse>> Forge(
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

        if (string.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(await ForgeWithAiAsync(text, characterName, workTitle, chapterRange, ct));
        }

        return Ok(forge.Forge(text, characterName, workTitle, chapterRange));
    }

    private async Task<PersonaForgeResponse> ForgeWithAiAsync(
        string text, string characterName, string? workTitle, string? chapterRange, CancellationToken ct)
    {
        var validation = pipeline.Validate();
        if (validation is not null)
        {
            logger.LogWarning("AI 流水线未就绪，回退规则模式：{Reason}", validation);
            var fallback = forge.Forge(text, characterName, workTitle, chapterRange);
            return fallback with { Message = $"{fallback.Message}（AI 未配置，已回退规则模式）" };
        }

        var result = await pipeline.ForgeAsync(text, characterName, workTitle, chapterRange, ct);
        if (result.Success)
            return result;

        logger.LogWarning("AI 流水线失败，回退规则模式：{Reason}", result.Message);
        var rule = forge.Forge(text, characterName, workTitle, chapterRange);
        return rule with { Message = $"{rule.Message}（AI 生成失败，已回退规则模式）" };
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
