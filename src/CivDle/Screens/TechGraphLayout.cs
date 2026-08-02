using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do <b>hvězdice</b>: kořen uprostřed, každá další
/// technologie na prstenci podle toho, jak daleko je od kořene. Strom se čte
/// jako výbuch od středu ven.
///
/// <para>Klíč k tomu, aby se hrany nekřížily, není v rozvržení, ale v datech:
/// hvězdice je bez křížení <b>jen když je graf strom</b> (každý uzel má nejvýš
/// jednoho rodiče). Předchozí verze byla hvězdice nad obecným grafem a křížení
/// do ní byla zabudovaná — technologie s prerekvizitami ve dvou ramenech musela
/// nutně vést spoj přes celý kruh; proto se tehdy ustoupilo k vrstvám. Teď je
/// <c>tech.json</c> skutečný strom, takže hvězdice funguje: každému podstromu
/// patří vlastní úhlová výseč a hrany z ní nikdy nevystoupí.</para>
///
/// <para>Šířka výseče je úměrná <b>počtu listů</b> podstromu, ne počtu dětí —
/// jinak by hubená větev dostala stejný prostor jako celá průmyslová éra
/// a uzly na okraji by se slily.</para>
///
/// <para>Uzel s víc rodiči (mod, budoucí data) se hvězdici nerozbije: bere se
/// první prerekvizita jako rodič ve stromu, zbytek se nakreslí jako hrana navíc.
/// Ta se křížit může — je to daň za data, která stromem nejsou.</para>
///
/// <para>Cyklus v datech ošetří strážce v procházení: uzel, který už na zásobníku
/// je, se přeskočí, místo aby se výpočet zacyklil.</para>
/// </summary>
public sealed class TechGraphLayout
{
    /// <summary>Průměr jádra hvězdy v pixelech.</summary>
    public const int StarSize = 18;

    /// <summary>Klikací (a hover) čtverec kolem hvězdy.</summary>
    public const int HitSize = 52;

    /// <summary>Rozestup prstenců. Vnitřní jsou hustší, vnější potřebují víc místa.</summary>
    private const int RingSpacing = 190;

    /// <summary>Poloměr prvního prstence — kolem kořene musí zbýt místo na popisek.</summary>
    private const int InnerRadius = 150;

    private const int Margin = 170;

    private readonly Vector2[] _centers;
    private readonly int[] _parent;

    public TechGraphLayout(DefRegistry<TechDef> techs)
    {
        int count = techs.Count;
        _centers = new Vector2[count];
        _parent = new int[count];
        if (count == 0)
        {
            Width = Height = 1;
            return;
        }

        var children = BuildTree(techs, _parent);
        int[] leaves = CountLeaves(children, _parent);
        int[] depth = ComputeDepth(_parent);

        int maxDepth = 0;
        for (int i = 0; i < count; i++)
        {
            maxDepth = Math.Max(maxDepth, depth[i]);
        }

        int radius = InnerRadius + maxDepth * RingSpacing;
        Width = Height = 2 * (radius + Margin);
        var origin = new Vector2(Width / 2f, Height / 2f);

        // Kořeny si rozdělí celý kruh podle svých listů; každý pak dělí svou výseč
        // dál mezi děti. Tím je zaručeno, že se podstromy úhlově nepřekryjí.
        int totalLeaves = 0;
        for (int i = 0; i < count; i++)
        {
            if (_parent[i] < 0)
            {
                totalLeaves += leaves[i];
            }
        }

        double angle = -Math.PI / 2; // první rameno míří nahoru, ne doprava
        for (int i = 0; i < count; i++)
        {
            if (_parent[i] >= 0)
            {
                continue;
            }

            double span = totalLeaves > 0 ? 2 * Math.PI * leaves[i] / totalLeaves : 2 * Math.PI;
            Place(i, angle, angle + span, depth, leaves, children, origin);
            angle += span;
        }
    }

    /// <summary>Celková šířka plátna stromu (pro omezení posunu).</summary>
    public int Width { get; }

    /// <summary>Celková výška plátna stromu (pro omezení posunu).</summary>
    public int Height { get; }

    /// <summary>Střed hvězdy v souřadnicích plátna.</summary>
    public Vector2 Center(int techIndex) => _centers[techIndex];

    /// <summary>
    /// Body spojnice mezi prerekvizitou a technologií. V hvězdici je to úsečka —
    /// obě hvězdy leží na sousedních prstencích ve stejné výseči, takže mezi nimi
    /// nic není.
    /// </summary>
    public void AppendEdgePoints(int from, int to, List<Vector2> points)
    {
        points.Clear();
        points.Add(_centers[from]);
        points.Add(_centers[to]);
    }

