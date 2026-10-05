using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Etl.Services;

/// <summary>
/// Loads the Ilal data of the Shamela books (<c>data/shamela_rijal/ilal.json</c>, written by
/// <c>rijal_pilot/pipeline.py</c> step 7) onto narrators that already carry their registry id (<c>SourceKey</c>):
/// <list type="bullet">
/// <item>mudallisin with Ibn Hajar's tiers (طبقات المدلسين, 152 in five tiers plus the editor's appendix, which has no tier);</item>
/// <item>mukhtalitun (الكواكب النيرات, المختلطين للعلائي) with the ikhtilat's severity, "nobody heard after it",
/// the students heard before / after / both / disputed with the critic's words, and the rules for groups of students.</item>
/// </list>
/// Replaces the old hand-written Itqan seeds. Safe to re-run: the flags, the hearings
/// and the group rules are reset first, so use it on a database built from Shamela, not on one seeded from Itqan.
/// </summary>
public class ShamelaIlalService(HadithTreeDbContext context, ILogger<ShamelaIlalService> logger)
{
    private static readonly JsonSerializerOptions Json = new();

    /// <summary>The books an Ilal claim comes from, by Shamela id.</summary>
    private static readonly Dictionary<int, string> BookNames = new()
    {
        [309] = "الكواكب النيرات",
        [25846] = "المختلطين للعلائي"
    };

