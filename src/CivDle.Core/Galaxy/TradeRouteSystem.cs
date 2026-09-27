using CivDle.Core.Content;

namespace CivDle.Core.Galaxy;

/// <summary>Co trasa právě dělá (karta světa, panel tras).</summary>
public enum TradeRouteStatus
{
    /// <summary>Zboží teče.</summary>
    Running,

    /// <summary>Zdrojový svět nemá co poslat (prázdný sklad nebo vše v rezervě).</summary>
    WaitingForGoods,

    /// <summary>Cílový sklad je plný (i se zbožím, které už je na cestě).</summary>
    DestinationFull,

    /// <summary>Na jednom z konců nestojí přístav.</summary>
    NoPort,
}

/// <summary>Proč trasu nejde založit.</summary>
public enum TradeBlocker
{
    /// <summary>Nic nebrání.</summary>
    None,

    /// <summary>Odkud i kam je tentýž svět.</summary>
    SameWorld,

    /// <summary>Jeden z konců není založená kolonie.</summary>
    NotColony,

    /// <summary>Zdrojový svět tuhle surovinu nevyváží.</summary>
    NotExported,

    /// <summary>Cílový svět surovinu nezná — neměl by ji kam dát ani k čemu.</summary>
    UnknownAtDestination,

    /// <summary>Stejná trasa už existuje.</summary>
    Duplicate,

    /// <summary>Galaxie má tras víc, než kolik se jich udrží přehledně.</summary>
    TooMany,
}

/// <summary>
/// Obchodní trasa (svety-design.md 2.5): svět A posílá surovinu R světu B.
/// Surovina jménem — každý svět má vlastní obsah a indexy.
/// </summary>
public sealed class TradeRoute
{
    public TradeRoute(int id, string fromWorldId, string toWorldId, string resourceId)
    {
        Id = id;
        FromWorldId = fromWorldId;
        ToWorldId = toWorldId;
        ResourceId = resourceId;
    }

    /// <summary>Stabilní číslo trasy (ukládá se, dávky na cestě na něj odkazují).</summary>
    public int Id { get; }

    /// <summary>Odkud.</summary>
    public string FromWorldId { get; }

    /// <summary>Kam.</summary>
    public string ToWorldId { get; }

    /// <summary>Co.</summary>
    public string ResourceId { get; }

    /// <summary>Kolik trasa kdy odvezla (statistika, ukládá se).</summary>
    public double TotalShipped { get; internal set; }

    /// <summary>Kolik za sekundu odplulo v posledním kroku (neukládá se).</summary>
    public double LastRate { get; internal set; }

    /// <summary>Co trasa dělala v posledním kroku (neukládá se).</summary>
    public TradeRouteStatus Status { get; internal set; } = TradeRouteStatus.Running;
}

/// <summary>
/// Dávka zboží na cestě. Nese cíl a surovinu sama — když hráč trasu zruší,
/// co už odplulo, dopluje (nic se neztratí).
/// </summary>
public sealed class Shipment
{
    public Shipment(int routeId, string toWorldId, string resourceId, double departedAt, double arrivesAt)
    {
        RouteId = routeId;
        ToWorldId = toWorldId;
        ResourceId = resourceId;
        DepartedAt = departedAt;
        ArrivesAt = arrivesAt;
    }

    /// <summary>Trasa, která dávku vypravila.</summary>
    public int RouteId { get; }

    /// <summary>Kam pluje.</summary>
    public string ToWorldId { get; }

    /// <summary>Co veze.</summary>
    public string ResourceId { get; }

    /// <summary>Kdy začala nakládat (galaktické sekundy).</summary>
    public double DepartedAt { get; }

    /// <summary>Kdy začne vykládat.</summary>
    public double ArrivesAt { get; }

    /// <summary>Kolik veze celkem.</summary>
    public double Amount { get; internal set; }

    /// <summary>Kolik už vyložila.</summary>
    public double Delivered { get; internal set; }

    /// <summary>Kolik je ještě na palubě.</summary>
    public double Aboard => Amount - Delivered;
}

/// <summary>
/// Co obchod potřebuje od světů. Aktivní svět odpovídá ze živé simulace,
/// neaktivní ze souhrnu a nevyrovnaného obchodu (<see cref="WorldRecord.PendingDelta"/>).
/// Rozhraní kvůli testům: zachování zboží se dá ověřit bez simulací.
/// </summary>
public interface ITradeWorlds
{
    /// <summary>Kolik suroviny smí svět poslat.</summary>
    double Available(string worldId, string resourceId);

    /// <summary>Kolik se jí do světa ještě vejde.</summary>
    double Room(string worldId, string resourceId);

    /// <summary>Odebere; vrací, kolik opravdu odešlo.</summary>
    double Take(string worldId, string resourceId, double amount);

    /// <summary>Přidá; vrací, kolik se opravdu vešlo.</summary>
    double Put(string worldId, string resourceId, double amount);

