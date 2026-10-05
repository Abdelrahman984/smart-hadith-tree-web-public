using System.IO.Compression;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Infrastructure.Data.Gawami;

public class GawamiImporterService : IGawamiImporterService
{
    private readonly IHadithTreeDbContext _context;
    private readonly ILogger<GawamiImporterService> _logger;

    public GawamiImporterService(IHadithTreeDbContext context, ILogger<GawamiImporterService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ImportNarratorsAsync(string dataDir, CancellationToken cancellationToken = default)
    {
        var rawyZipPath = Path.Combine(dataDir, "Rawy.zip");
        if (!File.Exists(rawyZipPath))
        {
            _logger.LogWarning("Rawy.zip not found at {Path}", rawyZipPath);
            return;
        }

        // 1. Read Trees.zip (WasfRotba.txt & BaladEkama.txt) for TotalNarrationsCount, UniqueHadithCount, and extra city mappings
        var statsByGawamiId = new Dictionary<int, (int TotalChains, int UniqueHadiths, string? Rank)>();
        var extraCitiesByGawamiId = new Dictionary<int, List<string>>();

        var treesZipPath = Path.Combine(dataDir, "Trees.zip");
        if (File.Exists(treesZipPath))
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var win1256 = Encoding.GetEncoding(1256);

            using var treesZip = ZipFile.OpenRead(treesZipPath);

            var rotbaEntry = treesZip.GetEntry("Trees/WasfRotba.txt");
            if (rotbaEntry != null)
            {
                using var stream = rotbaEntry.Open();
                using var sr = new StreamReader(stream, win1256);
                string? currentRank = null;

                while (!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split('\t');
                    if (cols.Length < 4) continue;

                    if (!string.IsNullOrWhiteSpace(cols[1]) && string.IsNullOrWhiteSpace(cols[2]))
                    {
                        currentRank = cols[1].Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(cols[2]) && cols.Length >= 6)
                    {
                        if (int.TryParse(cols[3].Trim(), out int gId))
                        {
                            int.TryParse(cols[4].Trim(), out int totalChains);
                            int.TryParse(cols[5].Trim(), out int uniqueHadiths);
                            statsByGawamiId[gId] = (totalChains, uniqueHadiths, currentRank);
                        }
                    }
                }
            }

            var baladEntry = treesZip.GetEntry("Trees/BaladEkama.txt");
            if (baladEntry != null)
            {
                using var stream = baladEntry.Open();
                using var sr = new StreamReader(stream, win1256);
                string? currentCity = null;

                while (!sr.EndOfStream)
                {
                    var line = await sr.ReadLineAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var cols = line.Split('\t');
                    if (cols.Length < 4) continue;

                    if (!string.IsNullOrWhiteSpace(cols[1]) && string.IsNullOrWhiteSpace(cols[2]))
                    {
                        currentCity = cols[1].Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(cols[2]) && currentCity != null)
                    {
                        if (int.TryParse(cols[3].Trim(), out int gId))
                        {
                            if (!extraCitiesByGawamiId.TryGetValue(gId, out var list))
                            {
                                list = [];
                                extraCitiesByGawamiId[gId] = list;
                            }
                            if (!list.Contains(currentCity))
                                list.Add(currentCity);
                        }
                    }
                }
            }
        }

        // 2. Load existing narrators from DB for smart matching & enrichment
        var existingNarrators = await _context.Narrators.ToListAsync(cancellationToken);
        var byGawamiId = existingNarrators
            .Where(n => n.GawamiId.HasValue)
            .GroupBy(n => n.GawamiId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var byNormalizedKnownAs = existingNarrators
            .Where(n => !string.IsNullOrWhiteSpace(n.KnownAs))
            .GroupBy(n => ArabicNormalizer.Normalize(n.KnownAs!))
            .ToDictionary(g => g.Key, g => g.ToList());

        var byNormalizedFullName = existingNarrators
            .Where(n => !string.IsNullOrWhiteSpace(n.FullName))
            .GroupBy(n => ArabicNormalizer.Normalize(n.FullName))
            .ToDictionary(g => g.Key, g => g.ToList());

        using var zip = ZipFile.OpenRead(rawyZipPath);
        var rawyEntry = zip.GetEntry("Rawy/Rawy.tbx");
        if (rawyEntry == null) return;

        var tempFile = Path.GetTempFileName();
        rawyEntry.ExtractToFile(tempFile, overwrite: true);

        try
        {
            using var reader = new GawamiTbxReader(tempFile);
            int updatedCount = 0;
            int addedCount = 0;

            while (reader.ReadNextRow() is { } row)
            {
                if (row.Length < 3) continue;

                if (!int.TryParse(row[0].TrimStart(' '), out int gawamiId))
                    continue;

                var fullName = row[2].Trim();
                var shohra = row.Length > 3 && !string.IsNullOrWhiteSpace(row[3]) ? row[3].Trim() : null;
                var kunyah = row.Length > 5 && !string.IsNullOrWhiteSpace(row[5]) ? row[5].Trim() : null;
                var rank = row.Length > 12 && !string.IsNullOrWhiteSpace(row[12]) ? row[12].Trim() : null;
                var tabaqaCode = row.Length > 13 && !string.IsNullOrWhiteSpace(row[13]) ? row[13].Trim() : null;
                var hasMukhtalit = row.Length > 14 && row[14].Trim() == "1";
                var isMudallis = (row.Length > 15 && row[15].Trim() == "1")
                                 || (rank != null && (rank.Contains("مدلس") || rank.Contains("بالتدليس")));

                int? birthYear = row.Length > 16 && int.TryParse(row[16].Trim(), out int by) ? by : null;
                int? deathYear = row.Length > 17 && int.TryParse(row[17].Trim(), out int dy) ? dy : null;

                var residencePlaces = row.Length > 19 && !string.IsNullOrWhiteSpace(row[19]) ? row[19].Trim() : null;
                var deathPlace = row.Length > 20 && !string.IsNullOrWhiteSpace(row[20]) ? row[20].Trim() : null;
                int? uniqueHadiths = row.Length > 26 && int.TryParse(row[26].Trim(), out int uh) ? uh : null;

                // Merge with Trees.zip stats & cities
                int? totalChains = null;
                if (statsByGawamiId.TryGetValue(gawamiId, out var treeStats))
                {
                    totalChains = treeStats.TotalChains;
                    uniqueHadiths ??= treeStats.UniqueHadiths;
                    rank ??= treeStats.Rank;
                }

                if (string.IsNullOrWhiteSpace(residencePlaces) && extraCitiesByGawamiId.TryGetValue(gawamiId, out var extraCities))
                {
                    residencePlaces = string.Join(" ، ", extraCities);
                }

                // Try matching an existing narrator
                Narrator? target = null;
                if (byGawamiId.TryGetValue(gawamiId, out var exactById))
                {
                    target = exactById;
                }
                else
                {
                    if (shohra != null && byNormalizedKnownAs.TryGetValue(ArabicNormalizer.Normalize(shohra), out var candidates))
                    {
                        target = candidates.FirstOrDefault(c =>
                            !c.DeathYearHijri.HasValue || !deathYear.HasValue || Math.Abs(c.DeathYearHijri.Value - deathYear.Value) <= 5)
                            ?? candidates.FirstOrDefault();
                    }

                    if (target == null && !string.IsNullOrWhiteSpace(fullName) &&
                        byNormalizedFullName.TryGetValue(ArabicNormalizer.Normalize(fullName), out var fullCandidates))
                    {
                        target = fullCandidates.FirstOrDefault(c =>
                            !c.DeathYearHijri.HasValue || !deathYear.HasValue || Math.Abs(c.DeathYearHijri.Value - deathYear.Value) <= 5)
                            ?? fullCandidates.FirstOrDefault();
                    }
                }

                if (target != null)
                {
                    target.GawamiId = gawamiId;
                    target.IsMudallis = target.IsMudallis || isMudallis;
                    target.HasMukhtalit = target.HasMukhtalit || hasMukhtalit;
                    target.ResidencePlaces ??= residencePlaces;
                    target.DeathPlace ??= deathPlace;
                    target.GawamiRank ??= rank;
                    target.TotalNarrationsCount ??= totalChains;
                    target.UniqueHadithCount ??= uniqueHadiths;
                    target.Kunyah ??= kunyah;
                    target.BirthYearHijri ??= birthYear;
                    target.DeathYearHijri ??= deathYear;
                    byGawamiId[gawamiId] = target;
                    updatedCount++;
                }
                else
                {
                    var narrator = new Narrator
                    {
                        Id = Guid.NewGuid(),
                        FullName = fullName,
                        KnownAs = shohra,
                        Kunyah = kunyah,
                        GenerationTier = MapTabaqaToArabic(tabaqaCode),
                        BirthYearHijri = birthYear,
                        DeathYearHijri = deathYear,
                        ResidencePlaces = residencePlaces,
                        DeathPlace = deathPlace,
                        GawamiRank = rank,
                        TotalNarrationsCount = totalChains,
                        UniqueHadithCount = uniqueHadiths,
                        GawamiId = gawamiId,
                        IsMudallis = isMudallis,
                        HasMukhtalit = hasMukhtalit,
                        ItqanGrade = MapRankToItqanGrade(rank)
                    };

                    _context.Narrators.Add(narrator);
                    byGawamiId[gawamiId] = narrator;
                    addedCount++;
                }

                if ((addedCount + updatedCount) % 2000 == 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Gawami Narrator Import Complete: {Updated} existing narrators enriched, {Added} new narrators added.", updatedCount, addedCount);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static string? MapTabaqaToArabic(string? code) => code switch
    {
        "1" => "صحابي",
        "2" => "من كبار التابعين",
        "3" => "من أوساط التابعين",
        "4" => "من صغار التابعين",
        "5" => "من صغار التابعين",
        "6" => "من الذين عاصروا صغار التابعين",
        "7" => "من كبار أتباع التابعين",
        "8" => "من الوسطى من أتباع التابعين",
        "9" => "من صغار أتباع التابعين",
        "10" => "من كبار الآخذين عن تبع الأتباع",
        "11" => "من أوساط الآخذين عن تبع الأتباع",
        "12" => "من صغار الآخذين عن تبع الأتباع",
        _ => null
    };

    private static string? MapRankToItqanGrade(string? rank)
    {
        if (string.IsNullOrWhiteSpace(rank)) return null;
        if (rank.Contains("صحابي")) return "companion";
        if (rank.Contains("كذاب") || rank.Contains("وضاع") || rank.Contains("يكذب")) return "fabricator";
        if (rank.Contains("متروك") || rank.Contains("واهي")) return "abandoned";
        if (rank.Contains("ثقة")) return "reliable";
        if (rank.Contains("صدوق") || rank.Contains("لا بأس به")) return "mostly_reliable";
        if (rank.Contains("ضعيف") || rank.Contains("لين")) return "weak";
        if (rank.Contains("مجهول") || rank.Contains("مقبول")) return "unknown";
        return null;
    }

    public async Task ImportScholarEvaluationsAsync(string dataDir, CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(dataDir, "Gar7_Data.zip");
        if (!File.Exists(zipPath)) return;

        // Build lookup from GawamiId -> Narrator.Id
        var narratorMap = await _context.Narrators
            .Where(n => n.GawamiId.HasValue)
            .Select(n => new { GawamiId = n.GawamiId!.Value, n.Id })
            .ToDictionaryAsync(x => x.GawamiId, x => x.Id, cancellationToken);

        using var zip = ZipFile.OpenRead(zipPath);

        // Map AlemID to ScholarName
        var dictAlem = new Dictionary<int, string>();
        var alemEntry = zip.GetEntry("Gar7_Data/Alem.TBX");
        if (alemEntry != null)
        {
            var tempAlem = Path.GetTempFileName();
            alemEntry.ExtractToFile(tempAlem, overwrite: true);
            using var reader = new GawamiTbxReader(tempAlem);
            while (reader.ReadNextRow() is { } row)
            {
                if (row.Length > 2 && int.TryParse(row[0].TrimStart(' '), out int aId))
                {
                    dictAlem[aId] = row[2];
                }
            }
            File.Delete(tempAlem);
        }

        var qawlEntry = zip.GetEntry("Gar7_Data/AlemQawl.TBX");
        if (qawlEntry == null) return;

        var tempQawl = Path.GetTempFileName();
        qawlEntry.ExtractToFile(tempQawl, overwrite: true);

        try
        {
            using var reader = new GawamiTbxReader(tempQawl);
            int count = 0;

            while (reader.ReadNextRow() is { } row)
            {
                if (row.Length < 3) continue;

                if (int.TryParse(row[0].TrimStart(' '), out int alemId) &&
                    int.TryParse(row[1].TrimStart(' '), out int rawyId) &&
                    narratorMap.TryGetValue(rawyId, out Guid narratorId))
                {
                    var text = row[2];

                    var eval = new ScholarEvaluation
                    {
                        Id = Guid.NewGuid(),
                        NarratorId = narratorId,
                        GawamiAlemId = alemId,
                        GawamiRawyId = rawyId,
                        EvaluationText = text,
                        ScholarName = dictAlem.TryGetValue(alemId, out var name) ? name : $"Alem {alemId}"
                    };

                    _context.ScholarEvaluations.Add(eval);
                    count++;
                }

                if (count > 0 && count % 5000 == 0)
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    _context.ScholarEvaluations.Local.Clear();
                }
            }
            await _context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            File.Delete(tempQawl);
        }
    }

    public Task ImportIsnadJudgmentsAsync(string dataDir, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
