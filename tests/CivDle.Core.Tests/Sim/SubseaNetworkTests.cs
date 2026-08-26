using CivDle.Core.Sim;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Šíření podmořské sítě. Testuje se nad vymyšlenou mapou (funkce „je tady
/// voda"), ne nad generovaným terénem — chyby v záplavě se hledají mnohem líp,
/// když je pobřeží nakreslené v testu.
/// </summary>
public class SubseaNetworkTests
{
    [Fact]
    public void DisabledNetworkCoversNothing()
    {
        var network = new SubseaNetwork(0, AllWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });

        Assert.False(network.IsEnabled);
        Assert.Equal(0, network.CoveredTiles);
        Assert.False(network.Covers(0, 0));
    }

    [Fact]
    public void WithoutAnchorsNothingIsReachable()
    {
        var network = new SubseaNetwork(5, AllWater);
        network.Rebuild(Array.Empty<SubseaNetwork.Anchor>());

        Assert.False(network.Covers(0, 0));
    }

    [Fact]
    public void ReachIsMeasuredInStepsNotInStraightLine()
    {
        var network = new SubseaNetwork(3, AllWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });

        Assert.True(network.Covers(3, 0));      // tři kroky rovně
        Assert.True(network.Covers(2, 1));      // tři kroky přes roh
        Assert.False(network.Covers(4, 0));     // o krok dál už ne
        Assert.False(network.Covers(2, 2));     // čtyři kroky přes roh
    }

    [Fact]
    public void LandIsAWall()
    {
        // Svislá hráz na x = 2 s jedinou skulinou u y = 10. Za hrází je voda,
        // ale doplavat se tam dá jenom oklikou — a ta je delší než dosah.
        bool IsWater(int x, int y) => x != 2 || y == 10;

        var network = new SubseaNetwork(4, IsWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });

        Assert.True(network.Covers(1, 0), "voda před hrází musí být v dosahu");
        Assert.False(network.Covers(3, 0), "za hrází se nesmí objevit dosah vzdušnou čarou");
    }

    [Fact]
    public void TheNarrowGapIsUsedWhenItFits()
    {
        // Táž hráz, ale skulina hned vedle kotvy a dosah stačí na oblouk kolem ní.
        bool IsWater(int x, int y) => x != 2 || y == 1;

        var network = new SubseaNetwork(4, IsWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });

        Assert.True(network.Covers(3, 1), "skulinou se protéct musí");
    }

    [Fact]
    public void AHarbourOnLandStartsFromTheWaterNextToIt()
    {
        // Souš vlevo od x = 0, moře vpravo. Přístav stojí celý na souši.
        bool IsWater(int x, int y) => x >= 0;

        var network = new SubseaNetwork(3, IsWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(-2, 0, 2, 2) });

        Assert.True(network.Covers(0, 0), "voda u mola musí být v dosahu");
        Assert.True(network.Covers(3, 0));
        Assert.False(network.Covers(4, 0));
    }

    [Fact]
    public void AnchorTilesThemselvesAreNotWaterAndStayOut()
    {
        bool IsWater(int x, int y) => x >= 0;

        var network = new SubseaNetwork(3, IsWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(-2, 0, 2, 2) });

        Assert.False(network.Covers(-1, 0), "souš pod přístavem není podmořská dlaždice");
    }

    [Fact]
    public void TwoAnchorsAddUp()
    {
        var network = new SubseaNetwork(2, AllWater);
        network.Rebuild(new[]
        {
            new SubseaNetwork.Anchor(0, 0, 1, 1),
            new SubseaNetwork.Anchor(20, 0, 1, 1),
        });

        Assert.True(network.Covers(2, 0));
        Assert.True(network.Covers(22, 0));
        Assert.False(network.Covers(11, 0), "mezi nimi je díra, tu nikdo nepokrývá");
    }

    [Fact]
    public void RemovingAnAnchorTakesItsReachAway()
    {
        var network = new SubseaNetwork(2, AllWater);
        var both = new[]
        {
            new SubseaNetwork.Anchor(0, 0, 1, 1),
            new SubseaNetwork.Anchor(20, 0, 1, 1),
        };

        network.Rebuild(both);
        Assert.True(network.Covers(22, 0));

        network.Rebuild(new[] { both[0] });
        Assert.False(network.Covers(22, 0), "po zboření přístavu musí jeho moře zmizet");
        Assert.True(network.Covers(2, 0), "ten druhý přístav ale zůstává");
    }

    [Fact]
    public void OverlappingAnchorsDoNotCountTilesTwice()
    {
        var network = new SubseaNetwork(2, AllWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });
        int alone = network.CoveredTiles;

        network.Rebuild(new[]
        {
            new SubseaNetwork.Anchor(0, 0, 1, 1),
            new SubseaNetwork.Anchor(0, 0, 1, 1),
        });

        Assert.Equal(alone, network.CoveredTiles);
    }

    [Fact]
    public void FootprintMustFitEntirely()
    {
        var network = new SubseaNetwork(2, AllWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });

        Assert.True(network.CoversFootprint(0, 0, 2, 1));
        Assert.False(network.CoversFootprint(1, 1, 2, 2), "roh 3,2 je pět kroků daleko");
    }

    [Fact]
    public void RebuildingIsRepeatable()
    {
        // Tentýž vstup musí dát tentýž výsledek — jinak by se save a načtení
        // rozešly a hráči by po restartu zmizely stavební plochy.
        var network = new SubseaNetwork(6, (x, y) => (x + y) % 7 != 0);
        var anchors = new[] { new SubseaNetwork.Anchor(1, 1, 2, 2) };

        network.Rebuild(anchors);
        int first = network.CoveredTiles;
        network.Rebuild(anchors);

        Assert.Equal(first, network.CoveredTiles);
    }

    [Fact]
    public void ClearingEmptiesTheNetwork()
    {
        var network = new SubseaNetwork(3, AllWater);
        network.Rebuild(new[] { new SubseaNetwork.Anchor(0, 0, 1, 1) });
        network.Clear();

        Assert.Equal(0, network.CoveredTiles);
        Assert.False(network.Covers(1, 0));
    }

    private static bool AllWater(int x, int y) => true;
}
