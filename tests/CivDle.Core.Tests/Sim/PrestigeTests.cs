using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vzestup (prestige): udělení bodů a reset éry, koupě trvalých upgradů a jejich
/// bonusy, prerekvizity a přežití savem (v5). Používá syntetický obsah s nízkou
/// podmínkou Vzestupu, ať se dá odtestovat bez dlouhého růstu populace.
/// </summary>
public class PrestigeTests
{
    // Vzestup dostupný hned (podmínka = startovní populace), body = populace ÷ 5.
    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    private static PrestigeUpgradeDef Upgrade(string id, string effect, double magnitude, int cost, params int[] prereqs) =>
        new(id, effect, magnitude, cost, prereqs);

    private static GameContent PrestigeContent(params PrestigeUpgradeDef[] upgrades) =>
        TestContent.Build(prestige: EarlyAscension, prestigeUpgrades: upgrades);

    private static Simulation NewSim(GameContent content) =>
        new(content, new UniformTerrain((byte)1)); // biom 1 = pevnina

    [Fact]
    public void Ascend_GrantsPoints_IncrementsLevel_ResetsEra()
    {
        var content = PrestigeContent();
        var sim = NewSim(content);
        sim.TryPlaceBuilding(0, 2, 2); // něco postavíme, ať je co resetovat

        Assert.True(sim.CanAscend());
        Assert.Equal(1, sim.PendingAscensionPoints()); // pop 5 ÷ 5

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(1, sim.AscensionLevel);
        Assert.Equal(1, sim.PrestigePoints);
        Assert.Equal(0, sim.Buildings.Length); // éra se zresetovala
    }

