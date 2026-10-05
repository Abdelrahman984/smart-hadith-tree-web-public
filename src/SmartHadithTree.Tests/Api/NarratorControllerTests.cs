using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SmartHadithTree.Api.Controllers;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using Xunit;

namespace SmartHadithTree.Tests.Api;

public class NarratorControllerTests
{
    private readonly Mock<INarratorService> _mockNarratorService;
    private readonly NarratorController _controller;

    public NarratorControllerTests()
    {
        _mockNarratorService = new Mock<INarratorService>();
        _controller = new NarratorController(_mockNarratorService.Object);
    }

    [Fact]
    public async Task GetNarrator_ShouldReturnNotFound_WhenNarratorDoesNotExist()
    {
        // Arrange
        var narratorId = Guid.NewGuid();
        _mockNarratorService
            .Setup(s => s.GetNarratorDetailsAsync(narratorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NarratorDetailDto?)null);

        // Act
        var result = await _controller.GetNarrator(narratorId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetNarrator_ShouldReturnOk_WhenNarratorExists()
    {
        // Arrange
        var narratorId = Guid.NewGuid();
        var dto = new NarratorDetailDto
        {
            Id = narratorId,
            FullName = "Imam Malik",
            KnownAs = "Malik bin Anas"
        };
        _mockNarratorService
            .Setup(s => s.GetNarratorDetailsAsync(narratorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _controller.GetNarrator(narratorId, CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedDto = okResult.Value.Should().BeOfType<NarratorDetailDto>().Subject;
        returnedDto.FullName.Should().Be("Imam Malik");
    }
}
