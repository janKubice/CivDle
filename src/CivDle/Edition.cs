namespace CivDle;

/// <summary>
/// Která edice hry je přeložená — plná, nebo demo.
///
/// <para>Je to <b>překladová konstanta</b>, ne nastavení ani soubor v datech.
/// Důvod je obchodní, ne technický: omylem rozeslaná plná hra označená jako
/// demo je průšvih a omylem zveřejněné demo s celým obsahem ještě větší. Když
/// o edici rozhoduje překlad, nemůže se přepnout ničím, co jde na disku
/// omylem přejmenovat nebo smazat.</para>
///
/// <para>Meze samotné (strop obyvatel, práh druhého Vzestupu, díl stromu) jsou
/// naopak <b>v datech</b> — ty se ladí podle toho, jak dlouho má ukázka trvat,
/// a překládat kvůli tomu hru je zbytečné.</para>
///
/// <para>Demo se vyrábí přes <c>./publish.sh &lt;rid&gt; demo</c>, které nastaví
/// <c>-p:GameEdition=Demo</c>.</para>
/// </summary>
public static class Edition
{
    /// <summary>
    /// Běží demoverze?
    ///
    /// <para><c>static readonly</c>, ne <c>const</c>: u konstanty by překladač
    /// v plném buildu viděl každé <c>if (Edition.IsDemo)</c> jako mrtvý kód
    /// a zasypal build varováními CS0162. Chování je stejné (hodnota se stejně
    /// rozhoduje při překladu), jen se nekřičí na něco, co je záměr.</para>
    /// </summary>
    public static readonly bool IsDemo =
#if DEMO
        true;
#else
        false;
#endif
}
