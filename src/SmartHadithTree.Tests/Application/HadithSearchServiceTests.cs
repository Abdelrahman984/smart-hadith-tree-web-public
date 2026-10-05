using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class HadithSearchServiceTests
{
    private HadithTreeDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new HadithTreeDbContext(options);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithShamelaAndOperator_ReturnsMatchingHadiths()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 1,
                MatnArabic = "نهى رسول الله صلى الله عليه وسلم عن بيع الغرر",
                NormalizedMatn = "نهى رسول الله صلى الله عليه وسلم عن بيع الغرر",
                NormalizedBookName = "صحيح البخاري"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 2,
                MatnArabic = "إنما الأعمال بالنيات",
                NormalizedMatn = "انما الاعمال بالنيات",
                NormalizedBookName = "صحيح البخاري"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "نهى", "بيع" },
            Operator = SearchLogicalOperator.And,
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(1);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithShamelaExclude_FiltersOutExcludedHadiths()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 101,
                MatnArabic = "صيام يوم عرفة أحتسب على الله أن يكفر السنة",
                NormalizedMatn = "صيام يوم عرفه احتسب على الله ان يكفر السنه",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 102,
                MatnArabic = "من صام رمضان إيمانا واحتسابا",
                NormalizedMatn = "من صام رمضان ايمانا واحتسابا",
                NormalizedBookName = "صحيح مسلم"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "صيام" },
            ExcludePhrases = new List<string> { "رمضان" },
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(101);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithOrdered_MatchesOnlyCorrectOrder()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 201,
                MatnArabic = "نهى عن بيع الملامسة",
                NormalizedMatn = "نهى عن بيع الملامسه",
                NormalizedBookName = "صحيح البخاري"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 202,
                MatnArabic = "البيع بالخيار ما لم يتفرقا ولا نهى في ذلك",
                NormalizedMatn = "البيع بالخيار ما لم يتفرقا ولا نهى في ذلك",
                NormalizedBookName = "صحيح البخاري"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "نهى", "بيع" },
            Operator = SearchLogicalOperator.And,
            IsOrdered = true,
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(201);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithBothAndAndOrPhrases_MatchesCorrectly()
    {
        // Arrange:
        // Hadith 301: Contains "نهى" (AND) + "الغرر" (OR match 1)
        // Hadith 302: Contains "نهى" (AND) + "النجش" (OR match 2)
        // Hadith 303: Contains "نهى" (AND) + "السلم" (neither OR matches)
        // Hadith 304: Contains "الغرر" (OR match) but NOT "نهى"
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 301,
                MatnArabic = "نهى رسول الله عن بيع الغرر",
                NormalizedMatn = "نهى رسول الله عن بيع الغرر",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 302,
                MatnArabic = "نهى رسول الله عن النجش",
                NormalizedMatn = "نهى رسول الله عن النجش",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 303,
                MatnArabic = "نهى عن بيع السلم في التمر",
                NormalizedMatn = "نهى عن بيع السلم في التمر",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 304,
                MatnArabic = "في بيع الغرر أحكام متعددة",
                NormalizedMatn = "في بيع الغرر احكام متعدده",
                NormalizedBookName = "صحيح مسلم"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        // Search for (AND: "نهى") + (OR: "الغرر" OR "النجش")
        var request = new SearchRequestDto
        {
            AndPhrases = new List<string> { "نهى" },
            OrPhrases = new List<string> { "الغرر", "النجش" },
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert:
        // Must match 301 and 302, but NOT 303 (missing OR) and NOT 304 (missing AND)
        result.Should().HaveCount(2);
        result.Select(r => r.HadithNumber).Should().BeEquivalentTo(new[] { 301, 302 });
    }

    [Fact]
    public async Task SearchHadithsAsync_WithIsnadScope_MatchesNormalizedFullIsnadText()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 401,
                MatnArabic = "إنما الأعمال بالنيات",
                NormalizedMatn = "انما الاعمال بالنيات",
                NormalizedBookName = "صحيح البخاري",
                FullIsnadText = "سفيان بن عيينه | يحيى بن سعيد الانصاري | عمر بن الخطاب ابو حفص"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 402,
                MatnArabic = "بني الإسلام على خمس",
                NormalizedMatn = "بني الاسلام على خمس",
                NormalizedBookName = "صحيح مسلم",
                FullIsnadText = "مالك بن انس | نافع | عبد الله بن عمر"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        // Search with unnormalized Hamza ("أبو حفص" / "عيينة") in Isnad scope
        var request = new SearchRequestDto
        {
            Query = "عيينة أبو حفص",
            Scope = SearchScope.Isnad,
            Match = SearchMatchType.AllWords
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(401);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithPagination_ReturnsRequestedPage()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        for (int i = 1; i <= 5; i++)
        {
            context.Hadiths.Add(new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 500 + i,
                MatnArabic = $"حديث رقم {i} في الإيمان",
                NormalizedMatn = $"حديث رقم {i} في الايمان",
                NormalizedBookName = "صحيح البخاري"
            });
        }
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Query = "الإيمان",
            Scope = SearchScope.Matn,
            Page = 2,
            PageSize = 2
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchHadithsAsync_FiltersOutScatteredMegaRecord_AndRanksExactPhraseFirst()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var padding = new string('ا', 2500);
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "المعجم الكبير للطبراني",
                HadithNumber = 1103,
                MatnArabic = $"إذا ذهب {padding} أريد المذهب {padding} أبعد",
                NormalizedMatn = $"اذا ذهب {padding} اريد المذهب {padding} ابعد",
                NormalizedBookName = "المعجم الكبير للطبراني"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "مسند أحمد",
                HadithNumber = 18171,
                MatnArabic = "وكان إذا ذهب أبعد في المذهب",
                NormalizedMatn = "وكان اذا ذهب ابعد في المذهب",
                NormalizedBookName = "مسند احمد"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "سنن أبي داود",
                HadithNumber = 1,
                MatnArabic = "أن النبي صلى الله عليه وسلم كان إذا ذهب المذهب أبعد",
                NormalizedMatn = "ان النبي صلى الله عليه وسلم كان اذا ذهب المذهب ابعد",
                NormalizedBookName = "سنن ابي داود"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Query = "إِذَا ذَهَبَ الْمَذْهَبَ أَبْعَدَ",
            Scope = SearchScope.All,
            Match = SearchMatchType.AllWords
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert: Scattered mega-record (#1103) is excluded, and exact phrase (#1) ranks before variant (#18171)
        result.Should().HaveCount(2);
        result[0].HadithNumber.Should().Be(1);
        result[1].HadithNumber.Should().Be(18171);
    }

    [Fact]
    public async Task SearchHadithsAsync_KeepsLongHadithWithExactPhrase_WhenManyShorterMatchesExist()
    {
        // Arrange: 200 short hadiths contain every word (in another order); one longer hadith has the exact phrase.
        var context = GetInMemoryDbContext();
        for (var i = 0; i < 200; i++)
        {
            context.Hadiths.Add(new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "كتاب",
                HadithNumber = 1000 + i,
                MatnArabic = $"المذهب ابعد اذا ذهب {i}",
                NormalizedMatn = $"المذهب ابعد اذا ذهب {i}",
                NormalizedBookName = "كتاب"
            });
        }

        var filler = string.Join(' ', System.Linq.Enumerable.Repeat("وكان", 120));
        var longHadith = new HadithText
        {
            Id = Guid.NewGuid(),
            BookName = "كتاب",
            HadithNumber = 1,
            MatnArabic = $"{filler} اذا ذهب المذهب ابعد {filler}",
            NormalizedMatn = $"{filler} اذا ذهب المذهب ابعد {filler}",
            NormalizedBookName = "كتاب"
        };
        context.Hadiths.Add(longHadith);
        await context.SaveChangesAsync();

        var service = new HadithSearchService(context, new Mock<IHadithChainRepository>().Object, new Mock<ITaqwiyahService>().Object);

        // Act
        var result = await service.SearchHadithsAsync(new SearchRequestDto
        {
            Query = "اذا ذهب المذهب ابعد",
            Scope = SearchScope.Matn,
            Match = SearchMatchType.AllWords
        });

        // Assert
        result.Should().NotBeEmpty();
        result[0].Id.Should().Be(longHadith.Id, "an exact-phrase hit must not be cut by the shortest-first candidate cap");
        result[0].RelevancePercent.Should().Be(100);
    }

    [Fact]
    public async Task SearchHadithsAsync_ReturnsRelevanceWithReason()
    {
        var context = GetInMemoryDbContext();
        context.Hadiths.Add(new HadithText
        {
            Id = Guid.NewGuid(),
            BookName = "كتاب",
            HadithNumber = 1,
            MatnArabic = "الطهور شطر الايمان",
            NormalizedMatn = "الطهور شطر الايمان",
            NormalizedBookName = "كتاب"
        });
        await context.SaveChangesAsync();
        var service = new HadithSearchService(context, new Mock<IHadithChainRepository>().Object, new Mock<ITaqwiyahService>().Object);

        var result = await service.SearchHadithsAsync(new SearchRequestDto { Query = "الطهور شطر", Scope = SearchScope.Matn });

        result.Should().ContainSingle();
        result[0].RelevancePercent.Should().BeGreaterThan(0);
        result[0].RelevanceReason.Should().NotBeNullOrEmpty();
    }
}

