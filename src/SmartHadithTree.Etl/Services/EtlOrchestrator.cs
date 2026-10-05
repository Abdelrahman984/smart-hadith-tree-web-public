using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Parsers;

namespace SmartHadithTree.Etl.Services;

/// <summary>
/// Orchestrates the full ETL pipeline: selects the appropriate parser for the
/// data source, parses the data, and hands it off for bulk ingestion.
/// </summary>
public class EtlOrchestrator(
    IEnumerable<IDataSourceParser> parsers,
    BulkDataIngestionService ingestionService,
    ILogger<EtlOrchestrator> logger)
{
    /// <summary>
    /// Runs the ETL pipeline for the given source path.
    /// Automatically selects the correct parser based on the source.
    /// </summary>
    /// <param name="sourcePath">
    /// Path to a data file, directory of files, or the keyword "seed" for test data.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RunAsync(string sourcePath, CancellationToken ct = default)
    {
        logger.LogInformation("ETL pipeline starting for source: {Source}", sourcePath);
        var stopwatch = Stopwatch.StartNew();

        // 1. Find a parser that can handle this source
        var parser = parsers.FirstOrDefault(p => p.CanParse(sourcePath));
        if (parser is null)
        {
            logger.LogError(
                "No parser found for source: {Source}. Available parsers: {Parsers}",
                sourcePath,
                string.Join(", ", parsers.Select(p => p.Name)));
            throw new InvalidOperationException($"No parser can handle source: {sourcePath}");
        }

        logger.LogInformation("Selected parser: {Parser}", parser.Name);

        // 2. Parse the data source
        var dataset = await parser.ParseAsync(sourcePath, ct);

        logger.LogInformation(
            "Parsing complete in {Elapsed:F1}s. Records: {Total} " +
            "(Narrators: {N}, Hadiths: {H}, Transmissions: {T}, Evaluations: {E})",
            stopwatch.Elapsed.TotalSeconds,
            dataset.TotalRecords,
            dataset.Narrators.Count,
            dataset.Hadiths.Count,
            dataset.Transmissions.Count,
            dataset.ScholarEvaluations.Count);

        if (dataset.TotalRecords == 0)
        {
            logger.LogWarning("Parsed dataset is empty. Nothing to ingest.");
            return;
        }

        // 3. Bulk ingest into SQL Server
        await ingestionService.IngestAsync(dataset, ct);

        stopwatch.Stop();
        logger.LogInformation(
            "ETL pipeline completed successfully in {Elapsed:F1}s. " +
            "Total records ingested: {Total}.",
            stopwatch.Elapsed.TotalSeconds,
            dataset.TotalRecords);
    }
}
