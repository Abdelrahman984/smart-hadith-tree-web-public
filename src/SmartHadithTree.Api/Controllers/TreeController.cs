using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TreeController(IHadithSearchService searchService) : ControllerBase
{
    [HttpGet("{hadithId:guid}")]
    public async Task<ActionResult<IsnadTreeResponseDto>> GetTree(Guid hadithId, CancellationToken ct)
    {
        var tree = await searchService.GetIsnadTreeAsync(hadithId, ct);

        if (tree == null)
            return NotFound($"Hadith with ID {hadithId} not found.");

        return Ok(tree);
    }
}
