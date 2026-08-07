using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Hlídá, že obě dlouhodobé vrstvy (Velké dílo a Odkaz) opravdu <b>dorazí
/// z dat až do hry</b>.
///
/// <para>Existuje kvůli konkrétní chybě: Velké dílo se z <c>grandwork.json</c>
/// načetlo, ale zapomnělo se předat do <see cref="CivDle.Core.Content.GameContent"/>,
/// takže mechanika byla v ostré hře tiše vypnutá. Jednotkové testy to nechytly —
/// stavěly si obsah samy. Tenhle test se ptá ostrých dat.</para>
/// </summary>
public class PrestigeLayerContentTests
{
    [Fact]
    public void RealContent_HasTheGrandWorkEnabled()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.GrandWork.IsEnabled, "Velké dílo musí být v ostrých datech zapnuté.");
        Assert.True(content.GrandWork.CostGrowth > 1.0, "Bez růstu ceny by dílo nebylo bezedné.");
    }

    [Fact]
    public void RealContent_HasTheLegacyLayerEnabled()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Legacy.IsEnabled, "Odkaz musí být v ostrých datech zapnutý.");
        Assert.True(content.LegacyUpgrades.Count >= 5, $"Odkaz má mít aspoň 5 upgradů, má {content.LegacyUpgrades.Count}");
    }

    [Fact]
    public void LegacyUpgrades_TouchTheAxisThatMakesTheLayerWorthDoing()
    {
        // Kdyby Odkaz uměl jen „ještě víc výroby", byl by to jen dražší Vzestup.
        // Aspoň jeden upgrade musí sahat na samotné vzestupování.
        var content = TestData.LoadRealContent();

        bool touchesAscension = false;
        foreach (var upgrade in content.LegacyUpgrades.All)
        {
            if (upgrade.Effect is "ascension_points_mult" or "ascension_discount")
            {
                touchesAscension = true;
                break;
            }
        }

        Assert.True(touchesAscension, "Odkaz musí mít upgrade, který zrychluje samotné vzestupování.");
    }

    [Fact]
    public void RepeatableUpgrades_ExistInBothLayers()
    {
        // Opakovatelnost je to, co dělá z prestiže nekonečnou osu — bez ní má
        // strom pevný strop a po pár Vzestupech není co kupovat.
        var content = TestData.LoadRealContent();

        Assert.Contains(content.PrestigeUpgrades.All, u => u.IsRepeatable);
        Assert.Contains(content.LegacyUpgrades.All, u => u.IsRepeatable);
    }
}
