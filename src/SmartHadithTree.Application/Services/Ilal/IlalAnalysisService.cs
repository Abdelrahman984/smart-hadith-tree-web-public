using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Runs the Ilal rule engine over a set of turuq. Narrator metadata (mudallis tiers, ikhtilat,
/// teacher/student relations) is loaded in a few batched queries, then every rule is evaluated
/// in memory against the same <see cref="IlalContext"/>.
/// </summary>
public class IlalAnalysisService(IHadithTreeDbContext context) : IIlalAnalysisService
{
    /// <summary>The rules run for every analysis, in reporting order.</summary>
    public static IReadOnlyList<IIlalRule> DefaultRules { get; } =
    [
        new TadlisRule(),
        new IkhtilatRule(),
        new HiddenInqitaRule(),
        new MatnAtMadarRule(),
        new RafWaqfRule(),
        new WaslIrsalRule()
    ];

    public async Task<IlalReportDto> AnalyzeAsync(IReadOnlyCollection<Guid> hadithIds, CancellationToken ct = default)
    {
        var ilalContext = await LoadContextAsync(hadithIds, ct);
        return Analyze(ilalContext);
    }

    /// <summary>Runs all rules against an already-loaded context.</summary>
    public static IlalReportDto Analyze(IlalContext ilalContext, IEnumerable<IIlalRule>? rules = null)
    {
        // Witnesses through another Companion are not routes to the same madar: the rules see the routes only.
        var mainCompanion = Shawahid.MainCompanion(ilalContext);
        var routes = ilalContext.Chains.Where(c => !Shawahid.IsShahid(ilalContext, c, mainCompanion)).ToList();
        var shawahidCount = ilalContext.Chains.Count - routes.Count;
        var ruleContext = shawahidCount == 0 ? ilalContext : ilalContext.WithChains(routes);

        var findings = (rules ?? DefaultRules)
            .SelectMany(r => r.Evaluate(ruleContext))
            .OrderByDescending(f => f.Severity)
            .ThenByDescending(f => f.Confidence)
            .ToList();

        var madars = IsnadBranching.FindSplitPoints(ruleContext)
            .Select(s => new IlalMadarDto
            {
                NarratorId = s.MadarId,
                NarratorName = ilalContext.NameOf(s.MadarId),
                BranchCount = s.Branches.Count,
                HadithIds = s.AllChains.Select(c => c.HadithId).Distinct().ToList()
            })
            .OrderByDescending(m => m.HadithIds.Count)
            .ToList();

        return new IlalReportDto
        {
            AnalyzedHadithIds = ilalContext.Chains.Select(c => c.HadithId).Distinct().ToList(),
            Turuq = ilalContext.Chains.Select(c =>
            {
                // The compiler (index 0) is not part of the strength of the isnad, only the narrators above him.
                var above = c.Path.Skip(1).ToList();
                var weakest = above
                    .Select((id, i) => (Id: id, Tier: ilalContext.TierOf(id), Index: i))
                    .Where(x => ilalContext.IsRanked(x.Id))
                    .OrderByDescending(x => x.Tier).ThenBy(x => x.Index)
                    .Cast<(Guid Id, int Tier, int Index)?>()
                    .FirstOrDefault();
                return new IlalTariqDto
                {
                    HadithId = c.HadithId,
                    BookName = c.BookName,
                    HadithNumber = c.HadithNumber,
                    IsMarfu = MatnText.IsMarfu(c.MatnArabic),
                    WeakestTier = weakest?.Tier,
                    WeakestNarratorId = weakest?.Id,
                    UnratedNarratorCount = above.Count(id => !ilalContext.IsRanked(id)),
                    CompanionId = Shawahid.CompanionOf(ilalContext, c),
                    CompanionName = Shawahid.CompanionOf(ilalContext, c) is { } companion ? ilalContext.NameOf(companion) : null,
                    IsShahid = Shawahid.IsShahid(ilalContext, c, mainCompanion)
                };
            }).ToList(),
            Madars = madars,
            Findings = findings,
            HasQadihah = findings.Any(f => f.Severity == IllahSeverity.Qadihah),
            SummaryAr = Summarize(findings, routes.Count, shawahidCount)
        };
    }

    private async Task<IlalContext> LoadContextAsync(IReadOnlyCollection<Guid> hadithIds, CancellationToken ct)
    {
        var ids = hadithIds.Distinct().ToList();

        var hadiths = await context.Hadiths.AsNoTracking()
            .Where(h => ids.Contains(h.Id))
            .Select(h => new { h.Id, h.BookName, h.HadithNumber, h.MatnArabic })
            .ToListAsync(ct);

        var transmissions = await context.Transmissions.AsNoTracking()
            .Where(t => ids.Contains(t.HadithId))
            .Select(t => new { t.HadithId, t.StudentId, t.SheikhId, t.TransmissionTerm, t.StepOrder })
            .ToListAsync(ct);

        var linksByHadith = transmissions
            .GroupBy(t => t.HadithId)
            .ToDictionary(g => g.Key, g => g.Select(t => new IlalLink(t.StudentId, t.SheikhId, t.TransmissionTerm, t.StepOrder)).ToList());

        // Keep the caller's order: the database returns rows in no particular order, and the rules
        // (representative chain per branch, clustering, tie-breaks) must not depend on it.
        // A tahwil isnad (several chains for one hadith) gives one chain per branch.
        var chains = hadiths
            .OrderBy(h => ids.IndexOf(h.Id))
            .SelectMany(h => BuildPaths(linksByHadith.GetValueOrDefault(h.Id) ?? [])
                .Select(path => new IlalChain
                {
                    HadithId = h.Id,
                    BookName = h.BookName,
                    HadithNumber = h.HadithNumber,
                    MatnArabic = h.MatnArabic,
                    Links = path
                }))
            .Where(c => c.Links.Count > 0)
            .ToList();

        var narratorIds = chains.SelectMany(c => c.Path).Distinct().ToList();

        var narratorRows = await context.Narrators.AsNoTracking()
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new
            {
                n.Id,
                n.FullName,
                n.KnownAs,
                n.Kunyah,
                n.ItqanGrade,
                n.IbnHajarRank,
                n.GenerationTier,
                n.MudallisTier,
                n.HasMukhtalit,
                n.IkhtilatNote,
                n.IkhtilatSeverity,
                n.NoHearingAfterIkhtilat
            })
            .ToListAsync(ct);

        var narrators = narratorRows.ToDictionary(
            n => n.Id,
            n => new IlalNarrator(
                n.Id,
                NarratorNameFormatter.FormatDisplayName(n.FullName, n.KnownAs, n.Kunyah),
                n.ItqanGrade,
                n.GenerationTier,
                n.MudallisTier,
                n.HasMukhtalit,
                n.IkhtilatNote,
                n.IbnHajarRank,
                n.IkhtilatSeverity,
                n.NoHearingAfterIkhtilat));

        var relations = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.TeacherId) && narratorIds.Contains(r.StudentId))
            .Select(r => new { r.TeacherId, r.StudentId })
            .ToListAsync(ct);

        var teachersWithData = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.TeacherId))
            .Select(r => r.TeacherId)
            .Distinct()
            .ToListAsync(ct);

        var studentsWithData = await context.NarratorRelations.AsNoTracking()
            .Where(r => narratorIds.Contains(r.StudentId))
            .Select(r => r.StudentId)
            .Distinct()
            .ToListAsync(ct);

        var hearings = await context.MukhtalitHearings.AsNoTracking()
            .Where(h => narratorIds.Contains(h.MukhtalitId))
            .Select(h => new { h.MukhtalitId, h.StudentId, h.Timing, h.Evidence, h.SourceBook })
            .ToListAsync(ct);

        var groupRules = await context.MukhtalitGroupRules.AsNoTracking()
            .Where(r => narratorIds.Contains(r.MukhtalitId))
            .Select(r => new { r.MukhtalitId, r.GroupAr, r.Timing, r.Quote })
            .ToListAsync(ct);

        return new IlalContext
        {
            Chains = chains,
            Narrators = narrators,
            Relations = relations.Select(r => (r.TeacherId, r.StudentId)).ToHashSet(),
            NarratorsWithRelations = teachersWithData.Concat(studentsWithData).ToHashSet(),
            Hearings = hearings.ToDictionary(h => (h.MukhtalitId, h.StudentId), h => h.Timing),
            HearingEvidence = hearings
                .Where(h => !string.IsNullOrWhiteSpace(h.Evidence))
                .ToDictionary(h => (h.MukhtalitId, h.StudentId),
                    h => string.IsNullOrWhiteSpace(h.SourceBook) ? h.Evidence! : $"{h.Evidence} ({h.SourceBook})"),
            GroupRules = groupRules
                .GroupBy(r => r.MukhtalitId)
                .ToDictionary(g => g.Key,
                    g => (IReadOnlyList<MukhtalitGroupRuleInfo>)g.Select(r => new MukhtalitGroupRuleInfo(r.GroupAr, r.Timing, r.Quote)).ToList())
        };
    }

    /// <summary>Most chains kept for one hadith (a tahwil isnad with many heads).</summary>
    private const int MaxPathsPerHadith = 16;

    /// <summary>
    /// Every chain of a hadith, each from the compiler upward. A tahwil isnad is stored as several chains that
    /// each start at step 1 (the same student, several first sheikhs), so a path is followed from every step-1
    /// link and splits wherever the next step has several links for the same student. Chains with the same
    /// narrators are returned once. A hadith with one chain gives one path.
    /// </summary>
    internal static List<List<IlalLink>> BuildPaths(IReadOnlyCollection<IlalLink> links)
    {
        var paths = new List<List<IlalLink>>();
        var seen = new HashSet<string>();

        void Walk(IlalLink current, List<IlalLink> path, HashSet<Guid> visited)
        {
            if (paths.Count >= MaxPathsPerHadith) return;
            path.Add(current);
            visited.Add(current.SheikhId);

            // The next step; when the steps have a gap (older data) the nearest later step is used.
            var candidates = links
                .Where(l => l.StudentId == current.SheikhId && l.StepOrder > current.StepOrder && !visited.Contains(l.SheikhId))
                .ToList();
            if (candidates.Count > 0)
            {
                var step = candidates.Min(l => l.StepOrder);
                foreach (var next in candidates.Where(l => l.StepOrder == step))
                    Walk(next, [.. path], [.. visited]);
            }
            else if (seen.Add(string.Join('>', path.Select(l => l.SheikhId))))
            {
                paths.Add(path);
            }
        }

        foreach (var first in links.Where(l => l.StepOrder == 1))
            Walk(first, [], []);

        return paths;
    }

    private static string Summarize(IReadOnlyCollection<IlalFindingDto> findings, int chainCount, int shawahidCount)
    {
        var shawahidNote = shawahidCount == 0 ? "" : $" وفُصل {shawahidCount} {(shawahidCount == 1 ? "شاهد" : "شواهد")} عن صحابي آخر فلم يدخل في المقارنة.";
        if (chainCount == 0)
            return "لا توجد أسانيد مستخرجة لهذه الأحاديث، فتعذّر فحص العلل.";

        if (findings.Count == 0)
            return chainCount == 1
                ? "لم تظهر علة في هذا الإسناد بحسب القواعد الآلية. ويُستحسن جمع الطرق للكشف عن العلل الخفية." + shawahidNote
                : $"لم تظهر علة في الطرق المجموعة ({chainCount}) بحسب القواعد الآلية.{shawahidNote}";

        var qadihah = findings.Count(f => f.Severity == IllahSeverity.Qadihah);
        var ghayr = findings.Count(f => f.Severity == IllahSeverity.GhayrQadihah);
        var tanbih = findings.Count(f => f.Severity == IllahSeverity.Tanbih);

        var parts = new List<string>();
        if (qadihah > 0) parts.Add($"{qadihah} علة قادحة");
        if (ghayr > 0) parts.Add($"{ghayr} علة غير قادحة");
        if (tanbih > 0) parts.Add($"{tanbih} تنبيه");

        return $"ظهر بعد فحص {chainCount} {(chainCount == 1 ? "طريق" : "طرق")}: {string.Join("، ", parts)}. وهذه نتائج آلية تُعين المحقق ولا تغني عن نظره.{shawahidNote}";
    }
}
