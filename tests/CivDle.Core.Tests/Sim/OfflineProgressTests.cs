using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Offline postup: dohnat čas od uložení = odtikat tolik tiků. Deterministické,
/// takže dohon musí sednout na ruční tikání; strop dohonu drží čas na uzdě.
/// </summary>
public class OfflineProgressTests
{
    [Fact]
    public void Apply_EqualsManualTicks()
    {
        var content = TestData.LoadRealContent();
        byte grass = (byte)content.Biomes.IndexOf("grassland");
        int house = content.Buildings.IndexOf("house");

        var offline = new Simulation(content, new UniformTerrain(grass));
        var manual = new Simulation(content, new UniformTerrain(grass));
        offline.TryPlaceBuilding(house, 2, 2);
        manual.TryPlaceBuilding(house, 2, 2);

        // 10 minut = 600 s × 10 Hz = 6000 tiků.
        for (int i = 0; i < 6000; i++)
        {
            manual.Tick();
        }

        var now = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        var summary = OfflineProgress.Apply(offline, now.AddSeconds(-600), now);

        Assert.Equal(600, summary.CreditedSeconds);
        Assert.Equal(manual.Population, offline.Population);
        Assert.Equal(manual.Buildings.Length, offline.Buildings.Length);
        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(manual.GetResource(i), offline.GetResource(i));
        }
    }

    [Fact]
    public void Apply_CapsCreditedTime()
    {
        var content = TestContent.Build();
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        var now = new DateTime(2026, 7, 21, 12, 0, 0, DateTimeKind.Utc);
        var summary = OfflineProgress.Apply(sim, now.AddHours(-100), now);

        Assert.Equal(OfflineProgress.MaxCreditedSeconds, summary.CreditedSeconds);
        Assert.Equal(100 * 3600, summary.ElapsedSeconds); // uplynulý čas se hlásí celý
    }
}
