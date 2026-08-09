using CivDle.Core.Config;

namespace CivDle.Rendering;

/// <summary>
/// Kdy se co přestane kreslit. Jedno místo pro všechny prahy, ne konstanta
/// schovaná v každém rendereru zvlášť.
///
/// <para>Proč to vzniklo: prahy byly rozházené (0.5 tady, 0.55 tam) a všechny
/// seděly těsně nad hranicí agregátního pohledu. Vzniklo z toho úzké pásmo
/// přiblížení, ve kterém se kreslily desetitisíce drobných spritů přes celou
/// obrazovku — měření ukázalo 191 ms na snímek při zoomu 0.55 a 3 ms hned
/// o kousek dál, kde už se nekreslilo nic. Padesátinásobný útes na půl otáčky
/// kolečka.</para>
///
/// <para>Princip: detail se nemá vypínat naráz, ale po vrstvách odshora dolů.
/// Čím dál je kamera, tím dřív zmizí věci, které stejně nejsou vidět —
/// dekorace jsou pár pixelů, stromy o něco víc, budovy nejdéle.</para>
///
/// <para><b>Hráč si celou tu stupnici posouvá</b> volbou v nastavení
/// (<see cref="DetailQuality"/>). Násobič je jeden pro všechny vrstvy, takže
/// jejich pořadí zůstane zachované — mění se jen, jak brzy se vzdají. Stroje
/// se liší řádově a jedna konstanta v kódu nemůže vyhovět všem.</para>
///
/// <para>Proč statické: prahy čte deset rendererů v každém snímku a jsou to
/// čistě prezentační data bez stavu. Protahovat jeden float konstruktory
/// všech vrstev by nic nezpřehlednilo. Nastavuje se na jednom místě
/// (<see cref="Apply"/>) při startu a při změně nastavení.</para>
/// </summary>
public static class DetailLevel
{
    /// <summary>Výchozí (vyvážené) prahy — hodnoty, se kterými se hra ladila.</summary>
    public const float BaseDecorations = 1.25f;

    /// <summary>Výchozí práh těžitelných uzlů.</summary>
    public const float BaseHarvestables = 0.95f;

    /// <summary>Výchozí práh detailních spritů budov.</summary>
    public const float BaseBuildingSprites = 1.0f;

    /// <summary>Výchozí práh živých drobností.</summary>
    public const float BaseCreatures = 0.9f;

    /// <summary>Výchozí rozpočet na procházení dlaždic po jedné.</summary>
    public const int BaseMaxPerTileWork = 9_000;

    /// <summary>Zvolený stupeň detailu (výchozí = vyvážený).</summary>
    public static DetailQuality Quality { get; private set; } = DetailQuality.Balanced;

    /// <summary>
    /// Násobič prahů. Menší číslo = detail vydrží dál do dálky (a stojí víc
    /// výkonu), větší = vzdá se dřív.
    /// </summary>
    public static float Factor { get; private set; } = 1.0f;

    /// <summary>
    /// Pod tímhle přiblížením se nekreslí drobné dekorace (kytky, kamínky).
    /// Jsou to 1–6px tečky; při menším zoomu z nich je jen šum, ale platí se
    /// za ně plnou cenou.
    /// </summary>
    public static float Decorations { get; private set; } = BaseDecorations;

    /// <summary>
    /// Pod tímhle přiblížením se nekreslí těžitelné uzly (stromy, kameny).
    /// Jsou větší než dekorace, takže vydrží déle — ale je jich jeden na
    /// dlaždici, takže při oddálení jde o desetitisíce spritů.
    /// </summary>
    public static float Harvestables { get; private set; } = BaseHarvestables;

    /// <summary>
    /// Pod tímhle přiblížením se budovy kreslí zjednodušeně: jeden barevný
    /// obdélník místo spritu, stínu a odznaků. Tvar města zůstane čitelný,
    /// jen se za něj neplatí třemi kresbami na budovu.
    /// </summary>
    public static float BuildingSprites { get; private set; } = BaseBuildingSprites;

    /// <summary>
    /// Pod tímhle přiblížením se nekreslí živé drobnosti (chodci, zvěř,
    /// bubliny, zlaté nálezy). Na téhle vzdálenosti jsou to jednotlivé pixely.
    /// </summary>
    public static float Creatures { get; private set; } = BaseCreatures;

