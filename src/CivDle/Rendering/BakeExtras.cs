using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// „Město z výšky" nad mapou hustoty (endgame.md, B4): z béžové šachovnice
/// udělá mapu, na které je poznat, kde je centrum, jaké čtvrti kde leží
/// a kudy vedou hlavní tahy.
///
/// <list type="bullet">
/// <item><b>Čtvrti barvou.</b> Buňka, ve které má většina budov čtvrť jednoho
/// druhu, dostane barvu toho druhu — nebo stylu, který mu hráč přiřadil.</item>
/// <item><b>Výška stínem.</b> Zvlášť upečený reliéf: světlá hrana tam, kde je
/// buňka hustší než soused na straně slunce (vlevo nahoře), stín tam, kde je
/// řidší. Hustá centra tak z mapy vystoupí jako panorama.</item>
/// <item><b>Centra září.</b> V noci svítí okolí těžiště sídel víc, podle
/// hodnosti — z výšky je vidět, kde město žije.</item>
/// <item><b>Hlavní tahy.</b> Buňky se silnicí mimo zástavbu se nakreslí jako
/// cesta: mapa dostane kostru mezi sídly.</item>
/// </list>
///
/// <para>Všechno se peče do textur při změně zástavby, stylů nebo silnic —
/// za snímek to nestojí nic navíc. Pracovní pole se drží přes celý život
/// mapy, žádná alokace při pečení.</para>
/// </summary>
public sealed class BakeExtras
{
    private const int Cells = DensityMap.ChunkCells;

    private static readonly Color Road = new(196, 186, 160);
    private static readonly Color RoadNight = new Color(255, 214, 150) * 0.08f;

    private int[] _votes = Array.Empty<int>();
    private int _types;
    private IReadOnlyList<District> _districts = Array.Empty<District>();
    private readonly DistrictCatalog _catalog;

    /// <param name="catalog">Druhy čtvrtí a jejich styly (barvy z dat).</param>
    public BakeExtras(DistrictCatalog catalog) => _catalog = catalog;

    /// <summary>Upečený reliéf posledního kusu.</summary>
    public Color[] Relief { get; } = new Color[Cells * Cells];

    /// <summary>Kolik silnic je v které buňce (klíč: <see cref="TileKey"/> buňky).</summary>
    public Dictionary<long, int> RoadCells { get; } = new();

    /// <summary>Mění se s každým přepočtem silnic — kusy se podle ní přepečou.</summary>
    public long RoadVersion { get; set; }

    /// <summary>Připraví pečení kusu: vynuluje hlasy čtvrtí a reliéf.</summary>
    public void Begin(Simulation simulation)
    {
        _districts = simulation.Districts;
        _types = _catalog.Types.Count;
        int needed = Cells * Cells * Math.Max(1, _types);
        if (_votes.Length < needed)
        {
            _votes = new int[needed];
        }
        else
        {
            Array.Clear(_votes, 0, needed);
        }

        Array.Clear(Relief);
    }

    /// <summary>Budova v buňce hlasuje za druh své čtvrti.</summary>
    public void Vote(int cell, int districtIndex)
    {
        if (_types == 0 || districtIndex < 0 || districtIndex >= _districts.Count)
        {
            return;
        }

        int type = _districts[districtIndex].TypeIndex;
        if (type >= 0 && type < _types)
        {
            _votes[cell * _types + type]++;
        }
    }

