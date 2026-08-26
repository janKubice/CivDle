using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Fáze stavby — div se má stavět před očima, ne se objevit hotový.
///
/// <para>Výběr fáze je čistá funkce postupu, takže se dá otestovat bez okna.
/// A validace dat je tu proto, že chybná fáze se jinak projeví až tím, že se
/// div desítky minut kreslí špatně — a to nikdo nespojí s daty.</para>
/// </summary>
public class BuildStageTests
{
    [Fact]
    public void RealContentStagesTheBigWonders()
    {
        var content = TestData.LoadRealContent();
        var staged = content.Buildings.All.Where(b => b.HasStages).ToList();

        Assert.NotEmpty(staged);
        Assert.All(staged, b => Assert.True(b.BuildTicks > 0, $"{b.Id} má fáze, ale staví se okamžitě"));
    }

    [Theory]
    [InlineData(0.0, "stage.foundation")]
    [InlineData(0.34, "stage.foundation")]
    [InlineData(0.35, "stage.frame")]
    [InlineData(0.77, "stage.frame")]
    [InlineData(0.78, "building.spaceport")]
    [InlineData(1.0, "building.spaceport")]
    public void TheStagePicksUpWhereTheProgressIs(double progress, string expected)
    {
        var content = TestData.LoadRealContent();
        var spaceport = content.Buildings[content.Buildings.IndexOf("spaceport")];

        Assert.Equal(expected, spaceport.StageSpriteAt(progress));
    }

    [Fact]
    public void ABuildingWithoutStagesSaysSo()
    {
        var content = TestData.LoadRealContent();
        var house = content.Buildings[content.Buildings.IndexOf("house")];

        Assert.False(house.HasStages);
        Assert.Empty(house.Stages);
        Assert.Null(house.StageSpriteAt(0.5));
    }

    [Fact]
    public void EveryStagedBuildingStartsAtZero()
    {
        // Kdyby první fáze začínala až v půlce, do té doby by se nekreslilo nic.
        var content = TestData.LoadRealContent();

        foreach (var def in content.Buildings.All.Where(b => b.HasStages))
        {
            Assert.Equal(0.0, def.Stages[0].AtProgress);
        }
    }

    [Fact]
    public void StagesAreInAscendingOrder()
    {
        var content = TestData.LoadRealContent();

        foreach (var def in content.Buildings.All.Where(b => b.HasStages))
        {
            for (int i = 1; i < def.Stages.Count; i++)
            {
                Assert.True(
                    def.Stages[i].AtProgress > def.Stages[i - 1].AtProgress,
                    $"{def.Id}: fáze {i} začíná dřív než ta před ní");
            }
        }
    }
}
