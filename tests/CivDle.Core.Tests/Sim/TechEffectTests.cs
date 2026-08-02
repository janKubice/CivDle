using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Pasivní efekty technologií: kromě odemykání budov může tech dát trvalý bonus
/// (stejná behavior-ID jako upgrady Vzestupu). Platí v rámci běhu — Vzestup
/// výzkum resetuje, takže bonus zmizí s ním.
/// </summary>
public class TechEffectTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private static GameContent EffectContent(string effect, double magnitude)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 100, BaseStorage: 100) };
        var tech = new TechDef(
            "boost", new[] { new ResourceAmount(0, 10) },
            System.Array.Empty<int>(), System.Array.Empty<int>(), effect, magnitude);
        return TestContent.Build(biomes, 1, resources, techs: new[] { tech });
    }

    [Fact]
    public void ResearchingPassiveTech_RaisesMultiplier()
    {
        var sim = new Simulation(EffectContent("production_mult", 0.5), Grass());
        Assert.Equal(1.0, sim.Bonuses.ProductionMult);

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));

        Assert.Equal(1.5, sim.Bonuses.ProductionMult);
    }

    [Fact]
    public void StorageTech_AppliesToCapsImmediately()
    {
        // Skladový bonus musí hned přepočítat i odvozený stav (kapacity), ne až příště.
        var sim = new Simulation(EffectContent("storage_mult", 1.0), Grass());
        double before = sim.GetStorageCap(0);

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));

        Assert.Equal(before * 2, sim.GetStorageCap(0));
    }

    [Fact]
    public void UnknownEffect_IsIgnored()
    {
        // Data smí předběhnout kód — neznámé behavior-ID nesmí shodit ani nic změnit.
        var sim = new Simulation(EffectContent("nejaky_budouci_efekt", 5.0), Grass());

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));

        Assert.Equal(1.0, sim.Bonuses.ProductionMult);
        Assert.Equal(1.0, sim.Bonuses.GrowthMult);
    }

    [Fact]
    public void RealContent_TechTreeIsAConnectedWebWithPassives()
    {
        var content = TestData.LoadRealContent();
        var techs = content.Techs;

        // Strom má být bohatý, ne pár uzlů — drobná vylepšení jsou to, čím je plný.
        Assert.True(techs.Count >= 90, $"tech tree má mít aspoň 90 uzlů, má {techs.Count}");

        int passives = techs.All.Count(t => t.HasPassiveEffect);
        Assert.True(passives >= 50, $"aspoň 50 technologií má dávat pasivní bonus, dává {passives}");

        // Dřív se tu vyžadovaly křížové vazby („síť"). Záměr se změnil: strom se
        // kreslí jako hvězdice a ta je bez křížení jen tehdy, když má každý uzel
        // nejvýš jednoho rodiče. Rozvětvenost proto neměří počet prereků, ale
        // počet uzlů, které mají víc než jedno dítě.
        var childCount = new int[techs.Count];
        foreach (var tech in techs.All)
        {
            foreach (int prereq in tech.PrerequisiteIndices)
            {
                childCount[prereq]++;
            }
        }

        int branchPoints = childCount.Count(c => c > 1);
        Assert.True(branchPoints >= 8, $"hvězda má mít víc ramen, větvení je jen {branchPoints}");

        // Právě jeden kořen — jádro hvězdy, od kterého se všechno odvíjí.
        Assert.Single(techs.All.Where(t => t.PrerequisiteIndices.Count == 0));
    }

    [Fact]
    public void RealContent_HasTechsAimedAtSingleResources()
    {
        // Drobnosti typu „+5 % dřeva" jsou důvod, proč je strom velký a ne jen
        // dlouhý seznam téhož globálního bonusu.
        var techs = TestData.LoadRealContent().Techs;

        int targeted = techs.All.Count(t => t.TargetResourceIndex >= 0);

        Assert.True(targeted >= 12, $"cílených vylepšení má být aspoň 12, je {targeted}");
    }
}
