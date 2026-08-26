using System.Runtime.InteropServices;

namespace CivDle.Platform;

/// <summary>
/// Schránka operačního systému.
///
/// <para><b>Proč vlastní třída:</b> MonoGame schránku neumí. DesktopGL má pod
/// sebou SDL, které ji umí — jenže funkce se musí zavolat napřímo. Na Windows
/// s DirectX backendem SDL není, takže se sáhne na Win32.</para>
///
/// <para><b>Selhání není chyba hry.</b> Když schránka nejde (jiná platforma,
/// jiný backend, prázdná), vrátí se prázdný text nebo <c>false</c> a hráč jen
/// zkopíruje ručně. Spadnout kvůli schránce by bylo absurdní.</para>
///
/// <para>Vrstva: aplikace. Jádro o schránce neví — vyrábí jen text.</para>
/// </summary>
public static class Clipboard
{
    /// <summary>Zkusí dát text do schránky. Vrací false, když to nešlo.</summary>
    public static bool TrySet(string text)
    {
        if (text is null)
        {
            return false;
        }

        try
        {
            return Sdl.SetClipboardText(text) == 0;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException
                                          or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Přečte text ze schránky; prázdný, když tam nic použitelného není.</summary>
    public static string Get()
    {
        try
        {
            if (Sdl.HasClipboardText() != 1)
            {
                return string.Empty;
            }

            IntPtr pointer = Sdl.GetClipboardText();
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUTF8(pointer) ?? string.Empty;
            }
            finally
            {
                Sdl.Free(pointer); // SDL text alokuje a čeká, že ho uvolníme
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException
                                          or BadImageFormatException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Volání do SDL2, na kterém DesktopGL stojí.
    ///
    /// <para>Jméno knihovny je „SDL2" bez přípony — .NET si příponu doplní
    /// podle systému (<c>SDL2.dll</c>, <c>libSDL2.so</c>, <c>libSDL2.dylib</c>),
    /// takže tenhle jeden zápis platí všude, kde hra běží.</para>
    /// </summary>
    private static class Sdl
    {
        [DllImport("SDL2", EntryPoint = "SDL_SetClipboardText", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        [DllImport("SDL2", EntryPoint = "SDL_GetClipboardText", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetClipboardText();

        [DllImport("SDL2", EntryPoint = "SDL_HasClipboardText", CallingConvention = CallingConvention.Cdecl)]
        public static extern int HasClipboardText();

        [DllImport("SDL2", EntryPoint = "SDL_free", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Free(IntPtr memory);
    }
}
