using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Achievementy jako overlay: odemčené (zlaté, s popisem) a zamčené (šedé; skryté
/// jen „???"). Účet-wide — přetrvávají napříč hrami. ID jsou stabilní i pro budoucí
/// napojení na Steam. Simulace mezitím stojí.
/// </summary>
public sealed class AchievementsScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public AchievementsScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
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

        _desktop.Render();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var achievements = _screens.Content.Achievements;

        int unlockedCount = 0;
        for (int i = 0; i < achievements.Count; i++)
        {
            if (_simulation.IsAchievementUnlocked(i))
            {
                unlockedCount++;
            }
        }

        var list = new VerticalStackPanel { Spacing = 8 };
        for (int i = 0; i < achievements.Count; i++)
        {
            list.Widgets.Add(Row(i));
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["hud.achievements"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("achievement.unlockedCount", unlockedCount, achievements.Count),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(230, 200, 110),
        });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 360, Width = 460 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget Row(int index)
    {
        var loc = _screens.Loc;
        var achievement = _screens.Content.Achievements[index];
        bool unlocked = _simulation.IsAchievementUnlocked(index);

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(unlocked ? new Color(40, 36, 22, 235) : new Color(26, 30, 38, 235)),
        };

        // Skrytý a stále zamčený achievement se neprozradí.
        if (!unlocked && achievement.Hidden)
        {
            row.Widgets.Add(new Label { Text = loc["achievement.hidden"], TextColor = new Color(120, 125, 135) });
            return row;
        }

        row.Widgets.Add(new Label
        {
            Text = loc[achievement.NameKey],
            TextColor = unlocked ? new Color(240, 210, 120) : new Color(160, 165, 175),
        });
        row.Widgets.Add(new Label { Text = loc[achievement.DescriptionKey], TextColor = Color.LightGray, Wrap = true });
        row.Widgets.Add(new Label
        {
            Text = unlocked ? loc["quest.done"] : loc["achievement.locked"],
            TextColor = unlocked ? Color.LightGreen : new Color(150, 150, 160),
        });
        return row;
    }
}
