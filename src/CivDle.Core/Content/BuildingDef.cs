namespace CivDle.Core.Content;

/// <summary>Množství jedné suroviny — surovina jako index, ne string (hot path).</summary>
public sealed record ResourceAmount(int ResourceIndex, int Amount);

/// <summary>
/// Výrobní recept budovy: každých <paramref name="TimeTicks"/> tiků spotřebuje
/// vstupy a vyrobí výstupy. Bez vstupů = těžba/produkce z terénu.
/// </summary>
public sealed record Recipe(
    IReadOnlyList<ResourceAmount> Inputs,
    IReadOnlyList<ResourceAmount> Outputs,
    int TimeTicks);

/// <summary>
/// Co budova dělá s okolím za sekundu provozu. Kladné číslo špiní, záporné čistí —
/// čistička je v datech tatáž věc s obráceným znaménkem, ne zvláštní druh budovy.
///
/// <para>Právě tohle drží bronzovou dobu čistou: znečištění nevzniká z kódu, ale
/// z dat, a hutě, doly a továrny ho mají až od industriální éry dál.</para>
/// </summary>
/// <param name="Air">Kouř — nese se daleko a kazí lidem náladu.</param>
/// <param name="Water">Splašky — kalí vodu a dusí pobřežní výrobu.</param>
/// <param name="Soil">Hlušina a struska — otravují půdu pod poli a lesy.</param>
public sealed record PollutionOutput(double Air, double Water, double Soil)
{
    /// <summary>Budova, které je okolí lhostejné (výchozí stav všech starých dat).</summary>
    public static PollutionOutput None { get; } = new(0, 0, 0);

    /// <summary>Nedělá tahle budova s okolím vůbec nic?</summary>
    public bool IsNeutral => Air == 0 && Water == 0 && Soil == 0;

    /// <summary>Čistí budova aspoň jeden kanál? (Čističky si platí údržbu, špinící ne.)</summary>
    public bool IsCleaner => Air < 0 || Water < 0 || Soil < 0;

    /// <summary>Hodnota jednoho kanálu — ať se nemusí větvit u každé budovy.</summary>
    public double Get(Sim.PollutionKind kind) => kind switch
    {
        Sim.PollutionKind.Air => Air,
        Sim.PollutionKind.Water => Water,
        _ => Soil,
    };
}

/// <summary>
/// Milníky za počet budov jednoho typu: „každá desátá pila zrychlí všechny pily".
///
/// <para>Proč to ve hře je: dvacátá farma byla do téhle chvíle stejně zajímavá
/// jako první — přibyla další porce výroby a nic víc. Tohle je motor, na kterém
/// stojí celý žánr: stavět tutéž budovu dokola má smysl, protože každý kus
/// posouvá k viditelnému prahu, a po jeho překročení se zlepší <b>všechny</b>
/// budovy toho typu naráz.</para>
///
/// <para>Práh je „každých N", ne výčet: díky tomu je nekonečný a škáluje
/// s velkoměstem stejně jako s vesnicí. Strop drží čísla při zemi.</para>
/// </summary>
/// <param name="Every">Po kolika budovách přijde další stupeň.</param>
/// <param name="BonusPerStep">O kolik každý stupeň zvedne výrobu (0.25 = +25 %).</param>
/// <param name="MaxSteps">Kolik stupňů se nejvýš počítá.</param>
public sealed record BuildingMilestones(int Every, double BonusPerStep, int MaxSteps)
{
    /// <summary>Kolikátý stupeň má město při daném počtu budov (0 = zatím žádný).</summary>
    public int TierFor(long count) =>
        Every <= 0 ? 0 : (int)Math.Min(MaxSteps, count / Every);

    /// <summary>Násobič výroby při daném počtu budov.</summary>
    public double MultiplierFor(long count) => 1.0 + TierFor(count) * BonusPerStep;

    /// <summary>
    /// Kolik budov ještě chybí do dalšího stupně; 0 = strop je vyčerpaný.
    /// UI z toho píše „ještě 3 do dalšího stupně" — bez toho je milník neviditelný.
    /// </summary>
    public long ToNextTier(long count)
    {
        if (Every <= 0 || TierFor(count) >= MaxSteps)
        {
            return 0;
        }

        return Every - count % Every;
    }
}

/// <summary>
/// Co budova pravidelně předvede. Behavior-ID z JSON (viz data-driven-content.md):
/// data říkají <b>co a jak často</b>, kód <b>jak to nakreslit</b>.
/// </summary>
public enum SpectacleEffect
{
    /// <summary>Start rakety: sloup ohně, stoupající tečka, vlečka kouře.</summary>
    RocketLaunch,

    /// <summary>Urychlovač částic: prstenec, který se roztočí a bliskne.</summary>
    ParticleBeam,

    /// <summary>Pulz z orbitálního prstence: kruhová vlna přes okolí.</summary>
    RingPulse,

    /// <summary>Výheň světa: výšleh roztaveného kovu z komína.</summary>
    ForgeFlare,

    /// <summary>Maják na vrcholu věže: pomalu pulzující světlo.</summary>
    SpireBeacon,
}

