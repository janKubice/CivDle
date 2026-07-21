using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Skrýše na mapě (objevování): rozmístění je deterministické z pozice, vyzvednutí
/// dá surovinu jednou za dlaždici a přežije save (v7).
/// </summary>
public class DiscoveryTests
{
    private static (int X, int Y) FindDiscovery(Simulation sim)
    {
        for (int y = -60; y <= 60; y++)
        {
            for (int x = -60; x <= 60; x++)
            {
                if (sim.IsDiscoveryTile(x, y))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("V okolí se nenašla žádná skrýš.");
    }

    [Fact]
    public void Claim_GivesResource_Once()
    {
        var sim = new Simulation(TestContent.Build(), new UniformTerrain((byte)1));
        var (x, y) = FindDiscovery(sim);

        Assert.False(sim.IsDiscoveryClaimed(x, y));
        double before = sim.GetResource(0); // jediná surovina v test-obsahu
        Assert.True(sim.TryClaimDiscovery(x, y, out int resourceIndex, out int amount));
        Assert.InRange(amount, 20, 59);
        Assert.Equal(before + amount, sim.GetResource(resourceIndex));
        Assert.True(sim.IsDiscoveryClaimed(x, y));

        Assert.False(sim.TryClaimDiscovery(x, y, out _, out _)); // podruhé už nic
    }

    [Fact]
    public void SaveRoundtrip_KeepsClaimed()
    {
        var content = TestContent.Build();
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        var (x, y) = FindDiscovery(sim);
        sim.TryClaimDiscovery(x, y, out _, out _);

        var metadata = new SaveMetadata(1, "s", "test", System.DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.IsDiscoveryClaimed(x, y));
    }
}
