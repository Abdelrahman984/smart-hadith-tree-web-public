using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// A rule the books give for a whole group of students of a mukhtalit narrator
/// (e.g. «من سمع منه بالبصرة قبل أن يقدم بغداد»), as opposed to a named student
/// (<see cref="MukhtalitHearing"/>).
/// </summary>
public class MukhtalitGroupRule
{
    public Guid Id { get; set; }

    /// <summary>FK to the narrator who suffered ikhtilat.</summary>
    public Guid MukhtalitId { get; set; }

    public Narrator Mukhtalit { get; set; } = null!;

    /// <summary>The group the rule speaks about, in the book's words.</summary>
    public string GroupAr { get; set; } = string.Empty;

    public HearingTiming Timing { get; set; }

    /// <summary>The book's words that state the rule (verbatim).</summary>
    public string Quote { get; set; } = string.Empty;

    public string? SourceBook { get; set; }
}
