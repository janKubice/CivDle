using System.Text.Json;

namespace CivDle.Core.Content.Mods;

/// <summary>
/// Jeden mod: složka s <c>mod.json</c> a datovými soubory, které přebíjejí
/// nebo doplňují základní hru.
/// </summary>
/// <param name="Id">Stabilní ID (podle něj se mod pozná v savu i v seznamu).</param>
/// <param name="Name">Jméno pro hráče.</param>
/// <param name="Version">Verze, jak ji uvádí autor.</param>
/// <param name="Directory">Složka s datovými soubory modu.</param>
public sealed record ModPackage(string Id, string Name, string Version, string Directory);

/// <summary>
/// Najde mody ve složce <c>mods/</c> vedle hry.
///
/// <para>Proč to ve hře je: obsah je celý v JSON, ale bez tohohle kroku by ho
/// musel modder přepisovat přímo v <c>data/</c> — což znamená, že mu každá
/// aktualizace hry změny přemaže a dva mody se nikdy nedají použít naráz.
/// Vlastní složka obojí řeší.</para>
///
/// <para>Řadí se podle jména složky, aby bylo pořadí uplatnění modů
/// deterministické: kdo je později v abecedě, přebíjí.</para>
/// </summary>
public static class ModCatalog
{
    private const string ManifestName = "mod.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Popis modu tak, jak leží v <c>mod.json</c>.</summary>
    private sealed record ManifestDto(string? Id, string? Name, string? Version, bool? Enabled);

    /// <summary>
    /// Projde složku a vrátí zapnuté mody. Chybějící složka není chyba —
    /// naprostá většina hráčů žádný mod nemá.
    /// </summary>
    public static IReadOnlyList<ModPackage> Discover(string modsDirectory)
    {
        if (!Directory.Exists(modsDirectory))
        {
            return Array.Empty<ModPackage>();
        }

        var mods = new List<ModPackage>();
        foreach (string directory in Directory.GetDirectories(modsDirectory).OrderBy(d => d, StringComparer.Ordinal))
        {
            string manifestPath = Path.Combine(directory, ManifestName);
            if (!File.Exists(manifestPath))
            {
                continue; // složka bez manifestu není mod (třeba zapomenutý zip)
            }

            ManifestDto? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ManifestDto>(File.ReadAllText(manifestPath), Options);
            }
            catch (JsonException ex)
            {
                throw new ContentLoadException(manifestPath, $"Neplatný JSON v popisu modu: {ex.Message}");
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                throw new ContentLoadException(manifestPath, "Mod nemá vyplněné 'id'.");
            }

            if (manifest.Enabled == false)
            {
                continue; // hráč si ho vypnul, ale nechal ve složce
            }

            string id = manifest.Id.Trim();
            if (mods.Any(m => m.Id == id))
            {
                throw new ContentLoadException(manifestPath, $"Mod s ID '{id}' už je načtený z jiné složky.");
            }

            mods.Add(new ModPackage(
                id,
                string.IsNullOrWhiteSpace(manifest.Name) ? id : manifest.Name.Trim(),
                string.IsNullOrWhiteSpace(manifest.Version) ? "1.0" : manifest.Version.Trim(),
                directory));
        }

        return mods;
    }
}
