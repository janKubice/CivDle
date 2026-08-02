using System.Runtime.InteropServices;

namespace CivDle;

/// <summary>
/// Ukáže chybu při startu jako systémové okno.
///
/// <para>Proč to existuje: hráč ze Steamu nemá konzoli. Klikne na Hrát, nic se
/// neobjeví — a jde žádat o vrácení peněz, protože hra „nefunguje". Konzolový
/// výpis ani <c>crash.log</c> mu nepomůžou, když o nich neví. Systémové okno je
/// jediná cesta, jak se k němu ta věta dostane.</para>
///
/// <para>Vědomě jen Windows: tam je hráčů nejvíc a <c>user32</c> je vždycky po
/// ruce (nezávisle na tom, jestli hra běží na OpenGL, nebo DirectX). Na Linuxu
/// a macOS zůstává konzole, kterou tamní hráči spíš mají.</para>
/// </summary>
internal static class StartupAlert
{
    private const uint IconError = 0x00000010;

    /// <summary>Okno bez vlastníka — hra v tuhle chvíli žádné nemá.</summary>
    private const uint TaskModal = 0x00002000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);

    /// <summary>
    /// Vyskočí okno s vysvětlením. Stack trace se do něj schválně nedává —
    /// hráč z něj nic nemá a jen by odstrčil větu, která má smysl.
    /// </summary>
    /// <param name="summary">Jedna věta, co se stalo.</param>
    /// <param name="hint">Co s tím dělat (může být prázdné).</param>
    /// <param name="crashLogPath">Kam se uložil podrobný výpis (může být prázdné).</param>
    public static void Show(string summary, string? hint, string crashLogPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var text = new System.Text.StringBuilder(summary);
        if (!string.IsNullOrEmpty(hint))
        {
            text.AppendLine().AppendLine().Append(hint);
        }

        if (crashLogPath.Length > 0)
        {
            text.AppendLine().AppendLine().Append("Podrobnosti: ").Append(crashLogPath);
        }

        try
        {
            MessageBoxW(IntPtr.Zero, text.ToString(), "CivDle se nespustil", IconError | TaskModal);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Prostředí bez user32 (Server Core) — zbývá konzole a crash.log.
        }
    }
}
