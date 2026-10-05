using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>A deterministic check for one family of hidden defects (علل).</summary>
public interface IIlalRule
{
    IEnumerable<IlalFindingDto> Evaluate(IlalContext context);
}
