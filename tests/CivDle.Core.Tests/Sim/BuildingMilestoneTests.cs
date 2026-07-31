using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Milníky za počet budov: dvacátá farma nesmí být jen další porce výroby.
/// Po překročení prahu se zlepší <b>všechny</b> budovy toho typu naráz.
///
/// <para>Testy hlídají hlavně to, co z milníku dělá rozhodnutí, a ne jen číslo:
/// práh platí zpětně i pro dávno postavené budovy, strop drží čísla při zemi,
/// staveniště se nepočítá a bonus zmizí, když budovy ubudou.</para>
/// </summary>
public class BuildingMilestoneTests
{
    private const int Wood = 0;
    private const int Farm = 0;
    private const int Plain = 1;

    /// <summary>Práh: každé 3 budovy, +25 %, nejvýš 2 stupně.</summary>
    private static BuildingMilestones Steps => new(Every: 3, BonusPerStep: 0.25, MaxSteps: 2);

    private static GameContent Content(int buildTicks = 0)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000),
        };

        // WorkerSlots: 0 → obsazenost je vždy plná a měření výroby nezávisí na
        // tom, kolik ve městě zrovna žije lidí.
        var farm = new BuildingDef(
            "farm", "production", new RgbColor(120, 160, 90), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 10) }, TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            BuildTicks: buildTicks,
            MilestonesOrNull: Steps);

        // Stejná budova bez milníků — kontrolní vzorek i „jiný typ".
        var plain = farm with { Id = "plain", MilestonesOrNull = null };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        return TestContent.Build(biomes, 1, resources, new[] { farm, plain }, gameplay);
    }

    private static Simulation NewSim(int buildTicks = 0) => new(Content(buildTicks), new UniformTerrain(1));

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static void Build(Simulation sim, int defIndex, int count, int y = 10)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(defIndex, 10 + i * 2, y));
        }
    }

    // ----- samotný práh -----

    [Fact]
    public void TierFor_CountsWholeStepsOnly()
    {
        Assert.Equal(0, Steps.TierFor(0));
        Assert.Equal(0, Steps.TierFor(2));
        Assert.Equal(1, Steps.TierFor(3));
        Assert.Equal(1, Steps.TierFor(5));
        Assert.Equal(2, Steps.TierFor(6));
    }

    [Fact]
    public void TierFor_StopsAtTheCeiling()
    {
        // Bez stropu by šlo jedním typem budovy škálovat výrobu donekonečna.
        Assert.Equal(2, Steps.TierFor(9));
        Assert.Equal(2, Steps.TierFor(1_000_000));
        Assert.Equal(1.5, Steps.MultiplierFor(1_000_000), 6);
    }

    [Fact]
    public void ToNextTier_CountsDownAndThenGoesQuiet()
    {
        // UI z toho píše „ještě N do dalšího stupně"; u stropu nemá co slibovat.
        Assert.Equal(3, Steps.ToNextTier(0));
        Assert.Equal(1, Steps.ToNextTier(2));
        Assert.Equal(3, Steps.ToNextTier(3));
        Assert.Equal(0, Steps.ToNextTier(6));
        Assert.Equal(0, Steps.ToNextTier(50));
    }

    // ----- milník v simulaci -----

    [Fact]
    public void BelowTheThreshold_NothingChanges()
    {
        var sim = NewSim();
        Build(sim, Farm, 2);

        Tick(sim, 60);

        Assert.Equal(2, sim.MilestoneCount(Farm));
        Assert.Equal(0, sim.MilestoneTier(Farm));
        Assert.Equal(1.0, sim.MilestoneMultiplier(Farm), 6);
        Assert.Equal(1f, sim.Buildings[0].MilestoneMult, 3);
    }

    [Fact]
    public void CrossingTheThreshold_LiftsEvenTheOldestBuilding()
    {
        // Tohle je jádro mechaniky: bonus platí zpětně, ne jen pro novou budovu.
        var sim = NewSim();
        Build(sim, Farm, 3);

        Tick(sim, 60);

        Assert.Equal(1, sim.MilestoneTier(Farm));
        Assert.Equal(1.25, sim.MilestoneMultiplier(Farm), 6);
        Assert.Equal(1.25f, sim.Buildings[0].MilestoneMult, 3);
        Assert.Equal(1.25f, sim.Buildings[2].MilestoneMult, 3);
    }

    [Fact]
    public void TheBonusHasACeilingInTheSimulationToo()
    {
        var sim = NewSim();
        Build(sim, Farm, 12);

        Tick(sim, 60);

        Assert.Equal(12, sim.MilestoneCount(Farm));
        Assert.Equal(1.5f, sim.Buildings[0].MilestoneMult, 3); // ne 1 + 4 × 0.25
        Assert.Equal(0, sim.MilestoneToNextTier(Farm));
    }

    [Fact]
    public void OtherTypesAreLeftAlone()
    {
        var sim = NewSim();
        Build(sim, Farm, 3);
        Build(sim, Plain, 3, y: 20);

        Tick(sim, 60);

        Assert.Equal(1.0, sim.MilestoneMultiplier(Plain), 6);
        Assert.Equal(0, sim.MilestoneToNextTier(Plain));
        Assert.Equal(1f, sim.Buildings[3].MilestoneMult, 3);
    }

    [Fact]
    public void ConstructionSitesDoNotCount()
    {
        // Milník je odměna za hotové město. Jinak by šel bonus „půjčit" si
        // rozkopanou plochou, kterou hráč nikdy nedostaví.
        var sim = NewSim(buildTicks: 1000);
        Build(sim, Farm, 3);

        Tick(sim, 60);

        Assert.All(sim.Buildings.ToArray(), b => Assert.False(b.IsComplete));
        Assert.Equal(0, sim.MilestoneCount(Farm));
        Assert.Equal(1.0, sim.MilestoneMultiplier(Farm), 6);
    }

    [Fact]
    public void ANewBuildingInheritsTheBonusImmediately()
    {
        // Bez toho by čerstvá budova vyráběla pod úrovní zbytku města, dokud
        // neproběhne pomalý přepočet — a hráč by viděl nevysvětlené škubnutí.
        var sim = NewSim();
        Build(sim, Farm, 3);
        Tick(sim, 60);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Farm, 40, 40));

        Assert.Equal(1.25f, sim.Buildings[3].MilestoneMult, 3);
    }

    [Fact]
    public void DemolishingBackUnderTheThresholdTakesTheBonusAway()
    {
        var sim = NewSim();
        Build(sim, Farm, 3);
        Tick(sim, 60);
        Assert.True(sim.Buildings[0].MilestoneMult > 1f);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(2));
        Tick(sim, 60);

        Assert.Equal(2, sim.MilestoneCount(Farm));
        Assert.Equal(1f, sim.Buildings[0].MilestoneMult, 3);
    }

    // ----- dopad na výrobu -----

    [Fact]
    public void TheBonusActuallyReachesProduction()
    {
        // Bez tohohle testu by milník mohl být jen číslo v panelu budovy.
        var withMilestones = NewSim();
        Build(withMilestones, Farm, 3);
        Tick(withMilestones, 200);

        var without = NewSim();
        Build(without, Plain, 3);
        Tick(without, 200);

        double boosted = withMilestones.GetResource(Wood);
        double plain = without.GetResource(Wood);

        Assert.True(plain > 0, "Kontrolní vzorek musí něco vyrobit, jinak test nic neměří.");
        Assert.True(boosted > plain * 1.2,
            $"Milník se do výroby nepromítl: {boosted:F0} vs {plain:F0}.");
    }

    // ----- ohlášení milníku -----

    /// <summary>Posbírá zprávy, které simulace vyrobila.</summary>
    private static List<GameNotification> Notifications(Simulation sim)
    {
        var result = new List<GameNotification>();
        while (sim.TryDequeueNotification(out var note))
        {
            result.Add(note);
        }

        return result;
    }

    private static int MilestoneEvents(Simulation sim)
    {
        int count = 0;
        for (int i = 0; i < sim.VisualEvents.Count; i++)
        {
            if (sim.VisualEvents[i].Kind == VisualEventKind.MilestoneReached)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void CrossingTheThresholdIsAnnounced()
    {
        // Bez tohohle byl milník tichý: výroba poskočila a hráč se to dozvěděl,
        // jen když si sám otevřel panel budovy.
        var sim = NewSim();
        Build(sim, Farm, 3);

        Tick(sim, 60);

        var note = Assert.Single(Notifications(sim), n => n.Kind == NotificationKind.BuildingMilestone);
        Assert.Equal("building.farm", note.SubjectKey);
        Assert.True(MilestoneEvents(sim) > 0, "Ohňostroj se má odpálit nad městem.");
    }

    [Fact]
    public void TheSameThresholdIsAnnouncedOnlyOnce()
    {
        var sim = NewSim();
        Build(sim, Farm, 3);
        Tick(sim, 60);
        Notifications(sim);
        sim.VisualEvents.Clear();

        Tick(sim, 200);

        Assert.Empty(Notifications(sim));
        Assert.Equal(0, MilestoneEvents(sim));
    }

    [Fact]
    public void StayingBelowTheThresholdSaysNothing()
    {
        var sim = NewSim();
        Build(sim, Farm, 2);

        Tick(sim, 60);

        Assert.DoesNotContain(Notifications(sim), n => n.Kind == NotificationKind.BuildingMilestone);
    }

    [Fact]
    public void LosingBuildingsIsNotCelebrated()
    {
        // Hra netrestá — a rozhodně o propadu nedělá slávu. Po znovudosažení
        // se ale práh ohlásit musí, jinak by druhá stavba byla tichá.
        var sim = NewSim();
        Build(sim, Farm, 3);
        Tick(sim, 60);
        Notifications(sim);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(2));
        Tick(sim, 60);
        Assert.DoesNotContain(Notifications(sim), n => n.Kind == NotificationKind.BuildingMilestone);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Farm, 40, 40));
        Tick(sim, 60);

        Assert.Contains(Notifications(sim), n => n.Kind == NotificationKind.BuildingMilestone);
    }

    // ----- save/load -----

    [Fact]
    public void TheBonusAppliesRightAfterLoading()
    {
        // Milníky se neukládají (odvodí se z počtu budov). Kdyby se po načtení
        // čekalo na první pomalý přepočet, hráč by chvíli vyráběl pod úrovní.
        var sim = NewSim();
        Build(sim, Farm, 6);
        Tick(sim, 60);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, Content());

        Assert.Equal(6, loaded.MilestoneCount(Farm));
        Assert.Equal(1.5, loaded.MilestoneMultiplier(Farm), 6);
        Assert.Equal(1.5f, loaded.Buildings[0].MilestoneMult, 3);
    }

    [Fact]
    public void LoadingASaveDoesNotReplayOldCelebrations()
    {
        // Jinak by hráče po každém spuštění zasypaly oslavy věcí, které zvládl
        // už minule.
        var sim = NewSim();
        Build(sim, Farm, 6);
        Tick(sim, 60);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, Content());

        Assert.DoesNotContain(Notifications(loaded), n => n.Kind == NotificationKind.BuildingMilestone);
        Assert.Equal(0, MilestoneEvents(loaded));
    }
}
