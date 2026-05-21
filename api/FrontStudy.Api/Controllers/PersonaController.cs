/**
 * PersonaController.cs — 上传小说文本并生成 persona skill 文件
 */
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonaController(PersonaForgeService forge) : ControllerBase
{
    private static readonly string[] AllowedExt = [".txt", ".md", ".text"];

    [HttpPost("forge")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<PersonaForgeResponse>> Forge(
        [FromForm] string characterName,
        [FromForm] string? workTitle,
        [FromForm] string? chapterRange,
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

            using var reader = new StreamReader(file.OpenReadStream());
            text = await reader.ReadToEndAsync(ct);
        }
        else
        {
            return BadRequest(new PersonaForgeResponse(false, "请上传文本文件", null, null, null, null, null, null));
        }

        var result = forge.Forge(text, characterName, workTitle, chapterRange);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
