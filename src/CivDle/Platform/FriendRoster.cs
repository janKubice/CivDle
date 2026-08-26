using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Platform;

/// <summary>Kamarád i s hotovým obrázkem, připravený ke kreslení.</summary>
/// <param name="Name">Jak se mu píše v přátelích.</param>
/// <param name="Avatar">Obrázek profilu, nebo <c>null</c>, když ho nemá.</param>
public sealed record FriendFace(string Name, Texture2D? Avatar);

/// <summary>
/// Kamarádi ze Steamu připravení pro hru — jména a hotové textury.
///
/// <para><b>Proč vlastní třída:</b> <see cref="SteamFriendsSource"/> schválně
/// nezná grafiku (vrací syrová data), ale karavana potřebuje texturu. Tohle je
/// to jediné místo, kde se z bajtů stane obrázek — a kde se drží, aby se
/// nevyráběl znovu při každé karavaně.</para>
///
/// <para><b>Bez Steamu je seznam prázdný</b> a všechno ostatní jede dál:
/// karavany jezdí, jen bez jmen. To není chyba, to je normální stav pro
/// každého, kdo hru spustí napřímo.</para>
/// </summary>
public sealed class FriendRoster : IDisposable
{
    private readonly List<FriendFace> _friends = new();
    private bool _loaded;
    private bool _disposed;

    /// <summary>Kolik kamarádů se povedlo načíst.</summary>
    public int Count => _friends.Count;

    /// <summary>Má hra vůbec koho ukázat?</summary>
    public bool HasFriends => _friends.Count > 0;

    /// <summary>
    /// Načte přátele — jednou za spuštění.
    ///
    /// <para>Ne při každé karavaně: seznam se během hraní nemění tak, aby to
    /// stálo za dotaz do Steamu a výrobu textur každou minutu a půl.</para>
    /// </summary>
    public void LoadOnce(SteamFriendsSource source, GraphicsDevice device)
    {
        if (_loaded || _disposed)
        {
            return;
        }

        _loaded = true;
        foreach (var friend in source.Load())
        {
            _friends.Add(new FriendFace(friend.Name, TryMakeAvatar(friend.AvatarHandle, device)));
        }
    }

    /// <summary>
    /// Kamarád pro danou jízdu. Vybírá se deterministicky z klíče, ne náhodně —
    /// jedna karavana má patřit jednomu člověku po celou cestu, ne blikat mezi
    /// jmény při každém snímku.
    /// </summary>
    public FriendFace? Pick(long key)
    {
        if (_friends.Count == 0)
        {
            return null;
        }

        ulong hash = (ulong)key * 0x9E3779B97F4A7C15UL;
        hash ^= hash >> 29;
        return _friends[(int)(hash % (ulong)_friends.Count)];
    }

    private static Texture2D? TryMakeAvatar(int handle, GraphicsDevice device)
    {
        if (!SteamFriendsSource.TryReadAvatar(handle, out int width, out int height, out byte[] rgba))
        {
            return null;
        }

        // Steam vrací RGBA po bajtech; MonoGame chce Color. Předělá se to jednou
        // při načtení, ne při kreslení.
        var pixels = new Microsoft.Xna.Framework.Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            int at = i * 4;
            pixels[i] = new Microsoft.Xna.Framework.Color(rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]);
        }

        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var friend in _friends)
        {
            friend.Avatar?.Dispose();
        }

        _friends.Clear();
    }
}
