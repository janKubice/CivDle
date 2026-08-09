using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Strop měřítka (populationCap stupně Vzestupu).
///
/// <para>Hráč to popsal jako „na 25 tisících se to zastavilo a bylo to
/// antiklimatické". Dvě příčiny: strop patřil <b>stupni</b>, takže na posledním
/// se zastavil napořád a mezi stupni se nehnul vůbec — a hlavně o něm hra
/// mlčela, takže zastavený růst vypadal jako porucha, ne jako pobídka.</para>
/// </summary>
public class ScaleCapTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 1_000_000, BaseStorage: 10_000_000),
    };

    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    /// <summary>Žebřík dvou stupňů se skokem ×100 — z něj se bere i růst nad rámec.</summary>
    private static readonly AscensionTierDef[] Tiers =
    {
        new("village", 0, 20, Array.Empty<int>()),
        new("city", 1, 2_000, Array.Empty<int>()),
    };

    private static GameContent Content()
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 1_000_000,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Planks,
            buildings: new[] { house },
            prestige: EarlyAscension,
            ascensionTiers: Tiers);
    }

    private static Simulation World(GameContent content) =>
        new(content, new UniformTerrain((byte)1));

    /// <summary>Vzestoupí zadaný početkrát (podmínka je splněná od začátku).</summary>
    private static void Ascend(Simulation sim, int times)
    {
        for (int i = 0; i < times; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        }
    }

    [Fact]
    public void TheCapFollowsTheLadderWhileItLasts()
    {
        var sim = World(Content());

        Assert.Equal(20, sim.PopulationCap);
        Ascend(sim, 1);
        Assert.Equal(2_000, sim.PopulationCap);
    }

    [Fact]
    public void EveryFurtherAscensionStillRaisesIt()
    {
        // Jádro chyby: za posledním stupněm žebříku se strop zastavil napořád.
        var sim = World(Content());
        Ascend(sim, 1);
        double atLastTier = sim.PopulationCap;

        Ascend(sim, 1);

        Assert.True(sim.PopulationCap > atLastTier,
            $"strop se po dalším Vzestupu nehnul z {atLastTier}");
    }

    [Fact]
    public void TheGrowthComesFromTheData()
    {
        // Poměr posledních dvou stupňů je ×100 — o tolik má růst i dál.
        var sim = World(Content());
        Ascend(sim, 2);

        Assert.Equal(2_000 * 100.0, sim.PopulationCap, 3);
    }

    [Fact]
    public void HittingTheCapIsAnnouncedOnce()
    {
        var content = Content();
        var sim = World(content);
        sim.TryPlaceBuilding(0, 5, 5); // bydlení nad strop, ať rozhoduje měřítko
        while (sim.TryDequeueNotification(out _))
        {
            // vyprázdnit, co napadalo ze stavby
        }

        for (int i = 0; i < 4000 && !sim.IsAtScaleCap; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsAtScaleCap, "populace nedorostla na strop");

        bool announced = false;
        while (sim.TryDequeueNotification(out var note))
        {
            announced |= note.TitleKey == "toast.scaleCapped";
        }

        Assert.True(announced, "dosažení stropu se hráči nikde neřeklo");

        // A podruhé už ne — z pobídky by byla otrava.
        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        while (sim.TryDequeueNotification(out var note))
        {
            Assert.NotEqual("toast.scaleCapped", note.TitleKey);
        }
    }

    [Fact]
    public void AtTheCapTheGovernorStopsAskingForHousing()
    {
        // Další dům nikoho nepřivede; guvernér má dělat něco jiného.
        var content = Content();
        var sim = World(content);
        sim.TryPlaceBuilding(0, 5, 5);
        for (int i = 0; i < 4000 && !sim.IsAtScaleCap; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsAtScaleCap);
        Assert.False(new GovernorNeeds(content).NeedsHousing(sim));
    }

    [Fact]
    public void BelowTheCapHousingIsStillANeed()
    {
        var content = Content();
        var sim = World(content);

        // Bez jediného domu je bydlení jen základní tábor — populace na něj dosáhne.
        for (int i = 0; i < 2000; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.IsAtScaleCap, "test má smysl jen pod stropem měřítka");
        Assert.True(new GovernorNeeds(content).NeedsHousing(sim));
    }
}
