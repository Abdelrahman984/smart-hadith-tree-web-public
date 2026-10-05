using Microsoft.AspNetCore.Mvc;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IBooksService _booksService;

    public BooksController(IBooksService booksService)
    {
        _booksService = booksService;
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetBooks(CancellationToken cancellationToken)
    {
        var books = await _booksService.GetBooksAsync(cancellationToken);
        return Ok(books);
    }

    [HttpGet("{bookName}/chapters")]
    public async Task<ActionResult<List<string>>> GetChapters(string bookName, CancellationToken cancellationToken)
    {
        var chapters = await _booksService.GetChaptersAsync(bookName, cancellationToken);
        return Ok(chapters);
    }

    [HttpGet("{bookName}/chapters/{chapter}/hadiths")]
    public async Task<ActionResult<List<HadithSearchResultDto>>> GetHadiths(string bookName, string chapter, CancellationToken cancellationToken)
    {
        var hadiths = await _booksService.GetHadithsAsync(bookName, chapter, cancellationToken);
        return Ok(hadiths);
    }
}