    /// <summary>Kolik za sekundu odbaví přístavy světa.</summary>
    double PortCapacity(string worldId);
}

/// <summary>
/// Obchod mezi světy (svety-design.md 2.5, 7.5). Každou galaktickou sekundu
/// naloží každá trasa, kolik unese, a dávky na cestě vykládají u cíle.
///
/// <para><b>Trasa nic nevytváří:</b> co odejde z A, dorazí do B — nebo je na
/// cestě. Nakládá se jen tolik, kolik se v cíli vejde i se zbožím, které už
/// pluje; když cíl přesto nestačí (hráč mezitím vyprázdnil sklady), dávka
/// počká u cíle, nic nepropadne.</para>
///
/// <para><b>Plynulý tok, ne skoky:</b> dávka se plní po dobu okna
/// (<see cref="TradeConfig.DispatchSeconds"/>) a stejně dlouho vykládá.
/// Sklad u cíle tak roste rovnoměrně, evidence toků ukazuje klidné číslo
/// a na cestě je pár dávek, ne tisíc.</para>
///
/// <para><b>Kapacita:</b> přístav se dělí mezi trasy, které jím vedou; trasa
/// dostane menší z podílů na obou koncích.</para>
///
/// <para>Vrstva: jádro galaxie (OOP). Na tik simulace nesahá — běží z
/// <see cref="GalaxySession.Update"/> jednou za galaktickou sekundu.</para>
/// </summary>
public sealed class TradeRouteSystem
{
    /// <summary>Nejvíc tras v galaxii (šest světů, pár artiklů mezi každými dvěma).</summary>
    public const int MaxRoutes = 48;

    /// <summary>Pod tímhle množstvím se nic nevozí (zaokrouhlovací šum).</summary>
    private const double Epsilon = 1e-6;

    private readonly List<TradeRoute> _routes = new();
    private readonly List<Shipment> _shipments = new();
    private readonly Dictionary<string, int> _routesThrough = new(StringComparer.Ordinal);

    /// <summary>Trasy v pořadí založení.</summary>
    public IReadOnlyList<TradeRoute> Routes => _routes;

    /// <summary>Dávky na cestě.</summary>
    public IReadOnlyList<Shipment> Shipments => _shipments;

    /// <summary>Kdy obchod naposled počítal (galaktické sekundy); NaN = ještě nikdy.</summary>
    public double LastAdvancedAt { get; set; } = double.NaN;

    /// <summary>Číslo příští trasy.</summary>
    public int NextRouteId { get; set; } = 1;

    /// <summary>Trasa podle čísla, nebo <c>null</c>.</summary>
    public TradeRoute? Find(int id) => _routes.Find(r => r.Id == id);

    /// <summary>Existuje trasa odkud–kam–co?</summary>
    public bool Exists(string from, string to, string resourceId) =>
        _routes.Exists(r => r.FromWorldId == from && r.ToWorldId == to && r.ResourceId == resourceId);

    /// <summary>Založí trasu (pravidla hlídá <see cref="GalaxySession.CanOpenRoute"/>).</summary>
    public TradeRoute Open(string from, string to, string resourceId)
    {
        var route = new TradeRoute(NextRouteId++, from, to, resourceId);
        _routes.Add(route);
        return route;
    }

    /// <summary>Obnoví trasu ze savu.</summary>
    internal TradeRoute Restore(int id, string from, string to, string resourceId, double totalShipped)
    {
        var route = new TradeRoute(id, from, to, resourceId) { TotalShipped = totalShipped };
        _routes.Add(route);
        NextRouteId = Math.Max(NextRouteId, id + 1);
        return route;
    }

    /// <summary>Obnoví dávku na cestě ze savu.</summary>
    internal void Restore(Shipment shipment) => _shipments.Add(shipment);

    /// <summary>Zruší trasu. Co už pluje, dopluje.</summary>
    public bool Close(int id) => _routes.RemoveAll(r => r.Id == id) > 0;

    /// <summary>Kolik suroviny pluje do světa (i dávky zrušených tras).</summary>
    public double InTransitTo(string worldId, string resourceId)
    {
        double total = 0;
        foreach (var shipment in _shipments)
        {
            if (shipment.ToWorldId == worldId && shipment.ResourceId == resourceId)
            {
                total += shipment.Aboard;
            }
        }

        return total;
    }

    /// <summary>Kolik veze trasa právě na cestě.</summary>
    public double InTransitOn(int routeId)
    {
        double total = 0;
        foreach (var shipment in _shipments)
        {
            if (shipment.RouteId == routeId)
            {
                total += shipment.Aboard;
            }
        }

        return total;
    }

    /// <summary>
    /// Kapacita trasy: podíl přístavu na každém konci (přístav se dělí mezi
    /// trasy, které jím vedou), z nich menší.
    /// </summary>
    public double CapacityOf(TradeRoute route, ITradeWorlds worlds)
    {
        CountRoutesThroughPorts();
        return CapacityOf(route, worlds.PortCapacity(route.FromWorldId), worlds.PortCapacity(route.ToWorldId));
    }

