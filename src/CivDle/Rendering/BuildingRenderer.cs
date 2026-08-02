using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení budov a ghost náhledu umisťování. Budova se kreslí spritem
/// (klíč <c>building.&lt;id&gt;</c> z <see cref="SpriteLibrary"/>); bez spritu se
/// vrátí k barevnému obdélníku z definice. Culling podle viditelného výřezu.
/// Čte jen ze simulace, nikdy do ní nezapisuje.
/// </summary>
public sealed class BuildingRenderer
{
    private const int Inset = 2;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;

    public BuildingRenderer(Texture2D whitePixel, GameContent content, SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _content = content;
        _sprites = sprites;
    }

    /// <summary>Vykreslí všechny viditelné budovy.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();

        // Zjednodušený režim: při oddálení jsou budovy pár pixelů velké, takže
        // sprite, stín ani odznaky nejsou k rozeznání — a přitom stojí tři kresby
        // na budovu místo jedné. Tvar města zůstane čitelný z barev.
        bool detailed = camera.Zoom >= DetailLevel.BuildingSprites;

        // Prosperita se propisuje do obrazu jen zblízka: z výšky by z nádechu
        // zbyl jeden pixel a stálo by to dva dotazy na mřížku u každé budovy.
        bool showsProsperity = detailed;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        var buildings = simulation.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];

            int x = building.X * tileSize;
            int y = building.Y * tileSize;
            int width = def.FootprintWidth * tileSize;
            int height = def.FootprintHeight * tileSize;
            if (x + width < min.X || x > max.X || y + height < min.Y || y > max.Y)
            {
                continue;
            }

            if (!detailed)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), def.MapColor.ToXna());
                continue;
            }

            // Staveniště (divy světa): budova je vidět jen zpola vztyčená a nad ní
            // roste pruh postupu. Bez toho by rozestavěný div vypadal jako hotový,
            // který se z neznámého důvodu fláká.
            if (!building.IsComplete)
            {
                DrawConstructionSite(spriteBatch, def, x, y, width, height, simulation.ConstructionProgress01(i));
                continue;
            }

            // Jak se tomuhle místu daří — z toho se odvodí nádech i ozdoby.
            // Prosperita je vlastnost světa; render ji jen zobrazuje.
            double prosperity = showsProsperity ? simulation.ProsperityAt(building.X, building.Y) : 1.0;
            var tint = ProsperityLook.Tint(prosperity);

            var sprite = _sprites.Get($"building.{def.Id}");
            if (sprite is not null)
            {
                // Jemný stín pod budovou, ať „sedí" na terénu.
                spriteBatch.Draw(_pixel, new Rectangle(x + 2, y + height - 3, width - 2, 3), Color.Black * 0.25f);
                spriteBatch.Draw(sprite, new Rectangle(x, y, width, height), tint);
            }
            else
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), Color.Black * 0.6f);
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x + Inset, y + Inset, width - 2 * Inset, height - 2 * Inset),
                    ProsperityLook.Modulate(def.MapColor.ToXna(), tint));
            }

            if (showsProsperity)
            {
                DrawProsperityDetail(spriteBatch, i, prosperity, x, y, width, height);
            }

            // Stojící budova má být VIDĚT — a hlavně má být poznat PROČ. Jeden
            // červený roh pro všechno znamenal, že hráč viděl „něco je špatně"
            // a musel hádat; barva teď důvod rozliší a bublina ho pojmenuje.
            DrawStallBadge(spriteBatch, building.Stall, x, y, width);
        }

        spriteBatch.End();
    }

    /// <summary>Barva odznaku podle důvodu, proč budova stojí.</summary>
    public static Color StallColor(BuildingStall stall) => stall switch
    {
        BuildingStall.NoWorkers => new Color(255, 190, 70),   // oranžová = chybí lidi
        BuildingStall.MissingInput => new Color(240, 90, 80), // červená = chybí surovina
        BuildingStall.NoTerrain => new Color(150, 110, 220),  // fialová = došlo okolí
        _ => Color.Transparent,
    };

    /// <summary>
    /// Odznak v rohu budovy. Rozestavěná budova ho nedostane — u té je vidět
    /// lešení i pruh postupu, druhá cedule by jen šuměla.
    /// </summary>
    private void DrawStallBadge(SpriteBatch spriteBatch, BuildingStall stall, int x, int y, int width)
    {
        var color = StallColor(stall);
        if (color == Color.Transparent)
        {
            return;
        }

        spriteBatch.Draw(_pixel, new Rectangle(x + width - 8, y + 2, 6, 6), Color.Black * 0.5f);
        spriteBatch.Draw(_pixel, new Rectangle(x + width - 7, y + 3, 4, 4), color);
    }

    /// <summary>
    /// Drobnost, ze které je poznat, jak se domu vede: kvetoucí dům dostane
    /// truhlík pod okny, zašlý šmouhu od kouře.
    ///
    /// <para>Schválně jen pár pixelů — je to periferní signál, ne odznak.
    /// Hráč si má všimnout, že ulice zezelenala, ne číst ikonky.</para>
    /// </summary>
    private void DrawProsperityDetail(
        SpriteBatch spriteBatch, int buildingIndex, double prosperity, int x, int y, int width, int height)
    {
        if (ProsperityLook.HasOrnament(prosperity))
        {
            // Truhlík na parapetu: vodorovný proužek u spodní hrany.
            int boxWidth = Math.Max(2, width / 3);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(x + width / 2 - boxWidth / 2, y + height - 5, boxWidth, 2),
                ProsperityLook.OrnamentColor(buildingIndex));
            return;
        }

        if (ProsperityLook.HasGrime(prosperity))
        {
            // Šmouha po zdi: svislý tmavý pruh od střechy dolů.
            spriteBatch.Draw(
                _pixel,
                new Rectangle(x + width / 4, y + 2, 1, Math.Max(2, height / 2)),
                new Color(40, 38, 34) * 0.35f);
        }
    }

    /// <summary>
    /// Staveniště: budova vyrůstá zdola nahoru podle postupu, nahoře pruh
    /// s procenty. Roste zdola schválně — je to čitelné i na malém zoomu,
    /// kdy pruh splývá.
    /// </summary>
    private void DrawConstructionSite(
        SpriteBatch spriteBatch, BuildingDef def, int x, int y, int width, int height, double progress)
    {
        spriteBatch.Draw(_pixel, new Rectangle(x + 2, y + height - 3, width - 2, 3), Color.Black * 0.25f);

        // Základy: obrys rozestavěné budovy, ať je vidět, kolik místa zabere.
        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), new Color(60, 55, 45) * 0.45f);

        int risen = Math.Max(1, (int)(height * progress));
        var sprite = _sprites.Get($"building.{def.Id}");
        var partial = new Rectangle(x, y + height - risen, width, risen);
        if (sprite is not null)
        {
            // Výřez spodní části spritu — budova doslova roste ze země.
            var source = new Rectangle(
                0, sprite.Height - Math.Max(1, (int)(sprite.Height * progress)),
                sprite.Width, Math.Max(1, (int)(sprite.Height * progress)));
            spriteBatch.Draw(sprite, partial, source, Color.White * 0.85f);
        }
        else
        {
            spriteBatch.Draw(_pixel, partial, def.MapColor.ToXna() * 0.85f);
        }

        // Lešení: dvě vodorovné linky přes celý půdorys.
        var scaffold = new Color(220, 190, 120) * 0.8f;
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height / 3, width, 1), scaffold);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + 2 * height / 3, width, 1), scaffold);

        // Pruh postupu nad staveništěm.
        const int barHeight = 3;
        int barY = y - barHeight - 2;
        spriteBatch.Draw(_pixel, new Rectangle(x, barY, width, barHeight), Color.Black * 0.55f);
        spriteBatch.Draw(_pixel,
            new Rectangle(x, barY, Math.Max(1, (int)(width * progress)), barHeight),
            new Color(240, 200, 90));
    }

    /// <summary>Poloprůhledný náhled budovy pod kurzorem — zelenkavý lze / červený nelze.</summary>
    public void DrawGhost(SpriteBatch spriteBatch, Camera2D camera, BuildingDef def, int tileX, int tileY, bool canPlace)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int x = tileX * tileSize;
        int y = tileY * tileSize;
        int width = def.FootprintWidth * tileSize;
        int height = def.FootprintHeight * tileSize;

        var tint = canPlace ? Color.White * 0.65f : Color.Red * 0.55f;
        var frame = canPlace ? new Color(120, 240, 140) : new Color(240, 110, 100);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        var sprite = _sprites.Get($"building.{def.Id}");
        if (sprite is not null)
        {
            spriteBatch.Draw(sprite, new Rectangle(x, y, width, height), tint);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), tint);
        }

        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height - 2, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, height), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x + width - 2, y, 2, height), frame);
        spriteBatch.End();
    }
}
