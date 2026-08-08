using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vylepšení, které budovu <b>zvětší</b>.
///
/// <para>Vylepšení dlouho jen vyměnilo definici na místě, protože všechny stupně
/// měly stejný půdorys. Jakmile ho ale nejvyšší stupeň bydlení zvětší (arkologie
/// 3×3), musí se nové dlaždice zabrat — jinak by budova stála na políčkách,
/// o kterých mapa obsazenosti neví, a dalo by se do ní stavět.</para>
/// </summary>
public class UpgradeFootprintTests
{
    private const int Small = 0;
    private const int Big = 1;

    /// <summary>Chatrč 1×1, která se vylepší na věž 2×2.</summary>
    private static GameContent GrowingContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 10_000),
        };

        var small = TestContent.SimpleBuilding("hut", biomes.Length, housing: 4,
            upgradesToIndex: Big, upgradeCost: new[] { new ResourceAmount(0, 10) });
        var big = TestContent.SimpleBuilding("tower", biomes.Length, housing: 20, buildable: false)
            with { FootprintWidth = 2, FootprintHeight = 2 };

        return TestContent.Build(biomes, 1, resources, new[] { small, big });
    }

    private static Simulation NewSim() => new(GrowingContent(), new UniformTerrain(1));

    [Fact]
    public void UpgradeClaimsTheTilesItGrowsInto()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Small, 5, 5));

        Assert.Equal(PlacementResult.Ok, sim.TryUpgradeBuilding(0));

        // Všechny čtyři dlaždice teď patří té jedné budově.
        foreach (var (x, y) in new[] { (5, 5), (6, 5), (5, 6), (6, 6) })
        {
            Assert.True(sim.TryGetBuildingAt(x, y, out int index), $"dlaždice {x},{y} má patřit věži");
            Assert.Equal(0, index);
        }
    }

    [Fact]
    public void UpgradeIsRefusedWhenThereIsNoRoom()
    {
        // Soused stojí v cestě růstu. Vylepšit nejde — a hlavně se nesmí
        // stát napůl: budova zůstane tím, čím byla.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Small, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Small, 6, 5));

        Assert.Equal(PlacementResult.Occupied, sim.CanUpgrade(0));
        Assert.Equal(PlacementResult.Occupied, sim.TryUpgradeBuilding(0));
        Assert.Equal(Small, sim.Buildings[0].DefIndex);
    }

    [Fact]
    public void RealContentGrowsTheTopHousingTier()
    {
        // Nejvyšší stupeň bydlení má vypadat jako mrakodrap, ne jako chalupa.
        var content = TestData.LoadRealContent();
        var arcology = content.Buildings[content.Buildings.IndexOf("arcology")];

        Assert.Equal(3, arcology.FootprintWidth);
        Assert.Equal(3, arcology.FootprintHeight);
    }

    [Fact]
    public void MergedHousesCanBeUpgradedFurther()
    {
        // Sloučený dům býval slepá ulička: 2×2 a konec. Teď má vlastní řetězec
        // — a zůstává 2×2, roste jen kapacita.
        var content = TestData.LoadRealContent();
        var manor = content.Buildings[content.Buildings.IndexOf("manor")];

        Assert.True(manor.HasUpgrade, "sloučený dům musí jít vylepšovat dál");

        var next = content.Buildings[manor.UpgradesToIndex];
        Assert.Equal(manor.FootprintWidth, next.FootprintWidth);
        Assert.Equal(manor.FootprintHeight, next.FootprintHeight);
        Assert.True(next.HousingCapacity > manor.HousingCapacity);
    }
}
