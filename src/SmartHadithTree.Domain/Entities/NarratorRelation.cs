namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// A known teacher → student relationship (الشيوخ والتلاميذ) from the rijal sources.
/// Used to verify meeting/hearing (اللقاء والسماع) independently of any single chain.
/// </summary>
public class NarratorRelation
{
    public Guid Id { get; set; }

    /// <summary>FK to the teacher (الشيخ).</summary>
    public Guid TeacherId { get; set; }

    /// <summary>FK to the student (التلميذ).</summary>
    public Guid StudentId { get; set; }

    /// <summary>Where the relation came from (e.g. "itqan", "seed").</summary>
    public string Source { get; set; } = string.Empty;
}
