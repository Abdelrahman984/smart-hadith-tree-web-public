using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

/// <summary>Detects hidden defects (علل الحديث) across the turuq of a hadith.</summary>
public interface IIlalAnalysisService
{
    /// <summary>Analyzes the given hadiths as turuq of the same narration.</summary>
    Task<IlalReportDto> AnalyzeAsync(IReadOnlyCollection<Guid> hadithIds, CancellationToken ct = default);
}

/// <summary>Writes a scholarly Arabic explanation of an Ilal report using the configured LLM.</summary>
public interface IIlalExplanationService
{
    Task<IlalExplanationDto> ExplainAsync(IlalReportDto report, CancellationToken ct = default);
}
