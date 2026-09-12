using CivDle.Core.Sim;
using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vyšlapané stezky: hlína prosvítající tam, kudy lidé chodí.
///
/// <para><b>Proč se nepeče do terénu:</b> zem se vykresluje po chuncích do
/// textur a přepekla by se při každé změně. Stezky se mění pořád — zapékat
/// je by znamenalo přepékat celé okolí kamery každou vteřinu. Kreslí se proto
/// přes hotovou zem jako průhledný závoj.</para>
///
/// <para><b>Vrstva:</b> čistý render. Mapu došlapů plní
/// <see cref="AgentSystem"/>, tenhle renderer ji jen čte.</para>
/// </summary>
public sealed class WornPathRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>
    /// Barva ušlapané země. Tmavší a šedší než cesta — stezka není dlažba,
    /// je to tráva, která se nestihla vzpamatovat.
    /// </summary>
    private static readonly Color Trodden = new(122, 104, 76);

    /// <summary>
    /// Nejvyšší krytí i na úplně vyšlapané dlaždici. Přes to už by stezka
    /// přebila terén pod sebou a vypadala jako položená cesta.
    /// </summary>
    private const float MaxAlpha = 0.38f;

    /// <summary>Pod touhle mírou se ani nezačíná kreslit — šum by jen špinil zem.</summary>
    private const float Visible = 0.12f;

    private readonly Texture2D _pixel;
    private readonly FootfallMap _footfall;

    public WornPathRenderer(Texture2D whitePixel, FootfallMap footfall)
    {
        _pixel = whitePixel;
        _footfall = footfall;
    }

    /// <summary>
    /// Nakreslí stezky ve výřezu.
    ///
    /// <para>Prochází se dlaždice na obrazovce, ne zapamatované stezky: těch
    /// může být pár tisíc kdekoli po mapě, kdežto dlaždic ve výřezu je pár set
    /// a dotaz do slovníku je levnější než test viditelnosti.</para>
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (_footfall.Count == 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();

        // Vlastní dávka, jako každý renderer v téhle vrstvě: volající žádnou
        // otevřenou nenechává.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        int minX = (int)MathF.Floor(min.X / TileSize);
        int minY = (int)MathF.Floor(min.Y / TileSize);
        int maxX = (int)MathF.Ceiling(max.X / TileSize);
        int maxY = (int)MathF.Ceiling(max.Y / TileSize);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float wear = _footfall.WearAt(x, y);
                if (wear < Visible)
                {
                    continue;
                }

                // Pod budovou ani pod cestou se stezka nekreslí: tam by jen
                // špinila sprite, který ji stejně celou zakrývá.
                if (simulation.IsOccupied(x, y) || simulation.HasRoadAt(x, y))
                {
                    continue;
                }

                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize),
                    Trodden * Alpha(wear));
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Krytí podle ošlapání. Náběh od <see cref="Visible"/>, aby stezka
    /// <b>vystoupila</b> ze země, místo aby se objevila celá naráz.
    /// </summary>
    public static float Alpha(float wear)
    {
        if (wear < Visible)
        {
            return 0f;
        }

        float t = (wear - Visible) / (1f - Visible);
        return Math.Clamp(t, 0f, 1f) * MaxAlpha;
    }
}
