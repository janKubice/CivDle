using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Dědictví přes Vzestup — znalosti, základy města a mapa.
///
/// <para>Je to nejsilnější věc, kterou Odkaz nabízí: neruší se tím číslo, ale
/// <b>část samotného resetu</b>. Testy hlídají obojí — že se bez koupeného
/// dědictví nic nedědí (jinak by Vzestup přestal být Vzestup) a že se
/// s koupeným doopravdy dědí to, co slibuje.</para>
/// </summary>
public class LegacyInheritanceTests
{
    [Fact]
    public void ANewGameInheritsNothing()
    {
        // Bez koupeného dědictví musí Vzestup pořád mazat všechno — jinak by
        // se první hra chovala jinak, než jak je popsaná.
        var sim = World();

        Assert.Equal(0, sim.InheritedTechs);
        Assert.Equal(0, sim.InheritedBuildings);
        Assert.False(sim.InheritsMap);
        Assert.False(sim.HasInheritance);
    }

    [Fact]
    public void AscendingWithoutInheritanceWipesTheCity()
    {
        var sim = GrownWorld();
        Assert.True(sim.Buildings.Length > 0, "test potřebuje postavené město");

        AscendNow(sim);

        Assert.Equal(0, sim.Buildings.Length);
        Assert.Equal(0, sim.LastInheritedBuildings);
        Assert.Equal(0, sim.LastInheritedTechs);
    }

    [Fact]
    public void KeptBuildingsSurviveTheReset()
    {
        var sim = ReadyForInheritance("eternal_foundations", levels: 1);
        int keep = sim.InheritedBuildings;
        Assert.True(keep > 0, "vylepšení nezvedlo počet dědených budov");

        int before = sim.Buildings.Length;
        AscendNow(sim);

        Assert.True(sim.Buildings.Length > 0, "po Vzestupu nezůstala stát ani jedna budova");
        Assert.True(sim.Buildings.Length <= Math.Min(keep, before));
        Assert.Equal(sim.Buildings.Length, sim.LastInheritedBuildings);
    }

    [Fact]
    public void KeptTechsSurviveTheReset()
    {
        var sim = ReadyForInheritance("ancestral_libraries", levels: 1);
        Assert.True(sim.InheritedTechs > 0);

        AscendNow(sim);

        Assert.Equal(sim.InheritedTechs, sim.LastInheritedTechs);
        Assert.True(sim.TechsResearched > 0, "strom výzkumu zůstal na nule");
    }

