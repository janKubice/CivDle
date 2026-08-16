using CivDle.Rendering;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Přelet nad jedním městečkem: pomalý pohyb kamery přes hotový výřez 40×40.
///
/// <para>Kamera se schválně sotva hýbe. Rychlý přejezd ukáže víc plochy, ale
/// divák z něj nic nepřečte — a tenhle záběr má jediný úkol: aby si člověk stihl
/// všimnout, že tamhle je náměstí, tady park a mezi domy jsou dvorky.</para>
///
/// <para>Simulace se posouvá <b>pevným krokem podle pořadí snímku</b>, takže
/// z komínů kouří a po ulicích chodí lidi stejně rychle bez ohledu na to, jak
/// dlouho se snímek renderoval.</para>
///
/// <para>Titulek nese jen první městečko z přehlídky; na ostatních by se ta samá
/// věta opakovala a přebíjela to, na co se má divák dívat.</para>
/// </summary>
internal sealed class CityShowcaseShot : ITrailerShot
{
    private const double DurationSeconds = 7.0;

    /// <summary>
    /// Přiblížení na začátku a na konci — jemné najetí, ne let.
    ///
    /// <para>Horní mez drží zadání „ať je vidět celé to čtyřicetkrát čtyřicet":
    /// při dlaždici 16 px se čtyřicet řádků vejde do výšky 1080 px až do
    /// přiblížení <c>1080 / (40 × 16) ≈ 1,69</c>. Nad tím už městečko leze
    /// z obrazu ven.</para>
    /// </summary>
    private const float StartZoom = 1.36f;
    private const float EndZoom = 1.62f;

    /// <summary>
    /// Se titulkem se couvne a městečko se posune nahoru, aby mu pruh s textem
    /// neseděl na spodní čtvrtině. Dřív ho zakrýval — a zakrývat to, kvůli čemu
    /// se záběr točí, je ta nejhloupější možná chyba.
    /// </summary>
    private const float CaptionZoomOut = 0.86f;

    /// <summary>O kolik návrhových pixelů se městečko zvedne nad pruh s titulkem.</summary>
    private const float CaptionLift = 96f;

    /// <summary>O kolik dlaždic se kamera za celý záběr posune.</summary>
    private const float DriftTiles = 3.2f;

    private const float FadeSeconds = 0.6f;

    private static readonly Color Gold = new(255, 226, 150);

    private readonly TrailerCanvas _canvas;
    private readonly ShowcaseTown _town;
    private readonly WorldScene _scene;
    private readonly Camera2D _camera = new();
    private readonly IDisposable _fullDetail;
    private readonly string? _caption;
    private readonly Vector2 _driftDirection;

    /// <param name="caption">Titulek přes záběr; <c>null</c> = jen město.</param>
    public CityShowcaseShot(
        TrailerCanvas canvas, ScreenManager screens, int index, long seed, string? caption)
    {
        _canvas = canvas;
        _caption = caption;
        Name = $"{index:D2}-mesto";

        _town = ShowcaseTown.Build(screens.Content, seed);
        _scene = new WorldScene(screens, screens.Content, _town.Simulation.Seed);

        // Bez tohohle by renderer při oddálení přepnul na LOD a z městečka by
        // byly barevné čtverečky — tedy přesně to, co záběr nemá ukazovat.
        _fullDetail = DetailLevel.FullDetail();

        _camera.SetViewport(canvas.Width, canvas.Height);

        // Každé městečko se veze jiným směrem, ať přehlídka nevypadá jako jeden
        // dlouhý pohyb rozstříhaný na kusy.
        float angle = index * 1.9f;
        _driftDirection = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f);
    }

    public string Name { get; }

    public int FrameCount => VideoTiming.FrameCount(DurationSeconds);

    public void DrawFrame(int frameIndex)
    {
        int ticks = VideoTiming.TicksBeforeFrame(frameIndex);
        for (int i = 0; i < ticks; i++)
        {
            _town.Simulation.Tick();
        }

        _scene.Update(1f / VideoTiming.Fps, _town.Simulation);

        float time = (float)VideoTiming.TimeOfFrame(frameIndex);
        float t = time / (float)DurationSeconds;

        float zoom = MathHelper.Lerp(StartZoom, EndZoom, Smooth(t));
        if (_caption is not null)
        {
            zoom *= CaptionZoomOut;
        }

        // Posun se dělí přiblížením: na obrazovce má být pořád stejný, ať je
        // kamera kdekoli.
        float lift = _caption is null ? 0f : CaptionLift / zoom;

        _camera.Position = _town.Center
            + new Vector2(0f, lift)
            + _driftDirection * (t - 0.5f) * DriftTiles * TerrainRenderer.TileSize;
        _camera.SetCaptureZoom(zoom * _canvas.Scale);

        _scene.Draw(_camera, _town.Simulation, new Viewport(0, 0, _canvas.Width, _canvas.Height));

        _canvas.Begin();
        _canvas.Vignette(0.55f);
        DrawCaption(time);
        _canvas.FadeToBlack(FadeAmount(time));
        _canvas.End();
    }

    public void Dispose()
    {
        _scene.Dispose();
        _fullDetail.Dispose();
    }

    /// <summary>
    /// Titulek dole na tmavém podkladu. Naběhne po vteřině a zmizí dřív než
    /// záběr — poslední vteřinu má mít divák jen město.
    /// </summary>
    private void DrawCaption(float time)
    {
        if (_caption is null)
        {
            return;
        }

        float appear = Math.Clamp((time - 0.9f) / 0.7f, 0f, 1f);
        float leave = Math.Clamp((float)(DurationSeconds - 1.4f - time) / 0.7f, 0f, 1f);
        float alpha = Math.Min(appear, leave);
        if (alpha <= 0.001f)
        {
            return;
        }

        const float bandTop = 858f;
        const float bandHeight = 132f;

        _canvas.Fill(0, bandTop, TrailerCanvas.DesignWidth, bandHeight, new Color(9, 12, 17) * (0.72f * alpha));
        _canvas.Fill(0, bandTop, TrailerCanvas.DesignWidth, 2, Gold * (0.45f * alpha));

        float fontScale = _canvas.FitScale(_caption, TrailerCanvas.DesignWidth - 260f, 3.4f);
        _canvas.DrawCentered(
            _caption, TrailerCanvas.DesignWidth / 2f, bandTop + 36f, fontScale, Gold * alpha);
    }

    /// <summary>Plynulý náběh i dojezd — lineární zoom je na videu vidět jako škubnutí.</summary>
    private static float Smooth(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float FadeAmount(float time)
    {
        float inAmount = 1f - Math.Clamp(time / FadeSeconds, 0f, 1f);
        float outAmount = 1f - Math.Clamp((float)(DurationSeconds - time) / FadeSeconds, 0f, 1f);
        return Math.Max(inAmount, outAmount);
    }
}
