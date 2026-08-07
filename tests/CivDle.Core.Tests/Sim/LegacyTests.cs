using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Odkaz: druhá prestižní vrstva.
///
/// <para>Testuje se přesně to, kvůli čemu vrstva vznikla — že jde hlouběji než
/// Vzestup (smaže i jeho upgrady), že za to platí měnou, která zrychluje
/// <b>samotné vzestupování</b>, a že sleva na práh má dno. Bez posledního bodu
/// by se z Vzestupu po pár Odkazech stalo tlačítko.</para>
/// </summary>
public class LegacyTests
{
    private const int Wood = 0;

    /// <summary>Kolik dřeva stojí jeden Vzestup — základ, ze kterého se počítá i sleva.</summary>
    private const int AscendCost = 1000;

    /// <summary>
    /// Vzestup na zásobu dřeva, ne na populaci: dřevo jde nasadit
    /// (<see cref="Simulation.AddResource"/>), takže test řídí, kdy se vzestupuje.
    /// Práh nesmí růst, jinak by druhý Vzestup v testu nešel.
    /// </summary>
    private static PrestigeConfig Prestige => new(
        new GoalCondition(MetricKind.ResourceStock, Wood, AscendCost),
        MetricKind.ResourceStock,
        PointsParam: Wood,
        PointsDivisor: 100,
        RequirementGrowth: 1.0,
        PointsExponent: 1.0);

    /// <summary>Odkaz od 2 Vzestupů, body = počet Vzestupů (lineárně, ať se to dá počítat v hlavě).</summary>
    private static LegacyConfig Legacy(double requirementGrowth = 2.0) => new(
        new GoalCondition(MetricKind.AscensionLevel, -1, 2),
        requirementGrowth,
        MetricKind.AscensionLevel,
        PointsParam: -1,
        PointsDivisor: 1,
        PointsExponent: 1.0);

    private static PrestigeUpgradeDef LegacyUpgrade(
        string id, string effect, double magnitude, int cost = 1, int maxLevel = 40, double costGrowth = 1.0) =>
        new(id, effect, magnitude, cost, Array.Empty<int>(), maxLevel, costGrowth, KeyPrefix: "legacy");

    private static Simulation NewSim(GameContent content) => new(content, new UniformTerrain(1));

    /// <summary>Vytlačí simulaci na daný počet Vzestupů.</summary>
    private static void AscendTo(Simulation sim, int level)
    {
        while (sim.AscensionLevel < level)
        {
            sim.AddResource(Wood, AscendCost);
            Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        }
    }

    [Fact]
    public void LeavingALegacyWipesAscensionsAndTheirUpgrades()
    {
        // Tohle je celý smysl vrstvy: je to HLUBŠÍ řez než Vzestup. Kdyby si
        // hráč upgrady nechal, byl by Odkaz jen bonus zdarma a rozhodnutí
        // „udělat ho teď, nebo ještě ne" by vůbec nevzniklo.
        var content = TestContent.Build(
            prestige: Prestige,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef("industrious", "production_mult", 0.5, 1, Array.Empty<int>()),
            },
            legacy: Legacy(),
            legacyUpgrades: new[] { LegacyUpgrade("memory", "ascension_points_mult", 0.5) });

        var sim = NewSim(content);
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(1, sim.UpgradeLevel(0));

