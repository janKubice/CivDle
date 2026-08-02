namespace CivDle.Core.Config;

/// <summary>
/// Účet-wide profil hráče (napříč hrami a érami) — zatím odemčené achievementy.
/// Ukládá se mimo herní save (ten je per-hra), aby achievementy přetrvaly i po
/// novém startu. ID achievementů jsou stabilní (i pro budoucí napojení na Steam).
/// </summary>
public sealed class PlayerProfile
{
    /// <summary>ID odemčených achievementů (stabilní stringy).</summary>
    public List<string> UnlockedAchievements { get; set; } = new();

    /// <summary>Datum posledního vyzvednutí denní odměny (UTC, formát <c>yyyy-MM-dd</c>); prázdné = nikdy.</summary>
    public string LastDailyRewardDate { get; set; } = string.Empty;

    /// <summary>Aktuální série po sobě jdoucích dní s vyzvednutím.</summary>
    public int DailyStreak { get; set; }

    // ----- kronika (rekordy napříč všemi hrami) -----

    /// <summary>Nejvyšší populace, jakou kdy hráč měl.</summary>
    public double BestPopulation { get; set; }

    /// <summary>Nejvíc budov v jednom městě.</summary>
    public int BestBuildings { get; set; }

    /// <summary>Nejvyšší dosažený stupeň Vzestupu.</summary>
    public int BestAscension { get; set; }

    /// <summary>Kolik denních výzev už hráč splnil (celkem, napříč hrami).</summary>
    public int ChallengesCompleted { get; set; }

    /// <summary>Nejvíc sídel (osad, měst…) v jednom světě.</summary>
    public int BestSettlements { get; set; }

    /// <summary>
    /// Řadicí číslo nejvyšší dosažené éry. Ukládá se vedle <see cref="BestEraId"/>,
    /// protože porovnávat se musí podle pořadí, ale zobrazit podle jména.
    /// </summary>
    public int BestEraOrder { get; set; } = -1;

    /// <summary>ID nejvyšší dosažené éry (prázdné = žádná zaznamenaná).</summary>
    public string BestEraId { get; set; } = string.Empty;

    /// <summary>Nejvíc odevzdaných zakázek v jednom běhu.</summary>
    public long BestContracts { get; set; }

    /// <summary>Nejvíc dokončených divů světa v jednom běhu.</summary>
    public long BestWonders { get; set; }

    /// <summary>Nejdelší běh v herních sekundách (od založení světa po poslední uložení).</summary>
    public double LongestRunSeconds { get; set; }

    /// <summary>Celkový čas strávený ve hře, ve skutečných sekundách napříč všemi hrami.</summary>
    public double TotalPlaySeconds { get; set; }

    /// <summary>ID biomů, na kterých hráč někdy stavěl (sběratelský cíl kroniky).</summary>
    public List<string> SettledBiomes { get; set; } = new();

    /// <summary>
    /// Zapíše rekordy z právě běžící hry. Jen posouvá nahoru — kronika je síň
    /// slávy, ne aktuální stav, takže horší hra do ní nesmí zasáhnout.
    /// </summary>
    public bool RecordBest(double population, int buildings, int ascension)
    {
        bool changed = false;
        if (population > BestPopulation) { BestPopulation = population; changed = true; }
        if (buildings > BestBuildings) { BestBuildings = buildings; changed = true; }
        if (ascension > BestAscension) { BestAscension = ascension; changed = true; }
        return changed;
    }

    /// <summary>
    /// Zapíše celý souhrn běhu — hlavní vstup do kroniky. Stejné pravidlo jako
    /// u <see cref="RecordBest"/>: jen nahoru, nikdy dolů.
    /// </summary>
    /// <returns>True, když se něco posunulo (a profil má cenu uložit).</returns>
    public bool RecordRun(in RunRecord run)
    {
        bool changed = RecordBest(run.Population, run.Buildings, run.Ascension);
        if (run.Settlements > BestSettlements) { BestSettlements = run.Settlements; changed = true; }
        if (run.Contracts > BestContracts) { BestContracts = run.Contracts; changed = true; }
        if (run.Wonders > BestWonders) { BestWonders = run.Wonders; changed = true; }
        if (run.RunSeconds > LongestRunSeconds) { LongestRunSeconds = run.RunSeconds; changed = true; }

        // Éra se porovnává podle pořadí; jméno se veze s ním, aby kronika uměla
        // napsat „Doba železná" i pro éru, kterou hráč naposledy viděl před rokem.
        if (run.EraOrder > BestEraOrder && !string.IsNullOrEmpty(run.EraId))
        {
            BestEraOrder = run.EraOrder;
            BestEraId = run.EraId;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Připočte odehraný čas. Volá se s <b>přírůstkem</b> od minulého zápisu, ne
    /// s celkem — jinak by se čas při každém uložení sečetl znovu.
    /// </summary>
    public void AddPlaytime(double seconds)
    {
        if (seconds > 0)
        {
            TotalPlaySeconds += seconds;
        }
    }

    /// <summary>Přidá biom do kroniky; vrací true, když je to novinka (stojí za uložení).</summary>
    public bool RecordBiome(string biomeId)
    {
        if (SettledBiomes.Contains(biomeId))
        {
            return false;
        }

        SettledBiomes.Add(biomeId);
        return true;
    }
}

/// <summary>
/// Souhrn jednoho běhu pro kroniku. Struktura místo dlouhého seznamu parametrů:
/// devět čísel za sebou se dřív nebo později prohodí a nikdo si toho nevšimne.
/// </summary>
/// <param name="Population">Populace při zápisu.</param>
/// <param name="Buildings">Počet budov.</param>
/// <param name="Ascension">Dosažený stupeň Vzestupu.</param>
/// <param name="Settlements">Počet sídel ve světě.</param>
/// <param name="EraOrder">Řadicí číslo aktuální éry (−1 = neznámá).</param>
/// <param name="EraId">ID aktuální éry.</param>
/// <param name="Contracts">Kolik zakázek hráč v tomhle běhu odevzdal.</param>
/// <param name="Wonders">Kolik divů světa v tomhle běhu dokončil.</param>
/// <param name="RunSeconds">Jak dlouho běh trvá v herních sekundách.</param>
public readonly record struct RunRecord(
    double Population,
    int Buildings,
    int Ascension,
    int Settlements,
    int EraOrder,
    string EraId,
    long Contracts,
    long Wonders,
    double RunSeconds);
