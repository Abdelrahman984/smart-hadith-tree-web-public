using FluentAssertions;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>
/// A narration through another Companion is a witness (شاهد), not another route; a narrator with no grade is a gap
/// in the data, not a weakness.
/// </summary>
public class ShawahidAndGapTests
{
    private const string Text = "قال رسول الله ﷺ الطهور شطر الإيمان والحمد لله تملأ الميزان";
    private const string Other = "قال رسول الله ﷺ الصلاة نور والصدقة برهان والصبر ضياء والقرآن حجة لك أو عليك";

    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("ك1").Narrator("ك2").Narrator("ك3").Narrator("ك4")
        .Narrator("أبان").Narrator("يحيى").Narrator("معمر").Narrator("حجر")
        .Narrator("أبو مالك", "companion").Narrator("علي", "companion");

    [Fact]
    public void ChainThroughAnotherCompanion_IsAWitness_AndStaysOutOfTheComparison()
    {
        var b = Narrators();
        b.Chain(Text, "مسلم", "ك1", "حدثنا", "أبان", "عن", "يحيى", "عن", "أبو مالك");
        b.Chain(Text, "أحمد", "ك2", "حدثنا", "معمر", "عن", "يحيى", "عن", "أبو مالك");
        var witness = b.Chain(Other, "ابن أبي شيبة", "ك3", "حدثنا", "حجر", "عن", "علي");

        var report = IlalAnalysisService.Analyze(b.Build());

        report.Turuq.Single(t => t.HadithId == witness).IsShahid.Should().BeTrue();
        report.Turuq.Single(t => t.HadithId == witness).CompanionId.Should().Be(b.Id("علي"));
        report.Turuq.Count(t => t.IsShahid).Should().Be(1);
        report.Findings.SelectMany(f => f.HadithIds).Should().NotContain(witness);
        report.Madars.SelectMany(m => m.HadithIds).Should().NotContain(witness);
        report.SummaryAr.Should().Contain("شاهد");
    }

    [Fact]
    public void ChainThatStopsBeforeAnyCompanion_IsStillARoute()
    {
        var b = Narrators();
        b.Chain(Text, "مسلم", "ك1", "حدثنا", "أبان", "عن", "يحيى", "عن", "أبو مالك");
        b.Chain(Text, "أحمد", "ك2", "حدثنا", "معمر", "عن", "يحيى", "عن", "أبو مالك");
        var cut = b.Chain(Text, "الدارمي", "ك3", "حدثنا", "حجر", "عن", "يحيى");

        var tariq = IlalAnalysisService.Analyze(b.Build()).Turuq.Single(t => t.HadithId == cut);

        tariq.IsShahid.Should().BeFalse();
        tariq.CompanionId.Should().BeNull();
    }

    [Fact]
    public void MainCompanion_IsTheOneMostChainsReach_AndTheFirstOnATie()
    {
        var b = Narrators();
        b.Chain(Text, "أ", "ك1", "حدثنا", "حجر", "عن", "علي");
        b.Chain(Text, "ب", "ك2", "حدثنا", "أبان", "عن", "أبو مالك");

        Shawahid.MainCompanion(b.Build()).Should().Be(b.Id("علي"));
    }

    [Fact]
    public void LinkInvolvingAnUngradedNarrator_IsADataGap_NotAFinding()
    {
        var b = new IlalTestBuilder().Narrator("أ").Narrator("ب").Narrator("ج", "").Narrator("د");
        b.Chain("x", "كتاب", "أ", "حدثنا", "ب", "عن", "ج");
        b.Relation("ب", "أ").Relation("ج", "د").Relation("د", "ب");

        var finding = new HiddenInqitaRule().Evaluate(b.Build()).Single();

        finding.TitleAr.Should().Contain("بيانات ناقصة");
        finding.Confidence.Should().BeLessThan(0.3); // folded away in the panel
    }

