using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Civilizační doktríny: první rozhodnutí ve hře, které něco <b>vylučuje</b>.
///
/// <para>Hlídají se čtyři věci z plánu: dvě doktríny se nesčítají, efekt se
/// projeví hned po koupi, volba přežije save — a žádná cesta není proti
/// ostatním dvakrát silnější.</para>
/// </summary>
public class DoctrineTests
{
    [Fact]
    public void RealContentHasDoctrinesWithReachableNodes()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Doctrines.IsEnabled);
        foreach (var doctrine in content.Doctrines.Doctrines)
        {
            Assert.NotEmpty(doctrine.Nodes);

            // Aspoň jeden uzel bez prerekvizit — jinak by se do cesty nedalo vstoupit.
            Assert.Contains(doctrine.Nodes, node => node.PrerequisiteIndices.Count == 0);
        }
    }

    [Fact]
    public void NoDoctrineIsTwiceAsStrongAsAnother()
    {
        // Balanční test z plánu. Součet síly není přesné měřítko (efekty míří
        // na různá čísla), ale spolehlivě chytí cestu, která je o řád jinde.
        var content = TestData.LoadRealContent();
        var doctrines = content.Doctrines.Doctrines;

        double weakest = doctrines.Min(d => d.TotalMagnitude);
        double strongest = doctrines.Max(d => d.TotalMagnitude);

        Assert.True(
            strongest <= weakest * 2,
            $"nejsilnější doktrína dává {strongest:F2}, nejslabší {weakest:F2}");
    }

    [Fact]
    public void DoctrinesCostRoughlyTheSame()
    {
        var content = TestData.LoadRealContent();
        var doctrines = content.Doctrines.Doctrines;

        int cheapest = doctrines.Min(d => d.TotalCost);
        int dearest = doctrines.Max(d => d.TotalCost);

        Assert.True(dearest <= cheapest * 2, $"nejdražší stojí {dearest}, nejlevnější {cheapest}");
    }

    [Fact]
    public void BuyingANodeShowsUpInTheMultipliersRightAway()
    {
        var sim = World();
        sim.DebugGrantPrestigePoints(100);
        Assert.True(sim.TryChooseDoctrine(0));

        double before = sim.Bonuses.ProductionMult;
        Assert.True(sim.TryBuyDoctrineNode(0));

        Assert.True(sim.Bonuses.ProductionMult > before, "koupený uzel se nikam nepromítl");
    }

    [Fact]
    public void OnlyTheChosenDoctrineCounts()
    {
        // Kdyby se sčítaly obě, byla by volba k ničemu a hráč by sbíral všechny.
        var sim = World();
        sim.DebugGrantPrestigePoints(100);
        Assert.True(sim.TryChooseDoctrine(0));
        Assert.True(sim.TryBuyDoctrineNode(0));

        double onlyFirst = sim.Bonuses.ProductionMult;

        // Druhá doktrína míří na týž násobič. Kdyby se dala mít vedle první,
        // bylo by to tady vidět.
        Assert.False(sim.CanChooseDoctrine, "cestu s koupeným uzlem nesmí jít změnit");
        Assert.False(sim.TryChooseDoctrine(1));
        Assert.Equal(onlyFirst, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void ANodeNeedsItsPrerequisite()
    {
        var sim = World();
        sim.DebugGrantPrestigePoints(100);
        Assert.True(sim.TryChooseDoctrine(0));

        Assert.False(sim.CanBuyDoctrineNode(1), "druhý uzel jde koupit bez prvního");
        Assert.True(sim.TryBuyDoctrineNode(0));
        Assert.True(sim.CanBuyDoctrineNode(1));
    }

    [Fact]
    public void WithoutPointsNothingIsBought()
    {
        var sim = World();
        Assert.True(sim.TryChooseDoctrine(0));

        Assert.False(sim.CanBuyDoctrineNode(0));
        Assert.False(sim.TryBuyDoctrineNode(0));
        Assert.Equal(0, sim.DoctrineNodesOwned);
    }

    [Fact]
    public void AscendingRefundsThePointsAndReopensTheChoice()
    {
        // Doktrína je tvar TÉHLE civilizace. Kdyby se body nevracely, platila
        // by první volba napořád a nebyla by to volba, ale osud.
        var sim = World();
        sim.DebugGrantPrestigePoints(100);
        Assert.True(sim.TryChooseDoctrine(0));
        Assert.True(sim.TryBuyDoctrineNode(0));

        long afterBuying = sim.PrestigePoints;
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.True(sim.PrestigePoints > afterBuying, "body za doktrínu se nevrátily");
        Assert.Equal(-1, sim.DoctrineIndex);
        Assert.Equal(0, sim.DoctrineNodesOwned);
        Assert.True(sim.CanChooseDoctrine);
    }

    [Fact]
    public void TheChoiceSurvivesSaveAndLoad()
    {
        var content = DoctrineContent();
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.DebugGrantPrestigePoints(100);
        Assert.True(sim.TryChooseDoctrine(1));
        Assert.True(sim.TryBuyDoctrineNode(0));

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.Equal(1, loaded.DoctrineIndex);
        Assert.True(loaded.IsDoctrineNodeOwned(0));
        Assert.Equal(sim.Bonuses.ProductionMult, loaded.Bonuses.ProductionMult, 6);
    }

    // ----- pomocné -----

    private static Simulation World()
    {
        var sim = new Simulation(DoctrineContent(), new UniformTerrain(1), seed: 1);
        return sim;
    }

    private static GameContent DoctrineContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };

        return TestContent.Build(
            biomes: biomes,

            // Levný Vzestup, ať se dá v testu dosáhnout bez pěstování města.
            prestige: new PrestigeConfig(
                new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 1),
            doctrines: new DoctrineCatalog(new[]
            {
                new DoctrineDef("first", new[]
                {
                    new DoctrineNodeDef("a", "production_mult", 0.2, 3, Array.Empty<int>()),
                    new DoctrineNodeDef("b", "production_mult", 0.3, 6, new[] { 0 }),
                }),
                new DoctrineDef("second", new[]
                {
                    new DoctrineNodeDef("a", "production_mult", 0.25, 3, Array.Empty<int>()),
                }),
            }));
    }
}
