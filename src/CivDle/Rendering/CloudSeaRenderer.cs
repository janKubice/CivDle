using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Oblačný oceán a paluby Nebes (svety-design.md 4.5, 5.3): pod městem pásy
/// mraků plynného obra ve dvou vrstvách, každá pluje jinou rychlostí a jinak
/// posouvá s kamerou (paralaxa — spodní vrstva je „hlouběji"), na palubě
/// pláty s nýty, zábradlí a lana dolů na jejím okraji a stín paluby na
/// mracích pod ní. Z toho má hráč pocit výšky, aniž by si četl tooltip.
///
/// <para>Kreslí se nad upečeným terénem a pod budovami. Bez alokací za
/// snímek: barva pásu je funkce souřadnice a času, pláty a lana hash
/// dlaždice. Oblaka se kreslí po dvojicích dlaždic a při velkém oddálení
/// ještě řidčeji (rozpočet dlaždic jako u přílivu).</para>
///
/// <para>Vrstva: render. Ze simulace čte jen biom dlaždice.</para>
/// </summary>
public sealed class CloudSeaRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik buněk oblaků nejvýš za snímek; nad tím se řídne.</summary>
    private const int CellBudget = 6_000;

    /// <summary>Pod tímhle přiblížením se pláty a lana nekreslí — byl by to šum.</summary>
    private const float DetailZoom = 0.6f;

    /// <summary>Pásy obra: krémová, broskvová, okrová, rezavá, bílá (paleta z návrhu).</summary>
    private static readonly Color[] Bands =
    {
        new(242, 230, 208), new(236, 200, 160), new(214, 170, 118), new(186, 122, 84), new(250, 244, 232),
    };

    private static readonly Color Plate = new(150, 158, 170);
    private static readonly Color Seam = new(96, 102, 116);
    private static readonly Color Rivet = new(206, 212, 222);
    private static readonly Color Rail = new(222, 196, 150);
    private static readonly Color Rope = new(150, 122, 86);
    private static readonly Color Shadow = new(70, 50, 60);

    private readonly Texture2D _pixel;
    private readonly bool[] _void;
    private readonly int _deck;
    private float _time;

    public CloudSeaRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _void = new bool[content.Biomes.Count];
        for (int b = 0; b < _void.Length; b++)
        {
            _void[b] = content.Biomes[b].HasNoGround;
            Enabled |= _void[b];
        }

        _deck = content.World.Platform?.BiomeIndex ?? -1;
    }

    /// <summary>Má svět oblačný oceán? Jinak renderer nestojí nic.</summary>
    public bool Enabled { get; }

    /// <summary>Posune mraky (reálný čas — i v pauze obr víří).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Barva pásu mraků v bodě (0–1 světa po vrstvách): pásy podél osy x,
    /// jejich okraje zvlněné. Veřejná a čistá, ať se dá ladit bez grafiky.
    /// </summary>
    public static Color BandColor(float worldX, float worldY, int layer)
    {
        float wave = MathF.Sin(worldX * 0.045f + layer * 1.7f) * 3.5f + MathF.Sin(worldX * 0.013f - layer) * 7f;
        float band = (worldY + wave) / (layer == 0 ? 11f : 17f) + layer * 0.37f;
        int index = (int)MathF.Floor(band);
        float blend = band - index;
        int count = Bands.Length;
        var a = Bands[((index % count) + count) % count];
        var b = Bands[(((index + 1) % count) + count) % count];
        return Color.Lerp(a, b, MathHelper.SmoothStep(0f, 1f, blend));
    }

    /// <summary>Nakreslí oblaka, stíny palub a palubu ve výřezu.</summary>
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
        long cells = (long)(toX - fromX + 1) * (toY - fromY + 1) / 4;
        int step = 2 * (cells <= CellBudget ? 1 : (int)Math.Ceiling(Math.Sqrt(cells / (double)CellBudget)));
        var center = camera.Position / TileSize;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int layer = 0; layer < 2; layer++)
        {
            // Paralaxa: hlubší vrstva se s kamerou posouvá méně, a pluje pomaleji.
            float lag = layer == 0 ? 0.35f : 0.15f;
            float driftX = _time * (layer == 0 ? 1.1f : 2.3f) + center.X * lag;
            float driftY = center.Y * lag;
            float alpha = layer == 0 ? 0.9f : 0.35f;
            for (int y = fromY - Mod(fromY, step); y <= toY; y += step)
            {
                for (int x = fromX - Mod(fromX, step); x <= toX; x += step)
                {
                    if (!IsVoid(simulation, x, y))
                    {
                        continue;
                    }

                    var color = BandColor(x + driftX, y + driftY, layer);
                    spriteBatch.Draw(_pixel, new Rectangle(x * TileSize, y * TileSize, TileSize * step, TileSize * step), color * alpha);
                }
            }
        }

        // Z velké dálky stačí barva paluby z upečeného terénu — stíny a pláty
        // po dlaždicích by stály víc, než je vidět.
        if (_deck >= 0 && step <= 2)
        {
            DrawDeck(spriteBatch, simulation, fromX, fromY, toX, toY, camera.Zoom >= DetailZoom);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Paluba: stín na mracích pod jejím okrajem (vpravo dole — slunce vlevo
    /// nahoře), pláty s nýty, zábradlí na okraji a lana dolů do mraků.
    /// </summary>
    private void DrawDeck(SpriteBatch spriteBatch, Simulation simulation, int fromX, int fromY, int toX, int toY, bool detail)
    {
        for (int y = fromY; y <= toY; y++)
        {
            for (int x = fromX; x <= toX; x++)
            {
                byte biome = simulation.BiomeAt(x, y);
                int px = x * TileSize;
                int py = y * TileSize;
                if (biome < _void.Length && _void[biome])
                {
                    // Stín paluby: paluba nad (y−1) nebo vlevo (x−1) vrhá stín sem.
                    if (simulation.BiomeAt(x, y - 1) == _deck)
                    {
                        spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, TileSize / 2), Shadow * 0.35f);
                    }

                    if (simulation.BiomeAt(x - 1, y) == _deck)
                    {
                        spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize / 4, TileSize), Shadow * 0.25f);
                    }

                    continue;
                }

                if (biome != _deck)
                {
                    continue;
                }

                spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, TileSize), Plate * 0.55f);
                if (!detail)
                {
                    continue;
                }

                // Spáry plátů a nýty v rozích.
                spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, 1), Seam * 0.8f);
                spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, TileSize), Seam * 0.8f);
                spriteBatch.Draw(_pixel, new Rectangle(px + 2, py + 2, 1, 1), Rivet);
                spriteBatch.Draw(_pixel, new Rectangle(px + TileSize - 3, py + TileSize - 3, 1, 1), Rivet);

                // Okraj nad oblaky: zábradlí a lana visící dolů do mraků.
                if (IsVoid(simulation, x, y + 1))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py + TileSize - 2, TileSize, 2), Rail);
                    if (((x * 73856093) ^ (y * 19349663)) % 3 == 0)
                    {
                        int sway = (int)MathF.Round(MathF.Sin(_time * 0.8f + x) * 1.5f);
                        spriteBatch.Draw(_pixel, new Rectangle(px + TileSize / 2 + sway, py + TileSize, 1, TileSize), Rope * 0.8f);
                    }
                }

                if (IsVoid(simulation, x, y - 1))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, 1), Rail);
                }

                if (IsVoid(simulation, x - 1, y))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py, 1, TileSize), Rail);
                }

                if (IsVoid(simulation, x + 1, y))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px + TileSize - 1, py, 1, TileSize), Rail);
                }
            }
        }
    }

    private bool IsVoid(Simulation simulation, int x, int y)
    {
        byte biome = simulation.BiomeAt(x, y);
        return biome < _void.Length && _void[biome];
    }

    private static int Mod(int value, int step) => ((value % step) + step) % step;
}