    [Fact]
    public void BuyUpgrade_DeductsPoints_RaisesBonus()
    {
        var content = PrestigeContent(Upgrade("industrious", "production_mult", 0.30, 1));
        var sim = NewSim(content);
        sim.TryAscend(); // 1 bod

        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(0, sim.PrestigePoints);
        Assert.True(sim.IsUpgradePurchased(0));
        Assert.Equal(1.30, sim.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void BuyUpgrade_BlockedByPrerequisite()
    {
        var content = PrestigeContent(
            Upgrade("fertile", "growth_mult", 0.35, 1),
            Upgrade("spacious", "housing_mult", 0.40, 1, 0)); // vyžaduje fertile (index 0)
        var sim = NewSim(content);
        sim.TryAscend();

        Assert.Equal(PlacementResult.NotUnlocked, sim.CanBuyUpgrade(1));
        sim.TryBuyUpgrade(0);
        // po koupi prereku (a s dost body z dalšího Vzestupu) už jde koupit
        sim.TryAscend();
        Assert.Equal(PlacementResult.Ok, sim.CanBuyUpgrade(1));
    }

    [Fact]
    public void Harvest_ScalesWithHarvestBonus()
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            new Biome("forest", new RgbColor(20, 80, 20), 0f, IsWater: false,
                DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full, TemperatureRange: ValueRange.Full,
                ClickYield: new ClickYield(0, 2)),
        };
        var content = TestContent.Build(
            biomes: biomes,
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 1000) },
            prestige: EarlyAscension,
            prestigeUpgrades: new[] { Upgrade("sharp", "harvest_mult", 0.50, 1) });
        var sim = NewSim(content);
        sim.TryAscend();
        sim.TryBuyUpgrade(0); // +50 % sběr

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount));
        Assert.Equal(3, amount); // round(2 × 1.5)
    }

    [Fact]
    public void SaveRoundtrip_PersistsPrestige()
    {
        var content = PrestigeContent(Upgrade("industrious", "production_mult", 0.30, 1));
        var sim = NewSim(content);
        sim.TryAscend();
        sim.TryBuyUpgrade(0);

        var metadata = new SaveMetadata(7, "s", "test", DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(1, loaded.AscensionLevel);
        Assert.Equal(0, loaded.PrestigePoints);
        Assert.True(loaded.IsUpgradePurchased(0));
        Assert.Equal(1.30, loaded.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void RepeatableUpgrade_CompoundsAndGetsPricier()
    {
        // Jednorázové upgrady daly stromu pevný strop a po pár Vzestupech
        // nebylo co kupovat. Opakovatelný uzel je nekonečná osa — a skládá se
        // MOCNINOU, takže tři úrovně po +50 % jsou ×3,375, ne ×2,5.
        var content = TestContent.Build(
            prestige: EarlyAscension,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef(
                    "industrious", "production_mult", 0.5, Cost: 1,
                    PrerequisiteIndices: Array.Empty<int>(), MaxLevel: 3, CostGrowth: 2.0),
            });

        var sim = NewSim(content);
        for (int i = 0; i < 7; i++)
        {
            sim.TryAscend(); // sedm Vzestupů = sedm bodů (populace 5 ÷ 5)
        }

        Assert.Equal(1, sim.UpgradeCost(0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(1.5, sim.Bonuses.ProductionMult, 3);

        Assert.Equal(2, sim.UpgradeCost(0)); // každá další dvojnásobně dražší
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(2.25, sim.Bonuses.ProductionMult, 3);

        Assert.Equal(4, sim.UpgradeCost(0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(3.375, sim.Bonuses.ProductionMult, 3);

        // Na maximu už nejde koupit další.
        Assert.True(sim.IsUpgradeMaxed(0));
        Assert.Equal(PlacementResult.Occupied, sim.TryBuyUpgrade(0));
    }

    [Fact]
    public void UpgradeLevelsSurviveASave()
    {
        // Sav zapisuje ID jednou za každou úroveň, takže formát zůstal stejný
        // a starý sav se načte jako úroveň 1.
        var content = TestContent.Build(
            prestige: EarlyAscension,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef(
                    "industrious", "production_mult", 0.5, Cost: 1,
                    PrerequisiteIndices: Array.Empty<int>(), MaxLevel: 5, CostGrowth: 1.0),
            });

        var sim = NewSim(content);
        for (int i = 0; i < 3; i++)
        {
            sim.TryAscend();
        }

        sim.TryBuyUpgrade(0);
        sim.TryBuyUpgrade(0);
        sim.TryBuyUpgrade(0);
        Assert.Equal(3, sim.UpgradeLevel(0));

        var metadata = new SaveMetadata(7, "s", "test", DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(3, loaded.UpgradeLevel(0));
        Assert.Equal(sim.Bonuses.ProductionMult, loaded.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void PrestigePointsHaveDiminishingReturns()
    {
        // Při lineárním výnosu se vždycky vyplatilo hrát dál a rozhodnutí
        // „resetnout teď?" vůbec nevzniklo. S odmocninou dvojnásobná populace
        // nedá dvojnásobek bodů.
        // Body ze zásoby suroviny — ta se dá z testu nastavit přesně, na rozdíl
        // od populace, která roste vlastním tempem.
        var sqrt = new PrestigeConfig(
            new GoalCondition(MetricKind.Population, -1, 5),
            MetricKind.ResourceStock, 0, PointsDivisor: 1,
            RequirementGrowth: 1.0, PointsExponent: 0.5);

        var content = TestContent.Build(prestige: sqrt);
        var sim = NewSim(content);

        sim.AddResource(0, 100 - sim.GetResource(0));
        long atHundred = sim.PendingAscensionPoints();

        sim.AddResource(0, 300);
        long atFourHundred = sim.PendingAscensionPoints();

        Assert.Equal(10, atHundred);       // sqrt(100)
        Assert.Equal(20, atFourHundred);   // čtyřnásobná zásoba = jen dvojnásobek bodů
    }

    [Fact]
    public void ResearchAndPrestigeMultiplyEachOther()
    {
        // Pravidlo skládání: uvnitř kategorie součet, mezi kategoriemi součin.
        // Dřív padalo všechno do jednoho součtu a strop celého stromu byl ×4.
        var content = TestContent.Build(
            prestige: EarlyAscension,
            prestigeUpgrades: new[]
            {
                new PrestigeUpgradeDef(
                    "industrious", "production_mult", 1.0, Cost: 1,
                    PrerequisiteIndices: Array.Empty<int>()),
            },
            techs: new[]
            {
                new TechDef("mechanics", new[] { new ResourceAmount(0, 1) }, Array.Empty<int>(),
                    Array.Empty<int>(), "production_mult", 1.0),
            });

        var sim = NewSim(content);
        sim.TryAscend();           // 1 bod
        sim.TryBuyUpgrade(0);      // ×2 z prestiže
        sim.AddResource(0, 1000);
        sim.TryResearch(0);        // ×2 z výzkumu

        // Součin, ne součet: ×4, ne ×3.
        Assert.Equal(4.0, sim.Bonuses.ProductionMult, 3);
    }
}
