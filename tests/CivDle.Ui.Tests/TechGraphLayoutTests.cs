using CivDle.Core.Content;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozvržení tech stromu do souhvězdí je čistý výpočet — testuje se bez okna
/// i grafického zařízení. Kontroluje se, co hráč pozná okem: hvězdy se
/// nepřekrývají, hloubka roste směrem od středu a chybná data (cyklus) rozvržení
/// nezacyklí.
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
    public void Prerequisite_SitsCloserToCentreThanItsSuccessor()
    {
        var techs = LoadRealTechs();
        var layout = new TechGraphLayout(techs);
        var centre = new Vector2(layout.Width / 2f, layout.Height / 2f);

        for (int i = 0; i < techs.Count; i++)
        {
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                Assert.True(
                    Vector2.Distance(layout.Center(prereq), centre) < Vector2.Distance(layout.Center(i), centre),
                    $"{techs[prereq].Id} musí ležet blíž středu než {techs[i].Id}.");
            }
        }
    }

    [Fact]
    public void SingleRoot_SitsInTheCentre()
    {
        var layout = new TechGraphLayout(Registry(Tech("root")));

        Assert.Equal(new Vector2(layout.Width / 2f, layout.Height / 2f), layout.Center(0));
    }

    [Fact]
    public void EmptyTree_DoesNotThrow()
    {
        var layout = new TechGraphLayout(Registry());

        Assert.True(layout.Width > 0 && layout.Height > 0);
    }

    /// <summary>Cyklus v datech nesmí rozvržení zacyklit — strážce ho položí do prvního prstence.</summary>
    [Fact]
    public void CyclicPrerequisites_DoNotHang()
    {
        var layout = new TechGraphLayout(Registry(Tech("a", 1), Tech("b", 0)));

        Assert.NotEqual(layout.Center(0), layout.Center(1));
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
