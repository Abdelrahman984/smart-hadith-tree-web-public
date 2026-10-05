using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Etl.Parsers;

/// <summary>
/// Holds the complete parsed output from a data source, ready for bulk ingestion.
/// All GUIDs are pre-assigned client-side (sequential) so FK relationships are resolvable
/// in memory before any database round-trip.
/// </summary>
public class ParsedDataset
{
    public List<Narrator> Narrators { get; set; } = [];
    public List<HadithText> Hadiths { get; set; } = [];
    public List<Transmission> Transmissions { get; set; } = [];
    public List<ScholarEvaluation> ScholarEvaluations { get; set; } = [];

    /// <summary>Teacher/student relations known from the rijal books (the Shamela parser fills them).</summary>
    public List<NarratorRelation> NarratorRelations { get; set; } = [];

    /// <summary>Total entity count across all collections.</summary>
    public int TotalRecords =>
        Narrators.Count + Hadiths.Count + Transmissions.Count + ScholarEvaluations.Count + NarratorRelations.Count;
}
