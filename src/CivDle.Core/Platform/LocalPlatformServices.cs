using System.Text.Json;

namespace CivDle.Core.Platform;

/// <summary>
/// Platforma bez platformy: achievementy, statistiky i žebříčky vedené
/// v jednom souboru vedle savu.
///
/// <para><b>Není to atrapa.</b> Je to plnohodnotná implementace pro hráče, kteří
/// hru nespustili přes Steam — a při vývoji pro všechny. Achievementy se
/// odemykají doopravdy, statistiky se počítají doopravdy a žebříček ukazuje
/// vlastní rekordy. Až přijde Steam, přidá se druhá implementace vedle téhle,
/// nenahradí ji: hráč mimo Steam nesmí přijít o postup.</para>
///
/// <para>Žebříčky jsou tu nutně jen osobní — bez serveru není s kým se
/// porovnávat. Osobní rekord je ale to, na co se v idle hře stejně kouká
/// nejčastěji („překonal jsem se?").</para>
///
/// <para>Vrstva: jádro. Zapisuje na disk, ale nezná render ani UI.</para>
/// </summary>
public sealed class LocalPlatformServices : IPlatformServices
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private readonly HashSet<string> _achievements = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _stats = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _bests = new(StringComparer.Ordinal);
    private readonly List<string> _modDirectories = new();

    /// <summary>Tvar souboru na disku. Vlastní typ, ať se serializace nedělá ručně.</summary>
    private sealed record State(
        List<string>? Achievements,
        Dictionary<string, double>? Stats,
        Dictionary<string, long>? Bests);

    public LocalPlatformServices(string filePath)
    {
        _path = filePath;
        Load();
    }

    /// <summary>Lokální platforma je vždycky k dispozici — proto je výchozí.</summary>
    public bool IsAvailable => true;

    public string PlayerName { get; set; } = "Hráč";

    /// <summary>
    /// Bez serveru nemá smysl žebříčky zamykat: jsou osobní a nikoho nepoškodí,
    /// když si je hráč modem „pokazí". Zamykání dává smysl až u sdílených.
    /// </summary>
    public bool LeaderboardsAllowed => true;

    public void UnlockAchievement(string apiName) => _achievements.Add(apiName);

    public bool IsAchievementUnlocked(string apiName) => _achievements.Contains(apiName);

    public void SetStat(string apiName, long value) => _stats[apiName] = value;

    public void SetStat(string apiName, double value) => _stats[apiName] = value;

    public double GetStat(string apiName) => _stats.GetValueOrDefault(apiName);

    /// <summary>Osobní rekord se přepíše jen lepším skóre.</summary>
    public void SubmitScore(string leaderboardId, long score)
    {
        bool ascending = IsAscending(leaderboardId);
        if (!_bests.TryGetValue(leaderboardId, out long current))
        {
            _bests[leaderboardId] = score;
            return;
        }

        bool better = ascending ? score < current : score > current;
        if (better)
        {
            _bests[leaderboardId] = score;
        }
    }

    public IReadOnlyList<LeaderboardEntry> TopScores(string leaderboardId, int count)
    {
        _ = count;
        return _bests.TryGetValue(leaderboardId, out long best)
            ? new[] { new LeaderboardEntry(1, PlayerName, best, true) }
            : Array.Empty<LeaderboardEntry>();
    }

    public long? PersonalBest(string leaderboardId) =>
        _bests.TryGetValue(leaderboardId, out long best) ? best : null;

    /// <summary>Bez Workshopu jsou k dispozici jen mody, které si hráč zkopíroval sám.</summary>
    public IReadOnlyList<WorkshopItem> WorkshopItems() => Array.Empty<WorkshopItem>();

    public IReadOnlyList<string> SubscribedModDirectories() => _modDirectories;

    /// <summary>
    /// Žebříčky, kde je nižší lepší. Prefix je konvence z
    /// <c>docs/steam/generated/leaderboards.csv</c> — jediné dva „rychlostní"
    /// žebříčky, zbytek je „čím víc, tím líp".
    /// </summary>
    private static bool IsAscending(string leaderboardId) =>
        leaderboardId.Contains("FASTEST", StringComparison.Ordinal);

    public void Flush()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new State(_achievements.ToList(), _stats, _bests);
            File.WriteAllText(_path, JsonSerializer.Serialize(state, Options));
        }
        catch (IOException)
        {
            // Neuložený postup je mrzuté, spadlá hra horší. Achievementy nejsou
            // stav světa — hráč o partii nepřijde.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            return;
        }

        try
        {
            var state = JsonSerializer.Deserialize<State>(File.ReadAllText(_path), Options);
            if (state is null)
            {
                return;
            }

            foreach (string id in state.Achievements ?? new List<string>())
            {
                _achievements.Add(id);
            }

            foreach (var (key, value) in state.Stats ?? new Dictionary<string, double>())
            {
                _stats[key] = value;
            }

            foreach (var (key, value) in state.Bests ?? new Dictionary<string, long>())
            {
                _bests[key] = value;
            }
        }
        catch (JsonException)
        {
            // Poškozený soubor se přejde: hra musí naběhnout i tak a příští
            // uložení ho přepíše. Za achievementy nestojí odepřít partii.
        }
        catch (IOException)
        {
        }
    }
}
