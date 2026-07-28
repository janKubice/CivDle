namespace CivDle.Rendering;

/// <summary>
/// Kdy se co přestane kreslit. Jedno místo pro všechny prahy, ne konstanta
/// schovaná v každém rendereru zvlášť.
///
/// <para>Proč to vzniklo: prahy byly rozházené (0.5 tady, 0.55 tam) a všechny
/// seděly těsně nad hranicí agregátního pohledu. Vzniklo z toho úzké pásmo
/// přiblížení, ve kterém se kreslily desetitisíce drobných spritů přes celou
/// obrazovku — měření ukázalo 191 ms na snímek při zoomu 0.55 a 3 ms hned
/// o kousek dál, kde už se nekreslilo nic. Padesátinásobný útes na půl otáčky
/// kolečka.</para>
///
/// <para>Princip: detail se nemá vypínat naráz, ale po vrstvách odshora dolů.
/// Čím dál je kamera, tím dřív zmizí věci, které stejně nejsou vidět —
/// dekorace jsou pár pixelů, stromy o něco víc, budovy nejdéle.</para>
/// </summary>
public static class DetailLevel
{
    /// <summary>
    /// Pod tímhle přiblížením se nekreslí drobné dekorace (kytky, kamínky).
    /// Jsou to 1–6px tečky; při menším zoomu z nich je jen šum, ale platí se
    /// za ně plnou cenou.
    /// </summary>
    public const float Decorations = 1.25f;

    /// <summary>
    /// Pod tímhle přiblížením se nekreslí těžitelné uzly (stromy, kameny).
    /// Jsou větší než dekorace, takže vydrží déle — ale je jich jeden na
    /// dlaždici, takže při oddálení jde o desetitisíce spritů.
    /// </summary>
    public const float Harvestables = 0.95f;

    /// <summary>
    /// Pod tímhle přiblížením se budovy kreslí zjednodušeně: jeden barevný
    /// obdélník místo spritu, stínu a odznaků. Tvar města zůstane čitelný,
    /// jen se za něj neplatí třemi kresbami na budovu.
    /// </summary>
    public const float BuildingSprites = 1.0f;

    /// <summary>
    /// Pod tímhle přiblížením se nekreslí živé drobnosti (chodci, zvěř,
    /// bubliny, zlaté nálezy). Na téhle vzdálenosti jsou to jednotlivé pixely.
    /// </summary>
    public const float Creatures = 0.9f;

    /// <summary>
    /// Kolik dlaždic se ještě smí projít po jedné. Nad tímhle počtem se vrstva
    /// vzdá bez ohledu na zoom — pojistka pro nezvyklé poměry stran a rozlišení,
    /// aby jedno okno navíc neshodilo snímkovou frekvenci.
    /// </summary>
    public const int MaxPerTileWork = 9_000;

    /// <summary>Vejde se procházení dlaždic v daném obdélníku do rozpočtu?</summary>
    public static bool FitsBudget(int startX, int startY, int endX, int endY)
    {
        long tiles = (long)(endX - startX + 1) * (endY - startY + 1);
        return tiles > 0 && tiles <= MaxPerTileWork;
    }
}
