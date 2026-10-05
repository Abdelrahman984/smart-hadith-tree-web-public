using FluentAssertions;
using SmartHadithTree.Application.Services.Ilal;

namespace SmartHadithTree.Tests.Application.Ilal;

public class NarratorGradeScaleTests
{
    [Theory]
    [InlineData(1, "weak", 1)]          // Ibn Hajar's rank wins over the legacy grade
    [InlineData(3, null, 3)]
    [InlineData(12, "reliable", 12)]
    [InlineData(null, "reliable", 3)]   // no rank: the legacy Itqan grade
    [InlineData(null, null, NarratorGradeScale.DefaultTier)]
    [InlineData(0, "weak", 7)]          // not a rank
    [InlineData(13, null, NarratorGradeScale.DefaultTier)]
    public void ToTier_UsesTheRankWhenKnown(int? rank, string? gradeEn, int expected) =>
        NarratorGradeScale.ToTier(rank, gradeEn).Should().Be(expected);

    [Theory]
    [InlineData(1, "صحابي")]
    [InlineData(2, "ثقة ثبت")]
    [InlineData(3, "ثقة")]
    [InlineData(4, "صدوق")]
    [InlineData(6, "مقبول")]
    [InlineData(9, "مجهول")]
    [InlineData(10, "متروك")]
    [InlineData(12, "كذاب")]
    public void ToArabicLabel_NamesIbnHajarsRank(int rank, string expected) =>
        NarratorGradeScale.ToArabicLabel(rank, null).Should().Be(expected);

    [Theory]
    [InlineData(1, "companion")]
    [InlineData(2, "reliable")]
    [InlineData(3, "reliable")]
    [InlineData(5, "mostly_reliable")]
    [InlineData(8, "weak")]
    [InlineData(9, "unknown")]
    [InlineData(11, "abandoned")]
    [InlineData(12, "fabricator")]
    public void ToGradeEn_GivesTheStringTheFrontendKnows(int rank, string expected) =>
        NarratorGradeScale.ToGradeEn(rank).Should().Be(expected);

    [Fact]
    public void ToGradeEn_WithoutARank_IsNull() => NarratorGradeScale.ToGradeEn(null).Should().BeNull();

    [Fact]
    public void ACompanionByRank_IsNotATabii()
    {
        NarratorGradeScale.IsCompanion(1, null, null).Should().BeTrue();
        NarratorGradeScale.IsTabii(1, null, "الثانية").Should().BeFalse();
        NarratorGradeScale.IsTabii(3, null, "الثانية").Should().BeTrue();
        NarratorGradeScale.IsTabii(3, null, "السابعة").Should().BeFalse();
    }
}
