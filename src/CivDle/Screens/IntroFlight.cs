using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Úvodní nálet kamery na novou hru: z výšky a kus stranou dolů k táboráku,
/// kde město začne. Tři vteřiny, které řeknou „tohle je svět a tady je tvoje
/// místo v něm" dřív, než hráč dostane první úkol.
///
/// <para>Dřív hra začínala rovnou zblízka nad kusem trávy — bez měřítka, bez
/// kontextu a bez pocitu, že se něco začíná. Nálet jde kdykoli přerušit
/// kliknutím, klávesou nebo kolečkem: kdo chce hrát, nemá na nic čekat.</para>
///
/// <para>Čistá matematika bez grafiky — obrazovka jen každý snímek přečte
/// polohu a přiblížení a předá je kameře.</para>
/// </summary>
internal sealed class IntroFlight
{
    private readonly Vector2 _from;
    private readonly Vector2 _to;
    private readonly float _fromZoom;
    private readonly float _toZoom;
    private readonly float _seconds;
    private float _elapsed;

    /// <param name="target">Kam nálet končí (táborák).</param>
    /// <param name="startOffset">O kolik stranou začíná (ve světových souřadnicích).</param>
    /// <param name="fromZoom">Přiblížení na začátku (z výšky).</param>
    /// <param name="toZoom">Přiblížení na konci (hratelný pohled).</param>
    /// <param name="seconds">Délka náletu.</param>
    public IntroFlight(Vector2 target, Vector2 startOffset, float fromZoom, float toZoom, float seconds)
    {
        _to = target;
        _from = target + startOffset;
        _fromZoom = fromZoom;
        _toZoom = toZoom;
        _seconds = Math.Max(0.01f, seconds);
    }

    /// <summary>Doletěla kamera (nebo hráč nálet přerušil)?</summary>
    public bool IsDone => _elapsed >= _seconds;

    /// <summary>Kde je kamera teď.</summary>
    public Vector2 Position => Vector2.Lerp(_from, _to, Ease(Progress));

    /// <summary>
    /// Jak blízko je kamera teď. Přibližuje se geometricky, ne lineárně:
    /// lineární zoom z 0,6 na 2,2 by první polovinu „stál" a pak prudce skočil.
    /// </summary>
    public float Zoom => _fromZoom * MathF.Pow(_toZoom / _fromZoom, Ease(Progress));

    private float Progress => Math.Clamp(_elapsed / _seconds, 0f, 1f);

    /// <summary>Posune nálet o <paramref name="dt"/> sekund.</summary>
    public void Update(float dt) => _elapsed += dt;

    /// <summary>Doletí hned (hráč převzal kameru).</summary>
    public void Finish() => _elapsed = _seconds;

    /// <summary>Plynulý rozjezd i dojezd — kamera nemá začít ani skončit trhnutím.</summary>
    private static float Ease(float t) => t * t * (3f - 2f * t);
}
