using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Výběr výzvy (scénáře): svět s pevným zadáním, výhrou, prohrou — a odměnou.
///
/// <para>Proč to ve hře je: idle jádro nikdy nekončí, a to je jeho síla i jeho
/// mez. Výzva je ta druhá polovina — hodina, po které se dá říct „hotovo".
/// Volná hra běží dál vedle, tohle ji nenahrazuje: výzva má vlastní save
/// a její odměna (budova, politika) platí v každé další hře.</para>
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

        layout.Widgets.Add(MasterLine());
        if (ContinueButton() is { } resume)
        {
            layout.Widgets.Add(resume);
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
                Text = loc[RuleKey(scenario.Rules[i])],
                TextColor = UiPalette.Warn,
            });
        }

        // Odměna je důvod výzvu hrát — musí být vidět před startem, ne až po výhře.
        string reward = ChallengeRewardNames.ForKey(_screens.Content, loc, ChallengeRewards.KeyOf(scenario.Id));
        if (reward.Length > 0)
        {
            row.Widgets.Add(new Label { Text = loc.Format("scenarios.reward", reward), TextColor = UiPalette.Good, Wrap = true, Width = PanelWidth - 60 });
        }

        if (_screens.Profile.WonChallenges.Contains(scenario.Id))
        {
            row.Widgets.Add(new Label { Text = loc["scenarios.won"], TextColor = UiPalette.Good });
        }

        row.Widgets.Add(UiFactory.SmallButton(loc["scenarios.play"], () => Start(index)));
        return row;
    }

    /// <summary>
    /// Lokalizační klíč pravidla (<c>scenarios.rule.noRoads</c>). Z názvu výčtu,
    /// ne z ručního seznamu: nové pravidlo tak nemůže zůstat bez popisu tiše
    /// — chybějící klíč odhalí test lokalizace.
    /// </summary>
    public static string RuleKey(ScenarioRule rule)
    {
        string name = rule.ToString();
        return $"scenarios.rule.{char.ToLowerInvariant(name[0])}{name[1..]}";
    }

    /// <summary>Kolik výzev hráč dohrál a co dostane za všechny.</summary>
    private Widget MasterLine()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        int won = ChallengeRewards.WonCount(content.Scenarios, _screens.Profile.WonChallenges);
        string master = ChallengeRewardNames.ForKey(
            content, loc, ChallengeRewards.KeyOf(ChallengeRewards.AllChallengesId));
        return new Label
        {
            Text = loc.Format("scenarios.master", won, content.Scenarios.Count, master),
            TextColor = won == content.Scenarios.Count ? UiPalette.Good : UiPalette.Accent,
            Wrap = true,
            Width = PanelWidth - 20,
        };
    }

    /// <summary>
    /// Rozehraná výzva ze samostatného slotu. Bez tohohle by se do výzvy nedalo
    /// vrátit — hlavní „Pokračovat" v menu patří hlavnímu městu.
    /// </summary>
    private Widget? ContinueButton()
    {
        if (!_screens.ChallengeSaves.HasSave)
        {
            return null;
        }

        var loaded = _screens.ChallengeSaves.TryLoad(_screens.Content, out _);
        if (loaded?.Simulation.Scenario is not { } scenario)
        {
            return null;
        }

        return UiFactory.MenuButton(
            _screens.Loc.Format("scenarios.continue", _screens.Loc[scenario.NameKey]),
            () => Resume(loaded));
    }

    /// <summary>
    /// Vrátí se do rozehrané výzvy. Bez dohánění offline času: výzva běží na
    /// herní čas s limitem, a kdyby odtikala, zatímco byl hráč pryč, prohrál by
    /// ve spánku — to by byl přesný opak toho, co idle hra slibuje.
    /// </summary>
    private void Resume(LoadedGame loaded)
    {
        var info = new WorldInfo(loaded.Metadata.Seed, loaded.Metadata.SizeId, loaded.Metadata.PresetId);
        _screens.ReplaceAll(new LoadingScreen(
            _screens, "loading.savedGame", _ => new GameplayScreen(_screens, loaded.Simulation, info)));
    }

    /// <summary>
    /// Postaví svět scénáře a spustí hru.
    ///
    /// <para>Seed je z dat, ne náhodný: scénář má být pro všechny hráče tentýž
    /// svět, jinak se výsledky nedají srovnat a „těžké zadání" znamená u
    /// každého něco jiného. Svět skládá <see cref="ScenarioWorld"/> — tentýž,
    /// který ho skládá při načtení savu.</para>
    /// </summary>
    private void Start(int index)
    {
        var content = _screens.Content;
        var scenario = content.Scenarios[index];
        var simulation = ScenarioWorld.Create(content, index);
        var preset = ScenarioWorld.PresetFor(content, scenario);

        string sizeId = content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id;
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
        MetricKind.DefenceWaves => loc.Format("goal.waves", CivDle.Core.Numbers.Format(goal.Target)),
        _ => loc.Format("goal.generic", goal.Kind.ToString(), CivDle.Core.Numbers.Format(goal.Target)),
    };
}
