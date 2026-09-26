namespace CivDle.Core.Content;

/// <summary>Co udělá s budovou, když jí síť nestačí.</summary>
public enum NetworkShortage
{
    /// <summary>
    /// Výroba zpomalí poměrně podle pokrytí (proud, voda). Nedostatek je
    /// zpomalení čtvrti, ne vypínač.
    /// </summary>
    Slowdown,

    /// <summary>
    /// Pod prahem budova vypadne úplně (zamrzne, klesne) a sama se vrátí,
    /// jakmile pokrytí stoupne. Nic se nerozbije — měkký tlak, svety-design.md 1.2.
    /// </summary>
    Cutoff,
}

/// <summary>
/// Druh sítě (svety-design.md 7.2): elektřina, voda, teplo, vztlak. Všechny
/// se šíří stejně — zdroj zaplní svou buňku a teče do sousedních, dokud stačí
/// výkon — a liší se jen dosahem a tím, co znamená nedostatek.
///
/// <para><b>Proč data:</b> síť je „co" (dosah, práh, barva překryvu), šíření
/// je „jak". Nový svět s vlastní sítí tak nepotřebuje nový systém, jen řádek
/// v <c>networks.json</c> a budovy, které ji dodávají a chtějí.</para>
/// </summary>
/// <param name="Id">Stabilní ID (<c>power</c>, <c>water</c>, <c>heat</c>, <c>lift</c>).</param>
/// <param name="Range">Kolik buněk 8×8 od zdroje síť dosáhne (0 = vypnutá).</param>
/// <param name="Shortage">Co znamená nedostatek.</param>
/// <param name="CutoffBelow">Pod jakým pokrytím budova vypadne (jen <see cref="NetworkShortage.Cutoff"/>).</param>
/// <param name="OverlayColor">Barva překryvu sítě v nabídce Pohled.</param>
public sealed record NetworkTypeDef(
    string Id,
    int Range,
    NetworkShortage Shortage,
    double CutoffBelow,
    RgbColor OverlayColor)
{
    /// <summary>ID elektrické sítě — ta jediná se v datech budov píše starými poli.</summary>
    public const string PowerId = "power";

    /// <summary>Lokalizační klíč jména sítě.</summary>
    public string NameKey => $"network.{Id}";

    /// <summary>Šíří se síť vůbec? (Elektřina bez bloku <c>power</c> je globální číslo.)</summary>
    public bool IsEnabled => Range > 0;
}

/// <summary>
/// Jak budova používá jednu síť (mimo elektřinu, ta má svá stará pole).
/// </summary>
/// <param name="NetworkIndex">Index druhu sítě v <see cref="NetworkCatalog"/>.</param>
/// <param name="Supply">Kolik do sítě dodává.</param>
/// <param name="Demand">Kolik ze sítě chce.</param>
/// <param name="RelayRange">
/// Relé: síť, která dosáhne na buňku s touto budovou, teče dál, jako by tu
/// byl zdroj s tímto dosahem (tepelná věž, cisterna, kanát). 0 = není relé.
/// </param>
public readonly record struct NetworkUse(int NetworkIndex, int Supply, int Demand, int RelayRange);

/// <summary>
/// Druhy sítí. Index 0 je vždy elektřina — nastavuje ji <c>gameplay.json</c>
/// (blok <c>power</c>) a budovy ji mají ve starých polích <c>powerSupply</c> /
/// <c>powerDemand</c>, takže starší data i mody fungují beze změny. Ostatní
/// druhy přidává <c>networks.json</c>.
/// </summary>
public sealed class NetworkCatalog
{
    /// <summary>Index elektřiny.</summary>
    public const int PowerIndex = 0;

    private readonly List<NetworkTypeDef> _types;

    /// <param name="power">Elektřina (z bloku <c>power</c> v <c>gameplay.json</c>).</param>
    /// <param name="others">Další sítě z <c>networks.json</c>, v pořadí souboru.</param>
    public NetworkCatalog(NetworkTypeDef power, IReadOnlyList<NetworkTypeDef> others)
    {
        _types = new List<NetworkTypeDef>(others.Count + 1) { power };
        _types.AddRange(others);
    }

    /// <summary>Jen elektřina — obsah bez <c>networks.json</c> a testy.</summary>
    public static NetworkCatalog PowerOnly(PowerConfig power) => new(PowerType(power), Array.Empty<NetworkTypeDef>());

    /// <summary>Elektřina jako druh sítě: dosah z <c>gameplay.json</c>, nedostatek zpomaluje.</summary>
    public static NetworkTypeDef PowerType(PowerConfig power) =>
        new(NetworkTypeDef.PowerId, power.Range, NetworkShortage.Slowdown, 0, new RgbColor(255, 214, 90));

    /// <summary>
    /// Tentýž katalog s jiným nastavením elektřiny (přebití <c>gameplay</c>
    /// výzvou). Ostatní sítě zůstanou.
    /// </summary>
    public NetworkCatalog WithPower(PowerConfig power) => new(PowerType(power), _types.GetRange(1, _types.Count - 1));

    /// <summary>Všechny druhy; index 0 = elektřina.</summary>
    public IReadOnlyList<NetworkTypeDef> Types => _types;

    /// <summary>Počet druhů včetně elektřiny.</summary>
    public int Count => _types.Count;

    /// <summary>Druh sítě podle indexu.</summary>
    public NetworkTypeDef this[int index] => _types[index];

    /// <summary>Index druhu podle ID, −1 = neznámý.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < _types.Count; i++)
        {
            if (_types[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
