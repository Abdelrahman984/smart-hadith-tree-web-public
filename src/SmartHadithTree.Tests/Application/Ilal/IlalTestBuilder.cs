using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>Fluent builder for hand-made <see cref="IlalContext"/> fixtures.</summary>
public sealed class IlalTestBuilder
{
    private readonly Dictionary<string, IlalNarrator> _narrators = [];
    private readonly List<IlalChain> _chains = [];
    private readonly HashSet<(Guid, Guid)> _relations = [];
    private readonly HashSet<Guid> _withRelations = [];
    private readonly Dictionary<(Guid, Guid), HearingTiming> _hearings = [];
    private readonly Dictionary<(Guid, Guid), string> _evidence = [];
    private readonly Dictionary<Guid, List<MukhtalitGroupRuleInfo>> _groupRules = [];

    public IlalTestBuilder Narrator(string name, string grade = "reliable", string? tier = null,
        int? mudallisTier = null, bool mukhtalit = false, IkhtilatSeverity? severity = null,
        bool noHearingAfter = false, int? rank = null)
    {
        _narrators[name] = new IlalNarrator(Guid.NewGuid(), name, grade, tier, mudallisTier, mukhtalit, null,
            rank, severity, noHearingAfter);
        return this;
    }

    public Guid Id(string name) => _narrators[name].Id;

    /// <summary>
    /// Adds a chain. <paramref name="path"/> lists narrators from the compiler upward,
    /// with the transmission term before each sheikh, e.g. ("البخاري", "حدثنا", "مالك", "عن", "نافع").
    /// </summary>
    public Guid Chain(string matn, string book, params string[] path)
    {
        var links = new List<IlalLink>();
        for (int i = 0, step = 1; i + 2 < path.Length; i += 2, step++)
            links.Add(new IlalLink(Id(path[i]), Id(path[i + 2]), path[i + 1], step));

        var hadithId = Guid.NewGuid();
        _chains.Add(new IlalChain
        {
            HadithId = hadithId,
            BookName = book,
            HadithNumber = _chains.Count + 1,
            MatnArabic = matn,
            Links = links
        });
        return hadithId;
    }

    public IlalTestBuilder Relation(string teacher, string student)
    {
        _relations.Add((Id(teacher), Id(student)));
        _withRelations.Add(Id(teacher));
        _withRelations.Add(Id(student));
        return this;
    }

    public IlalTestBuilder Hearing(string mukhtalit, string student, HearingTiming timing)
    {
        _hearings[(Id(mukhtalit), Id(student))] = timing;
        return this;
    }

    public IlalTestBuilder Hearing(string mukhtalit, string student, HearingTiming timing, string evidence)
    {
        Hearing(mukhtalit, student, timing);
        _evidence[(Id(mukhtalit), Id(student))] = evidence;
        return this;
    }

    public IlalTestBuilder GroupRule(string mukhtalit, string group, HearingTiming timing, string quote)
    {
        if (!_groupRules.TryGetValue(Id(mukhtalit), out var list)) _groupRules[Id(mukhtalit)] = list = [];
        list.Add(new MukhtalitGroupRuleInfo(group, timing, quote));
        return this;
    }

    public IlalContext Build() => new()
    {
        Chains = _chains,
        Narrators = _narrators.Values.ToDictionary(n => n.Id),
        Relations = _relations,
        NarratorsWithRelations = _withRelations,
        Hearings = _hearings,
        HearingEvidence = _evidence,
        GroupRules = _groupRules.ToDictionary(p => p.Key, p => (IReadOnlyList<MukhtalitGroupRuleInfo>)p.Value)
    };
}
