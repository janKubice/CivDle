using CivDle.Core.Sim;
using CivDle.Rendering;
using CivDle.Screens;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Capture;

/// <summary>
/// Vyrobí kapsle do obchodu (header, library, ikonu…) ze <b>skutečné</b> herní
/// scény: stejný terén, stejné sprity budov, stejné silnice, jen bez HUD.
///
/// <para>Proč takhle a ne kreslenou grafikou: kapsle je slib. Když na ní bude
/// něco, co ve hře není, hráč to pozná na první screenshot — a naštve ho to
/// právem. Takhle je na obálce doslova ta hra.</para>
///
/// <para>Kreslí se do <see cref="RenderTarget2D"/> v přesném rozměru, který Steam
/// vyžaduje, takže nic nezávisí na velikosti okna.</para>
/// </summary>
public sealed class CapsuleDirector
{
    private const string Title = "CivDle";
    private const string Tagline = "An idle city that grows while you rest";

    private readonly string _outputDirectory;

    public CapsuleDirector(string outputDirectory) => _outputDirectory = outputDirectory;

    /// <summary>Vykreslí a uloží všechny kapsle. Volá se jednou, pak hra končí.</summary>
    public void RenderAll(ScreenManager screens, Simulation simulation)
    {
        var device = screens.GraphicsDevice;
        var content = screens.Content;
        long seed = simulation.Seed;

        var terrainRenderer = new TerrainRenderer(device, content.Biomes, seed);
        var decorations = new DecorationRenderer(screens.WhitePixel, content, seed);
        var roads = new RoadRenderer(screens.WhitePixel, content);
        var buildings = new BuildingRenderer(screens.WhitePixel, content, screens.Sprites);
        var harvestables = new HarvestableRenderer(screens.Sprites, content);
        var font = Stylesheet.Current.LabelStyle.Font;

        Directory.CreateDirectory(_outputDirectory);
        foreach (var spec in CapsuleSpec.All)
        {
            using var target = new RenderTarget2D(device, spec.Width, spec.Height);
            device.SetRenderTarget(target);
            device.Clear(new Color(18, 26, 30));

            DrawScene(screens, simulation, spec, terrainRenderer, decorations, roads, buildings, harvestables);
            DrawBranding(screens, spec, font);

            device.SetRenderTarget(null);

            string path = Path.Combine(_outputDirectory, spec.FileName + ".png");
            using var stream = File.Create(path);
            target.SaveAsPng(stream, spec.Width, spec.Height);
            Console.WriteLine($"kapsle: {path} ({spec.Width}×{spec.Height})");
        }
    }

    /// <summary>Vykreslí herní svět tak, aby vyplnil celou kapsli.</summary>
    private static void DrawScene(
        ScreenManager screens,
        Simulation simulation,
        CapsuleSpec spec,
        TerrainRenderer terrain,
        DecorationRenderer decorations,
        RoadRenderer roads,
        BuildingRenderer buildings,
        HarvestableRenderer harvestables)
    {
        var camera = new Camera2D();
        camera.SetViewport(spec.Width, spec.Height);

        // Zoom má dvě mantinely a mezi nimi je úzko:
        //   * moc blízko (bylo tu 3,0) → na kapsli je vidět třináct dlaždic
        //     a hlavní obrázek hry vypadá jako opakující se textura;
        //   * moc daleko → renderer přepne na LOD a budovy se kreslí jako
        //     čtverečky, takže z města zbydou barevné skvrny.
        // Proto pevný zoom nad prahem detailu (DetailLevel.Decorations = 1,25)
        // a víc města se ukáže samo tím, že je kapsle větší.
        // Rozhoduje ŠÍŘKA, ne kratší strana: header capsule je 460×215, takže
        // podle kratší strany spadla mezi ikony a dostala těsný zoom — a to je
        // druhý nejdůležitější obrázek na Steamu.
        float zoom = spec.Width < 250 ? 2.0f : 1.5f;
        camera.CenterOn(
            new Vector2(
                (simulation.CityCenterX + 0.5f) * TerrainRenderer.TileSize,
                (simulation.CityCenterY + 0.5f) * TerrainRenderer.TileSize),
            zoom);

        var spriteBatch = screens.SpriteBatch;
        terrain.Draw(spriteBatch, camera, simulation.Terrain);
        decorations.Draw(spriteBatch, camera, simulation.Terrain);
        harvestables.Draw(spriteBatch, camera, simulation);
        roads.Draw(spriteBatch, camera, simulation);
        buildings.Draw(spriteBatch, camera, simulation);
    }

    /// <summary>
    /// Ztmavení a název. Pruh pod textem je tam proto, aby byl název čitelný
    /// nad jakoukoli scénou — kapsle se čte v seznamu na dvě vteřiny.
    /// </summary>
    private static void DrawBranding(ScreenManager screens, CapsuleSpec spec, SpriteFontBase font)
    {
        var spriteBatch = screens.SpriteBatch;
        var pixel = screens.WhitePixel;

        spriteBatch.Begin();

        // Jemná viněta po okrajích, ať scéna nekončí ostrým řezem.
        int edge = Math.Max(2, Math.Min(spec.Width, spec.Height) / 12);
        spriteBatch.Draw(pixel, new Rectangle(0, 0, spec.Width, edge), new Color(10, 14, 18) * 0.45f);
        spriteBatch.Draw(pixel, new Rectangle(0, spec.Height - edge, spec.Width, edge), new Color(10, 14, 18) * 0.55f);

        if (spec.TitleScale > 0)
        {
            float scale = spec.TitleScale * Math.Min(spec.Width, spec.Height) / 215f;
            var size = font.MeasureString(Title) * scale;
            float x = (spec.Width - size.X) / 2f;
            float y = spec.Height * 0.5f - size.Y / 2f;

            int bandHeight = (int)(size.Y * (spec.ShowTagline ? 2.6f : 1.7f));
            int bandTop = (int)(y - size.Y * 0.4f);
            // Pruh pod názvem musí být dost tmavý, aby text držel i nad světlou
            // krajinou — na kapsli se nedá spoléhat na to, co je pod ním.
            spriteBatch.Draw(pixel, new Rectangle(0, bandTop, spec.Width, bandHeight), new Color(8, 12, 16) * 0.82f);

            spriteBatch.DrawString(font, Title, new Vector2(x + 2, y + 2), new Color(0, 0, 0, 160), scale: new Vector2(scale));
            spriteBatch.DrawString(font, Title, new Vector2(x, y), new Color(240, 226, 190), scale: new Vector2(scale));

            if (spec.ShowTagline)
            {
                float taglineScale = scale * 0.34f;
                var taglineSize = font.MeasureString(Tagline) * taglineScale;
                spriteBatch.DrawString(
                    font, Tagline,
                    new Vector2((spec.Width - taglineSize.X) / 2f, y + size.Y * 1.05f),
                    new Color(198, 206, 196),
                    scale: new Vector2(taglineScale));
            }
        }

        spriteBatch.End();
    }
}
