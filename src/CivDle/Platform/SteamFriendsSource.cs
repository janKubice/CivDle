using CivDle.Core.Platform;
using Steamworks;

namespace CivDle.Platform;

/// <summary>Kamarád ze Steamu — jméno a obrázek profilu.</summary>
/// <param name="Name">Jak se mu píše v přátelích.</param>
/// <param name="AvatarHandle">Steamovský handle obrázku; −1 = nemá.</param>
public readonly record struct SteamFriend(string Name, int AvatarHandle);

/// <summary>
/// Jména a obličeje kamarádů ze Steamu — pro karavany.
///
/// <para><b>Proč jen jména a obrázky:</b> „veze to, co jeho město vyrábí
/// nejvíc" jsou data o cizí rozehrané hře a ta bez serveru nikde nejsou.
/// Jméno a obličej naopak Steam vrací synchronně a hned — a karavana, na které
/// jede kamarád, je devadesát procent toho pocitu za odpoledne práce.</para>
///
/// <para><b>Bez Steamu se to tiše přeskočí.</b> Karavany jezdí dál, jen bez
/// jmen. Prázdný seznam není chyba.</para>
///
/// <para>Vrstva: aplikace. Vrací data, ne textury — z handle udělá obrázek až
/// render, který jediný má grafiku.</para>
/// </summary>
public sealed class SteamFriendsSource
{
    /// <summary>
    /// Kolik jmen se nejvýš vezme. Kdo má tisíc přátel, stejně uvidí jen ty,
    /// co se vejdou do karavan — a projít tisíc dotazů při každém spuštění
    /// je práce pro nikoho.
    /// </summary>
    private const int MaxFriends = 64;

    private readonly IPlatformServices _platform;

    public SteamFriendsSource(IPlatformServices platform) => _platform = platform;

    /// <summary>
    /// Načte přátele. Prázdný seznam znamená „Steam tu není" i „nikoho nemá" —
    /// pro volajícího je to totéž.
    /// </summary>
    public IReadOnlyList<SteamFriend> Load()
    {
        if (!_platform.IsAvailable)
        {
            return Array.Empty<SteamFriend>();
        }

        try
        {
            int count = Math.Min(MaxFriends, SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate));
            if (count <= 0)
            {
                return Array.Empty<SteamFriend>();
            }

            var friends = new List<SteamFriend>(count);
            for (int i = 0; i < count; i++)
            {
                var id = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
                string name = SteamFriends.GetFriendPersonaName(id);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                friends.Add(new SteamFriend(name, SteamFriends.GetMediumFriendAvatar(id)));
            }

            return friends;
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            return Array.Empty<SteamFriend>();
        }
    }

    /// <summary>
    /// Vytáhne ze Steamu obrázek profilu jako RGBA.
    ///
    /// <para>Vrací syrová data, ne texturu: tahle vrstva nemá grafiku a neměla
    /// by ji mít. Texturu z toho udělá render.</para>
    /// </summary>
    public static bool TryReadAvatar(int handle, out int width, out int height, out byte[] rgba)
    {
        width = 0;
        height = 0;
        rgba = Array.Empty<byte>();

        if (handle <= 0)
        {
            return false;
        }

        try
        {
            if (!SteamUtils.GetImageSize(handle, out uint w, out uint h) || w == 0 || h == 0)
            {
                return false;
            }

            var buffer = new byte[w * h * 4];
            if (!SteamUtils.GetImageRGBA(handle, buffer, buffer.Length))
            {
                return false;
            }

            width = (int)w;
            height = (int)h;
            rgba = buffer;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or DllNotFoundException
                                          or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
