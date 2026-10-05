namespace SmartHadithTree.Etl.Parsers;

/// <summary>
/// Pluggable interface for data source parsers.
/// Each implementation reads a specific external format (JSON, XML, SQL dump)
/// and maps it to our domain entities.
/// </summary>
public interface IDataSourceParser
{
    /// <summary>A human-readable name for this parser (e.g., "Sunnah.com JSON Parser").</summary>
    string Name { get; }

    /// <summary>
    /// Determines if this parser can handle the given source path (by extension or content inspection).
    /// </summary>
    /// <param name="sourcePath">Path to the data file or directory.</param>
    bool CanParse(string sourcePath);

    /// <summary>
    /// Parses the data source and returns a <see cref="ParsedDataset"/> with all entities
    /// ready for bulk insertion. GUIDs must be pre-assigned.
    /// </summary>
    /// <param name="sourcePath">Path to the data file or directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fully populated <see cref="ParsedDataset"/>.</returns>
    Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default);
}
