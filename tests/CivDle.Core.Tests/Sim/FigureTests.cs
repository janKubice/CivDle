using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Významné osobnosti: člověk se narodí na milníku, něco po dobu života
/// zlepší, a když zemře, zůstane po něm socha.
///
/// <para>Hlídá se hlavně to jedno, co se u téhle mechaniky kazí: <b>bonus
/// nesmí přežít nositele</b>. Podruhé pak to, že socha efekt <b>nahradí</b>,
/// ne přidá — jinak by se hráči vyplatilo lidi nechávat umírat.</para>
/// </summary>
public class FigureTests
{
    /// <summary>Milníky se kontrolují po dávkách, ne každý tik — testy musí odtikat aspoň jednu.</summary>
    private const int MilestoneCheckTicks = 12;

    [Fact]
    public void FigureIsBornOnItsMilestoneAndItsBonusApplies()
    {
        var (sim, _) = World();

        Assert.Empty(sim.Figures.Living);
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);

        BuildHutAndWaitForMilestone(sim);

        Assert.Single(sim.Figures.Living);
        Assert.Equal(1.5, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void TheBonusDiesWithHerAndTheStatueReplacesIt_NotAddsToIt()
    {
        // Tohle je celý smysl testu: po dožití má být násobič zpátky na jedné.
        // Kdyby se bonus sčítal do proměnné a při úmrtí se zapomněl odečíst,
        // zůstal by hráči napořád — a nic by to nenahlásilo.
        var (sim, content) = World();
        BuildHutAndWaitForMilestone(sim);

        int statueIndex = content.Buildings.IndexOf("statue");
        Assert.Equal(0, CountOf(sim, statueIndex));

        TickUntilNobodyIsAlive(sim);

        Assert.Empty(sim.Figures.Living);
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);
        Assert.Equal(1, CountOf(sim, statueIndex));

        // Socha stojí, ale výrobu nezvedá — je to připomínka, ne druhý bonus.
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void SheIsRememberedSoSheIsNotBornTwice()
    {
        var (sim, _) = World();
        BuildHutAndWaitForMilestone(sim);
        TickUntilNobodyIsAlive(sim);

        // Milník je splněný pořád (budova nezmizela). Kdyby se osobnost vázala
        // jen na „splněno", rodila by se každou kontrolu znovu.
        for (int i = 0; i < MilestoneCheckTicks * 5; i++)
        {
            sim.Tick();
        }

        Assert.Empty(sim.Figures.Living);
        Assert.Single(sim.Figures.Remembered);
    }

    [Fact]
    public void AscendingClearsTheFiguresOfThePreviousCivilisation()
    {
        var (sim, _) = World();
        BuildHutAndWaitForMilestone(sim);
        Assert.Single(sim.Figures.Living);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Empty(sim.Figures.Living);
        Assert.Empty(sim.Figures.Remembered);
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void LivingFiguresSurviveSaveAndLoad()
    {
        var content = FigureContent();
        var metadata = new SaveMetadata(1, "s", "test", DateTime.UtcNow);
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        BuildHutAndWaitForMilestone(sim);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.Single(loaded.Figures.Living);
        Assert.Single(loaded.Figures.Remembered);
        Assert.Equal(1.5, loaded.Bonuses.ProductionMult, 6);

        // A dožije doma i po načtení — narozena byla ve svém tiku, ne v nule.
        Assert.Equal(sim.Figures.Living[0].BornTick, loaded.Figures.Living[0].BornTick);
    }

    [Fact]
    public void ADeadFigureIsNotBornAgainAfterLoading()
    {
        var content = FigureContent();
        var metadata = new SaveMetadata(1, "s", "test", DateTime.UtcNow);
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        BuildHutAndWaitForMilestone(sim);
        TickUntilNobodyIsAlive(sim);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        for (int i = 0; i < MilestoneCheckTicks * 3; i++)
        {
            loaded.Tick();
        }

        Assert.Empty(loaded.Figures.Living);
        Assert.Single(loaded.Figures.Remembered);
    }

    [Fact]
    public void RealContentHasFiguresAndEachOneLeavesARealStatue()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Figures.IsEnabled, "bez osobností by hra přišla o jediný kus vlastního příběhu");
        foreach (var figure in content.Figures.Figures)
        {
            Assert.True(figure.NeedsMilestone, $"osobnost '{figure.Id}' se nemá kde narodit");
            Assert.True(figure.LeavesStatue, $"po osobnosti '{figure.Id}' nic nezbude");
            Assert.True(figure.LifeTicks > 0);

            var statue = content.Buildings[figure.StatueBuildingIndex];
            Assert.False(statue.Buildable, $"'{statue.Id}' je pomník, ne stavba na objednávku");
            Assert.True(statue.ServiceValue > 0, $"'{statue.Id}' by jinak byl jen barevný čtvereček");
        }
    }

    // ----- pomocné -----

    private static void BuildHutAndWaitForMilestone(Simulation sim)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));
        for (int i = 0; i < MilestoneCheckTicks; i++)
        {
            sim.Tick();
        }
    }

    private static void TickUntilNobodyIsAlive(Simulation sim)
    {
        for (int i = 0; i < 400 && sim.Figures.HasLiving; i++)
        {
            sim.Tick();
        }
    }

    private static int CountOf(Simulation sim, int defIndex)
    {
        int count = 0;
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DefIndex == defIndex)
            {
                count++;
            }
        }

        return count;
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = FigureContent();
        return (new Simulation(content, new UniformTerrain(1), seed: 1), content);
    }

    /// <summary>
    /// Nejmenší svět, ve kterém se dá osobnost narodit: jeden milník za první
    /// budovu, jedna osobnost s krátkým životem a socha, která po ní zbude.
    /// </summary>
    private static GameContent FigureContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var buildings = new[]
        {
            TestContent.SimpleBuilding("hut", biomes.Length),
            TestContent.Service("statue", serviceValue: 5, upkeepResource: 0, upkeepAmount: 0, biomeCount: biomes.Length),
        };

        return TestContent.Build(
            biomes: biomes,
            buildings: buildings,
            // Vzestup hned po první budově: test na „nová civilizace, noví
            // velikáni" jinak musí nejdřív vypěstovat město.
            prestige: new PrestigeConfig(
                new GoalCondition(MetricKind.TotalBuildings, -1, 1), MetricKind.Population, -1, 1),
            milestones: new[] { new MilestoneDef("first", new GoalCondition(MetricKind.TotalBuildings, -1, 1)) },
            figures: new FigureCatalog(new[]
            {
                new FigureDef("hero", "production_mult", Magnitude: 0.5, LifeTicks: 30,
                    MilestoneIndex: 0, StatueBuildingIndex: 1),
            }));
    }
}
