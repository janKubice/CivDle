using CivDle.Core.Sim;
using CivDle.Rendering;
using CivDle.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Sdílitelný okamžik: obrázek města s proužkem čísel dole — přesně to, co jde
/// hodit kamarádovi nebo na fórum.
///
/// <para>Proč to ve hře je: hráč po hodinách staví něco, na co je pyšný, a jediná
/// cesta, jak to ukázat, je systémový screenshot i s HUD, tlačítky a rozdělaným
/// menu. Karta udělá obrázek, který dává smysl i tomu, kdo hru nezná: vidí město
/// <b>a</b> ví, kolik v něm žije lidí a jak dlouho na něm hráč dělal.</para>
///
/// <para>Vrstva: kreslí do <see cref="RenderTarget2D"/> vlastní kamerou, takže
/// výsledek nezávisí na velikosti okna ani na tom, kam se hráč zrovna dívá —
/// karta má vždycky stejný tvar.</para>
/// </summary>
public sealed class ShareCard
{
    private readonly ScreenManager _screens;

    public ShareCard(ScreenManager screens) => _screens = screens;

    /// <summary>
    /// Vyrobí kartu z aktuálního stavu hry a uloží ji do složky profilu.
    /// Vrací cestu k souboru, aby ji šlo ukázat hráči — bez ní by obrázek
    /// vznikl někde, kde ho nikdo nenajde.
    /// </summary>
    public string Save(
        Simulation simulation, Camera2D sourceCamera, string directory, ShareCardOptions options)
    {
        using var detail = options.FullDetail ? Rendering.DetailLevel.FullDetail() : null;

        var device = _screens.GraphicsDevice;
        using var scene = new WorldScene(_screens, _screens.Content, simulation.Seed);

        // Jednorázový snímek: vrstvy, které si stav dopočítávají za běhu (zpevněná
        // zem), musí dostat aspoň jeden takt, jinak by na fotce chyběly.
        scene.Update(0f, simulation);

        using var target = new RenderTarget2D(device, options.Width, options.Height);
        device.SetRenderTarget(target);
        device.Clear(new Color(16, 22, 28));

        DrawScene(scene, simulation, sourceCamera, options);
        if (options.WithStrip)
        {
            DrawStrip(simulation, options);
        }

        device.SetRenderTarget(null);

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"civdle-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var stream = File.Create(path);
        target.SaveAsPng(stream, options.Width, options.Height);
        return path;
    }

    /// <summary>
    /// Vykreslí město tak, jak se na něj hráč dívá — jen v pevném poměru stran
    /// karty a bez HUD.
    ///
    /// <para>Zoom se přepočítá podle výšky výřezu, aby zůstal <b>stejný záběr</b>
    /// a přibyly jen pixely. Kdyby se převzal beze změny, byla by fotka ve 4K
    /// „víc světa" místo ostřejšího obrázku téhož místa.</para>
    /// </summary>
    private void DrawScene(
        WorldScene scene, Simulation simulation, Camera2D sourceCamera, ShareCardOptions options)
    {
        var viewport = new Viewport(0, 0, options.Width, options.SceneHeight);
        var camera = new Camera2D();
        camera.SetViewport(options.Width, options.SceneHeight);
        camera.Position = sourceCamera.Position;
        camera.SetCaptureZoom(options.ZoomFor(sourceCamera.Zoom, _screens.GraphicsDevice.Viewport.Height));

        scene.Draw(camera, simulation, viewport);
    }

    /// <summary>
    /// Proužek dole: jméno města, obyvatelé, budovy, měřítko a doba běhu.
    /// Tohle je ta půlka, kvůli které obrázek dává smysl i cizímu člověku.
    /// </summary>
    private void DrawStrip(Simulation simulation, ShareCardOptions options)
    {
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var font = Myra.Graphics2D.UI.Styles.Stylesheet.Current.LabelStyle.Font;
        var loc = _screens.Loc;

        // Všechno v proužku se škáluje s výškou obrázku. Font má pevnou velikost
        // v pixelech, takže by ve 4K byl text čtvrtinový a proužek by vypadal
        // jako omylem oříznutý.
        float scale = options.Scale;
        int strip = options.StripHeight;
        int top = options.Height - strip;
        int pad = (int)(32 * scale);

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, top, options.Width, strip), new Color(14, 18, 24) * 0.94f);
        spriteBatch.Draw(pixel, new Rectangle(0, top, options.Width, Math.Max(2, (int)(2 * scale))), new Color(255, 226, 150));

        var names = _screens.Content.SettlementNames;
        string headline = simulation.Settlements.Count > 0
            ? names[simulation.Settlements[0].NameIndex]
            : loc["share.unnamed"];
        DrawScaled(font, headline, new Vector2(pad, top + 18 * scale), new Color(255, 226, 150), scale);

        string numbers = loc.Format(
            "share.numbers",
            CivDle.Core.Numbers.Format(simulation.Population),
            simulation.Buildings.Length,
            simulation.AscensionLevel,
            DurationFormat.Human(simulation.TickCount / (double)Simulation.TicksPerSecond));
        DrawScaled(font, numbers, new Vector2(pad, top + 52 * scale), new Color(215, 220, 228), scale);

        // Podpis vpravo, ať je z obrázku poznat, odkud je.
        var brand = font.MeasureString("CivDle") * scale;
        DrawScaled(
            font, "CivDle", new Vector2(options.Width - pad - brand.X, top + 34 * scale),
            new Color(140, 150, 165), scale);

        spriteBatch.End();
    }

    private void DrawScaled(FontStashSharp.SpriteFontBase font, string text, Vector2 at, Color color, float scale) =>
        _screens.SpriteBatch.DrawString(font, text, at, color, scale: new Vector2(scale, scale));
}
