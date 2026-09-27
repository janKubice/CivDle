using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using Xunit;

namespace CivDle.Core.Tests.Galaxy;

/// <summary>
/// Obchodní trasy (svety-design.md 2.5) nad falešnými světy: zachování
/// zboží, doba cesty, kapacita přístavů, plný cíl, prázdný zdroj a zrušení
/// trasy. Bez simulací — pravidla obchodu se tak dají ověřit přesně.
/// </summary>
public class TradeRouteTests
{
    private static readonly TradeConfig Config = new(TravelSecondsPerStep: 20, DispatchSeconds: 5);

    [Fact]
    public void ARouteCreatesNothingWhatLeavesArrivesOrIsOnTheWay()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", stock: 1_000, cap: 10_000, port: 4);
        worlds.Set("b", "glass", stock: 0, cap: 10_000, port: 4);
        var trade = new TradeRouteSystem();
        var route = trade.Open("a", "b", "glass");

        for (int second = 0; second <= 120; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
            double left = 1_000 - worlds.Stock("a", "glass");
            double arrived = worlds.Stock("b", "glass");
            Assert.Equal(left, arrived + trade.InTransitTo("b", "glass"), 6);
        }

        Assert.Equal(1_000 - worlds.Stock("a", "glass"), route.TotalShipped, 6);
        Assert.True(worlds.Stock("b", "glass") > 0, "za dvě minuty má něco dorazit");
    }

    [Fact]
    public void GoodsArriveOnlyAfterTheTrip()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 4);
        worlds.Set("b", "glass", 0, 10_000, port: 4);
        var trade = new TradeRouteSystem();
        trade.Open("a", "b", "glass");

        for (int second = 0; second < 20; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
        }

        Assert.Equal(0, worlds.Stock("b", "glass"));      // cesta trvá 20 s
        Assert.Equal(76, trade.InTransitTo("b", "glass"), 6); // 19 s × 4/s už pluje

        for (int second = 20; second <= 30; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
        }

        Assert.True(worlds.Stock("b", "glass") > 0);
    }

    [Fact]
    public void TheTripTakesLongerBetweenFartherWorlds()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 4);
        worlds.Set("far", "glass", 0, 10_000, port: 4);
        var trade = new TradeRouteSystem();
        trade.Open("a", "far", "glass");

        for (int second = 0; second < 45; second++)
        {
            trade.Advance(second, worlds, Config, (_, _) => 3); // tři kroky = 60 s
        }

        Assert.Equal(0, worlds.Stock("far", "glass"));
    }

    [Fact]
    public void ThePortIsSharedByTheRoutesThatUseIt()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 6);
        worlds.Set("a", "spice", 1_000, 10_000, port: 6);
        worlds.Set("b", "glass", 0, 10_000, port: 100);
        worlds.Set("b", "spice", 0, 10_000, port: 100);
        var trade = new TradeRouteSystem();
        var glass = trade.Open("a", "b", "glass");
        var spice = trade.Open("a", "b", "spice");

        trade.Advance(0, worlds, Config, Steps);
        trade.Advance(10, worlds, Config, Steps);

        Assert.Equal(3, glass.LastRate, 6); // karavanseraj za 6/s se dělí na dvě trasy
        Assert.Equal(3, spice.LastRate, 6);
        Assert.Equal(3, trade.CapacityOf(glass, worlds), 6);
    }

    [Fact]
    public void ARouteWaitsWhenTheSourceIsEmptyAndRunsAgainLater()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 0, 10_000, port: 4);
        worlds.Set("b", "glass", 0, 10_000, port: 4);
        var trade = new TradeRouteSystem();
        var route = trade.Open("a", "b", "glass");

        trade.Advance(0, worlds, Config, Steps);
        trade.Advance(1, worlds, Config, Steps);
        Assert.Equal(TradeRouteStatus.WaitingForGoods, route.Status);

        worlds.Add("a", "glass", 50);
        trade.Advance(2, worlds, Config, Steps);
        Assert.Equal(TradeRouteStatus.Running, route.Status);
    }

    [Fact]
    public void NothingIsLoadedThatWouldNotFitAtTheDestination()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 50);
        worlds.Set("b", "glass", 90, 100, port: 50);
        var trade = new TradeRouteSystem();
        var route = trade.Open("a", "b", "glass");

        for (int second = 0; second <= 60; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
        }

        Assert.Equal(100, worlds.Stock("b", "glass"), 6);
        Assert.Equal(10, route.TotalShipped, 6); // víc se nevejde, víc neodjelo
        Assert.Equal(TradeRouteStatus.DestinationFull, route.Status);
    }

    [Fact]
    public void WithoutAPortNothingMoves()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 4);
        worlds.Set("b", "glass", 0, 10_000, port: 0);
        var trade = new TradeRouteSystem();
        var route = trade.Open("a", "b", "glass");

        trade.Advance(0, worlds, Config, Steps);
        trade.Advance(10, worlds, Config, Steps);

        Assert.Equal(TradeRouteStatus.NoPort, route.Status);
        Assert.Equal(1_000, worlds.Stock("a", "glass"));
    }

    [Fact]
    public void ClosingARouteLetsWhatIsOnTheWayArrive()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 4);
        worlds.Set("b", "glass", 0, 10_000, port: 4);
        var trade = new TradeRouteSystem();
        var route = trade.Open("a", "b", "glass");
        for (int second = 0; second <= 10; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
        }

        double shipped = route.TotalShipped;
        Assert.True(trade.Close(route.Id));
        for (int second = 11; second <= 60; second++)
        {
            trade.Advance(second, worlds, Config, Steps);
        }

        Assert.Equal(shipped, worlds.Stock("b", "glass"), 6);
        Assert.Empty(trade.Shipments);
        Assert.Equal(1_000 - shipped, worlds.Stock("a", "glass"), 6);
    }

    [Fact]
    public void AFirstStepOnlySetsTheClock()
    {
        var worlds = new FakeWorlds();
        worlds.Set("a", "glass", 1_000, 10_000, port: 4);
        worlds.Set("b", "glass", 0, 10_000, port: 4);
        var trade = new TradeRouteSystem();
        trade.Open("a", "b", "glass");

        trade.Advance(5_000, worlds, Config, Steps); // načtená hra: žádný náklad za minulost

        Assert.Equal(1_000, worlds.Stock("a", "glass"));
    }

    private static int Steps(string from, string to) => 1;

    /// <summary>Světy jako čísla: zásoba, sklad a přístav po světech.</summary>
    private sealed class FakeWorlds : ITradeWorlds
    {
        private readonly Dictionary<(string, string), double> _stock = new();
        private readonly Dictionary<(string, string), double> _cap = new();
        private readonly Dictionary<string, double> _port = new();

        public void Set(string world, string resource, double stock, double cap, double port)
        {
            _stock[(world, resource)] = stock;
            _cap[(world, resource)] = cap;
            _port[world] = port;
        }

        public void Add(string world, string resource, double amount) => _stock[(world, resource)] += amount;

        public double Stock(string world, string resource) => _stock[(world, resource)];

        public double Available(string worldId, string resourceId) => _stock[(worldId, resourceId)];

        public double Room(string worldId, string resourceId) =>
            _cap[(worldId, resourceId)] - _stock[(worldId, resourceId)];

        public double Take(string worldId, string resourceId, double amount)
        {
            double taken = Math.Min(amount, _stock[(worldId, resourceId)]);
            _stock[(worldId, resourceId)] -= taken;
            return taken;
        }

        public double Put(string worldId, string resourceId, double amount)
        {
            double put = Math.Min(amount, Room(worldId, resourceId));
            _stock[(worldId, resourceId)] += put;
            return put;
        }

        public double PortCapacity(string worldId) => _port.GetValueOrDefault(worldId);
    }
}
