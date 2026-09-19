using CivDle.Core.Sim;
using CivDle.Rendering;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Vyrobí <b>podklady</b> pro kapsle do obchodu ze skutečné herní scény: stejný
/// terén, stejné sprity budov, stejné silnice, jen bez HUD.
///
/// <para>Proč takhle a ne kreslenou grafikou: kapsle je slib. Když na ní bude
/// něco, co ve hře není, hráč to pozná na první screenshot — a naštve ho to
/// právem. Takhle je na obálce doslova ta hra.</para>
///
/// <para><b>Logo tady není.</b> Dřív se přes scénu kreslil tmavý pruh s názvem
/// herním fontem — a vypadalo to jako screenshot s popiskem, ne jako obálka.
/// Značku skládá až <c>tools/make_store_assets.py</c>, které umí gradient,
/// obrys a emblém; tenhle kód dodává jen město pod ní.</para>
///
/// <para>Kreslí se do <see cref="RenderTarget2D"/> v přesném rozměru, takže nic
/// nezávisí na velikosti okna.</para>
/// </summary>
public sealed class CapsuleDirector
{
    private readonly string _outputDirectory;
    private readonly bool _second;

    /// <summary>
    /// </summary>
    /// <param name="outputDirectory">Kam se podklady uloží.</param>
    /// <param name="second">
    /// Druhá verze vzhledu? Základní kreslí město v poledne a bez světla —
    /// je čitelné, ale ploché, takže z hlavního obrázku hry je zelená textura.
    /// Druhá staví na tom nejhezčím, co hra umí: nízké slunce, voda s odlesky
    /// a první rozsvícená okna.
    /// </param>
    public CapsuleDirector(string outputDirectory, bool second = false)
    {
        _outputDirectory = outputDirectory;
        _second = second;
    }

    /// <summary>Vykreslí a uloží všechny kapsle. Volá se jednou, pak hra končí.</summary>
    public void RenderAll(ScreenManager screens, Simulation simulation)
    {
        var device = screens.GraphicsDevice;
        var content = screens.Content;
        long seed = simulation.Seed;

        var terrainRenderer = new TerrainRenderer(device, content.Biomes, seed);
        var decorations = new DecorationRenderer(screens.WhitePixel, content, seed, screens.Sprites);
        var roads = new RoadRenderer(screens.WhitePixel, content);
        var buildings = new BuildingRenderer(screens.WhitePixel, content, screens.Sprites, screens.SoftShadow);
        var harvestables = new HarvestableRenderer(screens.Sprites, content);
        var water = new WaterRenderer(screens.WhitePixel);
        var lights = new LightsRenderer(screens.WhitePixel, content, screens.Sprites);

        Directory.CreateDirectory(_outputDirectory);
        foreach (var spec in CapsuleSpec.All)
        {
            using var target = new RenderTarget2D(device, spec.Width, spec.Height);
            device.SetRenderTarget(target);
            device.Clear(new Color(18, 26, 30));

            DrawScene(
                screens, simulation, spec, terrainRenderer, decorations, roads, buildings,
                harvestables, _second ? water : null, _second ? lights : null);

            if (_second)
            {
                // Světlo se vnáší násobením, ne závojem: násobení tmavá místa
                // ztmaví víc než světlá, takže kontrast roste. Průhledný
                // obdélník přes scénu ho naopak plošně dusí a obraz zmléční —
                // přesně to dělalo z kapsle placatou zelenou textturu.
                var light = DayNightCycle.LightColor(
                    simulation.TimeOfDay01, content.Gameplay.DayNight, simulation.CurrentSeason);

                DayNightCycle.DrawLight(
                    screens.SpriteBatch, screens.WhitePixel,
                    new Viewport(0, 0, spec.Width, spec.Height), light);
            }

            DrawEdgeShade(screens, spec);

            device.SetRenderTarget(null);

            string path = Path.Combine(_outputDirectory, spec.FileName + ".png");
            using var stream = File.Create(path);
            target.SaveAsPng(stream, spec.Width, spec.Height);
            Console.WriteLine($"podklad: {path} ({spec.Width}×{spec.Height})");
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
        HarvestableRenderer harvestables,
        WaterRenderer? water,
        LightsRenderer? lights)
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

        // Voda až za terénem: pěna u břehu a odlesky na hladině jsou to
        // nejhezčí, co hra kreslí, a na ploché kapsli z nich nebylo nic.
        water?.Draw(spriteBatch, camera, simulation);

        decorations.Draw(spriteBatch, camera, simulation.Terrain, simulation);
        harvestables.Draw(spriteBatch, camera, simulation);
        roads.Draw(spriteBatch, camera, simulation);
        buildings.Draw(spriteBatch, camera, simulation);

        // Okna se rozsvěcí podle noci. V podvečer je to pár teplých teček
        // mezi střechami — právě ony dělají z města místo, kde někdo bydlí.
        lights?.Draw(
            spriteBatch, camera, simulation,
            DayNightCycle.NightFactor(simulation.TimeOfDay01));
    }

    /// <summary>
    /// Jemné ztmavení nahoře a dole, ať scéna nekončí ostrým řezem a ať má
    /// logo nad čím sedět. Víc podklad nedělá — zbytek je práce kompozitoru.
    /// </summary>
    private static void DrawEdgeShade(ScreenManager screens, CapsuleSpec spec)
    {
        var spriteBatch = screens.SpriteBatch;
        var pixel = screens.WhitePixel;

        spriteBatch.Begin();

        // Po pruzích, ne jedním obdélníkem: ostrá hrana ztmavení je na kapsli
        // vidět víc než samotné ztmavení.
        int edge = Math.Max(4, Math.Min(spec.Width, spec.Height) / 6);
        for (int i = 0; i < edge; i++)
        {
            float t = 1f - (i / (float)edge);
            spriteBatch.Draw(pixel, new Rectangle(0, i, spec.Width, 1), new Color(8, 12, 18) * (0.42f * t));
            spriteBatch.Draw(pixel, new Rectangle(0, spec.Height - 1 - i, spec.Width, 1), new Color(8, 12, 18) * (0.55f * t));
        }

        spriteBatch.End();
    }
}
