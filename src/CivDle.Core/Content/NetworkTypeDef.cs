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
    RgbColor OverlayColor,
    IReadOnlyList<TerrainSource>? TerrainSourcesOrNull = null,
    NetworkHousing? Housing = null,
    NetworkGround? Ground = null,
    string ShortageLook = NetworkTypeDef.PlainLook)
{
    /// <summary>Vzhled budovy bez sítě: nic zvláštního (jen stav v inspektoru).</summary>
    public const string PlainLook = "none";

    /// <summary>Vzhled budovy bez tepla: jinovatka, rampouchy, zhasnutá okna.</summary>
    public const string FrostLook = "frost";

    /// <summary>Vzhled budovy bez vztlaku (Nebesa): klesá do mraků, tmavne a zhasne.</summary>
    public const string SinkLook = "sink";

    /// <summary>Známé vzhledy výpadku — loader jiné odmítne.</summary>
    public static IReadOnlySet<string> ShortageLooks { get; } =
        new HashSet<string>(StringComparer.Ordinal) { PlainLook, FrostLook, SinkLook };

    /// <summary>
    /// Přírodní zdroje: dlaždice biomu, které do sítě dodávají samy (oáza na
    /// Duně, horký pramen na Mrazu). Nikdo je nestaví a nikdo je nezboří.
    /// </summary>
    public IReadOnlyList<TerrainSource> TerrainSources => TerrainSourcesOrNull ?? Array.Empty<TerrainSource>();

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
/// <param name="CutoffBelow">
/// Tvrdý práh jen pro tuhle budovu: pod tímto pokrytím nejede vůbec (datlový
/// háj bez vody neurodí nic), i když síť jinak jen zpomaluje. 0 = řídí se druhem sítě.
/// </param>
public readonly record struct NetworkUse(int NetworkIndex, int Supply, int Demand, int RelayRange, double CutoffBelow = 0);

/// <summary>Přírodní zdroj sítě: kolik dodá každá dlaždice biomu.</summary>
/// <param name="BiomeIndex">Biom, který dodává (oáza).</param>
/// <param name="SupplyPerTile">Výkon jedné dlaždice.</param>
public sealed record TerrainSource(int BiomeIndex, double SupplyPerTile);

/// <summary>
/// Co nedostatek sítě dělá s bydlením: dům bez vody neroste tak rychle a žije
/// se v něm hůř. Obojí je podíl — půl města bez vody = půl postihu.
/// </summary>
/// <param name="GrowthPenalty">O kolik se zpomalí růst, když nemá síť nikdo (0–1).</param>
/// <param name="HappinessPenalty">Kolik bodů spokojenosti to vezme, když nemá síť nikdo (0–1).</param>
public sealed record NetworkHousing(double GrowthPenalty, double HappinessPenalty);

/// <summary>
/// Kdy zdroj dodává: stále, jen ve dne (sluneční zrcadla — nejvíc v poledne),
/// jen v noci (lapač rosy), nebo jen v bouřkovém pásu (hromosvod).
/// </summary>
public enum SupplyTime
{
    /// <summary>Pořád stejně.</summary>
    Always,

    /// <summary>Ve dne podle výšky slunce, v noci nic.</summary>
    Day,

    /// <summary>Jen v noci.</summary>
    Night,

    /// <summary>Jen když přes město jde bouřkový pás (<see cref="BurialLook.Storm"/>).</summary>
    Storm,
}

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

/// <summary>
/// Stopa sítě na zemi (svety-design.md 5.3 — „mechanika svítí"): kam síť
/// dosáhne, tam se půda změní — v poušti zezelená, na Mrazu roztaje sníh.
/// Hranice sítě je tak vidět bez překryvu.
/// </summary>
/// <param name="Color">Barva stopy (tráva, mokrá hlína).</param>
/// <param name="BiomeMask">Na kterých biomech se stopa kreslí (indexováno biomem); voda a skála ne.</param>
/// <param name="Density">Jak hustě (0–1): kolik dlaždic plně zásobené buňky dostane trs.</param>
public sealed record NetworkGround(RgbColor Color, IReadOnlyList<bool> BiomeMask, double Density);

