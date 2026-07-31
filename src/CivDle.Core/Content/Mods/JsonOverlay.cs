using System.Text.Json;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content.Mods;

/// <summary>
/// Slévání datových souborů: základní hra plus to, co k ní přidávají mody.
///
/// <para>Proč to takhle: veškerý obsah hry už v JSON je (budovy, suroviny,
/// vozidla, jazyky…). Chybělo jen jediné — způsob, jak k němu <b>přidat</b>
/// nebo v něm <b>přepsat</b> jednu položku, aniž by mod musel dodat celý soubor
/// a tím zmrazit hru na verzi, ve které vznikl. Tahle třída je celý ten chybějící
/// kus.</para>
///
/// <para>Pravidla jsou schválně nudná a předvídatelná:</para>
/// <list type="bullet">
/// <item>Pole položek s <c>id</c> se slévají podle <c>id</c>: shodné ID nahradí
/// původní záznam (na jeho místě, ať se nemění pořadí), nové se připojí na konec.</item>
/// <item>Pole bez <c>id</c> (barvy, seznamy jmen) mod nahradí celé — u nich není
/// co s čím párovat.</item>
/// <item>Objekty se slévají po klíčích do hloubky, takže mod smí změnit jediné
/// číslo v nastavení a zbytku se nedotknout.</item>
/// </list>
///
/// <para>Čistá funkce nad textem: žádné soubory, žádný stav — proto jde ověřit
/// testem a proto ji smí použít i nástroje mimo hru.</para>
/// </summary>
public static class JsonOverlay
{
    /// <summary>Klíč, podle kterého se párují položky v polích.</summary>
    private const string IdKey = "id";

    /// <summary>
    /// Slije základní soubor s vrstvami modů (v pořadí, v jakém se mají uplatnit —
    /// pozdější přebíjí dřívější).
    /// </summary>
    public static string Merge(string baseJson, IReadOnlyList<string> overlays)
    {
        if (overlays.Count == 0)
        {
            return baseJson;
        }

        var result = JsonNode.Parse(baseJson);
        foreach (string overlay in overlays)
        {
            result = MergeNode(result, JsonNode.Parse(overlay));
        }

        return result?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? baseJson;
    }

    private static JsonNode? MergeNode(JsonNode? baseNode, JsonNode? overlay)
    {
        if (overlay is null)
        {
            return baseNode;
        }

        return (baseNode, overlay) switch
        {
            (JsonObject baseObject, JsonObject overlayObject) => MergeObjects(baseObject, overlayObject),
            (JsonArray baseArray, JsonArray overlayArray) => MergeArrays(baseArray, overlayArray),

            // Hodnota (číslo, text, bool) i nesourodé typy: mod prostě vyhrává.
            _ => overlay.DeepClone(),
        };
    }

    private static JsonNode MergeObjects(JsonObject baseObject, JsonObject overlayObject)
    {
        var result = (JsonObject)baseObject.DeepClone();
        foreach (var pair in overlayObject)
        {
            result[pair.Key] = result.TryGetPropertyValue(pair.Key, out var existing)
                ? MergeNode(existing, pair.Value)
                : pair.Value?.DeepClone();
        }

        return result;
    }

    private static JsonNode MergeArrays(JsonArray baseArray, JsonArray overlayArray)
    {
        // Bez ID není co s čím párovat — mod nahradí celé pole.
        if (!HasIds(baseArray) || !HasIds(overlayArray))
        {
            return overlayArray.DeepClone();
        }

        var result = (JsonArray)baseArray.DeepClone();
        foreach (var item in overlayArray)
        {
            string? id = IdOf(item);
            if (id is null)
            {
                result.Add(item?.DeepClone());
                continue;
            }

            int existing = IndexOfId(result, id);
            if (existing >= 0)
            {
                // Na místě, ne na konci: pořadí položek nese význam (éry, stupně
                // měřítka) a mod, který mění cenu domu, ho nemá posunout na konec.
                var merged = MergeNode(result[existing], item);
                result[existing] = null;
                result[existing] = merged;
            }
            else
            {
                result.Add(item?.DeepClone());
            }
        }

        return result;
    }

    private static bool HasIds(JsonArray array)
    {
        foreach (var item in array)
        {
            if (item is JsonObject && IdOf(item) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static string? IdOf(JsonNode? node) =>
        node is JsonObject item && item.TryGetPropertyValue(IdKey, out var id) && id is JsonValue value
            ? value.GetValue<object>().ToString()
            : null;

    private static int IndexOfId(JsonArray array, string id)
    {
        for (int i = 0; i < array.Count; i++)
        {
            if (IdOf(array[i]) == id)
            {
                return i;
            }
        }

        return -1;
    }
}
