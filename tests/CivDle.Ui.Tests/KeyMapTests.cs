using CivDle.Core.Config;
using CivDle.Input;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Mapa kláves.
///
/// <para>Nejdůležitější je poslední test: <b>rozbité nastavení nesmí zamknout
/// ovládání</b>. Soubor s nastavením píše i člověk a překlep v něm nesmí
/// znamenat hru, ve které se nedá pohnout kamerou.</para>
/// </summary>
public class KeyMapTests
{
    [Fact]
    public void FreshSettingsGiveTheDefaultLayout()
    {
        var map = new KeyMap(new PlayerProfile());

        Assert.Equal(Keys.W, map.KeyFor(GameAction.CameraUp));
        Assert.Equal(Keys.B, map.KeyFor(GameAction.Bottlenecks));

        // Šipky vedle WASD: obojí musí fungovat, dokud si hráč nic nepřendá.
        Assert.Equal(Keys.Up, map.AlternateFor(GameAction.CameraUp));
    }

    [Fact]
    public void RebindingReplacesTheKeyAndItsAlternate()
    {
        // Alternativa musí zmizet taky. Kdyby zůstala, hráč, který si posun
        // vlevo dal na J, by měl pořád i šipku — a při přemapování dvou akcí
        // na kříž by vznikl konflikt, o kterém by nevěděl.
        var map = new KeyMap(new PlayerProfile());
        map.Rebind(GameAction.CameraUp, Keys.J);

        Assert.Equal(Keys.J, map.KeyFor(GameAction.CameraUp));
        Assert.Null(map.AlternateFor(GameAction.CameraUp));
    }

    [Fact]
    public void OnlyChangesAreSaved()
    {
        // Ukládat celé rozložení by znamenalo, že se hráči při změně výchozích
        // kláves v nové verzi zakonzervují ty staré.
        var map = new KeyMap(new PlayerProfile());
        Assert.Empty(map.ToSettings());

        map.Rebind(GameAction.Bottlenecks, Keys.N);
        var saved = map.ToSettings();

        Assert.Single(saved);
        Assert.Equal("N", saved["Bottlenecks"]);
    }

    [Fact]
    public void SavedBindingsComeBack()
    {
        var map = new KeyMap(new PlayerProfile());
        map.Rebind(GameAction.PowerOverlay, Keys.P);

        var reloaded = new KeyMap(new PlayerProfile { KeyBindings = map.ToSettings() });

        Assert.Equal(Keys.P, reloaded.KeyFor(GameAction.PowerOverlay));
    }

    [Fact]
    public void ATakenKeyIsReportedWithWhoHasIt()
    {
        var map = new KeyMap(new PlayerProfile());

        var conflict = map.ConflictOf(Keys.B, ignore: GameAction.PowerOverlay);

        Assert.Equal(GameAction.Bottlenecks, conflict);
        Assert.Null(map.ConflictOf(Keys.B, ignore: GameAction.Bottlenecks));
    }

    [Fact]
    public void ResettingOneActionBringsBackItsAlternateToo()
    {
        var map = new KeyMap(new PlayerProfile());
        map.Rebind(GameAction.CameraLeft, Keys.J);

        map.Reset(GameAction.CameraLeft);

        Assert.Equal(Keys.A, map.KeyFor(GameAction.CameraLeft));
        Assert.Equal(Keys.Left, map.AlternateFor(GameAction.CameraLeft));
    }

    [Fact]
    public void EveryActionHasAKey()
    {
        // Akce bez klávesy by byla položka v seznamu ovládání, která nic nedělá.
        var map = new KeyMap(new PlayerProfile());

        foreach (var action in Enum.GetValues<GameAction>())
        {
            Assert.NotEqual(Keys.None, map.KeyFor(action));
            Assert.NotEqual(Keys.None, KeyMap.DefaultFor(action));
        }
    }

    [Fact]
    public void ABrokenSettingsFileDoesNotLockTheControls()
    {
        // Soubor s nastavením píše i člověk. Překlep v něm nesmí znamenat hru,
        // ve které se nedá pohnout kamerou.
        var map = new KeyMap(new PlayerProfile
        {
            KeyBindings = new Dictionary<string, string>
            {
                ["CameraUp"] = "TahleKlavesaNeexistuje",
                ["UplneJinaAkce"] = "W",
                ["Bottlenecks"] = "N",
            },
        });

        Assert.Equal(Keys.W, map.KeyFor(GameAction.CameraUp));  // nesmysl se ignoroval
        Assert.Equal(Keys.N, map.KeyFor(GameAction.Bottlenecks)); // platná změna prošla
    }

    [Fact]
    public void EveryActionHasAName_InEveryLanguage()
    {
        // Obrazovka ovládání prochází celý výčet akcí, takže na novou akci
        // nemůže zapomenout. Zapomenout se dá na její JMÉNO — a pak v seznamu
        // svítí holý klíč. Tohle je jediné místo, kde se to pozná dřív než
        // ve hře.
        var content = new CivDle.Core.Content.ContentLoader()
            .LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));

        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new CivDle.Core.Content.Localization(content.Languages, content.Languages[language].Id);
            foreach (var action in Enum.GetValues<GameAction>())
            {
                string name = action.ToString();
                string key = $"controls.action.{char.ToLowerInvariant(name[0])}{name[1..]}";

                Assert.DoesNotContain("~", loc[key]);
            }
        }
    }
}
