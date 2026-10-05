using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Moq;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class HadithVerificationServiceTests
{
    private const string Matn = "إنما الأعمال بالنيات وإنما لكل امرئ ما نوى فمن كانت هجرته إلى الله ورسوله فهجرته إلى الله ورسوله";

    private static HadithVerificationService Create(params HadithSearchResultDto[] corpus)
    {
        var search = new Mock<IHadithSearchService>();
        search
            .Setup(s => s.SearchHadithsAsync(It.IsAny<SearchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(corpus.ToList());
        // An empty Kernel has no chat model, so the review step falls back to text matching.
        return new HadithVerificationService(search.Object, new Kernel());
    }

    private static HadithSearchResultDto Hadith(string matn, int number = 1) =>
        new() { Id = Guid.NewGuid(), BookName = "صحيح البخاري", HadithNumber = number, MatnArabic = matn };

    [Fact]
    public async Task Verify_ShouldReturnExact_WhenTextIsQuotedFromMatn()
    {
        var service = Create(Hadith(Matn));

        var result = await service.VerifyAsync("إنما الأعمال بالنيات وإنما لكل امرئ ما نوى");

        result.Status.Should().Be(HadithVerificationStatus.Exact);
        result.Matches.Should().ContainSingle().Which.BookName.Should().Be("صحيح البخاري");
    }

    [Fact]
    public async Task Verify_ShouldIgnoreDiacriticsAndHamzaForms()
    {
        var service = Create(Hadith(Matn));

        var result = await service.VerifyAsync("إِنَّمَا الْأَعْمَالُ بِالنِّيَّاتِ وَإِنَّمَا لِكُلِّ امْرِئٍ مَا نَوَى");

        result.Status.Should().Be(HadithVerificationStatus.Exact);
    }

    [Fact]
    public async Task Verify_ShouldNotFound_WhenNothingRetrieved_AndNameNoSource()
    {
        var service = Create();

        var result = await service.VerifyAsync("من قال كذا وكذا دخل الجنة بغير حساب");

        result.Status.Should().Be(HadithVerificationStatus.NotFound);
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ShouldNotFound_WhenRetrievedHadithIsUnrelated()
    {
        var service = Create(Hadith("الطهور شطر الإيمان والحمد لله تملأ الميزان"));

        var result = await service.VerifyAsync("من صلى الفجر في جماعة فهو في ذمة الله");

        result.Status.Should().Be(HadithVerificationStatus.NotFound);
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ShouldBeInvalid_WhenTextIsTooShortOrTooLong()
    {
        var service = Create(Hadith(Matn));

        (await service.VerifyAsync("إنما الأعمال")).Status.Should().Be(HadithVerificationStatus.Invalid);
        (await service.VerifyAsync(new string('ا', HadithVerificationService.MaxChars + 1)))
            .Status.Should().Be(HadithVerificationStatus.Invalid);
    }

    [Fact]
    public async Task Verify_ShouldOrderBestMatchFirst()
    {
        var service = Create(
            Hadith("إنما الأعمال بالنيات وإنما لكل امرئ ما نوى فمن كانت هجرته إلى دنيا يصيبها", 2),
            Hadith("إنما الأعمال بالنيات وإنما لكل امرئ ما نوى", 1));

        var result = await service.VerifyAsync("إنما الأعمال بالنيات وإنما لكل امرئ ما نوى");

        result.Matches[0].HadithNumber.Should().Be(1, "the shorter of two equal matches comes first");
    }

    /// <summary>A search that behaves like the real one: every query word must occur in the matn (substring).</summary>
    private static HadithVerificationService CreateStrict(params HadithSearchResultDto[] corpus)
    {
        var search = new Mock<IHadithSearchService>();
        search
            .Setup(s => s.SearchHadithsAsync(It.IsAny<SearchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchRequestDto r, CancellationToken _) =>
            {
                var words = r.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return corpus
                    .Where(h => words.All(w => SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(h.MatnArabic).Contains(w)))
                    .ToList();
            });
        return new HadithVerificationService(search.Object, new Kernel());
    }

    [Theory]
    [InlineData("الجنة أقرب إلى أحدكم من شراك نعله")]
    [InlineData("الجنة أقرب لأحدكم من شراك نعله")]
    [InlineData("الجنة أقرب إلى أحدكم من شراك نعله والنار مثل ذلك")]
    public async Task Verify_ShouldFind_WhenPrepositionOrPrefixDiffers(string input)
    {
        var service = CreateStrict(Hadith("الجنة أقرب إلى أحدكم من شراك نعله والنار مثل ذلك"));

        var result = await service.VerifyAsync(input);

        result.Status.Should().BeOneOf(HadithVerificationStatus.Exact, HadithVerificationStatus.Variant);
        result.Matches.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Verify_ShouldFind_WhenOneWordIsMisspelled()
    {
        var service = CreateStrict(Hadith(Matn));

        var result = await service.VerifyAsync("إنما الأعمال بالنيات وإنما لكل امرء ما نوا");

        result.Matches.Should().NotBeEmpty("the query retries without one word at a time");
    }

    [Fact]
    public async Task Verify_ShouldFindParaphrase_ThroughWordWindows_AndReportModelUnavailable()
    {
        var service = CreateStrict(Hadith("لا يؤمن أحدكم حتى يحب لأخيه ما يحب لنفسه"));

        // يكمل / إيمان are absent from the stored text, so only a window that skips them can retrieve it.
        var result = await service.VerifyAsync("لا يكمل إيمان أحدكم حتى يحب لأخيه ما يحب لنفسه");

        result.Matches.Should().NotBeEmpty();
        result.ModelStatus.Should().Be(VerificationModelStatus.Unavailable, "an empty Kernel has no chat model");
    }

    [Fact]
    public async Task Verify_ShouldReportModelNotNeeded_ForExactQuotation()
    {
        var service = Create(Hadith(Matn));

        var result = await service.VerifyAsync("إنما الأعمال بالنيات وإنما لكل امرئ ما نوى");

        result.ModelStatus.Should().Be(VerificationModelStatus.NotNeeded);
    }

    [Fact]
    public void UnmatchedWords_ShouldNameTheChangedWord_AndIgnoreDiacriticsAndPrepositions()
    {
        HadithVerificationService.UnmatchedWords("إنما الأقوال بالنيات وإنما لكل امرئ ما نوى", Matn)
            .Should().Equal("الأقوال");
        HadithVerificationService.UnmatchedWords("إِنَّمَا الْأَعْمَالُ بِالنِّيَّاتِ", Matn).Should().BeEmpty();
    }

    [Fact]
    public async Task Verify_ShouldReturnAlteredText_AsVariant_WithTheChangedWordShown()
    {
        var service = CreateStrict(Hadith(Matn));

        var result = await service.VerifyAsync("إنما الأقوال بالنيات وإنما لكل امرئ ما نوى");

        result.Status.Should().NotBe(HadithVerificationStatus.Exact);
        if (result.Matches.Count > 0) result.UnmatchedWords.Should().Contain("الأقوال");
    }

    [Fact]
    public async Task Verify_ShouldWidenWithWindows_WhenFirstResultsAreUnrelated()
    {
        // The all-words query returns only an unrelated hadith; the real one is reachable only by a 4-word window.
        var real = Hadith("لا يؤمن أحدكم حتى يحب لأخيه ما يحب لنفسه", 13);
        var noise = Hadith("أحدكم حتى يحب يحب يكمل ايمان", 99); // contains many query words, wrong order
        var search = new Mock<IHadithSearchService>();
        search
            .Setup(s => s.SearchHadithsAsync(It.IsAny<SearchRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchRequestDto r, CancellationToken _) =>
            {
                var words = r.Query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                return words.Length >= 5 ? new List<HadithSearchResultDto> { noise }
                    : new[] { real, noise }
                        .Where(h => words.All(w => SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(h.MatnArabic).Contains(w)))
                        .ToList();
            });
        var service = new HadithVerificationService(search.Object, new Kernel());

        var result = await service.VerifyAsync("لا يكمل إيمان أحدكم حتى يحب لأخيه ما يحب لنفسه");

        result.Matches.Should().NotBeEmpty();
        result.Matches[0].HadithNumber.Should().Be(13);
    }

    [Fact]
    public async Task Verify_ShouldNeverReportExact_ForNegatedRealHadith()
    {
        var service = Create(Hadith(Matn));

        var result = await service.VerifyAsync("إنما الأعمال ليست بالنيات وإنما لكل امرئ ما نوى");

        result.Status.Should().Be(HadithVerificationStatus.NotFound, "an added negation reverses the meaning");
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void HasUnmatchedNegation_ShouldIgnoreNegatorsPresentInBoth_AndPrefixedForms()
    {
        HadithVerificationService.HasUnmatchedNegation("لا ضرر ولا ضرار", "لا ضرر ولا ضرار في الإسلام").Should().BeFalse();
        HadithVerificationService.HasUnmatchedNegation("ولا ضرر ولا ضرار", "لا ضرر ولا ضرار").Should().BeFalse();
        HadithVerificationService.HasUnmatchedNegation("الأعمال ليست بالنيات", Matn).Should().BeTrue();
    }

    [Theory]
    [InlineData(1.0, HadithVerificationStatus.Exact)]
    [InlineData(0.7, HadithVerificationStatus.Variant)]
    [InlineData(0.4, HadithVerificationStatus.NotFound)]
    public void Classify_ShouldUseThresholds(double similarity, string expected) =>
        HadithVerificationService.Classify(similarity).Should().Be(expected);

    [Fact]
    public void PhraseIsGrounded_ShouldRequireThreeWordsPresentInBothTexts()
    {
        HadithVerificationService.PhraseIsGrounded("الأعمال بالنيات وإنما", "إنما الأعمال بالنيات وإنما لكل", Matn)
            .Should().BeTrue();
        HadithVerificationService.PhraseIsGrounded("الأعمال بالنيات", "إنما الأعمال بالنيات", Matn)
            .Should().BeFalse("fewer than three words");
        HadithVerificationService.PhraseIsGrounded("من صلى الفجر جماعة", "من صلى الفجر جماعة", Matn)
            .Should().BeFalse("not in the candidate");
    }
}
