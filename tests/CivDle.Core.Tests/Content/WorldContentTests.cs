using CivDle.Core.Content;
using CivDle.Core.Content.Mods;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Skutečná data všech světů galaxie (svety-design.md 7.1). Kolonie se ve hře
/// načítají líně — až při přistání. Tenhle test je načte všechny hned, takže
/// chyba v datech kolonie spadne při vývoji, ne hráči po hodinách u Domoviny.
/// </summary>
public sealed class WorldContentTests
{
    private static readonly Lazy<GalaxyContent> Galaxy = new(() =>
        new GalaxyContent(TestData.RealDataDirectory, Array.Empty<ModPackage>(), TestData.LoadRealContent()));

    public static IEnumerable<object[]> Colonies() =>
        Galaxy.Value.Catalog.Colonies.Select(world => new object[] { world.Id });

    [Fact]
    public void TheGalaxyIsOpenInTheRealData()
    {
        Assert.True(Galaxy.Value.Catalog.IsEnabled);
        Assert.Contains(Galaxy.Value.Catalog.Worlds, world => world.Id == "dune");
    }

    [Theory]
    [MemberData(nameof(Colonies))]
    public void EveryColonyLoads(string worldId)
    {
        var world = Galaxy.Value.For(worldId);

        Assert.Equal(worldId, world.World.Id);
        Assert.Equal(worldId, world.Atmosphere.Id);
        Assert.Equal(world.WorldGen.Presets[world.World.PresetIndex].Id, world.WorldGen.Presets[world.World.PresetIndex].Id);
    }

    [Theory]
    [MemberData(nameof(Colonies))]
    public void AColonyNeverGetsTheHomeworldsWonders(string worldId)
    {
        var world = Galaxy.Value.For(worldId);

        Assert.False(world.Buildings.TryIndexOf("star_gate", out _), "Hvězdná brána patří Domovině");
        Assert.False(world.Buildings.TryIndexOf("spaceport", out _), "kosmodrom patří Domovině");
        Assert.False(world.Resources.TryIndexOf("wood", out _), $"svět '{worldId}' nemá mít dřevo Domoviny");
    }

    /// <summary>
    /// Každá surovina, kterou svět chce (stavba, výzkum, recept, údržba,
    /// sloučení), na něm i vzniká — nebo je jen dovozem. Jinak stojí výzkum
    /// i guvernér navždy (Xeno měl nektar, který nic nevyrábělo).
    /// </summary>
    [Theory]
    [MemberData(nameof(Colonies))]
    public void EveryResourceAColonyNeedsIsMadeThere(string worldId)
    {
        var world = Galaxy.Value.For(worldId);
        var made = new HashSet<int>();
        var needed = new Dictionary<int, string>();
        void Need(IEnumerable<ResourceAmount> amounts, string by)
        {
            foreach (var amount in amounts)
            {
                needed.TryAdd(amount.ResourceIndex, by);
            }
        }

        for (int b = 0; b < world.Buildings.Count; b++)
        {
            var def = world.Buildings[b];
            if (def.Recipe is { } recipe)
            {
                made.UnionWith(recipe.Outputs.Select(o => o.ResourceIndex));
                Need(recipe.Inputs, def.Id);
            }

            if (def.Buildable)
            {
                Need(def.BuildCost, def.Id);
            }

            Need(def.Upkeep, def.Id);
            Need(def.MergeCost, def.Id);
            Need(def.UpgradeCost, def.Id);
        }

        for (int t = 0; t < world.Techs.Count; t++)
        {
            Need(world.Techs[t].Cost, "výzkum " + world.Techs[t].Id);
        }

        var missing = needed.Where(n => !made.Contains(n.Key) && !world.Resources[n.Key].ImportOnly)
            .Select(n => $"{world.Resources[n.Key].Id} (chce {n.Value})").ToList();
        Assert.True(missing.Count == 0, $"svět '{worldId}' nic nevyrábí: " + string.Join(", ", missing));
    }

    /// <summary>
    /// Galaktický div (svety-design.md 2.7): každá kolonie má ve Souhvězdí
    /// svůj stupeň — chce něco, co ona vyváží a Domovina jen dováží —
    /// a biokrystaly z Xena jsou až poslední.
    /// </summary>
    [Fact]
    public void TheConstellationNeedsEveryColonysExports()
    {
        var home = Galaxy.Value.Home;
        var wonder = home.Buildings[home.Buildings.IndexOf("constellation")];
        var project = wonder.ProjectOrNull!;
        Assert.Equal(ProjectRule.GalaxyUnited, project.OnComplete);
        Assert.False(wonder.AutoBuild); // divy staví hráč

        var wanted = project.Stages.SelectMany(s => s.Cost).Select(c => home.Resources[c.ResourceIndex].Id).ToHashSet();
        foreach (var world in Galaxy.Value.Catalog.Colonies)
        {
            var colony = Galaxy.Value.For(world.Id);
            var exports = colony.World.ExportIndices.Select(i => colony.Resources[i].Id).ToList();
            Assert.True(exports.Any(wanted.Contains), $"Souhvězdí nechce nic z vývozu '{world.Id}' ({string.Join(", ", exports)})");
        }

        foreach (string id in wanted.Where(id => home.Resources[home.Resources.IndexOf(id)].ImportOnly))
        {
            Assert.True(home.Buildings[home.Buildings.IndexOf("spaceport")].StorageBonus
                .Any(s => home.Resources[s.ResourceIndex].Id == id), $"kosmodrom neskladuje dovoz '{id}'");
        }

        var last = project.Stages[^1].Cost.Select(c => home.Resources[c.ResourceIndex].Id);
        Assert.Contains("biocrystal", last);
    }

    [Fact]
    public void TheDuneRunsOnWaterAndHasItsOwnGround()
    {
        var dune = Galaxy.Value.For("dune");

        Assert.True(dune.Networks.IndexOf("water") > 0);
        Assert.True(dune.Biomes.TryIndexOf("oasis", out _));
        Assert.True(dune.Biomes.TryIndexOf("salt_flat", out _));
        Assert.True(dune.Resources.TryIndexOf("glass", out _));
        // Sdílený sklad stojí na Duně hlínu, ne dřevo — a neskladuje obilí.
        var warehouse = dune.Buildings[dune.Buildings.IndexOf("warehouse")];
        Assert.All(warehouse.BuildCost, cost => Assert.NotEqual("wood", dune.Resources[cost.ResourceIndex].Id));
    }
}
