using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.DTOs;
using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmartHadithTree.Application.Services;

public interface IAiEvaluationService
{
    Task<ExtractedAiEvaluationDto> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default);
}

public class AiEvaluationService(Kernel kernel) : IAiEvaluationService
{
    public async Task<ExtractedAiEvaluationDto> GenerateNarratorEvaluationSummaryAsync(NarratorDetailDto narrator, CancellationToken ct = default)
    {
        if (narrator.Evaluations == null || narrator.Evaluations.Count == 0)
        {
            // No source material: abstain. Never infer a grade for a narrator we know nothing about.
            return new ExtractedAiEvaluationDto
            {
                Status = AiEvaluationStatus.NoEvaluations,
                Justification = "لا توجد أقوال مسجلة لهذا الراوي في البيانات؛ لا يُصدر النظام تصنيفاً دون مرجع."
            };
        }

        var prompt = $@"
أنت باحث محقق في علم الجرح والتعديل.
مهمتك هي قراءة أقوال العلماء التالية واستخراج الاقتباس الأكثر دقة وحسماً (يفضل أقوال ابن حجر في تقريب التهذيب أو الذهبي).
يجب عليك عدم التأليف أو التلخيص، بل استخراج النص الحرفي.
ثم، قم بتعيين الطبقة (Tier) من T1 إلى T12 بناءً على هذا القول.

T1 = صحابي
T2 = ثقة متقن
T3 = ثقة
T4 = صدوق
T5 = صدوق يهم
T6 = مقبول
T7 = ضعيف / مجهول
T8 = ضعيف جدا
T9-T11 = متروك / متهم
T12 = كذاب / وضاع

معلومات الراوي:
الاسم: {narrator.KnownAs ?? narrator.FullName}
الطبقة: {narrator.GenerationTier ?? "غير محددة"}
بلدان الإقامة والرحلة: {narrator.ResidencePlaces ?? "غير محددة"}
بلد الوفاة: {narrator.DeathPlace ?? "غير محدد"}
الرتبة في جوامع الكلم: {narrator.GawamiRank ?? "غير محددة"}
حجم المرويات: {(narrator.UniqueHadithCount.HasValue ? $"{narrator.UniqueHadithCount} حديث/طرف ({narrator.TotalNarrationsCount ?? narrator.UniqueHadithCount} إسناد)" : "غير محدد")}
ملاحظات العلل: {(narrator.IsMudallis ? "موصوف بالتدليس. " : "")}{(narrator.HasMukhtalit ? "موصوف بالاختلاط. " : "")}

أقوال العلماء:
{string.Join("\n", narrator.Evaluations.Select(e => $"- {e.ScholarName} (في كتاب {e.SourceBook ?? "غير محدد"}): {e.EvaluationText}"))}

استخرج البيانات وقم بإرجاعها ككائن JSON صالح فقط بالشكل التالي بدون أي نصوص إضافية أو علامات Markdown:
{{
  ""VerbatimQuote"": ""النص الحرفي للقول"",
  ""SourceBook"": ""اسم الكتاب أو العالم"",
  ""Tier"": ""مثال: T4"",
  ""Justification"": ""سبب اختيار هذه الطبقة باختصار شديد""
}}";

        try
        {
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var json = result.GetValue<string>()?.Trim();
            
            if (json != null)
            {
                // Remove markdown block if present
                if (json.StartsWith("```json")) json = json.Substring(7);
                if (json.StartsWith("```")) json = json.Substring(3);
                if (json.EndsWith("```")) json = json.Substring(0, json.Length - 3);
                json = json.Trim();

                var dto = JsonSerializer.Deserialize<ExtractedAiEvaluationDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto != null) return Validate(dto, narrator.Evaluations.Select(e => e.EvaluationText));
            }

            return Unavailable();
        }
        catch (Exception)
        {
            // Do not leak exception details (provider errors may contain keys or endpoints) to the client.
            return Unavailable();
        }
    }

    private static ExtractedAiEvaluationDto Unavailable() => new()
    {
        Status = AiEvaluationStatus.Unavailable,
        Justification = "تعذر توليد الملخص الآلي حالياً. راجع أقوال العلماء الأصلية أدناه."
    };

    /// <summary>
    /// Accepts the model output only if the quote is verbatim from the supplied evaluations and the tier
    /// is a known value. Otherwise the result is marked unverified and carries no tier.
    /// </summary>
    public static ExtractedAiEvaluationDto Validate(ExtractedAiEvaluationDto dto, IEnumerable<string> evaluationTexts)
    {
        var grounded = IsQuoteGrounded(dto.VerbatimQuote, evaluationTexts);
        if (!grounded || !TierPattern.IsMatch(dto.Tier ?? string.Empty))
        {
            return new ExtractedAiEvaluationDto
            {
                Status = AiEvaluationStatus.Unverified,
                Justification = "لم يُتحقق من مطابقة الاقتباس لأقوال العلماء المسجلة، فلا يُعرض تصنيف آلي. راجع الأقوال الأصلية."
            };
        }

        dto.Status = AiEvaluationStatus.Ok;
        return dto;
    }

    /// <summary>True when the quote appears verbatim (after diacritics/tatweel/whitespace normalization) in any evaluation text.</summary>
    public static bool IsQuoteGrounded(string? quote, IEnumerable<string> evaluationTexts)
    {
        var q = NormalizeForMatch(quote);
        if (q.Length == 0) return false;
        return evaluationTexts.Any(t => NormalizeForMatch(t).Contains(q, StringComparison.Ordinal));
    }

    private static readonly Regex TierPattern = new(@"^T([1-9]|1[0-2])$", RegexOptions.Compiled);
    private static readonly Regex StripPattern = new(@"[\u0610-\u061A\u064B-\u065F\u0670\u06D6-\u06ED\u0640«»""'\u201C\u201D]", RegexOptions.Compiled);

    private static string NormalizeForMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var stripped = StripPattern.Replace(text, string.Empty);
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }
}
