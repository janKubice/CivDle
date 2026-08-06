using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Hlídá, že ostrý obsah zůstává bohatý a správně zamčený: hráč má mít na startu
/// z čeho vybírat, ale pokročilé budovy musí čekat na technologii. Chrání před
/// tichým rozbitím gatování při editaci dat (např. buildable vs. tech unlock).
/// </summary>
public class ContentRichnessTests
{
    [Fact]
    public void RealContent_HasRichBuildingCatalogue()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Buildings.Count >= 45, $"budov má být aspoň 45, je {content.Buildings.Count}");
    }

    [Fact]
    public void EarlyGame_OffersSeveralBuildingsWithoutResearch()
    {
        // Bez jediné technologie musí být na výběr dost budov, ať má hráč co dělat hned.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        int available = 0;
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (sim.IsBuildingBuildable(i))
            {
                available++;
            }
        }

        Assert.True(available >= 10, $"na startu má být dostupných aspoň 10 budov, je {available}");
    }

    [Theory]
    [InlineData("charcoal_kiln")]
    [InlineData("machine_shop")]
    [InlineData("solar_array")]
    [InlineData("hydroponics")]
    [InlineData("windmill")]
    [InlineData("factory")]
    public void TechGatedBuildings_AreLockedBeforeResearch(string buildingId)
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        Assert.True(content.Buildings.TryIndexOf(buildingId, out int index), $"chybí budova '{buildingId}'");
        Assert.False(sim.IsBuildingBuildable(index), $"'{buildingId}' má být zamčená do vyzkoumání");
    }

    [Fact]
    public void EveryTechGatedBuilding_IsActuallyReachable()
    {
        // Budova zamčená technologií musí být v unlocks NĚJAKÉ technologie (nebo stupně
        // měřítka) — jinak by byla navždy nedostupná. Typická chyba: buildable:false
        // místo tech unlocku.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        var reachable = new HashSet<int>();
        foreach (var tech in content.Techs.All)
        {
            foreach (int index in tech.UnlockedBuildingIndices)
            {
                reachable.Add(index);
            }
        }

        foreach (var tier in content.AscensionTiers.All)
        {
            foreach (int index in tier.UnlockedBuildingIndices)
            {
                reachable.Add(index);
            }
        }

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var def = content.Buildings[i];
            if (sim.IsBuildingBuildable(i) || !def.Buildable)
            {
                continue; // dostupná hned, nebo záměrně jen přes vylepšení
            }

            Assert.True(reachable.Contains(i),
                $"budova '{def.Id}' je zamčená, ale žádná technologie ani stupeň měřítka ji neodemyká");
        }
    }

    [Fact]
    public void RealContent_TheMostTellingLandmarksHaveSprites()
    {
        // „Proč shipwreck není prostě fakt lodička potopená?" — barevný čtvereček
        // neřekne nic. Místa, která mají tvar (vrak, ruiny, kamenný kruh, kosti),
        // musí mít sprite; ostatní si vystačí s barvou.
        var landmarks = TestData.LoadRealContent().Landmarks;
        string[] mustLook = ["shipwreck", "ancient_ruins", "stone_circle", "mammoth_bones"];

        foreach (string id in mustLook)
        {
            Assert.True(landmarks.TryIndexOf(id, out int index), $"landmark '{id}' v datech chybí");
            Assert.NotNull(landmarks[index].SpriteKey);
        }
    }

    [Fact]
    public void RealContent_BigLandmarksSpanMoreThanOneTile()
    {
        // Vrak ani ruiny se do jedné dlaždice nevejdou tak, aby to vypadalo.
        var landmarks = TestData.LoadRealContent().Landmarks;

        Assert.True(landmarks.TryIndexOf("shipwreck", out int wreck));
        Assert.True(landmarks[wreck].Footprint >= 2);

        Assert.True(landmarks.TryIndexOf("ancient_ruins", out int ruins));
        Assert.True(landmarks[ruins].Footprint >= 2);
    }
}
