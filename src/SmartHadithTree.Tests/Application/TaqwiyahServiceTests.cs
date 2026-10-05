using Xunit;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Tests.Application;

public class TaqwiyahServiceTests
{
    private static ComparativeTreeResponseDto TreeOf(params (int? Tier, Guid? Narrator)[] routes) => new()
    {
        IlalReport = new IlalReportDto
        {
            Turuq = routes.Select(r => new IlalTariqDto
            {
                HadithId = Guid.NewGuid(),
                WeakestTier = r.Tier,
                WeakestNarratorId = r.Narrator
            }).ToList()
        }
    };

    [Fact]
    public void TwoWeakRoutesFailingAtDifferentNarrators_UpgradeToHasanLighayrih()
    {
        var tree = TreeOf((7, Guid.NewGuid()), (7, Guid.NewGuid()));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("حسن لغيره", tree.CalculatedGrade);
    }

    [Fact]
    public void TwoWeakRoutesSharingTheSameWeakNarrator_DoNotReinforceEachOther()
    {
        var shared = Guid.NewGuid();
        var tree = TreeOf((7, shared), (7, shared));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("ضعيف", tree.CalculatedGrade);
    }

    [Fact]
    public void FabricatorRoutes_AreNotUpgraded()
    {
        var tree = TreeOf((12, Guid.NewGuid()), (12, Guid.NewGuid()));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("موضوع / متروك", tree.CalculatedGrade);
    }

    [Fact]
    public void EveryRouteThroughASaduqMadar_IsHasanNotSahih()
    {
        // محمد بن عمرو بن علقمة (صدوق, tier 4) is on every route.
        var madar = Guid.NewGuid();
        var tree = TreeOf((4, madar), (4, madar), (4, madar));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("حسن", tree.CalculatedGrade);
    }

    [Fact]
    public void OneFullyReliableRoute_MakesTheHadithSahih()
    {
        var tree = TreeOf((4, Guid.NewGuid()), (3, Guid.NewGuid()));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("صحيح", tree.CalculatedGrade);
    }

    [Fact]
    public void NoResolvedRoutes_IsUnknown()
    {
        var tree = TreeOf((null, null));

        new TaqwiyahService().CalculateTreeStrength(tree);

        Assert.Equal("مجهول", tree.CalculatedGrade);
    }
}
