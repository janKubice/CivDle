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

    /// <summary>
    /// Hands-off (svety-design.md 7.13): guvernér sám, s výzkumem na sobě,
    /// dojde k první hvězdě Duny (dnes 90–105 min podle mapy). Hráč, který
    /// kliká a staví, bývá rychlejší — tohle je spodní hranice, ne cílový čas.
    /// </summary>
    [Theory]
    [InlineData(11)]
    [InlineData(2024)]
    [InlineData(77)]
    public void TheGovernorReachesTheFirstStarAlone(long seed)
    {
        var sim = Land(seed);
        sim.Plan.SetChoosesResearch(true);
        int star = Dune.Value.Quests.IndexOf("dune_star_settled");

        int minute = 0;
        while (minute < 130 && !sim.IsQuestCompleted(star))
        {
            Run(sim, minutes: 1);
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
        var sim = Land(seed);
        sim.Plan.SetChoosesResearch(true);
        for (int minute = 1; minute <= 120; minute++)
        {
            Run(sim, minutes: 1);
            if (minute % 10 == 0)
            {
                var counts = sim.Buildings.ToArray().GroupBy(b => Dune.Value.Buildings[b.DefIndex].Id)
                    .OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key}×{g.Count()}");
                int techs = Enumerable.Range(0, Dune.Value.Techs.Count).Count(sim.IsTechResearched);
                _output.WriteLine($"{minute,3} min: {sim.Population,6:0} lidí, bydlení {sim.HousingCapacity,6:0}, techs {techs}, bouří {sim.HazardsWeathered}/{sim.CalmHazards}, {string.Join(", ", counts)}");
                var open = Enumerable.Range(0, Dune.Value.Techs.Count)
                    .Where(t => !sim.IsTechResearched(t) && sim.CanResearch(t) != PlacementResult.NotUnlocked)
                    .Select(t => $"{Dune.Value.Techs[t].Id}[{string.Join("+", sim.ScaledResearchCost(t).Select(c => $"{Dune.Value.Resources[c.ResourceIndex].Id}{c.Amount}"))}:{sim.CanResearch(t)}]");
                _output.WriteLine("      otevřené: " + string.Join(" ", open.Take(8)));
                _output.WriteLine("      zásoby: " + string.Join(" ", Enumerable.Range(0, Dune.Value.Resources.Count)
                    .Select(r => $"{Dune.Value.Resources[r].Id}={sim.GetResource(r):0}/{sim.GetStorageCap(r):0}")));
            }
        }
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
