using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Jízda karavany: jede po silnicích k městu, výplata roste s délkou trasy
/// i s doprovodem, a ve slepé uličce se zastaví. Běží headless — <see cref="CaravanRun"/>
/// nesahá na grafiku, právě proto je oddělená od systému, který ji kreslí.
/// </summary>
public sealed class CaravanRunTests
{
    private static Simulation RoadWorld(out GameContent content, params (int X, int Y)[] roads)
    {
        content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
        foreach (var (x, y) in roads)
        {
            sim.AddRoadTileForTest(x, y);
        }

        return sim;
    }

    [Fact]
    public void CaravanWalksTheRoadTowardsTheCity()
    {
        // Rovná ulice od města doprava; karavana startuje na jejím konci.
        var sim = RoadWorld(out _, (1, 0), (2, 0), (3, 0), (4, 0));
        var run = new CaravanRun(4, 0);

        Assert.True(run.TryStepTowardsCity(sim));
        Assert.Equal(3, run.TileX);
        Assert.True(run.TryStepTowardsCity(sim));
        Assert.Equal(2, run.TileX);
        Assert.Equal(2, run.TilesTravelled);
    }

    [Fact]
    public void DeadEnd_StopsTheRun()
    {
        // Osamocená dlaždice: není kam pokračovat.
        var sim = RoadWorld(out _, (9, 9));
        var run = new CaravanRun(9, 9);

        Assert.False(run.TryStepTowardsCity(sim));
        Assert.Equal(0, run.TilesTravelled);
    }

    [Fact]
    public void CaravanNeverLeavesTheRoad()
    {
        // Vedle karavany není žádná silnice, jen volné pole — nesmí se hnout.
        var sim = RoadWorld(out _, (6, 6));
        var run = new CaravanRun(6, 6);

        Assert.False(run.TryStepTowardsCity(sim));
    }

    [Fact]
    public void ArrivalIsDetectedAtTheCityCentre()
    {
        var sim = RoadWorld(out _, (1, 0), (2, 0));

        Assert.True(new CaravanRun(sim.CityCenterX, sim.CityCenterY).HasArrived(sim));
        Assert.False(new CaravanRun(sim.CityCenterX + 9, sim.CityCenterY).HasArrived(sim));
    }

    [Fact]
    public void LongerRoute_PaysMore()
    {
        var sim = RoadWorld(out _, (1, 0), (2, 0), (3, 0), (4, 0), (5, 0));
        var shortRun = new CaravanRun(2, 0);
        var longRun = new CaravanRun(5, 0);

        while (shortRun.TryStepTowardsCity(sim)) { }
        while (longRun.TryStepTowardsCity(sim)) { }

        Assert.True(longRun.Payout() > shortRun.Payout(),
            $"delší trasa musí platit víc ({longRun.Payout()} vs {shortRun.Payout()})");
    }

    [Fact]
    public void EscortingRaisesThePayout()
    {
        var sim = RoadWorld(out _, (1, 0), (2, 0), (3, 0));
        var plain = new CaravanRun(3, 0);
        var escorted = new CaravanRun(3, 0);

        while (plain.TryStepTowardsCity(sim)) { }
        while (escorted.TryStepTowardsCity(sim)) { }
        for (int i = 0; i < 4; i++)
        {
            escorted.Escort();
        }

        Assert.True(escorted.Payout() > plain.Payout());
    }

    [Fact]
    public void EscortClicks_AreCapped()
    {
        var run = new CaravanRun(0, 0);
        for (int i = 0; i < 100; i++)
        {
            run.Escort();
        }

        Assert.Equal(CaravanRun.MaxEscortClicks, run.EscortClicks);
    }

    [Fact]
    public void CaravanThatWentNowhere_PaysNothing()
    {
        Assert.Equal(0, new CaravanRun(3, 3).Payout());
    }

    [Fact]
    public void CaravanBringsWhateverIsScarcest()
    {
        var sim = RoadWorld(out var content, (1, 0));
        int wood = content.Resources.IndexOf("wood");

        // Naplň všechno kromě dřeva — karavana má přivézt právě to.
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            if (i != wood && sim.IsResourceKnown(i))
            {
                sim.AddResource(i, sim.GetStorageCap(i));
            }
        }

        Assert.Equal(wood, CaravanRun.ScarcestKnownResource(sim));
    }
}