    /// <summary>Klikací čtverec kolem hvězdy (hit test i culling).</summary>
    public Rectangle Bounds(int techIndex)
    {
        var center = _centers[techIndex];
        return new Rectangle((int)center.X - HitSize / 2, (int)center.Y - HitSize / 2, HitSize, HitSize);
    }

    /// <summary>
    /// Rekurzivně posadí uzel doprostřed jeho výseče a rozdělí ji mezi děti podle
    /// jejich listů.
    /// </summary>
    private void Place(
        int node, double from, double to, int[] depth, int[] leaves,
        List<int>[] children, Vector2 origin)
    {
        double middle = (from + to) / 2;
        double radius = depth[node] == 0 ? 0 : InnerRadius + (depth[node] - 1) * RingSpacing;
        _centers[node] = origin + new Vector2(
            (float)(Math.Cos(middle) * radius),
            (float)(Math.Sin(middle) * radius));

        var kids = children[node];
        if (kids.Count == 0)
        {
            return;
        }

        int total = 0;
        for (int i = 0; i < kids.Count; i++)
        {
            total += leaves[kids[i]];
        }

        double cursor = from;
        for (int i = 0; i < kids.Count; i++)
        {
            double span = total > 0 ? (to - from) * leaves[kids[i]] / total : (to - from) / kids.Count;
            Place(kids[i], cursor, cursor + span, depth, leaves, children, origin);
            cursor += span;
        }
    }

    /// <summary>
    /// Poskládá strom: rodičem je první prerekvizita, ostatní se ignorují (kreslí
    /// se jako hrana navíc). Odkaz mimo rozsah i sebeodkaz se přeskočí.
    /// </summary>
    private static List<int>[] BuildTree(DefRegistry<TechDef> techs, int[] parent)
    {
        int count = techs.Count;
        var children = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            children[i] = new List<int>();
            parent[i] = -1;
        }

        for (int i = 0; i < count; i++)
        {
            var prereqs = techs[i].PrerequisiteIndices;
            if (prereqs.Count == 0)
            {
                continue;
            }

            int first = prereqs[0];
            if (first >= 0 && first < count && first != i)
            {
                parent[i] = first;
                children[first].Add(i);
            }
        }

        BreakCycles(parent, children);
        return children;
    }

    /// <summary>
    /// Rozpojí případný cyklus v datech: uzel, ze kterého se po rodičích nedojde
    /// ke kořeni, se povýší na kořen. Bez toho by rekurze v <see cref="Place"/>
    /// nikdy neskončila.
    /// </summary>
    private static void BreakCycles(int[] parent, List<int>[] children)
    {
        for (int i = 0; i < parent.Length; i++)
        {
            int walker = i;
            for (int steps = 0; walker >= 0 && steps <= parent.Length; steps++)
            {
                if (steps == parent.Length)
                {
                    children[parent[i]].Remove(i);
                    parent[i] = -1;
                    break;
                }

                walker = parent[walker];
            }
        }
    }

    /// <summary>Vzdálenost od kořene (kořen = 0).</summary>
    private static int[] ComputeDepth(int[] parent)
    {
        var depth = new int[parent.Length];
        for (int i = 0; i < parent.Length; i++)
        {
            int steps = 0;
            for (int walker = parent[i]; walker >= 0; walker = parent[walker])
            {
                steps++;
            }

            depth[i] = steps;
        }

        return depth;
    }

    /// <summary>
    /// Počet listů pod každým uzlem — jím se váží šířka úhlové výseče, aby si
    /// velká větev vzala víc místa než slepá ulička o jednom uzlu.
    /// </summary>
    private static int[] CountLeaves(List<int>[] children, int[] parent)
    {
        var leaves = new int[children.Length];
        var depth = ComputeDepth(parent);

        // Od nejhlubších uzlů nahoru: dítě je vždy hlubší než rodič, takže když
        // jdu sestupně podle hloubky, mám součty dětí hotové dřív než rodiče.
        var order = new int[children.Length];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (a, b) => depth[b].CompareTo(depth[a]));
        foreach (int node in order)
        {
            if (children[node].Count == 0)
            {
                leaves[node] = 1;
                continue;
            }

            int sum = 0;
            for (int i = 0; i < children[node].Count; i++)
            {
                sum += leaves[children[node][i]];
            }

            leaves[node] = sum;
        }

        return leaves;
    }
}
