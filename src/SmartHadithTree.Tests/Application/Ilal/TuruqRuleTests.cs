using FluentAssertions;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

public class MatnAtMadarRuleTests
{
    private const string Base = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من صام رمضان إيمانا واحتسابا غفر له ما تقدم من ذنبه";
    private const string WithAddition = Base + " وما تأخر من ذنبه كله";
    private const string Contradicting = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من قام ليلة القدر إيمانا واحتسابا غفر له ما تقدم من ذنبه";

    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("مالك").Narrator("سفيان").Narrator("معمر").Narrator("ضعيف1", "weak")
        .Narrator("الزهري").Narrator("أبو سلمة").Narrator("أبو هريرة", "companion")
        .Narrator("البخاري").Narrator("مسلم").Narrator("النسائي").Narrator("ابن ماجه");

    [Fact]
    public void AdditionByReliableNarrator_IsAcceptedZiyadah()
    {
        var b = Narrators();
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        var id = b.Chain(WithAddition, "سنن النسائي", "النسائي", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Ziyadah);
        finding.Severity.Should().Be(IllahSeverity.GhayrQadihah);
        finding.HadithIds.Should().Equal(id);
        finding.MatnComparison!.Segments.Should().Contain(s => s.Kind == "added");
    }

    [Fact]
    public void WeakNarratorContradictingReliablePeers_IsNakarah()
    {
        var b = Narrators();
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base, "صحيح مسلم", "مسلم", "حدثنا", "معمر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        var weak = b.Chain(Contradicting, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "ضعيف1", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Nakarah);
        finding.Severity.Should().Be(IllahSeverity.Qadihah);
        finding.HadithIds.Should().Equal(weak);
        finding.NarratorIds.Should().Contain(b.Id("الزهري"));
    }

    [Fact]
    public void ReliableNarratorContradictingMoreNumerousPeers_IsShudhudh()
    {
        var b = Narrators();
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base, "صحيح مسلم", "مسلم", "حدثنا", "معمر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Contradicting, "سنن النسائي", "النسائي", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        new MatnAtMadarRule().Evaluate(b.Build()).Single().Type.Should().Be(IllahType.Shudhudh);
    }

    [Fact]
    public void ComparableNarratorsContradicting_IsIdtirab()
    {
        var b = Narrators();
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Contradicting, "سنن النسائي", "النسائي", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Idtirab);
        finding.HadithIds.Should().HaveCount(2);
    }

    [Fact]
    public void MinorWordingDifferences_AreIgnored()
    {
        var b = Narrators();
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base.Replace("من صام", "مَن صام"), "سنن النسائي", "النسائي", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        new MatnAtMadarRule().Evaluate(b.Build()).Should().BeEmpty();
    }
}

public class RafWaqfRuleTests
{
    [Fact]
    public void WeakNarratorRaisingWhatReliablePeersStopAtCompanion_IsQadihah()
    {
        var b = new IlalTestBuilder()
            .Narrator("مالك").Narrator("معمر").Narrator("ضعيف1", "weak")
            .Narrator("نافع").Narrator("ابن عمر", "companion").Narrator("ك1").Narrator("ك2").Narrator("ك3");
        b.Chain("عن ابن عمر قال من السنة أن يغتسل يوم الجمعة", "موطأ مالك", "ك1", "حدثنا", "مالك", "عن", "نافع", "عن", "ابن عمر");
        b.Chain("عن ابن عمر قال من السنة أن يغتسل يوم الجمعة", "مصنف", "ك2", "حدثنا", "معمر", "عن", "نافع", "عن", "ابن عمر");
        var raised = b.Chain("عن ابن عمر قال قال رسول الله صلى الله عليه وسلم من السنة أن يغتسل يوم الجمعة", "سنن", "ك3", "حدثنا", "ضعيف1", "عن", "نافع", "عن", "ابن عمر");

        var finding = new RafWaqfRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.RafWaqf);
        finding.Severity.Should().Be(IllahSeverity.Qadihah);
        finding.HadithIds.Should().Equal(raised);
    }
}

