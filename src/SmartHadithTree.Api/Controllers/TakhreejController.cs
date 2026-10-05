using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TakhreejController(IHadithSearchService searchService) : ControllerBase
{
    /// <summary>
    /// Returns a merged comparative Isnad tree for multiple Hadiths across collections.
    /// </summary>
    /// <param name="ids">Comma-separated Hadith GUIDs to merge.</param>
    [HttpGet]
    public async Task<IActionResult> GetComparativeTree(
        [FromQuery] string ids, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ids))
            return BadRequest("At least one Hadith ID is required.");

        var hadithIds = new List<Guid>();
        foreach (var idStr in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(idStr, out var id))
                hadithIds.Add(id);
        }

        if (hadithIds.Count == 0)
            return BadRequest("No valid Hadith IDs provided.");

        var result = await searchService.GetComparativeTreeAsync(hadithIds, ct);

        if (result == null)
            return NotFound("No Hadiths found for the provided IDs.");

        return Ok(result);
    }

    /// <summary>
    /// Finds related Hadiths across all books for auto-Takhreej by matching Matn text.
    /// </summary>
    [HttpGet("related/{hadithId:guid}")]
    public async Task<IActionResult> GetRelatedHadiths(Guid hadithId, CancellationToken ct)
    {
        var results = await searchService.FindRelatedHadithsAsync(hadithId, ct);
        return Ok(results);
    }
}
