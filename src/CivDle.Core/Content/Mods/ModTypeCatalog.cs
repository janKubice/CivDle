using System.Text.Json;
using System.Text.Json.Nodes;

namespace CivDle.Core.Content.Mods;

/// <summary>Jak se pole vyplňuje a co znamená ve výsledném JSON.</summary>
public enum ModFieldKind
{
    /// <summary>Stabilní ID — malá písmena, číslice, podtržítko.</summary>
    Id,

    /// <summary>Volný text.</summary>
    Text,

    /// <summary>Celé číslo.</summary>
    Number,

    /// <summary>Desetinné číslo.</summary>
    Decimal,

    /// <summary>Ano/ne.</summary>
    Toggle,

    /// <summary>Barva <c>#RRGGBB</c>.</summary>
    Color,

    /// <summary>Výběr z pevné nabídky.</summary>
    Choice,

    /// <summary>Odkaz na jednu existující věc (surovina, budova, biom, technologie).</summary>
    Reference,

    /// <summary>Seznam odkazů.</summary>
    References,

    /// <summary>Dvojice surovina + množství (cena, recept, odměna).</summary>
    Amounts,

    /// <summary>Obrázek — kreslí se v editoru spritů.</summary>
    Sprite,

    /// <summary>Jméno pro hráče; nejde do obsahu, ale do jazyků modu.</summary>
    Lang,

    /// <summary>Popis pro hráče; taky do jazyků (klíč <c>…​.desc</c>).</summary>
    LangDescription,
}

/// <summary>
/// Jedno pole jednoho typu obsahu.
/// </summary>
/// <param name="Key">Klíč pole (jméno v editoru i v jazykových klíčích).</param>
/// <param name="Kind">Jak se vyplňuje.</param>
/// <param name="Path">
/// Kam hodnota patří ve výsledném JSON. Tečka = vnořený objekt, <c>[n]</c> = prvek
/// pole (<c>footprint[0]</c>, <c>recipe.input</c>, <c>choices[0].gain</c>).
/// Prázdná cesta = pole se do obsahu nezapisuje (jména a popisy).
/// </param>
/// <param name="Reference">Na co pole odkazuje (<c>resource</c>, <c>building</c>, <c>biome</c>, <c>tech</c>).</param>
/// <param name="Options">Nabídka u <see cref="ModFieldKind.Choice"/>.</param>
/// <param name="Default">Výchozí hodnota (řetězec; čísla se převedou).</param>
/// <param name="Min">Dolní mez u čísel.</param>
/// <param name="Max">Horní mez u čísel.</param>
/// <param name="Requires">
/// Klíč pole, bez kterého tohle nemá smysl zapisovat.
///
/// <para>Existuje kvůli výchozím hodnotám ve vnořených objektech: doba výroby
/// má rozumné výchozí číslo, ale zapsat ji u budovy, která nic nevyrábí, by
/// vyrobilo prázdný recept — tedy výrobnu, co nic nedělá a jen bere lidi.</para>
/// </param>
public sealed record ModFieldDef(
    string Key,
    ModFieldKind Kind,
    string Path,
    string Reference = "",
    IReadOnlyList<string>? Options = null,
    string Default = "",
    double Min = double.MinValue,
    double Max = double.MaxValue,
    string Requires = "")
{
    /// <summary>Zapisuje se pole do obsahu (na rozdíl od jmen a popisů)?</summary>
    public bool GoesToContent => Path.Length > 0;

    /// <summary>Lokalizační klíč jména pole v editoru.</summary>
    public string LabelKey => $"modfield.{Key}";
}

