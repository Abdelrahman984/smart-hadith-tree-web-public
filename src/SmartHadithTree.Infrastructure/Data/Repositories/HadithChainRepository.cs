using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;

using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Infrastructure.Data.Repositories;

public class HadithChainRepository(HadithTreeDbContext context) : IHadithChainRepository
{
    /// <summary>
    /// Traverses the Isnad graph for a given Hadith using a Recursive CTE,
    /// returning all narrators in the chain from the compiler up to the original source.
    /// </summary>
    public async Task<List<IsnadNodeDto>> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default)
    {
        // We use a Recursive CTE to walk the graph.
        // Anchor: The Student in StepOrder 1 (the Compiler, e.g. Al-Bukhari).
        // Recursive: Join the Transmissions table where StudentId matches the parent's NarratorId.
        
        var sql = @"
            WITH RecursiveChain AS (
                -- Anchor: The Compiler (Student of Step 1)
                -- DISTINCT: a tahwil isnad has several step-1 rows (one per chain) for the same compiler
                SELECT DISTINCT
                    CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER) AS Id,
                    t.StudentId AS NarratorId,
                    0 AS StepOrder,
                    CAST(NULL AS UNIQUEIDENTIFIER) AS ParentNodeId,
                    CAST(NULL AS NVARCHAR(50)) AS TransmissionTerm,
                    t.HadithId
                FROM Transmissions t
                WHERE t.HadithId = {0} AND t.StepOrder = 1

                UNION ALL

                -- Recursive: The Sheikh of the current Narrator
                SELECT 
                    child.Id,
                    child.SheikhId AS NarratorId,
                    child.StepOrder,
                    parent.Id AS ParentNodeId,
                    child.TransmissionTerm,
                    child.HadithId
                FROM Transmissions child
                INNER JOIN RecursiveChain parent 
                    ON child.StudentId = parent.NarratorId 
                    AND child.HadithId = parent.HadithId
                    AND child.StepOrder = parent.StepOrder + 1
            )
            SELECT 
                rc.Id,
                rc.NarratorId,
                n.FullName AS NarratorName,
                n.KnownAs,
                n.GenerationTier,
                rc.StepOrder,
                rc.ParentNodeId,
                rc.TransmissionTerm,
                COALESCE(n.ItqanGrade, CASE n.IbnHajarRank WHEN 1 THEN 'companion' WHEN 2 THEN 'reliable' WHEN 3 THEN 'reliable' WHEN 4 THEN 'mostly_reliable' WHEN 5 THEN 'mostly_reliable' WHEN 6 THEN 'weak' WHEN 7 THEN 'weak' WHEN 8 THEN 'weak' WHEN 9 THEN 'unknown' WHEN 10 THEN 'abandoned' WHEN 11 THEN 'abandoned' WHEN 12 THEN 'fabricator' END) AS GradeEn,
                n.IsMudallis,
                n.HasMukhtalit,
                n.ResidencePlaces,
                n.DeathPlace,
                n.GawamiRank,
                n.TotalNarrationsCount,
                n.UniqueHadithCount,
                CAST(0 AS BIT) AS IsAnomaly,
                CAST(NULL AS NVARCHAR(MAX)) AS AnomalyReason,
                CAST(NULL AS NVARCHAR(MAX)) AS TravelNote
            FROM RecursiveChain rc
            INNER JOIN Narrators n ON rc.NarratorId = n.Id
            ORDER BY rc.StepOrder ASC;
        ";

        // Execute raw SQL mapping to the DTO directly
        var nodes = await context.Database
            .SqlQueryRaw<IsnadNodeDto>(sql, hadithId)
            .ToListAsync(ct);

        // Fetch birth/death years and places for the narrators in this chain
        var narratorIds = nodes.Select(n => n.NarratorId).Distinct().ToList();
        var narratorDates = await context.Narrators
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new { n.Id, n.BirthYearHijri, n.DeathYearHijri, n.ResidencePlaces, n.DeathPlace })
            .ToDictionaryAsync(n => n.Id, ct);

        // Detect anomalies (Temporal & Geographic Inqita')
        foreach (var node in nodes)
        {
            if (node.ParentNodeId.HasValue && node.ParentNodeId.Value != Guid.Empty)
            {
                var studentNode = nodes.FirstOrDefault(n => n.Id == node.ParentNodeId.Value);
                if (studentNode != null && narratorDates.TryGetValue(node.NarratorId, out var sheikhMeta) && narratorDates.TryGetValue(studentNode.NarratorId, out var studentMeta))
                {
                    // 1. Temporal check: If student was born AFTER sheikh died
                    if (studentMeta.BirthYearHijri.HasValue && sheikhMeta.DeathYearHijri.HasValue && 
                        studentMeta.BirthYearHijri.Value > sheikhMeta.DeathYearHijri.Value)
                    {
                        node.IsAnomaly = true;
                        node.AnomalyReason = $"انقطاع زمني: التلميذ ولد سنة {studentMeta.BirthYearHijri} بعد وفاة الشيخ سنة {sheikhMeta.DeathYearHijri}";
                    }
                    // 2. Geographic hint (not a break): no shared city or region
                    else if (PlaceRegions.HaveNothingInCommon(sheikhMeta.ResidencePlaces, sheikhMeta.DeathPlace, studentMeta.ResidencePlaces, studentMeta.DeathPlace, out var travelNote))
                    {
                        node.TravelNote = travelNote;
                    }
                }
            }
        }

        return nodes;
    }

    /// <summary>
    /// Traverses the Isnad graphs for multiple Hadiths using a multi-anchor Recursive CTE,
    /// merging shared narrators into unified nodes with source attribution.
    /// </summary>
    public async Task<List<ComparativeIsnadNodeDto>> GetComparativeIsnadTreeAsync(
        List<Guid> hadithIds, CancellationToken ct = default)
    {
        if (hadithIds.Count == 0) return [];

        // Build parameterized placeholders for the IN clause
        var paramPlaceholders = string.Join(", ", hadithIds.Select((_, i) => $"{{{i}}}"));
        var parameters = hadithIds.Cast<object>().ToArray();

        var sql = $@"
            WITH ChainRows AS (
                -- Anchor: Distinct Compiler (Student of Step 1) per Hadith
                SELECT 
                    NEWID() AS Id,
                    c.StudentId AS NarratorId,
                    0 AS StepOrder,
                    CAST(NULL AS UNIQUEIDENTIFIER) AS ParentNodeId,
                    CAST(NULL AS NVARCHAR(50)) AS TransmissionTerm,
                    c.HadithId
                FROM (
                    SELECT DISTINCT t.StudentId, t.HadithId
                    FROM Transmissions t
                    WHERE t.HadithId IN ({paramPlaceholders}) AND t.StepOrder = 1
                ) c

                UNION ALL

                -- Edges: Every transmission step (Sheikh -> Student) for the selected Hadiths
                SELECT 
                    t.Id,
                    t.SheikhId AS NarratorId,
                    t.StepOrder,
                    t.StudentId AS ParentNodeId,
                    t.TransmissionTerm,
                    t.HadithId
                FROM Transmissions t
                WHERE t.HadithId IN ({paramPlaceholders})
            )
            SELECT 
                cr.Id,
                cr.NarratorId,
                n.FullName AS NarratorName,
                n.KnownAs,
                n.GenerationTier,
                cr.StepOrder,
                cr.ParentNodeId,
                cr.TransmissionTerm,
                COALESCE(n.ItqanGrade, CASE n.IbnHajarRank WHEN 1 THEN 'companion' WHEN 2 THEN 'reliable' WHEN 3 THEN 'reliable' WHEN 4 THEN 'mostly_reliable' WHEN 5 THEN 'mostly_reliable' WHEN 6 THEN 'weak' WHEN 7 THEN 'weak' WHEN 8 THEN 'weak' WHEN 9 THEN 'unknown' WHEN 10 THEN 'abandoned' WHEN 11 THEN 'abandoned' WHEN 12 THEN 'fabricator' END) AS GradeEn,
                n.IsMudallis,
                n.HasMukhtalit,
                n.ResidencePlaces,
                n.DeathPlace,
                n.GawamiRank,
                n.TotalNarrationsCount,
                n.UniqueHadithCount,
                CAST(0 AS BIT) AS IsAnomaly,
                CAST(NULL AS NVARCHAR(MAX)) AS AnomalyReason,
                cr.HadithId AS SourceHadithId,
                h.BookName AS SourceBookName
            FROM ChainRows cr
            INNER JOIN Narrators n ON cr.NarratorId = n.Id
            INNER JOIN Hadiths h ON cr.HadithId = h.Id
            ORDER BY cr.StepOrder ASC;
        ";

        // Execute raw SQL — returns flat rows with per-hadith attribution
        var rawRows = await context.Database
            .SqlQueryRaw<ComparativeRawRow>(sql, parameters)
            .ToListAsync(ct);

        // Merge: group by NarratorId to collapse shared nodes
        var mergedNodes = new List<ComparativeIsnadNodeDto>();
        var narratorGroups = rawRows.GroupBy(r => r.NarratorId);

        foreach (var group in narratorGroups)
        {
            var first = group.First();
            var minStep = group.Min(r => r.StepOrder);

            // If this scholar is a compiler (StepOrder == 0) in at least one selected hadith,
            // keep the primary ReferenceNode's SourceHadithIds/SourceBooks scoped to the books they compiled,
            // and emit any intermediate-narrator links (where they are a teacher of a later compiler) as edge nodes.
            var attributionRows = minStep == 0
                ? group.Where(r => r.StepOrder == 0).ToList()
                : group.ToList();

            mergedNodes.Add(new ComparativeIsnadNodeDto
            {
                Id = first.Id,
                NarratorId = first.NarratorId,
                NarratorName = first.NarratorName,
                KnownAs = first.KnownAs,
                GenerationTier = first.GenerationTier,
                StepOrder = minStep,
                ParentNodeId = null,
                TransmissionTerm = first.TransmissionTerm,
                GradeEn = first.GradeEn,
                IsMudallis = first.IsMudallis,
                HasMukhtalit = first.HasMukhtalit,
                ResidencePlaces = first.ResidencePlaces,
                DeathPlace = first.DeathPlace,
                GawamiRank = first.GawamiRank,
                TotalNarrationsCount = first.TotalNarrationsCount,
                UniqueHadithCount = first.UniqueHadithCount,
                SourceHadithIds = attributionRows.Select(r => r.SourceHadithId).Distinct().ToList(),
                SourceBooks = attributionRows.Select(r => r.SourceBookName).Distinct().ToList()
            });
        }

        var mergedByNarratorId = mergedNodes.ToDictionary(n => n.NarratorId);

        // Helper to validate a directed edge (Sheikh -> Student) and prevent self-loops or non-companions above companions
        bool IsValidEdge(ComparativeRawRow sheikhRow, out ComparativeIsnadNodeDto? studentNode)
        {
            studentNode = null;
            if (sheikhRow.StepOrder <= 0 || !sheikhRow.ParentNodeId.HasValue || sheikhRow.ParentNodeId.Value == Guid.Empty)
                return false;

            if (!mergedByNarratorId.TryGetValue(sheikhRow.ParentNodeId.Value, out var student) ||
                student.NarratorId == sheikhRow.NarratorId)
            {
                return false;
            }

            // A Companion (1st generation) cannot have a non-Companion Sheikh above them
            if (string.Equals(student.GradeEn, "companion", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sheikhRow.GradeEn, "companion", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            studentNode = student;
            return true;
        }

        // Group valid directed edges (SheikhNarratorId -> StudentNarratorId) with their exact per-edge hadith/book attribution
        var edgeGroups = rawRows
            .Select(r => (Row: r, HasValidEdge: IsValidEdge(r, out var s), StudentNode: s))
            .Where(x => x.HasValidEdge && x.StudentNode != null)
            .GroupBy(x => (SheikhId: x.Row.NarratorId, StudentId: x.StudentNode!.NarratorId))
            .ToList();
        var additionalEdgeNodes = new List<ComparativeIsnadNodeDto>();

        // Attach primary and additional edges from edgeGroups
        foreach (var edgesForSheikh in edgeGroups.GroupBy(g => g.Key.SheikhId))
        {
            if (!mergedByNarratorId.TryGetValue(edgesForSheikh.Key, out var sheikhNode))
                continue;

            bool isCompilerNode = sheikhNode.StepOrder == 0;
            bool assignedPrimaryEdge = false;

            foreach (var edgeGroup in edgesForSheikh)
            {
                if (!mergedByNarratorId.TryGetValue(edgeGroup.Key.StudentId, out var studentNode))
                    continue;

                var edgeRows = edgeGroup.Select(x => x.Row).ToList();
                var firstEdgeRow = edgeRows[0];
                var edgeHadithIds = edgeRows.Select(r => r.SourceHadithId).Distinct().ToList();
                var edgeBooks = edgeRows.Select(r => r.SourceBookName).Distinct().ToList();

                // Assign the first edge directly to the primary node ONLY if the primary node is not a StepOrder=0 compiler
                // and only has a single student across the tree (so its SourceBooks match the node's SourceBooks).
                if (!isCompilerNode && !assignedPrimaryEdge && edgesForSheikh.Count() == 1)
                {
                    sheikhNode.ParentNodeId = studentNode.Id;
                    sheikhNode.TransmissionTerm = firstEdgeRow.TransmissionTerm;
                    assignedPrimaryEdge = true;
                }
                else
                {
                    additionalEdgeNodes.Add(new ComparativeIsnadNodeDto
                    {
                        Id = Guid.NewGuid(),
                        NarratorId = firstEdgeRow.NarratorId,
                        NarratorName = firstEdgeRow.NarratorName,
                        KnownAs = firstEdgeRow.KnownAs,
                        GenerationTier = firstEdgeRow.GenerationTier,
                        StepOrder = Math.Max(1, firstEdgeRow.StepOrder),
                        ParentNodeId = studentNode.Id,
                        TransmissionTerm = firstEdgeRow.TransmissionTerm,
                        GradeEn = firstEdgeRow.GradeEn,
                        IsMudallis = firstEdgeRow.IsMudallis,
                        HasMukhtalit = firstEdgeRow.HasMukhtalit,
                        ResidencePlaces = firstEdgeRow.ResidencePlaces,
                        DeathPlace = firstEdgeRow.DeathPlace,
                        GawamiRank = firstEdgeRow.GawamiRank,
                        TotalNarrationsCount = firstEdgeRow.TotalNarrationsCount,
                        UniqueHadithCount = firstEdgeRow.UniqueHadithCount,
                        SourceHadithIds = edgeHadithIds,
                        SourceBooks = edgeBooks
                    });
                }
            }
        }

        mergedNodes.AddRange(additionalEdgeNodes);

        // Detect anomalies (Inqita') using the same logic as single-chain
        var narratorIds = mergedNodes.Select(n => n.NarratorId).Distinct().ToList();
        var narratorDates = await context.Narrators
            .Where(n => narratorIds.Contains(n.Id))
            .Select(n => new { n.Id, n.BirthYearHijri, n.DeathYearHijri, n.ResidencePlaces, n.DeathPlace })
            .ToDictionaryAsync(n => n.Id, ct);

        foreach (var node in mergedNodes)
        {
            if (node.ParentNodeId.HasValue && node.ParentNodeId.Value != Guid.Empty)
            {
                var studentNode = mergedNodes.FirstOrDefault(n => n.Id == node.ParentNodeId.Value);
                if (studentNode != null &&
                    narratorDates.TryGetValue(node.NarratorId, out var sheikhMeta) &&
                    narratorDates.TryGetValue(studentNode.NarratorId, out var studentMeta))
                {
                    if (studentMeta.BirthYearHijri.HasValue && sheikhMeta.DeathYearHijri.HasValue &&
                        studentMeta.BirthYearHijri.Value > sheikhMeta.DeathYearHijri.Value)
                    {
                        node.IsAnomaly = true;
                        node.AnomalyReason = $"انقطاع زمني: التلميذ ولد سنة {studentMeta.BirthYearHijri} بعد وفاة الشيخ سنة {sheikhMeta.DeathYearHijri}";
                    }
                    else if (PlaceRegions.HaveNothingInCommon(sheikhMeta.ResidencePlaces, sheikhMeta.DeathPlace, studentMeta.ResidencePlaces, studentMeta.DeathPlace, out var travelNote))
                    {
                        node.TravelNote = travelNote;
                    }
                }
            }
        }

        return mergedNodes;
    }
}

/// <summary>
/// Internal DTO for raw CTE result rows before merging.
/// </summary>
internal class ComparativeRawRow
{
    public Guid Id { get; set; }
    public Guid NarratorId { get; set; }
    public string NarratorName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? GenerationTier { get; set; }
    public int StepOrder { get; set; }
    public Guid? ParentNodeId { get; set; }
    public string? TransmissionTerm { get; set; }
    public string? GradeEn { get; set; }
    public bool IsMudallis { get; set; }
    public bool HasMukhtalit { get; set; }
    public string? ResidencePlaces { get; set; }
    public string? DeathPlace { get; set; }
    public string? GawamiRank { get; set; }
    public int? TotalNarrationsCount { get; set; }
    public int? UniqueHadithCount { get; set; }
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }
    public Guid SourceHadithId { get; set; }
    public string SourceBookName { get; set; } = string.Empty;
}
