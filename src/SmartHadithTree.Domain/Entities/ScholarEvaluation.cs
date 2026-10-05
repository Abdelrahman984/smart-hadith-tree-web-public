namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Represents a scholar's evaluation (قول من أقوال الجرح والتعديل) of a narrator.
/// These raw textual records are the grounding data for the AI RAG pipeline.
/// </summary>
public class ScholarEvaluation
{
    public Guid Id { get; set; }

    // ── Narrator Reference ─────────────────────────────────────────

    /// <summary>FK to the narrator being evaluated.</summary>
    public Guid NarratorId { get; set; }

    /// <summary>Navigation to the evaluated narrator.</summary>
    public Narrator Narrator { get; set; } = null!;

    public int? GawamiAlemId { get; set; }
    public int? GawamiRawyId { get; set; }

    // ── Evaluation Data ────────────────────────────────────────────

    /// <summary>اسم الناقد — The scholar/critic's name (e.g., ابن حجر، الذهبي، يحيى بن معين).</summary>
    public string ScholarName { get; set; } = string.Empty;

    /// <summary>نص القول — The raw evaluation text as stated by the scholar.</summary>
    public string EvaluationText { get; set; } = string.Empty;

    /// <summary>المصدر — The source book (e.g., تهذيب التهذيب، الجرح والتعديل).</summary>
    public string? SourceBook { get; set; }

    /// <summary>Printed volume of <see cref="SourceBook"/> that holds the quote, when known.</summary>
    public string? SourceVolume { get; set; }

    /// <summary>Printed page of <see cref="SourceBook"/> that holds the quote, when known.</summary>
    public int? SourcePage { get; set; }

    /// <summary>
    /// تصنيف الحكم — A categorical rating (e.g., ثقة، صدوق، ضعيف، متروك، كذاب).
    /// Used for color-coding in the UI and for structured querying.
    /// </summary>
    public string? VerdictRating { get; set; }
}
