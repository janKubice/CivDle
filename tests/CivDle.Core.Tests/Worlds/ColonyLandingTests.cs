using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using CivDle.Core.Content.Mods;
using Xunit;

namespace CivDle.Core.Tests.Worlds;

/// <summary>Přistání kolonie na každém světě galaxie (svety-design.md 2.2).</summary>
public sealed class ColonyLandingTests
{
    public static IEnumerable<object[]> Colonies() =>
        new GalaxyContent(TestData.RealDataDirectory, Array.Empty<ModPackage>(), TestData.LoadRealContent())
            .Catalog.Colonies.Select(world => new object[] { world.Id });

    [Theory]
    [MemberData(nameof(Colonies))]
    public void TheFirstBuildingsGoUpAroundTheLander(string worldId)
    {
        // Střed města je hned po přistání u modulu — dřív zůstal na počátku
        // mapy, dokud se nepřepočítal, a guvernér postavil první lom sto
        // dlaždic od kolonie.
        var sim = ColonyFixture.Land(worldId, seed: 77);
        Assert.Equal((sim.LandingX, sim.LandingY), (sim.CityCenterX, sim.CityCenterY));

        ColonyFixture.Run(sim, minutes: 0.5);

        // Těžba smí pár desítek dlaždic za surovinou (korálový lom za útesem),
        // ne ale na druhý konec mapy.
        Assert.All(sim.Buildings.ToArray(), building => Assert.True(
            Math.Max(Math.Abs(building.X - sim.LandingX), Math.Abs(building.Y - sim.LandingY)) <= 40,
            $"{sim.Content.Buildings[building.DefIndex].Id} na {building.X},{building.Y} daleko od modulu na {sim.LandingX},{sim.LandingY}"));
    }
}
