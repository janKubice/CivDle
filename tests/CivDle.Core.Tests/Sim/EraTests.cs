using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Éry: aktuální éra je nejvyšší dosažená — základní éry (bez otevírací technologie)
/// jsou od startu, vyšší se odemknou vyzkoumáním své technologie.
/// </summary>
public class EraTests
{
    private static (GameContent Content, Simulation Sim) World()
    {
        var techs = new[] { new TechDef("bronze_working", new[] { new ResourceAmount(0, 5) }, System.Array.Empty<int>(), System.Array.Empty<int>()) };
        var eras = new[]
        {
            new EraDef("founding", 0, string.Empty),
            new EraDef("crafts", 1, string.Empty),
            new EraDef("bronze", 2, "bronze_working"),
        };
        var content = TestContent.Build(techs: techs, eras: eras);
        return (content, new Simulation(content, new UniformTerrain((byte)1)));
    }

    [Fact]
    public void StartsAtHighestBaselineEra()
    {
        var (content, sim) = World();
        Assert.Equal("crafts", content.Eras[sim.CurrentEraIndex].Id); // řemesla (order 1, bez tech)
    }

    [Fact]
    public void ResearchingUnlockTech_AdvancesEra()
    {
        var (content, sim) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0)); // bronze_working

        Assert.Equal("bronze", content.Eras[sim.CurrentEraIndex].Id);
    }
}
