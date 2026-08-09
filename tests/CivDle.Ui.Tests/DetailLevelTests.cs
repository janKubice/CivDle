using CivDle.Core.Config;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Prahy detailu (LOD) podle volby hráče.
///
/// <para>Volba nesmí jen tak „něco" změnit: pořadí vrstev musí zůstat
/// zachované (dekorace se vzdají dřív než budovy), jinak by na jednom stupni
/// zmizely domy a zůstaly kytky. A hlavně se musí dát vrátit zpátky — testy
/// běží ve sdíleném procesu a statické prahy jsou globální stav.</para>
/// </summary>
[Collection("DetailLevel")]
public class DetailLevelTests : IDisposable
{
    public void Dispose() => DetailLevel.Apply(DetailQuality.Balanced);

    [Fact]
    public void Balanced_KeepsTheTunedThresholds()
    {
        DetailLevel.Apply(DetailQuality.Balanced);

        Assert.Equal(DetailLevel.BaseDecorations, DetailLevel.Decorations);
        Assert.Equal(DetailLevel.BaseHarvestables, DetailLevel.Harvestables);
        Assert.Equal(DetailLevel.BaseBuildingSprites, DetailLevel.BuildingSprites);
        Assert.Equal(DetailLevel.BaseCreatures, DetailLevel.Creatures);
        Assert.Equal(DetailLevel.BaseMaxPerTileWork, DetailLevel.MaxPerTileWork);
    }

    [Fact]
    public void Performance_GivesUpSoonerThanBalanced()
    {
        DetailLevel.Apply(DetailQuality.Performance);

        // Vyšší práh = musíš být blíž, aby se vrstva ještě kreslila.
        Assert.True(DetailLevel.Decorations > DetailLevel.BaseDecorations);
        Assert.True(DetailLevel.Creatures > DetailLevel.BaseCreatures);
    }

    [Fact]
    public void Maximum_HoldsDetailFurtherOut()
    {
        DetailLevel.Apply(DetailQuality.Maximum);

        Assert.True(DetailLevel.Decorations < DetailLevel.BaseDecorations);
        Assert.True(DetailLevel.Creatures < DetailLevel.BaseCreatures);
        Assert.True(DetailLevel.MaxPerTileWork > DetailLevel.BaseMaxPerTileWork);
    }

    [Theory]
    [InlineData(DetailQuality.Performance)]
    [InlineData(DetailQuality.Balanced)]
    [InlineData(DetailQuality.Detailed)]
    [InlineData(DetailQuality.Maximum)]
    public void LayerOrderSurvivesEveryStep(DetailQuality quality)
    {
        // Dekorace jsou nejmenší, takže musí mizet první; budovy poslední.
        DetailLevel.Apply(quality);

        Assert.True(DetailLevel.Decorations > DetailLevel.BuildingSprites);
        Assert.True(DetailLevel.BuildingSprites > DetailLevel.Harvestables);
        Assert.True(DetailLevel.Harvestables > DetailLevel.Creatures);
    }

    [Fact]
    public void StepsAreMonotonic()
    {
        // Každý vyšší stupeň smí prahy jen snížit — jinak by „Detailní" na
        // některé vrstvě kreslilo míň než „Vyvážené".
        DetailQuality[] steps =
        {
            DetailQuality.Performance,
            DetailQuality.Balanced,
            DetailQuality.Detailed,
            DetailQuality.Maximum,
        };

        for (int i = 1; i < steps.Length; i++)
        {
            Assert.True(DetailLevel.FactorFor(steps[i]) < DetailLevel.FactorFor(steps[i - 1]),
                $"{steps[i]} musí mít nižší násobič než {steps[i - 1]}");
            Assert.True(DetailLevel.BudgetFor(steps[i]) > DetailLevel.BudgetFor(steps[i - 1]),
                $"{steps[i]} musí mít vyšší rozpočet než {steps[i - 1]}");
        }
    }

    [Fact]
    public void Scale_MovesOwnThresholdsToo()
    {
        // Vrstvy živého města mají vlastní vzdálenosti (auta vydrží dál než
        // chodci); násobič jim ten odstup musí zachovat.
        DetailLevel.Apply(DetailQuality.Performance);
        float people = DetailLevel.Scale(0.6f);
        float cars = DetailLevel.Scale(0.5f);

        Assert.True(people > 0.6f);
        Assert.True(cars < people);
    }

    [Fact]
    public void Budget_ChecksTheRectangle()
    {
        DetailLevel.Apply(DetailQuality.Balanced);

        Assert.True(DetailLevel.FitsBudget(0, 0, 49, 49));
        Assert.False(DetailLevel.FitsBudget(0, 0, 999, 999));
        Assert.False(DetailLevel.FitsBudget(10, 10, 0, 0)); // prázdný obdélník
    }
}
