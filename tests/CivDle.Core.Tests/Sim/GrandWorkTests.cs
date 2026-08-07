using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Velké dílo: bezedný odběr přebytků.
///
/// <para>Testuje se to, kvůli čemu vzniklo — že se do něj dá sypat donekonečna
/// (cena každého stupně roste), že dokončený stupeň dá TRVALÝ bonus, a že to
/// všechno přežije Vzestup i uložení. Bez posledních dvou bodů by to byl jen
/// způsob, jak suroviny spálit.</para>
/// </summary>
public class GrandWorkTests
{
    private const int Wood = 0;

    private static GameContent Content(double costGrowth = 2.0, int unlockAt = 0)
    {
        var config = new GrandWorkConfig(
            new[]
            {
                new GrandWorkStage(new[] { new ResourceAmount(Wood, 100) }, "production_mult", 0.5),
            },
            costGrowth,
            unlockAt);

        return TestContent.Build(grandWork: config);
    }

    private static Simulation NewSim(GameContent content) =>
        new(content, new UniformTerrain(1));

    [Fact]
    public void PouringInFinishesAStageAndGrantsAPermanentBonus()
    {
        var sim = NewSim(Content());
        sim.AddResource(Wood, 1000);

        double before = sim.Bonuses.ProductionMult;
        sim.InvestInGrandWork(Wood);

        Assert.Equal(1, sim.GrandWorkStage);
        Assert.Equal(before * 1.5, sim.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void EachStageCostsMore()
    {
        // Tohle dělá z díla BEZEDNÝ odběr: cena roste geometricky, takže se do
        // něj dá sypat, i když produkce vyletí o řády.
        var sim = NewSim(Content(costGrowth: 2.0));
        sim.AddResource(Wood, 100_000);

        Assert.Equal(100, sim.GrandWorkCost()[0].Amount, 1);
        sim.InvestInGrandWork(Wood);

        Assert.Equal(200, sim.GrandWorkCost()[0].Amount, 1);
        sim.InvestInGrandWork(Wood);

        Assert.Equal(400, sim.GrandWorkCost()[0].Amount, 1);
    }

    [Fact]
    public void PartialPourIsRemembered()
    {
        var sim = NewSim(Content());

        // Přesypat se dá jen to, co hráč má — startovní zásoba se počítá taky.
        double stock = sim.GetResource(Wood);
        sim.InvestInGrandWork(Wood);

        Assert.Equal(0, sim.GrandWorkStage);                     // stupeň ještě nehotový
        Assert.Equal(100 - stock, sim.GrandWorkRemaining(Wood), 1);
        Assert.Equal(0, sim.GetResource(Wood), 1);               // suroviny odešly do díla
    }

    [Fact]
    public void PouringNeverWastesMoreThanTheStageNeeds()
    {
        var sim = NewSim(Content());
        sim.AddResource(Wood, 1000);

        sim.InvestInGrandWork(Wood);

        // Vezme si 100 na stupeň, zbytek nechá — přesypat víc by suroviny
        // jen spálilo.
        Assert.Equal(900, sim.GetResource(Wood), 1);
    }

    [Fact]
    public void TheWorkSurvivesAscension()
    {
        // Celý smysl díla: je to jediná osa, která roste NAPŘÍČ běhy. Kdyby ho
        // Vzestup smazal, nemělo by cenu do něj sypat.
        var content = TestContent.Build(
            prestige: new PrestigeConfig(
                new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5),
            grandWork: new GrandWorkConfig(
                new[] { new GrandWorkStage(new[] { new ResourceAmount(Wood, 100) }, "production_mult", 0.5) },
                2.0, 0));

        var sim = NewSim(content);
        sim.AddResource(Wood, 1000);
        sim.InvestInGrandWork(Wood);
        double afterStage = sim.Bonuses.ProductionMult;

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(1, sim.GrandWorkStage);
        Assert.Equal(afterStage, sim.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void TheWorkSurvivesASave()
    {
        var content = Content();
        var sim = NewSim(content);

        // Přesně na první stupeň (100), ať po něm nic nezbude.
        sim.AddResource(Wood, 100 - sim.GetResource(Wood));
        sim.InvestInGrandWork(Wood);
        Assert.Equal(1, sim.GrandWorkStage);

        // Druhý stupeň jen načneme: chce 200, dáme 50.
        sim.AddResource(Wood, 50);
        sim.InvestInGrandWork(Wood);
        Assert.Equal(1, sim.GrandWorkStage);

        var metadata = new SaveMetadata(7, "s", "test", DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(1, loaded.GrandWorkStage);
        Assert.Equal(sim.GrandWorkRemaining(Wood), loaded.GrandWorkRemaining(Wood), 1);
        Assert.Equal(sim.Bonuses.ProductionMult, loaded.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void LockedUntilTheFirstAscension()
    {
        // Dřív než po Vzestupu hráč nemá co přebývat — nabídka „sypej sem" by
        // v té chvíli jen mátla.
        var sim = NewSim(Content(unlockAt: 1));
        sim.AddResource(Wood, 1000);

        Assert.False(sim.GrandWorkAvailable);
        Assert.Equal(0, sim.InvestInGrandWork(Wood));
        Assert.Equal(1000, sim.GetResource(Wood), 1);
    }
}
