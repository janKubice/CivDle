using System.Text.Json;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content.Mods;

/// <summary>V jakém stavu mod je.</summary>
public enum ModStatus
{
    /// <summary>V pořádku a zapnutý.</summary>
    Enabled,

    /// <summary>V pořádku, ale hráč si ho vypnul.</summary>
    Disabled,

    /// <summary>Vadný — nejde načíst. <see cref="ModInspection.Problem"/> říká proč.</summary>
    Broken,
}

/// <summary>
/// Co víme o jednom modu ve složce, včetně těch, které se načíst nedají.
/// </summary>
/// <param name="Id">ID z manifestu, nebo jméno složky, když manifest chybí.</param>
/// <param name="Name">Jméno pro hráče.</param>
/// <param name="Version">Verze podle autora.</param>
/// <param name="Directory">Složka na disku.</param>
/// <param name="Status">Stav.</param>
/// <param name="Problem">Popis závady, když je <see cref="ModStatus.Broken"/>.</param>
/// <param name="DataFiles">Které datové soubory mod přebíjí (pro přehled hráči).</param>
/// <param name="FromWorkshop">Přišel ze Steam Workshopu (pak se nedá mazat ze hry)?</param>
public sealed record ModInspection(
    string Id,
    string Name,
    string Version,
    string Directory,
    ModStatus Status,
    string Problem,
    IReadOnlyList<string> DataFiles,
    bool FromWorkshop);

/// <summary>
/// Prohlídka složky s mody pro <b>správce modů</b>.
///
/// <para>Proč vedle <see cref="ModCatalog"/>: ten je pro start hry a schválně
/// je přísný — vadný mod shodí načítání s jasnou hláškou, protože hrát
/// s napůl načteným obsahem je horší než nespustit se. Správce ale potřebuje
/// pravý opak: <b>ukázat i to, co je rozbité</b>, a říct proč. Kdyby používal
/// tutéž cestu, hráč by se o vadném modu dozvěděl jen tak, že mu hra
/// nenaběhne — a neměl by kde ho vypnout.</para>
///
/// <para>Vrstva: jádro (čte a zapisuje soubory), nezná render.</para>
/// </summary>
public static class ModInspector
{
    private const string ManifestName = "mod.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private sealed record ManifestDto(string? Id, string? Name, string? Version, bool? Enabled);

    /// <summary>
    /// Projde složky a popíše každý mod, který v nich je. Nikdy nevyhodí
    /// výjimku — vadný mod je řádek se závadou, ne pád správce.
    /// </summary>
    public static IReadOnlyList<ModInspection> Inspect(
        string localDirectory, IReadOnlyList<string>? workshopDirectories = null)
    {
        var found = new List<ModInspection>();
        Scan(localDirectory, fromWorkshop: false, found);

        foreach (string directory in workshopDirectories ?? Array.Empty<string>())
        {
            Scan(directory, fromWorkshop: true, found);
        }

        // Pořadí načítání je abecední podle složky — ukazuj ho stejně, ať hráč
        // vidí, kdo koho přebíjí.
        return found.OrderBy(m => m.Directory, StringComparer.Ordinal).ToList();
    }

    private static void Scan(string root, bool fromWorkshop, List<ModInspection> into)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }

        foreach (string directory in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            into.Add(InspectOne(directory, fromWorkshop));
        }
    }

    private static ModInspection InspectOne(string directory, bool fromWorkshop)
    {
        string fallbackName = Path.GetFileName(directory);
        string manifestPath = Path.Combine(directory, ManifestName);

        if (!File.Exists(manifestPath))
        {
            return Broken(directory, fallbackName, "Chybí mod.json.", fromWorkshop);
        }

        ManifestDto? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath), Options);
        }
        catch (JsonException ex)
        {
            return Broken(directory, fallbackName, $"Neplatný JSON: {ex.Message}", fromWorkshop);
        }
        catch (IOException ex)
        {
            return Broken(directory, fallbackName, $"Soubor nejde přečíst: {ex.Message}", fromWorkshop);
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
        {
            return Broken(directory, fallbackName, "Mod nemá vyplněné 'id'.", fromWorkshop);
        }

        var dataFiles = Directory
            .GetFiles(directory, "*.json")
            .Select(Path.GetFileName)
            .Where(f => f is not null && !string.Equals(f, ManifestName, StringComparison.OrdinalIgnoreCase))
            .Select(f => f!)
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        // Mod, který nic nepřebíjí, je skoro jistě omyl (rozbalený do špatné
        // podsložky) — hráči to řekni dřív, než se bude divit, že nic nedělá.
        string problem = dataFiles.Count == 0 ? "Mod neobsahuje žádný datový soubor." : string.Empty;

        return new ModInspection(
            manifest.Id.Trim(),
            string.IsNullOrWhiteSpace(manifest.Name) ? manifest.Id.Trim() : manifest.Name.Trim(),
            string.IsNullOrWhiteSpace(manifest.Version) ? "1.0" : manifest.Version.Trim(),
            directory,
            manifest.Enabled == false ? ModStatus.Disabled : ModStatus.Enabled,
            problem,
            dataFiles,
            fromWorkshop);
    }

    private static ModInspection Broken(string directory, string name, string problem, bool fromWorkshop) =>
        new(name, name, "?", directory, ModStatus.Broken, problem, Array.Empty<string>(), fromWorkshop);

    /// <summary>
    /// Zapne nebo vypne mod přepsáním <c>enabled</c> v jeho manifestu.
    ///
    /// <para>Ostatní pole se zachovají: manifest patří autorovi modu a přepsat
    /// mu ho na tvar, který zná hra, by mu zahodilo cokoli vlastního.</para>
    /// </summary>
    /// <returns><c>true</c>, když se zápis povedl.</returns>
    public static bool SetEnabled(ModInspection mod, bool enabled)
    {
        string manifestPath = Path.Combine(mod.Directory, ManifestName);
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject;
            if (node is null)
            {
                return false;
            }

            node["enabled"] = enabled;
            File.WriteAllText(manifestPath, node.ToJsonString(Options));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
