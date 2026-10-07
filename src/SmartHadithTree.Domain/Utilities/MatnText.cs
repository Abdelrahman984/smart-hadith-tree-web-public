using System.Text.RegularExpressions;

namespace SmartHadithTree.Domain.Utilities;

/// <summary>
/// Heuristics for working with hadith texts whose isnad and matn are stored together
/// (as in the Itqan dataset): tokenization, locating the matn body, and detecting
/// whether the text is attributed to the Prophet ﷺ (مرفوع).
/// </summary>
public static partial class MatnText
{
    // Honorifics vary between books and would otherwise show up as textual variants.
    [GeneratedRegex(@"صل[يى]\s+الله\s+عليه\s+(?:و?اله\s+)?وسلم|ﷺ|رضي\s+الله\s+عنهما|رضي\s+الله\s+عنهم|رضي\s+الله\s+عنها|رضي\s+الله\s+عنه|عليه\s+(?:السلام|الصلا[ةه]\s+والسلام)")]
    private static partial Regex HonorificsRegex();

    [GeneratedRegex(@"[^\p{L}\s]")]
    private static partial Regex NonLetterRegex();

    [GeneratedRegex(@"\p{Mn}|ـ")]
    private static partial Regex DiacriticsRegex();

    // Phrases that attribute the text to the Prophet ﷺ. Matched against normalized text.
    [GeneratedRegex(@"(رسول\s+الله|النبي|نبي\s+الله)")]
    private static partial Regex ProphetMarkerRegex();

    // The start of the matn: a transmission/speech verb directly followed by a reference to the Prophet ﷺ.
    // The prefix letters (و / ف) are allowed, but the verb must start a word: "فقال رسول الله" is a start,
    // while the "كان" inside "وكانت" is not. «كنت مع رسول الله» (a Companion narrating his own presence) starts the matn too.
    [GeneratedRegex(@"(?<!\p{L})[وف]?(?:قال|ان|عن|سمعت|سمع|رايت|كان|يقول|كنت\s+مع)\s+(?:رسول\s+الله|النبي|نبي\s+الله)")]
    private static partial Regex MatnStartRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    // The last transmission formula of the isnad, used when no Prophet marker exists (موقوف).
    [GeneratedRegex(@"(?:^|\s)(حدثنا|حدثني|اخبرنا|اخبرني|انبانا|سمعت|عن)\s", RegexOptions.RightToLeft)]
    private static partial Regex LastTransmissionTermRegex();

    [GeneratedRegex(@"\s(قال|يقول|انه|انها|ان)\s")]
    private static partial Regex SpeechStartRegex();

    // Compiler commentary, editorial grading notes, or a following isnad appended after the matn in some books.
    [GeneratedRegex(@"(?:^|\s)(قال\s+ابو\s+عيسي|قال\s+ابو\s+داود|قال\s+ابو\s+عبد\s+الرحمن|قال\s+ابو\s+عبد\s+الله|قال\s+ابو\s+الحسن|قال\s+الشيخ|قال\s+النسايي|قال\s+الاعظمي|قال\s+الالباني|قال\s+شعيب|قال\s+حسين\s+سليم|قال\s+المحقق|اسناده\s+صحيح|اسناده\s+حسن|اسناده\s+ضعيف|هذا\s+حديث|وفي\s+الباب\s+عن|وحدثنا|وحدثني|بهذا\s+الاسناد|فذكر\s+الحديث|فذكر\s+نحوه)(?:\s|$)")]
    private static partial Regex TrailingCommentaryRegex();

    // What a compiler or editor adds after (or in place of) the matn proper. Tested on normalized text, so
    // the editor's own brackets are already gone (see BracketedNoteRegex). Each alternative is a whole word:
    //  - a note on other routes or the hadith's grade: «رواه البخاري في الصحيح», «أخرجه مسلم», «لم يرو هذا الحديث عن», «حكم حسين»
    //  - a pointer to another text instead of the text itself: «فذكر مثله إلا أنه قال», «وذكر الحديث وقال فيه», «بمثل حديث»
    //  - the symbols of the printed edition: «ب د ع ف م تحفه اتحاف»
    [GeneratedRegex(@"(?:^|\s)(?:(?:و?كذلك\s+)?و?رواه|و?رواهما|و?اخرجه|و?اخرجاه|و?اخرجهما|لم\s+يرو|تفرد\s+به|لفظ\s+حديثهما|حكم\s+حسين|و?ذكر\s+الحديث|و?فذكر\s+(?:مثله|بمثله|بمثل|بنحو|نحو|الحديث)|بمثل\s+حديث|و?هذا\s+الحديث|ب\s+د\s+ع\s+ف\s+م|تحفه|اتحاف)(?:\s|$)")]
    private static partial Regex EditorialNoteRegex();

