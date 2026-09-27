using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Výheň se skutečnými daty (svety-design.md 4.4): kolonie přistane pod
/// sopkou, láva z průduchu teče k městu, guvernér ji zahrazuje a dojde k ★.
/// </summary>
public sealed class ForgeColonyTests
{
    private const string Forge = "forge";

    private readonly ITestOutputHelper _output;

    public ForgeColonyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheWorldLoadsWithItsEruptions()
    {
        var content = ColonyFixture.Content(Forge);

        int eruption = content.Hazards.EruptionIndex;
        Assert.True(eruption >= 0, "Výheň má mít erupce");
        var rule = content.Hazards.Hazards[eruption].Eruption!;
        Assert.Equal(content.Biomes.IndexOf("lava_vent"), rule.VentBiomeIndex);
        Assert.Equal(content.Biomes.IndexOf("basalt_field"), rule.CrustBiomeIndex);
        Assert.True(rule.FirstAfterSeconds <= 600, "první erupce má přijít do deseti minut (3.3)");
        Assert.Equal(LavaRole.Wall, content.Buildings[content.Buildings.IndexOf("lava_wall")].LavaRole);
        Assert.Equal(LavaRole.Channel, content.Buildings[content.Buildings.IndexOf("lava_channel")].LavaRole);
        Assert.True(content.Buildings[content.Buildings.IndexOf("seismograph")].Forecasts);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheLavaRunsPastTheLanding(long seed)
    {
        // Pravidlo světa má tlačit brzy (3.3): místo přistání leží pod
        // průduchem a láva z něj teče kolem — blízko, ale ne přes modul.
        var sim = ColonyFixture.Land(Forge, seed);
        ColonyFixture.Run(sim, minutes: 0.1); // střed města se spočítá na nízké frekvenci

        var path = sim.PredictedLavaPath;
        int closest = path.Count == 0 ? int.MaxValue : path.Skip(1)
            .Min(tile => Math.Max(Math.Abs(TileKey.X(tile) - sim.LandingX), Math.Abs(TileKey.Y(tile) - sim.LandingY)));

        _output.WriteLine($"seed {seed}: dráha {path.Count} dlaždic, nejblíž {closest} dlaždic od modulu");
        Assert.InRange(closest, 3, 16);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwn(long seed)
    {
        var sim = ColonyFixture.Land(Forge, seed);

        ColonyFixture.Run(sim, minutes: 20);

        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, {ColonyFixture.Census(sim)}");
        _output.WriteLine("stavy: " + string.Join(", ", sim.Buildings.ToArray()
            .GroupBy(b => $"{sim.Content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(12)
            .Select(g => $"{g.Key}×{g.Count()}")));
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
        Assert.True(sim.LavaLandTiles > 0, "za dvacet minut má láva aspoň jednou vytéct");
    }

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Výhně přes erupce.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = ColonyFixture.Land(Forge, seed);
        sim.Plan.SetChoosesResearch(true);
        int star = sim.Content.Quests.IndexOf("forge_star_settled");

        int minute = 0;
        while (minute < 130 && !sim.IsQuestCompleted(star))
        {
            ColonyFixture.Run(sim, minutes: 1);
            minute++;
        }

        _output.WriteLine($"seed {seed}: první hvězda za {minute} min, {sim.Population:0} lidí, erupce {sim.HazardsWeathered}/{sim.CalmHazards} klidných");
        Assert.True(sim.IsQuestCompleted(star), $"guvernér nedošel k ★ za 130 min ({sim.Population:0} lidí)");
    }

    /// <summary>Diagnostika: dlouhý běh bez hráče s výpisem (spouští se ručně).</summary>
    [Theory(Skip = "diagnostika balancu — pouštět ručně")]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void HandsOffTimeline(long seed)
    {
        var sim = ColonyFixture.Land(Forge, seed);
        sim.Plan.SetChoosesResearch(true);
        var content = sim.Content;
        for (int minute = 1; minute <= 120; minute++)
        {
            ColonyFixture.Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                int techs = Enumerable.Range(0, content.Techs.Count).Count(sim.IsTechResearched);
                int scorched = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.Scorched);
                int walls = sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].LavaRole != LavaRole.None);
                int seismographs = sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].Forecasts);
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, "
                    + $"zalité {scorched}, hráze {walls}, stanice {seismographs}, erupce {sim.HazardsWeathered}/{sim.CalmHazards}, nová zem {sim.LavaLandTiles}, {ColonyFixture.Census(sim, 9)}");
                _output.WriteLine("      stavy: " + string.Join(", ", sim.Buildings.ToArray()
                    .GroupBy(b => $"{content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(8)
                    .Select(g => $"{g.Key}×{g.Count()}")) + $" | agenda " + string.Join(" ", sim.GovernorAgenda.Select(a => $"{a.Need}:{a.Urgency}")));
                _output.WriteLine("      zásoby: " + string.Join(" ", Enumerable.Range(0, content.Resources.Count)
                    .Where(r => sim.GetResource(r) > 0)
                    .Select(r => $"{content.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
            }
        }
    }

    /// <summary>Diagnostika terénu kolem přistání (spouští se ručně).</summary>
    [Theory(Skip = "diagnostika terénu — pouštět ručně")]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TerrainAroundTheLanding(long seed)
    {
        var sim = ColonyFixture.Land(Forge, seed);
        var content = sim.Content;
        var counts = new Dictionary<string, int>();
        for (int y = sim.LandingY - 48; y <= sim.LandingY + 48; y++)
        {
            for (int x = sim.LandingX - 48; x <= sim.LandingX + 48; x++)
            {
                string id = content.Biomes[sim.BiomeAt(x, y)].Id;
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }

        _output.WriteLine($"seed {seed} @ {sim.LandingX},{sim.LandingY}: " + string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key}={c.Value}")));
        ColonyFixture.Run(sim, minutes: 0.1);
        var rule = content.Hazards.Hazards[content.Hazards.EruptionIndex].Eruption!;
        var vents = new List<(int X, int Y, double H)>();
        for (int y = sim.CityCenterY - 48; y <= sim.CityCenterY + 48; y++)
        {
            for (int x = sim.CityCenterX - 48; x <= sim.CityCenterX + 48; x++)
            {
                if (sim.BiomeAt(x, y) == rule.VentBiomeIndex)
                {
                    vents.Add((x, y, sim.ElevationAt(x, y)));
                }
            }
        }

        _output.WriteLine("budovy: " + string.Join(" ", sim.Buildings.ToArray().Select(b => $"{content.Buildings[b.DefIndex].Id}@{b.X},{b.Y}")));
        _output.WriteLine($"střed {sim.CityCenterX},{sim.CityCenterY}, průduchů {vents.Count}: "
            + string.Join(" ", vents.OrderBy(v => Math.Abs(v.X - sim.CityCenterX) + Math.Abs(v.Y - sim.CityCenterY)).Take(5).Select(v => $"{v.X},{v.Y}@{v.H:0.000}")));
        var path = sim.PredictedLavaPath;
        _output.WriteLine($"dráha lávy: {path.Count} dlaždic, od {(path.Count > 0 ? $"{TileKey.X(path[0])},{TileKey.Y(path[0])}" : "-")}"
            + $" do {(path.Count > 0 ? $"{TileKey.X(path[^1])},{TileKey.Y(path[^1])}" : "-")}");
    }
}
