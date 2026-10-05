using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHadithTree.Etl.Parsers.Shamela;

public class ShamelaJsonParser : IDataSourceParser
{
    private readonly ILogger<ShamelaJsonParser>? _logger;

    public string Name => "Shamela JSON Parser";

    public ShamelaJsonParser(ILogger<ShamelaJsonParser>? logger = null)
    {
        _logger = logger;
    }

    public bool CanParse(string sourcePath)
    {
        return Directory.Exists(sourcePath) && Directory.GetFiles(sourcePath, "index.json", SearchOption.AllDirectories).Any();
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var dataset = new ParsedDataset();
        
        var indexFiles = Directory.GetFiles(sourcePath, "index.json", SearchOption.AllDirectories);

        foreach (var indexFile in indexFiles)
        {
            var bookDir = Path.GetDirectoryName(indexFile);
            if (bookDir == null) continue;

            var slug = new DirectoryInfo(bookDir).Name;
            await ParseBookDirAsync(bookDir, slug, dataset, cancellationToken);
        }

        return dataset;
    }

    private async Task ParseBookDirAsync(string bookDir, string slug, ParsedDataset dataset, CancellationToken cancellationToken)
    {
        var jsonFiles = Directory.GetFiles(bookDir, "*.json").Where(f => !f.EndsWith("index.json")).ToList();

        // Regex to extract numbered hadiths: e.g. 1 - حدثنا... or [123] - حدثنا...
        var hadithRegex = new Regex(@"(?:^|\n)\s*[\[\(]?([٠-٩0-9]{1,5})[\]\)]?\s*[-–—:]\s*(.+?)(?=(?:\n\s*[\[\(]?[٠-٩0-9]{1,5}[\]\)]?\s*[-–—:])|$)", RegexOptions.Singleline);

        foreach (var file in jsonFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file, cancellationToken);
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var chapterTitle = root.TryGetProperty("title_text", out var tProp) ? tProp.GetString() : "Unknown Chapter";

                if (root.TryGetProperty("pages", out var pagesElement))
                {
                    foreach (var page in pagesElement.EnumerateArray())
                    {
                        var body = page.TryGetProperty("body", out var bProp) ? bProp.GetString() : string.Empty;
                        if (string.IsNullOrWhiteSpace(body)) continue;

                        var matches = hadithRegex.Matches(body);
                        foreach (Match match in matches)
                        {
                            var numStr = match.Groups[1].Value;
                            var text = match.Groups[2].Value.Trim();
                            
                            var hadithId = Guid.NewGuid();

                            dataset.Hadiths.Add(new HadithText
                            {
                                Id = hadithId,
                                BookName = slug,
                                HadithNumber = int.TryParse(NormalizeArabicDigits(numStr), out var hn) ? hn : 0,
                                Chapter = chapterTitle ?? "Unknown Chapter",
                                MatnArabic = text
                            });

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse Shamela JSON file {File}", file);
            }
        }

        _logger?.LogInformation("Parsed {Count} hadiths from {Slug}", dataset.Hadiths.Count(h => h.BookName == slug), slug);
    }

    private static string NormalizeArabicDigits(string input)
    {
        return input.Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3')
                    .Replace('٤', '4').Replace('٥', '5').Replace('٦', '6').Replace('٧', '7')
                    .Replace('٨', '8').Replace('٩', '9');
    }
}
