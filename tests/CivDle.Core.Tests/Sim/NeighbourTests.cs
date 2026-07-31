using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Sousedé: z anonymních karavan se staly konkrétní obchodní partnery, kteří se
/// vracejí a pamatují si vztah.
///
/// <para>Testuje se to, co z toho dělá dlouhý horizont: vztah roste obchodem,
/// lepší vztah líp platí, přednost dostává zanedbaný soused (aby hráč poznal
/// celé okolí) — a prázdný katalog nechá hru jako dřív.</para>
/// </summary>
public class NeighbourTests
{
    private const int Wood = 0;

    private static NeighbourCatalog Catalog(int tradesPerLevel = 2, double bonusPerLevel = 0.5, int maxLevel = 3)
    {
        var neighbours = new[]
        {
            new NeighbourDef("bay", new RgbColor(120, 160, 200)),
            new NeighbourDef("forges", new RgbColor(200, 130, 90)),
        };

        return new NeighbourCatalog(
            new DefRegistry<NeighbourDef>(neighbours, n => n.Id, "soused"),
            tradesPerLevel, bonusPerLevel, maxLevel);
    }

    private static Simulation NewSim(NeighbourCatalog? catalog = null)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        var content = TestContent.Build(
            biomes, 1, resources, gameplay: gameplay, neighbours: catalog ?? Catalog());
        return new Simulation(content, new UniformTerrain(1));
    }

    [Fact]
    public void StrangersPayTheBaseRate()
    {
        var sim = NewSim();

        int paid = sim.CompleteCaravan(neighbourIndex: 0, Wood, basePayout: 100);

        Assert.Equal(100, paid);
        Assert.Equal(1, sim.NeighbourTrades(0));
    }

    [Fact]
    public void TradingRaisesTheStandingAndThePay()
    {
        // Tohle je celý smysl sousedů: obchod se vyplácí líp, čím dýl spolu
        // obchodujete. Bez toho jsou karavany zas jen anonymní výplata.
        var sim = NewSim();
        sim.CompleteCaravan(0, Wood, 100);
        sim.CompleteCaravan(0, Wood, 100); // dva obchody → stupeň 1

        Assert.Equal(1, sim.NeighbourLevel(0));

        int paid = sim.CompleteCaravan(0, Wood, 100);
        Assert.True(paid > 100);
    }

    [Fact]
    public void StandingHasACeiling()
    {
        var sim = NewSim(Catalog(tradesPerLevel: 1, bonusPerLevel: 0.5, maxLevel: 2));
        for (int i = 0; i < 20; i++)
        {
            sim.CompleteCaravan(0, Wood, 100);
        }

        Assert.Equal(2, sim.NeighbourLevel(0));
        Assert.Equal(200, sim.CompleteCaravan(0, Wood, 100)); // 1 + 2 × 0.5
    }

    [Fact]
    public void WarmingUpIsAnnounced()
    {
        var sim = NewSim();
        sim.CompleteCaravan(0, Wood, 100);
        while (sim.TryDequeueNotification(out _))
        {
            // vyprázdnit, ať se počítá jen to, co přijde po povýšení vztahu
        }

        sim.CompleteCaravan(0, Wood, 100); // druhý obchod → stupeň 1

        bool announced = false;
        while (sim.TryDequeueNotification(out var note))
        {
            announced |= note.TitleKey == "toast.neighbourLevel";
        }

        Assert.True(announced);
    }

    [Fact]
    public void TheNeglectedNeighbourGoesNext()
    {
        // Bez tohohle by hráč obchodoval pořád s tím samým a ostatní by zůstali
        // navždy cizinci. Takhle pozná celé okolí.
        var sim = NewSim();
        Assert.Equal(0, sim.PickNeighbour());

        sim.CompleteCaravan(0, Wood, 100);

        Assert.Equal(1, sim.PickNeighbour());
    }

    [Fact]
    public void ThePayoutStillLandsInTheStore()
    {
        var sim = NewSim();
        double before = sim.GetResource(Wood);

        int paid = sim.CompleteCaravan(0, Wood, 60);

        Assert.Equal(before + paid, sim.GetResource(Wood), 3);
    }

    [Fact]
    public void AnAnonymousCaravanStillPays()
    {
        // Data bez sousedů (starší obsah) musí fungovat jako dřív.
        var sim = NewSim(NeighbourCatalog.Empty);

        int paid = sim.CompleteCaravan(neighbourIndex: -1, Wood, basePayout: 40);

        Assert.Equal(40, paid);
        Assert.False(sim.NeighboursEnabled);
        Assert.Equal(-1, sim.PickNeighbour());
    }
}
