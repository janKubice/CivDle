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
