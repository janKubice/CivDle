using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Rozmístění technologií do <b>hvězdice</b>: kořen uprostřed, každá další
/// technologie na prstenci podle toho, jak daleko je od kořene. Strom se čte
/// jako výbuch od středu ven.
///
/// <para>Aby se hrany nekřížily, musí platit dvě věci najednou. První je
/// v datech: hvězdice unese jen <b>strom</b> (každý uzel nejvýš jeden rodič).
/// Předchozí verze byla hvězdice nad obecným grafem a křížení do ní byla
/// zabudovaná — technologie s prerekvizitami ve dvou ramenech musela vést spoj
/// přes celý kruh; proto se tehdy ustoupilo k vrstvám.</para>
///
/// <para>Druhá je v <b>routování</b>, viz <see cref="AppendEdgePoints"/>. Strom
/// sám o sobě nestačí: rodič sedí uprostřed své výseče, a když je ta výseč
/// široká, rovná tětiva k okrajovému dítěti projede skrz výseče sourozenců.
/// Proto spojnice nevedou rovně, ale po prstenci rodiče a pak paprsčitě ven.</para>
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

    /// <summary>
    /// Nejmenší rozestup prstenců. Skutečný se dopočítá z počtu listů — viz
    /// <see cref="SpacingFor"/>. Kdyby byl pevný, každé přidání pár technologií
    /// by hvězdy na okraji naskládalo na sebe.
    /// </summary>
    private const int MinRingSpacing = 190;

    /// <summary>Rozestup prstenců téhle hvězdice (spočítaný v konstruktoru).</summary>
    private readonly int _ringSpacing;

    /// <summary>Nejmenší poloměr prvního prstence — kolem kořene musí zbýt místo na popisek.</summary>
    private const int MinInnerRadius = 150;

    /// <summary>Poloměr prvního prstence téhle hvězdice (spočítaný v konstruktoru).</summary>
    private readonly int _innerRadius;

    private const int Margin = 170;

    private readonly Vector2[] _centers;
    private readonly int[] _parent;

    /// <summary>Úhel hvězdy ve výseči — spojnice ho potřebují, aby vedly radiálně.</summary>
    private readonly double[] _angle;

    /// <summary>Prstenec hvězdy; spojnice z něj počítá mezikruží pro oblouk.</summary>
    private readonly int[] _depth;

    private Vector2 _origin;

    public TechGraphLayout(DefRegistry<TechDef> techs)
    {
        int count = techs.Count;
        _centers = new Vector2[count];
        _parent = new int[count];
        _angle = new double[count];
        _depth = new int[count];
        if (count == 0)
        {
            Width = Height = 1;
            return;
        }

        var children = BuildTree(techs, _parent);
        int[] leaves = CountLeaves(children, _parent);
        int[] depth = ComputeDepth(_parent);
        Array.Copy(depth, _depth, count);

        int maxDepth = 0;
        for (int i = 0; i < count; i++)
        {
            maxDepth = Math.Max(maxDepth, depth[i]);
        }

        int totalLeavesForSpacing = 0;
        for (int i = 0; i < count; i++)
        {
            if (_parent[i] < 0)
            {
                totalLeavesForSpacing += leaves[i];
            }
        }

        _innerRadius = InnerRadiusFor(totalLeavesForSpacing);
        _ringSpacing = MinRingSpacing;
        int radius = _innerRadius + maxDepth * _ringSpacing;
        Width = Height = 2 * (radius + Margin);
        var origin = new Vector2(Width / 2f, Height / 2f);
        _origin = origin;

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
    /// Body spojnice mezi prerekvizitou a technologií: <b>radiálně ven — obloukem
    /// — radiálně ven</b>.
    ///
    /// <para>Rovná tětiva mezi prstenci vypadá lákavě, ale kříží se. Úsečka mezi
    /// dvěma body na různých poloměrech se vybouluje ven až k poloměru toho
    /// vzdálenějšího, takže u široké výseče projede pásmem, kde už sedí vnukové
    /// jiné větve — přesně tohle dělalo v hvězdici pavučinu.</para>
    ///
    /// <para>Lomená spojnice to vylučuje z principu: obě radiální části leží na
    /// úhlu své hvězdy (a ten je uvnitř výseče rodiče), oblouk vede v mezikruží
    /// mezi prstenci a taky nevystoupí z výseče rodiče. Výseče sourozenců jsou
    /// disjunktní a různé hloubky mají různá mezikruží — dvě spojnice tedy nemají
    /// kde se potkat.</para>
    /// </summary>
    public void AppendEdgePoints(int from, int to, List<Vector2> points)
    {
        points.Clear();
        points.Add(_centers[from]);

        float ringRadius = RadiusAt(_depth[from]);
        double fromAngle = _angle[from];
        double toAngle = _angle[to];

        // Kořen leží ve středu, kde úhel nic neznamená — z něj vede rovná paprsčitá
        // čára. Dítě přesně na úhlu rodiče je taky jen paprsek.
        if (ringRadius > 0f && Math.Abs(toAngle - fromAngle) > 1e-6)
        {
            // Vrcholy oblouku sedí na SPOLEČNÉ úhlové mřížce. Sourozenecké oblouky
            // se na společném prstenci nutně překrývají; kdyby si každý dělil svůj
            // rozsah po svém, lámaly by se v jiných bodech a vzájemně se protínaly.
            // Na mřížce jsou překryté úseky totožné, takže se protnout nemají kde.
            double step = toAngle > fromAngle ? ArcStep : -ArcStep;
            for (double angle = Snap(fromAngle, step);
                 (toAngle - angle) * step > 0;
                 angle += step)
            {
                points.Add(PointAt(angle, ringRadius));
            }

            points.Add(PointAt(toAngle, ringRadius));
        }

        points.Add(_centers[to]);
    }

    /// <summary>Úhlová mřížka oblouku (~2°) — jemnější už oko nerozezná, jen přibudou body.</summary>
    private const double ArcStep = Math.PI / 90;

    /// <summary>První bod mřížky za daným úhlem ve směru kroku.</summary>
    private static double Snap(double angle, double step) =>
        step > 0
            ? Math.Ceiling(angle / ArcStep + 1e-9) * ArcStep
            : Math.Floor(angle / ArcStep - 1e-9) * ArcStep;

    private Vector2 PointAt(double angle, float radius) => _origin + new Vector2(
        (float)(Math.Cos(angle) * radius),
        (float)(Math.Sin(angle) * radius));

    private float RadiusAt(int depth) => depth == 0 ? 0f : _innerRadius + (depth - 1) * _ringSpacing;

    /// <summary>
    /// Jak velký musí být PRVNÍ prstenec, aby se žádné dvě hvězdy nedotýkaly.
    ///
    /// <para>Úhlová šířka uzlu je úměrná jeho listům, takže na jeden list připadá
    /// oblouk <c>2πR / listy</c>. Nejtěsněji je proto tam, kde list sedí
    /// <b>nejblíž středu</b> — ne na nejzazším prstenci, jak by se čekalo.
    /// (Tuhle úvahu jsem měl napoprvé obráceně a rozestup prstenců problém
    /// neřešil: sourozenci <c>ledgers</c> a <c>market_scales</c> na druhém
    /// prstenci zůstali 33 px od sebe.)</para>
    ///
    /// <para>Když tedy vyjde první prstenec dost velký, jsou v pořádku i všechny
    /// další — a strom snese libovolný počet technologií, aniž by se okraj slil.</para>
    /// </summary>
    private static int InnerRadiusFor(int totalLeaves)
    {
        const float perLeaf = StarSize * 2.6f; // hvězda a k tomu dýchací prostor
        return Math.Max(MinInnerRadius, (int)MathF.Ceiling(totalLeaves * perLeaf / MathF.Tau));
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
        double radius = RadiusAt(depth[node]);
        _angle[node] = middle;
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
