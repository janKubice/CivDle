using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Svět po založení nesmí být prázdná pláň. Jeden domek u startu je první věta,
/// kterou hra hráči řekne — „tohle tady stavíš".
///
/// <para>Testuje se to, co by z toho udělalo problém: budova musí stát zadarmo
/// (hráč na startu nic nemá), musí se opravdu objevit na mapě, a když se nikam
/// nevejde, hra kvůli tomu nesmí spadnout.</para>
/// </summary>
public class StartingBuildingsTests
{
    private const int Hut = 0;

    /// <summary>Chalupa s bydlením, ať jde ověřit i to, že se počítá do kapacity.</summary>
    private static GameContent Content(params int[] startingBuildings)
    {
        var gameplay = TestContent.DefaultGameplay with
        {
            StartingBuildingIndices = startingBuildings,
        };

        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        return TestContent.Build(
            biomes,
            buildings: new[] { TestContent.SimpleBuilding("hut", biomes.Length, housing: 9) },
            gameplay: gameplay);
    }

    /// <summary>Svět, kde se stavět nedá: budova povolená jen na souši, kolem samá voda.</summary>
    private static GameContent WaterOnlyContent(params int[] startingBuildings)
    {
        var gameplay = TestContent.DefaultGameplay with
        {
            StartingBuildingIndices = startingBuildings,
        };

        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var landOnly = TestContent.SimpleBuilding("hut", biomes.Length, housing: 9) with
        {
            AllowedBiomes = new[] { false, true },
        };

        return TestContent.Build(biomes, buildings: new[] { landOnly }, gameplay: gameplay);
    }

    [Fact]
    public void ANewWorldIsNotAnEmptyPlain()
    {
        var sim = new Simulation(Content(Hut), new UniformTerrain(1));

        Assert.Single(sim.Buildings.ToArray());
        Assert.Equal(Hut, sim.Buildings[0].DefIndex);
    }

    [Fact]
    public void TheStartingHouseIsFree()
    {
        // Na startu hráč nemá čím platit — kdyby se cena strhla, začal by
        // v mínusu, nebo (hůř) by domek tiše nevznikl.
        var empty = new Simulation(Content(), new UniformTerrain(1));
        var withHouse = new Simulation(Content(Hut), new UniformTerrain(1));

        for (int i = 0; i < empty.ResourceCount; i++)
        {
            Assert.Equal(empty.GetResource(i), withHouse.GetResource(i), 3);
        }
    }

    [Fact]
    public void TheHouseStandsNearTheStart()
    {
        // Domek na druhém konci mapy by hráč nikdy nenašel.
        var sim = new Simulation(Content(Hut), new UniformTerrain(1));

        var building = sim.Buildings[0];
        Assert.InRange(building.X, -24, 24);
        Assert.InRange(building.Y, -24, 24);
    }

    [Fact]
    public void ItCountsTowardsHousingLikeAnyOtherHouse()
    {
        var empty = new Simulation(Content(), new UniformTerrain(1));
        var withHouse = new Simulation(Content(Hut), new UniformTerrain(1));

        Assert.True(withHouse.HousingCapacity > empty.HousingCapacity);
    }

    [Fact]
    public void NoStartingBuildingsIsAValidWorld()
    {
        // Prázdný seznam je legitimní volba dat (a stav všech ostatních testů).
        var sim = new Simulation(Content(), new UniformTerrain(1));

        Assert.Empty(sim.Buildings.ToArray());
    }

    [Fact]
    public void AWorldWithNowhereToBuildStillStarts()
    {
        // Samá voda: domek se nemá kam postavit. Hra musí naběhnout i tak —
        // spadnout kvůli dekoraci by bylo nepřiměřené.
        var sim = new Simulation(WaterOnlyContent(Hut), new UniformTerrain(0));

        Assert.Empty(sim.Buildings.ToArray());
    }
}
