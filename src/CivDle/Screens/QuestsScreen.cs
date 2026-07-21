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
/// Úkoly jako overlay: aktivní (s pokrokem a odměnou) a splněné. Pevné úkoly
/// z <c>quests.json</c> + jeden dynamický, který se pořád posouvá výš. Vede hráče
/// hrou a dává mu pořád co dělat. Simulace mezitím stojí (jen vrchní obrazovka tiká).
/// </summary>
public sealed class QuestsScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public QuestsScreen(ScreenManager screens, Simulation simulation)
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

        _desktop.Render();
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        var list = new VerticalStackPanel { Spacing = 8 };

        list.Widgets.Add(Header(loc["panel.quests.active"]));
        for (int i = 0; i < content.Quests.Count; i++)
        {
            if (!_simulation.IsQuestCompleted(i))
            {
                var quest = content.Quests[i];
                list.Widgets.Add(ActiveRow(loc[quest.NameKey], loc[quest.DescriptionKey], quest.Condition, quest.Reward));
            }
        }

        // Dynamický úkol (vždy aktivní, roste s hrou).
        long dynTarget = _simulation.DynamicQuestTarget;
        var dyn = content.QuestsDynamic;
        list.Widgets.Add(ActiveRow(
            loc.Format("quest.dynamic", dynTarget), loc.Format("quest.dynamic.desc", dynTarget),
            new GoalCondition(dyn.BaseCondition.Kind, dyn.BaseCondition.Param, dynTarget), dyn.BaseReward));

        // Splněné.
        bool anyCompleted = false;
        for (int i = 0; i < content.Quests.Count; i++)
        {
            if (_simulation.IsQuestCompleted(i))
            {
                if (!anyCompleted)
                {
                    list.Widgets.Add(Header(loc["panel.quests.completed"]));
                    anyCompleted = true;
                }

                list.Widgets.Add(CompletedRow(loc[content.Quests[i].NameKey]));
            }
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["hud.quests"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 380, Width = 460 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = new Desktop { Root = root };
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        TextColor = UiFactory.Accent,
    };

    private Widget ActiveRow(string name, string desc, GoalCondition condition, IReadOnlyList<Core.Content.ResourceAmount> reward)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        long current = Math.Min(_simulation.EvaluateMetric(condition.Kind, condition.Param), condition.Target);

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(28, 36, 50, 235)),
        };
        row.Widgets.Add(new Label { Text = name, TextColor = UiFactory.Accent });
        row.Widgets.Add(new Label { Text = desc, TextColor = Color.LightGray, Wrap = true });
        row.Widgets.Add(new Label { Text = $"{current} / {condition.Target}", TextColor = new Color(150, 220, 150) });
        if (reward.Count > 0)
        {
            row.Widgets.Add(new Label
            {
                Text = loc.Format("panel.reward", CostFormat.Line(content, loc, reward)),
                TextColor = Color.Gray,
            });
        }

        return row;
    }

    private Widget CompletedRow(string name)
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel
        {
            Spacing = 8,
            Width = 436,
            Padding = new Thickness(12, 6),
            Background = new SolidBrush(new Color(24, 34, 28, 235)),
        };
        row.Widgets.Add(new Label { Text = name, TextColor = new Color(150, 170, 150) });
        row.Widgets.Add(new Label { Text = loc["quest.done"], TextColor = Color.LightGreen });
        return row;
    }
}