    /// <summary>
    /// Kolik dlaždic se ještě smí projít po jedné. Nad tímhle počtem se vrstva
    /// vzdá bez ohledu na zoom — pojistka pro nezvyklé poměry stran a rozlišení,
    /// aby jedno okno navíc neshodilo snímkovou frekvenci.
    /// </summary>
    public static int MaxPerTileWork { get; private set; } = BaseMaxPerTileWork;

    /// <summary>
    /// Násobič prahů pro daný stupeň. Nižší stupeň = větší číslo = detail se
    /// vzdá dřív.
    /// </summary>
    public static float FactorFor(DetailQuality quality) => quality switch
    {
        DetailQuality.Performance => 1.6f,
        DetailQuality.Detailed => 0.65f,
        DetailQuality.Maximum => 0.4f,
        _ => 1.0f,
    };

    /// <summary>
    /// Rozpočet na procházení dlaždic pro daný stupeň. Roste s detailem, jinak
    /// by pojistka uřízla přesně to, co si hráč vyšším stupněm zaplatil.
    /// </summary>
    public static int BudgetFor(DetailQuality quality) => quality switch
    {
        DetailQuality.Performance => 4_500,
        DetailQuality.Detailed => 20_000,
        DetailQuality.Maximum => 45_000,
        _ => BaseMaxPerTileWork,
    };

    /// <summary>Přepne prahy na zvolený stupeň detailu (volá se ze změny nastavení).</summary>
    public static void Apply(DetailQuality quality)
    {
        Quality = quality;
        Factor = FactorFor(quality);
        Decorations = BaseDecorations * Factor;
        Harvestables = BaseHarvestables * Factor;
        BuildingSprites = BaseBuildingSprites * Factor;
        Creatures = BaseCreatures * Factor;
        MaxPerTileWork = BudgetFor(quality);
    }

    /// <summary>
    /// Posune vlastní práh vrstvy podle nastavení. Vrstvy živého města mají
    /// každá svou vzdálenost (auta vydrží dál než chodci) a společný násobič
    /// jim ten odstup zachová.
    /// </summary>
    public static float Scale(float baseZoom) => baseZoom * Factor;

    /// <summary>
    /// Dočasně vypne LOD úplně: všechny vrstvy se kreslí bez ohledu na zoom.
    ///
    /// <para>Pro fotku. Na obrazovce je LOD správně — z výšky jsou stromy pod
    /// rozlišením a platit za ně plnou cenu nemá smysl. Na obrázku, který si
    /// hráč prohlíží nebo někam pošle, ale nikdo nikam nespěchá a chybějící
    /// detail je jediná věc, které si všimne.</para>
    ///
    /// <para>Používej s <c>using</c> — po dokreslení se prahy vrátí.</para>
    /// </summary>
    public static IDisposable FullDetail()
    {
        var scope = new DetailScope(Quality, Factor, Decorations, Harvestables, BuildingSprites, Creatures, MaxPerTileWork);
        Factor = 0f;
        Decorations = 0f;
        Harvestables = 0f;
        BuildingSprites = 0f;
        Creatures = 0f;
        MaxPerTileWork = int.MaxValue;
        return scope;
    }

    /// <summary>Zapamatované prahy, které se po dokreslení vrátí zpátky.</summary>
    private sealed class DetailScope : IDisposable
    {
        private readonly DetailQuality _quality;
        private readonly float _factor;
        private readonly float _decorations;
        private readonly float _harvestables;
        private readonly float _buildings;
        private readonly float _creatures;
        private readonly int _budget;

        public DetailScope(
            DetailQuality quality, float factor, float decorations, float harvestables,
            float buildings, float creatures, int budget)
        {
            _quality = quality;
            _factor = factor;
            _decorations = decorations;
            _harvestables = harvestables;
            _buildings = buildings;
            _creatures = creatures;
            _budget = budget;
        }

        public void Dispose()
        {
            Quality = _quality;
            Factor = _factor;
            Decorations = _decorations;
            Harvestables = _harvestables;
            BuildingSprites = _buildings;
            Creatures = _creatures;
            MaxPerTileWork = _budget;
        }
    }

    /// <summary>Vejde se procházení dlaždic v daném obdélníku do rozpočtu?</summary>
    public static bool FitsBudget(int startX, int startY, int endX, int endY)
    {
        // Obě strany zvlášť: obrácený obdélník (konec před začátkem) má obě
        // délky záporné a jejich součin by vyšel kladně — takový výřez se má
        // přeskočit, ne projít pozpátku.
        long width = (long)endX - startX + 1;
        long height = (long)endY - startY + 1;
        return width > 0 && height > 0 && width * height <= MaxPerTileWork;
    }
}
