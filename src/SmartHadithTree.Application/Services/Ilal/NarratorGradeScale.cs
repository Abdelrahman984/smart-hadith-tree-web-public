using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Shared mapping to the numeric T1–T12 scale used by both the Taqwiyah and Ilal engines (lower = stronger).
/// The scale is Ibn Hajar's twelve ranks of Taqrib al-Tahdhib (<c>Narrator.IbnHajarRank</c>): 1 Companion, 2 ثقة ثبت,
/// 3 ثقة, 4 صدوق, 5 صدوق يهم, 6 مقبول, 7 مستور, 8 ضعيف, 9 مجهول, 10 متروك, 11 متهم, 12 كذاب.
/// Overloads taking a <c>rank</c> use it when present and fall back to the legacy Itqan grade (grade_en) otherwise.
/// </summary>
public static class NarratorGradeScale
{
    /// <summary>Default tier for missing or unrecognized grades (ضعيف / مجهول).</summary>
    public const int DefaultTier = 7;

    private static readonly string[] RankLabels =
        ["", "صحابي", "ثقة ثبت", "ثقة", "صدوق", "صدوق يهم", "مقبول", "مستور", "ضعيف", "مجهول", "متروك", "متهم", "كذاب"];

    /// <summary>The tier of a narrator: Ibn Hajar's rank when known, else the legacy grade, else <see cref="DefaultTier"/>.</summary>
    public static int ToTier(int? rank, string? gradeEn) =>
        rank is >= 1 and <= 12 ? rank.Value : ToTier(gradeEn);

    /// <summary>Arabic label of Ibn Hajar's rank, else of the legacy grade.</summary>
    public static string ToArabicLabel(int? rank, string? gradeEn) =>
        rank is >= 1 and <= 12 ? RankLabels[rank.Value] : ToArabicLabel(gradeEn);

    /// <summary>True for a Companion: rank 1, the legacy grade, or a tabaqa that says so.</summary>
    public static bool IsCompanion(int? rank, string? gradeEn, string? generationTier) =>
        rank == 1 || IsCompanion(gradeEn, generationTier);

    /// <summary><see cref="IsTabii(string?, string?)"/> with Ibn Hajar's rank: a Companion is not a Successor.</summary>
    public static bool? IsTabii(int? rank, string? gradeEn, string? generationTier) =>
        rank == 1 ? false : IsTabii(gradeEn, generationTier);

    /// <summary>
    /// The legacy grade string the API and the frontend still use («companion», «reliable», «mostly_reliable»,
    /// «weak», «unknown», «abandoned», «fabricator») for a rank; null when there is no rank.
    /// </summary>
    public static string? ToGradeEn(int? rank) => rank switch
    {
        1 => "companion",
        2 or 3 => "reliable",
        4 or 5 => "mostly_reliable",
        6 or 7 or 8 => "weak",
        9 => "unknown",
        10 or 11 => "abandoned",
        12 => "fabricator",
        _ => null
    };

    /// <summary>Maps an Itqan grade_en to the T1–T12 tier number.</summary>
    public static int ToTier(string? gradeEn)
    {
        if (string.IsNullOrEmpty(gradeEn)) return DefaultTier;
        var g = gradeEn.ToLowerInvariant();
        if (g.Contains("companion")) return 1;
        if (g.Contains("reliable") && g.Contains("mostly")) return 4;
        if (g.Contains("reliable")) return 3;
        if (g.Contains("weak")) return 7;
        if (g.Contains("abandoned")) return 9;
        if (g.Contains("fabricator")) return 12;
        return DefaultTier;
    }

    /// <summary>Arabic label for an Itqan grade, for use in explanations.</summary>
    public static string ToArabicLabel(string? gradeEn)
    {
        if (string.IsNullOrEmpty(gradeEn)) return "غير محرر";
        var g = gradeEn.ToLowerInvariant();
        if (g.Contains("companion")) return "صحابي";
        if (g.Contains("reliable") && g.Contains("mostly")) return "صدوق";
        if (g.Contains("reliable")) return "ثقة";
        if (g.Contains("weak")) return "ضعيف";
        if (g.Contains("abandoned")) return "متروك";
        if (g.Contains("fabricator")) return "كذاب";
        if (g.Contains("unknown")) return "مجهول";
        return "غير محرر";
    }

    /// <summary>True when the narrator is a Companion (صحابي).</summary>
    public static bool IsCompanion(string? gradeEn, string? generationTier)
    {
        if (!string.IsNullOrEmpty(gradeEn) && gradeEn.Contains("companion", StringComparison.OrdinalIgnoreCase))
            return true;
        return MatnText.NormalizeForComparison(generationTier).Contains("صحاب");
    }

    /// <summary>
    /// True when the narrator is known to be a Successor (تابعي), false when known not to be,
    /// and null when the generation cannot be determined from the data.
    /// </summary>
    public static bool? IsTabii(string? gradeEn, string? generationTier)
    {
        if (IsCompanion(gradeEn, generationTier)) return false;

        var tier = MatnText.NormalizeForComparison(generationTier);
        if (string.IsNullOrEmpty(tier)) return null;

        if (tier.Contains("اتباع") || tier.Contains("تابعي التابعين") || tier.Contains("تبع"))
            return false;
        if (tier.Contains("تابع"))
            return true;

        // Ibn Hajr's tabaqat in Taqrib al-Tahdhib: 2–5 are Successors, 6+ are later.
        string[] tabiiOrdinals = ["الثانيه", "الثالثه", "الرابعه", "الخامسه"];
        string[] laterOrdinals = ["السادسه", "السابعه", "الثامنه", "التاسعه", "العاشره", "الحاديه عشره", "الثانيه عشره"];
        if (laterOrdinals.Any(tier.Contains)) return false;
        if (tabiiOrdinals.Any(tier.Contains)) return true;

        var digits = new string(tier.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n))
            return n is >= 2 and <= 5;

        return null;
    }
}
