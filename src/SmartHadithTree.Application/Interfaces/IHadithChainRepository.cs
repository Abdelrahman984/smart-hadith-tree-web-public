using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

public interface IHadithChainRepository
{
    Task<List<IsnadNodeDto>> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default);

    /// <summary>
    /// Traverses the Isnad graphs for multiple Hadiths using a multi-anchor Recursive CTE,
    /// merging shared narrators into unified nodes with source attribution.
    /// </summary>
    Task<List<ComparativeIsnadNodeDto>> GetComparativeIsnadTreeAsync(List<Guid> hadithIds, CancellationToken ct = default);
}
