/**
 * CardsController.cs — 已保存角色卡的增删查。
 */
using FrontStudy.Api.Data;
using FrontStudy.Api.DTOs;
using FrontStudy.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FrontStudy.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CardSummaryDto>>> List(CancellationToken ct)
    {
        var cards = await db.SavedCards
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new CardSummaryDto(c.Id, c.Slug, c.CharacterName, c.WorkTitle, c.CreatedAtUtc))
            .ToListAsync(ct);
        return Ok(cards);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CardDetailDto>> Get(long id, CancellationToken ct)
    {
        var card = await db.SavedCards.FindAsync([id], ct);
        if (card is null) return NotFound();
        return Ok(ToDetail(card));
    }

    [HttpPost]
    public async Task<ActionResult<CardDetailDto>> Save(SaveCardRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Slug) || string.IsNullOrWhiteSpace(request.CharacterName))
            return BadRequest(new { message = "Slug 与 CharacterName 不能为空" });

        var card = new SavedCard
        {
            Slug = request.Slug!.Trim(),
            CharacterName = request.CharacterName.Trim(),
            WorkTitle = request.WorkTitle ?? string.Empty,
            CardJson = request.CardJson ?? string.Empty,
            SkillMarkdown = request.SkillMarkdown ?? string.Empty,
            EvidenceMarkdown = request.EvidenceMarkdown ?? string.Empty,
        };
        db.SavedCards.Add(card);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = card.Id }, ToDetail(card));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var card = await db.SavedCards.FindAsync([id], ct);
        if (card is null) return NotFound();
        db.SavedCards.Remove(card);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static CardDetailDto ToDetail(SavedCard c) =>
        new(c.Id, c.Slug, c.CharacterName, c.WorkTitle, c.CardJson, c.SkillMarkdown, c.EvidenceMarkdown, c.CreatedAtUtc);
}
