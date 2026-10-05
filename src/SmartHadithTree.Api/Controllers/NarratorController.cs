using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/narrators")]
public class NarratorController(INarratorService narratorService) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<List<NarratorSearchResultDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest("Search query cannot be empty.");

        var results = await narratorService.SearchNarratorsAsync(q, ct);
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NarratorDetailDto>> GetNarrator(Guid id, CancellationToken ct)
    {
        var narrator = await narratorService.GetNarratorDetailsAsync(id, ct);
        if (narrator == null)
            return NotFound();

        return Ok(narrator);
    }

    [HttpGet("{id:guid}/tooltip")]
    public async Task<ActionResult<NarratorSummaryDto>> GetNarratorTooltip(Guid id, CancellationToken ct)
    {
        var tooltip = await narratorService.GetNarratorTooltipAsync(id, ct);
        if (tooltip == null)
            return NotFound();

        return Ok(tooltip);
    }

    [HttpGet("{id:guid}/ai-summary")]
    public async Task<ActionResult<ExtractedAiEvaluationDto>> GetNarratorAiSummary(Guid id, [FromServices] IAiEvaluationService aiService, CancellationToken ct)
    {
        var narrator = await narratorService.GetNarratorDetailsAsync(id, ct);
        if (narrator == null)
            return NotFound();

        try
        {
            var summary = await aiService.GenerateNarratorEvaluationSummaryAsync(narrator, ct);
            return Ok(summary);
        }
        catch (Exception)
        {
            return StatusCode(500, "AI evaluation failed.");
        }
    }
}
