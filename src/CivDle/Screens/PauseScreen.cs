using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Pauzovací menu jako overlay nad hrou: ztmaví běžící scénu a nabídne uložení,
/// návrat do hry, nastavení, hlavní menu, nebo ukončení. Simulace stojí
/// automaticky — ScreenManager aktualizuje jen vrchní obrazovku.
/// </summary>
public sealed class PauseScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly WorldInfo _info;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private Label _saveStatusLabel = null!;
    private string _saveStatusText = " ";

    public PauseScreen(ScreenManager screens, Simulation simulation, WorldInfo info)
    {
        _screens = screens;
        _simulation = simulation;
        _info = info;
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
        // Ztmavení hry pod menu — hra zůstává vidět, ale je jasné, že stojí.
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
        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["pause.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        _saveStatusLabel = new Label
        {
            Text = _saveStatusText,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGreen,
        };
        layout.Widgets.Add(_saveStatusLabel);
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.resume"], _screens.Pop));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.save"], SaveGame));
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.howto"], () => _screens.Push(new HowToPlayScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.settings"], () => _screens.Push(new SettingsScreen(_screens, showBackground: false))));
        // Odchod z rozehrané hry vždycky uloží — hráč nemá o postup přijít proto,
        // že zapomněl kliknout na „Uložit".
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.mainMenu"], () =>
        {
            SaveGame();
            _screens.ReplaceAll(new MainMenuScreen(_screens));
        }));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.quit"], () =>
        {
            SaveGame();
            _screens.ExitGame();
        }));

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    private void SaveGame()
    {
        var metadata = new SaveMetadata(_info.Seed, _info.SizeId, _info.PresetId, DateTime.UtcNow);
        bool saved = _screens.Saves.TrySave(_simulation, metadata);

        _saveStatusText = _screens.Loc[saved ? "pause.saved" : "pause.saveFailed"];
        _saveStatusLabel.Text = _saveStatusText;
        _saveStatusLabel.TextColor = saved ? Color.LightGreen : new Color(235, 120, 110);
    }
}
