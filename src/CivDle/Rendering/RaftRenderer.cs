using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Klády plující po řekách.
///
/// <para>Bez nich by plavení dřeva bylo číslo, které se objeví ve skladu —
/// a hráč by neměl jak poznat, že mu splav funguje. Pohyb po vodě je půlka
/// důvodu, proč tuhle mechaniku stavět.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění, kreslí jen to, co je
/// ve výřezu kamery, a jen zblízka — z výšky by kláda byla pod pixel.</para>
/// </summary>
public sealed class RaftRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    private readonly SpriteLibrary _sprites;
    private readonly Texture2D _pixel;

    public RaftRenderer(SpriteLibrary sprites, Texture2D whitePixel)
    {
        _sprites = sprites;
        _pixel = whitePixel;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (simulation.Rafts.Count == 0 || camera.Zoom < DetailLevel.Creatures)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        var sprite = _sprites.Get("fx.log");

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        foreach (var log in simulation.Rafts.Logs)
        {
            var bounds = new Rectangle(log.X * TileSize, log.Y * TileSize, TileSize, TileSize);
            if (bounds.Right < min.X || bounds.Left > max.X
                || bounds.Bottom < min.Y || bounds.Top > max.Y)
            {
                continue;
            }

            if (sprite is not null)
            {
                spriteBatch.Draw(sprite, bounds, Color.White);
            }
            else
            {
                spriteBatch.Draw(_pixel, bounds, new Color(146, 112, 68));
            }
        }

        spriteBatch.End();
    }
}
