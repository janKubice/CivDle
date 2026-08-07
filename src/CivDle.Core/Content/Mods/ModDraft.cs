using System.Text.Json;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content.Mods;

/// <summary>Rozpracovaná surovina v editoru modů.</summary>
/// <param name="Id">Stabilní ID (bez mezer, malá písmena).</param>
/// <param name="Name">Jméno pro hráče — zapíše se do lang souboru modu.</param>
/// <param name="Color">Barva v HUD, hex <c>#RRGGBB</c>.</param>
/// <param name="StartAmount">Kolik jí hráč má na startu éry.</param>
/// <param name="BaseStorage">Základní kapacita skladu.</param>
public sealed record ResourceDraft(
    string Id,
    string Name,
    string Color = "#B48C46",
    int StartAmount = 0,
    int BaseStorage = 500);

/// <summary>Jedna položka receptu nebo ceny: surovina a množství.</summary>
public sealed record AmountDraft(string ResourceId, int Amount);

/// <summary>Rozpracovaná budova v editoru modů.</summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Name">Jméno pro hráče.</param>
/// <param name="Description">Popis pro hráče.</param>
/// <param name="Category">Kategorie (housing, production, civic, storage, science, power…).</param>
/// <param name="Color">Barva na mapě, když nemá sprite.</param>
/// <param name="FootprintWidth">Šířka v dlaždicích.</param>
/// <param name="FootprintHeight">Výška v dlaždicích.</param>
/// <param name="WorkerSlots">Kolik lidí v ní pracuje.</param>
/// <param name="HousingCapacity">Kolik lidí ubytuje.</param>
/// <param name="BuildCost">Co stojí postavit.</param>
/// <param name="RecipeInputs">Co spotřebuje za výrobní cyklus.</param>
/// <param name="RecipeOutputs">Co vyrobí za výrobní cyklus.</param>
/// <param name="RecipeSeconds">Jak dlouho cyklus trvá.</param>
/// <param name="SpriteId">ID spritu (soubor <c>sprites/&lt;SpriteId&gt;.png</c>), nebo prázdné.</param>
/// <param name="AutoBuild">Smí ji stavět guvernér sám?</param>
/// <param name="AllowedBiomes">Biomy, na kterých smí stát (prázdné = suchá souš).</param>
public sealed record BuildingDraft(
    string Id,
    string Name,
    string Description = "",
    string Category = "production",
    string Color = "#C88C46",
    int FootprintWidth = 1,
    int FootprintHeight = 1,
    int WorkerSlots = 1,
    int HousingCapacity = 0,
    IReadOnlyList<AmountDraft>? BuildCost = null,
    IReadOnlyList<AmountDraft>? RecipeInputs = null,
    IReadOnlyList<AmountDraft>? RecipeOutputs = null,
    double RecipeSeconds = 5.0,
    string SpriteId = "",
    bool AutoBuild = false,
    IReadOnlyList<string>? AllowedBiomes = null);

