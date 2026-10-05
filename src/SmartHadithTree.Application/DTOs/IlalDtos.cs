using System.Text.Json.Serialization;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.DTOs;

/// <summary>
/// Result of the Ilal (hidden defects) analysis over one or more turuq of a hadith.
/// Findings are heuristic aids for the researcher (المحقق), not a final verdict.
/// </summary>
public class IlalReportDto
{
    /// <summary>The hadiths (turuq) that were analyzed.</summary>
    public List<Guid> AnalyzedHadithIds { get; set; } = [];

    /// <summary>Source details of each analyzed tariq, for labelling findings.</summary>
    public List<IlalTariqDto> Turuq { get; set; } = [];

    /// <summary>Common links (المدار) where the analyzed turuq branch apart.</summary>
    public List<IlalMadarDto> Madars { get; set; } = [];

    public List<IlalFindingDto> Findings { get; set; } = [];

    /// <summary>True when at least one finding is a decisive defect (علة قادحة).</summary>
    public bool HasQadihah { get; set; }

    /// <summary>A short Arabic summary of the findings.</summary>
    public string SummaryAr { get; set; } = string.Empty;
}

/// <summary>One analyzed tariq (a hadith in a specific book).</summary>
public class IlalTariqDto
{
    public Guid HadithId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }

    /// <summary>True when the text is attributed to the Prophet ﷺ (مرفوع).</summary>
    public bool IsMarfu { get; set; }

    /// <summary>
    /// Tier (1 = Companion … 12 = fabricator) of the weakest narrator in this tariq, excluding the compiler.
    /// Null when the chain could not be resolved at all.
    /// </summary>
    public int? WeakestTier { get; set; }

    /// <summary>The weakest narrator of this tariq (the first one met going up, on a tie).</summary>
    public Guid? WeakestNarratorId { get; set; }
}

/// <summary>A narrator at which two or more turuq diverge.</summary>
public class IlalMadarDto
{
    public Guid NarratorId { get; set; }
    public string NarratorName { get; set; } = string.Empty;

    /// <summary>Number of distinct students (branches) narrating from this narrator.</summary>
    public int BranchCount { get; set; }

    public List<Guid> HadithIds { get; set; } = [];
}

/// <summary>A single detected defect with the evidence that triggered it.</summary>
public class IlalFindingDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IllahType Type { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public IllahSeverity Severity { get; set; }

    /// <summary>Arabic title, e.g. "عنعنة مدلس".</summary>
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>Arabic explanation of the evidence behind this finding.</summary>
    public string EvidenceAr { get; set; } = string.Empty;

    /// <summary>Narrators involved (used by the UI to highlight nodes).</summary>
    public List<Guid> NarratorIds { get; set; } = [];

    /// <summary>The turuq (hadiths) affected by this defect.</summary>
    public List<Guid> HadithIds { get; set; } = [];

    /// <summary>Heuristic confidence between 0 and 1.</summary>
    public double Confidence { get; set; }

    /// <summary>Aligned matn comparison, for textual findings (زيادة / شذوذ / اضطراب).</summary>
    public MatnComparisonDto? MatnComparison { get; set; }
}

/// <summary>Token-level comparison between a reference matn and a compared matn.</summary>
public class MatnComparisonDto
{
    public Guid ReferenceHadithId { get; set; }
    public Guid ComparedHadithId { get; set; }
    public double Similarity { get; set; }
    public List<MatnSegmentDto> Segments { get; set; } = [];
}

/// <summary>A run of words that is equal, only in the reference, or only in the compared text.</summary>
public class MatnSegmentDto
{
    /// <summary>"equal", "added" (only in compared) or "removed" (only in reference).</summary>
    public string Kind { get; set; } = "equal";

    public string Text { get; set; } = string.Empty;
}

/// <summary>AI-written explanation of an Ilal report, grounded only in its findings.</summary>
public class IlalExplanationDto
{
    public string ExplanationAr { get; set; } = string.Empty;
    public List<string> Caveats { get; set; } = [];
}
