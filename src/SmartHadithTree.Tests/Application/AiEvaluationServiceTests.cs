using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.SemanticKernel;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class AiEvaluationServiceTests
{
    private static readonly string[] Evaluations =
    [
        "قال ابن حجر: صدوقٌ يَهِم",
        "قال الذهبي: ثقة حافظ"
    ];

    [Fact]
    public async Task GenerateSummary_ShouldAbstainWithoutTier_WhenNoEvaluations()
    {
        var service = new AiEvaluationService(new Kernel());
        var narrator = new NarratorDetailDto { FullName = "راوٍ مجهول", Evaluations = [] };

        var result = await service.GenerateNarratorEvaluationSummaryAsync(narrator);

        result.Status.Should().Be(AiEvaluationStatus.NoEvaluations);
        result.Tier.Should().BeEmpty();
        result.VerbatimQuote.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ShouldAccept_WhenQuoteIsVerbatimAndTierKnown()
    {
        var dto = new ExtractedAiEvaluationDto { VerbatimQuote = "صدوق يهم", Tier = "T5", Justification = "x" };

        var result = AiEvaluationService.Validate(dto, Evaluations);

        result.Status.Should().Be(AiEvaluationStatus.Ok);
        result.Tier.Should().Be("T5");
    }

    [Fact]
    public void Validate_ShouldReject_WhenQuoteIsNotInEvaluations()
    {
        var dto = new ExtractedAiEvaluationDto { VerbatimQuote = "قال أحمد: متروك", Tier = "T9", Justification = "x" };

        var result = AiEvaluationService.Validate(dto, Evaluations);

        result.Status.Should().Be(AiEvaluationStatus.Unverified);
        result.Tier.Should().BeEmpty();
        result.VerbatimQuote.Should().BeEmpty();
    }

    [Theory]
    [InlineData("T0")]
    [InlineData("T13")]
    [InlineData("ثقة")]
    [InlineData("")]
    public void Validate_ShouldReject_WhenTierIsNotKnown(string tier)
    {
        var dto = new ExtractedAiEvaluationDto { VerbatimQuote = "ثقة حافظ", Tier = tier, Justification = "x" };

        var result = AiEvaluationService.Validate(dto, Evaluations);

        result.Status.Should().Be(AiEvaluationStatus.Unverified);
    }

    [Fact]
    public void IsQuoteGrounded_ShouldIgnoreDiacriticsAndWhitespace()
    {
        AiEvaluationService.IsQuoteGrounded("  صَدُوقٌ   يَهِمُ ", Evaluations).Should().BeTrue();
        AiEvaluationService.IsQuoteGrounded("", Evaluations).Should().BeFalse();
        AiEvaluationService.IsQuoteGrounded(null, Evaluations).Should().BeFalse();
    }
}
