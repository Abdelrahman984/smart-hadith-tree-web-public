using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartHadithTree.Api.Controllers;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Tests.Api;

public class IlalControllerTests
{
    private readonly Mock<IIlalAnalysisService> _ilal = new();
    private readonly Mock<IHadithSearchService> _search = new();
    private readonly Mock<IIlalExplanationService> _explain = new();
    private readonly IlalController _controller;

    public IlalControllerTests()
    {
        _controller = new IlalController(_ilal.Object, _search.Object, _explain.Object);
    }

    [Fact]
    public async Task Analyze_WithoutValidIds_ReturnsBadRequest()
    {
        var result = await _controller.Analyze("not-a-guid", CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AnalyzeHadith_IncludesRelatedTuruq()
    {
        var id = Guid.NewGuid();
        var related = Guid.NewGuid();
        _search.Setup(s => s.FindRelatedHadithsAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new HadithSearchResultDto { Id = related }]);
        _ilal.Setup(s => s.AnalyzeAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IlalReportDto());

        var result = await _controller.AnalyzeHadith(id, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _ilal.Verify(s => s.AnalyzeAsync(
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { id, related })),
            It.IsAny<CancellationToken>()));
    }
}
