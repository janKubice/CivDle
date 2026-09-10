using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Kam až může vylepšení vyhnat násobič.
///
/// <para>Vylepšení se skládají mocninou: <c>(1 + magnitude)^level</c>. To je
/// v žánru normální, jenže dvě nenápadná čísla v datech se tou mocninou
/// vynásobí do nesmyslu — 0,4 na osmdesáti úrovních je bilionkrát víc bydlení.
/// Hráč pak měl deset chalup a dvacet milionů obyvatel, což z budování města
/// dělá formalitu: nemá smysl stavět, když strop stejně nikdy nedosáhneš.</para>
///
/// <para>Test nehlídá konkrétní čísla (ta jsou věcí vyvážení), ale <b>řád</b>:
/// žádná osa se nesmí utrhnout o víc než dva řády od ostatních. Selže ve chvíli,
/// kdy někdo zvedne magnitude nebo maxLevel a nevšimne si, co to udělá nahoře.</para>
/// </summary>
public class UpgradeCeilingTests
{
    /// <summary>
    /// Nejvyšší rozumný násobič z jedné osy. Sto je hodně; bilion je chyba.
    /// </summary>
    private const double MaxSaneCeiling = 100.0;

    /// <summary>
    /// Efekty, které se neskládají jako násobič — jsou to počty nebo podíly
    /// (kolik budov přežije Vzestup, kolik bodů se vrátí), a mocnina u nich
    /// znamená něco jiného. Ty se tímhle testem měřit nedají.
    /// </summary>
    private static readonly HashSet<string> NotMultipliers = new()
    {
        "keep_buildings", "keep_roads", "keep_techs", "keep_map", "keep_wonders",
        "keep_resources", "keep_history", "auto_research", "start_resources",
        "ascension_points_mult", "ascension_discount", "crit_chance", "jackpot_chance",
    };

    [Fact]
    public void NoPrestigeUpgradeRunsAwayToAbsurdity()
    {
        var content = TestData.LoadRealContent();

        foreach (var upgrade in content.PrestigeUpgrades.All)
        {
            AssertSane(upgrade.Id, upgrade.Effect, upgrade.MultiplierAtLevel(upgrade.MaxLevel));
        }
    }

    [Fact]
    public void NoLegacyUpgradeDoesEither()
    {
        // Odkaz je trvalý, takže se sčítá napříč všemi běhy — utržená osa tu
        // bolí ještě víc než u Vzestupu.
        var content = TestData.LoadRealContent();

        foreach (var upgrade in content.LegacyUpgrades.All)
        {
            AssertSane(upgrade.Id, upgrade.Effect, upgrade.MultiplierAtLevel(upgrade.MaxLevel));
        }
    }

    [Fact]
    public void HousingCannotOutgrowTheCityThatBuildsIt()
    {
        // Tohle je ta konkrétní stížnost: strop populace je bydlení, takže
        // když se bydlení vynásobí bilionkrát, přestane mít smysl stavět domy.
        var content = TestData.LoadRealContent();

        double housing = 1.0;
        foreach (var upgrade in content.PrestigeUpgrades.All)
        {
            if (upgrade.Effect == "housing_mult")
            {
                housing *= upgrade.MultiplierAtLevel(upgrade.MaxLevel);
            }
        }

        Assert.InRange(housing, 1.0, MaxSaneCeiling);
    }

    private static void AssertSane(string id, string effect, double ceiling)
    {
        if (NotMultipliers.Contains(effect))
        {
            return;
        }

        Assert.True(
            ceiling <= MaxSaneCeiling,
            $"'{id}' ({effect}) vyžene násobič na {ceiling:0.###e+0}× — to je mimo řád zbytku hry");
    }
}
