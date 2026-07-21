using System.Text.Json;

namespace CivDle.Core.Config;

/// <summary>
/// Načítání a ukládání účet-wide profilu (odemčené achievementy) do JSON.
/// Jako u nastavení tu ZÁMĚRNĚ není fail-fast: rozbitý či chybějící soubor nesmí
/// shodit hru — použije se prázdný profil.
/// </summary>
public sealed class ProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _filePath;

    /// <param name="filePath">Plná cesta k <c>profile.json</c> (typicky v profilu uživatele).</param>
    public ProfileStore(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>Načte profil; chybějící nebo nečitelný soubor = prázdný profil.</summary>
    public PlayerProfile Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new PlayerProfile();
            }

            return JsonSerializer.Deserialize<PlayerProfile>(File.ReadAllText(_filePath), JsonOptions) ?? new PlayerProfile();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new PlayerProfile();
        }
    }

    /// <summary>Uloží profil (založí i složku). Selhání zápisu hru neshazuje.</summary>
    public void Save(PlayerProfile profile)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(profile, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Hra poběží dál; příště se zápis zkusí znovu.
        }
    }
}
