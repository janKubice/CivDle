using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do <b>hvězdice</b>: každá kořenová technologie dostane
/// svou VÝSEČ a všechno, co z ní vyrůstá, zůstane uvnitř téhle výseče. Vzniknou
/// tak čitelná ramena — jedna větev je jedna oblast obrazovky.
///
/// <para>Předchozí verze rozsazovala uzly po prstencích rovnoměrně a řadila je jen
/// podle úhlu prerekvizit. Výsledek vypadal „na přeskáčku": navazující technologie
/// skončila na opačné straně kruhu než ta, ze které vychází, a spojnice se křížily
/// přes celý strom.</para>
///
/// <para>Jak to funguje: závislosti tvoří DAG, ale pro rozvržení se z něj udělá
/// STROM — každý uzel si vybere jednoho hlavního předchůdce (ten nejmělčí).
/// Ostatní hrany se pořád kreslí, jen neurčují polohu. Výseč se dělí mezi potomky
/// podle toho, kolik listů pod nimi visí, takže velké větve dostanou víc místa.</para>
///
/// <para>Poloměr roste s hloubkou, takže prerekvizita leží vždycky blíž středu než
/// to, co z ní vychází. Cyklus v datech ošetří strážce — uzel skončí jako kořen,
/// místo aby výpočet zacyklil.</para>
/// </summary>
public sealed class TechGraphLayout
{
    /// <summary>Průměr jádra hvězdy v pixelech.</summary>
    public const int StarSize = 18;

    /// <summary>Klikací (a hover) čtverec kolem hvězdy.</summary>
    public const int HitSize = 52;

    private const int FirstRing = 190;
    private const int RingSpacing = 175;
    private const int Margin = 150; // místo na popisky u krajních hvězd

    private readonly Vector2[] _centers;

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

        // Kostra pro rozvržení: každý uzel si vezme nejmělčího předchůdce.
        var children = new List<int>[count];
        var roots = new List<int>();
        for (int i = 0; i < count; i++)
        {
            children[i] = new List<int>();
        }

        for (int i = 0; i < count; i++)
        {
            int parent = PrimaryParent(techs, i, depth);
            if (parent < 0)
            {
                roots.Add(i);
            }
            else
            {
                children[parent].Add(i);
            }
        }

        // Cyklus v datech může sníst všechny kořeny (a ↔ b si ukazují navzájem).
        // Kostra se pak postaví znovu od nuly — jen připsat potomky by nechalo
        // původní hrany na místě a rekurze by se zacyklila.
        if (roots.Count == 0)
        {
            foreach (var list in children)
            {
                list.Clear();
            }

            roots.Add(0);
            children[0].AddRange(Enumerable.Range(1, count - 1));
        }

        // Váha větve = kolik listů pod ní visí. Široká větev dostane širší výseč.
        var weight = new int[count];
        foreach (int root in roots)
        {
            ComputeWeight(root, children, weight);
        }

        // Jediný kořen sedí přesně ve středu a jeho potomci se rozprostřou kolem;
        // víc kořenů si rozdělí kruh na výseče podle váhy.
        bool singleRoot = roots.Count == 1;
        int totalWeight = roots.Sum(r => weight[r]);
        float angle = 0f;
        foreach (int root in roots)
        {
            float span = MathHelper.TwoPi * weight[root] / Math.Max(1, totalWeight);
            Place(root, children, weight, depth, angle, span, atCentre: singleRoot);
            angle += span;
        }

        float maxRadius = 0f;
        for (int i = 0; i < count; i++)
        {
            maxRadius = Math.Max(maxRadius, _centers[i].Length());
        }

        int side = (int)(maxRadius + Margin) * 2;
        Width = Height = Math.Max(side, 1);

        var origin = new Vector2(side / 2f, side / 2f);
        for (int i = 0; i < count; i++)
        {
            _centers[i] += origin;
        }
    }

    /// <summary>Celková šířka plátna hvězdice (pro omezení posunu).</summary>
    public int Width { get; }

    /// <summary>Celková výška plátna hvězdice (pro omezení posunu).</summary>
    public int Height { get; }

    /// <summary>Střed hvězdy v souřadnicích plátna.</summary>
    public Vector2 Center(int techIndex) => _centers[techIndex];

    /// <summary>Klikací čtverec kolem hvězdy (hit test i culling).</summary>
    public Rectangle Bounds(int techIndex)
    {
        var center = _centers[techIndex];
        return new Rectangle((int)center.X - HitSize / 2, (int)center.Y - HitSize / 2, HitSize, HitSize);
    }

    /// <summary>
    /// Posadí uzel doprostřed jeho výseče a rozdělí ji mezi potomky podle jejich
    /// váhy. Rekurze je mělká (hloubka stromu), takže si ji můžeme dovolit.
    /// </summary>
    private void Place(
        int index, List<int>[] children, int[] weight, int[] depth,
        float startAngle, float span, bool atCentre = false)
    {
        float middle = startAngle + span / 2f;
        float radius = depth[index] == 0
            ? (atCentre ? 0f : FirstRing * 0.45f) // jediný kořen doprostřed, jinak těsný věnec
            : FirstRing + (depth[index] - 1) * RingSpacing;

        _centers[index] = new Vector2(MathF.Cos(middle), MathF.Sin(middle)) * radius;

        int childWeight = children[index].Sum(c => weight[c]);
        float childAngle = startAngle;
        foreach (int child in children[index])
        {
            float childSpan = span * weight[child] / Math.Max(1, childWeight);
            Place(child, children, weight, depth, childAngle, childSpan);
            childAngle += childSpan;
        }
    }

    /// <summary>
    /// Počet listů pod uzlem (list sám má váhu 1). Váha se zapisuje PŘED sestupem
    /// do potomků — chybná data se smyčkou by jinak rekurzi zacyklila a přetekl by
    /// zásobník (což se při psaní téhle verze i stalo).
    /// </summary>
    private static int ComputeWeight(int index, List<int>[] children, int[] weight)
    {
        if (weight[index] > 0)
        {
            return weight[index];
        }

        weight[index] = 1; // strážce proti smyčce
        int sum = 0;
        foreach (int child in children[index])
        {
            sum += ComputeWeight(child, children, weight);
        }

        weight[index] = Math.Max(1, sum);
        return weight[index];
    }

    /// <summary>
    /// Hlavní předchůdce pro rozvržení: ten nejmělčí. Uzel tak visí co nejblíž
    /// začátku své větve, místo aby ho zatáhla nějaká pozdní vedlejší závislost.
    /// </summary>
    private static int PrimaryParent(DefRegistry<TechDef> techs, int index, int[] depth)
    {
        int best = -1;
        foreach (int prereq in techs[index].PrerequisiteIndices)
        {
            if (prereq != index && (best < 0 || depth[prereq] < depth[best]))
            {
                best = prereq;
            }
        }

        return best;
    }

    private static int ComputeDepth(DefRegistry<TechDef> techs, int index, int[] depth, byte[] state)
    {
        if (state[index] == 2)
        {
            return depth[index];
        }

        if (state[index] == 1)
        {
            return 0; // strážce cyklu — chybná data nesmí zacyklit vykreslení
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
