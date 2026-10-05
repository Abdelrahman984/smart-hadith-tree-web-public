namespace SmartHadithTree.Etl.Models;

/// <summary>
/// A lightweight memory model for the narrator profile loaded from JSON,
/// used primarily for the Contextual Disambiguation Engine's graph traversal.
/// </summary>
public class NarratorNode
{
    public int ItqanId { get; set; }
    
    public string FullName { get; set; } = string.Empty;
    
    /// <summary>
    /// Generation / Tabaqat tier, useful as a fallback heuristic.
    /// </summary>
    public int? Generation { get; set; }

    /// <summary>
    /// Parsed score from Itqan dataset for tie-breaking.
    /// </summary>
    public int IdScore { get; set; }

    /// <summary>
    /// Grade score (reliability) for tie-breaking.
    /// </summary>
    public int GradeScore { get; set; }

    /// <summary>
    /// Ids of narrators who taught this narrator.
    /// </summary>
    public HashSet<int> Teachers { get; set; } = [];

    /// <summary>
    /// Ids of narrators who learned from this narrator.
    /// </summary>
    public HashSet<int> Students { get; set; } = [];
}
