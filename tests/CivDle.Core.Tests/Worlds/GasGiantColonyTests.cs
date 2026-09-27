using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Nebesa se skutečnými daty (svety-design.md 4.5): kolonie přistane na
/// palubě nad oblaky, guvernér rozšiřuje palubu, staví vaky, aby město
/// neklesalo, a přečká bouřkové pásy až k ★.
/// </summary>
public sealed class GasGiantColonyTests
{
    private const string GasGiant = "gas_giant";

    private readonly ITestOutputHelper _output;

    public GasGiantColonyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheWorldLoadsWithoutGroundAndWithStormBands()
    {
        var content = ColonyFixture.Content(GasGiant);

        var platform = content.World.Platform;
        Assert.NotNull(platform);
        Assert.Equal(content.Biomes.IndexOf("sky_deck"), platform!.BiomeIndex);
        Assert.True(content.Biomes[content.Biomes.IndexOf("cloud_sea")].HasNoGround);
        Assert.Equal(content.Biomes.IndexOf("cloud_sea"), content.WorldGen.Presets[content.World.PresetIndex].FillBiomeIndex);
        Assert.True(content.Hazards.BurialIsStorm, "bouřkový pás má zasahovat bleskem");
        Assert.Equal(SupplyTime.Storm, content.Buildings[content.Buildings.IndexOf("lightning_harvester")].SupplyTime);
        Assert.True(content.Networks.IndexOf("lift") > 0);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwn(long seed)
    {
        var sim = ColonyFixture.Land(GasGiant, seed);

        ColonyFixture.Run(sim, minutes: 20);

        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, paluba {sim.TerraformedTiles}, {ColonyFixture.Census(sim)}");
        _output.WriteLine("stavy: " + string.Join(", ", sim.Buildings.ToArray()
            .GroupBy(b => $"{sim.Content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(12)
            .Select(g => $"{g.Key}×{g.Count()}")));
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
        Assert.True(sim.TerraformedTiles > 0, "guvernér měl palubu rozšířit");
        Assert.All(sim.Buildings.ToArray(), b => Assert.False(
            sim.Content.Biomes[sim.BiomeAt(b.X, b.Y)].HasNoGround, "nic nestojí v oblacích"));
    }

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Nebes.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = ColonyFixture.Land(GasGiant, seed);
        sim.Plan.SetChoosesResearch(true);
        int star = sim.Content.Quests.IndexOf("gg_star_settled");

        int minute = 0;
        while (minute < 130 && !sim.IsQuestCompleted(star))
        {
            ColonyFixture.Run(sim, minutes: 1);
            minute++;
        }

        _output.WriteLine($"seed {seed}: první hvězda za {minute} min, {sim.Population:0} lidí, paluba {sim.TerraformedTiles}, bouře {sim.HazardsWeathered}/{sim.CalmHazards}");
        Assert.True(sim.IsQuestCompleted(star), $"guvernér nedošel k ★ za 130 min ({sim.Population:0} lidí)");
    }

    /// <summary>Diagnostika: dlouhý běh bez hráče s výpisem (spouští se ručně).</summary>
    [Theory(Skip = "diagnostika balancu — pouštět ručně")]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void HandsOffTimeline(long seed)
    {
        var sim = ColonyFixture.Land(GasGiant, seed);
        sim.Plan.SetChoosesResearch(true);
        var content = sim.Content;
        int lift = content.Networks.IndexOf("lift");
        for (int minute = 1; minute <= 120; minute++)
        {
            ColonyFixture.Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                int techs = Enumerable.Range(0, content.Techs.Count).Count(sim.IsTechResearched);
                int sinking = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.NetworkShortage);
                int struck = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.Struck);
                int shields = sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].Id == "storm_shield");
                int power = sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].PowerSupply > 0);
                string research = string.Join(",", new[] { "meteorology", "aerogel", "storm_shields", "storm_power" }
                    .Select(t => content.Techs.TryIndexOf(t, out int ti) && sim.IsTechResearched(ti) ? t[..3] : "-"));
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, "
                    + $"paluba {sim.TerraformedTiles}, klesá {sinking}, zasažené {struck}, štíty {shields}, zdroje proudu {power}, výzkum {research}, bouře {sim.HazardsWeathered}/{sim.CalmHazards}, {ColonyFixture.Census(sim, 9)}");
                _output.WriteLine("      stavy: " + string.Join(", ", sim.Buildings.ToArray()
                    .GroupBy(b => $"{content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(8)
                    .Select(g => $"{g.Key}×{g.Count()}")) + $" | agenda " + string.Join(" ", sim.GovernorAgenda.Select(a => $"{a.Need}:{a.Urgency}")));
                _output.WriteLine("      zásoby: " + string.Join(" ", Enumerable.Range(0, content.Resources.Count)
                    .Where(r => sim.GetResource(r) > 0)
                    .Select(r => $"{content.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
            }
        }
    }
}
