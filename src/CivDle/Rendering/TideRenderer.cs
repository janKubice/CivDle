using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Příliv na mapě (svety-design.md 4.3, 5.3): zaplavené přílivové mělčiny
/// pod vodou, pěna na čáře přílivu a tmavší mokrý písek, který odliv právě
/// odkryl. Čára přílivu po mapě putuje, protože níž položené mělčiny
/// zaplaví dřív — hráč vidí, kam voda dojde, dřív než si přečte hladinu.
///
/// <para>Kreslí se pod budovami (voda teče mezi kůly, zaplavená budova má
/// vodu po okna zvlášť z <see cref="HazardRenderer"/>). Bez alokací za snímek:
/// výška mělčiny je v paměti simulace, pěna se hýbe podle času a hashe dlaždice.
/// Při velkém oddálení se kreslí jen každá n-tá dlaždice (větším čtvercem).</para>
///
/// <para>Útes, který korálový lom vytěžil, na čas zbělá — a jak dorůstá,
/// barvu zase dostane (útes je uzel s dobíjením jako les).</para>
///
/// <para>Vrstva: render. Ze simulace jen čte (hladinu, výšku mělčin, biom, uzly).</para>
/// </summary>
public sealed class TideRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik dlaždic nejvýš projde za snímek, než se začne řídnout.</summary>
    private const int TileBudget = 20_000;

    /// <summary>Jak blízko pod hladinou je dlaždice ještě „na čáře" (pěna).</summary>
    private const double FoamBand = 0.05;

    /// <summary>Jak vysoko nad hladinou je písek ještě mokrý (po odlivu).</summary>
    private const double WetBand = 0.14;

    private static readonly Color Foam = new(238, 248, 250);
    private static readonly Color Bleach = new(240, 236, 226);
    private static readonly Color WetSand = new(70, 60, 44);

    private readonly Texture2D _pixel;
    private readonly Color _water;
    private readonly bool _enabled;

    /// <summary>Vodní biomy s těžitelným uzlem (útes) — ty po vytěžení blednou.</summary>
    private readonly bool[] _reef;
    private float _time;

    public TideRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _enabled = content.Hazards.TideIndex >= 0;

        // Voda přílivu má barvu mělčiny světa — stejná voda jako kolem ostrova.
        int shallow = content.Biomes.All.ToList().FindIndex(b => b.Id == "shallow_water");
        var color = shallow >= 0 ? content.Biomes[shallow].MapColor : new RgbColor(62, 133, 184);
        _water = new Color(color.R, color.G, color.B);

        _reef = new bool[content.Biomes.Count];
        for (int b = 0; b < _reef.Length; b++)
        {
            _reef[b] = content.Biomes[b].IsWater && content.Biomes[b].ClickYield is not null;
        }
    }

    /// <summary>Posune pěnu (reálný čas).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Nakreslí příliv ve výřezu; svět bez přílivu nestojí nic.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (!_enabled || !simulation.HasTides)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        int fromX = (int)Math.Floor(min.X / TileSize);
        int fromY = (int)Math.Floor(min.Y / TileSize);
        int toX = (int)Math.Ceiling(max.X / TileSize);
        int toY = (int)Math.Ceiling(max.Y / TileSize);
        long tiles = (long)(toX - fromX + 1) * (toY - fromY + 1);
        int step = tiles <= TileBudget ? 1 : (int)Math.Ceiling(Math.Sqrt(tiles / (double)TileBudget));

        double level = simulation.TideLevel;
        bool ebbing = !simulation.TideRising;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int y = fromY - fromY % step; y <= toY; y += step)
        {
            for (int x = fromX - fromX % step; x <= toX; x += step)
            {
                var rect = new Rectangle(x * TileSize, y * TileSize, TileSize * step, TileSize * step);
                double height = simulation.TideHeightAt(x, y);
                if (height >= 1.0)
                {
                    // Není mělčina — ale může to být vytěžený útes.
                    byte biome = simulation.BiomeAt(x, y);
                    if (biome < _reef.Length && _reef[biome])
                    {
                        DrawBleach(spriteBatch, simulation, x, y, rect);
                    }

                    continue;
                }

                if (simulation.IsFloodedAt(x, y))
                {
                    // Čím hlouběji pod hladinou, tím sytější voda.
                    float depth = (float)Math.Clamp((level - height) / 0.3, 0, 1);
                    spriteBatch.Draw(_pixel, rect, _water * (0.55f + 0.35f * depth));
                    if (level - height < FoamBand && step == 1)
                    {
                        DrawFoam(spriteBatch, x, y);
                    }
                }
                else if (ebbing && height - level < WetBand)
                {
                    // Mokrý písek: odliv ho právě odkryl, schne od kraje.
                    float wet = 1f - (float)((height - level) / WetBand);
                    spriteBatch.Draw(_pixel, rect, WetSand * (0.22f * wet));
                }
            }
        }

        spriteBatch.End();
    }

    /// <summary>Vytěžený útes zbělá: čím méně mu zbývá, tím bělejší.</summary>
    private void DrawBleach(SpriteBatch spriteBatch, Simulation simulation, int x, int y, Rectangle rect)
    {
        int max = simulation.NodeMaxCharges(x, y);
        if (max <= 0)
        {
            return;
        }

        int left = simulation.NodeChargesLeft(x, y);
        if (left < max)
        {
            spriteBatch.Draw(_pixel, rect, Bleach * (0.55f * (1f - left / (float)max)));
        }
    }

    /// <summary>Pěna na čáře přílivu: pár světlých čárek, které se pomalu vlní.</summary>
    private void DrawFoam(SpriteBatch spriteBatch, int x, int y)
    {
        uint h = Hash(x, y);
        int px = x * TileSize;
        int py = y * TileSize;
        for (int k = 0; k < 3; k++)
        {
            float wave = MathF.Sin(_time * 1.6f + k * 2.1f + (h & 0xFF) * 0.05f);
            int fx = px + (int)((h >> (k * 5)) % (uint)Math.Max(1, TileSize - 4));
            int fy = py + 2 + k * (TileSize / 3) + (int)(wave * 1.5f);
            spriteBatch.Draw(_pixel, new Rectangle(fx, fy, 4, 1), Foam * (0.55f + 0.3f * wave));
        }
    }

    private static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }
}
