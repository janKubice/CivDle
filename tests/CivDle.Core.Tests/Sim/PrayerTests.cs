using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Modlitby: jediné místo ve hře, kde hráč vědomě riskuje.
///
/// <para>Testuje se to, na čem ta mechanika stojí: víra se obětuje PŘEDEM
/// (jinak by to nebyl hazard), silnější modlitba je dražší a méně jistá, a
/// výsledek je deterministický — jinak by šel „přetočit" načtením savu.</para>
/// </summary>
public class PrayerTests
{
    private const int Faith = 1;

    private static GameContent Content(params PrayerDef[] prayers)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 10_000),
            new Resource("faith", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 10_000),
        };

        var catalog = new FaithCatalog(
            Faith, new DefRegistry<PrayerDef>(prayers, p => p.Id, "modlitba", allowEmpty: true));

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = 0, FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0.12,
        };

        return TestContent.Build(biomes, 1, resources, gameplay: gameplay, faith: catalog);
    }

    /// <summary>Modlitba, která vyjde vždycky (šance 1) — testuje se účinek, ne los.</summary>
    private static PrayerDef Sure(string id, string effect, double magnitude = 10, int radius = 0) =>
        new(id, effect, BaseCost: 10, BaseChance: 1.0, ChanceFalloff: 0.0, magnitude, radius);

    [Fact]
    public void FaithIsSpentEvenWhenTheAnswerIsSilence()
    {
        // Obětuje se předem — v tom je ten hazard. Kdyby se platilo za výsledek,
        // nebylo by co riskovat.
        var never = new PrayerDef("void", "bless_harvest", BaseCost: 10, BaseChance: 0.0001, ChanceFalloff: 0, Magnitude: 1, RadiusTiles: 0);
        var sim = new Simulation(Content(never), new UniformTerrain(1));
        double before = sim.GetResource(Faith);

        var outcome = sim.TryPray(0, 1, 0, 0);

        Assert.Equal(PrayerOutcome.Unanswered, outcome);
        Assert.Equal(before - 10, sim.GetResource(Faith), 3);
    }

    [Fact]
    public void AnAnsweredHarvestPrayerFillsTheStorehouse()
    {
        var sim = new Simulation(Content(Sure("plenty", "bless_harvest", magnitude: 50)), new UniformTerrain(1));

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 0, 0));

        Assert.Equal(50, sim.GetResource(0), 3);
    }

    [Fact]
    public void WithoutEnoughFaithNothingHappens()
    {
        var sim = new Simulation(Content(Sure("plenty", "bless_harvest")), new UniformTerrain(1));
        sim.AddResource(Faith, -sim.GetResource(Faith));

        Assert.Equal(PrayerOutcome.NotEnoughFaith, sim.TryPray(0, 1, 0, 0));
        Assert.Equal(0, sim.GetResource(0), 3);
    }

    [Fact]
    public void AStrongerPrayerCostsMoreAndIsLessLikely()
    {
        // Tohle je celé to rozhodnutí: malá jistota, nebo drahý hazard.
        var prayer = new PrayerDef("x", "bless_harvest", BaseCost: 20, BaseChance: 0.9, ChanceFalloff: 0.2, Magnitude: 1, RadiusTiles: 0);

        Assert.True(prayer.CostAt(3) > prayer.CostAt(1));
        Assert.True(prayer.ChanceAt(3) < prayer.ChanceAt(1));
    }

    [Fact]
    public void EvenAHopelessPrayerKeepsASpark()
    {
        // Nulová šance by z modlitby udělala jen zahození surovin.
        var prayer = new PrayerDef("x", "bless_harvest", BaseCost: 1, BaseChance: 0.2, ChanceFalloff: 0.9, Magnitude: 1, RadiusTiles: 0);

        Assert.True(prayer.ChanceAt(Simulation.MaxPrayerStrength) >= 0.05);
    }

    [Fact]
    public void TheSameWorldPraysTheSameWay()
    {
        // Deterministicky ze seedu a tiku — jinak by šel výsledek přetáčet
        // znovunačtením savu.
        var content = Content(new PrayerDef("x", "bless_harvest", BaseCost: 10, BaseChance: 0.5, ChanceFalloff: 0, Magnitude: 1, RadiusTiles: 0));
        var first = new Simulation(content, new UniformTerrain(1));
        var second = new Simulation(content, new UniformTerrain(1));

        Assert.Equal(first.TryPray(0, 1, 7, 9), second.TryPray(0, 1, 7, 9));
    }

    [Fact]
    public void AnUnknownEffectIsSilentlySkipped()
    {
        // Data smí předběhnout kód; z pohledu hráče je to nevyslyšená modlitba.
        var sim = new Simulation(Content(Sure("future", "nejaky_budouci_zazrak")), new UniformTerrain(1));

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 0, 0));
        Assert.Equal(0, sim.GetResource(0), 3);
    }

    [Fact]
    public void ARainBlessingRunsOutOnItsOwn()
    {
        var sim = new Simulation(Content(Sure("rain", "bless_rain", magnitude: 0.5)), new UniformTerrain(1));

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 0, 0));
        Assert.True(sim.RainBlessingActive);
        Assert.True(sim.RainFoodMult > 1.0);

        for (int i = 0; i < (int)Simulation.TicksPerSecond * 130; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.RainBlessingActive);
        Assert.Equal(1.0, sim.RainFoodMult, 3);
    }

    [Fact]
    public void AMeteorLevelsWhatStandsNearTheTarget()
    {
        var sim = new Simulation(Content(Sure("meteor", "smite_meteor", radius: 4)), new UniformTerrain(1));
        sim.AddResource(0, 500); // na stavbu (výchozí budova stojí surovinu 0)
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 20, 20));
        Assert.Single(sim.Buildings.ToArray());

        Assert.Equal(PrayerOutcome.Answered, sim.TryPray(0, 1, 20, 20));

        Assert.Empty(sim.Buildings.ToArray());
    }

    [Fact]
    public void WithoutFaithInTheDataPrayingIsUnavailable()
    {
        // Víra je volitelná mechanika — hra bez faith.json musí běžet dál.
        var sim = new Simulation(TestContent.Build(), new UniformTerrain(1));

        Assert.False(sim.FaithEnabled);
        Assert.Equal(PrayerOutcome.Unavailable, sim.TryPray(0, 1, 0, 0));
    }

    [Fact]
    public void RealContent_HasPrayersOnBothSides()
    {
        // Víra musí umět žehnat i trestat — jinak je to jen další zdroj bonusů.
        var faith = TestData.LoadRealContent().Faith;

        Assert.True(faith.IsEnabled);
        Assert.Contains(faith.Prayers.All, p => p.Effect.StartsWith("bless_", StringComparison.Ordinal));
        Assert.Contains(faith.Prayers.All, p => p.Effect.StartsWith("smite_", StringComparison.Ordinal));
    }
}
