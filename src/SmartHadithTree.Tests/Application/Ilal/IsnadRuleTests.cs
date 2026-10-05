using FluentAssertions;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

public class TadlisRuleTests
{
    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("أبو داود").Narrator("شعبة").Narrator("قتادة", mudallisTier: 3)
        .Narrator("أنس", "companion").Narrator("البخاري").Narrator("الثوري", mudallisTier: 2);

    [Fact]
    public void MudallisAnAna_WithoutHearingElsewhere_IsQadihah()
    {
        var b = Narrators();
        var id = b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "شعبة", "عن", "قتادة", "عن", "أنس");

        var findings = new TadlisRule().Evaluate(b.Build()).ToList();

        findings.Should().ContainSingle();
        findings[0].Type.Should().Be(IllahType.Tadlis);
        findings[0].Severity.Should().Be(IllahSeverity.Qadihah);
        findings[0].NarratorIds.Should().Contain([b.Id("قتادة"), b.Id("أنس")]);
        findings[0].HadithIds.Should().Equal(id);
    }

    [Fact]
    public void MudallisAnAna_WithExplicitHearingInAnotherTariq_IsOnlyTanbih()
    {
        var b = Narrators();
        b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "شعبة", "عن", "قتادة", "عن", "أنس");
        b.Chain("x", "صحيح البخاري", "البخاري", "حدثنا", "شعبة", "حدثنا", "قتادة", "سمعت", "أنس");

        var finding = new TadlisRule().Evaluate(b.Build()).Single();

        finding.Severity.Should().Be(IllahSeverity.Tanbih);
    }

    [Fact]
    public void MudallisAnAna_InSahihayn_IsGhayrQadihah()
    {
        var b = Narrators();
        b.Chain("x", "صحيح البخاري", "البخاري", "حدثنا", "شعبة", "عن", "قتادة", "عن", "أنس");

        new TadlisRule().Evaluate(b.Build()).Single().Severity.Should().Be(IllahSeverity.GhayrQadihah);
    }

    [Fact]
    public void MudallisAnAna_AlsoInSahihayn_IsGhayrQadihahForOtherBooksToo()
    {
        var b = Narrators();
        b.Chain("x", "صحيح البخاري", "البخاري", "حدثنا", "شعبة", "عن", "قتادة", "عن", "أنس");
        b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "شعبة", "عن", "قتادة", "عن", "أنس");

        var finding = new TadlisRule().Evaluate(b.Build()).Single();

        finding.Severity.Should().Be(IllahSeverity.GhayrQadihah);
        finding.HadithIds.Should().HaveCount(2);
    }

    [Fact]
    public void ToleratedTierMudallis_IsIgnored()
    {
        var b = Narrators();
        b.Chain("x", "سنن أبي داود", "أبو داود", "حدثنا", "الثوري", "عن", "أنس");

        new TadlisRule().Evaluate(b.Build()).Should().BeEmpty();
    }
}

public class IkhtilatRuleTests
{
    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("أحمد").Narrator("جرير").Narrator("شعبة").Narrator("عطاء بن السائب", mukhtalit: true).Narrator("أبوه");

    [Theory]
    [InlineData(HearingTiming.After, IllahSeverity.Qadihah)]
    [InlineData(HearingTiming.Unknown, IllahSeverity.Tanbih)]
    public void NarrationFromMukhtalit_IsFlaggedByHearingTiming(HearingTiming timing, IllahSeverity expected)
    {
        var b = Narrators();
        b.Chain("x", "مسند أحمد", "أحمد", "حدثنا", "جرير", "عن", "عطاء بن السائب", "عن", "أبوه");
        if (timing != HearingTiming.Unknown) b.Hearing("عطاء بن السائب", "جرير", timing);

        var finding = new IkhtilatRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.Ikhtilat);
        finding.Severity.Should().Be(expected);
    }

    [Fact]
    public void NarrationHeardBeforeIkhtilat_IsSound()
    {
        var b = Narrators();
        b.Chain("x", "مسند أحمد", "أحمد", "حدثنا", "شعبة", "عن", "عطاء بن السائب", "عن", "أبوه");
        b.Hearing("عطاء بن السائب", "شعبة", HearingTiming.Before);

        new IkhtilatRule().Evaluate(b.Build()).Should().BeEmpty();
    }
}

public class HiddenInqitaRuleTests
{
    [Fact]
    public void LinkWithoutKnownRelation_IsFlagged_WhenBothHaveRelationData()
    {
        var b = new IlalTestBuilder().Narrator("أ").Narrator("ب").Narrator("ج").Narrator("د");
        b.Chain("x", "كتاب", "أ", "حدثنا", "ب", "عن", "ج");
        b.Relation("ب", "أ").Relation("ج", "د").Relation("د", "ب"); // ب and ج both have data, but no ج→ب link

        var finding = new HiddenInqitaRule().Evaluate(b.Build()).Single();

        finding.Type.Should().Be(IllahType.HiddenInqita);
        finding.NarratorIds.Should().Equal(b.Id("ج"), b.Id("ب"));
    }

    [Fact]
    public void CompilersOwnLink_IsNotJudged()
    {
        // ب → أ is the compiler's link: the books rarely list the compilers' shaykhs, so it is skipped
        var b = new IlalTestBuilder().Narrator("أ").Narrator("ب").Narrator("ج").Narrator("د");
        b.Chain("x", "كتاب", "أ", "حدثنا", "ب", "عن", "ج");
        b.Relation("ج", "ب").Relation("ب", "د").Relation("د", "أ"); // أ and ب have data, but no ب→أ relation

        new HiddenInqitaRule().Evaluate(b.Build()).Should().BeEmpty();
    }

    [Fact]
    public void LinkWithSparseData_IsNotFlagged()
    {
        var b = new IlalTestBuilder().Narrator("أ").Narrator("ب").Narrator("ج");
        b.Chain("x", "كتاب", "أ", "حدثنا", "ب", "عن", "ج");
        b.Relation("ب", "أ"); // ج has no relation data at all

        new HiddenInqitaRule().Evaluate(b.Build()).Should().BeEmpty();
    }
}
