namespace CivDle.Core.Content;

/// <summary>
/// Významná osobnost — člověk, který se ve městě narodí, něco po dobu života
/// zlepší, a když zemře, zůstane po něm socha.
///
/// <para><b>Proč to ve hře je:</b> idle hra je z definice o číslech, která
/// rostou. Osobnost je jediná věc, která z běhu dělá <b>příběh</b> — město
/// nemá jen deset tisíc obyvatel, ale mělo Marii, za které se dvakrát rychleji
/// zkoumalo, a stojí jí tam socha.</para>
///
/// <para><b>Žádná nová mechanika.</b> Efekt jde tímtéž slovníkem jako Vzestup
/// a družice, socha je normální budova. Nové je jen to, že bonus po čase
/// zmizí a nahradí ho ta socha — a že to má jméno.</para>
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>figure.&lt;id&gt;</c>).</param>
/// <param name="Effect">Behavior-ID bonusu — tytéž efekty jako u Vzestupu.</param>
/// <param name="Magnitude">Síla bonusu, dokud osobnost žije.</param>
/// <param name="LifeTicks">Jak dlouho žije.</param>
/// <param name="MilestoneIndex">Milník, na kterém se narodí; −1 = kdykoli.</param>
/// <param name="StatueBuildingIndex">Socha, která po ní zůstane; −1 = žádná.</param>
public sealed record FigureDef(
    string Id,
    string Effect,
    double Magnitude,
    long LifeTicks,
    int MilestoneIndex = -1,
    int StatueBuildingIndex = -1)
{
    /// <summary>Zůstane po ní na mapě socha?</summary>
    public bool LeavesStatue => StatueBuildingIndex >= 0;

    /// <summary>Váže se na konkrétní milník?</summary>
    public bool NeedsMilestone => MilestoneIndex >= 0;
}

/// <summary>
/// Katalog osobností. Prázdný = mechanika vypnutá (starší data, mody).
/// </summary>
/// <param name="Figures">Kdo se může narodit.</param>
public sealed record FigureCatalog(IReadOnlyList<FigureDef> Figures)
{
    /// <summary>Hra bez osobností.</summary>
    public static FigureCatalog Empty { get; } = new(Array.Empty<FigureDef>());

    /// <summary>Rodí se ve hře vůbec někdo?</summary>
    public bool IsEnabled => Figures.Count > 0;

    public int Count => Figures.Count;

    public FigureDef this[int index] => Figures[index];

    /// <summary>Index podle ID, nebo −1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Figures.Count; i++)
        {
            if (string.Equals(Figures[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