    [Fact]
    public void InheritedTechsRespectPrerequisites()
    {
        // Kdyby se dědily nejdražší uzly, dostal by hráč konce větví bez
        // jejich základů a strom by vypadal rozbitě.
        var content = TestData.LoadRealContent();
        var sim = ReadyForInheritance("ancestral_libraries", levels: 3, content);

        AscendNow(sim);

        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (!sim.IsTechResearched(i))
            {
                continue;
            }

            foreach (int prereq in content.Techs[i].PrerequisiteIndices)
            {
                Assert.True(
                    sim.IsTechResearched(prereq),
                    $"zděděná technologie {content.Techs[i].Id} nemá vyzkoumaný předpoklad");
            }
        }
    }

    [Fact]
    public void TheMapStaysExploredWithTheEyeOfAges()
    {
        var sim = ReadyForInheritance("eye_of_ages", levels: 1);
        sim.Fog.Reveal(400, 400, 6); // kus světa daleko od startovní kolonie
        Assert.True(sim.Fog.IsExplored(400, 400));

        AscendNow(sim);

        Assert.True(sim.InheritsMap);
        Assert.True(sim.Fog.IsExplored(400, 400), "Oko věků mapu neudrželo");
    }

    [Fact]
    public void WithoutTheEyeTheMapGoesDarkAgain()
    {
        // Druhá půlka téhož: bez tohohle by test výše prošel i pro mlhu,
        // kterou Vzestup nikdy nemazal.
        var sim = GrownWorld();
        sim.Fog.Reveal(400, 400, 6);

        AscendNow(sim);

        Assert.False(sim.Fog.IsExplored(400, 400));
    }

    [Fact]
    public void KeptRoadsSurviveTheReset()
    {
        // Bez cest je zděděné jádro k ničemu: budovy stojí, ale nemají se jak
        // obsloužit a hráč překresluje totéž, co tam bylo.
        var sim = ReadyForInheritance("eternal_roads", levels: 1);
        for (int i = 0; i < 20; i++)
        {
            sim.AddRoadTileForTest(sim.CityCenterX + i, sim.CityCenterY);
        }

        int before = sim.RoadTiles.Count;
        Assert.True(before > 0);

        AscendNow(sim);

        Assert.True(sim.RoadTiles.Count > 0, "po Vzestupu nezůstala ani jedna silnice");
        Assert.Equal(sim.RoadTiles.Count, sim.LastInheritedRoads);
    }

    [Fact]
    public void KeptResourcesNeverMakeTheStartWorse()
    {
        // Kdyby zděděný podíl vyšel míň než běžný startovní stav, dostal by
        // hráč za koupené vylepšení HORŠÍ začátek — opak toho, co kupoval.
        var content = TestData.LoadRealContent();
        var plain = GrownWorld(content);
        AscendNow(plain);

        var rich = ReadyForInheritance("vaults_of_ages", levels: 1, content);
        rich.DebugFillStorages();
        AscendNow(rich);

        for (int r = 0; r < rich.ResourceCount; r++)
        {
            Assert.True(
                rich.GetResource(r) >= plain.GetResource(r) - 0.001,
                $"surovina {r}: dědictví dalo míň než běžný start");
        }
    }

    [Fact]
    public void TheEyeOfAgesIsLevelable()
    {
        // Binární vylepšení je slepá ulička — po jedné koupi se na něm uvízne.
        var sim = ReadyForInheritance("eye_of_ages", levels: 3);

        Assert.True(sim.InheritsMap);
        Assert.True(sim.InheritedRevealRadius > 0, "další úrovně Oka věků nic nepřidaly");
    }

    [Fact]
    public void TheChronicleSurvivesAscension()
    {
        var sim = ReadyForInheritance("unbroken_chronicle", levels: 1);
        sim.CaptureHistoryNow();
        int before = sim.History.Count;
        Assert.True(before > 0, "test potřebuje aspoň jeden snímek kroniky");

        AscendNow(sim);

        Assert.True(sim.History.Count >= before, "kronika se i s Nepřerušenou kronikou smazala");
    }

    [Fact]
    public void WithoutTheChronicleHistoryStartsOver()
    {
        var sim = GrownWorld();
        sim.CaptureHistoryNow();

        AscendNow(sim);

        Assert.True(sim.History.Count <= 1, "časosběr měl začít nanovo");
    }

    [Fact]
    public void RealContentOffersAllThreeInheritances()
    {
        // Efekt bez vylepšení v datech je mrtvý kód.
        var content = TestData.LoadRealContent();
        var effects = new HashSet<string>(StringComparer.Ordinal);
        var upgrades = content.LegacyUpgrades.All;
        for (int i = 0; i < upgrades.Count; i++)
        {
            effects.Add(upgrades[i].Effect);
        }

        Assert.Contains("keep_techs", effects);
        Assert.Contains("keep_buildings", effects);
        Assert.Contains("keep_map", effects);
        Assert.Contains("keep_roads", effects);
        Assert.Contains("keep_resources", effects);
        Assert.Contains("keep_wonders", effects);
        Assert.Contains("keep_history", effects);
    }

    // ----- pomůcky -----

    private static Simulation World(GameContent? content = null)
    {
        content ??= TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(GrasslandBiome(content)));
    }

    /// <summary>Svět s postaveným městem, plnými sklady a splněným prahem Vzestupu.</summary>
    private static Simulation GrownWorld(GameContent? content = null)
    {
        content ??= TestData.LoadRealContent();
        var sim = World(content);
        sim.DebugFillStorages();

        // Domů musí být dost na to, aby populace přerostla práh Vzestupu
        // (250 lidí ve výchozích datech) — jinak by nebylo co dědit.
        int house = FirstHousing(content);
        int placed = 0;
        for (int dy = -6; dy <= 6 && placed < 120; dy++)
        {
            for (int dx = -6; dx <= 6 && placed < 120; dx++)
            {
                if (sim.TryPlaceBuildingFree(house, sim.CityCenterX + dx * 2, sim.CityCenterY + dy * 2)
                    == PlacementResult.Ok)
                {
                    placed++;
                }
            }
        }

        sim.DebugAddPopulation(100_000);
        return sim;
    }

    /// <summary>Vzestup. Práh je splněný populací, kterou svět dostal v GrownWorld.</summary>
    private static void AscendNow(Simulation sim)
    {
        Assert.True(sim.CanAscend(), "test nesplnil práh Vzestupu — dědictví by nebylo co měřit");
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
    }

    /// <summary>
    /// Svět připravený na druhý Vzestup s koupeným dědictvím.
    ///
    /// <para>Jde stejnou cestou jako hráč, a to je smysl: Odkaz se odemyká až
    /// po prvním Vzestupu, takže se musí opravdu vzestoupit, teprve pak koupit
    /// vylepšení (i s jeho předpoklady) a město postavit znovu.</para>
    /// </summary>
    private static Simulation ReadyForInheritance(string upgradeId, int levels, GameContent? content = null)
    {
        content ??= TestData.LoadRealContent();

        var sim = GrownWorld(content);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend()); // Odkaz se odemyká Vzestupem

        BuyLegacyChain(sim, content, upgradeId, levels);
        Rebuild(sim, content); // Vzestup město smazal, druhý práh chce další lidi
        return sim;
    }

    /// <summary>
    /// Koupí vylepšení Odkazu i s celým řetězem předpokladů.
    ///
    /// <para>Rekurzivně: řetězy jsou víc než jeden článek dlouhé (Věčné cesty
    /// stojí na Věčných základech a ty na Věčné výhni), takže nákup jen přímých
    /// předpokladů skončí na „NotUnlocked".</para>
    /// </summary>
    private static void BuyLegacyChain(Simulation sim, GameContent content, string upgradeId, int levels)
    {
        var upgrades = content.LegacyUpgrades;
        Assert.True(upgrades.TryIndexOf(upgradeId, out int index), $"vylepšení '{upgradeId}' v datech chybí");

        sim.DebugGrantLegacyPoints(10_000_000);
        BuyPrerequisites(sim, content, index);

        for (int i = 0; i < levels; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(index));
        }
    }

    /// <summary>Koupí (rekurzivně) všechny předpoklady daného vylepšení.</summary>
    private static void BuyPrerequisites(Simulation sim, GameContent content, int index)
    {
        foreach (int prereq in content.LegacyUpgrades[index].PrerequisiteIndices)
        {
            if (sim.LegacyLevel(prereq) > 0)
            {
                continue;
            }

            BuyPrerequisites(sim, content, prereq);
            Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(prereq));
        }
    }

    /// <summary>Postaví město znovu — po Vzestupu je mapa prázdná.</summary>
    private static void Rebuild(Simulation sim, GameContent content)
    {
        sim.DebugFillStorages();
        int house = FirstHousing(content);
        int placed = 0;
        for (int dy = -12; dy <= 12 && placed < 400; dy++)
        {
            for (int dx = -12; dx <= 12 && placed < 400; dx++)
            {
                if (sim.TryPlaceBuildingFree(house, sim.CityCenterX + dx * 2, sim.CityCenterY + dy * 2)
                    == PlacementResult.Ok)
                {
                    placed++;
                }
            }
        }

        sim.DebugAddPopulation(1_000_000);
    }

    private static byte GrasslandBiome(GameContent content)
    {
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (content.Biomes[i].Id == "grassland")
            {
                return (byte)i;
            }
        }

        return 1;
    }

    private static int FirstHousing(GameContent content)
    {
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].Category == "housing" && content.Buildings[i].AutoBuild)
            {
                return i;
            }
        }

        return 0;
    }
}
