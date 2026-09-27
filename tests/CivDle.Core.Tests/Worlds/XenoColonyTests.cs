using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Xeno se skutečnými daty (svety-design.md 4.6): kolonie přistane u hnízda
/// flóry, flóra ji v tepech obrůstá, guvernér staví prořezávače a Stromy
/// života a dojde k ★ sám.
/// </summary>
public sealed class XenoColonyTests
{
    private const string Xeno = "xeno";

    private readonly ITestOutputHelper _output;

    public XenoColonyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheWorldLoadsWithSpreadingFlora()
    {
        var content = ColonyFixture.Content(Xeno);

        Assert.True(content.Hazards.FloraIndex >= 0, "Xeno má šíření flóry");
        var flora = content.Hazards.Hazards[content.Hazards.FloraIndex].Flora!;
        Assert.Equal(content.Biomes.IndexOf("xeno_nest"), flora.NestBiomeIndex);
        Assert.Equal(content.Biomes.IndexOf("xeno_bloom"), flora.BloomBiomeIndex);
        Assert.True(flora.SpreadOn[content.Biomes.IndexOf("xeno_moss")]);
        Assert.Equal(BuildingStall.Overgrown, content.Hazards.BurialStall);
        Assert.Equal(FloraRole.Pruner, content.Buildings[content.Buildings.IndexOf("pruner")].FloraRole);
        Assert.Equal(FloraRole.Barrier, content.Buildings[content.Buildings.IndexOf("spore_barrier")].FloraRole);
        Assert.Equal("biocrystal", content.Resources[content.World.ExportIndices[0]].Id);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheColonyLandsWithANestInSightButNotOnTheDoorstep(long seed)
    {
        var sim = ColonyFixture.Land(Xeno, seed);
        int nest = sim.Content.Biomes.IndexOf("xeno_nest");

        long best = long.MaxValue;
        for (int dy = -30; dy <= 30; dy++)
        {
            for (int dx = -30; dx <= 30; dx++)
            {
                if (sim.BiomeAt(sim.LandingX + dx, sim.LandingY + dy) == nest)
                {
                    best = Math.Min(best, (long)dx * dx + dy * dy);
                }
            }
        }

        double distance = Math.Sqrt(best);
        _output.WriteLine($"seed {seed}: nejbližší hnízdo {distance:0.0} dlaždic od modulu");
        Assert.InRange(distance, 6, 26); // hnízda se hledají po dvou dlaždicích
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwnWhileTheFloraSpreads(long seed)
    {
        var sim = ColonyFixture.Land(Xeno, seed);
        int bloom = sim.Content.Biomes.IndexOf("xeno_bloom");

        ColonyFixture.Run(sim, minutes: 20);

        int bloomTiles = BloomNear(sim, bloom);
        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, flóra {bloomTiles} dlaždic, tepy {sim.HazardsWeathered}/{sim.CalmHazards}, {ColonyFixture.Census(sim)}");
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
        Assert.True(bloomTiles > 0, "flóra měla u města vyrůst");
    }

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Xena.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = ColonyFixture.Land(Xeno, seed);
        sim.Plan.SetChoosesResearch(true);
        int star = sim.Content.Quests.IndexOf("xeno_star_settled");

        int minute = 0;
        while (minute < 130 && !sim.IsQuestCompleted(star))
        {
            ColonyFixture.Run(sim, minutes: 1);
            minute++;
        }

        _output.WriteLine($"seed {seed}: první hvězda za {minute} min, {sim.Population:0} lidí, tepy {sim.HazardsWeathered}/{sim.CalmHazards}");
        Assert.True(sim.IsQuestCompleted(star), $"guvernér nedošel k ★ za 130 min ({sim.Population:0} lidí)");
    }

    /// <summary>Diagnostika: dlouhý běh bez hráče s výpisem (spouští se ručně).</summary>
    [Theory(Skip = "diagnostika balancu — pouštět ručně")]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void HandsOffTimeline(long seed)
    {
        var sim = ColonyFixture.Land(Xeno, seed);
        sim.Plan.SetChoosesResearch(true);
        var content = sim.Content;
        int bloom = content.Biomes.IndexOf("xeno_bloom");
        for (int minute = 1; minute <= 120; minute++)
        {
            ColonyFixture.Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                int techs = Enumerable.Range(0, content.Techs.Count).Count(sim.IsTechResearched);
                int wrapped = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.Overgrown);
                int pruners = sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].FloraRole == FloraRole.Pruner);
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, "
                    + $"flóra {BloomNear(sim, bloom)} ({100.0 * BloomNear(sim, bloom) / (81 * 81):0}%), trees {sim.Buildings.ToArray().Count(b => content.Buildings[b.DefIndex].Id == "life_tree")}, obalené {wrapped}, prořezávače {pruners}, tepy {sim.HazardsWeathered}/{sim.CalmHazards}, {ColonyFixture.Census(sim, 9)}");
                _output.WriteLine("      stavy: " + string.Join(", ", sim.Buildings.ToArray()
                    .GroupBy(b => $"{content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(8)
                    .Select(g => $"{g.Key}×{g.Count()}")) + " | agenda " + string.Join(" ", sim.GovernorAgenda.Select(a => $"{a.Need}:{a.Urgency}")));
                _output.WriteLine("      nevyzkoumáno: " + string.Join(",", Enumerable.Range(0, content.Techs.Count).Where(t => !sim.IsTechResearched(t)).Select(t => content.Techs[t].Id)));
                _output.WriteLine("      zásoby: " + string.Join(" ", Enumerable.Range(0, content.Resources.Count)
                    .Where(r => sim.GetResource(r) > 0)
                    .Select(r => $"{content.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
            }
        }
    }

    /// <summary>Kolik dlaždic flóry je v okruhu 40 kolem středu města.</summary>
    private static int BloomNear(Simulation sim, int bloom)
    {
        int count = 0;
        for (int dy = -40; dy <= 40; dy++)
        {
            for (int dx = -40; dx <= 40; dx++)
            {
                if (sim.BiomeAt(sim.CityCenterX + dx, sim.CityCenterY + dy) == bloom)
                {
                    count++;
                }
            }
        }

        return count;
    }
}