public class WaslIrsalRuleTests
{
    [Fact]
    public void MawsulByWeakAgainstMursalByReliable_IsQadihah()
    {
        var b = new IlalTestBuilder()
            .Narrator("مالك").Narrator("ضعيف1", "weak").Narrator("ك1").Narrator("ك2")
            .Narrator("سعيد بن المسيب", tier: "من كبار الثانية").Narrator("أبو هريرة", "companion");
        b.Chain("عن سعيد بن المسيب أن رسول الله صلى الله عليه وسلم نهى عن بيع اللحم بالحيوان", "موطأ مالك",
            "ك1", "حدثنا", "مالك", "عن", "سعيد بن المسيب");
        var mawsul = b.Chain("عن أبي هريرة أن رسول الله صلى الله عليه وسلم نهى عن بيع اللحم بالحيوان", "سنن",
            "ك2", "حدثنا", "ضعيف1", "عن", "سعيد بن المسيب", "عن", "أبو هريرة");

        var finding = new WaslIrsalRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.WaslIrsal);
        finding.Severity.Should().Be(IllahSeverity.Qadihah);
        finding.HadithIds.Should().Equal(mawsul);
    }

    [Fact]
    public void MursalWithoutMawsulTariq_IsTanbih()
    {
        var b = new IlalTestBuilder()
            .Narrator("مالك").Narrator("ك1").Narrator("سعيد بن المسيب", tier: "تابعي");
        b.Chain("عن سعيد بن المسيب أن رسول الله صلى الله عليه وسلم نهى عن بيع اللحم بالحيوان", "موطأ مالك",
            "ك1", "حدثنا", "مالك", "عن", "سعيد بن المسيب");

        new WaslIrsalRule().Evaluate(b.Build()).Single().Severity.Should().Be(IllahSeverity.Tanbih);
    }
}

public class IlalTaqwiyahIntegrationTests
{
    [Fact]
    public void QadihahInEveryTariq_DowngradesSahihToMalul()
    {
        var h1 = Guid.NewGuid();
        var tree = new ComparativeTreeResponseDto
        {
            Sources = [new ComparativeHadithSourceDto { HadithId = h1 }],
            IlalReport = new IlalReportDto
            {
                Turuq = [new IlalTariqDto { HadithId = h1, WeakestTier = 3, WeakestNarratorId = Guid.NewGuid() }],
                HasQadihah = true,
                Findings = [new IlalFindingDto { Severity = IllahSeverity.Qadihah, TitleAr = "عنعنة مدلس", HadithIds = [h1] }]
            }
        };

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("ضعيف (معلول)");
        tree.TaqwiyahDetails.Should().Contain("عنعنة مدلس");
    }

    [Fact]
    public void QadihahInSomeTuruq_KeepsGradeWithWarning()
    {
        var h1 = Guid.NewGuid();
        var tree = new ComparativeTreeResponseDto
        {
            Sources = [new ComparativeHadithSourceDto { HadithId = h1 }, new ComparativeHadithSourceDto { HadithId = Guid.NewGuid() }],
            IlalReport = new IlalReportDto
            {
                Turuq = [new IlalTariqDto { HadithId = h1, WeakestTier = 3, WeakestNarratorId = Guid.NewGuid() }],
                HasQadihah = true,
                Findings = [new IlalFindingDto { Severity = IllahSeverity.Qadihah, TitleAr = "شذوذ", HadithIds = [h1] }]
            }
        };

        new TaqwiyahService().CalculateTreeStrength(tree);

        tree.CalculatedGrade.Should().Be("صحيح");
        tree.TaqwiyahDetails.Should().Contain("تنبيه");
    }
}

public class IlalAnalysisServiceTests
{
    [Fact]
    public void Analyze_ReportsMadarAndSummary()
    {
        var b = new IlalTestBuilder()
            .Narrator("ك1").Narrator("ك2").Narrator("مالك").Narrator("سفيان").Narrator("الزهري").Narrator("أنس", "companion");
        b.Chain("قال رسول الله ﷺ الدين النصيحة", "أ", "ك1", "حدثنا", "مالك", "عن", "الزهري", "عن", "أنس");
        b.Chain("قال رسول الله ﷺ الدين النصيحة", "ب", "ك2", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أنس");

        var report = IlalAnalysisService.Analyze(b.Build());

        report.Findings.Should().BeEmpty();
        report.HasQadihah.Should().BeFalse();
        report.Madars.Should().ContainSingle(m => m.NarratorId == b.Id("الزهري") && m.BranchCount == 2);
        report.SummaryAr.Should().Contain("لم تظهر علة");
    }

    [Fact]
    public void Analyze_ReportsWeakestNarratorAboveTheCompiler()
    {
        var b = new IlalTestBuilder()
            .Narrator("ك1", "weak").Narrator("مالك").Narrator("الزهري", "mostly_reliable").Narrator("أنس", "companion");
        b.Chain("قال رسول الله ﷺ الدين النصيحة", "أ", "ك1", "حدثنا", "مالك", "عن", "الزهري", "عن", "أنس");

        var tariq = IlalAnalysisService.Analyze(b.Build()).Turuq.Single();

        // the weak compiler is ignored; الزهري (tier 4) is the weakest link
        tariq.WeakestNarratorId.Should().Be(b.Id("الزهري"));
        tariq.WeakestTier.Should().Be(4);
    }
}
