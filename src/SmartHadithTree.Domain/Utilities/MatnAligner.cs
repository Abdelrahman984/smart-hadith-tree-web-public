namespace SmartHadithTree.Domain.Utilities;

/// <summary>Kind of a run of tokens in an alignment.</summary>
public enum AlignmentKind
{
    Equal,
    /// <summary>Present only in the compared text (زيادة).</summary>
    Added,
    /// <summary>Present only in the reference text (نقص).</summary>
    Removed
}

/// <summary>A contiguous run of tokens sharing the same <see cref="AlignmentKind"/>.</summary>
public sealed record AlignmentSegment(AlignmentKind Kind, IReadOnlyList<string> Tokens)
{
    public string Text => string.Join(' ', Tokens);
}

/// <summary>The result of aligning two token sequences.</summary>
public sealed record AlignmentResult(IReadOnlyList<AlignmentSegment> Segments, double Similarity)
{
    public int AddedCount => Segments.Where(s => s.Kind == AlignmentKind.Added).Sum(s => s.Tokens.Count);
    public int RemovedCount => Segments.Where(s => s.Kind == AlignmentKind.Removed).Sum(s => s.Tokens.Count);

    /// <summary>Longest single added run (in tokens).</summary>
    public int LongestAddedRun => Segments.Where(s => s.Kind == AlignmentKind.Added).Select(s => s.Tokens.Count).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Number of tokens involved in substitutions: a removed run directly followed or preceded
    /// by an added run (the compared text says something different at the same position).
    /// </summary>
    public int SubstitutedCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < Segments.Count - 1; i++)
            {
                var a = Segments[i];
                var b = Segments[i + 1];
                if (a.Kind != AlignmentKind.Equal && b.Kind != AlignmentKind.Equal && a.Kind != b.Kind)
                    count += Math.Min(a.Tokens.Count, b.Tokens.Count);
            }
            return count;
        }
    }
}

/// <summary>
/// Word-level alignment of two matn texts using the longest common subsequence (LCS).
/// Used to locate additions (زيادات) and contradictions between turuq of the same hadith.
/// </summary>
public static class MatnAligner
{
    /// <summary>Upper bound on tokens per text, keeping the O(n·m) table small.</summary>
    public const int MaxTokens = 800;

    /// <summary>Aligns two raw texts after normalizing and tokenizing them.</summary>
    public static AlignmentResult Align(string? reference, string? compared) =>
        Align(MatnText.Tokenize(reference), MatnText.Tokenize(compared));

    /// <summary>Aligns two token sequences.</summary>
    public static AlignmentResult Align(IReadOnlyList<string> reference, IReadOnlyList<string> compared)
    {
        var a = reference.Take(MaxTokens).ToArray();
        var b = compared.Take(MaxTokens).ToArray();
        int n = a.Length, m = b.Length;

        if (n == 0 && m == 0)
            return new AlignmentResult([], 1.0);

        // lcs[i, j] = LCS length of a[i..] and b[j..]
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        for (var j = m - 1; j >= 0; j--)
            lcs[i, j] = a[i] == b[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var segments = new List<AlignmentSegment>();
        var currentKind = AlignmentKind.Equal;
        var current = new List<string>();

        void Emit(AlignmentKind kind, string token)
        {
            if (current.Count > 0 && kind != currentKind)
            {
                segments.Add(new AlignmentSegment(currentKind, current));
                current = [];
            }
            currentKind = kind;
            current.Add(token);
        }

        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { Emit(AlignmentKind.Equal, a[x]); x++; y++; }
            else if (lcs[x + 1, y] >= lcs[x, y + 1]) { Emit(AlignmentKind.Removed, a[x]); x++; }
            else { Emit(AlignmentKind.Added, b[y]); y++; }
        }
        while (x < n) Emit(AlignmentKind.Removed, a[x++]);
        while (y < m) Emit(AlignmentKind.Added, b[y++]);
        if (current.Count > 0) segments.Add(new AlignmentSegment(currentKind, current));

        var similarity = 2.0 * lcs[0, 0] / (n + m);
        return new AlignmentResult(segments, similarity);
    }
}
