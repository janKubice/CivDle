using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení budov a ghost náhledu umisťování. MVP vizuál: barevný čtverec
/// s tmavým okrajem (barvy z JSON definic); s culling podle viditelného výřezu.
/// Čte jen ze simulace, nikdy do ní nezapisuje.
/// </summary>
public sealed class BuildingRenderer
{
    private const int Inset = 2;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    public BuildingRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Vykreslí všechny viditelné budovy.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        const int tileSize = MapRenderer.TileSize;
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

            spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), Color.Black * 0.6f);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(x + Inset, y + Inset, width - 2 * Inset, height - 2 * Inset),
                def.MapColor.ToXna());

            // Vyschlý vstup má být VIDĚT (fáze 3): stojící výroba dostane červený roh.
            // Progress == TimeTicks je přesně stav „cyklus hotový, čeká na vstupy".
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
        const int tileSize = MapRenderer.TileSize;
        var fill = canPlace ? def.MapColor.ToXna() * 0.6f : Color.Red * 0.45f;
        var frame = canPlace ? Color.White * 0.8f : Color.Red * 0.9f;

        int x = tileX * tileSize;
        int y = tileY * tileSize;
        int width = def.FootprintWidth * tileSize;
        int height = def.FootprintHeight * tileSize;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), fill);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height - 2, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, height), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x + width - 2, y, 2, height), frame);
        spriteBatch.End();
    }
}
