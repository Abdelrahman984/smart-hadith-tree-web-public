using System.Text.RegularExpressions;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Etl.Parsers.Shamela;

/// <summary>Registry entries, S1 encyclopedia entries and critics' quotes → domain entities.</summary>
public static partial class ShamelaNarratorMapper
{
    /// <summary>Book whose quotes are already in <c>registry.json</c> (the registry is parsed from it).</summary>
    public const string TahdhibAlKamal = "تهذيب الكمال";

    // The kunya at the start of a segment: «أبو بكر بن أبي شيبة» → «أبو بكر», «أبو عبد الله الكوفي» → «أبو عبد الله».
    [GeneratedRegex(@"^\s*((?:أبو|أبي|أم)\s+(?:عبد\s+\S+|عبيد\s+الله|[^\s،.]+))(?=\s|$)")]
    private static partial Regex KunyaRegex();

    /// <summary>
    /// Maps one registry entry. <paramref name="s1"/> (Shamela's own encyclopedia entry, when the registry entry
    /// maps to one exactly) adds the death year, the kunya and the residence.
    /// </summary>
    public static Narrator MapNarrator(RegistryEntry entry, S1Narrator? s1 = null)
    {
        var name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Header : entry.Name;
        var field = (string key) => s1?.Fields is { } f && f.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        return new Narrator
        {
            Id = Guid.NewGuid(),
            SourceKey = entry.Id,
            ShamelaManId = s1?.Id,
            FullName = Truncate(name.Trim(), 500)!,
            Kunyah = Truncate(field("الكنية") ?? ExtractKunya(entry.Header, entry.Name), 200),
            GenerationTier = Truncate(entry.Tabaqa, 150),
            DeathYearHijri = s1?.Death,
            ResidencePlaces = Truncate(field("بلد الإقامة"), 500),
            Biography = entry.Header,
            IbnHajarRank = entry.Rank,
            Verdict = Truncate(entry.Verdict, 500)
        };
    }

    /// <summary>The first kunya after the name in a Tahdhib header, or null.</summary>
    public static string? ExtractKunya(string? header, string? name)
    {
        if (string.IsNullOrWhiteSpace(header)) return null;
        var sentence = header.Split(['.', '\n'], 2)[0];
        foreach (var segment in sentence.Split('،').Skip(1).Take(4))
        {
            var match = KunyaRegex().Match(segment);
            if (match.Success && !(name ?? "").Trim().StartsWith(match.Groups[1].Value)) return match.Groups[1].Value;
        }
        return null;
    }

    /// <summary>Quotes of Tahdhib al-Kamal, as the registry holds them.</summary>
    public static IEnumerable<ScholarEvaluation> MapRegistryQuotes(Narrator narrator, RegistryEntry entry)
    {
        foreach (var q in entry.Quotes ?? [])
        {
            if (string.IsNullOrWhiteSpace(q.Text)) continue;
            var text = string.IsNullOrWhiteSpace(q.Via) ? q.Text.Trim() : $"{q.Text.Trim()} (رواية: {q.Via.Trim()})";
            yield return new ScholarEvaluation
            {
                Id = Guid.NewGuid(),
                NarratorId = narrator.Id,
                ScholarName = Truncate(q.Critic?.Trim(), 300) ?? "غير منسوب",
                EvaluationText = text,
                SourceBook = TahdhibAlKamal
            };
        }
    }

    /// <summary>
    /// Quotes of Shamela's encyclopedia with their book, volume and page. Tahdhib al-Kamal's are skipped:
    /// the registry already has them.
    /// </summary>
    public static IEnumerable<ScholarEvaluation> MapS1Quotes(Narrator narrator, S1Narrator s1)
    {
        foreach (var q in s1.Quotes ?? [])
        {
            if (string.IsNullOrWhiteSpace(q.Text) || q.Book == TahdhibAlKamal) continue;
            yield return new ScholarEvaluation
            {
                Id = Guid.NewGuid(),
                NarratorId = narrator.Id,
                ScholarName = Truncate(q.Critic?.Trim(), 300) ?? "غير منسوب",
                EvaluationText = q.Text.Trim(),
                SourceBook = Truncate(q.Book, 300),
                SourceVolume = q.Vol?.ToString(),
                SourcePage = q.Page
            };
        }
    }

