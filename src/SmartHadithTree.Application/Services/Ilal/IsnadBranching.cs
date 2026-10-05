namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>A group of turuq that reach the madar through the same student.</summary>
public sealed class IsnadBranch
{
    public required Guid StudentId { get; init; }
    public required IReadOnlyList<IlalChain> Chains { get; init; }

    /// <summary>Tier of the student who carries this branch from the madar (lower = stronger).</summary>
    public required int StudentTier { get; init; }

}

/// <summary>A common link (المدار) at which two or more branches diverge.</summary>
public sealed class SplitPoint
{
    public required Guid MadarId { get; init; }
    public required IReadOnlyList<IsnadBranch> Branches { get; init; }

    public IEnumerable<IlalChain> AllChains => Branches.SelectMany(b => b.Chains);
}

/// <summary>Finds the points where turuq diverge and weighs branches against each other (الترجيح).</summary>
public static class IsnadBranching
{
    /// <summary>
    /// Returns every narrator from whom at least two different students narrate across the given chains.
    /// </summary>
    public static List<SplitPoint> FindSplitPoints(IlalContext context)
    {
        var result = new List<SplitPoint>();
        var narratorIds = context.Chains.SelectMany(c => c.Path).Distinct();

        foreach (var madarId in narratorIds)
        {
            var branches = context.Chains
                .Select(c => (Chain: c, Student: c.StudentOf(madarId)))
                .Where(x => x.Student.HasValue)
                .GroupBy(x => x.Student!.Value)
                .Select(g => new IsnadBranch
                {
                    StudentId = g.Key,
                    Chains = g.Select(x => x.Chain).ToList(),
                    StudentTier = context.TierOf(g.Key)
                })
                .ToList();

            if (branches.Count >= 2)
                result.Add(new SplitPoint { MadarId = madarId, Branches = branches });
        }

        return result;
    }

    /// <summary>
    /// Compares the strength of two groups of narrations.
    /// Returns a positive number when <paramref name="aTier"/>/<paramref name="aCount"/> is stronger,
    /// negative when weaker, and 0 when they are comparable (no clear preference).
    /// A clear difference in reliability (two tiers or more) outweighs numbers; otherwise
    /// the larger number of narrators prevails, and then the more reliable side.
    /// </summary>
    public static int Compare(int aTier, int aCount, int bTier, int bCount)
    {
        var tierDiff = bTier - aTier; // positive = a is more reliable
        if (Math.Abs(tierDiff) >= 2) return Math.Sign(tierDiff);
        if (aCount != bCount) return Math.Sign(aCount - bCount);
        return Math.Sign(tierDiff);
    }

    /// <summary>
    /// Compares two sides of a disagreement, each made of one or more branches.
    /// The number that matters is how many distinct students of the madar are on each side
    /// (not how many books repeat the same student's narration).
    /// </summary>
    public static int Compare(IReadOnlyCollection<IsnadBranch> a, IReadOnlyCollection<IsnadBranch> b) =>
        Compare(a.Min(x => x.StudentTier), a.Count, b.Min(x => x.StudentTier), b.Count);
}
