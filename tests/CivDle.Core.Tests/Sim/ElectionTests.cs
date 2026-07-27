using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Volby na pozadí: kandidátka je odvozená ze seedu a období (po načtení savu
/// stejná), zvolený program dává bonus, a když si hráč nevybere, rozhodne se to
/// za něj — idle hra nesmí čekat na klik.
/// </summary>
public class ElectionTests
{
    private static readonly Resource[] WoodAndFood =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 200, BaseStorage: 10_000),
        new("food", new RgbColor(220, 180, 60), StartAmount: 500, BaseStorage: 10_000),
    };

    private static ElectionConfig Config(int termDays = 1) => new(
        new[]
        {
            new ElectionCandidateDef("mills", ElectionEffect.Production, 1.0),
            new ElectionCandidateDef("settlers", ElectionEffect.Growth, 1.0),
            new ElectionCandidateDef("diggers", ElectionEffect.Harvest, 1.0),
        },
        TermDays: termDays,
        BallotSize: 2);

    private static GameContent Content(ElectionConfig elections) => TestContent.Build(
        resources: WoodAndFood,
        gameplay: TestContent.DefaultGameplay with { FoodResourceIndex = 1 },
        elections: elections);

    private static Simulation Run(GameContent content, int ticks, long seed = 7)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1), seed);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim;
    }

    [Fact]
    public void FirstTerm_OpensOnItsOwn()
    {
        var sim = Run(Content(Config()), 12);

        Assert.True(sim.ElectionTerm >= 0);
        Assert.Equal(2, sim.BallotSize);
    }

    [Fact]
    public void Ballot_HasNoDuplicates()
    {
        var sim = Run(Content(Config()), 12);

        Assert.NotEqual(sim.BallotAt(0), sim.BallotAt(1));
    }

    [Fact]
    public void Ballot_IsTheSameForTheSameSeedAndTerm()
    {
        var first = Run(Content(Config()), 12);
        var second = Run(Content(Config()), 12);

        Assert.Equal(first.BallotAt(0), second.BallotAt(0));
        Assert.Equal(first.BallotAt(1), second.BallotAt(1));
    }

    [Fact]
    public void ElectedProgramme_AppliesItsBonus()
    {
        var sim = Run(Content(Config()), 12);

        // Index 0 = výroba +100 %; násobič se musí projevit hned po zvolení.
        sim.ElectCandidate(0);
        Assert.Equal(2.0, sim.ElectionProductionMult);
    }

    [Fact]
    public void EachEffect_LandsOnItsOwnMultiplier()
    {
        var sim = Run(Content(Config()), 12);

        sim.ElectCandidate(1); // růst
        Assert.Equal(2.0, sim.ElectionGrowthMult);
        Assert.Equal(1.0, sim.ElectionProductionMult);

        sim.ElectCandidate(2); // sběr
        Assert.Equal(2.0, sim.ElectionHarvestMult);
        Assert.Equal(1.0, sim.ElectionGrowthMult);
    }

    [Fact]
    public void TermNeverStartsWithoutAGovernment()
    {
        // Hráč nic nevybral — přesto musí někdo vládnout, jinak by nezájem
        // o volby znamenal trest v podobě žádného bonusu.
        var sim = Run(Content(Config()), 12);

        Assert.True(sim.HasElected);
        Assert.Equal(sim.BallotAt(0), sim.ElectedCandidate);
    }

    [Fact]
    public void NewTerm_ReplacesThePreviousGovernment()
    {
        var content = Content(Config(termDays: 1));
        long ticksPerDay = (long)(content.Gameplay.DayNight.DayLengthSeconds * Simulation.TicksPerSecond);
        var sim = Run(content, 12);

        long firstTerm = sim.ElectionTerm;
        for (int i = 0; i < ticksPerDay + 40; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.ElectionTerm > firstTerm, "Po uplynutí období musí přijít nové volby.");
        Assert.True(sim.HasElected);
    }

    [Fact]
    public void SaveRoundtrip_KeepsTheTermAndTheWinner()
    {
        var content = Content(Config());
        var sim = Run(content, 12);
        sim.ElectCandidate(sim.BallotAt(1));

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(7, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(sim.ElectionTerm, loaded.ElectionTerm);
        Assert.Equal(sim.ElectedCandidate, loaded.ElectedCandidate);
        Assert.Equal(sim.BallotAt(0), loaded.BallotAt(0));
    }

    [Fact]
    public void WithoutElectionData_NothingHappens()
    {
        var sim = Run(TestContent.Build(resources: WoodAndFood), 30);

        Assert.Equal(-1, sim.ElectionTerm);
        Assert.False(sim.HasElected);
        Assert.Equal(1.0, sim.ElectionProductionMult);
    }
}
