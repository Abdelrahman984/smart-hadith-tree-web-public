using System.Text.RegularExpressions;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

/// <summary>How well one matn matches the searched words, with a one-line Arabic reason.</summary>
/// <param name="Percent">0-100.</param>
/// <param name="ReasonAr">Why the score is what it is (shown to the user).</param>
/// <param name="WindowStartChar">Offset in the normalized matn where the tightest group of searched words starts.</param>
public sealed record RelevanceResult(int Percent, string ReasonAr, int WindowStartChar);

/// <summary>
/// Scores a matn against the searched terms without a model, so every result gets an instant, explainable score:
/// <c>percent = 100 x coverage x (0.25 + 0.50 x proximity + 0.25 x phrase/order)</c>.
/// <list type="bullet">
/// <item><b>coverage</b>: share of the searched terms found in the matn.</item>
/// <item><b>proximity</b>: how tight the smallest group of words containing all found terms is, measured in
/// words (one filler word between terms is free). A long hadith with the words far apart scores low.</item>
/// <item><b>phrase/order</b>: 1 if the exact phrase occurs, 0.5 if the terms appear in the searched order, else 0.</item>
/// </list>
/// The weights are a starting point to be tuned on the search evaluation set.
/// </summary>
public static class RelevanceScorer
{
    private const int MaxOccurrencesPerTerm = 100;
    private static readonly Regex Words = new(@"[ء-ي]+", RegexOptions.Compiled);

    /// <summary>The normalized words of a query (the same normalization the search uses).</summary>
    public static List<string> QueryTerms(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return Words.Matches(ArabicNormalizer.Normalize(query))
            .Select(m => m.Value)
            .Where(w => w.Length >= 2)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Scores <paramref name="normalizedMatn"/> (already normalized) against already-normalized terms.
    /// Returns null when no term occurs in the matn (for example, the hit was in the isnad or the book name).
    /// </summary>
    public static RelevanceResult? Score(string normalizedMatn, IReadOnlyList<string> terms, string? exactPhrase = null)
    {
        if (string.IsNullOrEmpty(normalizedMatn) || terms.Count == 0) return null;

        var tokenStarts = Words.Matches(normalizedMatn).Select(m => m.Index).ToArray();
        if (tokenStarts.Length == 0) return null;

        // A word the user repeated ("الحلال بين الحرام بين") must also occur that many times: the term list is
        // reduced to distinct terms with a multiplicity, and coverage counts min(occurrences, multiplicity).
        var unique = terms.Distinct().ToList();
        var mult = unique.Select(u => terms.Count(x => x == u)).ToArray();

        // Occurrences of every distinct term, as (word index, term index, length in words).
        var events = new List<(int Word, int Term, int Len)>();
        var termWords = new int[unique.Count];
        var found = new bool[unique.Count];
        var occurrences = new int[unique.Count];
        for (var t = 0; t < unique.Count; t++)
        {
            termWords[t] = Math.Max(1, Words.Matches(unique[t]).Count);
            var from = 0;
            // "الجنه" must also be found in "للجنه" (a prefix before the stem): fall back to the stem without "ال".
            var needle = unique[t];
            if (needle.StartsWith("ال", StringComparison.Ordinal) && needle.Length >= 5 && !normalizedMatn.Contains(needle, StringComparison.Ordinal))
                needle = needle[2..];
            while (occurrences[t] < MaxOccurrencesPerTerm)
            {
                var idx = normalizedMatn.IndexOf(needle, from, StringComparison.Ordinal);
                if (idx < 0) break;
                found[t] = true;
                var word = Array.BinarySearch(tokenStarts, idx);
                if (word < 0) word = Math.Max(0, ~word - 1);
                events.Add((word, t, termWords[t]));
                from = idx + Math.Max(1, needle.Length);
                occurrences[t]++;
            }
        }

        var foundCount = found.Count(f => f);
        if (foundCount == 0) return null;
        var coveredUnits = Enumerable.Range(0, unique.Count).Sum(t => Math.Min(occurrences[t], mult[t]));
        var coverage = (double)coveredUnits / terms.Count;

        events.Sort((a, b) => a.Word != b.Word ? a.Word.CompareTo(b.Word) : a.Term.CompareTo(b.Term));
        var (bestStart, bestSpan, inOrder) = SmallestWindow(events, found, foundCount);

        var foundWords = Enumerable.Range(0, unique.Count).Where(t => found[t]).Sum(t => termWords[t]);
        var ideal = foundWords + (foundCount - 1);
        var proximity = foundCount == 1 ? 1.0 : Math.Min(1.0, (double)ideal / Math.Max(1, bestSpan));

        var phrase = !string.IsNullOrEmpty(exactPhrase) && exactPhrase.Contains(' ')
                     && normalizedMatn.Contains(exactPhrase, StringComparison.Ordinal);
        var orderFactor = unique.Count == 1 ? 1.0 : phrase ? 1.0 : inOrder ? 0.5 : 0.0;

        var percent = (int)Math.Round(100 * coverage * (0.25 + 0.5 * proximity + 0.25 * orderFactor));
        percent = Math.Clamp(percent, 0, 100);

        return new RelevanceResult(percent, Reason(terms.Count, coveredUnits, bestSpan, ideal, phrase, inOrder), tokenStarts[Math.Min(bestStart, tokenStarts.Length - 1)]);
    }

    /// <summary>Smallest run of words (in the sorted events) that contains every found term, with its start and order.</summary>
    private static (int Start, int Span, bool InOrder) SmallestWindow(List<(int Word, int Term, int Len)> events, bool[] found, int foundCount)
    {
        var need = foundCount;
        var have = new int[found.Length];
        var covered = 0;
        var bestStart = events[0].Word;
        var bestSpan = int.MaxValue;
        var bestL = 0;
        var bestR = events.Count - 1;
        var l = 0;

        for (var r = 0; r < events.Count; r++)
        {
            if (have[events[r].Term]++ == 0) covered++;
            while (covered == need)
            {
                var maxEnd = 0;
                for (var i = l; i <= r; i++) maxEnd = Math.Max(maxEnd, events[i].Word + events[i].Len);
                var span = maxEnd - events[l].Word;
                if (span < bestSpan)
                {
                    bestSpan = span;
                    bestStart = events[l].Word;
                    bestL = l;
                    bestR = r;
                }
                if (--have[events[l].Term] == 0) covered--;
                l++;
            }
        }

        // Order: first occurrence of each term inside the best window, by position, must follow the term order.
        var seen = new HashSet<int>();
        var lastTerm = -1;
        var ordered = true;
        for (var i = bestL; i <= bestR && ordered; i++)
        {
            if (!seen.Add(events[i].Term)) continue;
            if (events[i].Term < lastTerm) ordered = false;
            lastTerm = events[i].Term;
        }

        return (bestStart, bestSpan == int.MaxValue ? 1 : bestSpan, ordered);
    }

    private static string Reason(int total, int found, int span, int ideal, bool phrase, bool inOrder)
    {
        if (total == 1) return "الكلمة موجودة في المتن";
        if (phrase) return "العبارة موجودة حرفياً";
        if (found == total)
        {
            var near = span <= ideal ? "كل الكلمات متجاورة" : $"كل الكلمات ({total} من {total}) داخل {span} كلمة";
            return inOrder ? near + "، بنفس الترتيب" : near;
        }
        return found == 1
            ? $"وُجدت كلمة واحدة من {total}"
            : $"وُجد {found} من {total} كلمات داخل {span} كلمة";
    }
}
