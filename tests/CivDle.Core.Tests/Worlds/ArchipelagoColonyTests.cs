using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Worlds;

/// <summary>
/// Souostroví se skutečnými daty (svety-design.md 4.3): kolonie přistane na
/// ostrově, příliv zaplavuje mělčiny, guvernér staví na kůlech a dojde k ★.
/// </summary>
public sealed class ArchipelagoColonyTests
{
    private const string Archipelago = "archipelago";

    private readonly ITestOutputHelper _output;

    public ArchipelagoColonyTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void TheWorldLoadsWithItsTideAndSurf()
    {
        var content = ColonyFixture.Content(Archipelago);

        int tide = content.Hazards.TideIndex;
        Assert.True(tide >= 0, "Souostroví má mít příliv");
        Assert.Equal(content.Biomes.IndexOf("tidal_flat"), content.Hazards.Hazards[tide].Tide!.FloodBiomeIndex);
        var cyclone = content.Hazards.Hazards[content.Hazards.IndexOf("cyclone")].Burial!;
        Assert.True(cyclone.CoastTiles > 0, "cyklón má bít jen pobřeží");
        Assert.True(content.Buildings[content.Buildings.IndexOf("stilt_hut")].Stilted);
        Assert.True(content.Buildings[content.Buildings.IndexOf("ferry_dock")].IsFerryDock);
    }

    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    public void TheColonyGrowsOnItsOwn(long seed)
    {
        var sim = ColonyFixture.Land(Archipelago, seed);

        ColonyFixture.Run(sim, minutes: 20);

        _output.WriteLine($"seed {seed}: {sim.Population:0} obyvatel, {ColonyFixture.Census(sim)}");
        _output.WriteLine("stavy: " + string.Join(", ", sim.Buildings.ToArray()
            .GroupBy(b => $"{sim.Content.Buildings[b.DefIndex].Id}:{b.Stall}").OrderByDescending(g => g.Count()).Take(12)
            .Select(g => $"{g.Key}×{g.Count()}")));
        Assert.True(sim.Population > 60, $"kolonie se nerozjela: {sim.Population:0} obyvatel");
    }

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Souostroví přes příliv a cyklóny.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = ColonyFixture.Land(Archipelago, seed);
        sim.Plan.SetChoosesResearch(true);
        int star = sim.Content.Quests.IndexOf("arch_star_settled");

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
        var sim = ColonyFixture.Land(Archipelago, seed);
        sim.Plan.SetChoosesResearch(true);
        var content = sim.Content;
        for (int minute = 1; minute <= 120; minute++)
        {
            ColonyFixture.Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                int techs = Enumerable.Range(0, content.Techs.Count).Count(sim.IsTechResearched);
                int flooded = sim.Buildings.ToArray().Count(b => b.Stall == BuildingStall.Flooded);
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, "
                    + $"zaplavené {flooded}, příliv {sim.TideLevel:0.00}, cyklóny {sim.HazardsWeathered}/{sim.CalmHazards}, země {sim.TerraformedTiles}, {ColonyFixture.Census(sim, 9)}");
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
        var sim = ColonyFixture.Land(Archipelago, seed);
        var content = sim.Content;
        var counts = new Dictionary<string, int>();
        var heights = new int[11];
        for (int y = sim.LandingY - 40; y <= sim.LandingY + 40; y++)
        {
            for (int x = sim.LandingX - 40; x <= sim.LandingX + 40; x++)
            {
                string id = content.Biomes[sim.BiomeAt(x, y)].Id;
                counts[id] = counts.GetValueOrDefault(id) + 1;
                double h = sim.TideHeightAt(x, y);
                if (h < 1.0 || id == "tidal_flat")
                {
                    heights[(int)Math.Floor(h * 10)]++;
                }
            }
        }

        _output.WriteLine($"seed {seed} @ {sim.LandingX},{sim.LandingY}: " + string.Join(", ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key}={c.Value}")));
        _output.WriteLine("výšky mělčin: " + string.Join(" ", heights));
    }
}
