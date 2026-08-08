using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Velké dílo jako výzkum a stavba, ne položka v menu.
///
/// <para>Sink na přebytky byl řádek, který se po prvním Vzestupu prostě objevil.
/// Teď je to meta: vyzkoumej Velký výkop, postav obří jámu — a teprve do ní je
/// kam sypat. Bez toho hráč nevidí na mapě nic, co by tomu odpovídalo.</para>
/// </summary>
public class GrandWorkGateTests
{
    private static Simulation Grass(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
    }

    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }
    }

    [Fact]
    public void RealContentTiesTheGrandWorkToATechAndABuilding()
    {
        var content = TestData.LoadRealContent();
        var config = content.GrandWork;

        Assert.True(config.IsEnabled);
        Assert.True(config.NeedsTech, "Velké dílo se má odemykat výzkumem");
        Assert.True(config.NeedsBuilding, "Velké dílo má mít stavbu na mapě");
        Assert.Equal("great_excavation", content.Techs[config.UnlockTechIndex].Id);
        Assert.Equal("great_pit", content.Buildings[config.BuildingIndex].Id);
    }

    [Fact]
    public void TheGreatWorkIsAGiantBuilding()
    {
        // „Fakt sink, díra do země úplně gigantická" — kdyby to byl domek 1×1,
        // celý smysl té stavby je pryč.
        var content = TestData.LoadRealContent();
        var pit = content.Buildings[content.Buildings.IndexOf("great_pit")];

        Assert.True(pit.FootprintWidth >= 5, $"jáma je široká jen {pit.FootprintWidth}");
        Assert.True(pit.FootprintHeight >= 5, $"jáma je vysoká jen {pit.FootprintHeight}");
        Assert.True(pit.WorkerSlots >= 20, "na takové dílo má být potřeba celé osazenstvo");
    }

    [Fact]
    public void WithoutTheTechAndThePitThereIsNowhereToPour()
    {
        var sim = Grass(out _);

        Assert.False(sim.GrandWorkAvailable);
    }

    [Fact]
    public void ResearchAloneIsNotEnough_ThePitHasToStand()
    {
        // Logika brány se zkouší na PODVRŽENÝCH datech: ostrý řetěz výzkumu
        // k Velkému výkopu je na desítky uzlů a test by pak měřil ekonomiku
        // stromu, ne to, co má. Že ostrá data ukazují na správný výzkum
        // a stavbu, hlídá RealContentTiesTheGrandWorkToATechAndABuilding.
        string directory = Path.Combine(AppContext.BaseDirectory, "tmp-grandwork", Guid.NewGuid().ToString("N"));
        CopyDirectory(TestData.RealDataDirectory, directory);
        try
        {
            string path = Path.Combine(directory, "grandwork.json");
            string json = File.ReadAllText(path)
                .Replace("\"great_excavation\"", "\"milling\"", StringComparison.Ordinal)
                .Replace("\"great_pit\"", "\"warehouse\"", StringComparison.Ordinal);
            File.WriteAllText(path, json);

            var content = new ContentLoader().LoadFrom(directory);
            var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

            Assert.False(sim.GrandWorkAvailable, "bez výzkumu i bez stavby nesmí být kam sypat");

            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(content.Techs.IndexOf("milling")));
            Assert.False(sim.GrandWorkAvailable, "samotný výzkum nestačí — jáma musí stát");

            TopUp(sim, content);
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("warehouse"), 10, 10));
            Assert.True(sim.GrandWorkAvailable, "s vykopanou jámou už je kam sypat");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }
}
