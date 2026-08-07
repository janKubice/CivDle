using CivDle.Core.Sim;

namespace CivDle.Core.Platform;

/// <summary>
/// Jeden žebříček: jak se jmenuje u platformy, čím se plní a jak se zobrazuje.
/// </summary>
/// <param name="Id">API jméno u platformy (musí sedět se Steamworks).</param>
/// <param name="LabelKey">Lokalizační klíč nadpisu.</param>
/// <param name="Ascending">Je nižší lepší? (Rychlostní žebříčky ano.)</param>
/// <param name="IsTime">Je hodnota čas v milisekundách? (Formátuje se jinak než počet.)</param>
public sealed record LeaderboardDef(string Id, string LabelKey, bool Ascending = false, bool IsTime = false);

/// <summary>
/// Most mezi simulací a platformou: <b>jediné místo, kde jsou API jména</b>
/// achievementů, statistik a žebříčků.
///
/// <para>Proč jedno místo: Steamworks jména jsou řetězce, které se nedají
/// zkontrolovat překladačem. Kdyby byly roztroušené po kódu, jeden překlep by
/// znamenal achievement, který se tiše nikdy neodemkne — a to je chyba, kterou
/// najdeš až po vydání. Takhle jsou pohromadě a dají se ověřit testem proti
/// vygenerovanému CSV.</para>
///
/// <para>Vrstva: jádro, čte ze simulace, nezapisuje do ní.</para>
/// </summary>
public static class PlatformCatalog
{
    /// <summary>Z herního ID achievementu udělá API jméno u platformy.</summary>
    public static string AchievementApiName(string achievementId) =>
        "ACH_" + achievementId.ToUpperInvariant();

    // ----- statistiky -----

    public const string StatPeakPopulation = "STAT_PEAK_POPULATION";
    public const string StatTotalBuildings = "STAT_TOTAL_BUILDINGS";
    public const string StatAscensions = "STAT_ASCENSIONS";
    public const string StatLegacies = "STAT_LEGACIES";
    public const string StatGrandWorkStage = "STAT_GRAND_WORK_STAGE";
    public const string StatTotalPower = "STAT_TOTAL_POWER";
    public const string StatTechsResearched = "STAT_TECHS_RESEARCHED";
    public const string StatCitiesAbsorbed = "STAT_CITIES_ABSORBED";
    public const string StatTilesExplored = "STAT_TILES_EXPLORED";
    public const string StatPlaytimeSeconds = "STAT_PLAYTIME_SECONDS";

    /// <summary>
    /// Žebříčky, které hra nabízí. Pořadí je pořadí na obrazovce — nahoře to,
    /// co hráče zajímá nejvíc.
    /// </summary>
    public static IReadOnlyList<LeaderboardDef> Leaderboards { get; } = new[]
    {
        new LeaderboardDef("LB_TOTAL_POWER", "board.power"),
        new LeaderboardDef("LB_PEAK_POPULATION", "board.population"),
        new LeaderboardDef("LB_ASCENSIONS", "board.ascensions"),
        new LeaderboardDef("LB_LEGACIES", "board.legacies"),
        new LeaderboardDef("LB_GRAND_WORK", "board.grandWork"),
        new LeaderboardDef("LB_BUILDINGS", "board.buildings"),
        new LeaderboardDef("LB_CITIES_ABSORBED", "board.cities"),
        new LeaderboardDef("LB_TILES_EXPLORED", "board.explored"),
        new LeaderboardDef("LB_FASTEST_ASCENSION", "board.fastestAscension", Ascending: true, IsTime: true),
    };

    /// <summary>
    /// Přepíše aktuální stav světa do statistik platformy.
    ///
    /// <para>Volá se na nízké frekvenci (ne každý tik) — je to zápis ven ze hry,
    /// ne součást simulace.</para>
    /// </summary>
    public static void PushStats(IPlatformServices platform, Simulation simulation)
    {
        platform.SetStat(StatPeakPopulation, Math.Max(simulation.PeakPopulation, (long)simulation.Population));
        platform.SetStat(StatTotalBuildings, simulation.Buildings.Length);
        platform.SetStat(StatAscensions, simulation.AscensionLevel);
        platform.SetStat(StatLegacies, simulation.LegacyDepth);
        platform.SetStat(StatGrandWorkStage, simulation.GrandWorkStage);
        platform.SetStat(StatTotalPower, simulation.TotalPower());
        platform.SetStat(StatTechsResearched, simulation.ResearchedTechCount);
        platform.SetStat(StatCitiesAbsorbed, simulation.CitiesJoined);
        platform.SetStat(StatTilesExplored, simulation.Fog.ExploredChunks);
    }

    /// <summary>
    /// Pošle do žebříčků, co je právě dosažené.
    ///
    /// <para><see cref="IPlatformServices.LeaderboardsAllowed"/> se kontroluje
    /// tady, a ne u každého volajícího: zapomenout na tu podmínku by znamenalo
    /// čísla z modů ve sdíleném žebříčku, což se pak nedá vzít zpět.</para>
    /// </summary>
    public static void PushScores(IPlatformServices platform, Simulation simulation)
    {
        if (!platform.LeaderboardsAllowed)
        {
            return;
        }

        platform.SubmitScore("LB_TOTAL_POWER", (long)Math.Min(simulation.TotalPower(), long.MaxValue / 2));
        platform.SubmitScore("LB_PEAK_POPULATION", Math.Max(simulation.PeakPopulation, (long)simulation.Population));
        platform.SubmitScore("LB_ASCENSIONS", simulation.AscensionLevel);
        platform.SubmitScore("LB_LEGACIES", simulation.LegacyDepth);
        platform.SubmitScore("LB_GRAND_WORK", simulation.GrandWorkStage);
        platform.SubmitScore("LB_BUILDINGS", simulation.Buildings.Length);
        platform.SubmitScore("LB_CITIES_ABSORBED", simulation.CitiesJoined);
        platform.SubmitScore("LB_TILES_EXPLORED", simulation.Fog.ExploredChunks);
    }
}
