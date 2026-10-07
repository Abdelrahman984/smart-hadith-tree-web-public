using System.Text.RegularExpressions;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

/// <summary>
/// The same narrator is written differently in different isnads: the registry holds his full name, while the
/// books mention him as "سفيان", "ابن عيينة" or "الزهري". Given the isnad texts the user picked, this finds the form
/// of his own name that those texts use most often, so the tree can show the name the reader sees in the matns.
/// </summary>
public static partial class NarratorMentionedName
{
    private static readonly HashSet<string> Connectors = ["بن", "ابن", "بنت"];
    private static readonly HashSet<string> CompoundFirstWords = ["عبد", "ابي", "ابو", "ام"];

    /// <summary>
    /// The form of the narrator's name most often found in <paramref name="isnadTexts"/>, spelled as in his registry
    /// name; null when no form of it is found (the caller keeps its own display name).
    /// </summary>
    public static string? Choose(
        string? fullName, string? knownAs, IEnumerable<string?> isnadTexts, string? kunyah = null,
        ISet<string>? ambiguousForms = null)
    {
        var candidates = Candidates(fullName, knownAs, kunyah)
            .Where(c => ambiguousForms?.Contains(string.Join(' ', c.Tokens)) != true)
            .ToList();
        if (candidates.Count == 0) return null;

        var texts = isnadTexts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => Tokenize(t!))
            .ToList();
        if (texts.Count == 0) return null;

        string? best = null;
        string[] bestTokens = [];
        var bestCount = 0;
        foreach (var (display, tokens) in candidates)
        {
            var count = texts.Sum(text => CountOccurrences(text, tokens));
            if (count == 0) continue;
            if (count > bestCount || (count == bestCount && tokens.Length > bestTokens.Length))
            {
                best = display;
                bestTokens = tokens;
                bestCount = count;
            }
        }

