using CivDle.Rendering.Effects;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Stánky trhu, který po sobě nechala karavana.
///
/// <para>Stojí kolem místa, kde karavana zastavila, a rostou i mizí postupně —
/// trh, který naskočí celý naráz, vypadá jako chyba vykreslení, ne jako
/// událost. Kreslí se nad zemí a pod budovami: je to dočasná věc na návsi,
/// ne stavba.</para>
///
/// <para>Vrstva: čistý render nad <see cref="MarketEvent"/>, který drží čas.</para>
/// </summary>
public sealed class MarketRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik stánků se postaví. Lichý počet, ať to nevypadá jako řada.</summary>
    private const int StallCount = 5;

    /// <summary>Rozmístění stánků kolem středu, v dlaždicích.</summary>
    private static readonly (int Dx, int Dy)[] Layout =
    {
        (-1, 0), (1, 0), (0, -1), (-1, 1), (1, 1),
    };

    /// <summary>Plachty stánků. Různé barvy, ať je z dálky poznat, že jich je víc.</summary>
    private static readonly Color[] Awnings =
    {
        new(206, 108, 92), new(210, 176, 96), new(132, 160, 190),
        new(176, 130, 178), new(146, 176, 124),
    };

    private readonly SpriteLibrary _sprites;
    private readonly MarketEvent _market;

    public MarketRenderer(SpriteLibrary sprites, MarketEvent market)
    {
        _sprites = sprites;
        _market = market;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (!_market.IsOpen)
        {
            return;
        }

        float scale = _market.StallScale;
        if (scale <= 0.01f)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        var center = _market.Position;
        if (center.X < min.X - TileSize * 4 || center.X > max.X + TileSize * 4
            || center.Y < min.Y - TileSize * 4 || center.Y > max.Y + TileSize * 4)
        {
            return; // trh je mimo obraz
        }

        var sprite = _sprites.Get("building.market");

        for (int i = 0; i < StallCount; i++)
        {
            var (dx, dy) = Layout[i];

            // Stánek roste od země nahoru: šířka zůstává, výška se krátí, a co
            // se ukrojí, to se přičte k horní hraně. Kdyby se zmenšoval do
            // středu, plachta by se vznášela nad trávou.
            int height = Math.Max(1, (int)(TileSize * scale));
            var rect = new Rectangle(
                (_market.TileX + dx) * TileSize,
                ((_market.TileY + dy) * TileSize) + (TileSize - height),
                TileSize,
                height);

            spriteBatch.Draw(sprite, rect, Color.White);

            // Pruh plachty přes střechu stánku — odliší trh od řady krámků
            // a je to to jediné, co je z něj vidět při odzoomování.
            int awning = Math.Max(1, (int)(TileSize * 0.28f * scale));
            spriteBatch.Draw(
                _sprites.Get("fx.shadow"),
                new Rectangle(rect.X, rect.Y, TileSize, awning),
                Awnings[i % Awnings.Length] * 0.9f);
        }
    }
}
