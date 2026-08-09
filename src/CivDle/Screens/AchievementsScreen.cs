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
            TextColor = UiPalette.TextBright,
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
            Background = new SolidBrush(unlocked ? UiPalette.Panel : UiPalette.Panel),
        };

        // Skrytý a stále zamčený achievement se neprozradí.
        if (!unlocked && achievement.Hidden)
        {
            row.Widgets.Add(new Label { Text = loc["achievement.hidden"], TextColor = UiPalette.TextDim });
            return row;
        }

        row.Widgets.Add(new Label
        {
            Text = loc[achievement.NameKey],
            TextColor = unlocked ? UiPalette.TextBright : UiPalette.TextDim,
        });
        row.Widgets.Add(new Label { Text = loc[achievement.DescriptionKey], TextColor = Color.LightGray, Wrap = true });
        row.Widgets.Add(new Label
        {
            Text = unlocked ? loc["quest.done"] : loc["achievement.locked"],
            TextColor = unlocked ? Color.LightGreen : UiPalette.TextDim,
        });
        return row;
    }
}