        Assert.True(sim.CanLeaveLegacy());
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());

        Assert.Equal(0, sim.AscensionLevel);
        Assert.Equal(0, sim.PrestigePoints);
        Assert.Equal(0, sim.UpgradeLevel(0));
        Assert.Equal(1, sim.LegacyDepth);
        Assert.Equal(2, sim.LegacyPoints); // body = počet Vzestupů
    }

    [Fact]
    public void LegacyIsNotOfferedBeforeTheFirstAscension()
    {
        // Vrstva nad mechanikou, kterou hráč ještě nezná, je jen matoucí tlačítko.
        var sim = NewSim(TestContent.Build(prestige: Prestige, legacy: Legacy()));

        Assert.False(sim.LegacyAvailable);
        Assert.False(sim.CanLeaveLegacy());
        Assert.Equal(PlacementResult.NotEnoughResources, sim.TryLeaveLegacy());
    }

    [Fact]
    public void EachLegacyIsHarderThanTheLast()
    {
        var sim = NewSim(TestContent.Build(
            prestige: Prestige, legacy: Legacy(requirementGrowth: 2.0)));

        Assert.Equal(2, sim.LegacyRequirement());
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());

        Assert.Equal(4, sim.LegacyRequirement());
    }

    [Fact]
    public void UpgradesMultiplyAscensionPoints()
    {
        // Hlavní osa vrstvy: neposiluje výrobu, ale samotné vzestupování.
        var content = TestContent.Build(
            prestige: Prestige,
            legacy: Legacy(),
            legacyUpgrades: new[] { LegacyUpgrade("memory", "ascension_points_mult", 1.0, maxLevel: 5) });

        var sim = NewSim(content);
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());

        sim.AddResource(Wood, AscendCost);
        long before = sim.PendingAscensionPoints();
        Assert.True(before > 0);

        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0)); // ×2
        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0)); // ×4 (skládá se mocninou)

        Assert.Equal(before * 4, sim.PendingAscensionPoints());
    }

    [Fact]
    public void UpgradesLowerTheAscensionThresholdButNeverBelowTheFloor()
    {
        // Sleva bez dna by z Vzestupu udělala formalitu na jedno kliknutí.
        var content = TestContent.Build(
            prestige: Prestige,
            legacy: Legacy(),
            legacyUpgrades: new[] { LegacyUpgrade("paths", "ascension_discount", 1.0, maxLevel: 40) });

        var sim = NewSim(content);
        Assert.Equal(AscendCost, sim.AscensionRequirement());

        // Dvacet Vzestupů = dvacet bodů Odkazu, tedy dvacet úrovní slevy.
        AscendTo(sim, 20);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());

        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0));
        Assert.Equal(AscendCost / 2, sim.AscensionRequirement());

        // Dvacet úrovní by práh srazilo na miliontinu; dno je 5 % původního.
        while (sim.CanBuyLegacyUpgrade(0) == PlacementResult.Ok)
        {
            sim.TryBuyLegacyUpgrade(0);
        }

        Assert.Equal((long)(AscendCost * 0.05), sim.AscensionRequirement());
    }

    [Fact]
    public void OrdinaryEffectsAreTheirOwnMultiplierCategory()
    {
        // Odkaz je čtvrtá kategorie: násobí se s Vzestupem, nesčítá se do něj.
        var content = TestContent.Build(
            prestige: Prestige,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef("industrious", "production_mult", 1.0, 1, Array.Empty<int>()),
            },
            legacy: Legacy(),
            legacyUpgrades: new[] { LegacyUpgrade("forge", "production_mult", 1.0, maxLevel: 5) });

        var sim = NewSim(content);
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());
        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0));

        Assert.Equal(2.0, sim.Bonuses.ProductionMult, 3);

        AscendTo(sim, 1);
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));

        // ×2 z Odkazu a ×2 z Vzestupu = ×4, ne ×3.
        Assert.Equal(4.0, sim.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void LegacySurvivesSaveAndLoad()
    {
        var content = TestContent.Build(
            prestige: Prestige,
            legacy: Legacy(),
            legacyUpgrades: new[] { LegacyUpgrade("memory", "ascension_points_mult", 0.5, maxLevel: 10) });

        var sim = NewSim(content);
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());
        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0));

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(1, loaded.LegacyDepth);
        Assert.Equal(sim.LegacyPoints, loaded.LegacyPoints);
        Assert.Equal(2, loaded.LegacyLevel(0));
    }

    [Fact]
    public void RepeatableLegacyUpgradesGetMoreExpensive()
    {
        var content = TestContent.Build(
            prestige: Prestige,
            legacy: Legacy(),
            legacyUpgrades: new[]
            {
                LegacyUpgrade("memory", "ascension_points_mult", 0.5, cost: 2, maxLevel: 10, costGrowth: 2.0),
            });

        var sim = NewSim(content);
        AscendTo(sim, 2);
        Assert.Equal(PlacementResult.Ok, sim.TryLeaveLegacy());

        Assert.Equal(2, sim.LegacyCost(0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuyLegacyUpgrade(0));
        Assert.Equal(4, sim.LegacyCost(0));
    }
}