/// <summary>
/// Popis jednoho typu obsahu, který umí ingame tvůrce vyrobit.
/// </summary>
/// <param name="Id">ID typu (<c>building</c>, <c>resource</c>…).</param>
/// <param name="File">Do jakého souboru modu se zapisuje.</param>
/// <param name="ArrayKey">Pod jakým klíčem leží pole záznamů.</param>
/// <param name="LangPrefix">Předpona jazykových klíčů (<c>building</c> → <c>building.&lt;id&gt;</c>).</param>
/// <param name="PlainList">Je to prostý seznam řetězců (jména měst), ne objekty?</param>
/// <param name="Fields">Pole typu v pořadí, v jakém se ukazují.</param>
public sealed record ModTypeDef(
    string Id,
    string File,
    string ArrayKey,
    string LangPrefix,
    bool PlainList,
    IReadOnlyList<ModFieldDef> Fields)
{
    /// <summary>Lokalizační klíč jména typu v editoru.</summary>
    public string NameKey => $"modtype.{Id}";

    /// <summary>Má typ obrázek (a tedy i kreslítko)?</summary>
    public bool HasSprite
    {
        get
        {
            foreach (var field in Fields)
            {
                if (field.Kind == ModFieldKind.Sprite)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

/// <summary>
/// Katalog typů obsahu pro ingame tvůrce (bod 2 seznamu z hraní).
///
/// <para>Proč jsou typy v datech (<c>data/mod-types.json</c>) a ne v kódu:
/// tvůrce má umět budovu, surovinu, událost, výzkum, faunu, jména měst i úkol —
/// a v principu cokoli dalšího, co je ve hře v datech. Bez katalogu by každý
/// další typ znamenal novou ručně psanou obrazovku a u sedmi typů se to
/// rozpadne. S ním je přidání typu <b>záznam v JSON</b>.</para>
///
/// <para>Vrstva: jádro, žádné UI. Obrazovka nad tím jen vykresluje pole podle
/// jejich druhu.</para>
/// </summary>
public sealed class ModTypeCatalog
{
    /// <summary>Prázdný katalog — hra i editor musí naběhnout i bez souboru.</summary>
    public static ModTypeCatalog Empty { get; } = new(Array.Empty<ModTypeDef>());

    private readonly Dictionary<string, ModTypeDef> _byId;

    public ModTypeCatalog(IReadOnlyList<ModTypeDef> types)
    {
        Types = types;
        _byId = new Dictionary<string, ModTypeDef>(StringComparer.Ordinal);
        foreach (var type in types)
        {
            _byId[type.Id] = type;
        }
    }

    /// <summary>Všechny typy v pořadí ze souboru.</summary>
    public IReadOnlyList<ModTypeDef> Types { get; }

    /// <summary>Má hra tvůrce obsahu vůbec zapnutý?</summary>
    public bool IsEnabled => Types.Count > 0;

    /// <summary>Najde typ podle ID; <c>null</c> = neznámý.</summary>
    public ModTypeDef? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Načte katalog ze souboru. Chybějící soubor = prázdný katalog (mechanika
    /// je volitelná), ale <b>rozbitý</b> soubor je chyba obsahu jako každá jiná.
    /// </summary>
    public static ModTypeCatalog LoadFrom(string path)
    {
        if (!File.Exists(path))
        {
            return Empty;
        }

        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
            ?? throw new ContentLoadException(path, "Soubor není objekt.");

        var types = new List<ModTypeDef>();
        foreach (var node in root["types"]?.AsArray() ?? new JsonArray())
        {
            types.Add(ParseType(path, node?.AsObject()
                ?? throw new ContentLoadException(path, "Typ obsahu není objekt.")));
        }

        return new ModTypeCatalog(types);
    }

    private static ModTypeDef ParseType(string path, JsonObject node)
    {
        string id = Text(node, "id");
        if (id.Length == 0)
        {
            throw new ContentLoadException(path, "Typ obsahu bez 'id'.");
        }

        string file = Text(node, "file");
        string arrayKey = Text(node, "arrayKey");
        if (file.Length == 0 || arrayKey.Length == 0)
        {
            throw new ContentLoadException(path, $"Typ '{id}' nemá 'file' nebo 'arrayKey' — nebylo by kam zapisovat.");
        }

        var fields = new List<ModFieldDef>();
        foreach (var fieldNode in node["fields"]?.AsArray() ?? new JsonArray())
        {
            fields.Add(ParseField(path, id, fieldNode?.AsObject()
                ?? throw new ContentLoadException(path, $"Typ '{id}': pole není objekt.")));
        }

        if (fields.Count == 0)
        {
            throw new ContentLoadException(path, $"Typ '{id}' nemá jediné pole — nedal by se vyplnit.");
        }

        return new ModTypeDef(
            id, file, arrayKey, Text(node, "langPrefix"),
            node["plainList"]?.GetValue<bool>() ?? false, fields);
    }

    private static ModFieldDef ParseField(string path, string typeId, JsonObject node)
    {
        string key = Text(node, "key");
        if (key.Length == 0)
        {
            throw new ContentLoadException(path, $"Typ '{typeId}': pole bez 'key'.");
        }

        string kindText = Text(node, "kind");
        var kind = kindText switch
        {
            "id" => ModFieldKind.Id,
            "text" => ModFieldKind.Text,
            "number" => ModFieldKind.Number,
            "decimal" => ModFieldKind.Decimal,
            "toggle" => ModFieldKind.Toggle,
            "color" => ModFieldKind.Color,
            "choice" => ModFieldKind.Choice,
            "reference" => ModFieldKind.Reference,
            "references" => ModFieldKind.References,
            "amounts" => ModFieldKind.Amounts,
            "sprite" => ModFieldKind.Sprite,
            "lang" => ModFieldKind.Lang,
            "langDesc" => ModFieldKind.LangDescription,

            // Neznámý druh pole je chyba obsahu: editor by ho neuměl vykreslit
            // a autor by přišel na to, že mu pole chybí, až u hotového modu.
            _ => throw new ContentLoadException(path, $"Typ '{typeId}', pole '{key}': neznámý druh '{kindText}'."),
        };

        var options = new List<string>();
        foreach (var option in node["options"]?.AsArray() ?? new JsonArray())
        {
            options.Add(option?.GetValue<string>() ?? string.Empty);
        }

        if (kind == ModFieldKind.Choice && options.Count == 0)
        {
            throw new ContentLoadException(path, $"Typ '{typeId}', pole '{key}': výběr bez nabídky 'options'.");
        }

        return new ModFieldDef(
            key,
            kind,
            Text(node, "path"),
            Text(node, "reference"),
            options,
            DefaultText(node),
            node["min"]?.GetValue<double>() ?? double.MinValue,
            node["max"]?.GetValue<double>() ?? double.MaxValue,
            Text(node, "requires"));
    }

    private static string Text(JsonObject node, string key) =>
        node[key]?.GetValue<string>()?.Trim() ?? string.Empty;

    /// <summary>Výchozí hodnota se v JSON píše svým typem (číslo, bool, text).</summary>
    private static string DefaultText(JsonObject node)
    {
        var value = node["default"];
        if (value is null)
        {
            return string.Empty;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.String => value.GetValue<string>(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetValue<double>().ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => string.Empty,
        };
    }
}
