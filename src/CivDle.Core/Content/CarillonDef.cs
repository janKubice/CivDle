namespace CivDle.Core.Content;

/// <summary>
/// Nastavení zvonohry: která budova zvoní, co zvoní ve výchozím stavu a jak
/// zní.
///
/// <para><b>Proč jsou tóny v datech, a ne v kódu:</b> zvonohra nepotřebuje ani
/// jeden zvukový soubor — tóny se generují. Tím pádem je „jak to zní" otázka
/// dvou čísel (základní frekvence a délka tónu), a to jsou data jako každá
/// jiná. Kód umí stupnici, data říkají, od kterého tónu se počítá.</para>
/// </summary>
/// <param name="BuildingIndex">Budova, která zvoní; −1 = mechanika vypnutá.</param>
/// <param name="DefaultTune">Melodie, se kterou zvonohra začíná (stupně stupnice, −1 = pauza).</param>
/// <param name="BaseFrequency">Frekvence nejnižšího tónu v hertzech.</param>
/// <param name="NoteSeconds">Jak dlouho zní jeden tón.</param>
public sealed record CarillonConfig(
    int BuildingIndex,
    IReadOnlyList<int> DefaultTune,
    double BaseFrequency,
    double NoteSeconds)
{
    /// <summary>Hra bez zvonohry.</summary>
    public static CarillonConfig Disabled { get; } =
        new(-1, Array.Empty<int>(), 523.25, 0.45);

    /// <summary>Dá se zvonohra ve hře vůbec postavit?</summary>
    public bool IsEnabled => BuildingIndex >= 0;
}