    // Another isnad pasted into the record: «حدثنا علي بن عبد الله» (a Companion saying «حدثنا رسول الله» is not one).
    // Used only once the start of the matn is known; before that, the isnad itself is still in the body.
    [GeneratedRegex(@"(?:^|\s)(?:حدثنا|حدثني|اخبرنا|اخبرني)\s+(?!رسول|النبي|نبي|الصادق)")]
    private static partial Regex PastedIsnadRegex();

    // «[حكم حسين سليم أسد]: …», «[٦٦١]», «[عن الأعمش]»: the editor's insertions and page markers, always in square brackets.
    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex BracketedNoteRegex();

    /// <summary>
    /// Normalizes text for comparison: strips diacritics, tatweel, honorifics and punctuation,
    /// and unifies alef / ta marbuta / alef maqsura forms.
    /// </summary>
    public static string NormalizeForComparison(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var s = DiacriticsRegex().Replace(text, "");
        s = ArabicNormalizer.Normalize(s).Replace('ى', 'ي').Replace('ؤ', 'و').Replace('ئ', 'ي');
        s = HonorificsRegex().Replace(s, " ");
        s = NonLetterRegex().Replace(s, " ");
        return WhitespaceRegex().Replace(s, " ").Trim();
    }

    /// <summary>Splits normalized text into word tokens.</summary>
    public static string[] Tokenize(string? text) =>
        NormalizeForComparison(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Returns the normalized matn body (the text after the isnad), without trailing compiler
    /// commentary. When the start of the matn cannot be located, the text after the last
    /// transmission formula is used, and failing that the whole normalized text.
    /// </summary>
    public static string ExtractBody(string? fullText)
    {
        var normalized = NormalizeForComparison(BracketedNoteRegex().Replace(fullText ?? string.Empty, " "));
        if (normalized.Length == 0) return normalized;

        int start;
        var match = MatnStartRegex().Match(normalized);
        if (match.Success)
        {
            start = match.Index;
        }
        else
        {
            // Mawquf texts: skip past the last transmission formula, then the narrator's name.
            start = 0;
            var lastTerm = LastTransmissionTermRegex().Match(normalized, (int)(normalized.Length * 0.6));
            if (lastTerm.Success)
            {
                var speech = SpeechStartRegex().Match(normalized, lastTerm.Index + lastTerm.Length);
                start = speech.Success ? speech.Index + 1 : lastTerm.Index + lastTerm.Length;
            }
        }

        var body = normalized[start..];
        var tail = new[]
            {
                TrailingCommentaryRegex().Match(body),
                EditorialNoteRegex().Match(body),
                match.Success ? PastedIsnadRegex().Match(body) : Match.Empty
            }
            .Where(m => m.Success && m.Index > 0)
            .OrderBy(m => m.Index)
            .FirstOrDefault();
        if (tail != null)
            body = body[..tail.Index];

        return body.Trim();
    }

    // «سمعت رسول ﷺ»: a printed edition drops «الله», but the honorific after «رسول» still says whom it means.
    [GeneratedRegex(@"رسول\s*(?:ﷺ|صل[يى]\s+الله)")]
    private static partial Regex MessengerWithHonorificRegex();

    /// <summary>
    /// True when the text attributes a saying or act to the Prophet ﷺ (مرفوع);
    /// false suggests the text stops at a Companion or later narrator (موقوف / مقطوع).
    /// </summary>
    public static bool IsMarfu(string? fullText) =>
        ProphetMarkerRegex().IsMatch(NormalizeForComparison(fullText))
        || MessengerWithHonorificRegex().IsMatch(DiacriticsRegex().Replace(fullText ?? string.Empty, ""));
}
