using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Budovy, které přerůstají svůj půdorys.
///
/// <para>V pohledu shora zabírá mrakodrap přesně tolik místa jako chalupa
/// o stejném půdorysu. Pozdní město tak vypadalo jako to rané, jen z jiných
/// barev. Přerůstání je jediný způsob, jak ve dvourozměrné scéně říct „tohle
/// je vysoké".</para>
///
/// <para>Výška se ale platí zakrýváním. Testuje se proto obojí, co se tím
/// rozbije, když se na to zapomene: že se kreslí správným směrem a že výběr
/// myší trefí to, co hráč vidí, ne to, co je za tím.</para>
/// </summary>
public class TallBuildingTests
{
    [Fact]
    public void AFlatBuildingIsDrawnExactlyOnItsFootprint()
    {
        // Nula znamená „jako dosud". Kdyby se lišila, změnil by se vzhled
        // všech osmadevadesáti budov naráz.
        var footprint = new Rectangle(32, 48, 32, 32);

        Assert.Equal(footprint, BuildingRenderer.VisualRect(footprint, 0));
    }

    [Fact]
    public void ATallBuildingGrowsUpwardsAndKeepsItsFeet()
    {
        // Roste nahoru po obrazovce: v pohledu shora se stavba tyčí směrem od
        // diváka. Kdyby rostla dolů, stála by v půdorysu sousedů pod sebou.
        var footprint = new Rectangle(32, 48, 32, 32);

        var visual = BuildingRenderer.VisualRect(footprint, 2);

        Assert.Equal(footprint.Bottom, visual.Bottom);
        Assert.Equal(footprint.X, visual.X);
        Assert.Equal(footprint.Width, visual.Width);
        Assert.True(visual.Y < footprint.Y, "vysoká budova nepřerostla nahoru");
    }

    [Fact]
    public void TheExtraHeightIsCountedInTiles()
    {
        var footprint = new Rectangle(0, 64, 32, 32);

        var visual = BuildingRenderer.VisualRect(footprint, 2);

        Assert.Equal(2 * TerrainRenderer.TileSize, footprint.Y - visual.Y);
    }

    [Fact]
    public void TheFacadeAboveTheFootprintBelongsToTheTower()
    {
        // Tohle je ta chyba, kterou nikdo nenahlásí — jen mu ovládání bude
        // připadat rozbité: klikne na věž a otevře se mu dům, který nevidí.
        Assert.True(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 2, tileY: 9));
        Assert.True(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 2, tileY: 8));
    }

    [Fact]
    public void TheReachStopsAtTheDeclaredHeight()
    {
        // Prohledávání musí mít strop. Bez něj by věž „chytala" kliknutí přes
        // půl obrazovky.
        Assert.False(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 2, tileY: 7));
    }

    [Fact]
    public void TheFootprintItselfIsSomebodyElsesJob()
    {
        // Řádky uvnitř půdorysu najde obyčejný dotaz na dlaždici. Započítat je
        // podruhé by znamenalo, že vysoká budova přebije kohokoli, kdo na ní
        // stojí.
        Assert.False(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 2, tileY: 10));
        Assert.False(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 2, tileY: 11));
    }

    [Fact]
    public void AFlatBuildingIsNeverPickedThroughItsRoof()
    {
        // Nízké budovy se kryjí se svým půdorysem — ty najde obyčejný dotaz na
        // dlaždici a tudy procházet nesmí.
        Assert.False(BuildingRenderer.CoversTile(buildingY: 10, visualHeight: 0, tileY: 9));
    }

    [Fact]
    public void NothingIsPickedInAnEmptyWorld()
    {
        // Průchod prázdnou mapou nesmí nic najít ani spadnout — a přes tenhle
        // případ jde každý klik do volné krajiny.
        var (sim, content) = NewWorld();

        Assert.False(BuildingRenderer.TryPickTall(
            sim, content, 3000, 3000, BuildingRenderer.MaxVisualHeight(content), out int picked));
        Assert.Equal(-1, picked);
    }

    [Fact]
    public void TheContentActuallyHasSomethingTall()
    {
        // Kdyby žádná budova výšku neměla, byla by celá tahle cesta mrtvý kód.
        var content = LoadContent();

        Assert.True(BuildingRenderer.MaxVisualHeight(content) > 0);
    }

    private static (Simulation Sim, GameContent Content) NewWorld()
    {
        var content = LoadContent();
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 20260728), 20260728);
        sim.SkipTutorial();
        return (sim, content);
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
