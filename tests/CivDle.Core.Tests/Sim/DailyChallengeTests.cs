using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Denní výzvy: sada je stejná pro všechny a pro daný den neměnná, počítá se jen
/// dnešní přírůstek (jinak by rozehraná hra splnila „nasbírej 150 dřeva" hned)
/// a stav přežije save.
/// </summary>
public class DailyChallengeTests
{
    private static readonly Resource[] WoodAndFood =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 0, BaseStorage: 10_000),
        new("food", new RgbColor(220, 180, 60), StartAmount: 200, BaseStorage: 10_000),
    };

    /// <summary>Fond dvou výzev: jedna na populaci (stavová), jedna na sběr (kumulativní).</summary>
    private static ChallengeCatalog Catalog(int dailyCount = 1) => new(
        new[]
        {
            new ChallengeDef("grow", new GoalCondition(MetricKind.Population, -1, 5), new[] { new ResourceAmount(0, 25) }),
            new ChallengeDef("chop", new GoalCondition(MetricKind.Harvested, 0, 10), new[] { new ResourceAmount(0, 40) }),
        },
        dailyCount);

    /// <summary>Obsah, kde je jídlem opravdu „food" — jinak by populace snědla odměnu ve dřevě.</summary>
    private static GameContent Content(ChallengeCatalog catalog) => TestContent.Build(
        resources: WoodAndFood,
        gameplay: TestContent.DefaultGameplay with { FoodResourceIndex = 1 },
        challenges: catalog);

    private static Simulation Fresh(ChallengeCatalog catalog) =>
        new(Content(catalog), new UniformTerrain((byte)1));

    // ----- výběr sady -----

    [Fact]
    public void SameDay_AlwaysPicksTheSameSet()
    {
        var first = DailyChallenges.Select(poolSize: 12, dailyCount: 3, "2026-07-27");
        var second = DailyChallenges.Select(poolSize: 12, dailyCount: 3, "2026-07-27");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentDays_EventuallyDiffer()
    {
        // Jeden konkrétní pár se shodnout může; přes týden se sada měnit musí,
        // jinak by „denní" výzvy byly pořád tytéž.
        var baseline = DailyChallenges.Select(12, 3, "2026-07-27");
        bool anyDifferent = false;
        for (int day = 28; day <= 31 && !anyDifferent; day++)
        {
            anyDifferent = !DailyChallenges.Select(12, 3, $"2026-07-{day}").SequenceEqual(baseline);
        }

        Assert.True(anyDifferent, "Sada výzev se přes několik dní vůbec nezměnila.");
    }

    [Fact]
    public void Selection_HasNoDuplicates_AndFitsThePool()
    {
        var chosen = DailyChallenges.Select(poolSize: 5, dailyCount: 5, "2026-01-01");

        Assert.Equal(5, chosen.Length);
        Assert.Equal(chosen.Length, chosen.Distinct().Count());
        Assert.All(chosen, i => Assert.InRange(i, 0, 4));
    }

    [Fact]
    public void AskingForMoreThanThePool_GivesTheWholePool()
    {
        Assert.Equal(3, DailyChallenges.Select(poolSize: 3, dailyCount: 9, "2026-01-01").Length);
    }

    // ----- pokrok -----

    [Fact]
    public void CumulativeMetric_CountsOnlyTodaysProgress()
    {
        // Hráč už nasbíral 500 dřeva; dnešní výzva má měřit od nuly.
        Assert.Equal(20, DailyChallenges.Progress(MetricKind.Harvested, current: 520, baseline: 500));
    }

    [Fact]
    public void StateMetric_CountsTheAbsoluteValue()
    {
        // „Měj 60 obyvatel" je stav, ne přírůstek — základ se neodečítá.
        Assert.Equal(60, DailyChallenges.Progress(MetricKind.Population, current: 60, baseline: 45));
    }

    [Fact]
    public void NewDay_RebasesCumulativeProgress()
    {
        var sim = Fresh(Catalog(dailyCount: 2));
        sim.SetChallengeDay("2026-07-27");
        sim.TryHarvest(3, 3, out _, out _, out _);

        long before = TotalProgress(sim, Catalog(dailyCount: 2));
        sim.SetChallengeDay("2026-07-28"); // další den → nová sada, nový základ

        Assert.True(TotalProgress(sim, Catalog(dailyCount: 2)) <= before,
            "Po přechodu na nový den se kumulativní pokrok musí vynulovat.");
        Assert.Equal("2026-07-28", sim.ChallengeDay);
    }

    [Fact]
    public void SameDayAgain_DoesNotResetProgress()
    {
        var sim = Fresh(Catalog(dailyCount: 2));
        sim.SetChallengeDay("2026-07-27");
        for (int i = 0; i < 5; i++)
        {
            sim.TryHarvest(3 + i, 3, out _, out _, out _);
        }

        long progress = TotalProgress(sim, Catalog(dailyCount: 2));
        sim.SetChallengeDay("2026-07-27"); // tentýž den (např. po znovunačtení)

        Assert.Equal(progress, TotalProgress(sim, Catalog(dailyCount: 2)));
    }

    // ----- splnění -----

    [Fact]
    public void MetChallenge_IsMarkedDone_Rewarded_AndAnnounced()
    {
        // „Měj 5 obyvatel" platí od startu, takže se splní hned po vydání sady.
        var content = Content(Catalog(dailyCount: 2));
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.SetChallengeDay("2026-07-27");

        int growSlot = sim.ActiveChallenges.ToList().IndexOf(0);
        double woodBefore = sim.GetResource(0);
        for (int i = 0; i < 12; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsChallengeDone(growSlot));
        Assert.True(sim.GetResource(0) >= woodBefore + 25);
        Assert.Contains(Notifications(sim), n => n.SubjectKey == "challenge.grow");
    }

    [Fact]
    public void WithoutChallengeDay_NothingIsIssued()
    {
        var sim = Fresh(Catalog());

        Assert.Empty(sim.ActiveChallenges);
        Assert.Equal(string.Empty, sim.ChallengeDay);
    }

    [Fact]
    public void SaveRoundtrip_KeepsTodaysSetAndProgress()
    {
        var content = Content(Catalog(dailyCount: 2));
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.SetChallengeDay("2026-07-27");
        for (int i = 0; i < 12; i++)
        {
            sim.Tick();
        }

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(sim.ChallengeDay, loaded.ChallengeDay);
        Assert.Equal(sim.ActiveChallenges, loaded.ActiveChallenges);
        for (int slot = 0; slot < sim.ActiveChallenges.Count; slot++)
        {
            Assert.Equal(sim.IsChallengeDone(slot), loaded.IsChallengeDone(slot));
            Assert.Equal(sim.ChallengeProgress(slot), loaded.ChallengeProgress(slot));
        }
    }

    /// <summary>Součet pokroku jen přes kumulativní výzvy (stavové se dnem nemění).</summary>
    private static long TotalProgress(Simulation sim, ChallengeCatalog catalog)
    {
        long total = 0;
        for (int slot = 0; slot < sim.ActiveChallenges.Count; slot++)
        {
            var condition = catalog.Challenges[sim.ActiveChallenges[slot]].Condition;
            if (DailyChallenges.IsCumulative(condition.Kind))
            {
                total += sim.ChallengeProgress(slot);
            }
        }

        return total;
    }

    private static List<GameNotification> Notifications(Simulation sim)
    {
        var all = new List<GameNotification>();
        while (sim.TryDequeueNotification(out var note))
        {
            all.Add(note);
        }

        return all;
    }
}
