using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

public interface IHadithVerificationService
{
    Task<HadithVerificationResultDto> VerifyAsync(string text, CancellationToken ct = default);
}

/// <summary>
/// Checks whether a pasted text is a hadith in the corpus (retrieval, then a model review of the candidates).
/// The model never supplies a hadith, a source or a ruling: it can only (a) veto a close-wording match,
/// or (b) upgrade a weak match when it quotes a phrase that really appears in both the input and the candidate.
/// With no model configured, or when the model fails, the answer comes from text matching alone.
/// </summary>
public class HadithVerificationService(IHadithSearchService searchService, Kernel kernel) : IHadithVerificationService
{
    public const int MinWords = 3;
    public const int MaxChars = 600;
    public const double ExactThreshold = 0.95;
    public const double VariantThreshold = 0.6;
    public const double ReviewThreshold = 0.3;
    /// <summary>Model review time limit. Override with the VERIFY_REVIEW_TIMEOUT_SECONDS environment variable (2-60).</summary>
    private static readonly TimeSpan ReviewTimeout = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("VERIFY_REVIEW_TIMEOUT_SECONDS"), out var seconds)
            ? Math.Clamp(seconds, 2, 60)
            : 8);
    private const int MaxMatches = 5;
    private const int CandidatePageSize = 40;
    private const int MaxWindowQueries = 8;
    private const int WindowPageSize = 100;

    public async Task<HadithVerificationResultDto> VerifyAsync(string text, CancellationToken ct = default)
    {
        text = (text ?? string.Empty).Trim();
        var words = Tokenize(text, fold: false);

        if (words.Count < MinWords || text.Length > MaxChars)
        {
            return new HadithVerificationResultDto
            {
                Status = HadithVerificationStatus.Invalid,
                Explanation = $"أدخل نصاً من {MinWords} كلمات على الأقل وبحد أقصى {MaxChars} حرفاً."
            };
        }

        var candidates = await RetrieveAsync(words, ct);
        var inputBigrams = Bigrams(Tokenize(text, fold: true, stem: true));

        List<VerifiedHadithMatchDto> Score(List<HadithSearchResultDto> pool) => pool
            .Select(c => new VerifiedHadithMatchDto
            {
                Id = c.Id,
                BookName = c.BookName,
                HadithNumber = c.HadithNumber,
                Chapter = c.Chapter,
                MatnArabic = c.MatnArabic,
                Similarity = Math.Round(Containment(inputBigrams, Bigrams(Tokenize(c.MatnArabic, fold: true, stem: true))), 3)
            })
            .OrderByDescending(m => m.Similarity)
            .ThenBy(m => m.MatnArabic.Length)
            .Take(MaxMatches)
            .ToList();

        var scored = Score(candidates);

        // A paraphrase or a typo makes some words absent from the stored text, so the all-words queries return
        // nothing useful. Widen with runs of consecutive words (four first: more specific, then three), pooling the
        // candidates and re-ranking, until a variant-level match appears.
        foreach (var size in new[] { 4, 3 })
        {
            if ((scored.FirstOrDefault()?.Similarity ?? 0) >= VariantThreshold) break;
            var extra = await RetrieveByWindowsAsync(words, size, ct);
            if (extra.Count == 0) continue;
            candidates = candidates.Concat(extra).GroupBy(c => c.Id).Select(g => g.First()).ToList();
            scored = Score(candidates);
        }

        var best = scored.FirstOrDefault();
        string Diag(string decidedBy) =>
            $"candidates={candidates.Count}; best={(best == null ? "none" : $"{best.Similarity:0.###} ({best.BookName} {best.HadithNumber})")}; decided-by={decidedBy}; review-timeout={ReviewTimeout.TotalSeconds:0}s";

        if (best == null || best.Similarity < ReviewThreshold)
        {
            var weak = NotFound();
            weak.Diagnostics = Diag(best == null ? "no-candidates" : "below-review-threshold");
            return weak;
        }

        var status = Classify(best.Similarity);
        var method = "lexical";
        var explanation = string.Empty;
        var modelStatus = VerificationModelStatus.NotNeeded;

        // A negation the matching hadith does not contain reverses the meaning, even when most word pairs match.
        // Decided in code, not by the model: a wrong "match" here would put words in the Prophet's mouth.
        if (status != HadithVerificationStatus.NotFound && HasUnmatchedNegation(text, best.MatnArabic))
        {
            var negated = NotFound(method,
                "النص يشبه حديثاً موجوداً في الكتب لكنه يختلف عنه بأداة نفي ليست في المتن، فالمعنى مختلف؛ لذلك لا ننسبه إلى ذلك الحديث.");
            negated.Diagnostics = Diag("negation-guard");
            return negated;
        }

        // Exact quotations need no model. The model only reviews the uncertain band.
        if (status != HadithVerificationStatus.Exact)
        {
            var (review, reviewStatus) = await ReviewAsync(text, scored.Take(3).ToList(), ct);
            modelStatus = reviewStatus;
            if (review != null)
            {
                method = "ai+lexical";
                explanation = review.Explanation;
                var index = review.CandidateIndex - 1;

                if (status == HadithVerificationStatus.Variant && review.Same == false && index == 0)
                {
                    // Safe direction: the model saw a meaning difference (negation, different subject) in the top match.
                    status = HadithVerificationStatus.NotFound;
                }
                else if (status == HadithVerificationStatus.NotFound && review.Same == true
                         && index >= 0 && index < scored.Count
                         && PhraseIsGrounded(review.SharedPhrase, text, scored[index].MatnArabic))
                {
                    status = HadithVerificationStatus.Variant;
                    scored = [scored[index], .. scored.Where((_, i) => i != index)];
                }
            }
        }

        if (status == HadithVerificationStatus.NotFound)
        {
            var notFound = NotFound(method, explanation);
            notFound.ModelStatus = modelStatus;
            notFound.Diagnostics = Diag(method == "ai+lexical" ? "model-review" : "similarity-threshold");
            return notFound;
        }

        return new HadithVerificationResultDto
        {
            Status = status,
            Method = method,
            ModelStatus = modelStatus,
            Diagnostics = Diag(method == "ai+lexical" ? "model-review" : "similarity-threshold"),
            UnmatchedWords = UnmatchedWords(text, scored[0].MatnArabic),
            Matches = scored.Where(m => m.Similarity >= ReviewThreshold).ToList(),
            Explanation = string.IsNullOrWhiteSpace(explanation) ? DefaultExplanation(status) : explanation
        };
    }

    private async Task<List<HadithSearchResultDto>> RetrieveAsync(List<string> words, CancellationToken ct)
    {
        // Prefix-stripped stems (لأحدكم -> أحدكم): the search is a substring match, so a stem also finds
        // the prefixed forms and the text written with a different preposition (إلى أحدكم).
        var distinctive = words
            .Where(w => !StopWords.Contains(w))
            .Select(StripPrefix)
            .Where(w => w.Length >= 3)
            .Distinct()
            .OrderByDescending(w => w.Length)
            .ToList();

        // Strictest first (5 longest words must all occur), then relax.
        foreach (var take in new[] { 5, 3, 2 })
        {
            if (distinctive.Count < take) continue;
            var results = await SearchAllWordsAsync(distinctive.Take(take), ct);
            if (results.Count > 0) return results;
        }

        return [];
    }

    /// <summary>Candidates for every run of <paramref name="size"/> consecutive content words, in the user's order.</summary>
    private async Task<List<HadithSearchResultDto>> RetrieveByWindowsAsync(List<string> words, int size, CancellationToken ct)
    {
        var ordered = words
            .Where(w => !StopWords.Contains(w))
            .Select(StripPrefix)
            .Where(w => w.Length >= 3)
            .ToList();
        var pooled = new Dictionary<Guid, HadithSearchResultDto>();
        var tried = new HashSet<string>();
        for (var i = 0; i + size <= ordered.Count && tried.Count < MaxWindowQueries; i++)
        {
            var window = ordered.Skip(i).Take(size).ToList();
            if (!tried.Add(string.Join(' ', window))) continue;
            foreach (var r in await SearchAllWordsAsync(window, ct, WindowPageSize)) pooled.TryAdd(r.Id, r);
        }
        return pooled.Values.ToList();
    }

    private Task<List<HadithSearchResultDto>> SearchAllWordsAsync(IEnumerable<string> words, CancellationToken ct, int pageSize = CandidatePageSize) =>
        searchService.SearchHadithsAsync(new SearchRequestDto
        {
            Query = string.Join(' ', words),
            Scope = SearchScope.Matn,
            Match = SearchMatchType.AllWords,
            PageSize = pageSize
        }, ct);

    private async Task<(ModelReview? Review, string Status)> ReviewAsync(string text, List<VerifiedHadithMatchDto> candidates, CancellationToken ct)
    {
        var list = string.Join("\n", candidates.Select((c, i) =>
            $"[{i + 1}] ({c.BookName} رقم {c.HadithNumber}): {Truncate(c.MatnArabic, 350)}"));

        var prompt = $@"
أنت مساعد تحقق من نصوص الحديث. لديك نص أدخله المستخدم، وقائمة نصوص (متون) مسترجعة من قاعدة بيانات الكتب.
مهمتك فقط الحكم: هل نص المستخدم هو الحديث نفسه الوارد في أحد المتون المرقمة (ولو اختلف اللفظ يسيراً)؟

قواعد صارمة:
- لا تذكر أي حديث أو كتاب أو راوٍ أو حكم غير موجود في القائمة أدناه.
- لا تحكم بصحة الحديث أو ضعفه.
- إن اختلف المعنى (نفي مقابل إثبات، موضوع مختلف، راوٍ مختلف في المتن) فالجواب Same=false.
- إن كان الجواب Same=true فاقتبس في SharedPhrase عبارة من ثلاث كلمات فأكثر نقلاً حرفياً من نص المستخدم، وهي نفسها موجودة في المتن المختار.

نص المستخدم:
{text}

المتون المسترجعة:
{list}

أرجع كائن JSON صالحاً فقط بدون أي نص إضافي أو علامات Markdown:
{{
  ""Same"": true أو false,
  ""CandidateIndex"": رقم المتن الأقرب (من 1),
  ""SharedPhrase"": ""عبارة حرفية مشتركة"",
  ""Explanation"": ""جملة أو جملتان بالعربية عن الفرق أو التطابق""
}}";

        try
        {
            // A slow provider must not stall the answer: past the limit we fall back to text matching.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ReviewTimeout);
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeout.Token);
            var json = StripFences(result.GetValue<string>());
            if (json.Length == 0) return (null, VerificationModelStatus.Unavailable);
            var review = JsonSerializer.Deserialize<ModelReview>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return review == null ? (null, VerificationModelStatus.Unavailable) : (review, VerificationModelStatus.Reviewed);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, VerificationModelStatus.Timeout);
        }
        catch (Exception)
        {
            // No model configured, provider error, or bad JSON: fall back to text matching. Do not leak details.
            return (null, VerificationModelStatus.Unavailable);
        }
    }

    private sealed class ModelReview
    {
        public bool? Same { get; set; }
        public int CandidateIndex { get; set; }
        public string SharedPhrase { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }

    public static string Classify(double similarity) =>
        similarity >= ExactThreshold ? HadithVerificationStatus.Exact
        : similarity >= VariantThreshold ? HadithVerificationStatus.Variant
        : HadithVerificationStatus.NotFound;

    /// <summary>
    /// Words of the input (as typed) that do not occur in the candidate, ignoring diacritics, hamza forms,
    /// prepositions and attached prefixes. Shows exactly which word of a "close wording" text was changed.
    /// </summary>
    public static List<string> UnmatchedWords(string input, string candidate)
    {
        var known = Tokenize(candidate, fold: true, stem: true).ToHashSet();
        var result = new List<string>();
        foreach (var raw in Regex.Split(input ?? string.Empty, @"\s+"))
        {
            var typed = NonLetters.Replace(Diacritics.Replace(raw, string.Empty), string.Empty);
            var stems = Tokenize(typed, fold: true, stem: true);
            if (stems.Count == 1 && !known.Contains(stems[0]) && !result.Contains(typed)) result.Add(typed);
        }
        return result.Take(8).ToList();
    }

    /// <summary>True when the input uses a negation word (لا، لم، لن، ليس، غير…) that never occurs in the candidate.</summary>
    public static bool HasUnmatchedNegation(string input, string candidate)
    {
        var candidateNegators = Tokenize(candidate, fold: true).Select(BaseNegator).Where(n => n != null).ToHashSet();
        return Tokenize(input, fold: true)
            .Select(BaseNegator)
            .Any(n => n != null && !candidateNegators.Contains(n));
    }

    /// <summary>The negator a token is (also with a leading و/ف), or null.</summary>
    private static string? BaseNegator(string token)
    {
        if (Negators.Contains(token)) return token;
        if (token.Length > 2 && (token[0] == '\u0648' || token[0] == '\u0641') && Negators.Contains(token[1..]))
            return token[1..];
        return null;
    }

    private static readonly HashSet<string> Negators = new(
        new[] { "لا", "لم", "لن", "ليس", "ليست", "ليسوا", "غير", "بدون" }
        .Select(w => ArabicNormalizer.Normalize(w)));

    /// <summary>True when the phrase has at least three words and occurs in both the input and the candidate.</summary>
    public static bool PhraseIsGrounded(string? phrase, string input, string candidate)
    {
        var p = Tokenize(phrase ?? string.Empty, fold: true);
        if (p.Count < 3) return false;
        var needle = " " + string.Join(' ', p) + " ";
        return Contains(input, needle) && Contains(candidate, needle);

        static bool Contains(string haystack, string needle) =>
            (" " + string.Join(' ', Tokenize(haystack, fold: true)) + " ").Contains(needle, StringComparison.Ordinal);
    }

    /// <summary>Share of the input's word pairs (in order) that also occur in the candidate.</summary>
    public static double Containment(HashSet<string> inputBigrams, HashSet<string> candidateBigrams)
    {
        if (inputBigrams.Count == 0) return 0;
        var found = inputBigrams.Count(candidateBigrams.Contains);
        return (double)found / inputBigrams.Count;
    }

    public static HashSet<string> Bigrams(List<string> tokens)
    {
        var set = new HashSet<string>();
        for (var i = 0; i + 1 < tokens.Count; i++) set.Add(tokens[i] + " " + tokens[i + 1]);
        return set;
    }

    private static readonly Regex Diacritics = new(@"[ؐ-ًؚ-ٰٟۖ-ۭـ]", RegexOptions.Compiled);
    private static readonly Regex NonLetters = new(@"[^ء-غف-ي\s]", RegexOptions.Compiled);

    /// <summary>
    /// Words of the text after the corpus normalization. <paramref name="fold"/> also merges ى/ي and ؤ/ئ/ء forms
    /// so that scoring tolerates typing variants; retrieval uses the unfolded form to match the stored text.
    /// </summary>
    public static List<string> Tokenize(string text, bool fold, bool stem = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var t = ArabicNormalizer.Normalize(Diacritics.Replace(text, string.Empty));
        t = NonLetters.Replace(t, " ");
        if (fold || stem) t = t.Replace('ى', 'ي').Replace('ؤ', 'ء').Replace('ئ', 'ء');
        var tokens = t.Split(' ', StringSplitOptions.RemoveEmptyEntries).AsEnumerable();
        // Scoring only: drop prepositions and attached one-letter prefixes so that "لأحدكم" equals "إلى أحدكم".
        // Not used by the negation check, where stripping would turn ليست into يست.
        if (stem) tokens = tokens.Where(w => !Prepositions.Contains(w)).Select(StripPrefix);
        return tokens.ToList();
    }

    /// <summary>Removes one attached prefix letter (و ف ب ل ك) when at least three letters remain.</summary>
    public static string StripPrefix(string word) =>
        word.Length >= 4 && "\u0648\u0641\u0628\u0644\u0643".Contains(word[0]) ? word[1..] : word;

    /// <summary>Prepositions (folded form) that differ between wordings without changing the hadith.</summary>
    private static readonly HashSet<string> Prepositions = ["الي", "علي", "في", "من", "عن"];

    private static readonly HashSet<string> StopWords = new(
        new[] { "قال", "عن", "رسول", "الله", "صلي", "صلى", "عليه", "وسلم", "ان", "من", "في", "ما", "لا", "الي", "الى", "على",
         "هذا", "هذه", "ذلك", "كان", "يا", "ثم", "او", "ولا", "قالوا", "انه", "انها", "الذي", "التي" }
        .Select(w => ArabicNormalizer.Normalize(w)));

    private static string StripFences(string? raw)
    {
        var json = (raw ?? string.Empty).Trim();
        if (json.StartsWith("```json")) json = json[7..];
        if (json.StartsWith("```")) json = json[3..];
        if (json.EndsWith("```")) json = json[..^3];
        return json.Trim();
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static string DefaultExplanation(string status) => status == HadithVerificationStatus.Exact
        ? "النص مطابق لمتن الحديث المذكور في المصدر أدناه."
        : "اللفظ قريب من المتن المذكور وليس مطابقاً له؛ قارن بين النصين قبل النسبة.";

    private static HadithVerificationResultDto NotFound(string method = "lexical", string explanation = "") => new()
    {
        Status = HadithVerificationStatus.NotFound,
        Method = method,
        Explanation = string.IsNullOrWhiteSpace(explanation)
            ? "لم نجد نصاً مطابقاً في الكتب المتاحة لدينا. هذا لا يعني أن الحديث مختلق، فقد يكون في كتب أخرى أو بلفظ آخر؛ ولا ننسبه إلى أي مصدر."
            : explanation
    };
}
