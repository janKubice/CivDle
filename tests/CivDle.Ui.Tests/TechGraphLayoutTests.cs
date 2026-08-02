using CivDle.Core.Content;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozvržení stromu technologií je čistý výpočet — testuje se bez okna
/// i grafického zařízení.
///
/// <para>Hvězdice je bez křížení jen tehdy, když jsou data strom. Proto se tu
/// hlídá obojí: že <c>tech.json</c> stromem <b>je</b>, a že se pak opravdu
/// nekříží ani jeden spoj. Kdyby někdo přidal technologii se dvěma rodiči,
/// spadne první test a bude hned jasné proč — místo aby se za měsíc někdo divil,
/// odkud se v hvězdici vzal pavouk.</para>
/// </summary>
public sealed class TechGraphLayoutTests
{
    [Fact]
    public void RealContent_IsATree()
    {
        // Podmínka, na které stojí celé rozvržení: každá technologie nejvýš
        // jednoho rodiče, a právě jeden kořen uprostřed hvězdy.
        var techs = LoadRealTechs();

        int roots = 0;
        for (int i = 0; i < techs.Count; i++)
        {
            Assert.True(techs[i].PrerequisiteIndices.Count <= 1,
                $"Technologie '{techs[i].Id}' má {techs[i].PrerequisiteIndices.Count} prerekvizit — "
                + "hvězdice unese jen strom (jeden rodič na uzel).");
            if (techs[i].PrerequisiteIndices.Count == 0)
            {
                roots++;
            }
        }

        Assert.Equal(1, roots);
    }

    [Fact]
    public void RealContent_HasNoCrossingsAtAll()
    {
        // Tohle je celý důvod, proč je strom stromem. Ne „málo křížení" — žádné.
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        Assert.Equal(0, CountCrossings(techs, layout));
    }

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
    public void RealContent_ChildrenSitFurtherFromTheCentre()
    {
        // Hvězdicová obdoba „hrana vede vždycky dopředu": co je vyzkoumatelné
        // dřív, je blíž středu. Jinak by se strom nedal číst jako výbuch od jádra.
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);
        var origin = new Vector2(layout.Width / 2f, layout.Height / 2f);

        for (int i = 0; i < techs.Count; i++)
        {
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                float parent = Vector2.Distance(layout.Center(prereq), origin);
                float child = Vector2.Distance(layout.Center(i), origin);
                Assert.True(child > parent,
                    $"{techs[i].Id} musí ležet dál od středu než {techs[prereq].Id}.");
            }
        }
    }

    [Fact]
    public void TheRootSitsInTheMiddle()
    {
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);

        int root = -1;
        for (int i = 0; i < techs.Count && root < 0; i++)
        {
            if (techs[i].PrerequisiteIndices.Count == 0)
            {
                root = i;
            }
        }

        Assert.Equal(new Vector2(layout.Width / 2f, layout.Height / 2f), layout.Center(root));
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

    /// <summary>Cyklus v datech nesmí rozvržení zacyklit ani zaseknout rekurzi.</summary>
    [Fact]
    public void CyclicPrerequisites_DoNotHang()
    {
        var layout = new TechGraphLayout(Registry(Tech("a", 1), Tech("b", 0)));

        Assert.NotEqual(layout.Center(0), layout.Center(1));
    }

    /// <summary>Uzel s víc rodiči hvězdici nerozbije — bere se první, zbytek je hrana navíc.</summary>
    [Fact]
    public void ExtraPrerequisites_DoNotBreakTheLayout()
    {
        var layout = new TechGraphLayout(Registry(Tech("root"), Tech("a", 0), Tech("b", 0, 1)));

        Assert.True(layout.Width > 0);
        Assert.NotEqual(layout.Center(1), layout.Center(2));
    }

    /// <summary>Spočítá, kolikrát se dvě spojnice protnou.</summary>
    private static int CountCrossings(DefRegistry<TechDef> techs, TechGraphLayout layout)
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
