using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IGawamiImporterService _importerService;

    public AdminController(IGawamiImporterService importerService)
    {
        _importerService = importerService;
    }

    [HttpPost("import-gawami")]
    public async Task<IActionResult> ImportGawami(CancellationToken cancellationToken)
    {
        var dataDir = @"D:\Islamic\حديث\GK4.5\برنامج جوامع الكلم\Data";
        await _importerService.ImportNarratorsAsync(dataDir, cancellationToken);
        return Ok(new { Message = "Gawami Import Completed" });
    }

    [HttpPost("import-gawami-evaluations")]
    public async Task<IActionResult> ImportEvaluations(CancellationToken cancellationToken)
    {
        var dataDir = @"D:\Islamic\حديث\GK4.5\برنامج جوامع الكلم\Data";
        await _importerService.ImportScholarEvaluationsAsync(dataDir, cancellationToken);
        return Ok(new { Message = "Evaluations Import Completed" });
    }
}
