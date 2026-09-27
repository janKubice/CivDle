using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Flóra Xena na mapě (svety-design.md 4.6, 5.3): flóra jemně „dýchá"
/// (pomalá vlna jasu přes celé pole), na okraji se plazí úponky k sousedním
/// dlaždicím a hnízda pulzují. V noci svítí — Xeno má mít nejkrásnější noc
/// v galaxii.
///
/// <para>Kreslí se nad upečeným terénem, pod budovami. Bez alokací za snímek:
/// jas je funkce souřadnice a času, úponky hash dlaždice. Při velkém
/// oddálení jen jas po větších čtvercích, bez úponků.</para>
///
/// <para>Vrstva: render. Ze simulace čte jen biom dlaždice a denní čas.</para>
/// </summary>
public sealed class FloraRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik dlaždic nejvýš za snímek; nad tím se řídne.</summary>
    private const int TileBudget = 8_000;

    private static readonly Color Breath = new(210, 120, 255);
    private static readonly Color NightGlow = new(90, 240, 220);
    private static readonly Color Tendril = new(150, 80, 200);
    private static readonly Color NestCore = new(255, 140, 230);

    private readonly Texture2D _pixel;
    private readonly int _bloom = -1;
    private readonly int _nest = -1;
    private float _time;

    public FloraRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        int flora = content.Hazards.FloraIndex;
        if (flora >= 0 && content.Hazards.Hazards[flora].Flora is { } rule)
        {
            _bloom = rule.BloomBiomeIndex;
            _nest = rule.NestBiomeIndex;
        }
    }

    /// <summary>Má svět flóru? Jinak renderer nestojí nic.</summary>
    public bool Enabled => _bloom >= 0;

    /// <summary>Posune dýchání (reálný čas).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Jas „dechu" flóry v bodě a čase (0–1): pomalá vlna, která přechází
    /// přes pole. Čistá funkce, ať se dá ladit bez grafiky.
    /// </summary>
    public static float BreathAt(int x, int y, float time) =>
        0.5f + 0.5f * MathF.Sin(time * 0.6f + x * 0.21f + y * 0.17f);

    /// <summary>Nakreslí flóru ve výřezu.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (!Enabled)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        int fromX = (int)Math.Floor(min.X / TileSize) - 1;
        int fromY = (int)Math.Floor(min.Y / TileSize) - 1;
        int toX = (int)Math.Ceiling(max.X / TileSize) + 1;
        int toY = (int)Math.Ceiling(max.Y / TileSize) + 1;
        long tiles = (long)(toX - fromX + 1) * (toY - fromY + 1);
        int step = tiles <= TileBudget ? 1 : (int)Math.Ceiling(Math.Sqrt(tiles / (double)TileBudget));

        // Noc: čím níž slunce, tím víc flóra svítí.
        float night = 1f - (float)SupplyCurve.Sun(simulation.TimeOfDay01);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend, transformMatrix: camera.Transform);
        for (int y = fromY - Mod(fromY, step); y <= toY; y += step)
        {
            for (int x = fromX - Mod(fromX, step); x <= toX; x += step)
            {
                byte biome = simulation.BiomeAt(x, y);
                bool nest = biome == _nest;
                if (biome != _bloom && !nest)
                {
                    continue;
                }

                float breath = BreathAt(x, y, _time);
                var rect = new Rectangle(x * TileSize, y * TileSize, TileSize * step, TileSize * step);
                spriteBatch.Draw(_pixel, rect, Breath * (0.06f + 0.08f * breath));
                if (night > 0.2f)
                {
                    spriteBatch.Draw(_pixel, rect, NightGlow * (night * (0.08f + 0.1f * breath)));
                }

                if (step > 1)
                {
                    continue;
                }

                if (nest)
                {
                    float pulse = 0.5f + 0.5f * MathF.Sin(_time * 2f + x);
                    spriteBatch.Draw(_pixel, new Rectangle(x * TileSize + 5, y * TileSize + 5, 6, 6), NestCore * (0.4f + 0.5f * pulse));
                }

                DrawTendrils(spriteBatch, simulation, x, y);
            }
        }

        spriteBatch.End();
    }

    /// <summary>Úponky k sousedům, kteří flórou ještě nejsou — okraj se plazí.</summary>
    private void DrawTendrils(SpriteBatch spriteBatch, Simulation simulation, int x, int y)
    {
        int px = x * TileSize;
        int py = y * TileSize;
        float sway = MathF.Sin(_time * 0.9f + x * 0.7f + y) * 1.5f;
        if (!IsFlora(simulation, x + 1, y))
        {
            int length = 3 + (int)(Hash(x, y, 1) * 5);
            spriteBatch.Draw(_pixel, new Rectangle(px + TileSize, py + 4 + (int)sway, length, 1), Tendril);
            spriteBatch.Draw(_pixel, new Rectangle(px + TileSize, py + 10 - (int)sway, length / 2 + 1, 1), Tendril * 0.8f);
        }

        if (!IsFlora(simulation, x - 1, y))
        {
            int length = 3 + (int)(Hash(x, y, 2) * 5);
            spriteBatch.Draw(_pixel, new Rectangle(px - length, py + 6 + (int)sway, length, 1), Tendril);
        }

        if (!IsFlora(simulation, x, y + 1))
        {
            int length = 3 + (int)(Hash(x, y, 3) * 5);
            spriteBatch.Draw(_pixel, new Rectangle(px + 5 + (int)sway, py + TileSize, 1, length), Tendril);
        }

        if (!IsFlora(simulation, x, y - 1))
        {
            int length = 3 + (int)(Hash(x, y, 4) * 5);
            spriteBatch.Draw(_pixel, new Rectangle(px + 9 - (int)sway, py - length, 1, length), Tendril * 0.8f);
        }
    }

    private bool IsFlora(Simulation simulation, int x, int y)
    {
        byte biome = simulation.BiomeAt(x, y);
        return biome == _bloom || biome == _nest;
    }

    private static int Mod(int value, int step) => ((value % step) + step) % step;

    private static float Hash(int x, int y, int salt)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + salt * 1274126177);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }
}
