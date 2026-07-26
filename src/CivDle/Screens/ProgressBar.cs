using Microsoft.Xna.Framework;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Jednoduchý ukazatel pokroku: tmavá dráha a barevná výplň. Číslo „12 / 40" samo
/// o sobě nenese, jak daleko hráč je — pruh to ukáže na první pohled (feedback:
/// „questy jsou nepřehledné").
///
/// <para>Vlastní widget místo Myra <c>HorizontalProgressBar</c>, protože potřebujeme
/// vlastní barvy (pokrok mění odstín podle blízkosti cíle) a pevnou šířku v HUD.</para>
/// </summary>
internal sealed class ProgressBar
{
    private static readonly Color TrackColor = new(32, 40, 54, 220);
    private static readonly Color FarColor = new(90, 150, 200);
    private static readonly Color NearColor = new(150, 220, 150);

    private readonly int _width;
    private readonly Panel _fill;

    public ProgressBar(int width, int height = 6)
    {
        _width = width;
        _fill = new Panel
        {
            Width = 0,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidBrush(FarColor),
        };

        var track = new Panel
        {
            Width = width,
            Height = height,
            Background = new SolidBrush(TrackColor),
        };
        track.Widgets.Add(_fill);
        Root = track;
    }

    /// <summary>Widget k vložení do rozvržení.</summary>
    public Panel Root { get; }

    /// <summary>Nastaví pokrok 0–1; blízko cíle přejde pruh do zelené („už jen kousek").</summary>
    public void SetProgress(double fraction)
    {
        fraction = Math.Clamp(fraction, 0.0, 1.0);
        _fill.Width = (int)Math.Round(fraction * _width);
        _fill.Background = new SolidBrush(fraction >= 0.75 ? NearColor : FarColor);
    }
}
