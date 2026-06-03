/**
 * PersonaController.cs — 上传小说文本并生成 persona skill 文件
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
    AiPersonaRefinementService aiRefinement) : ControllerBase
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
            return BadRequest(new PersonaForgeResponse(false, "请填写人物姓名", null, null, null, null, null, null));

        string text;
        if (file is { Length: > 0 })
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExt.Contains(ext))
                return BadRequest(new PersonaForgeResponse(false, "仅支持 .txt / .md 文本文件", null, null, null, null, null, null));

            text = await ReadTextFileAsync(file, ct);
        }
        else
        {
            return BadRequest(new PersonaForgeResponse(false, "请上传文本文件", null, null, null, null, null, null));
        }

        var result = forge.Forge(text, characterName, workTitle, chapterRange);
        if (!result.Success)
            return BadRequest(result);

        if (string.Equals(mode, "ai", StringComparison.OrdinalIgnoreCase))
        {
            result = await aiRefinement.RefineAsync(result, ct);
            if (!result.Success)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        return Ok(result);
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
            // Registering the provider lets .NET decode those legacy code pages.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }
}
