using System.Runtime.InteropServices;

namespace CivDle;

/// <summary>
/// Zviditelní chybu při startu i ve Windows buildu.
///
/// <para>Proč to existuje: hra je <c>WinExe</c>, aby při normálním spuštění
/// nevyskakovalo černé okno konzole. Jenže tím zmizí i chybové hlášky — když
/// hra spadne dřív, než otevře okno, uživatel vidí doslova nic a nemá se čeho
/// chytit. „Nespustí se to a nic se nestane" je nejhorší možná chybová hláška.</para>
///
/// <para>Řešení: když hru pustil terminál (<c>dotnet run</c>, cmd, PowerShell),
/// připoj se k jeho konzoli a piš do ní. Když ne (dvojklik z Průzkumníka),
/// nestane se nic a zůstává <c>crash.log</c>.</para>
/// </summary>
internal static class StartupConsole
{
    /// <summary>Konzole rodičovského procesu (viz Win32 <c>AttachConsole</c>).</summary>
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    /// <summary>
    /// Připojí se ke konzoli rodiče, pokud nějaká je. Mimo Windows nedělá nic —
    /// tam konzole funguje normálně a stderr je vidět sám od sebe.
    /// </summary>
    public static void AttachToParentIfPossible()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            AttachConsole(AttachParentProcess);
        }
        catch (DllNotFoundException)
        {
            // Nestandardní prostředí bez kernel32 — chyba pořád skončí v crash.log.
        }
        catch (EntryPointNotFoundException)
        {
            // Totéž; zviditelnění chyby nesmí samo shodit ohlašování chyby.
        }
    }
}