    [Fact]
    public void LinkBetweenGradedNarrators_StaysAFinding()
    {
        var b = new IlalTestBuilder().Narrator("أ").Narrator("ب").Narrator("ج").Narrator("د");
        b.Chain("x", "كتاب", "أ", "حدثنا", "ب", "عن", "ج");
        b.Relation("ب", "أ").Relation("ج", "د").Relation("د", "ب");

        var finding = new HiddenInqitaRule().Evaluate(b.Build()).Single();

        finding.TitleAr.Should().Be("لم يثبت اللقاء");
        finding.Confidence.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public void Analyze_LeavesUngradedNarratorsOutOfTheWeakestLink_AndCountsThem()
    {
        var b = new IlalTestBuilder().Narrator("ك").Narrator("مالك", "").Narrator("الزهري").Narrator("أنس", "companion");
        b.Chain(Text, "أ", "ك", "حدثنا", "مالك", "عن", "الزهري", "عن", "أنس");

        var tariq = IlalAnalysisService.Analyze(b.Build()).Turuq.Single();

        tariq.UnratedNarratorCount.Should().Be(1);
        tariq.WeakestNarratorId.Should().Be(b.Id("الزهري"));
        tariq.WeakestTier.Should().Be(3);
    }

    private static ComparativeTreeResponseDto Tree(params (int? Tier, int Unrated)[] routes) => new()
    {
        IlalReport = new IlalReportDto
        {
            Turuq = routes.Select(r => new IlalTariqDto
            {
                HadithId = Guid.NewGuid(),
                WeakestTier = r.Tier,
                WeakestNarratorId = Guid.NewGuid(),
                UnratedNarratorCount = r.Unrated
            }).ToList()
        }
    };

    [Fact]
    public void RoutesAllThroughUngradedNarrators_AreNotGradedWeak()
    {
        var tree = Tree((null, 2), (null, 1));

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("غير محرر");
    }

    [Fact]
    public void ARouteWithAnUngradedNarrator_IsNotSahih_OnTheGradedPartAlone()
    {
        var tree = Tree((3, 1), (3, 1));

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("غير محرر");
    }

    [Fact]
    public void ASoundRoute_StillGivesTheGrade_WhenAnotherRouteIsUndetermined()
    {
        var tree = Tree((3, 0), (null, 2));

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("صحيح");
        tree.TaqwiyahDetails.Should().Contain("لم تُحرَّر");
    }

    [Fact]
    public void WeakRoutesPlusAnUndeterminedOne_AreNotCalledWeak()
    {
        var tree = Tree((8, 0), (null, 1));

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("غير محرر");
    }

    [Fact]
    public void ARouteWeakOnItsGradedNarrators_IsWeakWhateverTheUngradedOnesAre()
    {
        var tree = Tree((8, 2));

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("ضعيف");
    }

    [Fact]
    public void RafWaqf_AgainstASideWithNoGradedStudent_IsATanbihNotQadihah()
    {
        var b = new IlalTestBuilder()
            .Narrator("مالك").Narrator("معمر").Narrator("مجهول", "")
            .Narrator("نافع").Narrator("ابن عمر", "companion").Narrator("ك1").Narrator("ك2").Narrator("ك3");
        b.Chain("عن ابن عمر قال من السنة أن يغتسل يوم الجمعة", "موطأ مالك", "ك1", "حدثنا", "مالك", "عن", "نافع", "عن", "ابن عمر");
        b.Chain("عن ابن عمر قال من السنة أن يغتسل يوم الجمعة", "مصنف", "ك2", "حدثنا", "معمر", "عن", "نافع", "عن", "ابن عمر");
        b.Chain("عن ابن عمر قال قال رسول الله صلى الله عليه وسلم من السنة أن يغتسل يوم الجمعة", "سنن", "ك3", "حدثنا", "مجهول", "عن", "نافع", "عن", "ابن عمر");

        var finding = new RafWaqfRule().Evaluate(b.Build()).Single();

        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.EvidenceAr.Should().Contain("لم يُحرَّر");
    }
}
