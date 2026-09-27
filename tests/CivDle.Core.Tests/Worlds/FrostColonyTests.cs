using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Mráz se skutečnými daty (svety-design.md 4.2): kolonie přistane u tepla,
/// rozjede se sama, v zimě je polární noc a bez tepla se mrzne.
/// </summary>
public sealed class FrostColonyTests
{
    private const string Frost = "frost";

    private readonly ITestOutputHelper _output;

    public FrostColonyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheWorldLoadsWithItsHeatAndItsPolarNight()
    {
        var content = ColonyFixture.Content(Frost);

        int heat = content.Networks.IndexOf("heat");
        Assert.True(heat > 0);
        Assert.Equal(NetworkShortage.Cutoff, content.Networks[heat].Shortage);
        Assert.Equal(NetworkTypeDef.FrostLook, content.Networks[heat].ShortageLook);
        int winter = content.Seasons.PolarNightIndex;
        Assert.True(winter >= 0, "Mráz má mít polární noc");
        Assert.Equal(1.5, content.Seasons.Seasons[winter].NetworkDemandMult(heat), 6);
        Assert.True(content.Seasons.Seasons[winter].Daylight < 0.25, "polární noc má mít krátký den");
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwn(long seed)
    {
        var sim = ColonyFixture.Land(Frost, seed);

        ColonyFixture.Run(sim, minutes: 20);

        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, {ColonyFixture.Census(sim)}");
        _output.WriteLine("stavy: " + string.Join(", ", sim.Buildings.ToArray()
            .GroupBy(b => $"{sim.Content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(12)
            .Select(g => $"{g.Key}×{g.Count()}")));
        _output.WriteLine("zásoby: " + string.Join(" ", Enumerable.Range(0, sim.Content.Resources.Count)
            .Where(r => sim.GetStorageCap(r) > 0 && sim.Content.Resources[r].Id is "food" or "lichen" or "peat" or "ice")
            .Select(r => $"{sim.Content.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
    }

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Mrazu přes polární noci a vánice.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = ColonyFixture.Land(Frost, seed);
        sim.Plan.SetChoosesResearch(true);
        int star = sim.Content.Quests.IndexOf("frost_star_settled");

        int minute = 0;
        while (minute < 130 && !sim.IsQuestCompleted(star))
        {
            ColonyFixture.Run(sim, minutes: 1);
            minute++;
        }

        _output.WriteLine($"seed {seed}: první hvězda za {minute} min, {sim.Population:0} lidí");
        Assert.True(sim.IsQuestCompleted(star), $"guvernér nedošel k ★ za 130 min ({sim.Population:0} lidí)");
    }

    /// <summary>Diagnostika: dlouhý běh bez hráče s výpisem (spouští se ručně).</summary>
    [Theory(Skip = "diagnostika balancu — pouštět ručně")]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void HandsOffTimeline(long seed)
    {
        var sim = ColonyFixture.Land(Frost, seed);
        sim.Plan.SetChoosesResearch(true);
        var content = sim.Content;
        for (int minute = 1; minute <= 120; minute++)
        {
            ColonyFixture.Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                int techs = Enumerable.Range(0, content.Techs.Count).Count(sim.IsTechResearched);
                int frozen = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.NetworkShortage);
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, "
                    + $"zamrzlé {frozen}, období {sim.CurrentSeason?.Id}, teplé noci {sim.WarmWinters}, {ColonyFixture.Census(sim, 8)}");
                _output.WriteLine("      zásoby: " + string.Join(" ", Enumerable.Range(0, content.Resources.Count)
                    .Where(r => sim.GetResource(r) > 0 || content.Resources[r].Id is "frost_crystal" or "cut_crystal" or "heat_cell")
                    .Select(r => $"{content.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
            }
        }
    }
}
