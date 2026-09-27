using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Archiv měst: města, která hráč odložil Novou hrou+ (endgame.md, C3).
/// Každé jde vrátit do hlavního slotu a hrát dál; to, co se hrálo předtím,
/// se samo archivuje — výměna nikdy nic nesmaže.
///
/// <para>Archivované město se po návratu nedohání offline: v archivu
/// nestárlo, jen čekalo. Dohánět měsíce odložení by z archivu udělalo
/// automat na zdroje.</para>
/// </summary>
public sealed class ArchiveScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private string? _status;

    public ArchiveScreen(ScreenManager screens)
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
            Text = loc["archive.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = loc["archive.desc"], TextColor = UiPalette.Text, Wrap = true, Width = PanelWidth - 20 });

        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 30 };
        foreach (string path in _screens.Saves.ArchivedFiles())
        {
            string name = Path.GetFileNameWithoutExtension(path);
            list.Widgets.Add(UiFactory.SmallButton(loc.Format("archive.play", name), () => Restore(path)));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Width = PanelWidth - 10, Height = 320 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["newgame.back"], _screens.Pop));
        if (_status is not null)
        {
            layout.Widgets.Add(new Label { Text = _status, TextColor = UiPalette.Bad });
        }

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    private void Restore(string path)
    {
        if (!_screens.Saves.TryRestoreFromArchive(path, DateTime.UtcNow)
            || _screens.Saves.TryLoad(_screens.Galaxy, out _) is not { } loaded)
        {
            _status = _screens.Loc["menu.loadFailed"];
            BuildUi();
            return;
        }

        // Archivované město mohlo mít galaxii — vrací se celá, s koloniemi.
        var info = new WorldInfo(loaded.Metadata.Seed, loaded.Metadata.SizeId, loaded.Metadata.PresetId);
        var session = Core.Galaxy.GalaxySession.Resume(_screens.Galaxy, loaded);
        _screens.BeginSession(session);
        _screens.ReplaceAll(new LoadingScreen(
            _screens, "loading.savedGame", _ => new GameplayScreen(_screens, loaded.Simulation, info, null, session)));
    }
}
