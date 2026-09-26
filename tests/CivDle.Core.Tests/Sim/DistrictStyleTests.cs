using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Styly čtvrtí (endgame.md, B4): kosmetika přiřazená druhu čtvrti. Hlídá
/// se, že odměnu nejde použít dřív, než ji hráč má, že styl sedí jen druhům,
/// pro které je, a že volba přežije save.
/// </summary>
public class DistrictStyleTests
{
    [Fact]
    public void ARewardStyleIsLockedUntilItsChallengeIsWon()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1), 1);
        int residential = content.Districts.Types.IndexOf("residential");
        int log = Style(content, "log");

        Assert.False(sim.SetDistrictStyle(residential, log));
        Assert.Equal(-1, sim.DistrictStyleOf(residential));

        sim.SetProfileUnlocks(ChallengeRewards.UnlockKeys(content.Scenarios, new[] { "hard_winter" }));
        Assert.True(sim.SetDistrictStyle(residential, log));
        Assert.Equal(log, sim.DistrictStyleOf(residential));
    }

    [Fact]
    public void AStyleOnlyFitsTheDistrictsItIsFor()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1), 1);
        int industrial = content.Districts.Types.IndexOf("industrial");

        Assert.False(sim.SetDistrictStyle(industrial, Style(content, "garden")));
        Assert.True(sim.SetDistrictStyle(industrial, Style(content, "brick")));
        Assert.True(sim.SetDistrictStyle(industrial, -1)); // zpět na výchozí
    }

    [Fact]
    public void ChosenStylesSurviveASave()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 5), 5);
        int civic = content.Districts.Types.IndexOf("civic");
        int slate = Style(content, "slate");
        Assert.True(sim.SetDistrictStyle(civic, slate));

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(5, "medium", preset.Id, DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.Equal(slate, loaded.DistrictStyleOf(civic));
    }

    [Fact]
    public void RealStylesIncludeFreeOnesAndRewards()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Districts.Styles.Count >= 6);
        Assert.Contains(content.Districts.Styles, s => s.UnlockedBy is null);
        Assert.Contains(content.Districts.Styles, s => s.UnlockedBy is not null);
    }

    private static int Style(GameContent content, string id)
    {
        for (int i = 0; i < content.Districts.Styles.Count; i++)
        {
            if (content.Districts.Styles[i].Id == id)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"styl {id} v datech chybí");
    }
}
