namespace SmartHadithTree.Application.Services;

using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

public class BooksService : IBooksService
{
    private readonly IHadithTreeDbContext _context;

    public BooksService(IHadithTreeDbContext context)
    {
        _context = context;
    }

    public async Task<List<string>> GetBooksAsync(CancellationToken cancellationToken = default)
    {
        var books = await _context.Hadiths
            .Where(h => !string.IsNullOrEmpty(h.BookName))
            .Select(h => h.BookName)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Filter out books with English characters to only show Arabic books
        return books
            .Where(b => !b.Any(c => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')))
            .OrderBy(b => b)
            .ToList();
    }

    public async Task<List<string>> GetChaptersAsync(string bookName, CancellationToken cancellationToken = default)
    {
        return await _context.Hadiths
            .Where(h => h.BookName == bookName && !string.IsNullOrEmpty(h.Chapter))
            .GroupBy(h => h.Chapter)
            .OrderBy(g => g.Min(h => h.HadithNumber))
            .Select(g => g.Key!)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<HadithSearchResultDto>> GetHadithsAsync(string bookName, string chapter, CancellationToken cancellationToken = default)
    {
        return await _context.Hadiths
            .Where(h => h.BookName == bookName && h.Chapter == chapter)
            .OrderBy(h => h.HadithNumber)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                MatnSnippet = h.MatnArabic.Length > 150 ? h.MatnArabic.Substring(0, 150) + "..." : h.MatnArabic
            })
            .ToListAsync(cancellationToken);
    }
}
