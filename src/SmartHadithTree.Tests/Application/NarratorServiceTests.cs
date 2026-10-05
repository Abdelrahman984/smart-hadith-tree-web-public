using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class NarratorServiceTests
{
    private HadithTreeDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new HadithTreeDbContext(options);
    }

    [Fact]
    public async Task GetNarratorDetailsAsync_ShouldReturnNull_WhenNarratorNotFound()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var service = new NarratorService(context);

        // Act
        var result = await service.GetNarratorDetailsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNarratorDetailsAsync_ShouldReturnNarrator_WhenExists()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var narratorId = Guid.NewGuid();
        context.Narrators.Add(new Narrator
        {
            Id = narratorId,
            FullName = "Test Narrator",
            KnownAs = "Test",
            Biography = "A test biography."
        });
        await context.SaveChangesAsync(default);
        
        var service = new NarratorService(context);

        // Act
        var result = await service.GetNarratorDetailsAsync(narratorId);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Test Narrator");
        result.Biography.Should().Be("A test biography.");
    }
}
