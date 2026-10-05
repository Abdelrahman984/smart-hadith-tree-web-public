using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;

namespace SmartHadithTree.Api.Controllers;

/// <summary>
/// التحقق من نص حديث: هل هو في الكتب المتاحة؟ لا يصدر النظام حكماً على الحديث ولا ينسب نصاً لمصدر لا يوجد فيه.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VerifyController(IHadithVerificationService verificationService) : ControllerBase
{
    /// <summary>Checks a pasted text against the corpus.</summary>
    [HttpPost]
    public async Task<ActionResult<HadithVerificationResultDto>> Verify([FromBody] VerifyHadithRequestDto request, CancellationToken ct)
    {
        var result = await verificationService.VerifyAsync(request.Text, ct);
        return Ok(result);
    }
}
