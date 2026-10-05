using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>Grading by Ibn Hajar's rank and the Shamela-shaped data (no Itqan grade) through the rules and the service.</summary>
public class ShamelaGradingTests
{
    private const string Base = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من صام رمضان إيمانا واحتسابا غفر له ما تقدم من ذنبه";
    private const string Contradicting = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من قام ليلة القدر إيمانا واحتسابا غفر له ما تقدم من ذنبه";

    [Fact]
    public void TheRank_DecidesWhoIsWeak_NotTheLegacyGrade()
    {
        // The legacy grade says the opposite on purpose: the rank must win.
        var b = new IlalTestBuilder()
            .Narrator("مالك", "weak", rank: 3).Narrator("معمر", "weak", rank: 3).Narrator("منكر", "reliable", rank: 8)
            .Narrator("الزهري").Narrator("أبو سلمة").Narrator("أبو هريرة", rank: 1)
            .Narrator("البخاري").Narrator("مسلم").Narrator("ابن ماجه");
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base, "صحيح مسلم", "مسلم", "حدثنا", "معمر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        var weak = b.Chain(Contradicting, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "منكر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Nakarah);
        finding.HadithIds.Should().Equal(weak);
    }

    [Fact]
    public void IlalNarrator_ReadsTierLabelAndGenerationFromTheRank()
    {
        var b = new IlalTestBuilder().Narrator("الصحابي", grade: "", rank: 1).Narrator("التابعي", grade: "", tier: "الثانية", rank: 3)
            .Narrator("المتأخر", grade: "", tier: "العاشرة", rank: 4).Narrator("بلا رتبة", grade: "");
        var ctx = b.Build();

        ctx.Narrator(b.Id("الصحابي"))!.Should().Match<IlalNarrator>(n => n.Tier == 1 && n.IsCompanion && n.IsTabii == false && n.GradeLabel == "صحابي");
        ctx.Narrator(b.Id("التابعي"))!.Should().Match<IlalNarrator>(n => n.Tier == 3 && !n.IsCompanion && n.IsTabii == true && n.GradeLabel == "ثقة");
        ctx.Narrator(b.Id("المتأخر"))!.Should().Match<IlalNarrator>(n => n.Tier == 4 && n.IsTabii == false);
        ctx.Narrator(b.Id("بلا رتبة"))!.Tier.Should().Be(NarratorGradeScale.DefaultTier);
    }

    [Fact]
    public void AMudallisWithoutATier_TheEditorsAppendix_IsNotFlagged()
    {
        var b = new IlalTestBuilder().Narrator("أبو داود").Narrator("شعبة").Narrator("ملحق", mudallisTier: null).Narrator("أنس", "companion");
        b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "شعبة", "عن", "ملحق", "عن", "أنس");

        new TadlisRule().Evaluate(b.Build()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(3, 0.6)]
    [InlineData(4, 0.75)]
    [InlineData(5, 0.75)]
    public void TheDeeperTheTier_TheMoreConfidentTheFinding(int tier, double confidence)
    {
        var b = new IlalTestBuilder().Narrator("أبو داود").Narrator("شعبة").Narrator("مدلس", mudallisTier: tier).Narrator("أنس", "companion");
        b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "شعبة", "عن", "مدلس", "عن", "أنس");

        new TadlisRule().Evaluate(b.Build()).Single().Confidence.Should().Be(confidence);
    }

    private static HadithTreeDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HadithTreeDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    // Narrators as the Shamela loaders write them: a registry id and Ibn Hajar's rank, no Itqan grade.
    private static Narrator Shamela(string name, int rank, Action<Narrator>? more = null)
    {
        var n = new Narrator { Id = Guid.NewGuid(), FullName = name, SourceKey = "tk" + name.GetHashCode(), IbnHajarRank = rank };
        more?.Invoke(n);
        return n;
    }

    private static Transmission Link(HadithText h, int step, Narrator student, Narrator sheikh, string? term) =>
        new() { Id = Guid.NewGuid(), HadithId = h.Id, StepOrder = step, StudentId = student.Id, SheikhId = sheikh.Id, TransmissionTerm = term };

    private static HadithText Hadith(string book = "مسند أحمد") => new()
    {
        Id = Guid.NewGuid(), BookName = book, HadithNumber = 1, MatnArabic = "حدثنا جرير عن ابن عيينة عن أبيه",
        NormalizedMatn = "", NormalizedBookName = ""
    };

    [Fact]
    public async Task AnalyzeAsync_UsesSeverityHearingEvidenceAndRankFromTheDatabase()
    {
        await using var db = NewDb();
        var compiler = Shamela("أحمد", 3);
        var jarir = Shamela("جرير", 3);
        var mukhtalit = Shamela("المختلط", 4, n => { n.HasMukhtalit = true; n.IkhtilatSeverity = IkhtilatSeverity.Harmful; n.IkhtilatNote = "اختلط بآخره"; });
        var father = Shamela("أبوه", 1);
        var hadith = Hadith();
        db.Narrators.AddRange(compiler, jarir, mukhtalit, father);
        db.Hadiths.Add(hadith);
        db.Transmissions.AddRange(Link(hadith, 1, compiler, jarir, "حدثنا"), Link(hadith, 2, jarir, mukhtalit, "عن"), Link(hadith, 3, mukhtalit, father, "عن"));
        db.MukhtalitHearings.Add(new MukhtalitHearing
        {
            Id = Guid.NewGuid(), MukhtalitId = mukhtalit.Id, StudentId = jarir.Id, Timing = HearingTiming.After,
            Evidence = "سمع منه بعد ما اختلط", SourceBook = "الكواكب النيرات"
        });
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        var finding = report.Findings.Should().ContainSingle(f => f.Type == IllahType.Ikhtilat).Subject;
        finding.Severity.Should().Be(IllahSeverity.Qadihah);
        finding.EvidenceAr.Should().Contain("سمع منه بعد ما اختلط").And.Contain("الكواكب النيرات").And.Contain("اختلاطًا فاحشًا");
    }

    [Fact]
    public async Task AnalyzeAsync_LoadsGroupRulesIntoTheEvidenceOfAWeakerMukhtalit()
    {
        await using var db = NewDb();
        var compiler = Shamela("أحمد", 3);
        var jarir = Shamela("جرير", 3);
        var mukhtalit = Shamela("المختلط", 5, n => { n.HasMukhtalit = true; n.IkhtilatSeverity = IkhtilatSeverity.Disputed; });
        var father = Shamela("أبوه", 1);
        var hadith = Hadith();
        db.Narrators.AddRange(compiler, jarir, mukhtalit, father);
        db.Hadiths.Add(hadith);
        db.Transmissions.AddRange(Link(hadith, 1, compiler, jarir, "حدثنا"), Link(hadith, 2, jarir, mukhtalit, "عن"), Link(hadith, 3, mukhtalit, father, "عن"));
        db.MukhtalitGroupRules.Add(new MukhtalitGroupRule
        {
            Id = Guid.NewGuid(), MukhtalitId = mukhtalit.Id, GroupAr = "من سمع منه قديما", Timing = HearingTiming.Before,
            Quote = "فمن سمع منه قديما فصحيح"
        });
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        var finding = report.Findings.Should().ContainSingle(f => f.Type == IllahType.Ikhtilat).Subject;
        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.EvidenceAr.Should().Contain("فمن سمع منه قديما فصحيح").And.Contain("واختُلف في اختلاطه");
    }

    [Fact]
    public async Task AnalyzeAsync_ADisputedIkhtilatOfAThiqah_WithUnknownTiming_IsNotFlagged()
    {
        await using var db = NewDb();
        var compiler = Shamela("أحمد", 3);
        var jarir = Shamela("جرير", 3);
        var mukhtalit = Shamela("ابن عيينة", 2, n => { n.HasMukhtalit = true; n.IkhtilatSeverity = IkhtilatSeverity.Disputed; });
        var father = Shamela("أبوه", 1);
        var hadith = Hadith();
        db.Narrators.AddRange(compiler, jarir, mukhtalit, father);
        db.Hadiths.Add(hadith);
        db.Transmissions.AddRange(Link(hadith, 1, compiler, jarir, "حدثنا"), Link(hadith, 2, jarir, mukhtalit, "عن"), Link(hadith, 3, mukhtalit, father, "عن"));
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.Findings.Should().NotContain(f => f.Type == IllahType.Ikhtilat);
    }

    [Fact]
    public async Task AnalyzeAsync_ADeadEndInTheChain_IsHandledAndOnlyTheConnectedPartIsAnalyzed()
    {
        // The resolver leaves a name undecided: the chain loader makes no link across it (A ← ? ← C is not A ← C).
        await using var db = NewDb();
        var compiler = Shamela("أحمد", 3);
        var a = Shamela("أ", 3);
        var mudallis = Shamela("مدلس", 4, n => { n.IsMudallis = true; n.MudallisTier = 4; });
        var top = Shamela("أنس", 1);
        var hadith = Hadith();
        db.Narrators.AddRange(compiler, a, mudallis, top);
        db.Hadiths.Add(hadith);
        // compiler ← a, then a gap, then mudallis ← top (its own step 2, student is not the previous sheikh).
        db.Transmissions.AddRange(Link(hadith, 1, compiler, a, "حدثنا"), Link(hadith, 2, mudallis, top, "عن"));
        await db.SaveChangesAsync();

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.AnalyzedHadithIds.Should().Equal(hadith.Id);
        report.Findings.Should().NotContain(f => f.Type == IllahType.Tadlis);   // the mudallis's link is cut off, not guessed
    }
}
