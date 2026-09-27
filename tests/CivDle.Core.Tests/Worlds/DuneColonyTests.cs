using CivDle.Core.Content;
using CivDle.Core.Content.Mods;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Duna se skutečnými daty (svety-design.md 4.1): kolonie přistane, rozjede
/// se sama a voda rozhoduje, kde se dá žít.
/// </summary>
public sealed class DuneColonyTests
{
    private static readonly Lazy<GameContent> Dune = new(() =>
        new GalaxyContent(TestData.RealDataDirectory, Array.Empty<ModPackage>(), TestData.LoadRealContent()).For("dune"));

    private readonly ITestOutputHelper _output;

    public DuneColonyTests(ITestOutputHelper output) => _output = output;

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwn(long seed)
    {
        var sim = Land(seed);

        Run(sim, minutes: 20);

        var counts = sim.Buildings.ToArray().GroupBy(b => Dune.Value.Buildings[b.DefIndex].Id)
            .OrderByDescending(g => g.Count()).Select(g => $"{g.Key}×{g.Count()}");
        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, {string.Join(", ", counts)}");
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
    }

    internal static Simulation Land(long seed)
    {
        var content = Dune.Value;
        var preset = content.WorldGen.Presets[content.World.PresetIndex];
        var terrain = WorldTerrain.Create(content, preset, seed);
        var site = LandingSiteFinder.Find(content, terrain)[0];
        var sim = new Simulation(content, terrain, seed);
        Assert.Equal(PlacementResult.Ok, sim.Land(site.X, site.Y));
        return sim;
    }

    internal static void Run(Simulation sim, double minutes)
    {
        long ticks = (long)(minutes * 60 * Simulation.TicksPerSecond);
        for (long i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
