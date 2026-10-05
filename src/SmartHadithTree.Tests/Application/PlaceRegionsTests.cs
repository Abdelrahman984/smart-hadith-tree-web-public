using FluentAssertions;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Tests.Application;

public class PlaceRegionsTests
{
    [Fact]
    public void ExtractPlaces_DropsWordsThatDescribeTheStay()
    {
        PlaceRegions.ExtractPlaces("سكن المدينة، ونزل البصرة", null)
            .Should().BeEquivalentTo("المدينه", "البصره");
        PlaceRegions.ExtractPlaces("بغداد، مرو، قرية زرزم، خراسان", null)
            .Should().BeEquivalentTo("بغداد", "مرو", "زرزم", "خراسان");
    }

    [Fact]
    public void SameRegion_IsNotReported()
    {
        // بغداد and البصرة are both العراق
        PlaceRegions.HaveNothingInCommon("بغداد", null, "البصرة", null, out var note).Should().BeFalse();
        note.Should().BeNull();
    }

    [Fact]
    public void DifferentRegions_AreReportedWithAReviewNote()
    {
        // Ali b. Hujr (بغداد، مرو، قرية زرزم، خراسان) taught al-Nasa'i (زقاق القناديل، مصر): different regions
        PlaceRegions.HaveNothingInCommon(
            "بغداد، مرو، قرية زرزم، خراسان", null, "زقاق القناديل، مصر", null, out var note).Should().BeTrue();
        note.Should().Contain("يُراجع").And.Contain("رحلة")
            .And.Contain("زقاق القناديل").And.Contain("زرزم"); // shown as written (ة not turned into ه)
    }

    [Fact]
    public void MedinaAndMecca_AreTheSameRegion()
    {
        PlaceRegions.HaveNothingInCommon("المدينة", null, "مكة", null, out _).Should().BeFalse();
    }

    [Fact]
    public void ResidenceOverlap_IsNotReported()
    {
        PlaceRegions.HaveNothingInCommon("سكن المدينة، ونزل البصرة", null, "البصرة", null, out _).Should().BeFalse();
    }

    [Fact]
    public void UnknownPlaces_AreOnlyComparedByName()
    {
        PlaceRegions.HaveNothingInCommon("قرية س", null, "قرية ص", null, out _).Should().BeTrue();
        PlaceRegions.HaveNothingInCommon("قرية س", null, "س", null, out _).Should().BeFalse();
    }

    [Fact]
    public void NoPlaces_IsNotReported()
    {
        PlaceRegions.HaveNothingInCommon(null, null, "البصرة", null, out _).Should().BeFalse();
    }
}
