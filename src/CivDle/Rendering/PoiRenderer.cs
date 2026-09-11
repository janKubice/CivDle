using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Anomálie na mapě: značka, ke které stojí za to poslat výpravu.
///
/// <para>Pulzuje schválně. Anomálie je jediná věc na mapě, kterou hráč
/// <b>nepostavil</b> a která na něj čeká — kdyby jen tiše ležela mezi stromy,
/// nikdo by ji nenašel, a mechanika, na kterou se nepřijde, ve hře není.</para>
///
/// <para>Seznam se nedrží: <see cref="PointOfInterestSystem"/> ho dopočítá
/// z hashe pro právě viditelný výřez. Renderer si tedy nemusí nic pamatovat
/// ani nic invalidovat.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění.</para>
/// </summary>
public sealed class PoiRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    private static readonly Color Idle = new(214, 178, 255);
    private static readonly Color Target = new(255, 226, 150);

    private readonly SpriteLibrary _sprites;
    private readonly Texture2D _pixel;

    /// <summary>Jeden seznam na celý život — žádná alokace za snímek.</summary>
    private readonly List<PointOfInterest> _visible = new();

    private float _pulse;

    public PoiRenderer(SpriteLibrary sprites, Texture2D whitePixel)
    {
        _sprites = sprites;
        _pixel = whitePixel;
    }

    public void Update(float dt) => _pulse += dt;

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        simulation.PointsOfInterest.InRange(
            (int)Math.Floor(min.X / TileSize),
            (int)Math.Floor(min.Y / TileSize),
            (int)Math.Ceiling(max.X / TileSize),
            (int)Math.Ceiling(max.Y / TileSize),
            _visible);

        if (_visible.Count == 0 && !simulation.ExpeditionRunning)
        {
            return;
        }

        var sprite = _sprites.Get("fx.anomaly");
        float glow = 0.55f + (0.45f * MathF.Sin(_pulse * 2.4f));

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        for (int i = 0; i < _visible.Count; i++)
        {
            // Zastavěné zvláštní místo se nekreslí. Značka pod budovou vypadala
            // jako kus kulisy, který se zapomněl smazat — hráč přes oázu
            // postavil a ona tam pořád svítila.
            if (simulation.IsOccupied(_visible[i].X, _visible[i].Y)
                || simulation.HasRoadAt(_visible[i].X, _visible[i].Y))
            {
                continue;
            }

            Mark(spriteBatch, sprite, _visible[i].X, _visible[i].Y, Idle * glow);
        }

        // Cíl běžící výpravy zůstane vidět, i když je „vybraný": jinak by
        // hráč po vypravení ztratil z mapy jediné místo, na kterém se něco děje.
        if (simulation.ExpeditionRunning)
        {
            var target = simulation.ExpeditionTarget;
            Mark(spriteBatch, sprite, target.X, target.Y, Target);
        }

        spriteBatch.End();
    }

    private void Mark(SpriteBatch spriteBatch, Texture2D? sprite, int tileX, int tileY, Color color)
    {
        var bounds = new Rectangle(tileX * TileSize, tileY * TileSize, TileSize, TileSize);
        if (sprite is not null)
        {
            spriteBatch.Draw(sprite, bounds, color);
            return;
        }

        spriteBatch.Draw(_pixel, bounds, color);
    }
}
