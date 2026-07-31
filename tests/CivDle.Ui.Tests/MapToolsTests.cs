using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Input;
using CivDle.Rendering;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Nástroje mapy jsou stavový automat — a přesně tam vznikaly chyby, dokud byl
/// stav rozsypaný po <c>GameplayScreen</c> a mazal se ručně na šesti místech.
/// Testuje se hlavně jediný invariant, na kterém všechno stojí: <b>naráz smí být
/// aktivní jen jeden nástroj</b>.
///
/// <para>Běží headless — <see cref="MapTools"/> nesahá na grafiku ani na Myru.</para>
/// </summary>
public sealed class MapToolsTests
{
    [Fact]
    public void OnlyOneToolCanBeActiveAtATime()
    {
        var tools = NewTools();

        tools.ToggleBuilding(0);
        AssertExactlyOneActive(tools);

        tools.TogglePlant();
        Assert.True(tools.PlantMode);
        Assert.Equal(-1, tools.SelectedBuilding);
        AssertExactlyOneActive(tools);

        tools.ToggleZone(0);
        Assert.True(tools.ZoneMode);
        Assert.False(tools.PlantMode);
        AssertExactlyOneActive(tools);

        tools.StartMove(0);
        Assert.Equal(0, tools.MovingBuildingIndex);
        Assert.False(tools.ZoneMode);
        AssertExactlyOneActive(tools);
    }

    [Fact]
    public void TogglingTheSameToolTwiceTurnsItOff()
    {
        var tools = NewTools();

        tools.ToggleBuilding(3);
        tools.ToggleBuilding(3);
        Assert.Equal(-1, tools.SelectedBuilding);

        tools.TogglePlant();
        tools.TogglePlant();
        Assert.False(tools.PlantMode);

        tools.ToggleZone(1);
        tools.ToggleZone(1);
        Assert.False(tools.ZoneMode);
    }

    [Fact]
    public void SwitchingBuildings_KeepsTheNewOneSelected()
    {
        var tools = NewTools();

        tools.ToggleBuilding(1);
        tools.ToggleBuilding(2);

        Assert.Equal(2, tools.SelectedBuilding);
    }

    [Fact]
    public void ToggleZone_SwitchesTypeInsteadOfLeavingTheMode()
    {
        var tools = NewTools();

        tools.ToggleZone(0);
        tools.ToggleZone(1);

        Assert.True(tools.ZoneMode);
        Assert.Equal(1, tools.ZonePaintTypeIndex);
    }

    /// <summary>Escape ruší po vrstvách; když není co, obrazovka otevře pauzu.</summary>
    [Fact]
    public void CancelTopmost_PeelsOneToolAtATime()
    {
        var tools = NewTools();

        Assert.False(tools.CancelTopmost()); // nic aktivního

        tools.ToggleBuilding(0);
        Assert.True(tools.CancelTopmost());
        Assert.False(tools.AnyActive);
        Assert.False(tools.CancelTopmost());
    }

    [Fact]
    public void Clear_TurnsOffEverythingIncludingGhosts()
    {
        var tools = NewTools();
        tools.ToggleZone(0);

        tools.Clear();

        Assert.False(tools.AnyActive);
        Assert.False(tools.GhostVisible);
        Assert.False(tools.PlantGhostActive);
        Assert.False(tools.MoveGhostActive);
        Assert.False(tools.ZonePreviewActive);
    }

    // ----- hromadná stavba -----

    [Fact]
    public void BatchSize_StartsAtOne()
    {
        // Výchozí chování musí zůstat „klik = jedna budova".
        Assert.Equal(1, NewTools().BatchSize);
    }

    [Fact]
    public void CycleBatchSize_WalksTheLadderAndWrapsAround()
    {
        var tools = NewTools();
        var sizes = tools.BatchSizes;
        Assert.True(sizes.Count > 1, "Data mají nabízet aspoň dva násobiče.");

        for (int i = 1; i < sizes.Count; i++)
        {
            tools.CycleBatchSize();
            Assert.Equal(sizes[i], tools.BatchSize);
        }

        tools.CycleBatchSize();
        Assert.Equal(sizes[0], tools.BatchSize);
    }

    [Fact]
    public void BatchSize_SurvivesSwitchingBuildings()
    {
        // Hráč, který staví po pětadvaceti, to obvykle chce dělat i u další budovy.
        var tools = NewTools();
        tools.SetBatchSize(5);

        tools.ToggleBuilding(0);
        tools.ToggleBuilding(1);

        Assert.Equal(5, tools.BatchSize);
    }

    [Fact]
    public void SetBatchSize_NeverGoesBelowOne()
    {
        var tools = NewTools();

        tools.SetBatchSize(0);

        Assert.Equal(1, tools.BatchSize);
    }

    [Fact]
    public void BulkPlan_IsEmptyUntilThePlayerDrags()
    {
        // Duch plánu se kreslí, jen když se opravdu táhne — jinak by se přes
        // mapu vykreslovaly budovy, které nikdo neobjednal.
        var tools = NewTools();
        tools.ToggleBuilding(0);

        Assert.Empty(tools.BulkPlan);
        Assert.Equal(0, tools.BulkBuildable);
    }

    [Fact]
    public void Clear_ThrowsAwayThePlan()
    {
        var tools = NewTools();
        tools.ToggleBuilding(0);

        tools.Clear();

        Assert.Empty(tools.BulkPlan);
        Assert.Equal(0, tools.BulkBuildable);
    }

    private static void AssertExactlyOneActive(MapTools tools)
    {
        int active = (tools.SelectedBuilding >= 0 ? 1 : 0)
            + (tools.PlantMode ? 1 : 0)
            + (tools.ZoneMode ? 1 : 0)
            + (tools.MovingBuildingIndex >= 0 ? 1 : 0);

        Assert.Equal(1, active);
        Assert.True(tools.AnyActive);
    }

    private static MapTools NewTools()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var simulation = new Simulation(content, new UniformTerrain(LandBiome(content)));
        return new MapTools(simulation, new Camera2D(), new InputManager(), content);
    }

    private static byte LandBiome(GameContent content)
    {
        for (byte i = 0; i < content.Biomes.Count; i++)
        {
            if (!content.Biomes[i].IsWater)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Obsah nemá pevninský biom.");
    }
}
