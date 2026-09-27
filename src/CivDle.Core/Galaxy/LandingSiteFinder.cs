using CivDle.Core.Content;
using CivDle.Core.Sim;
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
/// <para><b>Svět s erupcemi</b> (Výheň): přednost mají místa „pod sopkou" —
/// láva z nejbližšího průduchu teče kolem, blízko, ale ne přes modul. Jinak
/// by jedna mapa dostala město na okraji kráteru a jiná by lávu u města
/// neviděla celou hodinu; pravidlo světa má tlačit brzy a u každého startu
/// stejně (svety-design.md 3.3).</para>
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
    /// Průduch blíž než tohle je „na dohled": první láva by zalila modul dřív,
    /// než hráč pochopí, co se děje.
    /// </summary>
    private const int VentMinDistance = 12;

    /// <summary>Láva blíž k modulu než tohle by ho zalila hned první erupcí.</summary>
    private const int LavaMinDistance = 5;

    /// <summary>Láva dál než tohle se k rostoucímu městu dostane až po hodině.</summary>
    private const int LavaMaxDistance = 14;

    /// <summary>Krok, po kterém se hledají průduchy (skvrna průduchu je širší).</summary>
    private const int VentStride = 2;

    /// <summary>Přednost místa pod průduchem — přebije pestrost okolí.</summary>
    private const int UnderVentBonus = 200;

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
        var eruption = content.Hazards.EruptionIndex >= 0 ? content.Hazards.Hazards[content.Hazards.EruptionIndex].Eruption : null;
        var vents = eruption is null ? null : FindVents(terrain, eruption);
        var lavaPaths = new Dictionary<(int, int), List<long>>();
        var scored = new List<(int X, int Y, int Score)>();
        for (int y = -SearchRadius; y <= SearchRadius; y += Stride)
        {
            for (int x = -SearchRadius; x <= SearchRadius; x += Stride)
            {
                if (!Fits(content, def, terrain, x, y))
                {
                    continue;
                }

                // Blíž k počátku mírně lepší: kamera i mapa galaxie začínají tam.
                int distancePenalty = (Math.Abs(x) + Math.Abs(y)) / 40;
                int score = Score(content, terrain, x, y) - distancePenalty;
                if (vents is not null && IsUnderVent(content, terrain, vents, lavaPaths, eruption!, x, y))
                {
                    score += UnderVentBonus;
                }

                scored.Add((x, y, score));
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

    /// <summary>Průduchy v celé oblasti hledání (i s okrajem dosahu jevu).</summary>
    private static List<(int X, int Y)> FindVents(ITerrain terrain, EruptionRule rule)
    {
        var vents = new List<(int X, int Y)>();
        int reach = SearchRadius + rule.SearchRadius;
        for (int y = -reach; y <= reach; y += VentStride)
        {
            for (int x = -reach; x <= reach; x += VentStride)
            {
                if (terrain.BiomeAt(x, y) == rule.VentBiomeIndex)
                {
                    vents.Add((x, y));
                }
            }
        }

        return vents;
    }

    /// <summary>
    /// Místo pod sopkou: nejbližší průduch (ten, ze kterého poteče láva) leží
    /// v dosahu jevu a ne na dohled, a jeho láva po holém terénu proteče
    /// blízko modulu, ale ne přes něj. Dráhy se počítají jednou na průduch.
    /// </summary>
    private static bool IsUnderVent(GameContent content, ITerrain terrain, List<(int X, int Y)> vents,
        Dictionary<(int, int), List<long>> lavaPaths, EruptionRule rule, int x, int y)
    {
        long best = long.MaxValue;
        (int X, int Y) nearest = default;
        foreach (var vent in vents)
        {
            long d = (long)(vent.X - x) * (vent.X - x) + (long)(vent.Y - y) * (vent.Y - y);
            if (d < best)
            {
                best = d;
                nearest = vent;
            }
        }

        long reach = (long)rule.SearchRadius * rule.SearchRadius;
        if (best < (long)VentMinDistance * VentMinDistance || best > reach)
        {
            return false;
        }

        if (!lavaPaths.TryGetValue(nearest, out var path))
        {
            path = new List<long>();
            LavaFlow.Trace(new TerrainLavaGround(terrain, content.Biomes, rule), rule.FlowLength, nearest.X, nearest.Y, path);
            lavaPaths[nearest] = path;
        }

        int closest = int.MaxValue;
        foreach (long tile in path)
        {
            int d = Math.Max(Math.Abs(TileKey.X(tile) - x), Math.Abs(TileKey.Y(tile) - y));
            closest = Math.Min(closest, d);
        }

        return closest is >= LavaMinDistance and <= LavaMaxDistance;
    }

    /// <summary>
    /// Sedne modul na místo? Na světě bez země (Nebesa) stačí oblaka, ze kterých
    /// loď udělá palubu (<see cref="PlatformDef"/>).
    /// </summary>
    private static bool Fits(GameContent content, BuildingDef def, ITerrain terrain, int x, int y)
    {
        var platform = content.World.Platform;
        for (int ty = y; ty < y + def.FootprintHeight; ty++)
        {
            for (int tx = x; tx < x + def.FootprintWidth; tx++)
            {
                byte biome = terrain.BiomeAt(tx, ty);
                if (!def.IsBiomeAllowed(biome)
                    && (platform is null || !content.Terraform[platform.TerraformIndex].AppliesTo(biome)))
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
