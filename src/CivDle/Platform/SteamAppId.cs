namespace CivDle.Platform;

/// <summary>
/// App ID hry na Steamu.
///
/// <para>Na jednom místě schválně: číslo se objevuje v <c>steam_appid.txt</c>,
/// při publikaci do Workshopu a při startu Steamu. Rozepsané na třech místech
/// by se dřív nebo později rozešlo — a to se pozná až tím, že se mod publikuje
/// pod cizí hru.</para>
///
/// <para>Demoverze má na Steamu <b>vlastní App ID</b>; až bude, přibude sem
/// vedle tohohle a vybere se podle <c>Edition.IsDemo</c>.</para>
/// </summary>
public static class SteamAppId
{
    /// <summary>Plná verze CivDle.</summary>
    public const uint CivDle = 5045220;
}
