namespace CivDle.Core.Content;

/// <summary>
/// Co budova umí, když se blíží útok. Chybí-li v datech, budova se nebrání —
/// drtivá většina jich takových je a nic navíc je to nestojí.
/// </summary>
/// <param name="Range">Dostřel v dlaždicích.</param>
/// <param name="Damage">Kolik ubere jedna rána.</param>
/// <param name="IntervalTicks">Jak často střílí.</param>
public sealed record DefenseRule(int Range, int Damage, int IntervalTicks)
{
    /// <summary>Brání se budova vůbec?</summary>
    public bool IsArmed => Range > 0 && Damage > 0;
}

/// <summary>Jeden druh útočníka.</summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>attacker.&lt;id&gt;</c>).</param>
/// <param name="Sprite">ID spritu.</param>
/// <param name="Health">Kolik vydrží.</param>
/// <param name="SpeedTilesPerTick">Jak rychle jde k městu.</param>
/// <param name="Damage">Kolik tiků vyřadí budovu, do které se pustí.</param>
/// <param name="AttackIntervalTicks">Jak často udeří.</param>
public sealed record AttackerDef(
    string Id,
    string Sprite,
    int Health,
    double SpeedTilesPerTick,
    int Damage,
    int AttackIntervalTicks);

/// <summary>Kolik čeho přijde v jedné vlně.</summary>
/// <param name="AttackerIndex">Index druhu útočníka.</param>
/// <param name="Count">Kolik jich je.</param>
public readonly record struct WaveEntry(int AttackerIndex, int Count);

/// <summary>
/// Frontier Defense — <b>volitelný režim</b>, ne nové pravidlo pro všechny.
///
/// <para>Tohle je ta podmínka, na které stojí, jestli přídavek hru poškodí.
/// CivDle je relaxační builder; kdo režim nezapne, o boji se nedozví. Zapíná
/// se při zakládání světa a zpátky cesta nevede — stejně jako u pískoviště,
/// a ze stejného důvodu: jinak by se dal zapnout na dobré vlně a vypnout na
/// zlé.</para>
///
/// <para>Rozvrh vln je <b>deterministický</b>: v kterém tiku, co a jak silné.
/// Žádná náhoda bez seedu — jinak by se rozešel save i test.</para>
/// </summary>
/// <param name="FirstWaveTick">Kdy přijde první vlna.</param>
/// <param name="WaveIntervalTicks">Rozestup mezi vlnami.</param>
/// <param name="StrengthGrowth">Kolikrát je každá další vlna početnější.</param>
/// <param name="SpawnDistance">Jak daleko od města se útočníci objeví (dlaždice).</param>
/// <param name="RepairTicks">Za jak dlouho se poškozená budova sama opraví.</param>
/// <param name="Attackers">Druhy útočníků.</param>
/// <param name="Waves">Vzor vln; cyklí se dokola a sílí.</param>
public sealed record FrontierConfig(
    int FirstWaveTick,
    int WaveIntervalTicks,
    double StrengthGrowth,
    int SpawnDistance,
    int RepairTicks,
    IReadOnlyList<AttackerDef> Attackers,
    IReadOnlyList<IReadOnlyList<WaveEntry>> Waves)
{
    /// <summary>Hra bez obrany (starší data, mody).</summary>
    public static FrontierConfig Disabled { get; } = new(
        0, 0, 1.0, 0, 0, Array.Empty<AttackerDef>(), Array.Empty<IReadOnlyList<WaveEntry>>());

    /// <summary>Dá se režim vůbec zapnout? (Prázdná data = ne.)</summary>
    public bool IsAvailable => Attackers.Count > 0 && Waves.Count > 0 && WaveIntervalTicks > 0;

    /// <summary>Vzor vlny (cyklí se dokola).</summary>
    public IReadOnlyList<WaveEntry> WaveAt(int wave) => Waves[wave % Waves.Count];

    /// <summary>V kterém tiku přijde n-tá vlna (číslováno od nuly).</summary>
    public long TickOfWave(int wave) => FirstWaveTick + (long)wave * WaveIntervalTicks;

    /// <summary>
    /// Kolik útočníků daného záznamu přijde v n-té vlně. Roste geometricky —
    /// desátá vlna má být znát, ne být totéž co první.
    /// </summary>
    public int CountInWave(int wave, WaveEntry entry) =>
        Math.Max(1, (int)Math.Round(entry.Count * Math.Pow(StrengthGrowth, wave)));
}
