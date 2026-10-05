using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Represents a Hadith narrator (الراوي) in the chain of transmission.
/// </summary>
public class Narrator
{
    public Guid Id { get; set; }

    /// <summary>الاسم الكامل — Full name with nasab (lineage).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>الشهرة / اللقب — The name the narrator is commonly known by.</summary>
    public string? KnownAs { get; set; }

    /// <summary>الكنية — Honorific (e.g., أبو هريرة).</summary>
    public string? Kunyah { get; set; }

    /// <summary>الطبقة — Generation tier (صحابي، تابعي، تابع التابعين...).</summary>
    public string? GenerationTier { get; set; }

    /// <summary>سنة الولادة بالهجري.</summary>
    public int? BirthYearHijri { get; set; }

    /// <summary>سنة الوفاة بالهجري.</summary>
    public int? DeathYearHijri { get; set; }

    /// <summary>مكان الولادة.</summary>
    public string? BirthPlace { get; set; }

    /// <summary>مكان الوفاة.</summary>
    public string? DeathPlace { get; set; }

    /// <summary>بلدان الإقامة والرحلة (مفصولة بفاصلة، مثل: البصرة ، الكوفة ، بغداد).</summary>
    public string? ResidencePlaces { get; set; }

    /// <summary>وصف الرتبة في جوامع الكلم (مثل: ثقة ثبت مشهور بالتدليس).</summary>
    public string? GawamiRank { get; set; }

    /// <summary>إجمالي عدد الأسانيد التي ورد فيها الراوي في جوامع الكلم.</summary>
    public int? TotalNarrationsCount { get; set; }

    /// <summary>عدد أطراف الأحاديث الفريدة التي رواها في جوامع الكلم.</summary>
    public int? UniqueHadithCount { get; set; }

    /// <summary>ترجمة الراوي — Biographical notes.</summary>
    public string? Biography { get; set; }

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>Transmissions where this narrator is the sheikh (teacher).</summary>
    public ICollection<Transmission> TransmissionsAsSheikh { get; set; } = [];

    /// <summary>Transmissions where this narrator is the student (receiver).</summary>
    public ICollection<Transmission> TransmissionsAsStudent { get; set; } = [];

    /// <summary>
    /// ID from the R3GENESI5/Itqan dataset (for mapping).
    /// </summary>
    /// <summary>
    /// Id of the narrator in the Shamela-built registry (<c>registry.json</c>), e.g. <c>tk3438s</c> or <c>rayy:260</c>.
    /// Replaces <see cref="ItqanId"/> once the migration is complete (Phase 7).
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>Id in Shamela's narrator encyclopedia (S1.db), when the narrator is linked there.</summary>
    public int? ShamelaManId { get; set; }

    /// <summary>Ibn Hajar's rank in Taqrib al-Tahdhib, 1 (Companion) to 12. Replaces <see cref="ItqanGrade"/>.</summary>
    public int? IbnHajarRank { get; set; }

    /// <summary>Ibn Hajar's verdict in Taqrib al-Tahdhib, in his own words.</summary>
    public string? Verdict { get; set; }

    /// <summary>Legacy Itqan id; removed in Phase 7.</summary>
    public int? ItqanId { get; set; }

    public int? GawamiId { get; set; }
    public bool IsMudallis { get; set; }
    public bool HasMukhtalit { get; set; }

    /// <summary>
    /// مرتبة التدليس — Ibn Hajr's tier in "طبقات المدلسين" (1–5). Null when not a known mudallis.
    /// Tiers 1–2 are tolerated; tiers 3+ require an explicit statement of hearing (تصريح بالسماع).
    /// </summary>
    public int? MudallisTier { get; set; }

    /// <summary>وصف الاختلاط — Note on when/how the narrator's memory deteriorated.</summary>
    public string? IkhtilatNote { get; set; }

    /// <summary>How the books rate his ikhtilat; null when he is not a mukhtalit.</summary>
    public IkhtilatSeverity? IkhtilatSeverity { get; set; }

    /// <summary>The books state that nobody heard from him after the ikhtilat.</summary>
    public bool NoHearingAfterIkhtilat { get; set; }

    public ICollection<MukhtalitGroupRule> MukhtalitGroupRules { get; set; } = [];

    /// <summary>Records of which students heard from this narrator before/after their ikhtilat.</summary>
    public ICollection<MukhtalitHearing> MukhtalitHearings { get; set; } = [];

    /// <summary>
    /// Standardized English grade from Itqan (reliable, weak, etc.) for UI color mapping.
    /// </summary>
    public string? ItqanGrade { get; set; }

    /// <summary>
    /// classical evaluation records
    /// </summary>
    public ICollection<ScholarEvaluation> ScholarEvaluations { get; set; } = new List<ScholarEvaluation>();
}
