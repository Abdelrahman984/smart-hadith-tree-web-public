using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

public class HadithSearchService(
    IHadithTreeDbContext context,
    IHadithChainRepository chainRepository,
    ITaqwiyahService taqwiyahService,
    IIlalAnalysisService? ilalService = null) : IHadithSearchService
{
    public async Task<List<HadithSearchResultDto>> SearchHadithsAsync(SearchRequestDto request, CancellationToken ct = default)
    {
        // 1. Determine phrases
        var rawAndPhrases = request.AndPhrases != null && request.AndPhrases.Count > 0
            ? request.AndPhrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
            : [];

        var rawOrPhrases = request.OrPhrases != null && request.OrPhrases.Count > 0
            ? request.OrPhrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
            : [];

        // Backward compatibility with legacy request.Phrases and request.Operator
        if (rawAndPhrases.Count == 0 && rawOrPhrases.Count == 0)
        {
            var legacyPhrases = request.Phrases != null && request.Phrases.Count > 0
                ? request.Phrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
                : [];

            if (legacyPhrases.Count == 0 && !string.IsNullOrWhiteSpace(request.Query))
            {
                legacyPhrases = [request.Query.Trim()];
            }

            if (request.Operator == SearchLogicalOperator.Or)
            {
                rawOrPhrases = legacyPhrases;
            }
            else
            {
                rawAndPhrases = legacyPhrases;
            }
        }

        if (rawAndPhrases.Count == 0 && rawOrPhrases.Count == 0)
            return [];

        var normalizedAndPhrases = rawAndPhrases
            .Select(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize)
            .ToList();

        var normalizedOrPhrases = rawOrPhrases
            .Select(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize)
            .ToList();

        var excludePhrases = (request.ExcludePhrases ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(p.Trim()))
            .ToList();

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize > 0 ? request.PageSize : 50, 1, 200);
        var skip = (page - 1) * pageSize;

        var queryable = context.Hadiths.AsNoTracking();

        // 2. Exclude phrases (NOT / ليس)
        foreach (var excluded in excludePhrases)
        {
            queryable = queryable.Where(h => !h.NormalizedMatn.Contains(excluded));
        }

        // 3. Apply AND phrases
        if (normalizedAndPhrases.Count == 1 && normalizedOrPhrases.Count == 0 && request.Match != SearchMatchType.Exact)
        {
            var singleNormalized = normalizedAndPhrases[0];
            var words = singleNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (request.Match == SearchMatchType.AnyWord && words.Length > 1)
            {
                queryable = ApplyOrPhrases(queryable, words.ToList(), request.Scope);
            }
            else // AllWords or single word
            {
                foreach (var w in words)
                {
                    if (request.Scope == SearchScope.All)
                    {
                        queryable = queryable.Where(h => h.NormalizedMatn.Contains(w) ||
                                                         h.NormalizedBookName.Contains(w) ||
                                                         (h.FullIsnadText != null && h.FullIsnadText.Contains(w)));
                    }
                    else if (request.Scope == SearchScope.Matn)
                    {
                        queryable = queryable.Where(h => h.NormalizedMatn.Contains(w));
                    }
                    else if (request.Scope == SearchScope.Isnad)
                    {
                        queryable = queryable.Where(h => h.FullIsnadText != null && h.FullIsnadText.Contains(w));
                    }
                }
            }
        }
        else
        {
            foreach (var np in normalizedAndPhrases)
            {
                if (request.Scope == SearchScope.All)
                {
                    queryable = queryable.Where(h => h.NormalizedMatn.Contains(np) ||
                                                     h.NormalizedBookName.Contains(np) ||
                                                     (h.FullIsnadText != null && h.FullIsnadText.Contains(np)));
                }
                else if (request.Scope == SearchScope.Matn)
                {
                    queryable = queryable.Where(h => h.NormalizedMatn.Contains(np));
                }
                else if (request.Scope == SearchScope.Isnad)
                {
                    queryable = queryable.Where(h => h.FullIsnadText != null && h.FullIsnadText.Contains(np));
                }
            }
        }

        // 4. Apply OR phrases (if any)
        if (normalizedOrPhrases.Count > 0)
        {
            queryable = ApplyOrPhrases(queryable, normalizedOrPhrases, request.Scope);
        }

        // 5. In-Order (مرتبة) and Proximity (متقاربة) evaluation on AND phrases
        var orderingPhrases = normalizedAndPhrases.Count > 1 ? normalizedAndPhrases : normalizedOrPhrases;
        if ((request.IsOrdered || request.IsProximity) && orderingPhrases.Count > 1)
        {
            // Push ordered constraint directly to SQL Server when searching Matn/All with AND phrases
            if (request.IsOrdered && normalizedAndPhrases.Count > 1)
            {
                var orderedPattern = "%" + string.Join("%", orderingPhrases) + "%";
                queryable = queryable.Where(h => EF.Functions.Like(h.NormalizedMatn, orderedPattern));
            }

            var candidates = await queryable
                .OrderBy(h => h.MatnArabic.Length)
                .Skip(skip)
                .Take(Math.Max(100, pageSize * 2))
                .Select(h => new
                {
                    h.Id,
                    h.BookName,
                    h.HadithNumber,
                    h.Chapter,
                    h.MatnArabic,
                    h.NormalizedMatn
                })
                .ToListAsync(ct);

            var filtered = candidates.Where(h =>
            {
                if (request.IsOrdered && !CheckOrdered(h.NormalizedMatn, orderingPhrases))
                    return false;

                if (request.IsProximity && !CheckProximity(h.NormalizedMatn, orderingPhrases, request.ProximityWords))
                    return false;

                return true;
            })
            .Select(h => new { Hadith = h, Relevance = RelevanceScorer.Score(h.NormalizedMatn, orderingPhrases) })
            .OrderByDescending(x => x.Relevance?.Percent ?? -1)
            .Take(pageSize)
            .Select(x => new HadithSearchResultDto
            {
                Id = x.Hadith.Id,
                BookName = x.Hadith.BookName,
                HadithNumber = x.Hadith.HadithNumber,
                Chapter = x.Hadith.Chapter,
                MatnArabic = ExtractRelevantMatn(x.Hadith.MatnArabic, x.Hadith.NormalizedMatn, orderingPhrases),
                MatnSnippet = BuildMatchSnippet(x.Hadith.MatnArabic, x.Hadith.NormalizedMatn, orderingPhrases),
                RelevancePercent = x.Relevance?.Percent,
                RelevanceReason = x.Relevance?.ReasonAr
            })
            .ToList();

            return filtered;
        }

        var activeTerms = normalizedAndPhrases.Count == 1 && normalizedOrPhrases.Count == 0 && request.Match != SearchMatchType.Exact
            ? normalizedAndPhrases[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList()
            : normalizedAndPhrases.Concat(normalizedOrPhrases).ToList();

        var exactNormalizedPhrase = normalizedAndPhrases.Count == 1 && normalizedOrPhrases.Count == 0
            ? normalizedAndPhrases[0]
            : string.Join(" ", normalizedAndPhrases);

        // The fetch below keeps the shortest matches. A long hadith that contains the exact phrase could be cut
        // before ranking, so when the cap is reached the exact-phrase hits are fetched as well.
        var candidateCap = skip + Math.Max(150, pageSize * 3);
        var rawCandidates = await queryable
            .OrderBy(h => h.MatnArabic.Length)
            .Take(candidateCap)
            .Select(h => new Candidate
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                NormalizedMatn = h.NormalizedMatn,
                FullIsnadText = h.FullIsnadText
            })
            .ToListAsync(ct);

        if (rawCandidates.Count >= candidateCap && exactNormalizedPhrase.Contains(' '))
        {
            var known = rawCandidates.Select(c => c.Id).ToHashSet();
            var exactHits = await queryable
                .Where(h => h.NormalizedMatn.Contains(exactNormalizedPhrase))
                .OrderBy(h => h.MatnArabic.Length)
                .Take(ExactPhraseCandidateCap)
                .Select(h => new Candidate
                {
                    Id = h.Id,
                    BookName = h.BookName,
                    HadithNumber = h.HadithNumber,
                    Chapter = h.Chapter,
                    MatnArabic = h.MatnArabic,
                    NormalizedMatn = h.NormalizedMatn,
                    FullIsnadText = h.FullIsnadText
                })
                .ToListAsync(ct);
            rawCandidates.AddRange(exactHits.Where(c => known.Add(c.Id)));
        }

        var ranked = rawCandidates
            .Select(h =>
            {
                var (isValidCluster, clusterSpan) = EvaluateTermCluster(
                    h.NormalizedMatn,
                    h.FullIsnadText,
                    activeTerms,
                    request.Match,
                    request.Scope);

                bool hasExactPhrase = !string.IsNullOrEmpty(exactNormalizedPhrase) &&
                                      h.NormalizedMatn.Contains(exactNormalizedPhrase, StringComparison.Ordinal);

                return new
                {
                    Hadith = h,
                    IsValidCluster = isValidCluster,
                    HasExactPhrase = hasExactPhrase,
                    ClusterSpan = clusterSpan
                };
            })
            .Where(x => x.IsValidCluster)
            .Select(x => new
            {
                x.Hadith,
                x.HasExactPhrase,
                x.ClusterSpan,
                Relevance = request.Scope == SearchScope.Isnad
                    ? null
                    : RelevanceScorer.Score(x.Hadith.NormalizedMatn, activeTerms, exactNormalizedPhrase)
            })
            .OrderByDescending(x => x.Relevance?.Percent ?? -1)
            .ThenByDescending(x => x.HasExactPhrase)
            .ThenBy(x => x.ClusterSpan)
            .ThenBy(x => x.Hadith.MatnArabic.Length)
            .ThenBy(x => x.Hadith.HadithNumber)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new HadithSearchResultDto
            {
                Id = x.Hadith.Id,
                BookName = x.Hadith.BookName,
                HadithNumber = x.Hadith.HadithNumber,
                Chapter = x.Hadith.Chapter,
                MatnArabic = ExtractRelevantMatn(x.Hadith.MatnArabic, x.Hadith.NormalizedMatn, activeTerms),
                MatnSnippet = BuildMatchSnippet(x.Hadith.MatnArabic, x.Hadith.NormalizedMatn, activeTerms),
                RelevancePercent = x.Relevance?.Percent,
                RelevanceReason = x.Relevance?.ReasonAr
            })
            .ToList();

        return ranked;
    }

    private const int ExactPhraseCandidateCap = 300;

    /// <summary>A search hit with the columns needed for ranking.</summary>
    private sealed class Candidate
    {
        public Guid Id { get; set; }
        public string BookName { get; set; } = string.Empty;
        public int HadithNumber { get; set; }
        public string? Chapter { get; set; }
        public string MatnArabic { get; set; } = string.Empty;
        public string NormalizedMatn { get; set; } = string.Empty;
        public string? FullIsnadText { get; set; }
    }

    private const int MaxClusterWindowChars = 800;
    private const int MaxReturnedMatnLength = 4000;

    private static (bool IsValid, int Span) EvaluateTermCluster(
        string normalizedMatn,
        string? fullIsnadText,
        List<string> terms,
        SearchMatchType matchType,
        SearchScope scope)
    {
        if (terms.Count <= 1 || matchType == SearchMatchType.AnyWord)
            return (true, 0);

        if (scope == SearchScope.Isnad)
        {
            return (true, fullIsnadText?.Length ?? 0);
        }

        // Anchor on the longest (most specific) search term to locate co-occurring clusters
        var anchor = terms.OrderByDescending(t => t.Length).First();
        int searchFrom = 0;
        int bestSpan = int.MaxValue;
        bool foundCluster = false;

        while (searchFrom < normalizedMatn.Length)
        {
            int anchorIdx = normalizedMatn.IndexOf(anchor, searchFrom, StringComparison.Ordinal);
            if (anchorIdx == -1)
                break;

            int winStart = Math.Max(0, anchorIdx - MaxClusterWindowChars);
            int winEnd = Math.Min(normalizedMatn.Length, anchorIdx + anchor.Length + MaxClusterWindowChars);
            var window = normalizedMatn.AsSpan(winStart, winEnd - winStart);

            int minPos = anchorIdx - winStart;
            int maxPos = minPos + anchor.Length;
            bool allInWindow = true;

            foreach (var term in terms)
            {
                int relIdx = window.IndexOf(term.AsSpan(), StringComparison.Ordinal);
                if (relIdx == -1)
                {
                    allInWindow = false;
                    break;
                }
                if (relIdx < minPos) minPos = relIdx;
                if (relIdx + term.Length > maxPos) maxPos = relIdx + term.Length;
            }

            if (allInWindow)
            {
                foundCluster = true;
                int span = maxPos - minPos;
                if (span < bestSpan)
                    bestSpan = span;
            }

            searchFrom = anchorIdx + anchor.Length;
        }

        if (foundCluster)
            return (true, bestSpan);

        // If Scope == All and terms matched across Isnad/BookName rather than Matn alone
        if (scope == SearchScope.All && !string.IsNullOrEmpty(fullIsnadText))
        {
            bool allInIsnad = terms.All(t => fullIsnadText.Contains(t, StringComparison.Ordinal));
            if (allInIsnad)
                return (true, fullIsnadText.Length);
        }

        // If the entire record is short, allow it; if it's a multi-hadith mega-record with scattered words, reject it
        if (normalizedMatn.Length <= MaxClusterWindowChars * 2)
            return (true, normalizedMatn.Length);

        return (false, int.MaxValue);
    }

    private static string ExtractRelevantMatn(string matnArabic, string normalizedMatn, List<string> terms)
    {
        if (matnArabic.Length <= MaxReturnedMatnLength || terms.Count == 0 || normalizedMatn.Length == 0)
            return matnArabic;

        int approxRawIdx = EstimateRawMatchIndex(matnArabic, normalizedMatn, terms);
        int start = Math.Max(0, approxRawIdx - 600);
        int length = Math.Min(MaxReturnedMatnLength, matnArabic.Length - start);
        var slice = matnArabic.Substring(start, length).Trim();
        return (start > 0 ? "... " : "") + slice + (start + length < matnArabic.Length ? " ..." : "");
    }

    private static string BuildMatchSnippet(string matnArabic, string normalizedMatn, List<string> terms)
    {
        const int snippetLen = 180;
        if (matnArabic.Length <= snippetLen)
            return matnArabic;

        if (terms.Count == 0 || normalizedMatn.Length == 0)
            return matnArabic.Substring(0, 150) + "...";

        int approxRawIdx = EstimateRawMatchIndex(matnArabic, normalizedMatn, terms);
        int start = Math.Max(0, approxRawIdx - 60);
        int length = Math.Min(snippetLen, matnArabic.Length - start);
        var snippet = matnArabic.Substring(start, length).Trim();
        return (start > 0 ? "..." : "") + snippet + (start + length < matnArabic.Length ? "..." : "");
    }

    private static int EstimateRawMatchIndex(string matnArabic, string normalizedMatn, List<string> terms)
    {
        var anchor = terms.OrderByDescending(t => t.Length).First();
        int searchFrom = 0;
        int bestNormIdx = normalizedMatn.IndexOf(anchor, StringComparison.Ordinal);

        while (searchFrom < normalizedMatn.Length)
        {
            int idx = normalizedMatn.IndexOf(anchor, searchFrom, StringComparison.Ordinal);
            if (idx == -1) break;

            int winStart = Math.Max(0, idx - MaxClusterWindowChars);
            int winEnd = Math.Min(normalizedMatn.Length, idx + anchor.Length + MaxClusterWindowChars);
            var window = normalizedMatn.AsSpan(winStart, winEnd - winStart);

            bool allFound = true;
            foreach (var t in terms)
            {
                if (window.IndexOf(t.AsSpan(), StringComparison.Ordinal) == -1)
                {
                    allFound = false;
                    break;
                }
            }

            if (allFound)
            {
                bestNormIdx = idx;
                break;
            }
            searchFrom = idx + anchor.Length;
        }

        if (bestNormIdx <= 0)
            return 0;

        double ratio = (double)bestNormIdx / normalizedMatn.Length;
        return Math.Clamp((int)(ratio * matnArabic.Length), 0, Math.Max(0, matnArabic.Length - 1));
    }

    private static IQueryable<HadithText> ApplyOrPhrases(IQueryable<HadithText> queryable, List<string> phrases, SearchScope scope)
    {
        if (phrases.Count == 0) return queryable;

        var parameter = Expression.Parameter(typeof(HadithText), "h");
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? combined = null;

        foreach (var phrase in phrases)
        {
            var phraseConst = Expression.Constant(phrase);
            Expression predicate;

            if (scope == SearchScope.Matn)
            {
                var matnProp = Expression.Property(parameter, nameof(HadithText.NormalizedMatn));
                predicate = Expression.Call(matnProp, containsMethod, phraseConst);
            }
            else if (scope == SearchScope.Isnad)
            {
                var isnadProp = Expression.Property(parameter, nameof(HadithText.FullIsnadText));
                var isnadNotNull = Expression.NotEqual(isnadProp, Expression.Constant(null, typeof(string)));
                var isnadContains = Expression.Call(isnadProp, containsMethod, phraseConst);
                predicate = Expression.AndAlso(isnadNotNull, isnadContains);
            }
            else
            {
                var matnProp = Expression.Property(parameter, nameof(HadithText.NormalizedMatn));
                var matnContains = Expression.Call(matnProp, containsMethod, phraseConst);

                var bookProp = Expression.Property(parameter, nameof(HadithText.NormalizedBookName));
                var bookContains = Expression.Call(bookProp, containsMethod, phraseConst);

                var isnadProp = Expression.Property(parameter, nameof(HadithText.FullIsnadText));
                var isnadNotNull = Expression.NotEqual(isnadProp, Expression.Constant(null, typeof(string)));
                var isnadContains = Expression.Call(isnadProp, containsMethod, phraseConst);
                var isnadPredicate = Expression.AndAlso(isnadNotNull, isnadContains);

                predicate = Expression.OrElse(Expression.OrElse(matnContains, bookContains), isnadPredicate);
            }

            combined = combined == null ? predicate : Expression.OrElse(combined, predicate);
        }

        if (combined != null)
        {
            var lambda = Expression.Lambda<Func<HadithText, bool>>(combined, parameter);
            queryable = queryable.Where(lambda);
        }

        return queryable;
    }

    private static bool CheckOrdered(string text, List<string> phrases)
    {
        int lastIdx = -1;
        foreach (var phrase in phrases)
        {
            int idx = text.IndexOf(phrase, lastIdx == -1 ? 0 : lastIdx + phrase.Length, StringComparison.Ordinal);
            if (idx == -1 || (lastIdx != -1 && idx <= lastIdx))
                return false;
            lastIdx = idx;
        }
        return true;
    }

    private static bool CheckProximity(string text, List<string> phrases, int maxWords)
    {
        var words = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var wordIndices = new List<int>();

        foreach (var phrase in phrases)
        {
            var firstWord = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(firstWord)) continue;

            int foundIdx = -1;
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Contains(firstWord, StringComparison.Ordinal))
                {
                    foundIdx = i;
                    break;
                }
            }

            if (foundIdx == -1) return false;
            wordIndices.Add(foundIdx);
        }

        if (wordIndices.Count < 2) return true;
        return (wordIndices.Max() - wordIndices.Min()) <= maxWords;
    }

    public async Task<IsnadTreeResponseDto?> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default)
    {
        var hadith = await context.Hadiths
            .Where(h => h.Id == hadithId)
            .FirstOrDefaultAsync(ct);

        if (hadith == null)
            return null;

        var nodes = await chainRepository.GetIsnadTreeAsync(hadithId, ct);

        return new IsnadTreeResponseDto
        {
            HadithId = hadith.Id,
            BookName = hadith.BookName,
            HadithNumber = hadith.HadithNumber,
            MatnArabic = hadith.MatnArabic,
            Nodes = nodes
        };
    }

    /// <summary>
    /// Merges the Isnad chains of multiple Hadiths into a single comparative tree (Takhreej).
    /// </summary>
    public async Task<ComparativeTreeResponseDto?> GetComparativeTreeAsync(
        List<Guid> hadithIds, CancellationToken ct = default)
    {
        if (hadithIds.Count == 0) return null;

        // Fetch source Hadith metadata
        var hadiths = await context.Hadiths
            .Where(h => hadithIds.Contains(h.Id))
            .ToListAsync(ct);

        if (hadiths.Count == 0) return null;

        // Keep the order the caller asked for (the database returns rows in no particular order).
        hadiths = hadiths.OrderBy(h => hadithIds.IndexOf(h.Id)).ToList();

        var sources = hadiths.Select(h => new ComparativeHadithSourceDto
        {
            HadithId = h.Id,
            BookName = h.BookName,
            HadithNumber = h.HadithNumber,
            MatnArabic = h.MatnArabic,
            MatnSnippet = h.MatnArabic.Length > 150
                ? h.MatnArabic.Substring(0, 150) + "..."
                : h.MatnArabic
        }).ToList();

        // Get merged chain nodes
        var nodes = await chainRepository.GetComparativeIsnadTreeAsync(hadithIds, ct);

        var response = new ComparativeTreeResponseDto
        {
            Sources = sources,
            Nodes = nodes
        };

        // Matn variation: flag the turuq whose wording differs substantively from the most typical text.
        if (sources.Count > 1)
        {
            var bodies = hadiths
                .Select(h => MatnText.Tokenize(MatnText.ExtractBody(h.MatnArabic)))
                .ToList();

            foreach (var index in MatnVariation.FindVariants(bodies))
            {
                var hadithId = hadiths[index].Id;
                var matchingEdgeNodes = nodes.Where(n =>
                    n.StepOrder == 1 &&
                    n.ParentNodeId.HasValue &&
                    n.SourceHadithIds.Contains(hadithId));
                foreach (var edgeNode in matchingEdgeNodes)
                {
                    edgeNode.HasMatnVariation = true;
                    edgeNode.MatnVariationSnippet = "يوجد اختلاف أو زيادة في لفظ المتن مقارنة بأغلب الروايات.";
                }
            }
        }

        if (ilalService != null)
            response.IlalReport = await ilalService.AnalyzeAsync(hadithIds, ct);

        taqwiyahService.CalculateTreeStrength(response);

        return response;
    }

    /// <summary>
    /// Finds related Hadiths across all books by matching normalized Matn text.
    /// Extracts a representative substring from the source Hadith's text and
    /// searches for matches in other books.
    /// </summary>
    public async Task<List<HadithSearchResultDto>> FindRelatedHadithsAsync(
        Guid hadithId, CancellationToken ct = default)
    {
        var hadith = await context.Hadiths
            .Where(h => h.Id == hadithId)
            .FirstOrDefaultAsync(ct);

        if (hadith == null) return [];

        // Extract a representative substring from the Matn for cross-book matching.
        // Skip the first ~30 chars to avoid matching common Isnad prefixes.
        var normalizedMatn = hadith.NormalizedMatn;
        var searchSubstring = normalizedMatn.Length > 110
            ? normalizedMatn.Substring(30, 80)
            : normalizedMatn.Length > 40
                ? normalizedMatn.Substring(0, 40)
                : normalizedMatn;

        if (string.IsNullOrWhiteSpace(searchSubstring)) return [];

        var relatedHadiths = await context.Hadiths
            .Where(h => h.Id != hadithId && h.NormalizedMatn.Contains(searchSubstring))
            .Take(20)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                MatnSnippet = h.MatnArabic.Length > 150
                    ? h.MatnArabic.Substring(0, 150) + "..."
                    : h.MatnArabic
            })
            .ToListAsync(ct);

        return relatedHadiths;
    }
}
