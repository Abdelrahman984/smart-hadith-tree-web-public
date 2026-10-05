using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

public interface IHadithSearchService
{
    Task<List<HadithSearchResultDto>> SearchHadithsAsync(SearchRequestDto request, CancellationToken ct = default);
    Task<IsnadTreeResponseDto?> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default);

    /// <summary>
    /// Merges the Isnad chains of multiple Hadiths into a single comparative tree.
    /// </summary>
    Task<ComparativeTreeResponseDto?> GetComparativeTreeAsync(List<Guid> hadithIds, CancellationToken ct = default);

    /// <summary>
    /// Finds related Hadiths across all books by matching normalized Matn text.
    /// </summary>
    Task<List<HadithSearchResultDto>> FindRelatedHadithsAsync(Guid hadithId, CancellationToken ct = default);
}

public interface INarratorService
{
    Task<List<NarratorSearchResultDto>> SearchNarratorsAsync(string query, CancellationToken ct = default);
    Task<NarratorDetailDto?> GetNarratorDetailsAsync(Guid narratorId, CancellationToken ct = default);
    Task<NarratorSummaryDto?> GetNarratorTooltipAsync(Guid narratorId, CancellationToken ct = default);
}
