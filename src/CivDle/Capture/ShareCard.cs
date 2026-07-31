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
    /// <summary>Rozměr obrázku. Široký formát, protože se sdílí do příspěvků.</summary>
    public const int Width = 1600;

    /// <summary>Výška obrázku včetně proužku s čísly.</summary>
    public const int Height = 900;

    /// <summary>Výška proužku s čísly dole.</summary>
    private const int StripHeight = 96;

    private readonly ScreenManager _screens;

    public ShareCard(ScreenManager screens) => _screens = screens;

    /// <summary>
    /// Vyrobí kartu z aktuálního stavu hry a uloží ji do složky profilu.
    /// Vrací cestu k souboru, aby ji šlo ukázat hráči — bez ní by obrázek
    /// vznikl někde, kde ho nikdo nenajde.
    /// </summary>
    public string Save(Simulation simulation, Camera2D sourceCamera, string directory)
    {
        var device = _screens.GraphicsDevice;
        var content = _screens.Content;

        var terrain = new TerrainRenderer(device, content.Biomes, simulation.Seed);
        var decorations = new DecorationRenderer(_screens.WhitePixel, content, simulation.Seed);
        var roads = new RoadRenderer(_screens.WhitePixel, content);
        var buildings = new BuildingRenderer(_screens.WhitePixel, content, _screens.Sprites);

        using var target = new RenderTarget2D(device, Width, Height);
        device.SetRenderTarget(target);
        device.Clear(new Color(16, 22, 28));

        DrawScene(simulation, sourceCamera, terrain, decorations, roads, buildings);
        DrawStrip(simulation);

        device.SetRenderTarget(null);
        terrain.Dispose();

        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"civdle-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        using var stream = File.Create(path);
        target.SaveAsPng(stream, Width, Height);
        return path;
    }

    /// <summary>
    /// Vykreslí město tak, jak se na něj hráč dívá — jen v pevném poměru stran
    /// karty a bez HUD.
    /// </summary>
    private void DrawScene(
        Simulation simulation,
        Camera2D sourceCamera,
        TerrainRenderer terrain,
        DecorationRenderer decorations,
        RoadRenderer roads,
        BuildingRenderer buildings)
    {
        var camera = new Camera2D();
        camera.SetViewport(Width, Height - StripHeight);
        camera.Position = sourceCamera.Position;
        camera.SetZoom(sourceCamera.Zoom);

        var spriteBatch = _screens.SpriteBatch;
        terrain.Draw(spriteBatch, camera, simulation.Terrain);
        decorations.Draw(spriteBatch, camera, simulation.Terrain);
        roads.Draw(spriteBatch, camera, simulation);
        buildings.Draw(spriteBatch, camera, simulation);
    }

    /// <summary>
    /// Proužek dole: jméno města, obyvatelé, budovy, měřítko a doba běhu.
    /// Tohle je ta půlka, kvůli které obrázek dává smysl i cizímu člověku.
    /// </summary>
    private void DrawStrip(Simulation simulation)
    {
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var font = Myra.Graphics2D.UI.Styles.Stylesheet.Current.LabelStyle.Font;
        var loc = _screens.Loc;

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, Height - StripHeight, Width, StripHeight), new Color(14, 18, 24) * 0.94f);
        spriteBatch.Draw(pixel, new Rectangle(0, Height - StripHeight, Width, 2), new Color(255, 226, 150));

        var names = _screens.Content.SettlementNames;
        string headline = simulation.Settlements.Count > 0
            ? names[simulation.Settlements[0].NameIndex]
            : loc["share.unnamed"];
        spriteBatch.DrawString(font, headline, new Vector2(32, Height - StripHeight + 18), new Color(255, 226, 150));

        string numbers = loc.Format(
            "share.numbers",
            CivDle.Core.Numbers.Format(simulation.Population),
            simulation.Buildings.Length,
            simulation.AscensionLevel,
            DurationFormat.Human(simulation.TickCount / (double)Simulation.TicksPerSecond));
        spriteBatch.DrawString(font, numbers, new Vector2(32, Height - StripHeight + 52), new Color(215, 220, 228));

        // Podpis vpravo, ať je z obrázku poznat, odkud je.
        var brand = font.MeasureString("CivDle");
        spriteBatch.DrawString(
            font, "CivDle", new Vector2(Width - 32 - brand.X, Height - StripHeight + 34), new Color(140, 150, 165));

        spriteBatch.End();
    }
}
