using System.Text.Json;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content;

/// <summary>
/// Který obsah patří kterému světu (svety-design.md 7.1).
///
/// <para><b>Jak to funguje.</b> Každý svět si načte vlastní <see cref="GameContent"/>
/// ze stejné složky <c>data/</c>: nejdřív se ze základních souborů vyberou položky,
/// které mu patří, a pak se přes ně přeloží složka světa
/// (<c>data/worlds/&lt;id&gt;/</c>) stejným mechanismem jako mody. Simulace, UI
/// i render pak vidí jen obsah svého světa — nikde se nic nefiltruje za běhu.</para>
///
/// <para><b>Značka <c>worlds</c>.</b> Položka v základním souboru může nést
/// seznam světů, kam patří; <c>"*"</c> = všude. Bez značky platí výchozí
/// rozsah souboru: <b>obsah</b> (budovy, suroviny, výzkum, úkoly…) patří jen
/// Domovině, <b>struktura</b> (biomy, velikosti map, éry, politiky…) všem.
/// Proč obsah jen Domovině: kolonie nesmí omylem dostat Hvězdnou bránu nebo
/// elektrárnu na uran, který tam není — sdílí se jen to, co se výslovně označí,
/// a chybný odkaz spadne při startu (fail-fast), ne za hodinu hraní.</para>
///
/// <para><b>Náhrady surovin.</b> Sdílená budova (sklad, škola) stojí na Domovině
/// dřevo a kámen; na Duně žádné dřevo není. Svět proto může říct „dřevo = cihla"
/// a sdíleným položkám se klíče surovin přejmenují (<c>substitutes</c>
/// ve <c>world.json</c>).</para>
///
/// <para>Čistá funkce nad textem — žádné soubory, žádný stav, testovatelná.</para>
/// </summary>
public static class WorldScope
{
    /// <summary>ID Domoviny (hlavního města první kapitoly).</summary>
    public const string HomeId = "home";

    /// <summary>Značka „patří všem světům".</summary>
    public const string Everywhere = "*";

    /// <summary>Název pole se seznamem světů na položce.</summary>
    public const string TagKey = "worlds";

    /// <summary>
    /// Jak se filtruje jeden soubor.
    /// </summary>
    /// <param name="Arrays">Pole položek s <c>id</c>; tečka jde do hloubky (<c>planting.species</c>).</param>
    /// <param name="HomeOnly">Výchozí rozsah položky bez značky: <c>true</c> = jen Domovina, <c>false</c> = všude.</param>
    /// <param name="SubstituteWholeFile">
    /// Náhrady surovin platí pro celý soubor, ne jen pro vybrané položky —
    /// nastavení (<c>gameplay.json</c>) nemá položky, ale denní odměna v něm
    /// za dřevo na Duně dávat nesmí.
    /// </param>
    private sealed record FileScope(string[] Arrays, bool HomeOnly, bool SubstituteWholeFile = false)
    {
        public static implicit operator FileScope((string[] Arrays, bool HomeOnly) pair) => new(pair.Arrays, pair.HomeOnly);
    }

