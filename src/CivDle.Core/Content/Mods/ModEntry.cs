using System.Globalization;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content.Mods;

/// <summary>
/// Jeden vyplněný kus obsahu v tvůrci: typ + hodnoty jeho polí.
///
/// <para>Hodnoty se drží jako <b>text</b>, protože přesně to hráč v editoru
/// vyplňuje — převod na číslo, bool nebo pole se dělá až při zápisu, podle
/// druhu pole z katalogu. Kdyby si model držel typy sám, musel by znát každý
/// typ obsahu, a tím by celé oddělení „data popisují, kód vykonává" padlo.</para>
///
/// <para>Seznamy (odkazy, jména) se ukládají oddělené čárkou, dvojice
/// surovina+množství jako <c>wood=20, stone=10</c>. Je to formát, který jde
/// napsat i rukou — mod je pořád jen JSON a hráč ho smí opravit v poznámkovém
/// bloku.</para>
/// </summary>
public sealed class ModEntry
{
    public ModEntry(string typeId)
    {
        TypeId = typeId;
    }

    /// <summary>ID typu z <see cref="ModTypeCatalog"/>.</summary>
    public string TypeId { get; }

    /// <summary>Vyplněné hodnoty podle klíčů polí.</summary>
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    /// <summary>Hodnota pole, nebo jeho výchozí hodnota z katalogu.</summary>
    public string Value(ModFieldDef field) =>
        Values.TryGetValue(field.Key, out string? value) && value.Length > 0 ? value : field.Default;

    /// <summary>Nastaví hodnotu pole (prázdná = pole se nezapíše).</summary>
    public ModEntry With(string key, string value)
    {
        Values[key] = value;
        return this;
    }

    /// <summary>ID záznamu — bere se z prvního pole druhu <see cref="ModFieldKind.Id"/>.</summary>
    public string IdOf(ModTypeDef type)
    {
        foreach (var field in type.Fields)
        {
            if (field.Kind == ModFieldKind.Id)
            {
                return Value(field);
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// Převod vyplněných záznamů na JSON podle katalogu typů.
///
/// <para>Jediné místo, kde se ví, jak z „hráč napsal 20" vznikne
/// <c>"buildCost": { "wood": 20 }</c>. Díky tomu jde zápis testovat bez UI
/// a nový typ obsahu nepotřebuje ani řádek nového kódu.</para>
/// </summary>
public static class ModEntryWriter
{
    /// <summary>Složí JSON jednoho záznamu (objekt, nebo prostý text u seznamů jmen).</summary>
    public static JsonNode? ToJson(ModEntry entry, ModTypeDef type)
    {
        if (type.PlainList)
        {
            string text = entry.Values.Values.FirstOrDefault() ?? string.Empty;
            return text.Length == 0 ? null : JsonValue.Create(text);
        }

        var node = new JsonObject();
        foreach (var field in type.Fields)
        {
            if (!field.GoesToContent)
            {
                continue; // jméno a popis jdou do jazyků, ne do obsahu
            }

            // Pole, které závisí na jiném: bez něj se nezapíše, i kdyby mělo
            // výchozí hodnotu (doba výroby u budovy, která nic nevyrábí).
            if (field.Requires.Length > 0 && Lookup(entry, type, field.Requires).Length == 0)
            {
                continue;
            }

            var value = ValueNode(entry.Value(field), field);
            if (value is not null)
            {
                Assign(node, field.Path, value);
            }
        }

        return node;
    }

    /// <summary>
    /// Hodnota pole jako JSON. <c>null</c> = pole se nezapíše — prázdný recept
    /// nebo prázdný seznam biomů má jiný význam než chybějící klíč.
    /// </summary>
    private static JsonNode? ValueNode(string raw, ModFieldDef field)
    {
        string text = raw.Trim();
        switch (field.Kind)
        {
            case ModFieldKind.Toggle:
                return text.Length == 0 ? null : JsonValue.Create(
                    text.Equals("true", StringComparison.OrdinalIgnoreCase));

            case ModFieldKind.Number:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole)
                    ? JsonValue.Create(whole)
                    : null;

            case ModFieldKind.Decimal:
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                    ? JsonValue.Create(number)
                    : null;

            case ModFieldKind.References:
                var list = new JsonArray();
                foreach (string item in Split(text))
                {
                    list.Add(item);
                }

                return list.Count == 0 ? null : list;

            case ModFieldKind.Amounts:
                var amounts = new JsonObject();
                foreach (string pair in Split(text))
                {
                    int split = pair.IndexOf('=');
                    if (split <= 0)
                    {
                        continue; // „wood" bez množství nedává smysl — přeskočit
                    }

                    string id = pair[..split].Trim();
                    if (id.Length > 0 && int.TryParse(
                            pair[(split + 1)..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
                    {
                        amounts[id] = amount;
                    }
                }

                return amounts.Count == 0 ? null : amounts;

            default:
                return text.Length == 0 ? null : JsonValue.Create(text);
        }
    }

    /// <summary>Hodnota jiného pole téhož záznamu (pro závislosti).</summary>
    private static string Lookup(ModEntry entry, ModTypeDef type, string key)
    {
        foreach (var field in type.Fields)
        {
            if (field.Key == key)
            {
                return entry.Value(field).Trim();
            }
        }

        return string.Empty;
    }

    /// <summary>Rozdělí seznam psaný čárkami; prázdné položky se zahodí.</summary>
    public static IEnumerable<string> Split(string text)
    {
        foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    /// <summary>
    /// Uloží hodnotu na tečkovou cestu a založí přitom, co po cestě chybí:
    /// <c>recipe.input</c> vyrobí vnořený objekt, <c>footprint[0]</c> pole.
    /// </summary>
    private static void Assign(JsonObject root, string path, JsonNode value)
    {
        var segments = path.Split('.');
        JsonObject current = root;

        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            int bracket = segment.IndexOf('[');
            bool last = i == segments.Length - 1;

            if (bracket < 0)
            {
                if (last)
                {
                    current[segment] = value;
                    return;
                }

                current = Child(current, segment);
                continue;
            }

            string name = segment[..bracket];
            int index = int.Parse(
                segment[(bracket + 1)..segment.IndexOf(']')], CultureInfo.InvariantCulture);

            if (current[name] is not JsonArray array)
            {
                array = new JsonArray();
                current[name] = array;
            }

            while (array.Count <= index)
            {
                array.Add(last ? JsonValue.Create(0) : new JsonObject());
            }

            if (last)
            {
                array[index] = value;
                return;
            }

            if (array[index] is not JsonObject item)
            {
                item = new JsonObject();
                array[index] = item;
            }

            current = item;
        }
    }

    private static JsonObject Child(JsonObject parent, string key)
    {
        if (parent[key] is JsonObject existing)
        {
            return existing;
        }

        var child = new JsonObject();
        parent[key] = child;
        return child;
    }
}
