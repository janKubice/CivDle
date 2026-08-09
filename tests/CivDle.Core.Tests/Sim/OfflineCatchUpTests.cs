using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Dohánění offline času po dávkách.
///
/// <para>Hráč to popsal takhle: „dám Continue, ukazatel dojede na konec, hra
/// pořád čeká a pak spadne; když při tom kliknu, zamrzne to". Dohon se počítal
/// jedním cyklem v konstruktoru herní obrazovky — dvanáct hodin je 432 000 tiků
/// a s bonusem Vzestupu mnohonásobek. Po tu dobu okno nepřekreslovalo ani
/// nereagovalo, takže ho systém označil za mrtvé.</para>
///
/// <para>Testuje se to, na čem ta oprava stojí: dá se posouvat po kouscích,
/// dá se přerušit, a čísla po přerušení nelžou.</para>
/// </summary>
public class OfflineCatchUpTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(120, 90, 60), StartAmount: 100, BaseStorage: 1_000_000),
    };

    private static GameContent Content()
    {
        var camp = new BuildingDef(
            "camp", "production", new RgbColor(90, 120, 70), 1, 1,
            WorkerSlots: 2, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(0, 1) }, 10),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Wood,
            buildings: new[] { camp });
    }

    private static Simulation World(GameContent content)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryPlaceBuilding(0, 2, 2);
        return sim;
    }

    private static OfflineCatchUp CatchUp(Simulation sim, int minutesAway)
    {
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        return new OfflineCatchUp(sim, now.AddMinutes(-minutesAway), now);
    }

    [Fact]
    public void ItAdvancesInSlicesInsteadOfOneLongLoop()
    {
        var sim = World(Content());
        var catchUp = CatchUp(sim, 10);
        Assert.True(catchUp.TotalTicks > 0);

        catchUp.Advance(100);

        Assert.Equal(100, catchUp.DoneTicks);
        Assert.False(catchUp.IsDone);
        Assert.InRange(catchUp.Progress, 0.0, 1.0);
    }

    [Fact]
    public void AdvancingPastTheEndIsHarmless()
    {
        var sim = World(Content());
        var catchUp = CatchUp(sim, 10);

        catchUp.Advance(catchUp.TotalTicks * 10);

        Assert.True(catchUp.IsDone);
        Assert.Equal(catchUp.TotalTicks, catchUp.DoneTicks);
        Assert.Equal(1.0, catchUp.Progress, 6);
    }

    [Fact]
    public void SkippingStopsTheWorkImmediately()
    {
        var sim = World(Content());
        var catchUp = CatchUp(sim, 60);
        catchUp.Advance(50);

        catchUp.Skip();
        catchUp.Advance(10_000);

        Assert.True(catchUp.IsDone);
        Assert.True(catchUp.WasSkipped);
        Assert.Equal(50, catchUp.DoneTicks);
    }

    [Fact]
    public void ASkippedCatchUpDoesNotPromiseTimeItNeverSimulated()
    {
        // Hráč si nechá to, co se spočítalo — ale souhrn mu nesmí tvrdit,
        // že započítal celou hodinu, když se odtikala setina.
        var sim = World(Content());
        var catchUp = CatchUp(sim, 60);
        catchUp.Advance(catchUp.TotalTicks / 100);
        catchUp.Skip();

        var summary = catchUp.Finish();

        Assert.True(summary.CreditedSeconds < catchUp.CreditedSeconds,
            $"po přeskočení se hlásí {summary.CreditedSeconds} s z {catchUp.CreditedSeconds} s");
        Assert.Equal(3600, summary.ElapsedSeconds); // skutečně uplynulý čas se nemění
    }

    [Fact]
    public void TheWholeCatchUpGivesTheSameGainsAsTheOneShotHelper()
    {
        // Dávkování nesmí měnit výsledek — jen to, kdy se počítá.
        var content = Content();
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

        var sliced = World(content);
        var catchUp = new OfflineCatchUp(sliced, now.AddMinutes(-10), now);
        while (!catchUp.IsDone)
        {
            catchUp.Advance(37); // schválně nerovná dávka
        }

        var atOnce = World(content);
        var expected = OfflineProgress.Apply(atOnce, now.AddMinutes(-10), now);
        var actual = catchUp.Finish();

        Assert.Equal(expected.CreditedSeconds, actual.CreditedSeconds);
        Assert.Equal(expected.ResourceGains, actual.ResourceGains);
        Assert.Equal(expected.PopulationGain, actual.PopulationGain, 6);
    }

    [Fact]
    public void EvenAHugeBonusCannotMakeItRunForever()
    {
        // Strop na čase nestačí: bonus Vzestupu počet tiků násobí.
        var sim = World(Content());
        var catchUp = CatchUp(sim, 60 * 24 * 30);

        Assert.InRange(catchUp.TotalTicks, 1, OfflineCatchUp.MaxTicks);
    }

    [Fact]
    public void ComingBackImmediatelyHasNothingToCatchUp()
    {
        var sim = World(Content());
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var catchUp = new OfflineCatchUp(sim, now, now);

        Assert.Equal(0, catchUp.TotalTicks);
        Assert.True(catchUp.IsDone);
        Assert.False(catchUp.Finish().Worthwhile);
    }
}
