using CivDle.Core.Content;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle;

/// <summary>
/// Překládá známé pády při startu do věty, se kterou se dá něco dělat.
///
/// <para>Proč to existuje: chyby, které hru zastaví ještě před oknem, skoro nikdy
/// nejsou chyby hry — jsou to staré ovladače, blokace od Windows nebo rozbitá
/// data. Hráč na ně dostane stack trace, kterému nerozumí, a hru odinstaluje.
/// Jedna věta „aktualizuj ovladače" zachrání víc hráčů než celý výpis.</para>
///
/// <para>Text je česky natvrdo schválně: tohle se děje dřív, než se stihne
/// načíst lokalizace — a hláška, která se neukáže, protože ji nemá kdo přeložit,
/// je k ničemu.</para>
/// </summary>
internal static class StartupDiagnosis
{
    /// <summary>
    /// Windows: „Zásada řízení aplikací tento soubor zablokovala."
    /// Chodí od Smart App Control (Windows 11) i od firemní WDAC politiky.
    /// </summary>
    private const int BlockedByAppControl = unchecked((int)0x800711C7);

    /// <summary>
    /// Rada k dané výjimce, nebo <c>null</c>, když ji neznáme a stačí výpis.
    /// </summary>
    public static string? HintFor(Exception exception) => exception switch
    {
        // Nejčastější pád na cizím stroji: OpenGL bez framebufferů. To není stará
        // karta — to je Windows, který podstrčil softwarový GDI renderer.
        NoSuitableGraphicsDeviceException => string.Join(
            Environment.NewLine,
            "Grafika neumí, co hra potřebuje (OpenGL s framebuffery).",
            "  • Nainstaluj ovladače přímo od výrobce grafiky (NVIDIA / AMD / Intel), ne z Windows Update.",
            "  • Přes Vzdálenou plochu hra nepoběží — ta OpenGL 3 nenabízí. Spusť ji přímo u počítače.",
            "  • Ve virtuálu zapni 3D akceleraci."),

        // Windows umí zablokovat nepodepsanou knihovnu dřív, než ji hra načte.
        // Bez vysvětlení to vypadá jako rozbitá instalace.
        FileLoadException { HResult: BlockedByAppControl } blocked => string.Join(
            Environment.NewLine,
            $"Windows zablokoval knihovnu hry: {Path.GetFileName(blocked.FileName ?? "?")}",
            "  Blokuje to Inteligentní řízení aplikací (Smart App Control) nebo firemní zásada.",
            "  • Pusť podepsaný build hry místo vývojářského (viz publish.cmd).",
            "  • Na firemním počítači si vyžádej výjimku u správce.",
            "  • Pozor: Smart App Control jde vypnout jen jednou — zpátky se bez reinstalace Windows nezapne."),

        // Data-driven obsah je fail-fast; tady chybu nejspíš způsobil mod nebo
        // ruční úprava JSONu, takže má smysl poslat hráče do dat, ne do kódu.
        ContentLoadException => string.Join(
            Environment.NewLine,
            "Herní data se nepovedlo načíst.",
            "  • Když máš mody, zkus je dočasně odebrat ze složky mods/.",
            "  • Jinak dej data/ do původního stavu (git checkout data)."),

        _ => null,
    };
}
