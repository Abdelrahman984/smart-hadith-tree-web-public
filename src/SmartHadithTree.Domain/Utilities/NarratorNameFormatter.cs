using System.Text.RegularExpressions;

namespace SmartHadithTree.Domain.Utilities;

/// <summary>
/// Formats narrator names into concise, recognizable scholarly display names for 'Ilal reports,
/// Madar summaries, and UI labels, avoiding generic honorifics (e.g. "الحافظ") and overly long genealogies.
/// </summary>
public static partial class NarratorNameFormatter
{
    private static readonly HashSet<string> GenericTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "الحافظ",
        "شيخ الإسلام",
        "الحافظ ، شيخ الإسلام",
        "الإمام",
        "الفقيه",
        "القاضي",
        "الوراق",
        "المقرئ",
        "الزاهد",
        "العابد",
        "الحجة",
        "الأمين",
        "الشهيد",
        "الأمير",
        "الصغير",
        "الكبير",
        "الفراء",
        "الصغير ، الفراء",
        "النحاس",
        "الصرام",
        "الجرب",
        "مسلم"
    };

    [GeneratedRegex(@"\s*(?:،\s*ويقال|،\s*قيل|،\s*وقيل|،\s*وهو|،\s*قال|\s+قال\s+بن|\s+من\s+السابقين|:).*$")]
    private static partial Regex TrailingBioRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();

    /// <summary>
    /// Returns a concise, human-readable scholarly name for a narrator.
    /// </summary>
    public static string FormatDisplayName(string? fullName, string? knownAs = null, string? kunyah = null)
    {
        var rawFull = (fullName ?? string.Empty).Trim();
        var rawKnown = (knownAs ?? string.Empty).Trim().Trim('-', '.', '،', ' ');

        // 1. Check well-known canonical scholars by fullName / kunyah patterns first
        if (rawFull.StartsWith("محمد بن إسماعيل بن إبراهيم بن المغيرة", StringComparison.Ordinal))
            return "البخاري";
        if (rawFull.StartsWith("مسلم بن الحجاج", StringComparison.Ordinal))
            return "مسلم";
        if (rawFull.StartsWith("سليمان بن الأشعث", StringComparison.Ordinal))
            return "أبو داود";
        if (rawFull.StartsWith("محمد بن عيسى بن سورة", StringComparison.Ordinal))
            return "الترمذي";
        if (rawFull.StartsWith("أحمد بن شعيب بن علي", StringComparison.Ordinal))
            return "النسائي";
        if (rawFull.StartsWith("محمد بن ماجه", StringComparison.Ordinal) || rawFull.StartsWith("محمد بن يزيد بن ماجه", StringComparison.Ordinal))
            return "ابن ماجه";
        if (rawFull.StartsWith("أحمد بن محمد بن حنبل", StringComparison.Ordinal))
            return "أحمد بن حنبل";
        if (rawFull.StartsWith("عبد الله بن أحمد بن محمد بن حنبل", StringComparison.Ordinal))
            return "عبد الله بن أحمد بن حنبل";
        if (rawFull.StartsWith("سليمان بن أحمد بن أيوب", StringComparison.Ordinal))
            return "الطبراني";
        if (rawFull.StartsWith("أحمد بن الحسين بن علي بن موسى", StringComparison.Ordinal))
            return "البيهقي";
        if (rawFull.StartsWith("محمد بن عبد الله بن محمد بن حمدويه", StringComparison.Ordinal))
            return "الحاكم النيسابوري";
        if (rawFull.StartsWith("محمد بن إسحاق بن خزيمة", StringComparison.Ordinal))
            return "ابن خزيمة";
        if (rawFull.StartsWith("إسماعيل بن إبراهيم بن مقسم", StringComparison.Ordinal))
            return "إسماعيل بن علية";
        if (rawFull.StartsWith("عبد الله بن محمد بن أبي شيبة", StringComparison.Ordinal))
            return "أبو بكر بن أبي شيبة";
        if (rawFull.StartsWith("محمد بن يعقوب بن يوسف بن معقل", StringComparison.Ordinal))
            return "أبو العباس الأصم";

        // 2. Clean FullName of alternative lineage notes and embedded biographies
        var cleanedFull = TrailingBioRegex().Replace(rawFull, "").Trim(' ', '،', '.', '-', ':');
        cleanedFull = MultiSpaceRegex().Replace(cleanedFull, " ");

        var shortFull = ShortenNasab(cleanedFull);

        // 3. If knownAs is a specific, non-generic nickname (e.g. "الأعمش", "الأعور", "قتيبة"), append or use it
        if (!string.IsNullOrWhiteSpace(rawKnown) && !IsGenericTitle(rawKnown))
        {
            if (string.IsNullOrWhiteSpace(shortFull) || shortFull.Split(' ').Length == 1)
                return string.IsNullOrWhiteSpace(shortFull) ? rawKnown : $"{shortFull} ({rawKnown})";

            if (!shortFull.Contains(rawKnown, StringComparison.Ordinal))
                return $"{shortFull} ({rawKnown})";
        }

        if (!string.IsNullOrWhiteSpace(shortFull))
            return shortFull;

        return !string.IsNullOrWhiteSpace(rawKnown) ? rawKnown : "راوٍ غير مسمى";
    }

    private static bool IsGenericTitle(string knownAs)
    {
        if (GenericTitles.Contains(knownAs))
            return true;

        var parts = knownAs.Split(['،', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length > 0 && parts.All(p => GenericTitles.Contains(p));
    }

    private static string ShortenNasab(string cleanedFull)
    {
        if (string.IsNullOrWhiteSpace(cleanedFull))
            return string.Empty;

        var tokens = cleanedFull.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length <= 4)
            return cleanedFull;

        // Walk tokens and stop after the second "بن"/"ابن" segment (e.g. "محمد بن عمرو بن علقمة" or "المغيرة بن شعبة")
        var result = new List<string>();
        int binCount = 0;

        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token is "بن" or "ابن")
            {
                binCount++;
                if (binCount > 2)
                    break;

                result.Add(token);
                // Add the father's name (including compound names like "عبد الله" or "أبي شيبة")
                if (i + 1 < tokens.Length)
                {
                    i++;
                    result.Add(tokens[i]);
                    if (tokens[i] is "عبد" or "أبو" or "أبي" or "أبا" or "ذو" or "ذي" && i + 1 < tokens.Length)
                    {
                        i++;
                        result.Add(tokens[i]);
                    }
                }
                // For well-known 2-part names (Companion or famous sheikh), 1 "بن" is often enough if the next is a distant ancestor,
                // but keeping up to 2 "بن" distinguishes homonyms like "محمد بن عمرو بن علقمة" vs "محمد بن عمرو بن خالد".
            }
            else
            {
                result.Add(token);
            }
        }

        return string.Join(" ", result);
    }
}
