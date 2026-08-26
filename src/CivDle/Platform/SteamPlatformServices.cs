using CivDle.Core.Platform;
using Steamworks;

namespace CivDle.Platform;

/// <summary>
/// Steam jako herní platforma — achievementy, statistiky, jméno hráče
/// a mody z Workshopu.
///
/// <para><b>Obálka, ne náhrada.</b> Všechno se zapisuje i do lokální
/// implementace: hráč o postup nepřijde, když si hru jednou spustí bez Steamu,
/// a čtení jde vždycky z lokálních dat, která jsou po ruce okamžitě. Steam se
/// ptá asynchronně a čekat na odpověď uprostřed snímku nejde.</para>
///
/// <para><b>Když Steam neběží, hra běží dál.</b> <see cref="TryCreate"/> vrátí
/// <c>null</c> a volající zůstane u lokální implementace. Chybí-li rovnou
/// nativní knihovna (hra spuštěná napřímo, jiná platforma), skončí to
/// <see cref="DllNotFoundException"/> — a i to je jen „Steam tu není".</para>
///
/// <para><b>Co tudy zatím nejde:</b> sdílené žebříčky. Steam je umí, ale jen
/// asynchronně přes callbacky a odladit se to dá pouze proti živému klientu
/// s vydaným App ID. Proto <see cref="HasOnlineLeaderboards"/> zůstává
/// <c>false</c> a žebříčky jedou lokálně — radši ať UI netvrdí připojení,
/// které nemá.</para>
///
/// <para>Vrstva: aplikace. Jádro zná jen <see cref="IPlatformServices"/>,
/// takže na Steamworks nezávisí a dá se testovat bez něj.</para>
/// </summary>
public sealed class SteamPlatformServices : IPlatformServices, IDisposable
{
    private readonly IPlatformServices _local;
    private bool _disposed;

    private SteamPlatformServices(IPlatformServices local) => _local = local;

    /// <summary>
    /// Zkusí nastartovat Steam. Vrací <c>null</c>, když neběží, hra není přes
    /// něj spuštěná, nebo tu nativní knihovna vůbec není.
    /// </summary>
    /// <param name="local">Lokální implementace, do které se zapisuje souběžně.</param>
    public static SteamPlatformServices? TryCreate(IPlatformServices local)
    {
        try
        {
            if (!SteamAPI.Init())
            {
                return null;
            }
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException
                                          or EntryPointNotFoundException or InvalidOperationException)
        {
            // Nativní knihovna chybí nebo je pro jinou architekturu. To není
            // chyba hry — jen tu Steam není.
            Console.WriteLine($"Steam není k dispozici: {error.Message}");
            return null;
        }

        Console.WriteLine("Steam: připojeno");
        return new SteamPlatformServices(local);
    }

    /// <summary>
    /// Nechá Steam zpracovat, co mu přišlo. Volá se jednou za snímek.
    ///
    /// <para>Bez tohohle se nikdy nedoručí žádná odpověď a Steam se po chvíli
    /// tváří, že hra zamrzla.</para>
    /// </summary>
    public void Pump()
    {
        if (!_disposed)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public bool IsAvailable => !_disposed;

    /// <summary>Sdílené žebříčky zatím ne — viz poznámka u třídy.</summary>
    public bool HasOnlineLeaderboards => false;

    public bool LeaderboardsAllowed => _local.LeaderboardsAllowed;

    /// <summary>Jméno ze Steamu; když se nepovede, zůstane to lokální.</summary>
    public string PlayerName
    {
        get
        {
            try
            {
                string name = SteamFriends.GetPersonaName();
                return string.IsNullOrWhiteSpace(name) ? _local.PlayerName : name;
            }
            catch (InvalidOperationException)
            {
                return _local.PlayerName;
            }
        }
    }

    public void UnlockAchievement(string apiName)
    {
        _local.UnlockAchievement(apiName);
        Guarded(() =>
        {
            SteamUserStats.SetAchievement(apiName);
            SteamUserStats.StoreStats();
        });
    }

    /// <summary>Čte se z lokálních dat — jsou po ruce hned a Steam je jen kopie navíc.</summary>
    public bool IsAchievementUnlocked(string apiName) => _local.IsAchievementUnlocked(apiName);

    public void SetStat(string apiName, long value)
    {
        _local.SetStat(apiName, value);

        // Steam bere jen 32bitová celá čísla. Populace v pozdní hře je přeteče,
        // takže se ořízne na strop — statistika je tabulka na profilu, ne
        // podklad pro výpočet, a „přes dvě miliardy" je pravdivější než přetečení.
        Guarded(() => SteamUserStats.SetStat(apiName, (int)Math.Clamp(value, int.MinValue, int.MaxValue)));
    }

    public void SetStat(string apiName, double value)
    {
        _local.SetStat(apiName, value);
        Guarded(() => SteamUserStats.SetStat(apiName, (float)value));
    }

    public double GetStat(string apiName) => _local.GetStat(apiName);

    public void SubmitScore(string leaderboardId, long score) => _local.SubmitScore(leaderboardId, score);

    public IReadOnlyList<LeaderboardEntry> TopScores(string leaderboardId, int count) =>
        _local.TopScores(leaderboardId, count);

    public long? PersonalBest(string leaderboardId) => _local.PersonalBest(leaderboardId);

    public IReadOnlyList<WorkshopItem> WorkshopItems() => _local.WorkshopItems();

    /// <summary>
    /// Složky odebraných modů z Workshopu.
    ///
    /// <para>Tohle Steam umí synchronně, takže to jde rovnou — odběry se
    /// stahují na pozadí a hra se jen zeptá, kde leží.</para>
    /// </summary>
    public IReadOnlyList<string> SubscribedModDirectories()
    {
        var directories = new List<string>(_local.SubscribedModDirectories());

        Guarded(() =>
        {
            uint count = SteamUGC.GetNumSubscribedItems();
            if (count == 0)
            {
                return;
            }

            var ids = new PublishedFileId_t[count];
            uint got = SteamUGC.GetSubscribedItems(ids, count);
            for (uint i = 0; i < got; i++)
            {
                if (SteamUGC.GetItemInstallInfo(ids[i], out _, out string folder, 1024, out _)
                    && !string.IsNullOrWhiteSpace(folder))
                {
                    directories.Add(folder);
                }
            }
        });

        return directories;
    }

    public void Flush()
    {
        _local.Flush();
        Guarded(() => SteamUserStats.StoreStats());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Guarded(SteamAPI.Shutdown);
    }

    /// <summary>
    /// Zavolá Steam a spolkne jeho selhání.
    ///
    /// <para>Steam může kdykoli spadnout, být odhlášený nebo se restartovat.
    /// Hra kvůli tomu spadnout nesmí — achievement, který se nezapsal do
    /// Steamu, je pořád zapsaný lokálně.</para>
    /// </summary>
    private void Guarded(Action action)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            action();
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            Console.WriteLine($"Steam neodpověděl: {error.Message}");
        }
    }
}
