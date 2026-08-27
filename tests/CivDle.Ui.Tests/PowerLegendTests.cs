using CivDle.Core.Content;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Legenda rozvodu proudu — barevná mřížka bez ní je hádanka.
///
/// <para>Hlídá se to, co se dá tiše rozejít: legenda si nesmí držet vlastní
/// paletu (jinak by vysvětlovala jiné barvy, než jsou na mapě) a každá barva
/// z mapy musí mít v legendě svou položku i své slovo ve všech jazycích.</para>
/// </summary>
public class PowerLegendTests
{
    [Fact]
    public void EveryColourOnTheMapHasARowInTheLegend()
    {
        // Prahy jsou v ColorFor; kdyby někdo přidal pátý stupeň a legendu
        // zapomněl, spadne to tady, ne až hráči pod rukama.
        double[] coverages = { 1.0, 0.999, 0.7, 0.6, 0.3, 0.001, 0.0 };

        foreach (double coverage in coverages)
        {
            int slot = PowerOverlayRenderer.LegendSlot(coverage);
            Assert.InRange(slot, 0, PowerOverlayRenderer.Legend.Count - 1);
            Assert.Equal(PowerOverlayRenderer.ColorFor(coverage), PowerOverlayRenderer.Legend[slot].Color);
        }
    }

    [Fact]
    public void TheLegendHasNoTwoRowsOfTheSameColour()
    {
        // Dvě stejné barvy by znamenaly dvě položky, které hráč od sebe
        // na mapě nerozezná — a jedna z nich by měla vždycky nulu.
        var colors = PowerOverlayRenderer.Legend.Select(entry => entry.Color).ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public void EveryRowHasWordsInEveryLanguage()
    {
        var content = LoadContent();

        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);
            foreach (var (key, _) in PowerOverlayRenderer.Legend)
            {
                Assert.DoesNotContain("~", loc[key]);
            }

            Assert.DoesNotContain("~", loc["power.legend.hint"]);
        }
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