    /// <summary>
    /// Filtrované soubory. Soubory mimo tabulku (jazyky, sítě…) se nefiltrují.
    /// </summary>
    private static readonly Dictionary<string, FileScope> Scopes = new(StringComparer.Ordinal)
    {
        // Obsah: bez značky jen Domovina.
        ["resources.json"] = (new[] { "resources" }, true),
        ["buildings.json"] = (new[] { "buildings" }, true),
        ["tech.json"] = (new[] { "techs" }, true),
        ["quests.json"] = (new[] { "quests" }, true),
        ["achievements.json"] = (new[] { "achievements" }, true),
        ["events.json"] = (new[] { "events" }, true),
        ["landmarks.json"] = (new[] { "landmarks" }, true),
        ["features.json"] = (new[] { "features" }, true),
        ["terraform.json"] = (new[] { "terraform" }, true),
        ["milestones.json"] = (new[] { "milestones" }, true),
        ["contracts.json"] = (new[] { "contracts" }, true),
        ["citizens.json"] = (new[] { "requests" }, true),
        ["scenarios.json"] = (new[] { "scenarios" }, true),
        ["challenges.json"] = (new[] { "challenges" }, true),
        ["orbit.json"] = (new[] { "satellites" }, true),
        ["figures.json"] = (new[] { "figures" }, true),
        ["poi.json"] = (new[] { "relics", "kinds" }, true),
        ["doctrines.json"] = (new[] { "doctrines" }, true),
        ["tutorial.json"] = (new[] { "steps" }, true),
        ["weather.json"] = (new[] { "weather" }, true),
        ["fauna.json"] = (new[] { "fauna" }, true),
        ["ambience.json"] = (new[] { "ambience" }, true),
        ["faith.json"] = (new[] { "prayers" }, true),
        ["frontier.json"] = (new[] { "attackers" }, true),
        ["npc-cities.json"] = (new[] { "archetypes" }, true),
        ["vehicles.json"] = (new[] { "aircraft" }, true),
        ["zones.json"] = (new[] { "zones" }, true),
        ["ascension-tiers.json"] = (new[] { "tiers" }, true),

        // Struktura: bez značky všude.
        ["prestige.json"] = (new[] { "upgrades" }, false),
        ["legacy.json"] = (new[] { "upgrades" }, false),
        ["biomes.json"] = (new[] { "biomes" }, false),
        ["decorations.json"] = (new[] { "decorations" }, false),
        ["districts.json"] = (new[] { "districts", "styles" }, false),
        ["policies.json"] = (new[] { "policies" }, false),
        ["worldgen.json"] = (new[] { "sizes", "presets" }, false),
        ["eras.json"] = (new[] { "eras" }, false),
        ["elections.json"] = (new[] { "candidates" }, false),
        ["seasons.json"] = (new[] { "seasons" }, false),
        ["settlement-ranks.json"] = (new[] { "ranks" }, false),
        ["chronicle.json"] = (new[] { "lines" }, false),

        // Nastavení: sázení nabízí druhy Domoviny (háj dává dřevo) — kolonie
        // si nabídne vlastní. Zbytek souboru zůstane, jen se přejmenují suroviny.
        ["gameplay.json"] = new FileScope(new[] { "planting.species" }, true, SubstituteWholeFile: true),
    };

    /// <summary>
    /// Pole, ve kterých smí sdílená položka tiše ztratit surovinu, kterou svět
    /// nemá: kapacita skladu pro obilí na Duně nic neznamená a nic nerozbije.
    /// Cena ani recept takhle chránit nejde — ty musí spadnout (fail-fast).
    /// </summary>
    private static readonly HashSet<string> PrunableFields = new(StringComparer.OrdinalIgnoreCase) { "storage" };

    /// <summary>
    /// Pole, jejichž <b>textová hodnota</b> je ID suroviny — ta se při náhradě
    /// přepisují také (klíče slovníků cen a receptů se přepisují vždy).
    /// </summary>
    private static readonly HashSet<string> ResourceValueFields = new(StringComparer.Ordinal)
    {
        "resource", "targetResource", "foodResource",
    };

    /// <summary>Filtruje se tenhle soubor podle světů?</summary>
    public static bool IsScoped(string fileName) => Scopes.ContainsKey(fileName);

    /// <summary>
    /// Vybere ze základního souboru položky patřící světu <paramref name="worldId"/>,
    /// odstraní z nich značku a (mimo Domovinu) přejmenuje nahrazené suroviny.
    /// Soubor mimo tabulku vrátí beze změny.
    /// </summary>
    /// <param name="fileName">Jméno souboru (<c>buildings.json</c>).</param>
    /// <param name="json">Text základního souboru.</param>
    /// <param name="worldId">Pro který svět se načítá.</param>
    /// <param name="substitutes">Náhrady surovin světa (ID → ID); smí být prázdné.</param>
    /// <param name="knownResources">
    /// Suroviny světa; sdíleným položkám se podle nich odříznou kapacity skladů
    /// pro suroviny, které svět nemá (<see cref="PrunableFields"/>). <c>null</c>
    /// = nic neořezávat (suroviny samotné, Domovina).
    /// </param>
    public static string Filter(
        string fileName, string json, string worldId, IReadOnlyDictionary<string, string>? substitutes = null,
        IReadOnlySet<string>? knownResources = null)
    {
        if (!Scopes.TryGetValue(fileName, out var scope))
        {
            return json;
        }

        bool home = worldId == HomeId;
        bool substitute = !home && substitutes is { Count: > 0 };
        if (home && !json.Contains($"\"{TagKey}\"", StringComparison.Ordinal))
        {
            return json; // Domovina bez jediné značky = soubor přesně jako dřív (rychlá cesta)
        }

        if (JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) is not JsonObject root)
        {
            return json;
        }

