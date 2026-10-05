using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Etl.Parsers.Shamela;

/// <summary>
/// Loads the Shamela-built dataset (migration Phase 5, option B: the resolver stays in Python and writes files).
/// The source path is the folder <c>data/shamela_rijal</c> that holds <c>registry.json</c> (rijal_pilot/pipeline.py)
/// and <c>chains/&lt;slug&gt;.json</c> (rijal_pilot/export_chains_shamela.py). The texts are read from the sibling
/// folder <c>data/shamela/&lt;slug&gt;/</c> (build_shamela_books.py + split_parts.py), and Shamela's narrator
/// encyclopedia from <c>data/shamela/narrators.json</c> with its mapping <c>review/links/s1_registry_map.json</c>.
/// </summary>
public sealed class ShamelaDatasetParser(ILogger<ShamelaDatasetParser>? logger = null) : IDataSourceParser
{
    private const string RelationSource = "shamela";

    private static readonly JsonSerializerOptions Json = new();

    public string Name => "Shamela Dataset Parser (registry + chains)";

    public bool CanParse(string sourcePath) =>
        Directory.Exists(sourcePath) && File.Exists(Path.Combine(sourcePath, "registry.json"));

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var rijalDir = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var textsDir = Path.Combine(Path.GetDirectoryName(rijalDir)!, "shamela");
        var dataset = new ParsedDataset();

        // 1. Narrators, their relations and quotes.
        var registry = await ReadAsync<List<RegistryEntry>>(Path.Combine(rijalDir, "registry.json"), cancellationToken)
                       ?? throw new InvalidDataException("registry.json is empty.");
        var s1ByRegistry = await LoadS1Async(rijalDir, textsDir, cancellationToken);

        var bySourceKey = new Dictionary<string, Guid>(registry.Count);
        foreach (var entry in registry)
        {
            s1ByRegistry.TryGetValue(entry.Id, out var s1);
            var narrator = ShamelaNarratorMapper.MapNarrator(entry, s1);
            dataset.Narrators.Add(narrator);
            bySourceKey[entry.Id] = narrator.Id;
            dataset.ScholarEvaluations.AddRange(ShamelaNarratorMapper.MapRegistryQuotes(narrator, entry));
            if (s1 is not null)
                dataset.ScholarEvaluations.AddRange(ShamelaNarratorMapper.MapS1Quotes(narrator, s1));
        }
        dataset.NarratorRelations = BuildRelations(registry, bySourceKey);
        logger?.LogInformation(
            "Registry: {Narrators} narrators ({WithS1} with a Shamela encyclopedia entry), {Evaluations} quotes, {Relations} relations.",
            dataset.Narrators.Count, s1ByRegistry.Count, dataset.ScholarEvaluations.Count, dataset.NarratorRelations.Count);

