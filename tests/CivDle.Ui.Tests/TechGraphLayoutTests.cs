using CivDle.Core.Content;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozvržení stromu technologií je čistý výpočet — testuje se bez okna
/// i grafického zařízení. Hráčova stížnost byla „moc překřížených spojů", takže
/// se křížení přímo měří; jinak by se dalo jen doufat, že to vypadá líp.
/// </summary>
public sealed class TechGraphLayoutTests
{
    [Fact]
    public void RealContent_StarsNeverOverlap()
    {
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        for (int a = 0; a < techs.Count; a++)
        {
            for (int b = a + 1; b < techs.Count; b++)
            {
                float distance = Vector2.Distance(layout.Center(a), layout.Center(b));
                Assert.True(distance >= TechGraphLayout.StarSize * 2,
                    $"Hvězdy {techs[a].Id} a {techs[b].Id} jsou na sobě ({distance:0.#} px).");
            }
        }
    }

    [Fact]
    public void Prerequisite_SitsToTheLeftOfItsSuccessor()
    {
        // Základ vrstveného rozvržení: hrana vede vždycky doprava, nikdy zpátky.
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        for (int i = 0; i < techs.Count; i++)
        {
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                Assert.True(layout.Center(prereq).X < layout.Center(i).X,
                    $"{techs[prereq].Id} musí ležet vlevo od {techs[i].Id}.");
            }
        }
    }

    [Fact]
    public void RealContent_HasFewCrossings()
    {
        // Naměřeno na ostrém obsahu: naivní pořadí ~175 křížení, vrstvené
        // rozvržení s mediánem a prohazováním ~61. Práh je nad tím s rezervou —
        // hlídá návrat k „pavouku", neladí heuristiku na jednotku přesně.
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        int crossings = CountCrossings(techs, new RealLayout(layout));

        Assert.True(crossings <= 80, $"Spojů se kříží {crossings} (naivní pořadí: {CountCrossings(techs, new FileOrderLayout(techs))}).");
    }

    [Fact]
    public void Barycentre_BeatsNaiveOrdering()
    {
        // Kontrola, že heuristika opravdu něco dělá: proti pořadí ze souboru
        // (uzly ve sloupci tak, jak přišly) musí být křížení míň.
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        int smart = CountCrossings(techs, new RealLayout(layout));
        int naive = CountCrossings(techs, new FileOrderLayout(techs));

        Assert.True(smart < naive, $"Barycentrické řazení nepomohlo: {smart} vs {naive} křížení.");
    }

    [Fact]
    public void SingleTech_GetsAValidCanvas()
    {
        var layout = new TechGraphLayout(Registry(Tech("root")));

        Assert.True(layout.Width > 0 && layout.Height > 0);
        Assert.True(layout.Center(0).X > 0 && layout.Center(0).Y > 0);
    }

    [Fact]
    public void EmptyTree_DoesNotThrow()
    {
        var layout = new TechGraphLayout(Registry());

        Assert.True(layout.Width > 0 && layout.Height > 0);
    }

    /// <summary>Cyklus v datech nesmí rozvržení zacyklit — strážce ho položí do prvního sloupce.</summary>
    [Fact]
    public void CyclicPrerequisites_DoNotHang()
    {
        var layout = new TechGraphLayout(Registry(Tech("a", 1), Tech("b", 0)));

        Assert.NotEqual(layout.Center(0), layout.Center(1));
    }

    /// <summary>
    /// Spočítá, kolikrát se dvě spojnice protnou. Bere úseky tak, jak se opravdu
    /// kreslí — u dlouhých hran tedy včetně bodů lomu, jinak by test měřil jiný
    /// obrázek, než hráč vidí.
    /// </summary>
    private static int CountCrossings(DefRegistry<TechDef> techs, ILayout layout)
    {
        var edges = new List<(Vector2 A, Vector2 B)>();
        var points = new List<Vector2>();
        for (int i = 0; i < techs.Count; i++)
        {
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                layout.AppendEdgePoints(prereq, i, points);
                for (int p = 0; p + 1 < points.Count; p++)
                {
                    edges.Add((points[p], points[p + 1]));
                }
            }
        }

        int crossings = 0;
        for (int a = 0; a < edges.Count; a++)
        {
            for (int b = a + 1; b < edges.Count; b++)
            {
                // Hrany se společným koncem se nepočítají — ty se protnout musí.
                if (SharesEndpoint(edges[a], edges[b]))
                {
                    continue;
                }

                if (SegmentsIntersect(edges[a].A, edges[a].B, edges[b].A, edges[b].B))
                {
                    crossings++;
                }
            }
        }

        return crossings;
    }

    private static bool SharesEndpoint((Vector2 A, Vector2 B) first, (Vector2 A, Vector2 B) second) =>
        first.A == second.A || first.A == second.B || first.B == second.A || first.B == second.B;

    private static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float d1 = Cross(p3, p4, p1);
        float d2 = Cross(p3, p4, p2);
        float d3 = Cross(p1, p2, p3);
        float d4 = Cross(p1, p2, p4);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
            && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 p) =>
        (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

    private interface ILayout
    {
        Vector2 Center(int index);

        void AppendEdgePoints(int from, int to, List<Vector2> points);
    }

    private sealed class RealLayout : ILayout
    {
        private readonly TechGraphLayout _layout;

        public RealLayout(TechGraphLayout layout) => _layout = layout;

        public Vector2 Center(int index) => _layout.Center(index);

        public void AppendEdgePoints(int from, int to, List<Vector2> points) =>
            _layout.AppendEdgePoints(from, to, points);
    }

    /// <summary>Srovnávací základ: sloupce podle hloubky, ale pořadí uvnitř ze souboru.</summary>
    private sealed class FileOrderLayout : ILayout
    {
        private readonly Vector2[] _centers;

        public FileOrderLayout(DefRegistry<TechDef> techs)
        {
            int count = techs.Count;
            _centers = new Vector2[count];
            var depth = new int[count];
            for (int i = 0; i < count; i++)
            {
                depth[i] = Depth(techs, i, new bool[count]);
            }

            var used = new Dictionary<int, int>();
            for (int i = 0; i < count; i++)
            {
                int row = used.GetValueOrDefault(depth[i]);
                used[depth[i]] = row + 1;
                _centers[i] = new Vector2(depth[i] * 230, row * 96);
            }
        }

        public Vector2 Center(int index) => _centers[index];

        /// <summary>Naivní základ kreslí rovně — právě to je ta varianta, se kterou se porovnává.</summary>
        public void AppendEdgePoints(int from, int to, List<Vector2> points)
        {
            points.Clear();
            points.Add(_centers[from]);
            points.Add(_centers[to]);
        }

        private static int Depth(DefRegistry<TechDef> techs, int index, bool[] visiting)
        {
            if (visiting[index])
            {
                return 0;
            }

            visiting[index] = true;
            int best = 0;
            foreach (int prereq in techs[index].PrerequisiteIndices)
            {
                best = Math.Max(best, Depth(techs, prereq, visiting) + 1);
            }

            visiting[index] = false;
            return best;
        }
    }

    private static DefRegistry<TechDef> LoadRealTechs() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data")).Techs;

    private static DefRegistry<TechDef> Registry(params TechDef[] techs) =>
        new(techs, t => t.Id, "technologie", allowEmpty: true);

    private static TechDef Tech(string id, params int[] prerequisites) => new(
        id,
        Array.Empty<ResourceAmount>(),
        prerequisites,
        Array.Empty<int>());
}
