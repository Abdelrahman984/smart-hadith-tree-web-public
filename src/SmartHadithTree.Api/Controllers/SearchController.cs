using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using Microsoft.AspNetCore.RateLimiting;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(IHadithSearchService searchService, ISearchJudgeService judgeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<HadithSearchResultDto>>> Search([FromQuery] SearchRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) && (request.Phrases == null || request.Phrases.Count == 0))
            return BadRequest("Search query cannot be empty.");

        var results = await searchService.SearchHadithsAsync(request, ct);
        return Ok(results);
    }

    [HttpPost("advanced")]
    public async Task<ActionResult<List<HadithSearchResultDto>>> AdvancedSearch([FromBody] SearchRequestDto request, CancellationToken ct)
    {
        var results = await searchService.SearchHadithsAsync(request, ct);
        return Ok(results);
    }

    /// <summary>
    /// Optional AI check of the results on the current page. The server loads the matn from the ids; the client
    /// sends no hadith text. At most 50 ids per request.
    /// </summary>
    [HttpPost("ai-judge")]
    [EnableRateLimiting("ai")]
    public async Task<ActionResult<SearchJudgeResponseDto>> JudgeResults([FromBody] SearchJudgeRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest("Query cannot be empty.");
        if (request.Ids == null || request.Ids.Count == 0)
            return BadRequest("At least one result id is required.");
        if (request.Ids.Count > SearchJudgeService.MaxIds)
            return BadRequest($"At most {SearchJudgeService.MaxIds} ids per request.");

        return Ok(await judgeService.JudgeAsync(request.Query, request.Ids, ct));
    }
}
