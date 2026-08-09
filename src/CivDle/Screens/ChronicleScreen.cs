using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Kronika: co hráč dokázal napříč všemi hrami. Rekordy (nejvyšší populace,
/// nejvíc budov, nejvyšší Vzestup, splněné výzvy) a sbírka biomů, na kterých
/// už kdy stavěl.
///
/// <para>Existuje proto, že jedna hra vždycky skončí Vzestupem a začne od nuly.
/// Bez kroniky by po ní nezůstalo nic než achievementy — tohle dává důvod
/// zkusit i svět, kde by hráče jinak nenapadlo stavět (ledovec, sopka).</para>
///
/// <para>Data jdou z účet-wide profilu, ne ze savu — přežijí i smazání hry.</para>
/// </summary>
public sealed class ChronicleScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public ChronicleScreen(ScreenManager screens)
    {
        _screens = screens;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var profile = _screens.Profile;
        var biomes = _screens.Content.Biomes;

        var list = new VerticalStackPanel { Spacing = 8 };

        // Rekordy jednoho světa: „co nejvíc se mi kdy povedlo".
        list.Widgets.Add(Header(loc["chronicle.records"]));
        list.Widgets.Add(Line(loc["chronicle.bestPopulation"], CivDle.Core.Numbers.Format(profile.BestPopulation)));
        list.Widgets.Add(Line(loc["chronicle.bestBuildings"], CivDle.Core.Numbers.Format(profile.BestBuildings)));
        list.Widgets.Add(Line(loc["chronicle.bestSettlements"], CivDle.Core.Numbers.Format(profile.BestSettlements)));
        list.Widgets.Add(Line(loc["chronicle.bestAscension"], profile.BestAscension.ToString()));
        list.Widgets.Add(Line(loc["chronicle.bestEra"], BestEraName()));
        list.Widgets.Add(Line(loc["chronicle.longestRun"], profile.LongestRunSeconds > 0
            ? DurationFormat.Human(profile.LongestRunSeconds)
            : "—"));

        // Souhrn napříč hrami: „co všechno už mám za sebou".
        list.Widgets.Add(Header(loc["chronicle.totals"]));
        list.Widgets.Add(Line(loc["chronicle.challengesDone"], profile.ChallengesCompleted.ToString()));
        list.Widgets.Add(Line(loc["chronicle.bestContracts"], CivDle.Core.Numbers.Format(profile.BestContracts)));
        list.Widgets.Add(Line(loc["chronicle.bestWonders"], CivDle.Core.Numbers.Format(profile.BestWonders)));
        list.Widgets.Add(Line(
            loc["chronicle.achievements"],
            $"{profile.UnlockedAchievements.Count} / {_screens.Content.Achievements.Count}"));
        list.Widgets.Add(Line(loc["chronicle.playtime"], profile.TotalPlaySeconds > 0
            ? DurationFormat.Human(profile.TotalPlaySeconds)
            : "—"));

        list.Widgets.Add(Header(loc.Format("chronicle.biomes", profile.SettledBiomes.Count, LandBiomeCount())));
        for (int i = 0; i < biomes.Count; i++)
        {
            var biome = biomes[i];
            if (biome.IsWater)
            {
                continue; // na vodě se nestaví, do sbírky nepatří
            }

            bool settled = profile.SettledBiomes.Contains(biome.Id);
            list.Widgets.Add(new Label
            {
                Text = settled ? loc[biome.NameKey] : "· · ·",
                TextColor = settled ? UiPalette.Text : UiPalette.TextDim,
                Tooltip = settled ? null : loc["chronicle.locked"],
            });
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["menu.chronicle"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 380, Width = 440 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Jméno nejvyšší dosažené éry. Kronika si drží ID, ne přeložený text —
    /// jinak by po přepnutí jazyka zůstal v síni slávy český nápis.
    ///
    /// <para>Éra, která mezitím z dat zmizela (mod), se ukáže jako pomlčka;
    /// spadnout kvůli tomu by bylo nepřiměřené.</para>
    /// </summary>
    private string BestEraName()
    {
        string id = _screens.Profile.BestEraId;
        if (string.IsNullOrEmpty(id) || !_screens.Content.Eras.TryIndexOf(id, out int index))
        {
            return "—";
        }

        return _screens.Loc[_screens.Content.Eras[index].NameKey];
    }

    /// <summary>Kolik biomů jde vůbec zastavět — jmenovatel sbírky.</summary>
    private int LandBiomeCount()
    {
        var biomes = _screens.Content.Biomes;
        int count = 0;
        for (int i = 0; i < biomes.Count; i++)
        {
            if (!biomes[i].IsWater)
            {
                count++;
            }
        }

        return count;
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        TextColor = UiFactory.Accent,
    };

    private static HorizontalStackPanel Line(string label, string value)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(new Label { Text = label, Width = 260 });
        row.Widgets.Add(new Label { Text = value, TextColor = UiPalette.Text });
        return row;
    }
}
