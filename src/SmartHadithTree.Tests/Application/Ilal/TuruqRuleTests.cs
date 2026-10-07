using FluentAssertions;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;
using SmartHadithTree.Domain.Utilities;

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

/// <summary>
/// «الطهور شطر الإيمان»: Ibn Abi Shayba (37) and Awana (38) quote only the opening saying; Muslim and
/// the others give the whole hadith. The short texts are abridgements, not omissions by a narrator.
/// </summary>
public class AbridgedMatnTests
{
    private const string Short = "أن رسول الله ﷺ كان يقول: الطهور شطر الإيمان";
    private const string Long = "قال رسول الله ﷺ: الطهور شطر الإيمان، والحمد لله تملأ الميزان، وسبحان الله والحمد لله تملآن ما بين السماوات والأرض، والصلاة نور، والصدقة برهان";
    private const string Wudu = "قال رسول الله ﷺ: الطهور شطر الإيمان، والوضوء ضياء، والصبر برهان، والصلاة نور، والصدقة سر";

    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("أبان").Narrator("يحيى").Narrator("معمر").Narrator("ثالث")
        .Narrator("الراوي").Narrator("الصحابي", "companion")
        .Narrator("ابن أبي شيبة").Narrator("مسلم").Narrator("أحمد").Narrator("الدارمي");

    [Theory]
    [InlineData(Short, Long)]
    [InlineData(Long, Short)]
    public void ShortFragmentOfALongerText_IsNotAnAdditionOrOmission(string reference, string compared)
    {
        var diff = MatnAligner.Align(MatnText.ExtractBody(reference), MatnText.ExtractBody(compared));

        MatnAtMadarRule.Classify(diff).Should().Be(MatnAtMadarRule.DiffKind.None);
    }

    [Fact]
    public void FragmentCompiler_DoesNotProduceFindingsAgainstTheLongVersions()
    {
        var b = Narrators();
        b.Chain(Short, "مصنف ابن أبي شيبة", "ابن أبي شيبة", "حدثنا", "أبان", "عن", "الراوي", "عن", "الصحابي");
        b.Chain(Long, "صحيح مسلم", "مسلم", "حدثنا", "يحيى", "عن", "الراوي", "عن", "الصحابي");
        b.Chain(Long, "مسند أحمد", "أحمد", "حدثنا", "معمر", "عن", "الراوي", "عن", "الصحابي");

        new MatnAtMadarRule().Evaluate(b.Build()).Should().BeEmpty();
    }

    [Fact]
    public void FragmentIsNotCountedAsSupportForAnyVersion()
    {
        // Two real versions that differ; the fragment must not tip the balance toward either of them.
        var b = Narrators();
        b.Chain(Short, "مصنف ابن أبي شيبة", "ابن أبي شيبة", "حدثنا", "أبان", "عن", "الراوي", "عن", "الصحابي");
        b.Chain(Long, "صحيح مسلم", "مسلم", "حدثنا", "يحيى", "عن", "الراوي", "عن", "الصحابي");
        b.Chain(Wudu, "سنن الدارمي", "الدارمي", "حدثنا", "معمر", "عن", "الراوي", "عن", "الصحابي");

        var findings = new MatnAtMadarRule().Evaluate(b.Build()).ToList();

        findings.Should().ContainSingle().Which.Type.Should().Be(IllahType.Idtirab);
    }

    [Fact]
    public void RealAdditionOverAFullShortText_StillCounts()
    {
        // Four content words are a narration of their own, so the extra clause is a real addition.
        var b = Narrators();
        b.Chain("عن الصحابي أن رسول الله ﷺ كان إذا ذهب المذهب أبعد", "مصنف ابن أبي شيبة", "ابن أبي شيبة", "حدثنا", "أبان", "عن", "الراوي", "عن", "الصحابي");
        b.Chain("عن الصحابي أن رسول الله ﷺ كان إذا ذهب المذهب أبعد فذهب لحاجة فقال ائتني بوضوء فتوضأ ومسح على الخفين", "صحيح مسلم", "مسلم", "حدثنا", "يحيى", "عن", "الراوي", "عن", "الصحابي");

        new MatnAtMadarRule().Evaluate(b.Build()).Should().ContainSingle()
            .Which.Type.Should().Be(IllahType.Ziyadah);
    }
}

/// <summary>A narrator the data has no grade for is «غير محرر»: no verdict, which is not the same as weak.</summary>
public class UnrankedNarratorTests
{
    private const string Base = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من صام رمضان إيمانا واحتسابا غفر له ما تقدم من ذنبه";
    private const string WithAddition = Base + " وما تأخر من ذنبه كله";
    private const string Contradicting = "عن أبي هريرة أن رسول الله صلى الله عليه وسلم قال من قام ليلة القدر إيمانا واحتسابا غفر له ما تقدم من ذنبه";

    private static IlalTestBuilder Narrators(string studentGrade) => new IlalTestBuilder()
        .Narrator("مالك").Narrator("معمر").Narrator("الطالب", studentGrade)
        .Narrator("الزهري").Narrator("أبو سلمة").Narrator("أبو هريرة", "companion")
        .Narrator("البخاري").Narrator("مسلم").Narrator("ابن ماجه");

    [Theory]
    [InlineData("", false)]
    [InlineData("weak", true)]
    [InlineData("unknown", true)]
    [InlineData("reliable", true)]
    public void IsRanked_TellsNoVerdictFromAVerdict(string grade, bool expected)
    {
        var b = new IlalTestBuilder().Narrator("راو", grade);

        b.Build().Narrator(b.Id("راو"))!.IsRanked.Should().Be(expected);
    }

    [Fact]
    public void ARank_IsAVerdictEvenWithoutALegacyGrade()
    {
        var b = new IlalTestBuilder().Narrator("راو", "", rank: 8);

        b.Build().IsRanked(b.Id("راو")).Should().BeTrue();
    }

    [Fact]
    public void UnrankedNarratorContradictingReliablePeers_IsATanbihNotNakarah()
    {
        var b = Narrators("");
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base, "صحيح مسلم", "مسلم", "حدثنا", "معمر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Contradicting, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "الطالب", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().NotBe(IllahType.Nakarah);
        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.EvidenceAr.Should().Contain("غير محرر");
    }

    [Fact]
    public void RankedWeakNarratorContradictingReliablePeers_IsStillNakarah()
    {
        var b = Narrators("weak");
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Base, "صحيح مسلم", "مسلم", "حدثنا", "معمر", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(Contradicting, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "الطالب", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Nakarah);
        finding.Severity.Should().Be(IllahSeverity.Qadihah);
    }

    [Fact]
    public void UnrankedNarratorAddingText_IsNotAWeakNarratorsAddition()
    {
        var b = Narrators("");
        b.Chain(Base, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");
        b.Chain(WithAddition, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "الطالب", "عن", "الزهري", "عن", "أبو سلمة", "عن", "أبو هريرة");

        var finding = new MatnAtMadarRule().Evaluate(b.Build()).Single();

        finding.Type.Should().NotBe(IllahType.Nakarah);
        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.EvidenceAr.Should().Contain("غير محرر").And.NotContain("ضعيف");
    }
}
