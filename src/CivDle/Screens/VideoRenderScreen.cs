using System.Diagnostics;
using CivDle.Capture;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Obrazovka, která dorenderuje natočený záběr do sekvence snímků.
///
/// <para>Renderuje se po kouscích s rozpočtem na snímek, ne v jedné smyčce.
/// Jeden 4K snímek bez LOD trvá klidně půl vteřiny a tisícovka takových by
/// okno na deset minut zmrazila — přesně ta chyba, kvůli které padalo
/// Continue. Takhle je vidět postup, jde to přerušit a systém hru neoznačí za
/// nereagující.</para>
/// </summary>
public sealed class VideoRenderScreen : IScreen
{
    /// <summary>
    /// Kolik milisekund snímku smí ukrojit render. Zbytek patří překreslení
    /// a vstupu. Víc než polovina snímku by z ukazatele udělala trhanou
    /// prezentaci.
    /// </summary>
    private const double MillisPerFrame = 8.0;

    private readonly ScreenManager _screens;
    private readonly VideoRender _render;
    private readonly Desktop _desktop;
    private readonly ProgressBar _bar;
    private readonly Label _status;
    private readonly Stopwatch _clock = new();

    private bool _finished;

    public VideoRenderScreen(ScreenManager screens, VideoRender render)
    {
        _screens = screens;
        _render = render;

        _status = new Label
        {
            Text = screens.Loc["video.rendering"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        _bar = new ProgressBar(460, height: 14);

        var layout = new VerticalStackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(_status);
        layout.Widgets.Add(_bar.Root);
        layout.Widgets.Add(UiFactory.SmallButton(screens.Loc["video.cancel"], _render.Cancel));

        _desktop = new Desktop { Root = layout };
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
        if (_finished)
        {
            return;
        }

        _clock.Restart();
        while (!_render.IsDone && _clock.Elapsed.TotalMilliseconds < MillisPerFrame)
        {
            _render.RenderNextFrame();
        }

        _bar.SetProgress(_render.Progress);
        _status.Text = _screens.Loc.Format(
            "video.progress", _render.DoneFrames, _render.TotalFrames, (int)(_render.Progress * 100));

        if (!_render.IsDone)
        {
            return;
        }

        _finished = true;

        // Cesta i příkaz do konzole: bez nich by autor našel složku plnou PNG
        // a nevěděl, co s nimi. Tohle je poslední krok, který hra udělat může —
        // kódovat video by znamenalo přibalit kodek.
        Console.WriteLine($"video: {_render.DoneFrames} snímků v {_render.Directory}");
        Console.WriteLine(_render.FfmpegCommand);

        _render.Dispose();
        _screens.Pop();
    }

    public void Draw(GameTime gameTime)
    {
        _screens.GraphicsDevice.Clear(new Color(12, 15, 20));
        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose() => _render.Dispose();
}