        return best == null ? null : WithNisbah(best, bestTokens, fullName, texts);
    }

    /// <summary>
    /// The chosen form followed by the narrator's own nisbah when an isnad writes it after the name
    /// ("علقمة بن وقاص" + "الليثي"). The nisbah also tells apart two narrators who share the short name.
    /// </summary>
    private static string WithNisbah(string name, string[] nameTokens, string? fullName, List<string[]> texts)
    {
        var nisbahs = (fullName ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && w.StartsWith("ال", StringComparison.Ordinal) && w != "الله")
            .Distinct()
            .Select(w => (Word: w, Token: Tokenize(w).FirstOrDefault()))
            .Where(n => n.Token != null)
            .ToList();

        // A form that is itself the nisbah ("الثوري") has nothing to add.
        if (nameTokens.Length == 1 && nisbahs.Any(n => n.Token == nameTokens[0])) return name;

        string? bestWord = null;
        var bestCount = 0;
        foreach (var (word, token) in nisbahs)
        {
            if (nameTokens.Contains(token!)) continue;
            var withNisbah = nameTokens.Append(token!).ToArray();
            var count = texts.Sum(text => CountOccurrences(text, withNisbah));
            if (count > bestCount)
            {
                bestWord = word;
                bestCount = count;
            }
        }

        return bestWord == null ? name : $"{name} {bestWord}";
    }

    /// <summary>How often each normalized form of the narrator's name occurs in the given isnad texts.</summary>
    public static Dictionary<string, int> Counts(
        string? fullName, string? knownAs, IEnumerable<string?> isnadTexts, string? kunyah = null)
    {
        var texts = isnadTexts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => Tokenize(t!)).ToList();
        return Candidates(fullName, knownAs, kunyah).ToDictionary(
            c => string.Join(' ', c.Tokens),
            c => texts.Sum(text => CountOccurrences(text, c.Tokens)));
    }

    /// <summary>
    /// The normalized forms of this narrator's name. A form that several narrators of one tree share ("أبو بكر",
    /// "محمد", "سفيان") cannot tell which of them an isnad means, so the caller collects these and leaves them out.
    /// </summary>
    public static IEnumerable<string> FormKeys(string? fullName, string? knownAs, string? kunyah = null) =>
        Candidates(fullName, knownAs, kunyah).Select(c => string.Join(' ', c.Tokens));

    /// <summary>Forms of the name worth looking for, each with its display spelling and normalized tokens.</summary>
    private static List<(string Display, string[] Tokens)> Candidates(string? fullName, string? knownAs, string? kunyah)
    {
        var forms = new List<string>();

        var name = Clean(fullName);
        if (name.Length > 0)
        {
            var parts = PatronymicSplit().Split(name);
            // Split keeps the connectors: [segment, connector, segment, connector, segment ...]
            var segments = parts.Where((_, i) => i % 2 == 0).Select(s => s.Trim()).ToList();
            var connectors = parts.Where((_, i) => i % 2 == 1).Select(c => c.Trim()).ToList();

            var first = segments[0];
            forms.Add(first);

            if (segments.Count > 1)
            {
                var father = FirstName(segments[1]);
                forms.Add($"{first} {connectors[0]} {father}");
                if (connectors[0] != "بنت") forms.Add($"ابن {father}");

                if (segments.Count > 2)
                    forms.Add($"{first} {connectors[0]} {father} {connectors[1]} {FirstName(segments[2])}");

                // The nisbah closing the name ("الثوري"): alone, and after the first name ("سفيان الثوري").
                foreach (var nisbah in segments[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1)
                             .Where(w => w.Length > 3 && w.StartsWith("ال", StringComparison.Ordinal)))
                {
                    forms.Add(nisbah);
                    forms.Add($"{first} {nisbah}");
                }
            }
        }

        foreach (var known in $"{knownAs}،{kunyah}".Split(['،', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            var form = Clean(known);
            if (form.Length > 1) forms.Add(form);
        }

        return forms
            .Select(f => (Display: f, Tokens: Tokenize(f)))
            .Where(c => c.Tokens.Length > 0)
            .GroupBy(c => string.Join(' ', c.Tokens))
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>The name without annotations such as "(…)", "، وقيل …" or "ويقال …".</summary>
    private static string Clean(string? raw)
    {
        var name = (raw ?? "").Trim();
        name = Annotation().Split(name)[0].Trim();
        return name;
    }

    /// <summary>A father's or grandfather's name: one word, or two for "عبد الله", "أبي شيبة".</summary>
    private static string FirstName(string segment)
    {
        var words = segment.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return segment;
        return words.Length > 1 && CompoundFirstWords.Contains(Tokenize(words[0]).FirstOrDefault() ?? "")
            ? $"{words[0]} {words[1]}"
            : words[0];
    }

    /// <summary>
    /// How many times the tokens occur in the text. A form without "بن" of its own, followed by "بن" ("محمد بن …",
    /// "أبو بكر بن أبي شيبة"), is the start of a longer name, not a mention of the shorter one, so it is left out.
    /// </summary>
    private static int CountOccurrences(string[] text, string[] tokens)
    {
        var count = 0;
        for (var i = 0; i + tokens.Length <= text.Length; i++)
        {
            var match = true;
            for (var j = 0; j < tokens.Length && match; j++)
                match = text[i + j] == tokens[j] || (j == 0 && text[i + j] == "و" + tokens[j]);
            if (!match) continue;

            // Right after "بن" or "أبو" the words are the rest of someone's nasab or kunyah ("محمد بن عبد الله",
            // "أبو عبد الله"), not a mention of a narrator of that name.
            if (i > 0 && !Connectors.Contains(tokens[0]) && tokens[0] != "ابو" &&
                (Connectors.Contains(text[i - 1]) || text[i - 1] == "ابو")) continue;

            var next = i + tokens.Length;
            if (next < text.Length && Connectors.Contains(text[next]) && !tokens.Any(Connectors.Contains)) continue;
            count++;
        }
        return count;
    }

    /// <summary>Words of the text without diacritics, with alef/yaa/ta marbuta unified, "أبي/أبا" read as "أبو" and "عبد X" joined and a leading "ال" dropped.</summary>
    private static string[] Tokenize(string text)
    {
        var plain = ArabicNormalizer.Normalize(text)
            .Replace("ٰ", "")
            .Replace("ـ", "")
            .Replace('ى', 'ي');
        plain = NotLetters().Replace(plain, " ");
        var words = plain
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w is "ابي" or "ابا" ? "ابو" : w)
            .ToList();
        for (var i = 0; i < words.Count; i++)
            if (words[i].Length > 4 && words[i].StartsWith("ال", StringComparison.Ordinal) && words[i] != "الله")
                words[i] = words[i][2..];

        // "عبد الله" and "عبدالله" are the same name: join "عبد" to the word after it.
        var tokens = new List<string>();
        for (var i = 0; i < words.Count; i++)
            tokens.Add(words[i] == "عبد" && i + 1 < words.Count ? words[i] + words[++i] : words[i]);
        return tokens.ToArray();
    }

    [GeneratedRegex(@"(\s+(?:بن|ابن|بنت)\s+)")]
    private static partial Regex PatronymicSplit();

    [GeneratedRegex(@"\s*[\(\[\{،,]|\s+:\s+|\s+(?:ويقال|وقيل)\b")]
    private static partial Regex Annotation();

    [GeneratedRegex(@"[^\p{L}]+")]
    private static partial Regex NotLetters();
}
