using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

/// <summary>
/// علل الحديث — hidden-defect analysis across the turuq of a hadith.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class IlalController(
    IIlalAnalysisService ilalService,
    IHadithSearchService searchService,
    IIlalExplanationService explanationService) : ControllerBase
{
    /// <summary>Upper bound on turuq analyzed in one request.</summary>
    private const int MaxHadiths = 30;

    /// <summary>
    /// Analyzes the given hadiths as turuq of one narration.
    /// </summary>
    /// <param name="ids">Comma-separated Hadith GUIDs.</param>
    [HttpGet]
    public async Task<IActionResult> Analyze([FromQuery] string ids, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest("At least one Hadith ID is required.");

        var hadithIds = ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(MaxHadiths)
            .ToList();

        if (hadithIds.Count == 0)
            return BadRequest("No valid Hadith IDs provided.");

        return Ok(await ilalService.AnalyzeAsync(hadithIds, ct));
    }

    /// <summary>
    /// Gathers the turuq of a hadith automatically (by matching its matn across books) and analyzes them.
    /// </summary>
    [HttpGet("{hadithId:guid}")]
    public async Task<IActionResult> AnalyzeHadith(Guid hadithId, CancellationToken ct)
    {
        var related = await searchService.FindRelatedHadithsAsync(hadithId, ct);
        var hadithIds = related
            .Select(r => r.Id)
            .Prepend(hadithId)
            .Distinct()
            .Take(MaxHadiths)
            .ToList();

        return Ok(await ilalService.AnalyzeAsync(hadithIds, ct));
    }

    /// <summary>
    /// Asks the AI to explain an Ilal report in scholarly Arabic, grounded only in its findings.
    /// </summary>
    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] IlalReportDto report, CancellationToken ct)
    {
        return Ok(await explanationService.ExplainAsync(report, ct));
    }
}
