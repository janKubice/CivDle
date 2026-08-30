using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Obrana: co přijde, kdy, a čím se to dá zastavit.
///
/// <para>Proč to ve hře je: režim doteď měl v rozhraní <b>jeden řádek</b>
/// („klid, další vlna za 4 min"). Hráč se z něj nedozvěděl, co přijde, čím se
/// bránit ani jestli mu vůbec něco střílí — a mechanika, o které se nedá nic
/// zjistit, působí jako by tam nebyla.</para>
///
/// <para>Ukazuje i <b>obranné budovy, které ještě nemá</b>: to je ta informace,
/// kvůli které se sem hráč podívá poprvé.</para>
///
/// <para>Vrstva: UI. Čte simulaci, nic v ní nemění.</para>
/// </summary>
public sealed class FrontierScreen : IScreen
{
    private const int PanelWidth = 660;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private Label _countdown = null!;

    public FrontierScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();

        // Odpočet se hýbe, i když je panel otevřený — je to jediné číslo,
        // kvůli kterému má smysl se na obrazovku dívat déle než vteřinu.
        _countdown.Text = CountdownText();

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
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["frontier.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        _countdown = new Label
        {
            Text = CountdownText(),
            TextColor = UiPalette.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        layout.Widgets.Add(_countdown);

        var body = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 20 };
        AddScore(body);
        AddNextWave(body);
        AddDefenders(body);
        AddAttackerKinds(body);

        layout.Widgets.Add(new ScrollViewer { Content = body, Width = PanelWidth - 10, Height = 380 });
        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private string CountdownText()
    {
        var loc = _screens.Loc;
        var frontier = _simulation.Frontier;
        if (frontier.Count > 0)
        {
            return loc.Format("frontier.enemies", frontier.Count);
        }

        double seconds = Math.Max(0, frontier.NextWaveTick - _simulation.TickCount) / Simulation.TicksPerSecond;
        return loc.Format("frontier.calm", DurationFormat.Human(seconds));
    }

    private void AddScore(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        var frontier = _simulation.Frontier;

        box.Widgets.Add(new Label { Text = loc["frontier.score"], TextColor = UiPalette.Accent });
        box.Widgets.Add(new Label
        {
            Text = loc.Format("frontier.killed", frontier.Killed),
            TextColor = UiPalette.Good,
        });

        // „Kolik jich prošlo" je ta správná míra neúspěchu: zásah město
        // nezničí, jen vyřadí budovu z výroby, takže se to jinak pozná těžko.
        box.Widgets.Add(new Label
        {
            Text = loc.Format("frontier.reached", frontier.ReachedCity),
            TextColor = frontier.ReachedCity > 0 ? UiPalette.Bad : UiPalette.TextDim,
        });
    }

    private void AddNextWave(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        var content = _screens.Content.Frontier;
        if (!content.IsAvailable)
        {
            return;
        }

        box.Widgets.Add(new Label { Text = loc["frontier.nextWave"], TextColor = UiPalette.Accent });

        foreach (var entry in content.WaveAt(_simulation.Frontier.NextWave))
        {
            var def = content.Attackers[entry.AttackerIndex];
            box.Widgets.Add(new Label
            {
                Text = loc.Format("frontier.waveEntry", entry.Count, loc[$"attacker.{def.Id}"]),
                TextColor = UiPalette.Text,
            });
        }
    }

    /// <summary>
    /// Co ve městě střílí — a co by mohlo. Zamčené se ukazují taky: kvůli
    /// tomu se sem hráč dívá poprvé.
    /// </summary>
    private void AddDefenders(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        box.Widgets.Add(new Label { Text = loc["frontier.defenders"], TextColor = UiPalette.Accent });

        bool any = false;
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var def = content.Buildings[i];
            if (!def.IsArmed)
            {
                continue;
            }

            any = true;
            long standing = _simulation.CountBuildingsOfType(i);
            var row = new VerticalStackPanel
            {
                Spacing = 2,
                Width = PanelWidth - 60,
                Padding = new Thickness(10, 5),
                Background = new SolidBrush(UiPalette.Panel),
            };

            row.Widgets.Add(new Label
            {
                Text = loc.Format("frontier.defender", loc[def.NameKey], standing),
                TextColor = standing > 0 ? UiPalette.Good : UiPalette.Text,
            });

            var rule = def.Defense!;
            row.Widgets.Add(new Label
            {
                Text = loc.Format("frontier.defenderStats", rule.Range, rule.Damage),
                TextColor = UiPalette.TextDim,
            });

            box.Widgets.Add(row);
        }

        if (!any)
        {
            box.Widgets.Add(new Label { Text = loc["frontier.noDefenders"], TextColor = UiPalette.Warn });
        }
    }

    private void AddAttackerKinds(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        var content = _screens.Content.Frontier;

        box.Widgets.Add(new Label { Text = loc["frontier.kinds"], TextColor = UiPalette.Accent });

        foreach (var def in content.Attackers)
        {
            box.Widgets.Add(new Label
            {
                Text = loc.Format("frontier.kind", loc[$"attacker.{def.Id}"], def.Health, def.Damage),
                TextColor = UiPalette.Text,
            });
        }
    }
}
