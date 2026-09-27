using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Galaxy;

/// <summary>
/// Tři místa, kam může kolonizační loď přistát (svety-design.md 2.2) — jako
/// výběr startu v úvodu hry, jen na cizí planetě.
///
/// <para><b>Co je dobré místo:</b> modul na něj sedne a kolem je pestrá souš
/// (víc biomů = víc surovin na dosah). Místa jsou od sebe daleko, aby volba
/// byla volba, ne tři body vedle sebe.</para>
///
/// <para>Deterministické ze seedu (terén je funkce seedu) a levné: kandidáti
/// po mřížce, okolí po vzorcích. Počítá se jednou, při otevření dialogu.</para>
/// </summary>
public static class LandingSiteFinder
{
    /// <summary>Jak daleko od počátku se hledá.</summary>
    private const int SearchRadius = 160;

    /// <summary>Krok mřížky kandidátů.</summary>
    private const int Stride = 8;

    /// <summary>Okolí, které se hodnotí.</summary>
    private const int Surroundings = 12;

    /// <summary>Nejmenší vzdálenost mezi nabídnutými místy.</summary>
    private const int MinSeparation = 48;

    /// <summary>
    /// Nabídne až <paramref name="count"/> míst seřazených od nejlepšího.
    /// Prázdný seznam = modul nikam nesedne (chybná data světa).
    /// </summary>
    public static IReadOnlyList<(int X, int Y)> Find(GameContent content, ITerrain terrain, int count = 3)
    {
        int module = content.World.LandingModuleIndex;
        if (module < 0)
        {
            return Array.Empty<(int, int)>();
        }

        var def = content.Buildings[module];
        var scored = new List<(int X, int Y, int Score)>();
        for (int y = -SearchRadius; y <= SearchRadius; y += Stride)
        {
            for (int x = -SearchRadius; x <= SearchRadius; x += Stride)
            {
                if (!Fits(def, terrain, x, y))
                {
                    continue;
                }

                // Blíž k počátku mírně lepší: kamera i mapa galaxie začínají tam.
                int distancePenalty = (Math.Abs(x) + Math.Abs(y)) / 40;
                scored.Add((x, y, Score(content, terrain, x, y) - distancePenalty));
            }
        }

        var chosen = new List<(int X, int Y)>();
        foreach (var candidate in scored.OrderByDescending(c => c.Score).ThenBy(c => Math.Abs(c.X) + Math.Abs(c.Y)))
        {
            if (chosen.All(c => Math.Abs(c.X - candidate.X) + Math.Abs(c.Y - candidate.Y) >= MinSeparation))
            {
                chosen.Add((candidate.X, candidate.Y));
                if (chosen.Count == count)
                {
                    break;
                }
            }
        }

        return chosen;
    }

    private static bool Fits(BuildingDef def, ITerrain terrain, int x, int y)
    {
        for (int ty = y; ty < y + def.FootprintHeight; ty++)
        {
            for (int tx = x; tx < x + def.FootprintWidth; tx++)
            {
                if (!def.IsBiomeAllowed(terrain.BiomeAt(tx, ty)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Pestrost okolí: počet různých biomů souše × 10 + podíl souše.</summary>
    private static int Score(GameContent content, ITerrain terrain, int x, int y)
    {
        Span<bool> seen = stackalloc bool[Math.Min(256, content.Biomes.Count)];
        int kinds = 0;
        int land = 0;
        int samples = 0;
        for (int dy = -Surroundings; dy <= Surroundings; dy += 3)
        {
            for (int dx = -Surroundings; dx <= Surroundings; dx += 3)
            {
                byte biome = terrain.BiomeAt(x + dx, y + dy);
                samples++;
                if (content.Biomes[biome].IsWater)
                {
                    continue;
                }

                land++;
                if (biome < seen.Length && !seen[biome])
                {
                    seen[biome] = true;
                    kinds++;
                }
            }
        }

        return kinds * 10 + land * 10 / Math.Max(1, samples);
    }
}
