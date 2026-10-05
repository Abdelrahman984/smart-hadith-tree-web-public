namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Represents a single link in the Isnad chain (السند).
/// This is a ternary join entity connecting a Sheikh (teacher) to a Student (receiver)
/// for a specific Hadith at a specific step in the chain.
/// </summary>
public class Transmission
{
    public Guid Id { get; set; }

    // ── Hadith Reference ───────────────────────────────────────────

    /// <summary>FK to the Hadith this transmission belongs to.</summary>
    public Guid HadithId { get; set; }

    /// <summary>Navigation to the associated Hadith.</summary>
    public HadithText Hadith { get; set; } = null!;

    // ── Sheikh (Teacher / المروي عنه) ──────────────────────────────

    /// <summary>FK to the narrator who transmitted (the sheikh / الشيخ).</summary>
    public Guid SheikhId { get; set; }

    /// <summary>Navigation to the sheikh narrator.</summary>
    public Narrator Sheikh { get; set; } = null!;

    // ── Student (Receiver / الراوي) ────────────────────────────────

    /// <summary>FK to the narrator who received (the student / التلميذ).</summary>
    public Guid StudentId { get; set; }

    /// <summary>Navigation to the student narrator.</summary>
    public Narrator Student { get; set; } = null!;

    // ── Chain Metadata ─────────────────────────────────────────────

    /// <summary>
    /// الترتيب في السند — Position in the chain.
    /// 1 = the compiler/author (e.g., البخاري), incrementing upward toward the Prophet ﷺ.
    /// </summary>
    public int StepOrder { get; set; }

    /// <summary>
    /// صيغة الأداء — The transmission formula used (e.g., حدثنا، أخبرنا، عن، سمعت).
    /// </summary>
    public string? TransmissionTerm { get; set; }
}
