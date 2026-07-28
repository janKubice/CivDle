using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Roční období: čtyřtaktní rytmus, kvůli kterému není každá minuta idle hry
/// stejná jako ta předchozí.
///
/// <para>Období je čistá funkce čísla dne — nic se neukládá, takže testy stačí
/// odtikat na správný den a ověřit, co se změnilo.</para>
/// </summary>
public class SeasonTests
{
    private const int Food = 0;
    private const int Wood = 1;

    private static SeasonDef Season(
        string id,
        double food = 1.0,
        double harvest = 1.0,
        double growth = 1.0,
        double fuel = 0.0,
        double coldGrowth = 1.0) =>
        new(id, new RgbColor(10, 20, 30), 0.1, food, harvest, growth, fuel, coldGrowth);

    /// <summary>Kalendář o dvou obdobích po jednom dni — v testu se pak rychle střídají.</summary>
    private static SeasonCalendar Calendar(params SeasonDef[] seasons) =>
        new(seasons, DaysPerSeason: 1, FuelResourceIndex: Wood);

    private static GameContent Content(SeasonCalendar? calendar = null, double growthPerSecond = 0)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass") with { ClickYield = new ClickYield(Wood, 10) },
        };
        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 1_000_000),
            new Resource("wood", new RgbColor(2, 2, 2), StartAmount: 500, BaseStorage: 1_000_000),
        };

        var farm = new BuildingDef(
            "farm", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(Food, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var sawmill = farm with
        {
            Id = "sawmill",
            Recipe = new Recipe(
                Array.Empty<ResourceAmount>(),
                new[] { new ResourceAmount(Wood, 10) },
                TimeTicks: 1),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = growthPerSecond,
            BaseHousingCapacity = 100_000,
            // Krátký den, ať se v testu dá dotikat do dalšího období.
            DayNight = TestContent.DefaultGameplay.DayNight with { DayLengthSeconds = 10, StartTimeOfDay = 0 },
        };

        return TestContent.Build(
            biomes, 1, resources, new[] { farm, sawmill }, gameplay,
            seasons: calendar ?? SeasonCalendar.Disabled);
    }

    private static Simulation NewSim(SeasonCalendar? calendar = null, double growthPerSecond = 0) =>
        new(Content(calendar, growthPerSecond), new UniformTerrain(1));

    /// <summary>Kolik tiků trvá jeden herní den při nastavení téhle testovací hry.</summary>
    private static int TicksPerDay(Simulation sim) => (int)(Simulation.TicksPerSecond * 10);

    private static double ProducedOver(Simulation sim, int resource, int ticks)
    {
        double before = sim.GetResource(resource);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(resource) - before;
    }

    [Fact]
    public void SeasonsFollowEachOtherAndWrapAround()
    {
        var sim = NewSim(Calendar(Season("spring"), Season("summer")));
        Assert.Equal(0, sim.CurrentSeasonIndex);

        for (int i = 0; i < TicksPerDay(sim); i++)
        {
            sim.Tick();
        }

        Assert.Equal(1, sim.CurrentSeasonIndex);

        for (int i = 0; i < TicksPerDay(sim); i++)
        {
            sim.Tick();
        }

        Assert.Equal(0, sim.CurrentSeasonIndex); // rok se zavřel
    }

    [Fact]
    public void Winter_CutsFoodProductionButNotTheRest()
    {
        // Zima podvazuje pole, ne hutě — jinak by se z ní stala jen globální brzda.
        var summer = NewSim(Calendar(Season("summer", food: 1.0)));
        Assert.Equal(PlacementResult.Ok, summer.TryPlaceBuildingFree(0, 0, 0)); // farma
        Assert.Equal(PlacementResult.Ok, summer.TryPlaceBuildingFree(1, 2, 0)); // pila
        double summerFood = ProducedOver(summer, Food, 20);
        double summerWood = ProducedOver(summer, Wood, 20);

        var winter = NewSim(Calendar(Season("winter", food: 0.5)));
        Assert.Equal(PlacementResult.Ok, winter.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, winter.TryPlaceBuildingFree(1, 2, 0));
        double winterFood = ProducedOver(winter, Food, 20);
        double winterWood = ProducedOver(winter, Wood, 20);

        Assert.True(winterFood < summerFood, $"v zimě má jídla ubýt ({winterFood} vs {summerFood})");
        Assert.Equal(summerWood, winterWood, 3);
    }

    [Fact]
    public void Autumn_MakesHandGatheringWorthMore()
    {
        var plain = NewSim(Calendar(Season("summer")));
        Assert.True(plain.TryHarvest(5, 5, out _, out int plainAmount));

        var autumn = NewSim(Calendar(Season("autumn", harvest: 2.0)));
        Assert.True(autumn.TryHarvest(5, 5, out _, out int autumnAmount));

        Assert.True(autumnAmount > plainAmount,
            $"na podzim má sběr nést víc ({autumnAmount} vs {plainAmount})");
    }

    [Fact]
    public void Spring_SpeedsUpGrowth()
    {
        var summer = NewSim(Calendar(Season("summer")), growthPerSecond: 1.0);
        double summerGrowth = GrowthOver(summer, 20);

        var spring = NewSim(Calendar(Season("spring", growth: 2.0)), growthPerSecond: 1.0);
        double springGrowth = GrowthOver(spring, 20);

        Assert.True(springGrowth > summerGrowth, $"na jaře má město růst rychleji ({springGrowth} vs {summerGrowth})");
    }

    [Fact]
    public void Winter_BurnsFuel()
    {
        var sim = NewSim(Calendar(Season("winter", fuel: 0.1)));
        double before = sim.GetResource(Wood);

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.GetResource(Wood) < before, "v zimě se má topit dřevem");
        Assert.True(sim.HasFuelForHeating);
    }

    [Fact]
    public void WinterWithoutFuel_FreezesGrowthButKillsNobody()
    {
        // Došlé palivo je stejná dohoda jako došlé jídlo: růst stojí, nikdo neumírá.
        var sim = NewSim(Calendar(Season("winter", fuel: 100.0, growth: 1.0, coldGrowth: 0.1)), growthPerSecond: 1.0);
        double populationBefore = sim.Population;

        for (int i = 0; i < 60; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.HasFuelForHeating);
        Assert.Equal(0, sim.GetResource(Wood), 3);
        Assert.True(sim.Population >= populationBefore, "v zimě nikdo neumírá, jen se přestane růst");
        Assert.Equal(0.1, sim.SeasonGrowthMult, 3);
    }

    [Fact]
    public void SummerAfterAColdWinter_ForgetsTheFreeze()
    {
        // Příznak mrznutí se nesmí zaseknout — v létě se netopí, tedy je vždy „teplo".
        var sim = NewSim(Calendar(Season("winter", fuel: 100.0), Season("summer")), growthPerSecond: 1.0);
        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.HasFuelForHeating);

        for (int i = 0; i < TicksPerDay(sim); i++)
        {
            sim.Tick();
        }

        Assert.Equal(1, sim.CurrentSeasonIndex);
        Assert.True(sim.HasFuelForHeating);
        Assert.Equal(1.0, sim.SeasonGrowthMult, 3);
    }

    [Fact]
    public void WithoutSeasons_NothingChanges()
    {
        // Starší data soubor nemají — hra pak musí běžet přesně jako dřív.
        var sim = NewSim();

        Assert.Equal(-1, sim.CurrentSeasonIndex);
        Assert.Null(sim.CurrentSeason);
        Assert.Equal(1.0, sim.SeasonFoodMult, 6);
        Assert.Equal(1.0, sim.SeasonHarvestMult, 6);
        Assert.Equal(1.0, sim.SeasonGrowthMult, 6);
        Assert.True(sim.HasFuelForHeating);
    }

    [Fact]
    public void RealContent_HasAFullYearWithADistinctWinter()
    {
        var calendar = TestData.LoadRealContent().Seasons;

        Assert.True(calendar.IsEnabled, "roční období mají být ve skutečných datech zapnutá");
        Assert.Equal(4, calendar.Seasons.Count);
        Assert.Contains(calendar.Seasons, s => s.FoodProductionMult < 1.0); // zima
        Assert.Contains(calendar.Seasons, s => s.HarvestMult > 1.0);        // podzim
        Assert.Contains(calendar.Seasons, s => s.GrowthMult > 1.0);         // jaro
        Assert.Contains(calendar.Seasons, s => s.NeedsHeating);

        // Rok se musí vejít do jednoho sezení, jinak hráč zimu nikdy neuvidí.
        Assert.True(calendar.DaysPerYear <= 12, $"rok o {calendar.DaysPerYear} dnech je na idle hru moc dlouhý");
    }

    private static double GrowthOver(Simulation sim, int ticks)
    {
        double before = sim.Population;
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.Population - before;
    }
}
