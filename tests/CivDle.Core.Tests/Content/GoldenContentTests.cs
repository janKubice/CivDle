using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Zlaté úlovky v datech.
///
/// <para>Chybný záznam se jinak projeví až tím, že po dvou minutách hraní
/// vyskočí neviditelný tvor bez odměny — a to nikdo nespojí s daty. Proto
/// se ověřuje při načtení, ne za běhu.</para>
/// </summary>
public class GoldenContentTests
{
    [Fact]
    public void RealDataDefinesSeveralKinds()
    {
        // Jeden bezejmenný třpyt byl původní stav; smysl změny je pestrost.
        var golden = TestData.LoadRealContent().Gameplay.Golden;

        Assert.True(golden.IsEnabled);
        Assert.True(golden.Kinds.Count >= 2, "v datech je jen jeden druh úlovku");
    }

    [Fact]
    public void EveryKindIsUsable()
    {
        var golden = TestData.LoadRealContent().Gameplay.Golden;

        foreach (var kind in golden.Kinds)
        {
            Assert.False(string.IsNullOrWhiteSpace(kind.Id));
            Assert.False(string.IsNullOrWhiteSpace(kind.Sprite));
            Assert.True(kind.LifeSeconds > 0, kind.Id);

            // Úlovek musí něco dát — jinak je to jen blikající past na klik.
            Assert.True(
                kind.GrantsFestival || kind.MinReward > 0 || kind.RewardFraction > 0,
                $"'{kind.Id}' nedává nic");
        }
    }

    [Fact]
    public void GapsMakeItRare()
    {
        // Vzácnost je celý smysl. Krátký rozestup a přestane to být událost.
        var golden = TestData.LoadRealContent().Gameplay.Golden;

        Assert.True(golden.MinGapSeconds >= 30, "úlovky by chodily jako na běžícím pásu");
        Assert.True(golden.MaxGapSeconds >= golden.MinGapSeconds);
    }

    [Fact]
    public void MissingBlockFallsBackInsteadOfCrashing()
    {
        // Starý gameplay.json (i z modu) musí načíst beze změny chování.
        var fallback = GoldenConfig.Default;

        Assert.True(fallback.IsEnabled);
        Assert.Single(fallback.Kinds);
    }

    [Fact]
    public void AtLeastOneKindThrowsAFestival()
    {
        // Odměna, která není balík surovin, je ten důvod, proč se to vůbec
        // předělávalo — jinak by stačilo přidat sprity.
        var golden = TestData.LoadRealContent().Gameplay.Golden;

        Assert.Contains(golden.Kinds, kind => kind.GrantsFestival);
    }
}
