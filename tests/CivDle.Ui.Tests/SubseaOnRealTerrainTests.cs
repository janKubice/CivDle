using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Podmoří nad opravdu generovaným světem, ne nad vymyšleným pobřežím.
///
/// <para>Testy v jádru staví na rovné hranici mezi souší a mořem, protože se
/// na ní dá tvrdit něco přesného. Skutečné pobřeží je ale roztřepené, plné
/// zálivů a ostrůvků — a právě na něm se pozná, jestli záplava po vodě opravdu
/// funguje, nebo jestli jsme si vyrobili pravidlo, které v praxi nikam
/// nedosáhne.</para>
/// </summary>
public class SubseaOnRealTerrainTests
{
    /// <summary>Semínka s pobřežím. Pevná, ať se test nechová pokaždé jinak.</summary>
    private static readonly long[] Seeds = { 20260728, 30313, 4242 };

    [Fact]
    public void AHarbourOnARealCoastOpensWaterForBuilding()
    {
        var content = LoadContent();
        int checkedWorlds = 0;

        foreach (long seed in Seeds)
        {
            var sim = NewWorld(content, seed);
            int harbour = content.Buildings.IndexOf("harbor");
            if (!TryFindCoast(sim, harbour, out int x, out int y))
            {
                continue; // tenhle seed je vnitrozemí; ostatní to prověří
            }

            checkedWorlds++;

            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(harbour, x, y));

            Assert.True(
                sim.Subsea.CoveredTiles > 0,
                $"seed {seed}: přístav na skutečném pobřeží neotevřel ani jednu dlaždici moře");

            // A hlavně: musí být kam tu farmu položit.
            int farm = content.Buildings.IndexOf("kelp_farm");
            Assert.True(
                FindPlaceableWater(sim, farm, x, y),
                $"seed {seed}: v dosahu přístavu není jediné místo pro podmořskou stavbu");
        }

        Assert.True(checkedWorlds > 0, "žádné ze semínek nemá pobřeží — test by neověřil nic");
    }

    [Fact]
    public void WithoutAHarbourRealSeaIsClosed()
    {
        var content = LoadContent();
        var sim = NewWorld(content, Seeds[0]);

        Assert.True(TryFindCoast(sim, content.Buildings.IndexOf("harbor"), out int x, out int y));
        Assert.Equal(0, sim.Subsea.CoveredTiles);

        int farm = content.Buildings.IndexOf("kelp_farm");
        Assert.False(FindPlaceableWater(sim, farm, x, y));
    }

    private static Simulation NewWorld(GameContent content, long seed)
    {
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, seed), seed);
        sim.SkipTutorial();

        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        return sim;
    }

    /// <summary>
    /// Místo, kam se přístav opravdu vejde. Ptá se rovnou simulace, ne „je
    /// někde poblíž voda" — přístav chce vodu <b>u svého půdorysu</b>, a na
    /// roztřepeném pobřeží je v tom rozdíl dvaceti dlaždic.
    /// </summary>
    private static bool TryFindCoast(Simulation sim, int harbourIndex, out int x, out int y)
    {
        const int Radius = 220;

        for (int dy = -Radius; dy <= Radius; dy++)
        {
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                x = sim.CityCenterX + dx;
                y = sim.CityCenterY + dy;
                if (sim.CanPlace(harbourIndex, x, y) == PlacementResult.Ok)
                {
                    return true;
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    private static bool FindPlaceableWater(Simulation sim, int defIndex, int nearX, int nearY)
    {
        const int Radius = 30;

        for (int dy = -Radius; dy <= Radius; dy++)
        {
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                if (sim.CanPlace(defIndex, nearX + dx, nearY + dy) == PlacementResult.Ok)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