        foreach (string arrayName in scope.Arrays)
        {
            if (FindPath(root, arrayName) is not JsonArray items)
            {
                continue;
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] is not JsonObject item)
                {
                    continue;
                }

                if (!BelongsTo(item, worldId, scope.HomeOnly))
                {
                    items.RemoveAt(i);
                    continue;
                }

                RemoveTag(item);
                if (substitute && !scope.SubstituteWholeFile)
                {
                    Substitute(item, substitutes!);
                }

                if (!home && knownResources is not null)
                {
                    Prune(item, knownResources);
                }
            }
        }

        if (substitute && scope.SubstituteWholeFile)
        {
            Substitute(root, substitutes!);
        }

        return root.ToJsonString();
    }

    /// <summary>Odřízne v kapacitách skladů suroviny, které svět nemá (do hloubky).</summary>
    private static void Prune(JsonObject node, IReadOnlySet<string> knownResources)
    {
        foreach (var pair in node.ToList())
        {
            if (pair.Value is JsonObject child)
            {
                if (PrunableFields.Contains(pair.Key))
                {
                    foreach (string key in child.Select(entry => entry.Key).ToList())
                    {
                        if (!knownResources.Contains(key))
                        {
                            child.Remove(key);
                        }
                    }
                }
                else
                {
                    Prune(child, knownResources);
                }
            }
        }
    }

    /// <summary>
    /// Patří položka světu? Značka rozhoduje, bez ní výchozí rozsah souboru.
    /// </summary>
    public static bool BelongsTo(JsonObject item, string worldId, bool homeOnlyByDefault)
    {
        if (FindProperty(item, TagKey) is not JsonArray tags)
        {
            return !homeOnlyByDefault || worldId == HomeId;
        }

        foreach (var tag in tags)
        {
            if (tag is JsonValue value && value.TryGetValue(out string? id)
                && (id == Everywhere || id == worldId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Přejmenuje suroviny v položce: klíče slovníků (ceny, recepty, sklady)
    /// i hodnoty polí, která na surovinu odkazují. Když se dvě suroviny slijí do
    /// jedné (dřevo i kámen → cihla), částky se sečtou.
    /// </summary>
    private static void Substitute(JsonObject node, IReadOnlyDictionary<string, string> substitutes)
    {
        var keys = node.Select(pair => pair.Key).ToList();
        foreach (string key in keys)
        {
            var value = node[key];
            if (value is JsonObject child)
            {
                Substitute(child, substitutes);
            }
            else if (value is JsonArray array)
            {
                foreach (var element in array)
                {
                    if (element is JsonObject elementObject)
                    {
                        Substitute(elementObject, substitutes);
                    }
                }
            }
            else if (value is JsonValue text && ResourceValueFields.Contains(key)
                && text.TryGetValue(out string? id) && substitutes.TryGetValue(id, out string? replacement))
            {
                node[key] = replacement;
            }

            if (!substitutes.TryGetValue(key, out string? renamed) || renamed == key)
            {
                continue;
            }

            var moved = node[key];
            node.Remove(key);
            if (node[renamed] is JsonValue existing && moved is JsonValue incoming
                && existing.TryGetValue(out double a) && incoming.TryGetValue(out double b))
            {
                node[renamed] = a + b == Math.Floor(a + b) ? JsonValue.Create((long)(a + b)) : JsonValue.Create(a + b);
            }
            else
            {
                node[renamed] = moved;
            }
        }
    }

    private static void RemoveTag(JsonObject item)
    {
        string? key = item.Select(pair => pair.Key)
            .FirstOrDefault(k => string.Equals(k, TagKey, StringComparison.OrdinalIgnoreCase));
        if (key is not null)
        {
            item.Remove(key);
        }
    }

    /// <summary>Vlastnost po tečkované cestě (<c>planting.species</c>).</summary>
    private static JsonNode? FindPath(JsonObject node, string path)
    {
        JsonNode? current = node;
        foreach (string part in path.Split('.'))
        {
            if (current is not JsonObject obj)
            {
                return null;
            }

            current = FindProperty(obj, part);
        }

        return current;
    }

    /// <summary>Vlastnost bez ohledu na velikost písmen (loader čte JSON stejně).</summary>
    private static JsonNode? FindProperty(JsonObject node, string name)
    {
        foreach (var pair in node)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
