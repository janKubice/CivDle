using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Cesty mezi cizími městy.
///
/// <para>Dřív se šlo naslepo k cíli a dlaždice, kam se dláždit nedalo, se prostě
/// přeskočily. Přes řeku z cesty zbyla přerušovaná čára a v horším případě jen
/// dva kusy dlažby na obou březích. Cesta má být <b>souvislá</b> — nebo žádná.</para>
/// </summary>
public class CityLinkRoadTests
{
    private static NpcCityLink Link() => new(
        new NpcCity(Key: 1, X: 0, Y: 0, ArchetypeIndex: 0, NameIndex: 0),
        new NpcCity(Key: 2, X: 40, Y: 0, ArchetypeIndex: 0, NameIndex: 0));

    /// <summary>Silnice, které <paramref name="towns"/> položil, seřazené podle vzniku.</summary>
    private static List<(int X, int Y)> RoadsOf(NpcTownSystem towns) =>
        towns.RoadTiles.Select(tile => (tile.X, tile.Y)).ToList();

    [Fact]
    public void ARouteAroundAnObstacleStaysContinuous()
    {
        // Zeď napříč cestou s jedinou dírou: trasa ji musí najít, ne prosvištět
        // skrz a nechat po sobě dvě části bez spojení.
        var towns = new NpcTownSystem(TestContent.Build());
        bool canPave(int x, int y) => x != 20 || y == 5;

        towns.Link(Link(), (0, 0), (40, 0), canPave);

        var roads = RoadsOf(towns);
        Assert.NotEmpty(roads);
        AssertConnected(roads);
        Assert.Contains((20, 5), roads); // jediná díra ve zdi
    }

    [Fact]
    public void AnImpassableWallLeavesNoRoadAtAll()
    {
        // Půl cesty do moře je horší než žádná cesta: hráč by viděl dlažbu, která
        // nikam nevede, a hledal by v tom chybu.
        var towns = new NpcTownSystem(TestContent.Build());
        bool canPave(int x, int y) => x != 20;

        towns.Link(Link(), (0, 0), (40, 0), canPave);

        Assert.Empty(towns.RoadTiles);
    }

    /// <summary>Každá dlaždice cesty musí sousedit hranou s nějakou další.</summary>
    private static void AssertConnected(List<(int X, int Y)> roads)
    {
        var set = new HashSet<(int X, int Y)>(roads);
        foreach (var (x, y) in roads)
        {
            bool touches =
                set.Contains((x - 1, y)) || set.Contains((x + 1, y))
                || set.Contains((x, y - 1)) || set.Contains((x, y + 1));

            Assert.True(touches, $"dlaždice cesty ({x},{y}) nesousedí s žádnou další");
        }
    }
}
