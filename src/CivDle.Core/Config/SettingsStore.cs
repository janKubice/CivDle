using System.Text.Json;
using System.Text.Json.Serialization;

namespace CivDle.Core.Config;

/// <summary>
/// Načítání a ukládání uživatelských nastavení do JSON souboru.
/// Na rozdíl od herního obsahu tady záměrně NENÍ fail-fast: rozbitý či chybějící
/// soubor nastavení nesmí hráči shodit hru — použijí se výchozí hodnoty.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    /// <param name="filePath">Plná cesta k <c>settings.json</c> (typicky v profilu uživatele).</param>
    public SettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Načte nastavení; chybějící nebo nečitelný soubor = výchozí hodnoty.</summary>
    public GameSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new GameSettings();
            }

            var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(_filePath), JsonOptions);
            return Sanitize(settings ?? new GameSettings());
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new GameSettings();
        }
    }

    /// <summary>Uloží nastavení (založí i složku). Selhání zápisu hru neshazuje.</summary>
    public void Save(GameSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Hra poběží dál s nastavením v paměti; příště se zápis zkusí znovu.
        }
    }

    /// <summary>Zdravé meze proti ručně rozbitému souboru (nulové rozlišení apod.).</summary>
    private static GameSettings Sanitize(GameSettings settings)
    {
        var defaults = new GameSettings();
        if (settings.ResolutionWidth < 640 || settings.ResolutionWidth > 7680
            || settings.ResolutionHeight < 480 || settings.ResolutionHeight > 4320)
        {
            settings = settings with
            {
                ResolutionWidth = defaults.ResolutionWidth,
                ResolutionHeight = defaults.ResolutionHeight,
            };
        }

        if (string.IsNullOrWhiteSpace(settings.Language))
        {
            settings = settings with { Language = defaults.Language };
        }

        if (settings.MasterVolume is < 0 or > 1 || float.IsNaN(settings.MasterVolume))
        {
            settings = settings with { MasterVolume = Math.Clamp(settings.MasterVolume, 0f, 1f) };
            if (float.IsNaN(settings.MasterVolume))
            {
                settings = settings with { MasterVolume = defaults.MasterVolume };
            }
        }

        // Neznámý stupeň detailu (starší nebo ručně upravený soubor) by jinak
        // prošel jako číslo mimo výčet a render by z něj počítal nesmysly.
        if (!Enum.IsDefined(settings.Detail))
        {
            settings = settings with { Detail = defaults.Detail };
        }

        return settings;
    }
}
