using System.Collections.Generic;
using FluentAssertions;
using SmartHadithTree.Application.Services;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class RelevanceScorerTests
{
    private static readonly string[] Terms = ["اذا", "ذهب", "المذهب", "ابعد"];
    private const string Phrase = "اذا ذهب المذهب ابعد";

    [Fact]
    public void Score_ShouldBe100_WhenExactPhraseIsPresent()
    {
        var result = RelevanceScorer.Score("ان النبي كان اذا ذهب المذهب ابعد", Terms, Phrase);

        result.Should().NotBeNull();
        result!.Percent.Should().Be(100);
        result.ReasonAr.Should().Contain("حرفياً");
    }

    [Fact]
    public void Score_ShouldRankTightWordsAboveScatteredWords()
    {
        var tight = RelevanceScorer.Score("وكان اذا ذهب ابعد في المذهب", Terms, Phrase)!;
        var filler = string.Join(' ', System.Linq.Enumerable.Repeat("كلمة", 300));
        var scattered = RelevanceScorer.Score($"اذا ذهب {filler} المذهب {filler} ابعد", Terms, Phrase)!;

        tight.Percent.Should().BeGreaterThan(scattered.Percent);
        scattered.Percent.Should().BeLessThan(40, "all words are present but hundreds of words apart");
        scattered.ReasonAr.Should().Contain("داخل");
    }

    [Fact]
    public void Score_ShouldDropWithMissingTerms_AndReportHowManyWereFound()
    {
        var result = RelevanceScorer.Score("اذا ذهب الرجل", Terms, Phrase)!;

        result.Percent.Should().BeLessThan(60);
        result.ReasonAr.Should().Contain("2 من 4");
    }

    [Fact]
    public void Score_ShouldReturnNull_WhenNoTermOccursInTheMatn()
    {
        RelevanceScorer.Score("نص لا علاقة له", Terms, Phrase).Should().BeNull();
        RelevanceScorer.Score("", Terms, Phrase).Should().BeNull();
        RelevanceScorer.Score("اذا ذهب", new List<string>(), null).Should().BeNull();
    }

    [Fact]
    public void Score_ShouldPreferTheSameOrder()
    {
        var inOrder = RelevanceScorer.Score("اذا ذهب ثم المذهب ثم ابعد", Terms, Phrase)!;
        var reversed = RelevanceScorer.Score("ابعد ثم المذهب ثم ذهب ثم اذا", Terms, Phrase)!;

        inOrder.Percent.Should().BeGreaterThan(reversed.Percent);
    }

    [Fact]
    public void Score_ShouldGiveTheStartOfTheTightestGroup()
    {
        var filler = string.Join(' ', System.Linq.Enumerable.Repeat("كلمة", 50));
        var matn = $"اذا {filler} ثم اذا ذهب المذهب ابعد";

        var result = RelevanceScorer.Score(matn, Terms, Phrase)!;

        matn.Substring(result.WindowStartChar).Should().StartWith("اذا ذهب المذهب");
    }

    [Fact]
    public void QueryTerms_ShouldNormalizeAndDropPunctuationAndDuplicates()
    {
        RelevanceScorer.QueryTerms("إِذَا ذَهَبَ، المَذهبَ أَبعد! إذا")
            .Should().Equal("اذا", "ذهب", "المذهب", "ابعد");
    }

    [Fact]
    public void Score_ShouldFindAWord_WhenAPrefixIsAttachedToItsStem()
    {
        // "للجنه" contains "جنه" but not "الجنه": the word must still count as found.
        var withPrefix = RelevanceScorer.Score("للجنه اقرب الى احدكم", new[] { "الجنه", "اقرب" }, "الجنه اقرب")!;

        withPrefix.Percent.Should().BeGreaterThan(70);
        withPrefix.ReasonAr.Should().NotContain("وُجد");
    }

    [Fact]
    public void Score_ShouldRequireARepeatedQueryWordToRepeat()
    {
        // Query "الحلال بين الحرام بين": the hadith of Nu'man has "بين" twice, the hadith about the duff only once.
        string[] terms = ["الحلال", "بين", "الحرام", "بين"];
        var nuaman = RelevanceScorer.Score("ان الحلال بين وان الحرام بين وبينهما امور مشتبهات", terms)!;
        var duff = RelevanceScorer.Score("فصل ما بين الحلال والحرام الصوت يعني الضرب بالدف", terms)!;

        nuaman.Percent.Should().BeGreaterThan(duff.Percent);
        duff.ReasonAr.Should().Contain("3 من 4");
    }
}