    /// <summary>Je v kusu aspoň jedna silnice? (Kus bez budov se jinak nepeče vůbec.)</summary>
    public bool HasRoadsIn(int originCellX, int originCellY)
    {
        if (RoadCells.Count == 0)
        {
            return false;
        }

        for (int y = 0; y < Cells; y++)
        {
            for (int x = 0; x < Cells; x++)
            {
                if (RoadCells.ContainsKey(TileKey.Pack(originCellX + x, originCellY + y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Dopeče čtvrti, tahy, reliéf a zář center do hotových pixelů.</summary>
    public void Finish(
        Simulation simulation, int originCellX, int originCellY,
        int[] counts, Color[] day, Color[] night)
    {
        for (int y = 0; y < Cells; y++)
        {
            for (int x = 0; x < Cells; x++)
            {
                int i = y * Cells + x;
                int count = counts[i];
                if (count > 0)
                {
                    PaintDistrict(simulation, i, count, day, night);
                }
                else if (RoadCells.GetValueOrDefault(TileKey.Pack(originCellX + x, originCellY + y)) >= 2)
                {
                    day[i] = Road * 0.75f;
                    night[i] = RoadNight;
                }

                Relief[i] = ReliefFor(counts, x, y);
            }
        }

        GlowCentres(simulation, originCellX, originCellY, night);
    }

    private void PaintDistrict(Simulation simulation, int cell, int count, Color[] day, Color[] night)
    {
        int best = -1, bestVotes = 0;
        for (int t = 0; t < _types; t++)
        {
            int votes = _votes[cell * _types + t];
            if (votes > bestVotes)
            {
                best = t;
                bestVotes = votes;
            }
        }

        // Většina, ne jen nejvíc: buňka napůl z domů mimo čtvrť zůstane hustotou.
        if (best < 0 || bestVotes * 2 < count)
        {
            return;
        }

        int style = simulation.DistrictStyleOf(best);
        var styleDef = style >= 0 && style < _catalog.Styles.Count ? _catalog.Styles[style] : null;
        var baseColor = (styleDef?.MapColor ?? _catalog.Types[best].MapColor).ToXna();
        float t01 = MathF.Min(1f, count / (float)DensityMap.DenseCount);
        day[cell] = Color.Lerp(baseColor, Color.White, 0.25f * t01) * (0.55f + (0.35f * t01));

        if (styleDef?.NightColor is { } nightColor)
        {
            float strength = night[cell].A / 255f;
            night[cell] = nightColor.ToXna() * strength;
        }
    }

    /// <summary>
    /// Reliéf buňky: rozdíl hustoty proti sousedovi na straně slunce. Na okraji
    /// kusu se soused bere jako stejný — jinak by na švech kusů vznikly čáry.
    /// </summary>
    private static Color ReliefFor(int[] counts, int x, int y)
    {
        int count = counts[y * Cells + x];
        int neighbour = x > 0 && y > 0 ? counts[(y - 1) * Cells + (x - 1)] : count;
        int difference = count - neighbour;
        if (difference > 0)
        {
            return Color.White * (0.16f * MathF.Min(1f, difference / (float)DensityMap.DenseCount));
        }

        if (difference < 0)
        {
            // Stín vrhá i hustý soused na prázdnou zem — město pak stojí, neleží.
            return Color.Black * (0.28f * MathF.Min(1f, -difference / (float)DensityMap.DenseCount));
        }

        return Color.Transparent;
    }

    /// <summary>
    /// Centra sídel v noci září víc — tím víc, čím vyšší hodnost. Světlo se
    /// jen přidává k tomu, co v buňce už svítí; prázdná buňka dostane jen
    /// slabý nádech, ať centrum nesvítí do polí.
    /// </summary>
    private static void GlowCentres(Simulation simulation, int originCellX, int originCellY, Color[] night)
    {
        var settlements = simulation.Settlements;
        for (int s = 0; s < settlements.Count; s++)
        {
            var settlement = settlements[s];
            int rank = Math.Max(0, settlement.RankIndex);
            float radius = 2f + rank * 1.5f;
            float cx = settlement.CenterX / DensityMap.CellTiles - originCellX;
            float cy = settlement.CenterY / DensityMap.CellTiles - originCellY;
            if (cx + radius < 0 || cy + radius < 0 || cx - radius >= Cells || cy - radius >= Cells)
            {
                continue;
            }

            float boost = 0.12f + 0.05f * rank;
            int minX = Math.Max(0, (int)(cx - radius)), maxX = Math.Min(Cells - 1, (int)(cx + radius));
            int minY = Math.Max(0, (int)(cy - radius)), maxY = Math.Min(Cells - 1, (int)(cy + radius));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float distance = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (distance >= radius)
                    {
                        continue;
                    }

                    int i = y * Cells + x;
                    float current = night[i].A / 255f;
                    float add = boost * (1f - distance / radius) * (current > 0 ? 1f : 0.3f);
                    float next = MathF.Min(1f, current + add);
                    night[i] = current > 0 ? night[i] * (next / current) : Color.White * next;
                }
            }
        }
    }
}
