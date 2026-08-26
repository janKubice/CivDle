using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Výběr scénáře: svět s pevným zadáním, výhrou a prohrou.
///
/// <para>Proč to ve hře je: idle jádro nikdy nekončí, a to je jeho síla i jeho
/// mez. Scénář je ta druhá polovina — hodina, po které se dá říct „hotovo".
/// Volná hra běží dál vedle, tohle ji nenahrazuje.</para>
///
/// <para>Vrstva: UI. Skládá svět podle dat scénáře a předá ho hře; pravidla
/// (co je výhra, co prohra) jsou v simulaci.</para>
/// </summary>
public sealed class ScenariosScreen : IScreen
{
    private const int PanelWidth = 700;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public ScenariosScreen(ScreenManager screens)
    {
        _screens = screens;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["scenarios.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["scenarios.desc"],
            TextColor = Color.LightGray,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        var list = new VerticalStackPanel { Spacing = 8, Width = PanelWidth - 20 };
        var catalog = _screens.Content.Scenarios;
        for (int i = 0; i < catalog.Count; i++)
        {
            list.Widgets.Add(Row(catalog[i], i));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Width = PanelWidth - 10, Height = 380 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["newgame.back"], _screens.Pop));

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    private Widget Row(ScenarioDef scenario, int index)
    {
        var loc = _screens.Loc;
        var row = new VerticalStackPanel { Spacing = 3, Width = PanelWidth - 40 };

        row.Widgets.Add(new Label { Text = loc[scenario.NameKey], TextColor = UiPalette.TextBright });
        row.Widgets.Add(new Label
        {
            Text = loc[scenario.DescriptionKey],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 60,
        });

        row.Widgets.Add(new Label
        {
            Text = loc.Format("scenarios.goal", GoalText.Of(scenario.Goal, loc)) + "   "
                + loc.Format("scenarios.timeLimit", scenario.HasTimeLimit
                    ? DurationFormat.Human(scenario.TimeLimitSeconds)
                    : loc["scenarios.noTimeLimit"]),
            TextColor = UiPalette.Accent,
        });

        // Zvláštní pravidla musí být vidět PŘED startem: „guvernér nestaví" se
        // jinak hráč dozví až tím, že mu půl hodiny nic nepřibývá.
        for (int i = 0; i < scenario.Rules.Count; i++)
        {
            row.Widgets.Add(new Label
            {
                Text = loc[$"scenarios.rule.{RuleKey(scenario.Rules[i])}"],
                TextColor = UiPalette.Warn,
            });
        }

        row.Widgets.Add(UiFactory.SmallButton(loc["scenarios.play"], () => Start(scenario, index)));
        return row;
    }

    private static string RuleKey(ScenarioRule rule) => rule switch
    {
        ScenarioRule.NoAutoBuild => "noAutoBuild",
        ScenarioRule.NoAscension => "noAscension",
        _ => rule.ToString(),
    };

    /// <summary>
    /// Postaví svět scénáře a spustí hru.
    ///
    /// <para>Seed je z dat, ne náhodný: scénář má být pro všechny hráče tentýž
    /// svět, jinak se výsledky nedají srovnat a „těžké zadání" znamená u
    /// každého něco jiného.</para>
    /// </summary>
    private void Start(ScenarioDef scenario, int index)
    {
        var content = _screens.Content;

        // Přebití čísel jde přes WithGameplay — táž cesta, kterou používají
        // nástroje na balanc. Druhá by znamenala dvě místa, kde se skládá
        // nastavení hry.
        var scenarioContent = scenario.Gameplay.IsEmpty
            ? content
            : content.WithGameplay(scenario.Gameplay.Apply(content.Gameplay));

        var preset = scenarioContent.WorldGen.Presets[
            scenario.PresetIndex >= 0 ? scenario.PresetIndex : scenarioContent.WorldGen.DefaultPresetIndex];

        var terrain = new ProceduralTerrain(scenarioContent.Biomes, preset, scenario.Seed);
        var simulation = new Simulation(scenarioContent, terrain, scenario.Seed);
        simulation.StartScenario(index);

        string sizeId = scenarioContent.WorldGen.Sizes[scenarioContent.WorldGen.DefaultSizeIndex].Id;
        var info = new WorldInfo(scenario.Seed, sizeId, preset.Id);

        _screens.ReplaceAll(new LoadingScreen(
            _screens, "loading.newWorld", _ => new GameplayScreen(_screens, simulation, info)));
    }
}

/// <summary>
/// Cíl scénáře slovy.
///
/// <para>Vlastní třída, protože tentýž cíl se ukazuje na dvou místech — při
/// výběru a pak celou hru v rohu. Kdyby si ho každé skládalo po svém, slibovalo
/// by menu něco jiného, než co hra počítá.</para>
/// </summary>
public static class GoalText
{
    /// <summary>Podmínka jako věta pro hráče.</summary>
    public static string Of(GoalCondition goal, Localization loc) => goal.Kind switch
    {
        MetricKind.Population => loc.Format("goal.population", CivDle.Core.Numbers.Format(goal.Target)),
        MetricKind.TotalBuildings => loc.Format("goal.buildings", CivDle.Core.Numbers.Format(goal.Target)),
        _ => loc.Format("goal.generic", goal.Kind.ToString(), CivDle.Core.Numbers.Format(goal.Target)),
    };
}
