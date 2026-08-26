using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Plán guvernéra na sídlo: „město A těžba, město B zemědělství".
///
/// <para>Hlídají se tři věci. Vlastní plán musí <b>nahradit</b> říšský, ne se
/// s ním sčítat — jinak by se říšský zákaz nedal v jednom městě povolit a celá
/// mechanika by nedávala smysl. Plán se musí vázat na <b>jméno</b>, ne na
/// pořadí sídla, protože pořadí se mění s každým postaveným domem. A musí
/// přežít save.</para>
/// </summary>
public class SettlementPlanTests
{
    [Fact]
    public void WithoutItsOwnPlanASettlementFollowsTheEmpire()
    {
        var (sim, _) = City();

        int name = sim.Settlements[0].NameIndex;
        Assert.False(sim.HasOwnPlan(name));
        Assert.Same(sim.Plan, sim.PlanForSettlement(name));

        sim.Plan.SetCategoryAllowed("test", false);
        Assert.False(sim.PlanForSettlement(name).AllowsCategory("test"));
    }

    [Fact]
    public void AnOwnPlanReplacesTheEmpireOne_ItDoesNotAddToIt()
    {
        // Tohle je celý smysl mechaniky: říšský zákaz musí jít v jednom městě
        // povolit. Kdyby se plány sčítaly, nešlo by to.
        var (sim, _) = City();
        int name = sim.Settlements[0].NameIndex;

        sim.Plan.SetCategoryAllowed("test", false);
        var own = sim.GiveOwnPlan(name);
        own.SetCategoryAllowed("test", true);

        Assert.True(sim.PlanForSettlement(name).AllowsCategory("test"));
        Assert.False(sim.Plan.AllowsCategory("test"), "říšský plán se nemá měnit");
    }

    [Fact]
    public void AnOwnPlanStartsAsACopyOfTheEmpireOne()
    {
        // Kdyby začínal prázdný, hráč by po kliknutí zjistil, že město najednou
        // staví to, co si všude jinde zakázal.
        var (sim, _) = City();
        sim.Plan.SetCategoryAllowed("test", false);

        var own = sim.GiveOwnPlan(sim.Settlements[0].NameIndex);

        Assert.False(own.AllowsCategory("test"));
    }

    [Fact]
    public void DroppingTheOwnPlanPutsTheSettlementBackUnderTheEmpire()
    {
        var (sim, _) = City();
        int name = sim.Settlements[0].NameIndex;
        sim.GiveOwnPlan(name).SetCategoryAllowed("test", false);

        sim.DropOwnPlan(name);

        Assert.False(sim.HasOwnPlan(name));
        Assert.True(sim.PlanForSettlement(name).AllowsCategory("test"));
    }

    [Fact]
    public void ThePlanFollowsTheTileItIsBuiltOn()
    {
        var (sim, house) = City();
        int name = sim.Settlements[0].NameIndex;
        var center = sim.Settlements[0];

        sim.GiveOwnPlan(name).SetCategoryAllowed("test", false);

        Assert.False(sim.PlanAt((int)center.CenterX, (int)center.CenterY).AllowsCategory("test"));

        // Daleko za obzorem už žádné sídlo není — platí říšský plán.
        Assert.True(sim.PlanAt(5000, 5000).AllowsCategory("test"));
        Assert.True(house >= 0);
    }

    [Fact]
    public void ThePlanSurvivesSaveAndLoad()
    {
        var (sim, _) = CityWith(out var content);
        int name = sim.Settlements[0].NameIndex;
        sim.GiveOwnPlan(name).SetCategoryAllowed("test", false);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.True(loaded.HasOwnPlan(name));
        Assert.False(loaded.PlanForSettlement(name).AllowsCategory("test"));
    }

    [Fact]
    public void TheOverviewCountsWhatIsInEachSettlement()
    {
        var (sim, house) = City();
        var stats = new List<SettlementStat>();

        sim.DescribeSettlements(stats);

        var stat = Assert.Single(stats);
        Assert.Equal(sim.Settlements[0].NameIndex, stat.NameIndex);
        Assert.True(stat.Buildings > 0);
        Assert.True(stat.Housing > 0, "domy se nezapočítaly do kapacity");
        Assert.True(house >= 0);
    }

    // ----- pomocné -----

    private static (Simulation Sim, int House) City() => CityWith(out _);

    private static (Simulation Sim, int House) CityWith(out GameContent content)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var buildings = new[] { TestContent.SimpleBuilding("house", biomes.Length, housing: 4) };
        content = TestContent.Build(biomes: biomes, buildings: buildings);

        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.DebugFillStorages();

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, x * 2, y * 2));
            }
        }

        // Sídla se hledají líně — bez tiku by seznam zůstal prázdný.
        for (int i = 0; i < 60; i++)
        {
            sim.Tick();
        }

        Assert.NotEmpty(sim.Settlements);
        return (sim, 0);
    }
}
