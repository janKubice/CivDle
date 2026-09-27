using CivDle.Core.Content;
using CivDle.Core.Content.Mods;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Kolonie se skutečnými daty pro testy světů: obsah světa (načtený jednou
/// na běh testů), přistání na prvním nabídnutém místě a běh v minutách.
/// </summary>
internal static class ColonyFixture
{
    private static readonly Lazy<GalaxyContent> Galaxy = new(() =>
        new GalaxyContent(TestData.RealDataDirectory, Array.Empty<ModPackage>(), TestData.LoadRealContent()));

    /// <summary>Obsah světa (líně, sdílený mezi testy).</summary>
    public static GameContent Content(string worldId) => Galaxy.Value.For(worldId);

    /// <summary>Kolonie přistála na prvním místě, které by nabídla obrazovka přistání.</summary>
    public static Simulation Land(string worldId, long seed)
    {
        var content = Content(worldId);
        var preset = content.WorldGen.Presets[content.World.PresetIndex];
        var terrain = WorldTerrain.Create(content, preset, seed);
        var site = LandingSiteFinder.Find(content, terrain)[0];
        var sim = new Simulation(content, terrain, seed);
        Assert.Equal(PlacementResult.Ok, sim.Land(site.X, site.Y));
        return sim;
    }

    /// <summary>Odtiká dané množství herních minut.</summary>
    public static void Run(Simulation sim, double minutes)
    {
        long ticks = (long)(minutes * 60 * Simulation.TicksPerSecond);
        for (long i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Kolik je kterých budov (pro výpis v diagnostice).</summary>
    public static string Census(Simulation sim, int top = 10) =>
        string.Join(", ", sim.Buildings.ToArray().GroupBy(b => sim.Content.Buildings[b.DefIndex].Id)
            .OrderByDescending(g => g.Count()).Take(top).Select(g => $"{g.Key}×{g.Count()}"));
}
