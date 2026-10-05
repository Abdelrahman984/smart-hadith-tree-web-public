using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Parsers;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Etl.Services;

/// <summary>
/// High-performance bulk data ingestion using EFCore.BulkExtensions.
/// Inserts entities in FK-dependency order within a single transaction.
/// </summary>
public class BulkDataIngestionService(
    HadithTreeDbContext context,
    ILogger<BulkDataIngestionService> logger)
{
    private static readonly BulkConfig DefaultBulkConfig = new()
    {
        BatchSize = 10_000,
        BulkCopyTimeout = 600,          // 10 minutes
        SetOutputIdentity = false,      // GUIDs are pre-assigned client-side
        PreserveInsertOrder = true,
        SqlBulkCopyOptions =
            SqlBulkCopyOptions.CheckConstraints |
            SqlBulkCopyOptions.TableLock |
            SqlBulkCopyOptions.KeepIdentity
    };

    /// <summary>
    /// Bulk-inserts all entities from a <see cref="ParsedDataset"/> into the database.
    /// Entities are inserted in FK-dependency order: Narrators → Hadiths → Transmissions → ScholarEvaluations.
    /// The entire operation is wrapped in a single transaction.
    /// </summary>
    public async Task IngestAsync(ParsedDataset dataset, CancellationToken ct = default)
    {
        if (dataset.TotalRecords == 0)
        {
            logger.LogWarning("Empty dataset provided, nothing to ingest.");
            return;
        }

        logger.LogInformation(
            "Starting bulk ingestion: {Narrators} narrators, {Hadiths} hadiths, " +
            "{Transmissions} transmissions, {Evaluations} evaluations.",
            dataset.Narrators.Count, dataset.Hadiths.Count,
            dataset.Transmissions.Count, dataset.ScholarEvaluations.Count);

        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        try
        {
            // 1. Independent entities first (no FK dependencies)
            if (dataset.Narrators.Count > 0)
            {
                logger.LogInformation("Inserting {Count} narrators...", dataset.Narrators.Count);
                await context.BulkInsertAsync(dataset.Narrators, DefaultBulkConfig, cancellationToken: ct);
            }

            if (dataset.NarratorRelations.Count > 0)
            {
                logger.LogInformation("Inserting {Count} narrator relations...", dataset.NarratorRelations.Count);
                await context.BulkInsertAsync(dataset.NarratorRelations, DefaultBulkConfig, cancellationToken: ct);
            }

            if (dataset.Hadiths.Count > 0)
            {
                logger.LogInformation("Inserting {Count} hadiths...", dataset.Hadiths.Count);
                await context.BulkInsertAsync(dataset.Hadiths, DefaultBulkConfig, cancellationToken: ct);
            }

            // 2. Dependent entities (require Narrators and Hadiths to exist)
            if (dataset.Transmissions.Count > 0)
            {
                logger.LogInformation("Inserting {Count} transmissions...", dataset.Transmissions.Count);
                await context.BulkInsertAsync(dataset.Transmissions, DefaultBulkConfig, cancellationToken: ct);
            }

            if (dataset.ScholarEvaluations.Count > 0)
            {
                logger.LogInformation("Inserting {Count} scholar evaluations...", dataset.ScholarEvaluations.Count);
                await context.BulkInsertAsync(dataset.ScholarEvaluations, DefaultBulkConfig, cancellationToken: ct);
            }

            await transaction.CommitAsync(ct);

            logger.LogInformation(
                "Bulk ingestion committed successfully. Total: {Total} records.",
                dataset.TotalRecords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bulk ingestion failed. Rolling back transaction.");
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    /// <summary>
    /// Performs an upsert (insert-or-update) for narrators, useful for re-running
    /// imports without creating duplicates.
    /// </summary>
    public async Task UpsertNarratorsAsync(ParsedDataset dataset, CancellationToken ct = default)
    {
        if (dataset.Narrators.Count == 0) return;

        var upsertConfig = new BulkConfig
        {
            BatchSize = 5_000,
            SetOutputIdentity = false,
            UpdateByProperties = [nameof(Domain.Entities.Narrator.FullName)]
        };

        logger.LogInformation("Upserting {Count} narrators...", dataset.Narrators.Count);
        await context.BulkInsertOrUpdateAsync(dataset.Narrators, upsertConfig, cancellationToken: ct);
    }

    /// <summary>
    /// Replaces all teacher/student relations from the given source (e.g. "itqan") in one transaction.
    /// </summary>
    public async Task ReplaceNarratorRelationsAsync(
        string source, List<Domain.Entities.NarratorRelation> relations, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var deleted = await context.NarratorRelations.Where(r => r.Source == source).ExecuteDeleteAsync(ct);
        logger.LogInformation("Removed {Count} existing '{Source}' relations.", deleted, source);

        logger.LogInformation("Inserting {Count} narrator relations...", relations.Count);
        await context.BulkInsertAsync(relations, DefaultBulkConfig, cancellationToken: ct);

        await transaction.CommitAsync(ct);
    }
}
