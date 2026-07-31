using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Zakázky: krátká smyčka mezi událostmi a úkoly.
///
/// <para>Testuje se to, co z nich dělá důvod něco udělat teď: nabídka se sama
/// objeví, běží jí termín, odevzdání je akce hráče (ne automatika) a vypršení
/// nikoho netrestá. A hlavně — nabídky rostou s městem, aby v pozdní hře
/// nechodily objednávky na dvacet prken.</para>
/// </summary>
public class ContractTests
{
    private const int Wood = 0;
    private const int Food = 1;

    private static ContractCatalog Catalog(
        int slots = 2, double restockSeconds = 1, double scaleGrowth = 1.0, double duration = 10,
        GoalCondition? gate = null)
    {
        var defs = new[]
        {
            new ContractDef("wood_order", Wood, 20, new[] { new ResourceAmount(Food, 30) }, duration),
            new ContractDef("food_order", Food, 25, new[] { new ResourceAmount(Wood, 40) }, duration, gate),
        };

        return new ContractCatalog(
            new ContractBoardConfig(slots, restockSeconds, scaleGrowth, MaxScale: 40),
            new DefRegistry<ContractDef>(defs, c => c.Id, "zakázka"));
    }

    private static Simulation NewSim(ContractCatalog? catalog = null, double startingResources = 500)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), startingResources, BaseStorage: 1_000_000),
            new Resource("food", new RgbColor(1, 1, 1), startingResources, BaseStorage: 1_000_000),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        // Vzestup hned od začátku — jeden z testů ověřuje, že nový svět začíná
        // s čistou nástěnkou.
        var prestige = new PrestigeConfig(
            new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 5);

        var content = TestContent.Build(
            biomes, 1, resources, gameplay: gameplay, prestige: prestige, contracts: catalog ?? Catalog());
        return new Simulation(content, new UniformTerrain(1));
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Najde první místo, na kterém visí nabídka (nebo −1).</summary>
    private static int FirstActive(Simulation sim)
    {
        for (int i = 0; i < sim.ContractSlots.Length; i++)
        {
            if (sim.ContractSlots[i].IsActive)
            {
                return i;
            }
        }

        return -1;
    }

    [Fact]
    public void BoardFillsItselfWithoutThePlayerDoingAnything()
    {
        var sim = NewSim();
        Assert.Equal(-1, FirstActive(sim)); // na začátku prázdno

        Tick(sim, 40);

        Assert.True(FirstActive(sim) >= 0);
        Assert.True(sim.ContractsEnabled);
    }

    [Fact]
    public void TwoSlotsNeverShowTheSameOrderTwice()
    {
        // Dvě stejné nabídky vedle sebe vypadají jako chyba, ne jako nabídka.
        var sim = NewSim();
        Tick(sim, 60);

        var slots = sim.ContractSlots;
        Assert.Equal(2, slots.Length);
        if (slots[0].IsActive && slots[1].IsActive)
        {
            Assert.NotEqual(slots[0].DefIndex, slots[1].DefIndex);
        }
    }

    [Fact]
    public void DeliveringPaysAndFreesTheSlot()
    {
        var sim = NewSim();
        Tick(sim, 40);
        int slot = FirstActive(sim);
        Assert.True(slot >= 0);

        var def = sim.ContractAt(slot)!;
        long demand = sim.ContractSlots[slot].DemandAmount;
        double before = sim.GetResource(def.DemandResourceIndex);
        var reward = sim.ContractReward(slot);
        double rewardBefore = sim.GetResource(reward[0].ResourceIndex);

        Assert.True(sim.TryFulfilContract(slot));

        Assert.Equal(before - demand, sim.GetResource(def.DemandResourceIndex), 3);
        Assert.True(sim.GetResource(reward[0].ResourceIndex) > rewardBefore);
        Assert.Equal(1, sim.ContractsCompleted);
        Assert.False(sim.ContractSlots[slot].IsActive);
    }

    [Fact]
    public void DeliveringNeedsTheGoodsInHand()
    {
        // Odevzdání je akce, ne automatika — a bez zboží prostě nejde.
        var sim = NewSim(startingResources: 0);
        Tick(sim, 40);
        int slot = FirstActive(sim);
        Assert.True(slot >= 0);

        Assert.False(sim.CanFulfilContract(slot));
        Assert.False(sim.TryFulfilContract(slot));
        Assert.Equal(0, sim.ContractsCompleted);
        Assert.True(sim.ContractSlots[slot].IsActive); // nabídka nikam nezmizela
    }

    [Fact]
    public void AnExpiredContractCostsNothing()
    {
        // Vypršení nesmí trestat — hra jinde taky netrestá za nepozornost.
        var sim = NewSim(Catalog(duration: 2));
        Tick(sim, 40);
        int slot = FirstActive(sim);
        Assert.True(slot >= 0);

        double woodBefore = sim.GetResource(Wood);
        double foodBefore = sim.GetResource(Food);
        Tick(sim, 40);

        Assert.Equal(woodBefore, sim.GetResource(Wood), 3);
        Assert.Equal(foodBefore, sim.GetResource(Food), 3);
        Assert.Equal(0, sim.ContractsCompleted);
    }

    [Fact]
    public void OrdersGrowWithEveryDeliveryDone()
    {
        // Bez toho by velkoměsto dostávalo objednávky na dvacet prken.
        var sim = NewSim(Catalog(scaleGrowth: 1.5, duration: 600));
        Tick(sim, 40);
        int slot = FirstActive(sim);
        long firstDemand = sim.ContractSlots[slot].DemandAmount;

        for (int i = 0; i < 5; i++)
        {
            int active = FirstActive(sim);
            if (active >= 0 && sim.CanFulfilContract(active))
            {
                sim.TryFulfilContract(active);
            }

            Tick(sim, 20);
        }

        int later = FirstActive(sim);
        Assert.True(later >= 0);
        Assert.True(sim.ContractsCompleted > 0);
        Assert.True(sim.ContractSlots[later].DemandAmount > firstDemand);
    }

    [Fact]
    public void RewardScalesWithTheOrder()
    {
        var sim = NewSim(Catalog(scaleGrowth: 1.5, duration: 600));
        Tick(sim, 40);
        var early = sim.ContractReward(FirstActive(sim));
        int earlyAmount = early[0].Amount;

        for (int i = 0; i < 5; i++)
        {
            int active = FirstActive(sim);
            if (active >= 0 && sim.CanFulfilContract(active))
            {
                sim.TryFulfilContract(active);
            }

            Tick(sim, 20);
        }

        var later = sim.ContractReward(FirstActive(sim));
        Assert.True(later[0].Amount > earlyAmount);
    }

    [Fact]
    public void OrdersTheCityCannotFillAreNeverOffered()
    {
        // Nabídka, kterou hráč nemůže splnit, je horší než žádná nabídka.
        var gate = new GoalCondition(MetricKind.Population, -1, 1_000_000);
        var sim = NewSim(Catalog(slots: 2, gate: gate));

        Tick(sim, 120);

        for (int i = 0; i < sim.ContractSlots.Length; i++)
        {
            if (sim.ContractAt(i) is { } def)
            {
                Assert.Equal("wood_order", def.Id); // ta za bránou se nikdy neukázala
            }
        }
    }

    [Fact]
    public void AscendingClearsTheBoard()
    {
        var sim = NewSim();
        Tick(sim, 40);
        Assert.True(FirstActive(sim) >= 0);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(-1, FirstActive(sim));
        Assert.Equal(0, sim.ContractsCompleted);
    }

    [Fact]
    public void DisabledCatalogLeavesTheGameAsItWas()
    {
        var sim = NewSim(ContractCatalog.Empty);

        Tick(sim, 200);

        Assert.False(sim.ContractsEnabled);
        Assert.Equal(0, sim.ContractSlots.Length);
    }
}
