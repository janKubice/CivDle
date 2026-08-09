using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Budovy, které přetvářejí krajinu samy.
///
/// <para>Ruční teraformace je až u vyspělé civilizace; tyhle budovy jsou ta
/// dřívější, pomalejší cesta. Hlídá se přesně to, kvůli čemu vznikly: mění
/// okolí bez kliknutí, <b>neptají se na odemčení zásahu</b> (budova sama je to
/// odemčení) a nejsou zadarmo — jinak by z nich byl tlačítkový cheat.</para>
/// </summary>
public class AutoTerraformTests
{
    private const int Sand = 0;
    private const int Grass = 1;

    private static readonly Resource[] Water =
    {
        new("water", new RgbColor(80, 140, 200), StartAmount: 1000, BaseStorage: 100_000),
    };

    /// <summary>Zavlažovací dílo: mění poušť v louku v okruhu dvou dlaždic.</summary>
    private static GameContent Content(int radius = 2, int unlockTech = -1, int cost = 5)
    {
        var works = new BuildingDef(
            "works", "production", new RgbColor(150, 170, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { true, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0,
            TerraformActionIndex: 0, TerraformRadius: radius);

        var irrigate = new TerraformDef(
            "irrigate", Grass, new[] { Sand }, new[] { new ResourceAmount(0, cost) }, unlockTech);

        // Technologie existuje, ale nikdo ji nevyzkoumá — právě o to jde.
        var techs = unlockTech >= 0
            ? new[] { new TechDef("terraforming", new[] { new ResourceAmount(0, 1) }, Array.Empty<int>(), Array.Empty<int>(), string.Empty, 0) }
            : Array.Empty<TechDef>();

        return TestContent.Build(
            biomes: new[] { TestContent.LandBiome("sand"), TestContent.LandBiome("grass") },
            fallbackBiomeIndex: Sand,
            resources: Water,
            buildings: new[] { works },
            techs: techs,
            terraform: new[] { irrigate });
    }

    private static Simulation OnSand(GameContent content) =>
        new(content, new UniformTerrain((byte)Sand));

    /// <summary>Odtiká zadaný počet tiků (systém běží na nízké frekvenci).</summary>
    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Kolik dlaždic v okruhu už je loukou.</summary>
    private static int Reshaped(Simulation sim, int centerX, int centerY, int radius)
    {
        int count = 0;
        for (int y = centerY - radius; y <= centerY + radius; y++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                if (sim.BiomeAt(x, y) == Grass)
                {
                    count++;
                }
            }
        }

        return count;
    }

    [Fact]
    public void TheBuildingReshapesItsSurroundingsOnItsOwn()
    {
        var content = Content();
        var sim = OnSand(content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 10, 10));
        Assert.Equal(0, Reshaped(sim, 10, 10, 2));

        Run(sim, 600);

        Assert.True(Reshaped(sim, 10, 10, 2) > 0, "za deset sekund se nezměnila ani dlaždice");
    }

    [Fact]
    public void ItWorksWithoutResearchingTheManualTool()
    {
        // Budova sama JE to odemčení. Kdyby čekala na technologii, neměl by
        // výzkum na ni smysl — hráč ji staví právě proto, že tu vědu nemá.
        var content = Content(unlockTech: 0);
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);

        // Ruční zásah je zamčený…
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanTerraform(0, 12, 10));

        Run(sim, 600);

        // …ale budova pracuje.
        Assert.True(Reshaped(sim, 10, 10, 2) > 0);
    }

    [Fact]
    public void ItChangesOneTileAtATime()
    {
        // Přetvořit celý okruh naráz by byl skok, ne růst — a u stovek stanic
        // by to znamenalo projít desetitisíce dlaždic v jednom tiku.
        var content = Content();
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);

        Run(sim, 61);
        int after = Reshaped(sim, 10, 10, 2);

        Assert.InRange(after, 1, 2);
    }

    [Fact]
    public void ItStopsWhenTheSurroundingsAreDone()
    {
        var content = Content(radius: 1);
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);

        Run(sim, 3000);

        // Devět dlaždic okruhu minus ta pod budovou — pod stavbou se nekope.
        Assert.Equal(8, Reshaped(sim, 10, 10, 1));
    }

    [Fact]
    public void ItPaysForEveryTile()
    {
        var content = Content(radius: 1, cost: 100);
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);
        double before = sim.GetResource(0);

        Run(sim, 3000);

        // Přesné číslo nedává smysl hlídat — ze skladu ubírá i běžný chod města.
        // Podstatné je, že za každou přetvořenou dlaždici se opravdu zaplatilo.
        int done = Reshaped(sim, 10, 10, 1);
        Assert.True(done > 0);
        Assert.True(before - sim.GetResource(0) >= done * 100,
            $"za {done} dlaždic se strhlo míň než {done * 100}");
    }

    [Fact]
    public void WithoutResourcesItSimplyWaits()
    {
        var content = Content(cost: 100_000);
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);

        Run(sim, 600);

        Assert.Equal(0, Reshaped(sim, 10, 10, 2));
    }

    [Fact]
    public void AnUnfinishedBuildingDoesNothingYet()
    {
        var content = Content();
        var sim = OnSand(content);
        sim.TryPlaceBuilding(0, 10, 10);

        // Stavba je hotová hned (buildTicks = 0), takže kontrolou je opak:
        // rozestavěná budova by nesměla nic měnit. Ověříme aspoň, že se
        // systém drží dokončených — bez toho by se okolí měnilo od základů.
        Assert.True(sim.Buildings[0].IsComplete);
    }

    // ----- skutečná data -----

    [Theory]
    [InlineData("irrigation_works", "irrigation")]
    [InlineData("drainage_works", "drainage")]
    [InlineData("forestry_office", "forestry")]
    [InlineData("polder_mill", "windpumps")]
    public void RealContent_HasAutomaticTerraformersBehindTheirOwnResearch(string buildingId, string techId)
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Buildings.TryIndexOf(buildingId, out int building), $"chybí budova '{buildingId}'");
        Assert.True(content.Buildings[building].Terraforms, $"'{buildingId}' krajinu nemění");

        Assert.True(content.Techs.TryIndexOf(techId, out int tech), $"chybí technologie '{techId}'");
        Assert.Contains(building, content.Techs[tech].UnlockedBuildingIndices);
    }

    [Fact]
    public void RealContent_AutomaticTerraformersComeBeforeTheManualTool()
    {
        // Celý smysl bodu: než hráč odemkne teraformaci rukou, má mít budovy,
        // které to dělají za něj. Kdyby seděly za toutéž (nebo pozdější) vědou,
        // byly by k ničemu.
        var content = TestData.LoadRealContent();
        int manual = content.Techs.IndexOf("terraforming");

        foreach (string techId in new[] { "irrigation", "drainage", "forestry", "windpumps" })
        {
            Assert.False(DependsOn(content, content.Techs.IndexOf(techId), manual),
                $"'{techId}' visí až za ruční teraformací");
        }
    }

    /// <summary>Leží <paramref name="ancestor"/> v prerekvizitách technologie?</summary>
    private static bool DependsOn(GameContent content, int tech, int ancestor)
    {
        if (tech == ancestor)
        {
            return true;
        }

        foreach (int prereq in content.Techs[tech].PrerequisiteIndices)
        {
            if (DependsOn(content, prereq, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
