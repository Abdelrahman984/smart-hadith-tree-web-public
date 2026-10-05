using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services.Ilal;

namespace SmartHadithTree.Application.Services;

public class NarratorService(IHadithTreeDbContext context) : INarratorService
{
    public async Task<List<NarratorSearchResultDto>> SearchNarratorsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(query);

        return await context.Narrators
            .Where(n => n.FullName.Contains(normalizedQuery) || (n.KnownAs != null && n.KnownAs.Contains(normalizedQuery)))
            .Take(50)
            .Select(n => new NarratorSearchResultDto
            {
                Id = n.Id,
                FullName = n.FullName,
                KnownAs = n.KnownAs,
                GenerationTier = n.GenerationTier,
                DeathYearHijri = n.DeathYearHijri
            })
            .ToListAsync(ct);
    }

    public async Task<NarratorDetailDto?> GetNarratorDetailsAsync(Guid narratorId, CancellationToken ct = default)
    {
        var narrator = await context.Narrators
            .Include(n => n.ScholarEvaluations)
            .FirstOrDefaultAsync(n => n.Id == narratorId, ct);

        if (narrator == null)
            return null;

        return new NarratorDetailDto
        {
            Id = narrator.Id,
            FullName = narrator.FullName,
            KnownAs = narrator.KnownAs,
            Kunyah = narrator.Kunyah,
            GenerationTier = narrator.GenerationTier,
            BirthYearHijri = narrator.BirthYearHijri,
            DeathYearHijri = narrator.DeathYearHijri,
            ResidencePlaces = narrator.ResidencePlaces,
            DeathPlace = narrator.DeathPlace,
            GawamiRank = narrator.GawamiRank,
            TotalNarrationsCount = narrator.TotalNarrationsCount,
            UniqueHadithCount = narrator.UniqueHadithCount,
            IsMudallis = narrator.IsMudallis,
            HasMukhtalit = narrator.HasMukhtalit,
            Biography = narrator.Biography,
            GradeEn = narrator.ItqanGrade ?? NarratorGradeScale.ToGradeEn(narrator.IbnHajarRank),
            Evaluations = narrator.ScholarEvaluations.Select(e => new ScholarEvaluationDto
            {
                ScholarName = e.ScholarName,
                EvaluationText = e.EvaluationText,
                SourceBook = e.SourceBook,
                VerdictRating = e.VerdictRating
            }).ToList()
        };
    }

    public async Task<NarratorSummaryDto?> GetNarratorTooltipAsync(Guid narratorId, CancellationToken ct = default)
    {
        var narrator = await context.Narrators
            .Where(n => n.Id == narratorId)
            .Select(n => new NarratorSummaryDto
            {
                Id = n.Id,
                FullName = n.KnownAs ?? n.FullName,
                GenerationTier = n.GenerationTier,
                GradeSummary = n.GawamiRank ?? n.Verdict ?? n.ScholarEvaluations.Select(e => e.VerdictRating).FirstOrDefault() ?? "غير معروف",
                GradeEn = n.ItqanGrade ?? NarratorGradeScale.ToGradeEn(n.IbnHajarRank)
            })
            .FirstOrDefaultAsync(ct);

        return narrator;
    }
}
