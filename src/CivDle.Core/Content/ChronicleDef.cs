namespace CivDle.Core.Content;

/// <summary>
/// Okamžik, ke kterému kronika umí napsat větu.
///
/// <para>Data říkají, <b>které</b> okamžiky se do kroniky zapisují a s jakým
/// prahem; kód říká, <b>jak</b> se takový okamžik v časosběru pozná. Kdyby si
/// podmínku nesla data (nějaké „když populace &gt; X a zároveň…"), byla by to
/// logika v JSON — a to se v tomhle projektu nedělá.</para>
/// </summary>
public enum ChronicleMoment
{
    /// <summary>První snímek — kdy a s čím to celé začalo.</summary>
    Founded,

    /// <summary>Snímek s největším přírůstkem obyvatel proti předchozímu.</summary>
    Growth,

    /// <summary>První snímek v každé nové éře.</summary>
    EraChange,

    /// <summary>Snímek s nejvyšší populací za celý běh.</summary>
    Peak,

    /// <summary>První snímek, kdy civilizace měla aspoň tolik sídel, kolik říká práh.</summary>
    Settlements,

    /// <summary>První snímek, kdy spokojenost klesla na práh nebo pod něj.</summary>
    Hardship,

    /// <summary>První snímek, kdy špína nad městem dosáhla prahu.</summary>
    Pollution,

    /// <summary>Poslední snímek — kde město stojí dnes.</summary>
    Today,
}

/// <summary>
/// Jedna věta kroniky: který okamžik popisuje a od jaké hranice se počítá.
///
/// <para>Text věty je v jazycích pod <c>chronicle.line.&lt;Id&gt;</c> — a schválně
/// tam, ne v datech: skloňování („po dvou letech" vs. „po pěti letech")
/// nejde vyřešit jednou šablonou pro všechny jazyky a překladatel musí mít
/// možnost napsat celou větu po svém.</para>
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace.</param>
/// <param name="Moment">Který okamžik věta popisuje.</param>
/// <param name="Threshold">
/// Hranice pro okamžiky, které ji potřebují (počet sídel, podíl spokojenosti
/// nebo špíny). U ostatních se neuplatní.
/// </param>
public sealed record ChronicleTemplateDef(string Id, ChronicleMoment Moment, double Threshold = 0)
{
    /// <summary>Lokalizační klíč věty.</summary>
    public string TextKey => $"chronicle.line.{Id}";
}

/// <summary>
/// Šablony vět kroniky. Prázdný katalog = kronika se nepíše (starší data, mody).
/// </summary>
/// <param name="Templates">Věty v pořadí, v jakém leží v datech.</param>
public sealed record ChronicleCatalog(IReadOnlyList<ChronicleTemplateDef> Templates)
{
    /// <summary>Hra bez kroniky.</summary>
    public static ChronicleCatalog Empty { get; } = new(Array.Empty<ChronicleTemplateDef>());

    /// <summary>Píše se vůbec kronika?</summary>
    public bool IsEnabled => Templates.Count > 0;

    public int Count => Templates.Count;

    public ChronicleTemplateDef this[int index] => Templates[index];
}
