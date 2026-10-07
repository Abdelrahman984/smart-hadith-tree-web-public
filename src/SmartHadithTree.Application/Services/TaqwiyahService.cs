using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using System.Linq;
using System.Collections.Generic;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Application.Services;

public class TaqwiyahService : ITaqwiyahService
{
    public void CalculateTreeStrength(ComparativeTreeResponseDto tree)
    {
        CalculateStructuralStrength(tree);
        ApplyIlal(tree);
    }

    /// <summary>
    /// Grades the hadith from its routes (turuq). Each route is as strong as its weakest narrator
    /// above the compiler; routes that share the same weakest narrator are one weakness, not two.
    /// A narrator with no grade is no verdict: a route through one is judged only when the graded ones already
    /// make it weak, otherwise it is left out, and a hadith none of whose routes can be judged is «غير محرر».
    /// </summary>
    private static void CalculateStructuralStrength(ComparativeTreeResponseDto tree)
    {
        var all = tree.IlalReport?.Turuq ?? [];
        var routes = all.Where(t => t.WeakestTier.HasValue && (t.UnratedNarratorCount == 0 || t.WeakestTier >= 6)).ToList();
        var pathStrengths = routes.Select(r => r.WeakestTier!.Value).ToList();
        var undetermined = all.Count(t => t.UnratedNarratorCount > 0 && !(t.WeakestTier >= 6));

        if (pathStrengths.Count == 0)
        {
            if (undetermined > 0)
            {
                tree.CalculatedGrade = "غير محرر";
                tree.TaqwiyahDetails = $"تعذّر الحكم: في كل الطرق ({undetermined}) رواة لم تُحرَّر أحوالهم في الكتب المعتمدة.";
                return;
            }

            tree.CalculatedGrade = "مجهول";
            tree.TaqwiyahDetails = "تعذّر تحديد رجال الأسانيد.";
            return;
        }

        var bestPath = pathStrengths.Min(); // lowest tier number is best

        if (bestPath <= 3)
        {
            tree.CalculatedGrade = "صحيح";
            tree.TaqwiyahDetails = "يوجد إسناد صحيح مستقل.";
        }
        else if (bestPath == 4 || bestPath == 5)
        {
            tree.CalculatedGrade = "حسن";
            tree.TaqwiyahDetails = "يوجد إسناد حسن لذاته.";
        }
        else if (bestPath >= 6 && bestPath <= 8)
        {
            // Check for Taqwiyah
            // Independent weak routes only: routes failing at the same narrator do not reinforce each other.
            var weakPathsCount = routes
                .Where(r => r.WeakestTier is >= 6 and <= 8)
                .Select(r => r.WeakestNarratorId)
                .Distinct()
                .Count();
            if (weakPathsCount >= 2)
            {
                tree.CalculatedGrade = "حسن لغيره";
                tree.TaqwiyahDetails = $"ارتقى الحديث إلى الحسن لغيره بمجموع {weakPathsCount} طرق ضعيفة.";
            }
            else
            {
                tree.CalculatedGrade = "ضعيف";
                tree.TaqwiyahDetails = "إسناد ضعيف ولم يوجد ما يجبره.";
            }

            // The weakness is of the judged routes; the others may be sound, so do not call the hadith weak.
            if (undetermined > 0)
            {
                tree.CalculatedGrade = "غير محرر";
                tree.TaqwiyahDetails = $"الطرق المحررة ضعيفة، وفي {undetermined} طرق أخرى رواة لم تُحرَّر أحوالهم فلا يُجزم بضعف الحديث.";
            }
        }
        else if (bestPath > 8)
        {
            tree.CalculatedGrade = "موضوع / متروك";
            tree.TaqwiyahDetails = "الحديث شديد الضعف أو موضوع، لا ينجبر بتعدد الطرق.";
        }

        if (undetermined > 0 && tree.CalculatedGrade is "صحيح" or "حسن")
            tree.TaqwiyahDetails += $" ولم تدخل في الحكم {undetermined} طرق فيها رواة لم تُحرَّر أحوالهم.";
    }

    /// <summary>
    /// Adjusts the grade using the Ilal report: a hadith whose every tariq carries a decisive
    /// defect (علة قادحة) is ma'lul, regardless of how strong its narrators look.
    /// </summary>
    private static void ApplyIlal(ComparativeTreeResponseDto tree)
    {
        var report = tree.IlalReport;
        if (report == null || !report.HasQadihah) return;

        var qadihah = report.Findings.Where(f => f.Severity == IllahSeverity.Qadihah).ToList();
        var defectiveHadiths = qadihah.SelectMany(f => f.HadithIds).ToHashSet();
        var sourceIds = tree.Sources.Count > 0
            ? tree.Sources.Select(s => s.HadithId).ToList()
            : report.AnalyzedHadithIds;
        var titles = string.Join("، ", qadihah.Select(f => f.TitleAr).Distinct());

        if (sourceIds.Count > 0 && sourceIds.All(defectiveHadiths.Contains))
        {
            if (tree.CalculatedGrade == "موضوع / متروك") return;

            var apparent = tree.CalculatedGrade;
            tree.CalculatedGrade = "ضعيف (معلول)";
            tree.TaqwiyahDetails = string.IsNullOrEmpty(apparent)
                ? $"أُعلّ الحديث في جميع طرقه بـ: {titles}."
                : $"ظاهر الإسناد ({apparent})، لكن أُعلّ الحديث في جميع طرقه بـ: {titles}.";
        }
        else
        {
            tree.TaqwiyahDetails = $"{tree.TaqwiyahDetails} تنبيه: في بعض الطرق علة قادحة ({titles}).".Trim();
        }
    }
}
