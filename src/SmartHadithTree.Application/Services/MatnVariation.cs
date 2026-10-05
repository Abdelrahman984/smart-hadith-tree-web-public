using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services;

/// <summary>
/// Finds which turuq differ in wording from the rest. The comparison is made against the
/// text most similar to all the others (the medoid), so the result does not depend on the
/// order the hadiths happen to be listed in.
/// </summary>
public static class MatnVariation
{
    /// <summary>
    /// Index of the text with the highest total similarity to the others. Ties go to the earliest
    /// text, so the caller's order decides only when the texts are equally central.
    /// </summary>
    public static int FindMedoid(IReadOnlyList<string[]> bodies)
    {
        if (bodies.Count <= 2) return 0;

        var totals = new double[bodies.Count];
        for (var i = 0; i < bodies.Count; i++)
        {
            for (var j = i + 1; j < bodies.Count; j++)
            {
                if (bodies[i].Length == 0 || bodies[j].Length == 0) continue;
                var similarity = MatnAligner.Align(bodies[i], bodies[j]).Similarity;
                totals[i] += similarity;
                totals[j] += similarity;
            }
        }

        var best = 0;
        for (var i = 1; i < totals.Length; i++)
            if (totals[i] > totals[best] + 1e-9) best = i;
        return best;
    }

    /// <summary>
    /// Indexes of the texts whose wording differs substantively from the medoid (a real addition,
    /// omission or contradiction; framing words and narration by meaning are ignored).
    /// </summary>
    public static IReadOnlySet<int> FindVariants(IReadOnlyList<string[]> bodies)
    {
        var variants = new HashSet<int>();
        if (bodies.Count < 2) return variants;

        var medoid = FindMedoid(bodies);
        if (bodies[medoid].Length == 0) return variants;

        for (var i = 0; i < bodies.Count; i++)
        {
            if (i == medoid || bodies[i].Length == 0) continue;
            var kind = MatnAtMadarRule.Classify(MatnAligner.Align(bodies[medoid], bodies[i]));
            if (kind != MatnAtMadarRule.DiffKind.None) variants.Add(i);
        }
        return variants;
    }
}
