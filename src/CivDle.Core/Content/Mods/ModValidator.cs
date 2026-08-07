namespace CivDle.Core.Content.Mods;

/// <summary>Výsledek kontroly modu.</summary>
/// <param name="Ok">Načte se mod bez chyby?</param>
/// <param name="Message">Co je špatně (nebo potvrzení, že je vše v pořádku).</param>
public readonly record struct ModCheck(bool Ok, string Message);

/// <summary>
/// Ověří, že se mod opravdu načte — <b>tím, že ho zkusí načíst</b>.
///
/// <para>Tohle je jádro celého editoru. Vlastní sada kontrol by nutně
/// zaostávala za skutečným loaderem a dřív nebo později by pustila mod, který
/// hru shodí při startu — tedy přesně ve chvíli, kdy už hráč nemá kde ho
/// vypnout. Proto se používá <see cref="ContentLoader"/>, ten samý, který hru
/// spouští: co projde tady, projde i při startu.</para>
///
/// <para>Kontroluje se do dočasné kopie, ne do živé hry: běžící partie se
/// nesmí změnit jen tím, že si hráč něco zkusil v editoru.</para>
///
/// <para>Vrstva: jádro. Sahá na disk, nezná render.</para>
/// </summary>
public static class ModValidator
{
    /// <summary>
    /// Zkusí načíst hru se zapnutým modem a vrátí, jak to dopadlo.
    /// </summary>
    /// <param name="dataDirectory">Složka se základními daty hry.</param>
    /// <param name="modDirectory">Složka kontrolovaného modu.</param>
    public static ModCheck Check(string dataDirectory, string modDirectory)
    {
        if (!Directory.Exists(modDirectory))
        {
            return new ModCheck(false, $"Složka modu neexistuje: {modDirectory}");
        }

        try
        {
            var package = new ModPackage(
                Path.GetFileName(modDirectory), Path.GetFileName(modDirectory), "0", modDirectory);

            var content = new ContentLoader().LoadFrom(dataDirectory, new[] { package });

            return new ModCheck(
                true,
                $"Mod je v pořádku. Po zapnutí bude hra znát {content.Buildings.Count} budov "
                + $"a {content.Resources.Count} surovin.");
        }
        catch (ContentLoadException ex)
        {
            // Hláška z loaderu je určená člověku a říká i soubor — přeposílá se
            // beze změny, protože vlastní přeformulování by z ní ubralo.
            return new ModCheck(false, ex.Message);
        }
        catch (Exception ex)
        {
            // Cokoli jiného je chyba, na kterou loader nebyl připravený. Pro
            // autora modu je pořád lepší vidět ji tady než při příštím startu.
            return new ModCheck(false, $"Neočekávaná chyba při načítání: {ex.Message}");
        }
    }
}
