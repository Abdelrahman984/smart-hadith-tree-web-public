using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Application.Services;

public interface ISearchJudgeService
{
    Task<SearchJudgeResponseDto> JudgeAsync(string query, IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

/// <summary>
/// Optional AI check of the results on a search page: does each matn match the meaning of the search?
/// The server loads the matn itself and sends the model only a short window around the searched words.
/// A label is accepted only when the model returns the result's number exactly once and a quote of three or more
/// words that really occurs in that matn; anything else is "not judged", never a match or a non-match.
/// Large pages are split into chunks sent in parallel. With no model, a timeout or a failure the page keeps the
/// code-computed relevance.
/// </summary>
public class SearchJudgeService(IHadithTreeDbContext context, Kernel kernel) : ISearchJudgeService
{
    public const int MaxIds = 50;
    public const int ChunkSize = 20;
    private const int WindowChars = 350;
    private const int MaxCachedItems = 5000;

    /// <summary>Per-call time limit. Override with SEARCH_AI_TIMEOUT_SECONDS (5-90).</summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("SEARCH_AI_TIMEOUT_SECONDS"), out var seconds)
            ? Math.Clamp(seconds, 5, 90)
            : 20);

    private static readonly ConcurrentDictionary<string, SearchJudgeItemDto> Cache = new();
    private static readonly Regex Words = new(@"[ء-ي]+", RegexOptions.Compiled);

    public async Task<SearchJudgeResponseDto> JudgeAsync(string query, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var terms = RelevanceScorer.QueryTerms(query);
        var requested = ids.Distinct().Take(MaxIds).ToList();
        if (terms.Count == 0 || requested.Count == 0)
            return new SearchJudgeResponseDto { Status = SearchJudgeStatus.Invalid };

        var queryKey = string.Join(' ', terms);
        var judged = new Dictionary<Guid, SearchJudgeItemDto>();
        foreach (var id in requested)
            if (Cache.TryGetValue($"{queryKey}|{id}", out var cached)) judged[id] = cached;

        var missing = requested.Where(id => !judged.ContainsKey(id)).ToList();
        var failedChunks = 0;
        var timedOut = 0;
        var chunkCount = 0;
        var diagnostics = new Dictionary<string, int>();
        void Count(string key, int n = 1) => diagnostics[key] = diagnostics.GetValueOrDefault(key) + n;

        if (missing.Count > 0)
        {
            var rows = (await context.Hadiths.AsNoTracking()
                    .Where(h => missing.Contains(h.Id))
                    .Select(h => new Row(h.Id, h.BookName, h.HadithNumber, h.MatnArabic, h.NormalizedMatn))
                    .ToListAsync(ct))
                .ToDictionary(r => r.Id);
            var ordered = missing.Where(rows.ContainsKey).Select(id => rows[id]).ToList();
            var chunks = ordered.Chunk(ChunkSize).ToList();
            chunkCount = chunks.Count;

            var outcomes = await Task.WhenAll(chunks.Select(chunk => JudgeChunkAsync(query, terms, chunk, ct)));
            foreach (var outcome in outcomes)
            {
                foreach (var (reason, n) in outcome.Rejections) Count(reason, n);
                if (outcome.Items == null)
                {
                    failedChunks++;
                    if (outcome.TimedOut) timedOut++;
                    Count($"chunk-{outcome.Failure ?? "failed"}");
                    continue;
                }
                foreach (var item in outcome.Items)
                {
                    judged[item.Id] = item;
                    if (SearchJudgeLevel.IsJudged(item.Level)) CacheItem(queryKey, item);
                }
            }
        }

        return new SearchJudgeResponseDto
        {
            Status = Status(chunkCount, failedChunks, timedOut),
            Diagnostics = $"asked={requested.Count}; cached={requested.Count - missing.Count}; chunks={chunkCount}; failed-chunks={failedChunks}; " +
                          $"time-limit={CallTimeout.TotalSeconds:0}s" +
                          (diagnostics.Count == 0 ? "" : "; " + string.Join(", ", diagnostics.OrderBy(d => d.Key).Select(d => $"{d.Key}={d.Value}"))),
            Items = requested.Select(id => judged.TryGetValue(id, out var item)
                ? item
                : new SearchJudgeItemDto { Id = id }).ToList()
        };
    }

    private static string Status(int chunks, int failed, int timedOut) =>
        failed == 0 ? SearchJudgeStatus.Reviewed
        : failed < chunks ? SearchJudgeStatus.Partial
        : timedOut > 0 ? SearchJudgeStatus.Timeout
        : SearchJudgeStatus.Unavailable;

    private static void CacheItem(string queryKey, SearchJudgeItemDto item)
    {
        if (Cache.Count > MaxCachedItems) Cache.Clear();
        Cache[$"{queryKey}|{item.Id}"] = item;
    }

    private async Task<ChunkOutcome> JudgeChunkAsync(string query, List<string> terms, Row[] chunk, CancellationToken ct)
    {
        var rejections = new Dictionary<string, int>();
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var prompt = BuildPrompt(query, terms, chunk);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(CallTimeout);
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeout.Token);
            rejections[$"model-ms-{started.ElapsedMilliseconds}"] = 1;
            var reply = result.GetValue<string>();
            var items = ParseAndValidate(reply, chunk.Select(r => (r.Id, r.MatnArabic)).ToList(), rejections);
            if (rejections.Keys.Any(k => !k.StartsWith("model-ms-")))
                Console.WriteLine($"ai-judge: rejected answers. Raw reply (first 800 chars): {(reply ?? "<null>")[..Math.Min(800, reply?.Length ?? 6)]}");
            // An answer that could not be read at all is a failed call, not a page of "not judged" results.
            return rejections.ContainsKey("unreadable-json")
                ? new ChunkOutcome(null, false, "unreadable-json", rejections)
                : new ChunkOutcome(items, false, null, rejections);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            rejections[$"timeout-after-ms-{started.ElapsedMilliseconds}"] = 1;
            return new ChunkOutcome(null, true, "timeout", rejections);
        }
        catch (Exception)
        {
            // No model configured or a provider error. Do not leak details.
            rejections[$"error-after-ms-{started.ElapsedMilliseconds}"] = 1;
            return new ChunkOutcome(null, false, "model-error", rejections);
        }
    }

    private static string BuildPrompt(string query, List<string> terms, Row[] chunk)
    {
        var list = string.Join("\n", chunk.Select((r, i) => $"[{i + 1}] {Window(r, terms)}"));
        return $@"
أنت مساعد يفحص نتائج بحث في متون الأحاديث. لديك عبارة بحث، وقائمة مقاطع مرقمة من متون أحاديث.
مهمتك فقط الحكم على كل مقطع بواحد من ثلاثة مستويات:
- match: المقطع نفسه هو الحديث أو الرواية التي تقرر معنى عبارة البحث (ولو اختلف اللفظ يسيراً).
- partial: عبارة البحث أو معناها ترد في المقطع جزءاً من حديث أطول أو في سياق مختلف، أو تُذكر عرضاً في تعليق أو كلام عن الحديث وليس متنه.
- scattered: كلمات البحث موجودة لكن المقطع في موضوع آخر أو حديث مختلف.

قواعد صارمة:
- لا تحكم على صحة الحديث أو ضعفه.
- لا تضف معلومة من خارج المقاطع.
- لكل مقطع اكتب Quote: عبارة من أربع إلى سبع كلمات متتالية منسوخة حرفياً من المقطع نفسه تدعم حكمك، واكتبها بدون تشكيل.
  لا تقل أبداً عن ثلاث كلمات: إن كانت عبارة البحث أقصر فانسخ معها الكلمات التي تليها أو تسبقها في المقطع حتى تبلغ أربع كلمات.
- اجعل Reason أقل من ست كلمات.
- أعطِ حكماً واحداً لكل رقم، ولا تكرر رقماً ولا تترك رقماً.

عبارة البحث: {query}

المقاطع:
{list}

أرجع مصفوفة JSON صالحة فقط بدون أي نص إضافي أو علامات Markdown:
[{{""Index"": 1, ""Level"": ""match"", ""Quote"": ""عبارة حرفية"", ""Reason"": ""جملة قصيرة بالعربية""}}]";
    }

    /// <summary>A short window of the raw matn around the tightest group of searched words.</summary>
    private static string Window(Row row, List<string> terms)
    {
        var raw = row.MatnArabic;
        var score = RelevanceScorer.Score(row.NormalizedMatn, terms);
        var start = 0;
        if (score != null && row.NormalizedMatn.Length > 0)
            start = (int)((double)score.WindowStartChar / row.NormalizedMatn.Length * raw.Length);
        var begin = Math.Max(0, start - 80);
        var length = Math.Min(WindowChars, raw.Length - begin);
        var text = raw.Substring(begin, length).Trim();
        var shown = (begin > 0 ? "… " : "") + text + (begin + length < raw.Length ? " …" : "");

        var normalizedShown = Normalize(shown);
        var missing = terms.Count(t => !normalizedShown.Contains(t, StringComparison.Ordinal));
        return missing > 0 ? shown + " (ملاحظة: بعض كلمات البحث لا تظهر في هذا المقطع)" : shown;
    }

    /// <summary>
    /// Reads the model's JSON and keeps only answers that pass every check. Returns one item per row, in order;
    /// rows without a valid answer are <see cref="SearchJudgeLevel.NotJudged"/>.
    /// </summary>
    public static List<SearchJudgeItemDto> ParseAndValidate(
        string? raw, IReadOnlyList<(Guid Id, string Matn)> rows, Dictionary<string, int>? rejections = null)
    {
        void Reject(string reason) { if (rejections != null) rejections[reason] = rejections.GetValueOrDefault(reason) + 1; }

        var items = rows.Select(r => new SearchJudgeItemDto { Id = r.Id }).ToList();
        var json = (raw ?? string.Empty).Trim();
        if (json.StartsWith("```json")) json = json[7..];
        if (json.StartsWith("```")) json = json[3..];
        if (json.EndsWith("```")) json = json[..^3];
        json = json.Trim();

        // Some models put text before or after the array: keep the outermost [ ... ].
        var open = json.IndexOf('[');
        var close = json.LastIndexOf(']');
        if (open >= 0 && close > open) json = json[open..(close + 1)];

        List<Raw>? answers;
        try
        {
            answers = JsonSerializer.Deserialize<List<Raw>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            Reject("unreadable-json");
            return items;
        }
        if (answers == null)
        {
            Reject("unreadable-json");
            return items;
        }

        // A number the model repeats is ambiguous, so none of its answers is trusted.
        foreach (var group in answers.GroupBy(a => a.Index))
        {
            if (group.Count() > 1) { Reject("repeated-index"); continue; }
            var answer = group.Single();
            var index = answer.Index - 1;
            if (index < 0 || index >= rows.Count) { Reject("bad-index"); continue; }

            var level = (answer.Level ?? string.Empty).Trim().ToLowerInvariant();
            if (!SearchJudgeLevel.IsJudged(level)) { Reject("bad-level"); continue; }
            if (WordCount(answer.Quote) < 3) { Reject("quote-under-3-words"); continue; }
            if (!QuoteIsInMatn(answer.Quote, rows[index].Matn)) { Reject("quote-not-in-matn"); continue; }

            items[index].Level = level;
            items[index].Quote = answer.Quote!.Trim();
            items[index].Reason = Truncate((answer.Reason ?? string.Empty).Trim(), 200);
        }

        var unanswered = items.Count(i => i.Level == SearchJudgeLevel.NotJudged) - answers.Count;
        for (var i = 0; i < Math.Max(0, unanswered); i++) Reject("no-answer");
        return items;
    }

    /// <summary>
    /// True when the quote's words occur, in order and next to each other, in the matn. Diacritics, punctuation,
    /// hamza forms and alef maqsura / ya are ignored, so a harmless spelling difference does not reject an answer.
    /// </summary>
    public static bool QuoteIsInMatn(string? quote, string matn)
    {
        var q = FoldWords(quote);
        return q.Trim().Length > 0 && FoldWords(matn).Contains(q, StringComparison.Ordinal);
    }

    private static int WordCount(string? text) => Words.Matches(FoldWords(text)).Count;

    private static readonly Regex Marks = new(@"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640]", RegexOptions.Compiled);

    /// <summary>The text as space-separated folded words, with a space at both ends so whole words are matched.</summary>
    private static string FoldWords(string? text)
    {
        var t = Marks.Replace(text ?? string.Empty, string.Empty)
            .Replace('\u0671', '\u0627').Replace('\u0649', '\u064A').Replace('\u0624', '\u0621').Replace('\u0626', '\u0621');
        return " " + string.Join(' ', Words.Matches(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(t)).Select(m => m.Value)) + " ";
    }

    private static string Normalize(string? text) =>
        string.Join(' ', Words.Matches(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(text ?? string.Empty)).Select(m => m.Value));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private sealed record Row(Guid Id, string BookName, int HadithNumber, string MatnArabic, string NormalizedMatn);
    private sealed record ChunkOutcome(List<SearchJudgeItemDto>? Items, bool TimedOut, string? Failure, Dictionary<string, int> Rejections);

    private sealed class Raw
    {
        public int Index { get; set; }
        public string? Level { get; set; }
        public string? Quote { get; set; }
        public string? Reason { get; set; }
    }
}
