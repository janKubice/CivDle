using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Háky, na kterých stojí cheat režim pro natáčení.
///
/// <para>Samotný přepínač bydlí v UI vrstvě (nemá co dělat v simulaci a hlavně
/// se neukládá do savu). Simulace mu ale musí poskytnout dvě věci, na kterých
/// to celé stojí: dosypat sklady kdykoli a udržet zrychlenou stavbu. Testuje se
/// to, co by při natáčení bylo vidět jako chyba — že se zrychlení dá plynule
/// obnovovat a že po vypnutí <b>samo odezní</b>.</para>
/// </summary>
public class CheatModeSupportTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(120, 90, 60), StartAmount: 5, BaseStorage: 500),
    };

    private static GameContent Content()
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 10,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Wood,
            buildings: new[] { house });
    }

    private static Simulation World() => new(Content(), new UniformTerrain((byte)1));

    [Fact]
    public void RefillingRepeatedlyKeepsStoragesFull()
    {
        // Cheat režim dosypává dokola. Opakované volání nesmí nic přetéct
        // ani rozbít — je to jeho normální provozní režim, ne výjimka.
        var sim = World();

        for (int i = 0; i < 50; i++)
        {
            sim.DebugFillStorages();
            sim.Tick();
        }

        for (int r = 0; r < sim.ResourceCount; r++)
        {
            Assert.InRange(sim.GetResource(r), 0, sim.GetStorageCap(r) + 0.001);
        }
    }

    [Fact]
    public void ReArmingTheBoostKeepsItAlive()
    {
        // Přesně tohle dělá cheat režim: jakmile zrychlení dojede, nasadí ho
        // znovu. Kdyby se restartovalo dřív, nikdy by nedoběhlo.
        var sim = World();

        for (int second = 0; second < 5; second++)
        {
            if (!sim.DebugBuildBoostActive)
            {
                sim.DebugBoostAutoBuild(100, seconds: 1);
            }

            for (int i = 0; i < (int)Simulation.TicksPerSecond; i++)
            {
                sim.Tick();
            }
        }

        // Po pěti sekundách musí být pořád možné ho nasadit znovu.
        sim.DebugBoostAutoBuild(100, seconds: 1);
        Assert.True(sim.DebugBuildBoostActive);
    }

    [Fact]
    public void TheBoostWearsOffWhenNobodyReArmsIt()
    {
        // Vypnutí cheatu znamená „přestat obnovovat". Kdyby zrychlení viselo
        // dál, zůstala by hra po natáčení rozbitá.
        var sim = World();
        sim.DebugBoostAutoBuild(100, seconds: 1);

        for (int i = 0; i < (int)Simulation.TicksPerSecond + 2; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.DebugBuildBoostActive);
    }

    [Fact]
    public void RefillingDoesNotDisturbTheClock()
    {
        // Dosypání surovin je zásah do zásob, ne do času — jinak by cheat
        // posouval denní dobu a záběr by v jeho průběhu měnil světlo.
        var sim = World();
        long before = sim.TickCount;

        sim.DebugFillStorages();

        Assert.Equal(before, sim.TickCount);
    }
}