        // 2. Hadiths and their chains, book by book.
        var chainsDir = Path.Combine(rijalDir, "chains");
        foreach (var chainsFile in Directory.GetFiles(chainsDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(chainsFile).StartsWith('_')) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var chains = await ReadAsync<BookChains>(chainsFile, cancellationToken);
            if (chains is null) continue;
            await ParseBookAsync(chains, Path.Combine(textsDir, chains.Book), bySourceKey, dataset, cancellationToken);
        }
        return dataset;
    }

    private async Task ParseBookAsync(
        BookChains chains, string bookDir, Dictionary<string, Guid> bySourceKey, ParsedDataset dataset, CancellationToken ct)
    {
        var info = await ReadAsync<ShamelaBookInfo>(Path.Combine(bookDir, "book.json"), ct)
                   ?? throw new InvalidDataException($"{bookDir}\\book.json is empty.");
        var bookName = ShamelaBookNames.For(chains.Book, info.Title);
        var index = await ReadAsync<List<ShamelaIndexEntry>>(Path.Combine(bookDir, "index.json"), ct) ?? [];
        var chapterOf = index.Where(i => !string.IsNullOrWhiteSpace(i.NameAr)).ToDictionary(i => i.File, i => i.NameAr!);
        var chainOf = chains.Chains.ToDictionary(c => c.Id);
        Guid? compiler = chains.Compiler is { } key && bySourceKey.TryGetValue(key, out var cg) ? cg : null;

        int hadiths = 0, linked = 0;
        foreach (var file in Directory.GetFiles(bookDir, "*.json")
                     .Where(f => int.TryParse(Path.GetFileNameWithoutExtension(f), out _))
                     .OrderBy(f => int.Parse(Path.GetFileNameWithoutExtension(f))))
        {
            var records = await ReadAsync<List<ShamelaRecord>>(file, ct) ?? [];
            chapterOf.TryGetValue(Path.GetFileName(file), out var chapter);
            foreach (var record in records)
            {
                var hadith = ShamelaHadithMapper.Map(record, bookName, chapter ?? record.Bab);
                if (hadith is null) continue;
                dataset.Hadiths.Add(hadith);
                hadiths++;
                if (!chainOf.TryGetValue(record.Id, out var chain)) continue;
                var transmissions = ShamelaChainBuilder.BuildAll(hadith.Id, compiler, chain, bySourceKey);
                if (transmissions.Count > 0) linked++;
                dataset.Transmissions.AddRange(transmissions);
            }
        }
        logger?.LogInformation("{Book}: {Hadiths} hadiths, {Linked} with at least one link.", bookName, hadiths, linked);
    }

    /// <summary>Strict teacher/student lists of the registry → relations (one per pair, whichever side lists it).</summary>
    public static List<NarratorRelation> BuildRelations(IEnumerable<RegistryEntry> registry, IReadOnlyDictionary<string, Guid> bySourceKey)
    {
        var seen = new HashSet<(Guid Teacher, Guid Student)>();
        var relations = new List<NarratorRelation>();

        void Add(string teacherKey, string studentKey)
        {
            if (!bySourceKey.TryGetValue(teacherKey, out var t) || !bySourceKey.TryGetValue(studentKey, out var s) || t == s) return;
            if (seen.Add((t, s)))
                relations.Add(new NarratorRelation { Id = Guid.NewGuid(), TeacherId = t, StudentId = s, Source = RelationSource });
        }

        foreach (var e in registry)
        {
            foreach (var teacher in e.Shuyukh ?? []) Add(teacher, e.Id);
            foreach (var student in e.Talamidh ?? []) Add(e.Id, student);
        }
        return relations;
    }

    /// <summary>Registry id → Shamela's encyclopedia entry, for the mappings <c>map_s1.py</c> marks exact.</summary>
    private async Task<Dictionary<string, S1Narrator>> LoadS1Async(string rijalDir, string textsDir, CancellationToken ct)
    {
        var mapFile = Path.Combine(rijalDir, "review", "links", "s1_registry_map.json");
        var narratorsFile = Path.Combine(textsDir, "narrators.json");
        if (!File.Exists(mapFile) || !File.Exists(narratorsFile))
        {
            logger?.LogWarning("Shamela encyclopedia not found ({Map}, {Narrators}): no death years, kunyas or page references.", mapFile, narratorsFile);
            return [];
        }
        var map = await ReadAsync<Dictionary<string, S1Mapping>>(mapFile, ct) ?? [];
        var s1 = (await ReadAsync<List<S1Narrator>>(narratorsFile, ct) ?? []).ToDictionary(n => n.Id.ToString());
        var result = new Dictionary<string, S1Narrator>();
        foreach (var (manId, m) in map.OrderBy(p => int.Parse(p.Key)))
        {
            if (m.Confidence != "exact" || m.Registry is null || !s1.TryGetValue(manId, out var narrator)) continue;
            result.TryAdd(m.Registry, narrator);      // five registry entries have two exact S1 ids: the lower one
        }
        return result;
    }

    private static async Task<T?> ReadAsync<T>(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, Json, ct);
    }
}