    /// <summary>
    /// Posune obchod do galaktického času <paramref name="now"/>: vyloží, co
    /// dorazilo, a naloží, co trasy za uplynulou dobu unesou. První volání jen
    /// nastaví hodiny.
    /// </summary>
    /// <param name="now">Galaktický čas.</param>
    /// <param name="worlds">Přístup ke světům.</param>
    /// <param name="config">Doba cesty a okno dávky.</param>
    /// <param name="stepsBetween">Kolik kroků mapy dělí dva světy.</param>
    public void Advance(double now, ITradeWorlds worlds, TradeConfig config, Func<string, string, int> stepsBetween)
    {
        if (double.IsNaN(LastAdvancedAt) || now < LastAdvancedAt)
        {
            LastAdvancedAt = now; // první krok, nebo hodiny šly zpět (načtení)
            return;
        }

        double dt = now - LastAdvancedAt;
        if (dt <= 0)
        {
            return;
        }

        LastAdvancedAt = now;

        // Napřed vykládka: uvolní místo v cíli dřív, než se na ni nakládá.
        Unload(now, worlds, config);
        Load(now, dt, worlds, config, stepsBetween);
    }

    private void Unload(double now, ITradeWorlds worlds, TradeConfig config)
    {
        for (int i = _shipments.Count - 1; i >= 0; i--)
        {
            var shipment = _shipments[i];
            if (now < shipment.ArrivesAt)
            {
                continue;
            }

            // Vykládá se tak dlouho, jak dlouho se nakládalo — sklad roste plynule.
            double share = Math.Clamp((now - shipment.ArrivesAt) / config.DispatchSeconds, 0, 1);
            double due = shipment.Amount * share - shipment.Delivered;
            if (due > Epsilon)
            {
                shipment.Delivered += worlds.Put(shipment.ToWorldId, shipment.ResourceId, due);
            }

            if (share >= 1 && shipment.Aboard <= Epsilon)
            {
                _shipments.RemoveAt(i);
            }
        }
    }

    private void Load(double now, double dt, ITradeWorlds worlds, TradeConfig config, Func<string, string, int> stepsBetween)
    {
        CountRoutesThroughPorts();
        foreach (var route in _routes)
        {
            route.LastRate = 0;
            double capacity = CapacityOf(route, worlds.PortCapacity(route.FromWorldId), worlds.PortCapacity(route.ToWorldId));
            if (capacity <= 0)
            {
                route.Status = TradeRouteStatus.NoPort;
                continue;
            }

            double room = worlds.Room(route.ToWorldId, route.ResourceId) - InTransitTo(route.ToWorldId, route.ResourceId);
            if (room <= Epsilon)
            {
                route.Status = TradeRouteStatus.DestinationFull;
                continue;
            }

            double wanted = Math.Min(capacity * dt, room);
            double taken = worlds.Available(route.FromWorldId, route.ResourceId) <= Epsilon
                ? 0
                : worlds.Take(route.FromWorldId, route.ResourceId, wanted);
            if (taken <= Epsilon)
            {
                route.Status = TradeRouteStatus.WaitingForGoods;
                continue;
            }

            Board(route, taken, now, config, stepsBetween);
            route.TotalShipped += taken;
            route.LastRate = taken / dt;
            route.Status = TradeRouteStatus.Running;
        }
    }

    /// <summary>Naloží do dávky, která se právě plní; když okno uplynulo, začne novou.</summary>
    private void Board(TradeRoute route, double amount, double now, TradeConfig config, Func<string, string, int> stepsBetween)
    {
        for (int i = _shipments.Count - 1; i >= 0; i--)
        {
            var open = _shipments[i];
            if (open.RouteId == route.Id && now - open.DepartedAt < config.DispatchSeconds && now < open.ArrivesAt)
            {
                open.Amount += amount;
                return;
            }
        }

        double travel = stepsBetween(route.FromWorldId, route.ToWorldId) * config.TravelSecondsPerStep;
        _shipments.Add(new Shipment(route.Id, route.ToWorldId, route.ResourceId, now, now + travel) { Amount = amount });
    }

    private void CountRoutesThroughPorts()
    {
        _routesThrough.Clear();
        foreach (var route in _routes)
        {
            _routesThrough[route.FromWorldId] = _routesThrough.GetValueOrDefault(route.FromWorldId) + 1;
            _routesThrough[route.ToWorldId] = _routesThrough.GetValueOrDefault(route.ToWorldId) + 1;
        }
    }

    private double CapacityOf(TradeRoute route, double fromPort, double toPort)
    {
        double fromShare = fromPort / Math.Max(1, _routesThrough.GetValueOrDefault(route.FromWorldId));
        double toShare = toPort / Math.Max(1, _routesThrough.GetValueOrDefault(route.ToWorldId));
        return Math.Min(fromShare, toShare);
    }
}