    internal static string? Truncate(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}

/// <summary>A Phase 4 record → <see cref="HadithText"/>.</summary>
public static class ShamelaHadithMapper
{
    /// <summary>
    /// Maps a record of kind "hadith". <c>MatnArabic</c> keeps the full cleaned text (isnad and matn together),
    /// as the Itqan data did, so the app's matn heuristics and display are unchanged; <c>FullIsnadText</c> is the
    /// isnad part(s). Returns null for the compiler's prose (kind "text") and empty records.
    /// </summary>
    public static HadithText? Map(ShamelaRecord record, string bookTitle, string? chapter)
    {
        if (record.Kind == "text" || string.IsNullOrWhiteSpace(record.Arabic)) return null;
        var arabic = record.Arabic;
        var isnad = string.Join('\n', (record.Parts ?? [])
            .Where(p => p.Type == "isnad" && p.Start >= 0 && p.End <= arabic.Length && p.End > p.Start)
            .Select(p => arabic[p.Start..p.End].Trim()));
        return new HadithText
        {
            Id = Guid.NewGuid(),
            BookName = bookTitle,
            NormalizedBookName = ArabicNormalizer.Normalize(bookTitle),
            HadithNumber = record.Number ?? record.Id,
            Volume = ShamelaNarratorMapper.Truncate(record.Vol, 50),
            Chapter = ShamelaNarratorMapper.Truncate(chapter, 500),
            MatnArabic = arabic,
            NormalizedMatn = ArabicNormalizer.Normalize(arabic),
            FullIsnadText = isnad.Length > 0 ? isnad : null
        };
    }
}

/// <summary>A resolved isnad → <see cref="Transmission"/>s.</summary>
public static class ShamelaChainBuilder
{
    /// <summary>The verb that stands for the link: the last explicit-hearing one if any, else the last verb.</summary>
    public static string? TermOf(IReadOnlyList<string>? verbs) =>
        verbs is { Count: > 0 } ? verbs.LastOrDefault(TransmissionTerms.IsExplicitHearing) ?? verbs[^1] : null;

    /// <summary>
    /// The transmissions of a record: one chain, or every chain of a tahwil isnad. Each chain is stored from step 1
    /// upward on its own (a narrator two chains share may sit at different steps), and a link that two chains
    /// share (the same student, sheikh and step) is stored once.
    /// </summary>
    public static List<Transmission> BuildAll(
        Guid hadithId, Guid? compiler, RecordChain chain, IReadOnlyDictionary<string, Guid> narratorBySourceKey)
    {
        if (chain.Branches is not { Count: > 1 } branches)
            return Build(hadithId, compiler, chain.Names, narratorBySourceKey);

        var seen = new HashSet<(int Step, Guid Student, Guid Sheikh)>();
        var result = new List<Transmission>();
        foreach (var branch in branches)
            foreach (var link in Build(hadithId, compiler, branch, narratorBySourceKey))
                if (seen.Add((link.StepOrder, link.StudentId, link.SheikhId)))
                    result.Add(link);
        return result;
    }

    /// <summary>
    /// Links the compiler to the first narrator and each narrator to the next (student ← sheikh), step 1 upwards.
    /// A name the resolver left undecided (<c>Id</c> null) is a gap: no link is made across it, because
    /// «A ← ? ← C» must not become «A ← C». A narrator repeated twice in a row makes no link either.
    /// The term of a link is the transmission verb after the student's name; when several come before the next name
    /// («حدثنا … عن …») an explicit-hearing one wins, as in the Python bench (null for the compiler's link).
    /// </summary>
    public static List<Transmission> Build(
        Guid hadithId, Guid? compiler, IReadOnlyList<ChainName> names, IReadOnlyDictionary<string, Guid> narratorBySourceKey)
    {
        var result = new List<Transmission>();
        var student = compiler;
        string? term = null;
        var step = 1;
        foreach (var name in names)
        {
            if (name.Id is null || !narratorBySourceKey.TryGetValue(name.Id, out var sheikh))
            {
                student = null;
                term = null;
                continue;
            }
            if (student is { } s && s != sheikh)
            {
                result.Add(new Transmission
                {
                    Id = Guid.NewGuid(),
                    HadithId = hadithId,
                    StudentId = s,
                    SheikhId = sheikh,
                    StepOrder = step++,
                    TransmissionTerm = ShamelaNarratorMapper.Truncate(term, 50)
                });
            }
            student = sheikh;
            term = TermOf(name.Verbs);
        }
        return result;
    }
}
