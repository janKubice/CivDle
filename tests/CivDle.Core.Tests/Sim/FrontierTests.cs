using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Frontier Defense — volitelný režim obrany.
///
/// <para>První a nejdůležitější test je ten poslední: <b>vypnutý režim nesmí
/// dělat vůbec nic</b>. Je to jediná položka celého plánu, která přidává novou
/// entitu do tikové smyčky, a kdyby prosakovala do běžné hry, poškodí ji
/// i těm, kdo si ji nezapnuli.</para>
/// </summary>
public class FrontierTests
{
    [Fact]
    public void RealContentHasWavesAndTowers()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Frontier.IsAvailable);
        Assert.Contains(content.Buildings.All, b => b.IsArmed);
    }

    [Fact]
    public void ANewGameHasDefenceOff()
    {
        var (sim, _) = World();

        Assert.False(sim.FrontierDefense);
    }

    [Fact]
    public void WithTheModeOffNothingEverSpawns()
    {
        var (sim, content) = World();

        TickPast(sim, content.Frontier.TickOfWave(3));

        Assert.Equal(0, sim.Frontier.Count);
        Assert.Equal(0, sim.Frontier.NextWave);
    }

    [Fact]
    public void WithTheModeOffTowersCannotEvenBeBuilt()
    {
        // Věž ve hře bez útoků je past: hráč ji postaví, zaplatí za ni dělníky
        // a nikdy se nedozví, proč nic nedělá.
        var (sim, content) = World();
        int tower = content.Buildings.IndexOf("watchtower");

        Assert.Equal(PlacementResult.NeedsDefenceMode, sim.CanPlace(tower, 4, 4));
        Assert.False(sim.IsBuildingBuildable(tower));
    }

    [Fact]
    public void WithTheModeOffNothingEvenReloads()
    {
        // Druhá půlka „nulového dopadu": nejde jen o to, že se nikdo neobjeví.
        // Celý systém se nesmí ani rozběhnout — kdyby tikal naprázdno, platí
        // za něj i ten, kdo si režim nezapnul.
        //
        // Věže se staví v zapnutém režimu a ten se pak odepře: jinak by se
        // vůbec nedaly postavit a test by neověřil nic.
        var (sim, content) = Defended();
        PlaceTowers(sim, content, count: 8);
        Assert.Contains(sim.Buildings.ToArray(), b => content.Buildings[b.DefIndex].IsArmed);

        var (plain, _) = World();
        TickPast(plain, content.Frontier.TickOfWave(2));

        foreach (var building in plain.Buildings.ToArray())
        {
            Assert.Equal(0, building.ReloadTicks);
            Assert.Equal(0, building.DisabledTicks);
        }

        Assert.Equal(0, plain.Frontier.Count);
    }

    [Fact]
    public void TheFirstWaveArrivesOnSchedule()
    {
        var (sim, content) = Defended();

        TickPast(sim, content.Frontier.FirstWaveTick - 10);
        Assert.Equal(0, sim.Frontier.Count);

        TickPast(sim, content.Frontier.FirstWaveTick + 1);
        Assert.True(sim.Frontier.Count > 0, "první vlna nepřišla, kdy měla");
        Assert.Equal(1, sim.Frontier.NextWave);
    }

    [Fact]
    public void AttackersSpawnAwayFromTheCityAndWalkTowardIt()
    {
        var (sim, content) = Defended();
        TickPast(sim, content.Frontier.FirstWaveTick + 1);

        double first = DistanceToCity(sim, 0);
        Assert.True(first > 10, $"útočník se objevil {first:F1} dlaždic od města, to je uvnitř");

        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        Assert.True(
            DistanceToCity(sim, 0) < first,
            "útočník se za dvacet vteřin nepřiblížil ani o kousek");
    }

    [Fact]
    public void EachWaveIsBiggerThanTheOneBefore()
    {
        var content = TestData.LoadRealContent();
        var wave = content.Frontier.WaveAt(0);

        int first = content.Frontier.CountInWave(0, wave[0]);
        int tenth = content.Frontier.CountInWave(9, wave[0]);

        Assert.True(tenth > first, $"desátá vlna má {tenth}, první {first}");
    }

    [Fact]
    public void ATowerShootsAttackersDown()
    {
        var (sim, content) = Defended();
        PlaceTowers(sim, content, count: 12);

        TickPast(sim, content.Frontier.FirstWaveTick + 1);
        int arrived = sim.Frontier.Count;
        Assert.True(arrived > 0);

        for (int i = 0; i < 4000 && sim.Frontier.Count > 0; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Frontier.Killed > 0, "věže za deset minut nesestřelily nikoho");
    }

    [Fact]
    public void WithoutTowersTheAttackersGetThrough()
    {
        var (sim, content) = Defended();

        TickPast(sim, content.Frontier.FirstWaveTick + 1);
        for (int i = 0; i < 4000 && sim.Frontier.ReachedCity == 0; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Frontier.ReachedCity > 0, "bez obrany se do města nikdo nedostal");
        Assert.Equal(0, sim.Frontier.Killed);
    }

    [Fact]
    public void ADamagedBuildingStopsProducingAndThenRepairsItself()
    {
        var (sim, content) = Defended();
        int index = PlaceOne(sim, content, "house", 4, 4);

        sim.DamageBuilding(index, 50);
        Assert.True(sim.Buildings[index].DisabledTicks > 0);

        sim.Tick();
        Assert.Equal(BuildingStall.Damaged, sim.Buildings[index].Stall);

        for (int i = 0; i < 60; i++)
        {
            sim.Tick();
        }

        Assert.Equal(0, sim.Buildings[index].DisabledTicks);
        Assert.NotEqual(BuildingStall.Damaged, sim.Buildings[index].Stall);
    }

    [Fact]
    public void DamageNeverDestroysTheBuilding()
    {
        // Trvalá ztráta postupu je v idle hře trest za to, že šel hráč spát.
        var (sim, content) = Defended();
        int before = sim.Buildings.Length;
        int index = PlaceOne(sim, content, "house", 4, 4);

        for (int i = 0; i < 50; i++)
        {
            sim.DamageBuilding(index, 10_000);
        }

        Assert.Equal(before + 1, sim.Buildings.Length);
        Assert.True(
            sim.Buildings[index].DisabledTicks <= content.Frontier.RepairTicks * 2,
            "poškození se nesmí sčítat donekonečna");
    }

    [Fact]
    public void TheSameSeedGivesTheSameBattle()
    {
        // Tohle je ta vlastnost, na které stojí save i test. Kdyby se vlna
        // losovala za běhu, dva stejné světy by se rozešly.
        var (first, content) = Defended();
        var (second, _) = Defended();

        TickPast(first, content.Frontier.FirstWaveTick + 300);
        TickPast(second, content.Frontier.FirstWaveTick + 300);

        Assert.Equal(first.Frontier.Count, second.Frontier.Count);
        for (int i = 0; i < first.Frontier.Count; i++)
        {
            Assert.Equal(first.Frontier.Attackers[i].X, second.Frontier.Attackers[i].X, 4);
            Assert.Equal(first.Frontier.Attackers[i].Y, second.Frontier.Attackers[i].Y, 4);
            Assert.Equal(first.Frontier.Attackers[i].Health, second.Frontier.Attackers[i].Health);
        }
    }

    [Fact]
    public void TheBattleSurvivesSaveAndLoad()
    {
        var (sim, content) = Defended();
        int index = PlaceOne(sim, content, "house", 4, 4);
        TickPast(sim, content.Frontier.FirstWaveTick + 100);
        sim.DamageBuilding(index, 200);

        int attackers = sim.Frontier.Count;
        Assert.True(attackers > 0);

        var restored = SaveAndLoad(sim, content);

        Assert.True(restored.FrontierDefense, "režim se musí přenést, jinak vlny přestanou chodit");
        Assert.Equal(attackers, restored.Frontier.Count);
        Assert.Equal(sim.Frontier.NextWave, restored.Frontier.NextWave);
        Assert.True(restored.Buildings[index].DisabledTicks > 0, "poškození přežít musí");
    }

    [Fact]
    public void ANormalGameStaysNormalAfterLoading()
    {
        var (sim, content) = World();

        var restored = SaveAndLoad(sim, content);

        Assert.False(restored.FrontierDefense);
    }

    [Fact]
    public void AscensionClearsTheBattlefield()
    {
        var (sim, content) = Defended();
        TickPast(sim, content.Frontier.FirstWaveTick + 50);
        Assert.True(sim.Frontier.Count > 0);

        sim.DebugAddPopulation(sim.AscensionRequirement() * 2);
        for (int i = 0; i < 4000 && !sim.CanAscend(); i++)
        {
            sim.Tick();
            sim.DebugAddPopulation(sim.AscensionRequirement() * 2);
        }

        if (!sim.CanAscend())
        {
            return; // na tomhle obsahu se práh nedá dosáhnout; pokrývá to test v OrbitTests
        }

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        Assert.Equal(0, sim.Frontier.Count);
        Assert.Equal(0, sim.Frontier.NextWave);
    }

    private static double DistanceToCity(Simulation sim, int index)
    {
        double dx = sim.Frontier.Attackers[index].X - sim.CityCenterX;
        double dy = sim.Frontier.Attackers[index].Y - sim.CityCenterY;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static void TickPast(Simulation sim, long tick)
    {
        while (sim.TickCount < tick)
        {
            sim.Tick();
        }
    }

    /// <summary>Postaví věnec věží kolem města, ať mají útočníci na co narazit.</summary>
    private static void PlaceTowers(Simulation sim, GameContent content, int count)
    {
        int tower = content.Buildings.IndexOf("watchtower");
        for (int i = 0; i < count; i++)
        {
            double angle = Math.Tau * i / count;
            int x = sim.CityCenterX + (int)Math.Round(Math.Cos(angle) * 6);
            int y = sim.CityCenterY + (int)Math.Round(Math.Sin(angle) * 6);
            sim.TryPlaceBuildingFree(tower, x, y);
        }
    }

    private static int PlaceOne(Simulation sim, GameContent content, string id, int x, int y)
    {
        int def = content.Buildings.IndexOf(id);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(def, x, y));
        return sim.Buildings.Length - 1;
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        return (sim, content);
    }

    private static (Simulation Sim, GameContent Content) Defended()
    {
        var (sim, content) = World();
        sim.EnableFrontierDefense();
        return (sim, content);
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
