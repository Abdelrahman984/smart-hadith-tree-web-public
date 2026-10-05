using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SmartHadithTree.Etl.Parsers.Shamela;
using Xunit;
using Xunit.Abstractions;

namespace SmartHadithTree.Tests.Etl;

public class ShamelaLoaderTests(ITestOutputHelper output)
{
    private static RegistryEntry Entry(string id, string name, string header = "", int? rank = null, string? verdict = null,
        List<string>? shuyukh = null, List<string>? talamidh = null, List<RegistryQuote>? quotes = null) =>
        new(id, name, header, "tahdhib", verdict, rank, "السابعة", shuyukh, talamidh, quotes);

    [Theory]
    [InlineData("سفيان بن سعيد بن مسروق الثوري ، أبو عبد الله الكوفي، من ثور", "سفيان بن سعيد بن مسروق الثوري", "أبو عبد الله")]
    [InlineData("محمد بن إسماعيل، أبو عبد الله البخاري، الحافظ.", "محمد بن إسماعيل", "أبو عبد الله")]
    [InlineData("عبد الله بن عثمان بن جبلة، أبو عبد الرحمن المروزي.", "عبد الله بن عثمان بن جبلة", "أبو عبد الرحمن")]
    [InlineData("أبو هريرة الدوسي اليماني، صاحب رسول الله.", "أبو هريرة الدوسي اليماني", null)]   // the name is the kunya
    [InlineData("حسن بن علي، ثقة.", "حسن بن علي", null)]
    public void ExtractKunya_ReadsTheKunyaAfterTheName(string header, string name, string? expected) =>
        ShamelaNarratorMapper.ExtractKunya(header, name).Should().Be(expected);

    [Fact]
    public void MapNarrator_UsesRegistryFieldsAndS1WhenMapped()
    {
        var entry = Entry("tk2407", "سفيان بن سعيد بن مسروق الثوري", "سفيان بن سعيد ، أبو عبد الله الكوفي.", rank: 2, verdict: "ثقة حافظ");
        var s1 = new S1Narrator(77, 161, new() { ["الكنية"] = "أبو عبد الله", ["بلد الإقامة"] = "الكوفة" }, null);

        var n = ShamelaNarratorMapper.MapNarrator(entry, s1);

        n.SourceKey.Should().Be("tk2407");
        n.ShamelaManId.Should().Be(77);
        n.DeathYearHijri.Should().Be(161);
        n.IbnHajarRank.Should().Be(2);
        n.Verdict.Should().Be("ثقة حافظ");
        n.GenerationTier.Should().Be("السابعة");
        n.Kunyah.Should().Be("أبو عبد الله");
        n.ResidencePlaces.Should().Be("الكوفة");
        n.ItqanId.Should().BeNull();
    }

    [Fact]
    public void MapNarrator_TruncatesToColumnLimits()
    {
        var n = ShamelaNarratorMapper.MapNarrator(Entry("tk1", new string('ب', 700), verdict: new string('ث', 600)));
        n.FullName.Length.Should().Be(500);
        n.Verdict!.Length.Should().Be(500);
    }

    [Fact]
    public void Quotes_RegistryAndS1_AvoidDuplicatesOfTahdhibAlKamal()
    {
        var narrator = ShamelaNarratorMapper.MapNarrator(Entry("tk1", "أ"));
        var registry = Entry("tk1", "أ", quotes: [new("ابن معين", "ابن أبي خيثمة", "ثقة."), new("فلان", null, "  ")]);
        var s1 = new S1Narrator(1, null, null, [
            new("garh", "ابن حبان", "ذكره في الثقات", "الثقات", 3, 120),
            new("garh", "ابن معين", "ثقة", ShamelaNarratorMapper.TahdhibAlKamal, 1, 2)]);

        var fromRegistry = ShamelaNarratorMapper.MapRegistryQuotes(narrator, registry).ToList();
        var fromS1 = ShamelaNarratorMapper.MapS1Quotes(narrator, s1).ToList();

        fromRegistry.Should().ContainSingle().Which.EvaluationText.Should().Be("ثقة. (رواية: ابن أبي خيثمة)");
        fromRegistry[0].SourceBook.Should().Be(ShamelaNarratorMapper.TahdhibAlKamal);
        var q = fromS1.Should().ContainSingle().Subject;
        (q.ScholarName, q.SourceBook, q.SourceVolume, q.SourcePage).Should().Be(("ابن حبان", "الثقات", "3", 120));
        fromRegistry.Concat(fromS1).Should().OnlyContain(e => e.NarratorId == narrator.Id);
    }

