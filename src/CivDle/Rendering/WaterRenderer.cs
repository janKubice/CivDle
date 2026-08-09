using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Třpyt na hladině — jediná část vody, která se nedá upéct do chunku.
///
/// <para>Hloubka a pěna u břehu jsou vlastnost místa, takže se počítají jednou
/// při pečení chunku (<see cref="TerrainPainter"/>). Odlesk je ale <b>pohyb</b>
/// a pohyb se do textury upéct nedá. A přitom je to on, kdo z modré plochy
/// udělá vodu: statická voda vypadá jako podlaha, dokud se po ní nezačne něco
/// mihotat.</para>
///
/// <para>Kreslí se jen tam, kde odlesk zrovna je — vlnová funkce se
/// vyhodnotí pro dlaždici a většina jich neprojde prahem. Z tisíce viditelných
/// vodních dlaždic se tak kreslí nižší desítky.</para>
///
/// <para>Vrstva: čistý render, čte jen simulaci.</para>
/// </summary>
public sealed class WaterRenderer
{
    /// <summary>Pod tímhle přiblížením se třpyt nekreslí — byl by to jen šum.</summary>
    public const float MinZoom = 0.5f;

    /// <summary>Nad tímhle prahem vlny se dlaždice zatřpytí.</summary>
    private const float SparkleThreshold = 0.72f;

    private static readonly Color Sparkle = new(226, 244, 255);

    private readonly Texture2D _pixel;

    /// <summary>Posun vln. Jediný stav rendereru.</summary>
    private float _time;

    public WaterRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>Posune vlny.</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Síla odlesku na dané dlaždici v daném čase (0–1).
    ///
    /// <para>Dvě vlny s různým směrem a rychlostí. Kdyby byla jen jedna,
    /// putovaly by po hladině viditelné pruhy; dvě se navzájem rozbíjejí na
    /// nepravidelné šupinky. Vlastní metoda, aby se dala ověřit bez grafiky —
    /// je to křivka, která se ladí.</para>
    /// </summary>
    public static float Shimmer(int tileX, int tileY, float time)
    {
        float a = MathF.Sin(tileX * 0.55f + tileY * 0.31f - time * 1.15f);
        float b = MathF.Sin(tileX * -0.23f + tileY * 0.71f + time * 0.73f);
        return Math.Clamp((a + b) * 0.25f + 0.5f, 0f, 1f);
    }

    /// <summary>Nakreslí odlesky na viditelné vodě.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < DetailLevel.Scale(MinZoom))
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        int startX = (int)MathF.Floor(min.X / tileSize);
        int startY = (int)MathF.Floor(min.Y / tileSize);
        int endX = (int)MathF.Ceiling(max.X / tileSize);
        int endY = (int)MathF.Ceiling(max.Y / tileSize);

        // Pojistka pro nezvyklá rozlišení: procházet dlaždice po jedné se vyplatí
        // jen do určitého počtu, pak se vrstva radši vzdá (viz DetailLevel).
        if (!DetailLevel.FitsBudget(startX, startY, endX, endY))
        {
            return;
        }

        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                float wave = Shimmer(x, y, _time);
                if (wave < SparkleThreshold || !simulation.IsWaterAt(x, y))
                {
                    continue;
                }

                // Síla odlesku roste od prahu, takže se šupinky rozsvěcují
                // a zhasínají plynule místo blikání.
                float alpha = (wave - SparkleThreshold) / (1f - SparkleThreshold) * 0.5f;
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x * tileSize + 4, y * tileSize + 6, 8, 2),
                    Sparkle * alpha);
            }
        }

        spriteBatch.End();
    }
}
