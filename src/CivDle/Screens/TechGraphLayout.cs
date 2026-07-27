using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do <b>sloupců podle hloubky</b>: vlevo to, co jde
/// vyzkoumat hned, vpravo to, co má za sebou nejdelší řetěz prerekvizit.
/// Spoje tak vedou zleva doprava a strom se čte jako postup v čase.
///
/// <para>Předchozí verze byla hvězdice. Vypadala hezky, ale křížení do ní byla
/// zabudovaná: technologie s prerekvizitami ve dvou různých ramenech musela
/// nutně vést spoj přes celý kruh. Vrstvené rozvržení tenhle problém nemá —
/// hrana jde vždy o sloupec doprava a kříží se nanejvýš v mezeře mezi dvěma
/// sousedními sloupci.</para>
///
/// <para>Pořadí uvnitř sloupce hledá <b>barycentrická heuristika</b>: uzel se
/// opakovaně stěhuje k průměrné výšce svých sousedů, střídavě podle prerekvizit
/// a podle toho, co z něj vychází. Je to standardní postup pro vrstvené grafy
/// (Sugiyama) a pár průchodů stačí.</para>
///
/// <para>Cyklus v datech ošetří strážce ve výpočtu hloubky: uzel skončí v prvním
/// sloupci, místo aby se výpočet zacyklil.</para>
/// </summary>
public sealed class TechGraphLayout
{
    /// <summary>Průměr jádra hvězdy v pixelech.</summary>
    public const int StarSize = 18;

    /// <summary>Klikací (a hover) čtverec kolem hvězdy.</summary>
    public const int HitSize = 52;

    private const int ColumnSpacing = 230;
    private const int RowSpacing = 96;
    private const int Margin = 150; // místo na popisky u krajních hvězd
    private const int BarycentreSweeps = 12;
    private const int TransposePasses = 6;

    private readonly Vector2[] _centers;
    private readonly LayeredOrdering? _ordering;

    public TechGraphLayout(DefRegistry<TechDef> techs)
    {
        int count = techs.Count;
        _centers = new Vector2[count];
        if (count == 0)
        {
            Width = Height = 1;
            return;
        }

        var depth = new int[count];
        var state = new byte[count];
        for (int i = 0; i < count; i++)
        {
            depth[i] = ComputeDepth(techs, i, depth, state);
        }

        var edges = new List<(int From, int To)>();
        for (int i = 0; i < count; i++)
        {
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                if (prereq >= 0 && prereq < count && depth[prereq] < depth[i])
                {
                    edges.Add((prereq, i));
                }
            }
        }

        var ordering = new LayeredOrdering(depth, edges);
        _ordering = ordering;

        int columnCount = 0;
        for (int i = 0; i < count; i++)
        {
            columnCount = Math.Max(columnCount, depth[i] + 1);
        }

        Width = Margin * 2 + Math.Max(1, columnCount - 1) * ColumnSpacing;
        Height = Margin * 2 + (ordering.WidestLayer - 1) * RowSpacing;

        for (int i = 0; i < count; i++)
        {
            // Vrstvy se svisle centrují, aby úzký sloupec nevisel u horního okraje.
            float offset = (Height - (ordering.LayerSizeOf(i) - 1) * RowSpacing) / 2f;
            _centers[i] = new Vector2(Margin + depth[i] * ColumnSpacing, offset + ordering.RowOf(i) * RowSpacing);
        }
    }

    /// <summary>Celková šířka plátna stromu (pro omezení posunu).</summary>
    public int Width { get; }

    /// <summary>Celková výška plátna stromu (pro omezení posunu).</summary>
    public int Height { get; }

    /// <summary>Střed hvězdy v souřadnicích plátna.</summary>
    public Vector2 Center(int techIndex) => _centers[techIndex];

    /// <summary>
    /// Lomené body spojnice mezi prerekvizitou a technologií. Dlouhá hrana se
    /// kreslí přes ně, takže vede tudy, kudy ji rozvržení protáhlo — rovná čára
    /// napříč několika sloupci by křížila všechno, čemu se řazení vyhnulo.
    /// </summary>
    public void AppendEdgePoints(int from, int to, List<Vector2> points)
    {
        points.Clear();
        points.Add(_centers[from]);
        if (_ordering is not null)
        {
            foreach (int dummy in _ordering.WaypointsOf(from, to))
            {
                float offset = (Height - (_ordering.LayerSizeOf(dummy) - 1) * RowSpacing) / 2f;
                points.Add(new Vector2(
                    Margin + _ordering.LayerOf(dummy) * ColumnSpacing,
                    offset + _ordering.RowOf(dummy) * RowSpacing));
            }
        }

        points.Add(_centers[to]);
    }

    /// <summary>Klikací čtverec kolem hvězdy (hit test i culling).</summary>
    public Rectangle Bounds(int techIndex)
    {
        var center = _centers[techIndex];
        return new Rectangle((int)center.X - HitSize / 2, (int)center.Y - HitSize / 2, HitSize, HitSize);
    }

    /// <summary>
    /// Hloubka = nejdelší řetěz prerekvizit. Strážce (<c>state</c>) drží uzly,
    /// které se zrovna počítají — cyklus v datech tak skončí hloubkou 0 místo
    /// nekonečné rekurze.
    /// </summary>
    private static int ComputeDepth(DefRegistry<TechDef> techs, int index, int[] depth, byte[] state)
    {
        if (state[index] == 2)
        {
            return depth[index];
        }

        if (state[index] == 1)
        {
            return 0; // cyklus — ber jako kořen
        }

        state[index] = 1;
        int best = 0;
        foreach (int prereq in techs[index].PrerequisiteIndices)
        {
            best = Math.Max(best, ComputeDepth(techs, prereq, depth, state) + 1);
        }

        depth[index] = best;
        state[index] = 2;
        return best;
    }
}
