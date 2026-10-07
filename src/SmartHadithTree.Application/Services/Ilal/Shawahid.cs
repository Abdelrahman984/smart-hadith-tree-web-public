namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Tells the routes (turuq) of a hadith from its witnesses (shawahid). A takhreej gathers every narration with the
/// same wording, but one that reaches another Companion is a different hadith with the same words: it can support
/// the hadith, yet it is not another route to the same madar, so it is kept out of the madar and narrator comparison.
/// </summary>
public static class Shawahid
{
    /// <summary>
    /// The Companion most chains end at (the first chain's on a tie). Only a chain that reaches a graded Companion
    /// counts: one that stops short of the Companion says nothing about which hadith it is.
    /// </summary>
    public static Guid? MainCompanion(IlalContext context) =>
        context.Chains
            .Select((chain, index) => (Top: CompanionOf(context, chain), Index: index))
            .Where(x => x.Top.HasValue)
            .GroupBy(x => x.Top!.Value)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Min(x => x.Index))
            .Select(g => (Guid?)g.Key)
            .FirstOrDefault();

    /// <summary>The Companion the chain ends at, or null when it ends at someone else (a chain cut short).</summary>
    public static Guid? CompanionOf(IlalContext context, IlalChain chain) =>
        chain.TopNarratorId is { } top && context.Narrator(top)?.IsCompanion == true ? top : null;

    /// <summary>True when the chain reaches a Companion other than <paramref name="mainCompanion"/>.</summary>
    public static bool IsShahid(IlalContext context, IlalChain chain, Guid? mainCompanion) =>
        mainCompanion.HasValue && CompanionOf(context, chain) is { } companion && companion != mainCompanion.Value;
}
