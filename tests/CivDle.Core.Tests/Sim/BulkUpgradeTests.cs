using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Hromadné vylepšení: jedním kliknutím povýšit všechny stejné budovy ve
/// čtvrti.
///
/// <para>Řeší nejotravnější klikání ve hře — u čtyřiceti domků čtyřicet
/// kliknutí, z nichž ani jedno není rozhodnutí. Testy hlídají to, co se
/// u hromadných akcí kazí: že se něco přeskočí, něco udělá dvakrát, nebo že
/// se při došlých surovinách neudělá vůbec nic.</para>
/// </summary>
public class BulkUpgradeTests
{
    [Fact]
    public void UpgradingAllReachesEveryHouse()
    {
        var (sim, content) = World();
        int house = Index(content, "house");
        var placed = PlaceRow(sim, house, count: 6);

        sim.DebugFillStorages();
        int upgraded = sim.TryUpgradeAllLike(placed[0]);

        Assert.Equal(6, upgraded);
        foreach (var building in sim.Buildings.ToArray())
        {
            Assert.NotEqual(house, building.DefIndex);
        }
    }

    [Fact]
    public void OnlyTheSameKindIsTouched()
    {
        // Vylepšit "všechny domy" nesmí sáhnout na sklad vedle nich.
        var (sim, content) = World();
        int house = Index(content, "house");
        int granary = Index(content, "granary");

        var placed = PlaceRow(sim, house, count: 3);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(granary, 40, 40));

        sim.DebugFillStorages();
        sim.TryUpgradeAllLike(placed[0]);

        int granaries = 0;
        foreach (var building in sim.Buildings.ToArray())
        {
            if (building.DefIndex == granary)
            {
                granaries++;
            }
        }

        Assert.Equal(1, granaries);
    }

    [Fact]
    public void ThePreviewMatchesWhatActuallyHappens()
    {
        // Tlačítko slibuje počet i cenu dopředu. Kdyby slib neseděl,
        // byl by horší než žádný.
        var (sim, content) = World();
        int house = Index(content, "house");
        var placed = PlaceRow(sim, house, count: 5);
        sim.DebugFillStorages();

        var (count, cost) = sim.PreviewUpgradeAll(placed[0]);
        Assert.Equal(5, count);

        var unit = content.Buildings[house].UpgradeCost;
        for (int i = 0; i < unit.Count; i++)
        {
            Assert.Equal(unit[i].Amount * 5, cost[i].Amount);
            Assert.Equal(unit[i].ResourceIndex, cost[i].ResourceIndex);
        }

        Assert.Equal(count, sim.TryUpgradeAllLike(placed[0]));
    }

    [Fact]
    public void RunningOutOfResourcesStillUpgradesWhatItCan()
    {
        // Částečný výsledek je lepší než odmítnutí: hráč vidí, že se něco
        // stalo, a doplní zbytek. Odmítnutí vypadá jako rozbité tlačítko.
        var (sim, content) = World();
        int house = Index(content, "house");
        var placed = PlaceRow(sim, house, count: 8);

        // Suroviny přesně na dvě vylepšení.
        var unit = content.Buildings[house].UpgradeCost;
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            sim.AddResource(i, -sim.GetResource(i));
        }

        foreach (var need in unit)
        {
            sim.AddResource(need.ResourceIndex, need.Amount * 2);
        }

        int upgraded = sim.TryUpgradeAllLike(placed[0]);

        Assert.InRange(upgraded, 1, 2);
    }

    [Fact]
    public void NothingToUpgradeIsNotAnError()
    {
        var (sim, content) = World();
        // Sklad se vylepšit nedá — nemá na co.
        int granary = Index(content, "granary");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(granary, 3, 3));
        sim.DebugFillStorages();

        var (count, cost) = sim.PreviewUpgradeAll(0);

        Assert.Equal(0, count);
        Assert.Empty(cost);
        Assert.Equal(0, sim.TryUpgradeAllLike(0));
    }

    [Fact]
    public void AnInvalidIndexIsSurvivable()
    {
        // UI si drží index budovy; po zbourání může ukazovat mimo.
        var (sim, _) = World();

        Assert.Equal(0, sim.TryUpgradeAllLike(-1));
        Assert.Equal(0, sim.TryUpgradeAllLike(9999));
        Assert.Equal(0, sim.PreviewUpgradeAll(9999).Count);
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        return (sim, content);
    }

    private static int Index(GameContent content, string id)
    {
        Assert.True(content.Buildings.TryIndexOf(id, out int index), id);
        return index;
    }

    /// <summary>Postaví řadu budov vedle sebe a vrátí jejich indexy.</summary>
    private static List<int> PlaceRow(Simulation sim, int defIndex, int count)
    {
        var placed = new List<int>();
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, 2 + i * 2, 2));
            placed.Add(sim.Buildings.Length - 1);
        }

        return placed;
    }
}
