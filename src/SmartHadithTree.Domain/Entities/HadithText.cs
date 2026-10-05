namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Represents the core text of a Hadith (المتن) along with its source reference.
/// </summary>
public class HadithText
{
    public Guid Id { get; set; }

    /// <summary>متن الحديث — The actual Arabic text of the Hadith.</summary>
    public string MatnArabic { get; set; } = string.Empty;
    public string NormalizedMatn { get; set; } = string.Empty;

    /// <summary>اسم الكتاب — Source book name (e.g., صحيح البخاري).</summary>
    public string BookName { get; set; } = string.Empty;
    public string NormalizedBookName { get; set; } = string.Empty;

    /// <summary>رقم الحديث — The Hadith number within the source book.</summary>
    public int HadithNumber { get; set; }

    /// <summary>المجلد — Volume number or identifier.</summary>
    public string? Volume { get; set; }

    /// <summary>الباب — Chapter or section heading.</summary>
    public string? Chapter { get; set; }

    /// <summary>نص السند الكامل — The raw, unparsed Isnad text as it appears in the source.</summary>
    public string? FullIsnadText { get; set; }

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>All transmission links (chain steps) for this Hadith.</summary>
    public ICollection<Transmission> Transmissions { get; set; } = [];

    public Guid? HadithClusterId { get; set; }
    public HadithCluster? Cluster { get; set; }
}