    public async Task RunAsync(string ilalJsonPath, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var stream = File.OpenRead(ilalJsonPath);
        var data = await JsonSerializer.DeserializeAsync<IlalFile>(stream, Json, ct)
                   ?? throw new InvalidDataException("ilal.json is empty.");

        var keys = data.Mudallisin.Select(m => m.Id)
            .Concat(data.Mukhtalitun.SelectMany(m => new[] { m.Id }.Concat(m.Hearings.Select(h => h.Id))))
            .Where(k => k != null).Distinct().ToList();
        var narrators = (await context.Narrators.Where(n => n.SourceKey != null && keys.Contains(n.SourceKey)).ToListAsync(ct))
            .ToDictionary(n => n.SourceKey!);

        await ResetAsync(ct);
        var (mudallisin, mukhtalitun, hearings, rules, skipped) = Apply(data, narrators, context);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ilal data in {Elapsed}: {Mudallisin} mudallisin, {Mukhtalitun} mukhtalitun, {Hearings} hearings, {Rules} group rules; " +
            "{Skipped} entries without a registry narrator skipped.",
            sw.Elapsed, mudallisin, mukhtalitun, hearings, rules, skipped);
    }

    private async Task ResetAsync(CancellationToken ct)
    {
        foreach (var n in await context.Narrators
                     .Where(n => n.IsMudallis || n.MudallisTier != null || n.HasMukhtalit || n.IkhtilatNote != null
                                 || n.IkhtilatSeverity != null || n.NoHearingAfterIkhtilat)
                     .ToListAsync(ct))
        {
            (n.IsMudallis, n.MudallisTier, n.HasMukhtalit, n.IkhtilatNote, n.IkhtilatSeverity, n.NoHearingAfterIkhtilat) =
                (false, null, false, null, null, false);
        }
        context.MukhtalitHearings.RemoveRange(await context.MukhtalitHearings.ToListAsync(ct));
        context.MukhtalitGroupRules.RemoveRange(await context.MukhtalitGroupRules.ToListAsync(ct));
        await context.SaveChangesAsync(ct);
    }

    /// <summary>Applies the file to the narrators (found by registry id) and queues the new hearings and rules.</summary>
    public static (int Mudallisin, int Mukhtalitun, int Hearings, int Rules, int Skipped) Apply(
        IlalFile data, IReadOnlyDictionary<string, Narrator> narrators, HadithTreeDbContext context)
    {
        int mudallisin = 0, mukhtalitun = 0, hearings = 0, rules = 0, skipped = 0;

        foreach (var m in data.Mudallisin)
        {
            if (m.Id is null || !narrators.TryGetValue(m.Id, out var narrator)) { skipped++; continue; }
            narrator.IsMudallis = true;
            // The appendix of the editor has no tier: he stays a mudallis without one (the rule needs tier 3+).
            narrator.MudallisTier = narrator.MudallisTier is { } t && m.Tier is { } mt ? Math.Min(t, mt) : m.Tier ?? narrator.MudallisTier;
            mudallisin++;
        }

        foreach (var m in data.Mukhtalitun)
        {
            if (m.Id is null || !narrators.TryGetValue(m.Id, out var narrator)) { skipped++; continue; }
            narrator.HasMukhtalit = true;
            narrator.IkhtilatSeverity = SeverityOf(m);
            narrator.NoHearingAfterIkhtilat = m.NoOneAfter.Count > 0;
            narrator.IkhtilatNote = Truncate(string.Join(" — ", m.Ikhtilat.Select(i => i.Text).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()), 1000);
            mukhtalitun++;

            var seen = new HashSet<Guid>();
            foreach (var h in m.Hearings)
            {
                if (h.Id is null || !narrators.TryGetValue(h.Id, out var student) || student.Id == narrator.Id || !seen.Add(student.Id)) continue;
                var timing = ParseTiming(h.Timing);
                var claim = h.Claims.FirstOrDefault(c => ParseTiming(c.Timing) == timing) ?? h.Claims.FirstOrDefault();
                context.MukhtalitHearings.Add(new MukhtalitHearing
                {
                    Id = Guid.NewGuid(),
                    MukhtalitId = narrator.Id,
                    StudentId = student.Id,
                    Timing = timing,
                    Evidence = Truncate(claim?.Quote, 1000),
                    SourceBook = claim is null ? null : BookName(claim.Book)
                });
                hearings++;
            }

            foreach (var r in m.GroupRules.Where(r => !string.IsNullOrWhiteSpace(r.Group) && !string.IsNullOrWhiteSpace(r.Quote)))
            {
                context.MukhtalitGroupRules.Add(new MukhtalitGroupRule
                {
                    Id = Guid.NewGuid(),
                    MukhtalitId = narrator.Id,
                    GroupAr = Truncate(r.Group, 300)!,
                    Timing = ParseTiming(r.Timing),
                    Quote = Truncate(r.Quote, 1000)!,
                    SourceBook = BookName(r.Book)
                });
                rules++;
            }
        }
        return (mudallisin, mukhtalitun, hearings, rules, skipped);
    }

    /// <summary>
    /// The most severe value the books give. When they give none (85 of 164), «disputed»: the books list him as a
    /// mukhtalit but say how much only for some. The rule flags a disputed ikhtilat only as a low-confidence note.
    /// </summary>
    public static IkhtilatSeverity SeverityOf(MukhtalitEntry m) => m.Severity
        .Select(s => s.Value switch { "harmful" => IkhtilatSeverity.Harmful, "disputed" => IkhtilatSeverity.Disputed, "light" => IkhtilatSeverity.Light, _ => (IkhtilatSeverity?)null })
        .Where(s => s != null).Select(s => s!.Value)
        .DefaultIfEmpty(IkhtilatSeverity.Disputed).Max();

    public static HearingTiming ParseTiming(string? timing) => timing switch
    {
        "before" => HearingTiming.Before,
        "after" => HearingTiming.After,
        "both" => HearingTiming.Both,
        "conflict" => HearingTiming.Conflict,
        _ => HearingTiming.Unknown
    };

    private static string? BookName(int book) => BookNames.GetValueOrDefault(book) ?? (book == 0 ? null : book.ToString());

    private static string? Truncate(string? s, int max) => string.IsNullOrEmpty(s) ? null : s.Length <= max ? s : s[..max];

    // ── ilal.json ─────────────────────────────────────────────────────

    public sealed class IlalFile
    {
        [JsonPropertyName("mudallisin")] public List<MudallisEntry> Mudallisin { get; init; } = [];
        [JsonPropertyName("mukhtalitun")] public List<MukhtalitEntry> Mukhtalitun { get; init; } = [];
    }

    public sealed class MudallisEntry
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("tier")] public int? Tier { get; init; }
    }

    public sealed class MukhtalitEntry
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("ikhtilat")] public List<IkhtilatText> Ikhtilat { get; init; } = [];
        [JsonPropertyName("severity")] public List<SeverityClaim> Severity { get; init; } = [];
        [JsonPropertyName("no_one_after")] public List<JsonElement> NoOneAfter { get; init; } = [];
        [JsonPropertyName("group_rules")] public List<GroupRuleEntry> GroupRules { get; init; } = [];
        [JsonPropertyName("hearings")] public List<HearingEntry> Hearings { get; init; } = [];
    }

    public sealed class IkhtilatText { [JsonPropertyName("text")] public string? Text { get; init; } }

    public sealed class SeverityClaim { [JsonPropertyName("value")] public string? Value { get; init; } }

    public sealed class GroupRuleEntry
    {
        [JsonPropertyName("group")] public string? Group { get; init; }
        [JsonPropertyName("timing")] public string? Timing { get; init; }
        [JsonPropertyName("quote")] public string? Quote { get; init; }
        [JsonPropertyName("book")] public int Book { get; init; }
    }

    public sealed class HearingEntry
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
        [JsonPropertyName("timing")] public string? Timing { get; init; }
        [JsonPropertyName("claims")] public List<ClaimEntry> Claims { get; init; } = [];
    }

    public sealed class ClaimEntry
    {
        [JsonPropertyName("timing")] public string? Timing { get; init; }
        [JsonPropertyName("quote")] public string? Quote { get; init; }
        [JsonPropertyName("book")] public int Book { get; init; }
    }
}
