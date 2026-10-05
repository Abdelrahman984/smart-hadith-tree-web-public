// Phase 6: run the real IlalAnalysisService on the same (paired) hadiths in two databases.
//   dotnet run -c Release -- <database> <v2|shamela> <pairs.json> <out.json> [perBook=150]
// Each hadith is analysed alone (its own isnad), so the counts are per-hadith tadlis / ikhtilat findings.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Infrastructure.Data;

var database = args[0];
var side = args[1] == "v2" ? 0 : 1;
var pairs = JsonSerializer.Deserialize<Dictionary<string, List<string[]>>>(File.ReadAllText(args[2]))!;
var perBook = args.Length > 4 ? int.Parse(args[4]) : 150;

var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
    .UseSqlServer($"Server=.;Database={database};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
        o => o.CommandTimeout(300))
    .Options;
await using var db = new HadithTreeDbContext(options);
var service = new IlalAnalysisService(db);

var result = new List<object>();
foreach (var (book, list) in pairs)
{
    // A deterministic, evenly spread sample of the pairs, the same for both databases.
    var step = Math.Max(1, list.Count / perBook);
    var picked = list.Where((_, i) => i % step == 0).Take(perBook).ToList();
    foreach (var pair in picked)
    {
        var report = await service.AnalyzeAsync([Guid.Parse(pair[side])]);
        result.Add(new
        {
            book,
            key = pair[0] + "|" + pair[1],
            chain = report.Turuq.Count,
            findings = report.Findings
                .Select(f => new { type = f.Type.ToString(), severity = f.Severity.ToString(), narrators = f.NarratorIds.Count, title = f.TitleAr, evidence = f.EvidenceAr.Length > 300 ? f.EvidenceAr[..300] : f.EvidenceAr })
                .ToList()
        });
    }
    Console.WriteLine($"{book}: {picked.Count}");
}
File.WriteAllText(args[3], JsonSerializer.Serialize(result));
