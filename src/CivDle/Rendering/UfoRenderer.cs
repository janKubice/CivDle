using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Talíř nad městem: když simulace hlásí návštěvu UFO, vykreslí se nad cílovou
/// dlaždicí létající talíř s pulzujícím paprskem a mírně se pohupuje. Kreslí se
/// v souřadnicích SVĚTA (uvnitř kamerové dávky), aby seděl na mapu.
///
/// <para>Čte jen ze simulace (je vidět? kam míří?), nikdy do ní nezapisuje —
/// veškerý dopad na svět řeší <see cref="Core.Sim.UfoSystem"/> v simulaci.</para>
/// </summary>
public sealed class UfoRenderer
{
    private static readonly Color HullColor = new(150, 160, 185);
    private static readonly Color DomeColor = new(120, 235, 255);
    private static readonly Color BeamColor = new(120, 235, 255, 70);

    /// <summary>Jak vysoko nad cílem talíř visí (v dlaždicích).</summary>
    private const float HoverTiles = 2.2f;

    private readonly Texture2D _pixel;

    public UfoRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>
    /// Vykreslí talíř, pokud právě přiletěl. Volá se uvnitř dávky s kamerovou
    /// maticí, hned po terénu a budovách (talíř má být nad vším na mapě).
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, float totalSeconds)
    {
        if (!simulation.IsUfoVisible)
        {
            return;
        }

        var (tileX, tileY) = simulation.UfoTarget;
        float tile = TerrainRenderer.TileSize;
        float centerX = (tileX + 0.5f) * tile;
        float groundY = (tileY + 0.5f) * tile;

        // Pohupování: talíř nikdy nestojí na místě, jinak vypadá jako nalepený sprite.
        float bob = MathF.Sin(totalSeconds * 2.1f) * tile * 0.18f;
        float centerY = groundY - HoverTiles * tile + bob;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        // Paprsek dolů — kužel z několika obdélníků, nejsvětlejší nahoře.
        int beamSteps = 6;
        for (int i = 0; i < beamSteps; i++)
        {
            float t = i / (float)beamSteps;
            float width = tile * (0.6f + t * 1.4f);
            float y = centerY + (groundY - centerY) * t;
            float height = (groundY - centerY) / beamSteps + 1f;
            spriteBatch.Draw(_pixel,
                new Rectangle((int)(centerX - width / 2f), (int)y, (int)width, (int)height),
                BeamColor * (1f - t * 0.6f));
        }

        // Trup: dvě zploštělá tělesa nad sebou + kopule.
        float hullWidth = tile * 2.2f;
        float hullHeight = tile * 0.45f;
        spriteBatch.Draw(_pixel,
            new Rectangle((int)(centerX - hullWidth / 2f), (int)(centerY - hullHeight / 2f), (int)hullWidth, (int)hullHeight),
            HullColor);
        spriteBatch.Draw(_pixel,
            new Rectangle((int)(centerX - hullWidth * 0.32f), (int)(centerY - hullHeight * 1.4f), (int)(hullWidth * 0.64f), (int)hullHeight),
            DomeColor);

        // Blikající světla po obvodu — pulz odlišuje talíř od kusu terénu.
        float blink = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        int lightSize = Math.Max(2, (int)(tile * 0.16f));
        for (int i = -1; i <= 1; i++)
        {
            spriteBatch.Draw(_pixel,
                new Rectangle((int)(centerX + i * hullWidth * 0.3f - lightSize / 2f), (int)(centerY + hullHeight * 0.1f), lightSize, lightSize),
                Color.Lerp(new Color(255, 120, 90), Color.White, blink));
        }

        spriteBatch.End();
    }
}
