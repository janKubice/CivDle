using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Zamoření na mapě: jemný závoj přes buňky, které průmysl pokazil.
///
/// <para>Proč skvrny a ne tint přes celou obrazovku: znečištění je <b>místo</b>.
/// Kdyby se ztmavila celá scéna, hráč by věděl, že je zle, ale ne kde — a nemohl
/// by se rozhodnout, kam postavit čističku. Takhle je vidět, který kout města
/// dýmá, a úklid má viditelný postup.</para>
///
/// <para>Barva nese druh: šedá vzduch, kalná zelenohnědá voda, rezavá půda.
/// V jedné buňce vyhrává ten nejsilnější — míchání tří závojů přes sebe by
/// dalo jen šedivou kaši.</para>
///
/// <para>Čte jen ze simulace (render do ní nikdy nepíše, CLAUDE.md) a kreslí
/// jen buňky ve výřezu kamery.</para>
/// </summary>
public sealed class PollutionRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Nejsilnější závoj i při plném zamoření — pod ním musí být pořád vidět město.</summary>
    private const float MaxAlpha = 0.42f;

    /// <summary>Pod touhle mírou se nekreslí nic; jinak by mapa byla trvale ušmudlaná.</summary>
    private const float MinSeverity = 0.04f;

    private static readonly Color AirColor = new(70, 66, 62);
    private static readonly Color WaterColor = new(64, 96, 78);
    private static readonly Color SoilColor = new(104, 72, 44);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    public PollutionRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Vykreslí závoj přes zamořené buňky ve výřezu.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var config = _content.Gameplay.Pollution;
        if (!config.IsEnabled || simulation.PollutionMap.DirtyCellCount == 0)
        {
            return;
        }

        int cellPixels = PollutionGrid.CellTiles * TileSize;
        var (min, max) = camera.VisibleWorldBounds();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        foreach (var (cellX, cellY, air, water, soil) in simulation.PollutionMap.Entries())
        {
            int px = cellX * cellPixels;
            int py = cellY * cellPixels;
            if (px + cellPixels < min.X || px > max.X || py + cellPixels < min.Y || py > max.Y)
            {
                continue;
            }

            // Nejsilnější kanál určuje barvu i sílu — tři závoje přes sebe by
            // splynuly do šedi a hráč by nepoznal, co vlastně kazí.
            var color = AirColor;
            double worst = air;
            if (water > worst)
            {
                worst = water;
                color = WaterColor;
            }

            if (soil > worst)
            {
                worst = soil;
                color = SoilColor;
            }

            float severity = (float)config.Severity(worst);
            if (severity < MinSeverity)
            {
                continue;
            }

            spriteBatch.Draw(_pixel, new Rectangle(px, py, cellPixels, cellPixels), color * (severity * MaxAlpha));
        }

        spriteBatch.End();
    }
}
