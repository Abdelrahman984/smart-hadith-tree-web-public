using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Records whether a student heard from a mukhtalit narrator (مختلط) before or after
/// the deterioration of their memory (قبل الاختلاط / بعد الاختلاط).
/// </summary>
public class MukhtalitHearing
{
    public Guid Id { get; set; }

    /// <summary>FK to the narrator who suffered ikhtilat.</summary>
    public Guid MukhtalitId { get; set; }

    public Narrator Mukhtalit { get; set; } = null!;

    /// <summary>FK to the student who heard from them.</summary>
    public Guid StudentId { get; set; }

    public HearingTiming Timing { get; set; }

    /// <summary>The critic's own words that state the timing (verbatim from the source book).</summary>
    public string? Evidence { get; set; }

    /// <summary>The book the evidence comes from (e.g. الكواكب النيرات).</summary>
    public string? SourceBook { get; set; }
}