    [Fact]
    public void MapHadith_KeepsFullTextAndSplitsOutTheIsnad()
    {
        var text = "حدثنا الحميدي قال حدثنا سفيان. سمعت النبي يقول: إنما الأعمال بالنيات. هذا حديث صحيح";
        var isnadEnd = text.IndexOf('.');
        var matnStart = text.IndexOf("سمعت", StringComparison.Ordinal);
        var remarkStart = text.IndexOf("هذا حديث", StringComparison.Ordinal);
        var record = new ShamelaRecord(7, 1, "hadith", "باب", "2", text,
            [new("isnad", 0, isnadEnd), new("matn", matnStart, remarkStart), new("remark", remarkStart, text.Length)]);

        var h = ShamelaHadithMapper.Map(record, "صحيح البخاري", "كتاب بدء الوحي")!;

        h.MatnArabic.Should().Be(text);
        h.FullIsnadText.Should().Be("حدثنا الحميدي قال حدثنا سفيان");
        (h.BookName, h.HadithNumber, h.Volume, h.Chapter).Should().Be(("صحيح البخاري", 1, "2", "كتاب بدء الوحي"));
        h.NormalizedMatn.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void MapHadith_FallsBackToTheRecordIdAndSkipsProse()
    {
        ShamelaHadithMapper.Map(new(9, null, "hadith", null, null, "حدثنا فلان عن فلان", null), "ب", null)!
            .HadithNumber.Should().Be(9);
        ShamelaHadithMapper.Map(new(1, null, "text", null, null, "مقدمة الكتاب", null), "ب", null).Should().BeNull();
        ShamelaHadithMapper.Map(new(2, 2, "hadith", null, null, "  ", null), "ب", null).Should().BeNull();
    }

    private static ChainName Name(string? id, params string[] verbs) => new("x", id, "unique", [.. verbs]);

    [Fact]
    public void ChainBuilder_LinksCompilerToFirstNarratorAndEachToTheNext()
    {
        var (c, a, b, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid> { ["c"] = c, ["a"] = a, ["b"] = b, ["d"] = d };

        var links = ShamelaChainBuilder.Build(Guid.NewGuid(), c, [Name("a", "حدثنا"), Name("b", "عن"), Name("d")], map);

        links.Select(l => (l.StudentId, l.SheikhId, l.StepOrder, l.TransmissionTerm)).Should().Equal(
            (c, a, 1, null), (a, b, 2, "حدثنا"), (b, d, 3, "عن"));
    }

    [Theory]
    [InlineData(new[] { "حدثنا", "عن" }, "حدثنا")]        // an explicit verb wins over a later «عن»
    [InlineData(new[] { "قال", "ثنا" }, "ثنا")]            // the abbreviation counts
    [InlineData(new[] { "عن" }, "عن")]
    [InlineData(new[] { "قال", "عن" }, "عن")]              // none explicit: the last verb
    [InlineData(new string[0], null)]
    public void TermOf_PrefersAnExplicitHearingVerb(string[] verbs, string? expected) =>
        ShamelaChainBuilder.TermOf(verbs).Should().Be(expected);

    [Fact]
    public void ChainBuilder_DoesNotBridgeAnUndecidedName()
    {
        var (c, a, d) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid> { ["c"] = c, ["a"] = a, ["d"] = d };

        var links = ShamelaChainBuilder.Build(Guid.NewGuid(), c, [Name("a"), Name(null), Name("d")], map);

        links.Should().ContainSingle().Which.SheikhId.Should().Be(a);   // «a ← ? ← d» is not «a ← d»
    }

    [Fact]
    public void ChainBuilder_StoresEveryChainOfATahwilIsnadFromStepOne()
    {
        // Mustadrak 493: قيس ← قتيبة and الصيدلاني ← ابن أيوب ← ابن موسى both reach إسماعيل, who is at step 3 on
        // one chain and step 4 on the other.
        var (c, qays, qutayba, sayduni, ayyub, musa, ismail, amr) = (
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid>
        {
            ["c"] = c, ["qays"] = qays, ["qutayba"] = qutayba, ["sayduni"] = sayduni, ["ayyub"] = ayyub,
            ["musa"] = musa, ["ismail"] = ismail, ["amr"] = amr
        };
        var first = new List<ChainName> { Name("qays"), Name("qutayba"), Name("ismail"), Name("amr") };
        var second = new List<ChainName> { Name("sayduni"), Name("ayyub"), Name("musa"), Name("ismail"), Name("amr") };
        var chain = new RecordChain(1, 493, first, [first, second]);

        var links = ShamelaChainBuilder.BuildAll(Guid.NewGuid(), c, chain, map);

        links.Select(l => (l.StudentId, l.SheikhId, l.StepOrder)).Should().BeEquivalentTo(new[]
        {
            (c, qays, 1), (qays, qutayba, 2), (qutayba, ismail, 3), (ismail, amr, 4),
            (c, sayduni, 1), (sayduni, ayyub, 2), (ayyub, musa, 3), (musa, ismail, 4), (ismail, amr, 5)
        });
    }

    [Fact]
    public void ChainBuilder_StoresALinkTwoChainsShareOnce()
    {
        // Two chains that differ only at the end share their first links.
        var (c, a, b, x, y) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid> { ["c"] = c, ["a"] = a, ["b"] = b, ["x"] = x, ["y"] = y };
        var one = new List<ChainName> { Name("a"), Name("b"), Name("x") };
        var two = new List<ChainName> { Name("a"), Name("b"), Name("y") };

        var links = ShamelaChainBuilder.BuildAll(Guid.NewGuid(), c, new RecordChain(1, null, one, [one, two]), map);

        links.Should().HaveCount(4);   // c←a, a←b once, b←x and b←y
        links.Count(l => l.StudentId == a && l.SheikhId == b).Should().Be(1);
    }

    [Fact]
    public void ChainBuilder_WithoutBranchesUsesTheNames()
    {
        var (c, a) = (Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid> { ["c"] = c, ["a"] = a };
        var names = new List<ChainName> { Name("a") };

        ShamelaChainBuilder.BuildAll(Guid.NewGuid(), c, new RecordChain(1, null, names), map).Should().ContainSingle();
        ShamelaChainBuilder.BuildAll(Guid.NewGuid(), c, new RecordChain(1, null, names, [names]), map).Should().ContainSingle();
    }

    [Fact]
    public void ChainBuilder_IgnoresRepeatsAndUnknownIdsAndWorksWithoutCompiler()
    {
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        var map = new Dictionary<string, Guid> { ["a"] = a, ["b"] = b };

        var links = ShamelaChainBuilder.Build(Guid.NewGuid(), null, [Name("a"), Name("a"), Name("zzz"), Name("b")], map);
        links.Should().BeEmpty();

        ShamelaChainBuilder.Build(Guid.NewGuid(), null, [Name("a"), Name("b")], map)
            .Should().ContainSingle().Which.Should().Match<SmartHadithTree.Domain.Entities.Transmission>(
                t => t.StudentId == a && t.SheikhId == b && t.StepOrder == 1);
    }

    [Fact]
    public void Relations_AreUniquePerPairWhicheverSideListsThem()
    {
        var keys = new Dictionary<string, Guid> { ["t"] = Guid.NewGuid(), ["s"] = Guid.NewGuid() };
        var registry = new[]
        {
            Entry("s", "طالب", shuyukh: ["t", "missing"]),     // the student lists his teacher
            Entry("t", "شيخ", talamidh: ["s", "t"])             // and the teacher lists him back; self-link ignored
        };

        var relations = ShamelaDatasetParser.BuildRelations(registry, keys);

        relations.Should().ContainSingle().Which.Should().Match<SmartHadithTree.Domain.Entities.NarratorRelation>(
            r => r.TeacherId == keys["t"] && r.StudentId == keys["s"] && r.Source == "shamela");
    }

    [Fact]
    public async Task ParseAsync_OnTheRealData_GivesTheExpectedCounts()
    {
        // Needs data/shamela_rijal (registry.json, chains/) and data/shamela; passes silently where they are absent.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "shamela_rijal", "chains"))) dir = dir.Parent;
        if (dir is null || !File.Exists(Path.Combine(dir.FullName, "data", "shamela_rijal", "registry.json"))) return;

        var parser = new ShamelaDatasetParser();
        var source = Path.Combine(dir.FullName, "data", "shamela_rijal");
        parser.CanParse(source).Should().BeTrue();

        var data = await parser.ParseAsync(source);
        output.WriteLine($"narrators {data.Narrators.Count}, hadiths {data.Hadiths.Count}, transmissions {data.Transmissions.Count}, " +
                         $"evaluations {data.ScholarEvaluations.Count}, relations {data.NarratorRelations.Count}, " +
                         $"with ShamelaManId {data.Narrators.Count(n => n.ShamelaManId != null)}, with death year {data.Narrators.Count(n => n.DeathYearHijri != null)}, " +
                         $"with kunya {data.Narrators.Count(n => n.Kunyah != null)}, with rank {data.Narrators.Count(n => n.IbnHajarRank != null)}, " +
                         $"hadiths with isnad text {data.Hadiths.Count(h => h.FullIsnadText != null)}");

        data.Narrators.Should().HaveCount(23502);
        data.Narrators.Select(n => n.SourceKey).Should().OnlyHaveUniqueItems();
        data.Hadiths.Select(h => h.BookName).Distinct().Should().BeEquivalentTo(ShamelaBookNames.BySlug.Values);
        data.Hadiths.Count.Should().BeInRange(270_000, 279_198);
        var narratorIds = data.Narrators.Select(n => n.Id).ToHashSet();
        data.Transmissions.Should().OnlyContain(t => narratorIds.Contains(t.StudentId) && narratorIds.Contains(t.SheikhId));
        data.Transmissions.Should().OnlyContain(t => t.StudentId != t.SheikhId);
        data.NarratorRelations.Should().NotBeEmpty();
        data.ScholarEvaluations.Should().OnlyContain(e => narratorIds.Contains(e.NarratorId));
    }
}
