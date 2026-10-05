namespace SmartHadithTree.Application.Interfaces;

using SmartHadithTree.Application.DTOs;

public interface IBooksService
{
    Task<List<string>> GetBooksAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetChaptersAsync(string bookName, CancellationToken cancellationToken = default);
    Task<List<HadithSearchResultDto>> GetHadithsAsync(string bookName, string chapter, CancellationToken cancellationToken = default);
}
