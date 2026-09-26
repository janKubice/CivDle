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
    // ----- odhadovaná část: dlouhá nepřítomnost -----

    /// <summary>Pila bez dřevorubce: dřevo jen ubývá, prkna jen z toho, co je na skladě.</summary>
    private static GameContent SawmillContent(double woodCap = 1_000_000)
    {
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 300, BaseStorage: woodCap),
            new Resource("planks", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000),
        };

        var saw = TestContent.Converter("saw", 0, 3, 1, 1, timeTicks: 10, biomeCount: 2, workerSlots: 2);
        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: resources,
            buildings: new[] { saw });
    }

    private static OfflineCatchUp Run(Simulation sim, int hoursAway)
    {
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var catchUp = new OfflineCatchUp(sim, now.AddHours(-hoursAway), now);
        catchUp.Advance(long.MaxValue);
        return catchUp;
    }

    [Fact]
    public void ALongAbsenceDoesBoundedWorkNoMatterHowLong()
    {
        // Tohle je ta chyba: dvanáct hodin se dřív tikalo tik po tiku a u velkého
        // města to trvalo hodiny. Práce teď nesmí růst s délkou nepřítomnosti.
        var sim = World(Content());
        var catchUp = Run(sim, 12);

        Assert.True(catchUp.IsEstimated);
        Assert.True(catchUp.TotalWork < 60_000, $"práce {catchUp.TotalWork} kroků");
        Assert.Equal(catchUp.TotalTicks, catchUp.DoneTicks); // přeskočený čas se započítá celý
        Assert.Equal(OfflineProgress.MaxCreditedSeconds, catchUp.Finish().CreditedSeconds);
    }

    [Fact]
    public void ABiggerCityGetsFewerPreciseTicks()
    {
        Assert.True(OfflineCatchUp.PreciseTicksFor(24_000) < OfflineCatchUp.PreciseTicksFor(1_500));
        Assert.Equal(OfflineCatchUp.MaxPreciseTicks, OfflineCatchUp.PreciseTicksFor(10));
        Assert.Equal(OfflineCatchUp.MinPreciseTicks, OfflineCatchUp.PreciseTicksFor(10_000_000));
    }

    [Fact]
    public void TheEstimateCreditsWhatTheCityReallyProduces()
    {
        // Odhad musí sedět na to, co by vyrobilo poctivé tikání — jinak by se
        // vyplatilo hru zavřít (nebo nevyplatilo).
        var content = Content();
        var estimated = World(content);
        double before = estimated.GetResource(0);
        Run(estimated, 3);

        var exact = World(content);
        for (long t = 0; t < 3 * 3600 * (long)Simulation.TicksPerSecond; t++)
        {
            exact.Tick();
        }

        double expected = exact.GetResource(0) - before;
        double actual = estimated.GetResource(0) - before;
        Assert.True(Math.Abs(actual - expected) <= expected * 0.05,
            $"odhad {actual:0} dřeva, poctivě {expected:0}");
        Assert.Equal(exact.TickCount, estimated.TickCount); // hodiny doběhly stejně daleko
    }

    [Fact]
    public void AnInputThatRunsOutStopsTheOutput()
    {
        // Pila bez dřevorubce spotřebuje sklad a stojí. Odhad nesmí připisovat
        // prkna z dřeva, které už není — skok skončí, když dřevo dojde.
        var sim = World(SawmillContent());
        sim.TryPlaceBuilding(0, 4, 4);

        Run(sim, 12);

        double planks = sim.GetResource(1);
        Assert.InRange(planks, 90, 100 + 20); // 300 dřeva / 3 = 100 prken (+ nejvýš minuta skoku navíc)
        Assert.Equal(0, sim.GetResource(0), 6);
    }

    [Fact]
    public void NothingIsCreditedBeyondTheStorage()
    {
        var sim = World(Content());
        var cap = sim.GetStorageCap(0);
        Run(sim, 12);

        Assert.True(sim.GetResource(0) <= cap);
    }

    [Fact]
    public void TheEstimateIsDeterministic()
    {
        // Rozpočet se odvozuje z počtu budov, ne z hodin — stejný sav musí dát
        // stejný výsledek na každém počítači.
        var content = Content();
        var a = World(content);
        var b = World(content);

        var summaryA = Run(a, 12).Finish();
        var summaryB = Run(b, 12).Finish();

        Assert.Equal(summaryA.ResourceGains, summaryB.ResourceGains);
        Assert.Equal(a.TickCount, b.TickCount);
    }

    [Fact]
    public void StepsStayShortSoTheWindowCanDraw()
    {
        // Jeden krok je přesný tik, skok, nebo jedno kolo guvernéra — nikdy
        // celá hodina naráz. Načítací obrazovka pak hlídá čas po každém kroku.
        var sim = World(Content());
        var now = new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
        var catchUp = new OfflineCatchUp(sim, now.AddHours(-12), now);

        long steps = 0;
        while (!catchUp.IsDone)
        {
            long ticksBefore = catchUp.DoneTicks;
            catchUp.Advance(1);
            steps++;
            Assert.True(catchUp.Progress >= 0 && catchUp.Progress <= 1);
            _ = ticksBefore;
        }

        Assert.True(steps < 60_000);
        Assert.Equal(1.0, catchUp.Progress, 6);
    }
    [Fact]
    public void RealContent_TheCityKeepsGrowingWhileItIsEstimated()
    {
        // Odhad nesmí z města udělat skanzen: guvernér v něm odehraje svá kola
        // za celý přeskočený čas, takže po návratu stojí víc domů a víc lidí.
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 20260728), 20260728);
        var start = CivDle.Core.Sim.StartSiteFinder.Find(sim);
        foreach (string id in new[] { "house", "farm", "lumber_camp", "house" })
        {
            PlaceNear(sim, content.Buildings.IndexOf(id), start.X, start.Y);
        }

        for (int i = 0; i < 600; i++)
        {
            sim.Tick();
        }

        var catchUp = Run(sim, 6);
        var summary = catchUp.Finish();

        Assert.True(catchUp.IsEstimated);
        Assert.True(summary.BuildingsGain > 0, "za šest hodin guvernér nic nepostavil");
        Assert.True(summary.PopulationGain > 0, "za šest hodin nepřibyl nikdo");
        Assert.True(sim.Population <= Math.Max(sim.HousingCapacity, 1) + 1e-6);
    }

    private static void PlaceNear(Simulation sim, int defIndex, int x, int y)
    {
        for (int r = 1; r < 30; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r
                        && sim.TryPlaceBuildingFree(defIndex, x + dx, y + dy) == PlacementResult.Ok)
                    {
                        return;
                    }
                }
            }
        }
    }
}
