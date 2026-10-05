using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>Narrator metadata needed by the Ilal rules.</summary>
public sealed record IlalNarrator(
    Guid Id,
    string Name,
    string? GradeEn,
    string? GenerationTier,
    int? MudallisTier,
    bool IsMukhtalit,
    string? IkhtilatNote,
    int? IbnHajarRank = null,
    IkhtilatSeverity? IkhtilatSeverity = null,
    bool NoHearingAfterIkhtilat = false)
{
    public int Tier => NarratorGradeScale.ToTier(IbnHajarRank, GradeEn);
    public bool IsCompanion => NarratorGradeScale.IsCompanion(IbnHajarRank, GradeEn, GenerationTier);
    public bool? IsTabii => NarratorGradeScale.IsTabii(IbnHajarRank, GradeEn, GenerationTier);
    public string GradeLabel => NarratorGradeScale.ToArabicLabel(IbnHajarRank, GradeEn);
}

/// <summary>A rule the books give for a group of a mukhtalit's students («من سمع منه قبل التغير»).</summary>
public sealed record MukhtalitGroupRuleInfo(string Group, HearingTiming Timing, string Quote);

/// <summary>One link of a chain: the student reports from the sheikh using <see cref="Term"/>.</summary>
public sealed record IlalLink(Guid StudentId, Guid SheikhId, string? Term, int StepOrder);

/// <summary>A single isnad (طريق) with its text, ordered from the compiler upward.</summary>
public sealed class IlalChain
{
    public required Guid HadithId { get; init; }
    public required string BookName { get; init; }
    public int HadithNumber { get; init; }
    public required string MatnArabic { get; init; }

    /// <summary>Links ordered from the compiler (step 1) up toward the Prophet ﷺ.</summary>
    public required IReadOnlyList<IlalLink> Links { get; init; }

    /// <summary>Narrators from the compiler (index 0) up to the top narrator.</summary>
    public IReadOnlyList<Guid> Path =>
        Links.Count == 0 ? [] : [Links[0].StudentId, .. Links.Select(l => l.SheikhId)];

    /// <summary>The highest narrator in the chain (usually the Companion).</summary>
    public Guid? TopNarratorId => Links.Count == 0 ? null : Links[^1].SheikhId;

    /// <summary>The narrator who received from <paramref name="narratorId"/> in this chain, if any.</summary>
    public Guid? StudentOf(Guid narratorId) =>
        Links.FirstOrDefault(l => l.SheikhId == narratorId)?.StudentId;

    /// <summary>The narrator from whom <paramref name="narratorId"/> received in this chain, if any.</summary>
    public IlalLink? LinkFrom(Guid narratorId) =>
        Links.FirstOrDefault(l => l.StudentId == narratorId);

    public bool Contains(Guid narratorId) => Path.Contains(narratorId);

    public string Label => $"{BookName} ({HadithNumber})";
}

/// <summary>Everything the rules need: the turuq plus narrator metadata, loaded in one batch.</summary>
public sealed class IlalContext
{
    public required IReadOnlyList<IlalChain> Chains { get; init; }
    public required IReadOnlyDictionary<Guid, IlalNarrator> Narrators { get; init; }

    /// <summary>Known (teacher, student) pairs among the narrators of these chains.</summary>
    public IReadOnlySet<(Guid TeacherId, Guid StudentId)> Relations { get; init; } = new HashSet<(Guid, Guid)>();

    /// <summary>Narrators that have any teacher/student data at all (to avoid false positives on sparse data).</summary>
    public IReadOnlySet<Guid> NarratorsWithRelations { get; init; } = new HashSet<Guid>();

    /// <summary>When each student heard from a mukhtalit narrator.</summary>
    public IReadOnlyDictionary<(Guid MukhtalitId, Guid StudentId), HearingTiming> Hearings { get; init; } =
        new Dictionary<(Guid, Guid), HearingTiming>();

    /// <summary>The critic's own words behind a recorded hearing, when the data has them.</summary>
    public IReadOnlyDictionary<(Guid MukhtalitId, Guid StudentId), string> HearingEvidence { get; init; } =
        new Dictionary<(Guid, Guid), string>();

    /// <summary>Group rules by mukhtalit narrator.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<MukhtalitGroupRuleInfo>> GroupRules { get; init; } =
        new Dictionary<Guid, IReadOnlyList<MukhtalitGroupRuleInfo>>();

    public IlalNarrator? Narrator(Guid id) => Narrators.GetValueOrDefault(id);

    public string NameOf(Guid id) => Narrators.TryGetValue(id, out var n) ? n.Name : "راوٍ غير معروف";

    public int TierOf(Guid id) => Narrators.TryGetValue(id, out var n) ? n.Tier : NarratorGradeScale.DefaultTier;
}
