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

            var sprite = _sprites.Get($"building.{def.Id}");
            if (sprite is not null)
            {
                // Jemný stín pod budovou, ať „sedí" na terénu.
                spriteBatch.Draw(_pixel, new Rectangle(x + 2, y + height - 3, width - 2, 3), Color.Black * 0.25f);
                spriteBatch.Draw(sprite, new Rectangle(x, y, width, height), Color.White);
            }
            else
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), Color.Black * 0.6f);
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x + Inset, y + Inset, width - 2 * Inset, height - 2 * Inset),
                    def.MapColor.ToXna());
            }

            // Vyschlý vstup má být VIDĚT (fáze 3): stojící výroba dostane červený roh.
            if (def.Recipe is not null && building.Progress >= def.Recipe.TimeTicks - 0.001f)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + width - 7, y + 3, 4, 4), Color.Red);
            }
        }

        spriteBatch.End();
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
