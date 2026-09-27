using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Kudy teče láva (Výheň, svety-design.md 4.4) — čistá funkce povrchu.
///
/// <para>Stejné trasování potřebuje simulace (se zástavbou: hráze, kanály)
/// i výběr místa přistání (holý terén, ještě bez simulace). Povrch je proto
/// za rozhraním <see cref="ILavaGround"/> a trasování generické přes
/// <c>struct</c> — žádný box, žádná alokace kromě dráhy, kterou volající
/// dodá a opakovaně používá.</para>
/// </summary>
public static class LavaFlow
{
    /// <summary>
    /// Dráha lávy z průduchu: vždy na nejnižšího souseda, kde ještě nebyla.
    /// Hrází neprojde, do kanálu steče (je „hlubší" než cokoli kolem), korytem
    /// teče dál, i kdyby mírně stoupalo, a na jeho konci se zastaví. Jinak do
    /// kopce jen o <see cref="EruptionRule.SpreadTolerance"/>; u moře ztuhne
    /// v novou zem a dál neteče.
    /// </summary>
    public static void Trace<TGround>(in TGround ground, int flowLength, int ventX, int ventY, List<long> path)
        where TGround : struct, ILavaGround
    {
        path.Clear();
        int x = ventX;
        int y = ventY;
        path.Add(TileKey.Pack(x, y));
        double here = ground.FlowHeight(x, y);
        bool inChannel = false;
        for (int step = 0; step < flowLength; step++)
        {
            double best = double.MaxValue;
            int bestX = 0;
            int bestY = 0;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    int nx = x + dx;
                    int ny = y + dy;
                    if (path.Contains(TileKey.Pack(nx, ny)))
                    {
                        continue;
                    }

                    double height = ground.FlowHeight(nx, ny);
                    if (height < best)
                    {
                        best = height;
                        bestX = nx;
                        bestY = ny;
                    }
                }
            }

            bool intoChannel = best < ChannelFloor;
            if (best == double.MaxValue || (best > here + EruptionRule.SpreadTolerance && !(inChannel && intoChannel)))
            {
                break; // zahrazeno, nebo prohlubeň, ze které láva nevyteče
            }

            x = bestX;
            y = bestY;
            here = best;
            inChannel = intoChannel;
            path.Add(TileKey.Pack(x, y));
            if (ground.IsWater(x, y))
            {
                break; // láva se potkala s mořem: pára, nová zem, konec
            }
        }
    }

    /// <summary>
    /// Terén má výšku 0–1 a kanál je o celou výšku hlouběji — koryto je
    /// tedy všechno pod nulou.
    /// </summary>
    public const double ChannelFloor = 0.0;

    /// <summary>Hráz: láva jí neprojde.</summary>
    public const double Wall = double.MaxValue;

    /// <summary>Výška pro lávu na holém terénu: ztuhlá láva je o kus výš.</summary>
    public static double TerrainHeight(double elevation, bool crust) =>
        crust ? elevation + EruptionRule.CrustRise : elevation;
}

/// <summary>Povrch, po kterém teče láva: výška pro lávu a voda, kde ztuhne.</summary>
public interface ILavaGround
{
    /// <summary>
    /// Jak „nízko" je dlaždice: výška terénu, <see cref="LavaFlow.Wall"/> pro
    /// hráz, pod <see cref="LavaFlow.ChannelFloor"/> pro koryto kanálu.
    /// </summary>
    double FlowHeight(int x, int y);

    /// <summary>Voda — láva v ní ztuhne a dál neteče.</summary>
    bool IsWater(int x, int y);
}

/// <summary>Holý terén bez zástavby — pro výběr místa přistání.</summary>
public readonly struct TerrainLavaGround : ILavaGround
{
    private readonly ITerrain _terrain;
    private readonly BiomeRegistry _biomes;
    private readonly int _crust;

    public TerrainLavaGround(ITerrain terrain, BiomeRegistry biomes, EruptionRule rule)
    {
        _terrain = terrain;
        _biomes = biomes;
        _crust = rule.CrustBiomeIndex;
    }

    public double FlowHeight(int x, int y) =>
        LavaFlow.TerrainHeight(_terrain.ElevationAt(x, y), _terrain.BiomeAt(x, y) == _crust);

    public bool IsWater(int x, int y) => _biomes[_terrain.BiomeAt(x, y)].IsWater;
}
