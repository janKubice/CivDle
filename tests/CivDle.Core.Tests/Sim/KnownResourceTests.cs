using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Známé suroviny: hra nesmí prozrazovat obsah, ke kterému se hráč ještě nedostal.
/// Surovinu, kterou nikdy neměl, HUD neukazuje a náhodné odměny (skrýše) ji nedávají.
/// </summary>
public class KnownResourceTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    /// <summary>Obsah: „wood" na startu (známé) + „steel" (na startu nedostupné).</summary>
    private static GameContent TwoResourceContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 10, BaseStorage: 1000),
            new Resource("steel", new RgbColor(110, 120, 126), StartAmount: 0, BaseStorage: 1000),
        };
        return TestContent.Build(biomes, 1, resources);
    }

    [Fact]
    public void StartingResources_AreKnown_OthersAreNot()
    {
        var sim = new Simulation(TwoResourceContent(), Grass());

        Assert.True(sim.IsResourceKnown(0));   // dřevo je na startu
        Assert.False(sim.IsResourceKnown(1));  // ocel hráč nikdy neviděl
        Assert.Equal(1, sim.KnownResourceCount);
    }

    [Fact]
    public void GainingResource_RevealsIt()
    {
        var sim = new Simulation(TwoResourceContent(), Grass());

        sim.AddResource(1, 5);

        Assert.True(sim.IsResourceKnown(1));
        Assert.Equal(2, sim.KnownResourceCount);
    }

    [Fact]
    public void SpendingBackToZero_KeepsItKnown()
    {
        // Jednou odhalená surovina se nesmí z UI zase ztratit, když ji hráč utratí.
        var sim = new Simulation(TwoResourceContent(), Grass());
        sim.AddResource(1, 5);
        sim.AddResource(1, -5);

        Assert.Equal(0, sim.GetResource(1));
        Assert.True(sim.IsResourceKnown(1));
    }

    [Fact]
    public void Discovery_NeverGivesUnknownResource()
    {
        // Skrýše losují jen ze známých surovin — jinak by vysypaly obsah pozdních ér.
        var sim = new Simulation(TwoResourceContent(), Grass(), seed: 4);

        int claimed = 0;
        for (int y = 0; y < 120 && claimed < 12; y++)
        {
            for (int x = 0; x < 120 && claimed < 12; x++)
            {
                if (!sim.IsDiscoveryTile(x, y) || !sim.TryClaimDiscovery(x, y, out int resourceIndex, out _))
                {
                    continue;
                }

                claimed++;
                Assert.Equal(0, resourceIndex); // jediná známá surovina je dřevo
            }
        }

        Assert.True(claimed > 0, "test nenašel žádnou skrýš k vyzvednutí");
    }
}
