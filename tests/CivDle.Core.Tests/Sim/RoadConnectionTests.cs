using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Silnice jako infrastruktura, ne dekorace: budova bez napojení na síť vyrábí
/// pomaleji. Testuje se jak samotné rozpoznání napojení (včetně velkých půdorysů),
/// tak dopad na výrobu — a taky to, že se dá vypnout daty.
/// </summary>
public sealed class RoadConnectionTests
{
    [Fact]
    public void BuildingNextToARoad_CountsAsConnected()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));
        sim.AddRoadTileForTest(40, 40); // síť někde existuje, ale ne u téhle budovy

        Assert.False(sim.IsBuildingConnected(0), "Vzdálená cesta není napojení.");

        sim.AddRoadTileForTest(4, 5); // dlaždice vlevo od budovy
        Assert.True(sim.IsBuildingConnected(0), "Cesta u zdi znamená napojení.");
    }

    [Fact]
    public void DiagonalRoad_DoesNotCount()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));
        sim.AddRoadTileForTest(40, 40); // ať mechanika vůbec platí

        sim.AddRoadTileForTest(4, 4); // jen roh

        Assert.False(sim.IsBuildingConnected(0), "Roh není napojení — zboží se po úhlopříčce nevozí.");
    }

    /// <summary>
    /// První budova nemá k čemu se napojit — dokud ve městě není žádná cesta,
    /// nesmí mechanika hráče trestat za něco, co ještě nemohl ovlivnit.
    /// </summary>
    [Fact]
    public void BeforeAnyRoadExists_EveryoneCountsAsConnected()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));

        Assert.True(sim.IsBuildingConnected(0));
    }

    [Fact]
    public void DisconnectedBuilding_ProducesSlower()
    {
        double connected = OutputAfter(withRoad: true);
        double alone = OutputAfter(withRoad: false);

        Assert.True(alone < connected,
            $"Napojení nemá vliv na výrobu (bez cesty {alone}, s cestou {connected}).");
    }

    /// <summary>Výchozí (nezadaná) hodnota nechá silnice bez mechanického dopadu.</summary>
    [Fact]
    public void WithoutTheSetting_RoadsDoNotAffectProduction()
    {
        double connected = OutputAfter(withRoad: true, disconnectedMult: 1.0);
        double alone = OutputAfter(withRoad: false, disconnectedMult: 1.0);

        Assert.Equal(connected, alone, 6);
    }

    [Fact]
    public void RealContent_TurnsRoadsIntoInfrastructure()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Gameplay.Roads.DisconnectedProductionMult < 1.0,
            "Ostrý obsah má mít silnice jako mechaniku, ne jako čáru na mapě.");
    }

    /// <summary>Odtiká výrobu jedné budovy a vrátí, kolik vyrobila.</summary>
    private static double OutputAfter(bool withRoad, double disconnectedMult = 0.5)
    {
        var content = ProducerContent(disconnectedMult);
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));

        // Síť musí existovat, jinak platí úleva pro úplný začátek hry.
        sim.AddRoadTileForTest(40, 40);
        if (withRoad)
        {
            sim.AddRoadTileForTest(4, 5);
        }

        for (int i = 0; i < 400; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(0);
    }

    private static Simulation NewSim() => new(ProducerContent(0.5), new UniformTerrain(1));

    /// <summary>Obsah s jednou budovou, která z ničeho vyrábí surovinu 0.</summary>
    private static GameContent ProducerContent(double disconnectedMult)
    {
        var gameplay = TestContent.DefaultGameplay;
        return TestContent.Build(
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 100000) },
            buildings: new[] { TestContent.Producer("hut", outputResource: 0, amount: 1, timeTicks: 10) },
            gameplay: gameplay with
            {
                Roads = gameplay.Roads with { DisconnectedProductionMult = disconnectedMult },
            });
    }
}
