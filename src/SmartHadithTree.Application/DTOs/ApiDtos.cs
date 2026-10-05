namespace SmartHadithTree.Application.DTOs;

public class HadithSearchResultDto
{
    public Guid Id { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string? Chapter { get; set; }
    public string MatnArabic { get; set; } = string.Empty;
    public string MatnSnippet { get; set; } = string.Empty;

    /// <summary>0-100: how well the matn matches the searched words (coverage, closeness, phrase/order). Null when not computed.</summary>
    public int? RelevancePercent { get; set; }

    /// <summary>One-line Arabic reason for <see cref="RelevancePercent"/>.</summary>
    public string? RelevanceReason { get; set; }
}

public class IsnadNodeDto
{
    public Guid Id { get; set; } // The Transmission Id or unique node id
    public Guid NarratorId { get; set; }
    public string NarratorName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? GenerationTier { get; set; }
    public int StepOrder { get; set; } // 1 = Compiler (Bukhari), higher = earlier (Sahabi)
    public Guid? ParentNodeId { get; set; } // Points to the student (who received it from this sheikh)
    public string? TransmissionTerm { get; set; } // حدثنا, عن
    public string? GradeEn { get; set; }

    /// <summary>مدلس — the narrator is listed in the mudallisin seed.</summary>
    public bool IsMudallis { get; set; }

    /// <summary>مختلط — the narrator's memory deteriorated late in life.</summary>
    public bool HasMukhtalit { get; set; }

    public string? ResidencePlaces { get; set; }
    public string? DeathPlace { get; set; }
    public string? GawamiRank { get; set; }
    public int? TotalNarrationsCount { get; set; }
    public int? UniqueHadithCount { get; set; }
    
    /// <summary>A proven break (انقطاع), e.g. the student was born after the sheikh died.</summary>
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }

    /// <summary>
    /// A hint for the researcher: the sheikh and student share no city or region. Scholars travelled
    /// widely, so this is not a break and never sets <see cref="IsAnomaly"/>.
    /// </summary>
    public string? TravelNote { get; set; }
}

public class IsnadTreeResponseDto
{
    public Guid HadithId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string MatnArabic { get; set; } = string.Empty;
    public List<IsnadNodeDto> Nodes { get; set; } = [];
}

public class NarratorSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? GenerationTier { get; set; }
    public string GradeSummary { get; set; } = string.Empty; // e.g. "ثقة", "ضعيف"
    public string? GradeEn { get; set; }
    public string? Tier { get; set; }
}

public class NarratorDetailDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? Kunyah { get; set; }
    public string? GenerationTier { get; set; }
    public int? BirthYearHijri { get; set; }
    public int? DeathYearHijri { get; set; }
    public string? ResidencePlaces { get; set; }
    public string? DeathPlace { get; set; }
    public string? GawamiRank { get; set; }
    public int? TotalNarrationsCount { get; set; }
    public int? UniqueHadithCount { get; set; }
    public bool IsMudallis { get; set; }
    public bool HasMukhtalit { get; set; }
    public string? Biography { get; set; }
    public string? GradeEn { get; set; }
    public List<ScholarEvaluationDto> Evaluations { get; set; } = [];
}

public class ScholarEvaluationDto
{
    public string ScholarName { get; set; } = string.Empty;
    public string EvaluationText { get; set; } = string.Empty;
    public string? SourceBook { get; set; }
    public string? VerdictRating { get; set; }
}

public class ExtractedAiEvaluationDto
{
    public string VerbatimQuote { get; set; } = string.Empty;
    public string SourceBook { get; set; } = string.Empty;
    public string Tier { get; set; } = string.Empty; // e.g. "T1", "T4"; empty unless Status is "ok"
    public string Justification { get; set; } = string.Empty;
    public string Status { get; set; } = AiEvaluationStatus.Ok;
}

/// <summary>Outcome of the AI narrator summary. Only <see cref="Ok"/> carries a quote and tier.</summary>
public static class AiEvaluationStatus
{
    public const string Ok = "ok";
    public const string NoEvaluations = "no-evaluations";
    public const string Unverified = "unverified";
    public const string Unavailable = "unavailable";
}

/// <summary>
/// Represents a source Hadith in the comparative (Takhreej) view.
/// </summary>
public class ComparativeHadithSourceDto
{
    public Guid HadithId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string MatnArabic { get; set; } = string.Empty;
    public string MatnSnippet { get; set; } = string.Empty;
}

/// <summary>
/// Extended node DTO that tracks which books/sources a narrator appears in
/// across multiple merged Isnad chains.
/// </summary>
public class ComparativeIsnadNodeDto : IsnadNodeDto
{
    /// <summary>Which source Hadith(s) this transmission belongs to.</summary>
    public List<Guid> SourceHadithIds { get; set; } = [];

    /// <summary>Which book(s) this narrator appears in for this cluster.</summary>
    public List<string> SourceBooks { get; set; } = [];

    /// <summary>Indicates if this node introduces a text variation.</summary>
    public bool HasMatnVariation { get; set; }

    /// <summary>The text diff or snippet highlighting the variation.</summary>
    public string? MatnVariationSnippet { get; set; }
}

/// <summary>
/// The unified tree response wrapping multiple sources into a single merged DAG.
/// </summary>
public class ComparativeTreeResponseDto
{
    public List<ComparativeHadithSourceDto> Sources { get; set; } = [];
    public List<ComparativeIsnadNodeDto> Nodes { get; set; } = [];
    public string? CalculatedGrade { get; set; }
    public string? TaqwiyahDetails { get; set; }

    /// <summary>نتيجة فحص العلل — hidden-defect analysis of the merged turuq.</summary>
    public IlalReportDto? IlalReport { get; set; }
}