/// <summary>
/// Podívaná megastruktury — periodický efekt, kvůli kterému se hráč vrátí
/// kamerou k tomu, co s takovou námahou postavil.
///
/// <para>Proč to ve hře je: div světa se stavěl desítky minut a pak jen stál.
/// Odměna, která se odehraje jednou při dostavbě, je odměna, kterou hráč zažije
/// jednou. Tohle z megastruktury dělá místo, kam se vyplatí koukat i potom.</para>
///
/// <para>Vrstva: čistě kulisa. Interval a druh jsou v datech, samotné kreslení
/// v renderu — simulace o podívané neví.</para>
/// </summary>
/// <param name="Effect">Který efekt se přehraje.</param>
/// <param name="IntervalSeconds">Jak často (v sekundách herního času).</param>
public sealed record BuildingSpectacle(SpectacleEffect Effect, double IntervalSeconds);

/// <summary>
/// Zvalidovaná definice budovy z <c>data/buildings.json</c> (typ; instance jsou
/// struktury v plochém poli simulace). Jméno je v jazykových souborech pod
/// <c>building.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Category">Kategorie do stavebního menu (housing, production, storage, civic…).</param>
/// <param name="MapColor">Barva budovy na mapě (fallback, když chybí sprite).</param>
/// <param name="FootprintWidth">Šířka v dlaždicích.</param>
/// <param name="FootprintHeight">Výška v dlaždicích.</param>
/// <param name="WorkerSlots">Kolik pracovníků budova zaměstná (0 = nepracovní budova).</param>
/// <param name="HousingCapacity">O kolik zvýší kapacitu bydlení (domy).</param>
/// <param name="BuildCost">Cena stavby.</param>
/// <param name="Recipe">Výroba; <c>null</c> = budova nevyrábí (dům).</param>
/// <param name="AllowedBiomes">Maska povolených biomů indexovaná indexem biomu.</param>
/// <param name="StorageBonus">O kolik budova zvýší kapacitu skladu surovin (sklady).</param>
/// <param name="AutoBuild">Smí ji stavět civilizace sama (auto-domy dle poptávky)?</param>
/// <param name="Buildable">Smí ji hráč přímo postavit (upgrade cíle = false, jen přes vylepšení)?</param>
/// <param name="UpgradesToIndex">Index budovy, na kterou lze vylepšit; −1 = konec řady.</param>
/// <param name="UpgradeCost">Cena vylepšení na další úroveň.</param>
/// <param name="PowerSupply">Kolik elektřiny budova dodává (elektrárny); 0 = žádnou.</param>
/// <param name="PowerDemand">Kolik elektřiny budova potřebuje; &gt;0 = její výroba škáluje s pokrytím sítě.</param>
/// <param name="MergesToIndex">Index budovy, na kterou se sloučí blok 2×2 stejných; −1 = neslučuje se.</param>
/// <param name="MergeCostOrNull">Cena sloučení (nad rámec už postavených budov).</param>
public sealed record BuildingDef(
    string Id,
    string Category,
    RgbColor MapColor,
    int FootprintWidth,
    int FootprintHeight,
    int WorkerSlots,
    int HousingCapacity,
    IReadOnlyList<ResourceAmount> BuildCost,
    Recipe? Recipe,
    bool[] AllowedBiomes,
    IReadOnlyList<ResourceAmount> StorageBonus,
    bool AutoBuild,
    bool Buildable,
    int UpgradesToIndex,
    IReadOnlyList<ResourceAmount> UpgradeCost,
    int PowerSupply,
    int PowerDemand,
    bool RequiresAdjacentWater = false,
    int ServiceValue = 0,
    IReadOnlyList<ResourceAmount>? UpkeepOrNull = null,
    int MergesToIndex = -1,
    IReadOnlyList<ResourceAmount>? MergeCostOrNull = null,
    AdjacencyRule? AdjacencyOrNull = null,
    int BuildTicks = 0,
    int TerrainHarvestRadius = 0,
    PollutionOutput? PollutionOrNull = null,
    int MinSettlementRank = -1,
    BuildingMilestones? MilestonesOrNull = null,
    BuildingSpectacle? SpectacleOrNull = null,
    int ReforestRadius = 0)
{
    /// <summary>
    /// Podívaná, kterou budova pravidelně předvádí; <c>null</c> = jen stojí.
    /// </summary>
    public BuildingSpectacle? Spectacle => SpectacleOrNull;

    /// <summary>Dělá tahle budova občas něco, na co stojí za to koukat?</summary>
    public bool HasSpectacle => SpectacleOrNull is not null;

    /// <summary>
    /// Milníky za počet budov tohoto typu; <c>null</c> = typ milníky nemá
    /// a chová se jako dřív.
    /// </summary>
    public BuildingMilestones? Milestones => MilestonesOrNull;

    /// <summary>Odměňuje se u tohohle typu množství?</summary>
    public bool HasMilestones => MilestonesOrNull is not null;

    /// <summary>
    /// Jak velké sídlo budova potřebuje (index stupně); −1 = kdekoli.
    ///
    /// <para>Existuje kvůli tomu, aby velikost místa něco znamenala: letiště
    /// nepatří do osady o třech chalupách. Drtivá většina budov požadavek nemá,
    /// takže rané hry se to vůbec netýká.</para>
    /// </summary>
    public bool NeedsSettlementRank => MinSettlementRank >= 0;

    /// <summary>
    /// Co budova dělá s okolím (špiní, čistí, nebo nic). Chybí-li v datech, je
    /// budova k okolí neutrální — starší obsah se tím chová jako dřív.
    /// </summary>
    public PollutionOutput Pollution => PollutionOrNull ?? PollutionOutput.None;

    /// <summary>Sahá tahle budova vůbec na znečištění? (Zkratka pro pomalý systém.)</summary>
    public bool AffectsPollution => PollutionOrNull is not null && !PollutionOrNull.IsNeutral;

    /// <summary>
    /// Bere budova surovinu přímo z krajiny? &gt;0 = dosah v dlaždicích, ve kterém
    /// těží uzly (les, ložisko). 0 = budova zpracovává dovezené vstupy a na
    /// okolní krajině jí nezáleží.
    ///
    /// <para>Tohle je ten rozdíl mezi „klikáním ubývá les" a „ubývá les". Pila
    /// nekácí proto, že na ni hráč klikne — kácí proto, že v ní pracují lidé.
    /// Čím větší město, tím víc lidí, tím rychleji mizí okolní porost.</para>
    /// </summary>
    public bool HarvestsTerrain => TerrainHarvestRadius > 0;

    /// <summary>
    /// Vysazuje budova okolí zpátky? (Lesní školka a spol.)
    ///
    /// <para>Proč to existuje: pily a lomy krajinu vytěží rychleji, než sama
    /// doroste, a hráči zůstane holina, se kterou nemůže nic dělat. Reforestace
    /// je odpověď — stojí místo i dělníky, ale les vrací.</para>
    /// </summary>
    public bool Reforests => ReforestRadius > 0;

    /// <summary>
    /// Jak dlouho se budova staví (v ticích). 0 = stojí hned, jako všechno ostatní.
    ///
    /// <para>Existuje kvůli divům světa: megastruktura, která vyroste jedním
    /// kliknutím, je jen drahá budova. S odpočtem je z ní událost — na mapě stojí
    /// staveniště, hráč se k němu vrací a dokončení něco znamená.</para>
    /// </summary>
    public bool TakesTimeToBuild => BuildTicks > 0;

    /// <summary>
    /// Pravidlo bonusu za okolí (pila u lesa, lom u hor); <c>null</c> = budově
    /// na okolí nezáleží a vyrábí všude stejně.
    /// </summary>
    public AdjacencyRule? Adjacency => AdjacencyOrNull;

    /// <summary>Záleží téhle budově na tom, co má kolem sebe?</summary>
    public bool HasAdjacencyBonus => AdjacencyOrNull is not null;

    /// <summary>
    /// Cena za sloučení bloku 2×2 do jedné velké budovy. Doplácí se k tomu, co už
    /// stojí — hráč čtyři domy nestaví znovu, jen je přestaví na jeden velký.
    /// </summary>
    public IReadOnlyList<ResourceAmount> MergeCost => MergeCostOrNull ?? Array.Empty<ResourceAmount>();

    /// <summary>Dá se čtveřice těchhle budov v bloku 2×2 sloučit v jednu větší?</summary>
    public bool CanMergeIntoBigger => MergesToIndex >= 0;

    /// <summary>
    /// Kolik „bodů služby" budova poskytuje (trh, sýpka, lázně…). 0 = budova
    /// obyvatele neobsluhuje. Přepočet na lidi řídí gameplay.json.
    /// </summary>
    public int Services => ServiceValue;

    /// <summary>
    /// Opakovaná cena za provoz. Bez zaplacení budova přestane sloužit (nic se
    /// neboří) — díky tomu jsou služby rozhodnutí, ne jednorázový nákup.
    /// </summary>
    public IReadOnlyList<ResourceAmount> Upkeep => UpkeepOrNull ?? Array.Empty<ResourceAmount>();

    /// <summary>
    /// Musí budova sousedit s vodou? Přístavy a rybolov dávají smysl jen na břehu —
    /// tím dostává pobřeží ekonomickou identitu (living-map.md §5).
    /// </summary>
    public bool NeedsWaterAccess => RequiresAdjacentWater;

    /// <summary>Potřebuje budova ke své výrobě elektřinu?</summary>
    public bool NeedsPower => PowerDemand > 0;

    /// <summary>Lokalizační klíč jména budovy.</summary>
    public string NameKey => $"building.{Id}";

    /// <summary>Smí budova stát na dlaždici s daným biomem?</summary>
    public bool IsBiomeAllowed(int biomeIndex) => AllowedBiomes[biomeIndex];

    /// <summary>Má budova další úroveň, na kterou lze vylepšit?</summary>
    public bool HasUpgrade => UpgradesToIndex >= 0;
}
