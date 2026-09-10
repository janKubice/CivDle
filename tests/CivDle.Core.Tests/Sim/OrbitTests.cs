using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Oběžná dráha: družice se nestaví na dlaždici, vypouští se — a co dělá,
/// dělá globálně.
///
/// <para>Hlídají se tři věci, na kterých to může tiše selhat: že se efekt
/// opravdu promítne do násobiče (a po sundání zmizí), že poloha na dráze je
/// funkce tiku (jinak by se obrázek rozešel se savem) a že se počty přenesou
/// přes uložení.</para>
/// </summary>
public class OrbitTests
{
    [Fact]
    public void RealContentHasSatellitesAndALaunchSite()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Orbit.IsEnabled);
        Assert.True(content.Orbit.NeedsLaunchSite, "bez kosmodromu by byla orbita jen položka v menu");
        Assert.All(content.Orbit.Satellites, s => Assert.True(s.MaxCount > 0));
    }

    [Fact]
    public void WithoutASpaceportNothingLaunches()
    {
        var (sim, content) = World();
        sim.DebugFillStorages();

        Assert.False(sim.HasLaunchSite);
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanLaunchSatellite(0));
        Assert.Equal(PlacementResult.NotUnlocked, sim.TryLaunchSatellite(0));
        Assert.Equal(0, sim.Orbit.TotalLaunched);
    }

    [Fact]
    public void ASpaceportOpensTheLaunchPad()
    {
        var (sim, content) = WorldWithSpaceport();

        Assert.True(sim.HasLaunchSite);
        Assert.Equal(PlacementResult.Ok, sim.CanLaunchSatellite(0));
    }

    [Fact]
    public void AnUnfinishedSpaceportDoesNotCount()
    {
        // Kosmodrom se staví hodiny. Kdyby stačilo položit staveniště, byla by
        // z doby stavby jen formalita.
        var (sim, content) = World();
        GrantAllTech(sim, content);
        sim.DebugGrantAscensionLevels(4);
        sim.DebugFillStorages();
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);
        GrowMetropolis(sim, content);

        int port = content.Buildings.IndexOf("spaceport");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(port, 9, 9));

        Assert.False(sim.HasLaunchSite, "rozestavěný kosmodrom nikam nic nevypustí");
    }

    [Fact]
    public void LaunchingCostsResourcesAndTakesTime()
    {
        var (sim, content) = WorldWithSpaceport();
        var cost = sim.Orbit.NextCost(0);
        double before = sim.GetResource(cost[0].ResourceIndex);

        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));

        Assert.Equal(before - cost[0].Amount, sim.GetResource(cost[0].ResourceIndex), 3);
        Assert.Equal(0, sim.Orbit.TotalLaunched);
        Assert.Equal(0, sim.Orbit.UnderConstruction);
    }

    [Fact]
    public void OnlyOneLaunchAtATime()
    {
        var (sim, content) = WorldWithSpaceport();

        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
        Assert.Equal(PlacementResult.Occupied, sim.CanLaunchSatellite(1));
    }

    [Fact]
    public void TheSatelliteReachesOrbitAndItsBonusShowsUp()
    {
        var (sim, content) = WorldWithSpaceport();
        double before = sim.Bonuses.ProductionMult;

        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0)); // solární zrcadlo = výroba
        TickUntilLaunched(sim, content.Orbit[0].BuildTicks + 2);

        Assert.Equal(1, sim.Orbit.CountOf(0));
        Assert.True(
            sim.Bonuses.ProductionMult > before,
            $"družice na výrobu nezvedla násobič ({before} → {sim.Bonuses.ProductionMult})");
    }

    [Fact]
    public void TakingItDownTakesTheBonusAway()
    {
        var (sim, content) = WorldWithSpaceport();
        double before = sim.Bonuses.ProductionMult;

        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
        TickUntilLaunched(sim, content.Orbit[0].BuildTicks + 2);

        Assert.True(sim.TryDismantleSatellite(0));

        Assert.Equal(0, sim.Orbit.CountOf(0));
        Assert.Equal(before, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void EachOneCostsMoreThanTheLast()
    {
        var (sim, content) = WorldWithSpaceport();
        var first = sim.Orbit.NextCost(0);

        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
        TickUntilLaunched(sim, content.Orbit[0].BuildTicks + 2);
        sim.DebugFillStorages();

        var second = sim.Orbit.NextCost(0);

        Assert.True(
            second[0].Amount > first[0].Amount,
            $"druhá družice stojí {second[0].Amount}, první {first[0].Amount}");
    }

    [Fact]
    public void TheOrbitHasACeiling()
    {
        // Na malém obsahu, ne na plné hře: šestá družice ostrého obsahu stojí
        // tisíce oceli a test by pak měřil hlavně kapacitu skladů.
        var (sim, catalog) = TinyOrbitWorld(maxCount: 2);

        for (int i = 0; i < 2; i++)
        {
            sim.DebugFillStorages();
            Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
            TickUntilLaunched(sim, catalog[0].BuildTicks + 2);
        }

        sim.DebugFillStorages();

        Assert.True(sim.Orbit.IsFull(0));
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanLaunchSatellite(0));
    }

    [Fact]
    public void PositionOnTheOrbitIsAFunctionOfTheTick()
    {
        // Kdyby se poloha držela jako stav, rozešla by se po načtení savu
        // a hráč by po restartu viděl družice někde jinde.
        var (sim, _) = WorldWithSpaceport();

        double a = sim.Orbit.AngleAt(0, 0, 5000, Simulation.TicksPerSecond);
        double b = sim.Orbit.AngleAt(0, 0, 5000, Simulation.TicksPerSecond);
        double later = sim.Orbit.AngleAt(0, 0, 5600, Simulation.TicksPerSecond);

        Assert.Equal(a, b);
        Assert.NotEqual(a, later);
    }

    [Fact]
    public void SatellitesOfOneKindDoNotSitOnTopOfEachOther()
    {
        var (sim, _) = WorldWithSpaceport();

        double first = sim.Orbit.AngleAt(0, 0, 1000, Simulation.TicksPerSecond);
        double second = sim.Orbit.AngleAt(0, 1, 1000, Simulation.TicksPerSecond);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void WhatIsUpThereSurvivesSaveAndLoad()
    {
        var (sim, content) = WorldWithSpaceport();
        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
        TickUntilLaunched(sim, content.Orbit[0].BuildTicks + 2);
        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(1)); // rozestavěná

        var restored = SaveAndLoad(sim, content);

        Assert.Equal(1, restored.Orbit.CountOf(0));
        Assert.Equal(1, restored.Orbit.UnderConstruction);
        Assert.True(restored.Orbit.TicksLeft > 0);
        Assert.True(restored.Bonuses.ProductionMult > 1.0, "bonus družice se po načtení musí obnovit");
    }

    [Fact]
    public void AnOlderSaveJustHasAnEmptyOrbit()
    {
        // Sekce v savu chybí = stav před tím, než orbita existovala.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();

        var restored = SaveAndLoad(sim, content);

        Assert.Equal(0, restored.Orbit.TotalLaunched);
        Assert.Equal(-1, restored.Orbit.UnderConstruction);
    }

    [Fact]
    public void AscensionEmptiesTheOrbit()
    {
        // Vzestup je nový svět v jiném měřítku, ne pokračování. Kosmodrom
        // po něm nestojí, takže by družice visely nad prázdnou planetou.
        //
        // Na malém obsahu s dostupným prahem Vzestupu: v plné hře by se test
        // musel doklikat k populaci pozdní éry a měřil by hlavně to.
        var (sim, catalog) = TinyOrbitWorld(maxCount: 3);
        Assert.Equal(PlacementResult.Ok, sim.TryLaunchSatellite(0));
        TickUntilLaunched(sim, catalog[0].BuildTicks + 2);
        Assert.Equal(1, sim.Orbit.CountOf(0));
        Assert.True(sim.Bonuses.ProductionMult > 1.0);

        sim.DebugAddPopulation(sim.AscensionRequirement() * 2);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(0, sim.Orbit.TotalLaunched);
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);
    }

    /// <summary>
    /// Malý svět s jedinou levnou družicí a bez kosmodromu (družice se dá
    /// vypustit rovnou). Pro pravidla, která nemají co dělat s ekonomikou
    /// pozdní hry — strop, reset, cena.
    /// </summary>
    private static (Simulation Sim, OrbitCatalog Catalog) TinyOrbitWorld(int maxCount)
    {
        var catalog = new OrbitCatalog(new[]
        {
            new SatelliteDef(
                "test_mirror", "orbit.test", new[] { new ResourceAmount(0, 5) },
                CostGrowth: 2.0, BuildTicks: 3, Effect: "production_mult",
                Magnitude: 0.1, MaxCount: maxCount, Altitude: 0.5, Speed: 0.02),
        });

        // Vyšší strop bydlení, ať se dá na práh Vzestupu dosáhnout bez stavění:
        // tenhle test není o tom, jak město roste.
        var content = TestContent.Build(
            orbit: catalog,
            gameplay: TestContent.DefaultGameplay with { BaseHousingCapacity = 500 });

        var sim = new Simulation(content, new UniformTerrain(1));
        sim.SkipTutorial();
        sim.DebugFillStorages();
        return (sim, catalog);
    }

    private static void TickUntilLaunched(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks && sim.Orbit.UnderConstruction >= 0; i++)
        {
            sim.Tick();
        }
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        return (sim, content);
    }

    /// <summary>
    /// Svět s dostavěným kosmodromem a plnými sklady.
    ///
    /// <para>Kosmodrom chce velkoměsto (300 budov v jednom shluku), takže se
    /// tu opravdu postaví — zkratka „nastav hodnost" by testovala něco jiného
    /// než co dělá hra.</para>
    /// </summary>
    private static (Simulation Sim, GameContent Content) WorldWithSpaceport()
    {
        var (sim, content) = World();
        GrantAllTech(sim, content);
        sim.DebugGrantAscensionLevels(4); // megastruktury chtějí měřítko
        sim.DebugFillStorages();

        // Guvernér ať do toho nesahá — jinak se počet budov ve shluku mění
        // pod rukama a test by byl závislý na tom, co zrovna postavil.
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);

        GrowMetropolis(sim, content);
        BuildStorage(sim, content);

        int port = content.Buildings.IndexOf("spaceport");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(port, 9, 9));

        // Kosmodrom se staví dlouho a rozestavěný nic nevypustí — dotikáme ho.
        for (int i = 0; i < content.Buildings[port].BuildTicks + 10 && !sim.HasLaunchSite; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.HasLaunchSite, "kosmodrom se v testu nedostavěl");
        sim.DebugFillStorages();
        return (sim, content);
    }

    /// <summary>
    /// Postaví sklady. Družice stojí stovky oceli a základní kapacita je
    /// desítky — bez skladů by se na ni nedalo našetřit ani v testu, natož
    /// ve hře. (Což je tak akorát: koncová meta má chtít vybudované město.)
    /// </summary>
    private static void BuildStorage(Simulation sim, GameContent content)
    {
        int warehouse = content.Buildings.IndexOf("warehouse");
        for (int i = 0; i < 16; i++)
        {
            sim.TryPlaceBuildingFree(warehouse, 24 + (i % 8) * 3, 24 + (i / 8) * 3);
        }
    }

    /// <summary>
    /// Postaví shluk, ze kterého se stane velkoměsto. Uprostřed nechá díru
    /// na kosmodrom — ten je megastruktura a zabírá 6×6 dlaždic, takže se
    /// mezi domky nevejde do žádné mezery, která by vznikla sama.
    /// </summary>
    private static void GrowMetropolis(Simulation sim, GameContent content)
    {
        int house = content.Buildings.IndexOf("house");
        for (int y = 0; y < 22; y++)
        {
            for (int x = 0; x < 22; x++)
            {
                if (x is >= 8 and <= 15 && y is >= 8 and <= 15)
                {
                    continue; // místo pro kosmodrom (6×6 s rezervou)
                }

                sim.TryPlaceBuildingFree(house, x, y);
            }
        }

        // Sídla se přepočítávají na nízké frekvenci; bez pár tiků by shluk
        // ještě neexistoval a kosmodrom by neměl u čeho stát.
        for (int i = 0; i < 200 && sim.NearestSettlementRank(9, 9) < content.SettlementRanks.IndexOf("metropolis"); i++)
        {
            sim.Tick();
        }
    }

    private static void GrantAllTech(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }
    }

    private static Simulation SaveAndLoad(Simulation sim, GameContent content)
    {
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(
            stream, sim, new SaveMetadata(sim.Seed, "medium", "continents", DateTime.UtcNow));
        stream.Position = 0;

        var (loaded, _) = new SaveGameSerializer().Read(stream, content);
        return loaded;
    }
}
