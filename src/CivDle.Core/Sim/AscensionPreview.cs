namespace CivDle.Core.Sim;

/// <summary>
/// Co přesně Vzestup udělá — spočítané dopředu, aby si to hráč mohl přečíst
/// dřív, než klikne.
///
/// <para>Proč to existuje: Vzestup je jediná nevratná akce ve hře. Do téhle
/// chvíle o něm tlačítko říkalo jen „+N bodů" a hráč se o tom, že přišel
/// o město, silnice i výzkum, dozvěděl až potom. Nejde o balanc, ale
/// o informovaný souhlas — hráč nemá být překvapený vlastním rozhodnutím.</para>
///
/// <para>Neměnný snímek stavu (CLAUDE.md: definice a přehledy jsou <c>record</c>y).
/// Nic nemění, jen popisuje.</para>
/// </summary>
/// <param name="PointsGained">Kolik bodů Vzestup udělí.</param>
/// <param name="PointsAfter">Kolik bodů bude hráč mít po Vzestupu.</param>
/// <param name="LevelAfter">Jaké měřítko (úroveň Vzestupu) tím hráč dosáhne.</param>
/// <param name="NextRequirement">Práh pro další Vzestup po tomhle.</param>
/// <param name="Buildings">Kolik budov o Vzestupu zanikne.</param>
/// <param name="Population">Kolik obyvatel se rozejde.</param>
/// <param name="RoadTiles">Kolik dlaždic silnic zmizí.</param>
/// <param name="Zones">Kolik zón se zruší.</param>
/// <param name="Techs">Kolik vyzkoumaných technologií se zapomene.</param>
/// <param name="Districts">Kolik čtvrtí se rozpadne.</param>
/// <param name="Wonders">Kolik divů světa v novém měřítku zmizí.</param>
/// <param name="UpgradesOwned">Kolik trvalých upgradů si hráč nechá.</param>
public readonly record struct AscensionPreview(
    long PointsGained,
    long PointsAfter,
    int LevelAfter,
    long NextRequirement,
    int Buildings,
    long Population,
    int RoadTiles,
    int Zones,
    int Techs,
    int Districts,
    long Wonders,
    int UpgradesOwned)
{
    /// <summary>Je vůbec o co přijít? (První Vzestup z prázdné mapy není ztráta.)</summary>
    public bool LosesAnything => Buildings > 0 || RoadTiles > 0 || Zones > 0 || Techs > 0;
}

/// <summary>
/// Bilance doběhnutého běhu — co za tou jednou nevratnou akcí zůstalo.
///
/// <para>Proč to ve hře je: prestige bez ohlédnutí je jen tlačítko „smazat
/// město". Shrnutí z něj dělá tečku za kapitolou — hráč vidí, kam došel,
/// a hlavně jestli to bylo dál než minule. To „dál než minule" je celý motor
/// opakovaného hraní.</para>
/// </summary>
/// <param name="Level">Kolikátý Vzestup to byl.</param>
/// <param name="DurationTicks">Jak dlouho běh trval (v ticích simulace).</param>
/// <param name="PeakPopulation">Nejvyšší dosažená populace běhu.</param>
/// <param name="Buildings">Kolik budov na konci stálo.</param>
/// <param name="Techs">Kolik technologií se stihlo vyzkoumat.</param>
/// <param name="Wonders">Kolik divů světa město dostavělo.</param>
/// <param name="PointsEarned">Kolik bodů Vzestupu běh vynesl.</param>
/// <param name="IsBestPopulation">Byl to nejlidnatější běh dosud?</param>
/// <param name="PreviousBestPopulation">S čím se poměřoval (0 = první běh).</param>
public readonly record struct RunSummary(
    int Level,
    long DurationTicks,
    long PeakPopulation,
    int Buildings,
    int Techs,
    long Wonders,
    long PointsEarned,
    bool IsBestPopulation,
    long PreviousBestPopulation)
{
    /// <summary>Jak dlouho běh trval v sekundách — UI z toho píše čas.</summary>
    public double DurationSeconds => DurationTicks / (double)Simulation.TicksPerSecond;

    /// <summary>Prázdné shrnutí (žádný běh ještě neskončil).</summary>
    public static RunSummary None { get; } = new(0, 0, 0, 0, 0, 0, 0, false, 0);

    /// <summary>Skončil vůbec nějaký běh?</summary>
    public bool Exists => Level > 0;
}
