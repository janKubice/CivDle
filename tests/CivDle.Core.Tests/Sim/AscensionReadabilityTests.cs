using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Čitelný Vzestup: hráč má vědět, co jediná nevratná akce ve hře udělá — dřív,
/// než klikne — a po ní má vidět, kam kapitola došla.
///
/// <para>Testuje se hlavně to, na čem stojí důvěra v ta čísla: náhled musí
/// popisovat SKUTEČNÝ stav (ne odhad), bilance se musí sebrat ještě před
/// resetem éry, a rekord se smí přepsat jen tehdy, když ho běh opravdu
/// překonal.</para>
/// </summary>
public class AscensionReadabilityTests
{
    // Vzestup dostupný hned (podmínka = startovní populace), body = populace ÷ 5.
    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    private static GameContent Content() => TestContent.Build(prestige: EarlyAscension);

    private static Simulation NewSim(GameContent? content = null) =>
        new(content ?? Content(), new UniformTerrain(1));

    // ----- náhled -----

    [Fact]
    public void PreviewCountsWhatIsActuallyOnTheMap()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 6, 6));

        var preview = sim.PreviewAscension();

        Assert.Equal(2, preview.Buildings);
        Assert.Equal((long)sim.Population, preview.Population);
        Assert.Equal(sim.RoadTiles.Count, preview.RoadTiles);
    }

    [Fact]
    public void PreviewAgreesWithWhatAscendingActuallyGives()
    {
        // Kdyby se náhled a skutečnost rozešly, byla by ta obrazovka horší než nic.
        var sim = NewSim();
        var preview = sim.PreviewAscension();

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(preview.PointsAfter, sim.PrestigePoints);
        Assert.Equal(preview.LevelAfter, sim.AscensionLevel);
        Assert.Equal(preview.NextRequirement, sim.AscensionRequirement());
    }

    [Fact]
    public void PreviewOnAnEmptyMapPromisesNoLoss()
    {
        // První Vzestup z holé mapy není ztráta — planý strašák by hráče jen mátl.
        var sim = NewSim();

        Assert.False(sim.PreviewAscension().LosesAnything);
    }

    [Fact]
    public void PreviewOnABuiltCityWarnsAboutTheLoss()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));

        Assert.True(sim.PreviewAscension().LosesAnything);
    }

    [Fact]
    public void PreviewKeepsCountOfPermanentUpgrades()
    {
        var content = TestContent.Build(
            prestige: EarlyAscension,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef("industrious", "production_mult", 0.30, 1, Array.Empty<int>()),
            });
        var sim = NewSim(content);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));

        Assert.Equal(1, sim.PreviewAscension().UpgradesOwned);
    }

    // ----- bilance běhu -----

    [Fact]
    public void ThereIsNoSummaryBeforeTheFirstAscension()
    {
        Assert.False(NewSim().LastRun.Exists);
    }

    [Fact]
    public void TheSummaryIsTakenBeforeTheEraIsWiped()
    {
        // Kdyby se bilance sbírala až po resetu, hlásila by samé nuly.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 6, 6));
        for (int i = 0; i < 40; i++)
        {
            sim.Tick();
        }

        long ticks = sim.TickCount;
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        var run = sim.LastRun;
        Assert.True(run.Exists);
        Assert.Equal(1, run.Level);
        Assert.Equal(2, run.Buildings);
        Assert.Equal(ticks, run.DurationTicks);
        Assert.True(run.PeakPopulation > 0);
        Assert.Equal(0, sim.Buildings.Length); // éra je pryč, bilance zůstala
    }

    [Fact]
    public void TheSummaryReportsThePointsItEarned()
    {
        var sim = NewSim();
        long expected = sim.PendingAscensionPoints();

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(expected, sim.LastRun.PointsEarned);
    }

    [Fact]
    public void TheFirstRunHasNothingToBeat()
    {
        var sim = NewSim();

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(0, sim.LastRun.PreviousBestPopulation);
        Assert.True(sim.LastRun.IsBestPopulation);
    }

    [Fact]
    public void AWeakerRunDoesNotOverwriteTheRecord()
    {
        // „Dál než minule" je celý motor opakovaného hraní — rekord se nesmí
        // přepsat během, který ho nepřekonal.
        var sim = NewSim();
        Grow(sim, 200);
        long firstPeak = sim.PeakPopulation;
        Assert.True(firstPeak > 0);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(PlacementResult.Ok, sim.TryAscend()); // hned podruhé, bez růstu

        Assert.False(sim.LastRun.IsBestPopulation);
        Assert.Equal(firstPeak, sim.LastRun.PreviousBestPopulation);
        Assert.Equal(firstPeak, sim.BestRunPopulation);
    }

    [Fact]
    public void ThePeakBelongsToTheRunNotToThePlayer()
    {
        var sim = NewSim();
        Grow(sim, 200);
        Assert.True(sim.PeakPopulation > 0);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(0, sim.PeakPopulation);       // nová kapitola začíná od nuly
        Assert.True(sim.BestRunPopulation > 0);    // rekord přetrvává
    }

    [Fact]
    public void TheRecordSurvivesSaveAndLoad()
    {
        var sim = NewSim();
        Grow(sim, 200);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        long best = sim.BestRunPopulation;
        Assert.True(best > 0);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, Content());

        Assert.Equal(best, loaded.BestRunPopulation);
    }

    /// <summary>Nechá město chvíli růst, ať má běh co vykázat.</summary>
    private static void Grow(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
