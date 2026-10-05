using System.Text.Json;
using Microsoft.SemanticKernel;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Asks the LLM (via Semantic Kernel) to explain an Ilal report in scholarly Arabic.
/// The prompt contains only the rule engine's findings, so the model explains rather than judges.
/// </summary>
public class IlalExplanationService(Kernel kernel) : IIlalExplanationService
{
    public async Task<IlalExplanationDto> ExplainAsync(IlalReportDto report, CancellationToken ct = default)
    {
        if (report.Findings.Count == 0)
        {
            return new IlalExplanationDto
            {
                ExplanationAr = report.SummaryAr,
                Caveats = ["لم تُرصد علل آلياً، وهذا لا ينفي وجود علة يكشفها جمع الطرق والنظر في كتب العلل."]
            };
        }

        var findingsText = string.Join("\n", report.Findings.Select((f, i) =>
            $"{i + 1}. [{SeverityLabel(f.Severity)}] {f.TitleAr}: {f.EvidenceAr}"));

        var madarsText = report.Madars.Count == 0
            ? "لا يوجد"
            : string.Join("، ", report.Madars.Select(m => $"{m.NarratorName} ({m.BranchCount} فروع)"));

        var prompt = $@"
أنت ناقد متخصص في علم علل الحديث، على منهج أئمة النقد كابن أبي حاتم والدارقطني وابن رجب.
بين يديك نتائج فحص آلي لطرق حديث واحد. مهمتك شرح هذه النتائج بلغة علمية موجزة للباحث.

قواعد صارمة:
- لا تضف أي علة أو راوٍ أو حكم غير مذكور في النتائج أدناه.
- لا تُصدر حكماً نهائياً على الحديث؛ بيّن ما تقتضيه النتائج وما يحتاج إلى تحقق.
- رتّب الشرح: المدار واختلاف الرواة عليه، ثم العلل القادحة، ثم غير القادحة، ثم التنبيهات.

مدار الحديث: {madarsText}
الملخص الآلي: {report.SummaryAr}

النتائج:
{findingsText}

أرجع كائن JSON صالحاً فقط بالشكل التالي بدون أي نص إضافي أو علامات Markdown:
{{
  ""ExplanationAr"": ""الشرح في فقرات قصيرة"",
  ""Caveats"": [""تنبيه منهجي 1"", ""تنبيه منهجي 2""]
}}";

        try
        {
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var json = result.GetValue<string>()?.Trim();

            if (json != null)
            {
                if (json.StartsWith("```json")) json = json[7..];
                if (json.StartsWith("```")) json = json[3..];
                if (json.EndsWith("```")) json = json[..^3];

                var dto = JsonSerializer.Deserialize<IlalExplanationDto>(json.Trim(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (dto != null) return dto;
            }

            return new IlalExplanationDto { ExplanationAr = "تعذّر توليد الشرح." };
        }
        catch (Exception ex)
        {
            return new IlalExplanationDto { ExplanationAr = $"حدث خطأ أثناء توليد الشرح: {ex.Message}" };
        }
    }

    private static string SeverityLabel(IllahSeverity severity) => severity switch
    {
        IllahSeverity.Qadihah => "علة قادحة",
        IllahSeverity.GhayrQadihah => "علة غير قادحة",
        _ => "تنبيه"
    };
}