/// <summary>
/// Rozpracovaný mod: co všechno v něm hráč nadefinoval, než to uloží.
///
/// <para>Je to <b>čistý model bez UI</b>, a schválně: díky tomu jde zapisování
/// i kontrola otestovat bez spouštění hry, a editor v UI je pak jen formulář
/// nad ním. Kdyby JSON skládala obrazovka, nešlo by ověřit, že to, co uloží,
/// hra opravdu načte.</para>
///
/// <para>Vrstva: jádro. Umí se zapsat na disk, nezná render.</para>
/// </summary>
public sealed class ModDraft
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public ModDraft(string id, string name, string version = "1.0.0")
    {
        Id = id;
        Name = name;
        Version = version;
    }

    public string Id { get; set; }

    public string Name { get; set; }

    public string Version { get; set; }

    public List<ResourceDraft> Resources { get; } = new();

    public List<BuildingDraft> Buildings { get; } = new();

    /// <summary>
    /// Zapíše mod do složky. Vrací cestu ke složce modu.
    ///
    /// <para>Zapisují se <b>jen soubory, které mod opravdu mění</b>. Prázdný
    /// <c>resources.json</c> by přebil základní hru prázdným seznamem a smazal
    /// by všechny suroviny.</para>
    /// </summary>
    public string WriteTo(string modsDirectory)
    {
        string directory = Path.Combine(modsDirectory, Id);
        Directory.CreateDirectory(directory);

        File.WriteAllText(
            Path.Combine(directory, "mod.json"),
            JsonSerializer.Serialize(
                new JsonObject
                {
                    ["id"] = Id,
                    ["name"] = Name,
                    ["version"] = Version,
                    ["enabled"] = true,
                },
                Options));

        if (Resources.Count > 0)
        {
            File.WriteAllText(Path.Combine(directory, "resources.json"), ResourcesJson());
        }

        if (Buildings.Count > 0)
        {
            File.WriteAllText(Path.Combine(directory, "buildings.json"), BuildingsJson());
        }

        WriteLanguageFiles(directory);
        return directory;
    }

    /// <summary>
    /// Jména a popisy jdou do <b>všech</b> jazyků, které hra zná.
    ///
    /// <para>Hra při načtení kontroluje úplnost překladů — mod, který doplní
    /// klíč jen do češtiny, by shodil start ve všech ostatních jazycích.
    /// Autor modu obvykle přeloží jeden jazyk, takže zbytek dostane tentýž
    /// text: raději nepřeložené než nespustitelné.</para>
    /// </summary>
    private void WriteLanguageFiles(string directory)
    {
        if (Resources.Count == 0 && Buildings.Count == 0)
        {
            return;
        }

        string langDirectory = Path.Combine(directory, "lang");
        Directory.CreateDirectory(langDirectory);

        var strings = new JsonObject();
        foreach (var resource in Resources)
        {
            strings[$"resource.{resource.Id}"] = resource.Name;
        }

        foreach (var building in Buildings)
        {
            strings[$"building.{building.Id}"] = building.Name;
            strings[$"building.{building.Id}.desc"] = building.Description;
        }

        foreach (string language in KnownLanguages)
        {
            var document = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["id"] = language,
                ["strings"] = strings.DeepClone(),
            };
            File.WriteAllText(Path.Combine(langDirectory, $"{language}.json"), document.ToJsonString(Options));
        }
    }

    /// <summary>Jazyky, které hra dodává. Mod musí doplnit klíče do všech.</summary>
    public static IReadOnlyList<string> KnownLanguages { get; } = new[] { "cs", "en", "de", "pl", "es" };

    private string ResourcesJson()
    {
        var array = new JsonArray();
        foreach (var resource in Resources)
        {
            array.Add(new JsonObject
            {
                ["id"] = resource.Id,
                ["mapColor"] = resource.Color,
                ["startAmount"] = resource.StartAmount,
                ["baseStorage"] = resource.BaseStorage,
            });
        }

        return new JsonObject { ["schemaVersion"] = 1, ["resources"] = array }.ToJsonString(Options);
    }

    private string BuildingsJson()
    {
        var array = new JsonArray();
        foreach (var building in Buildings)
        {
            var node = new JsonObject
            {
                ["id"] = building.Id,
                ["category"] = building.Category,
                ["mapColor"] = building.Color,
                ["footprint"] = new JsonArray(building.FootprintWidth, building.FootprintHeight),
                ["workerSlots"] = building.WorkerSlots,
                ["housingCapacity"] = building.HousingCapacity,
                ["buildCost"] = Amounts(building.BuildCost),
                ["allowedBiomes"] = Biomes(building.AllowedBiomes),
                ["buildable"] = true,
                ["autoBuild"] = building.AutoBuild,
            };

            if (building.SpriteId.Length > 0)
            {
                node["sprite"] = building.SpriteId;
            }

            // Recept se zapíše, jen když něco vyrábí. Prázdný recept by z budovy
            // udělal výrobnu, která nic nedělá a jen bere lidi.
            if (building.RecipeOutputs is { Count: > 0 })
            {
                node["recipe"] = new JsonObject
                {
                    ["input"] = Amounts(building.RecipeInputs),
                    ["output"] = Amounts(building.RecipeOutputs),

                    // Data počítají v ticích, editor v sekundách — hráč přemýšlí
                    // v sekundách a o tikovou frekvenci se starat nemá.
                    ["timeTicks"] = (int)Math.Max(1, Math.Round(building.RecipeSeconds * TicksPerSecond)),
                };
            }

            array.Add(node);
        }

        return new JsonObject { ["schemaVersion"] = 1, ["buildings"] = array }.ToJsonString(Options);
    }

    /// <summary>Tiků za sekundu, jak je má simulace. Editor mluví v sekundách.</summary>
    private const double TicksPerSecond = 10.0;

    private static JsonArray Biomes(IReadOnlyList<string>? biomes)
    {
        var node = new JsonArray();
        foreach (string biome in biomes ?? DefaultBiomes)
        {
            node.Add(biome);
        }

        return node;
    }

    /// <summary>
    /// Biomy, na kterých se smí stavět, když si autor nevybere. Suchá souš —
    /// prázdný seznam by znamenal budovu, kterou nejde postavit nikde, a to je
    /// nejhorší možná výchozí hodnota.
    /// </summary>
    public static IReadOnlyList<string> DefaultBiomes { get; } = new[]
    {
        "grassland", "steppe", "savanna", "tundra", "desert", "beach",
    };

    private static JsonObject Amounts(IReadOnlyList<AmountDraft>? amounts)
    {
        var node = new JsonObject();
        foreach (var amount in amounts ?? Array.Empty<AmountDraft>())
        {
            node[amount.ResourceId] = amount.Amount;
        }

        return node;
    }
}
