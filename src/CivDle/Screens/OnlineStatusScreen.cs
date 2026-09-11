using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Stav online funkcí: co běží, co ne a co z toho plyne.
///
/// <para>Proč to ve hře je: bez Steamu se achievementy ukládají lokálně,
/// Workshop nejde a karavany nevozí kamarády — a hráč o tom <b>neměl jak
/// vědět</b>. Tichá degradace je horší než hláška: hra vypadá rozbitě
/// („proč mi nepřibyl achievement?") místo aby vypadala offline.</para>
///
/// <para>Každý řádek říká, co to znamená, ne jen ano/ne. „Steam: ne" hráči
/// neřekne nic; „achievementy se ukládají jen k tobě do počítače" ano.</para>
///
/// <para>Vrstva: UI nad <see cref="Core.Platform.IPlatformServices"/>.</para>
/// </summary>
public sealed class OnlineStatusScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public OnlineStatusScreen(ScreenManager screens)
    {
        _screens = screens;
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

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var platform = _screens.Platform;
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["online.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(Row(
            loc["online.platform"],
            platform.IsAvailable,
            loc[platform.IsAvailable ? "online.platform.on" : "online.platform.off"]));

        layout.Widgets.Add(Row(
            loc["online.achievements"],
            platform.IsAvailable,
            loc[platform.IsAvailable ? "online.achievements.on" : "online.achievements.off"]));

        // Žebříčky jsou zvlášť: i s běžícím Steamem jsou v téhle hře lokální
        // (viz plán §7.0), a slibovat porovnání s ostatními by byla lež.
        layout.Widgets.Add(Row(
            loc["online.leaderboards"],
            platform.HasOnlineLeaderboards,
            loc[platform.HasOnlineLeaderboards ? "online.leaderboards.on" : "online.leaderboards.off"]));

        layout.Widgets.Add(Row(
            loc["online.workshop"],
            platform.IsAvailable,
            loc[platform.IsAvailable ? "online.workshop.on" : "online.workshop.off"]));

        layout.Widgets.Add(Row(
            loc["online.friends"],
            platform.IsAvailable,
            loc[platform.IsAvailable ? "online.friends.on" : "online.friends.off"]));

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("online.playerName", platform.PlayerName),
            TextColor = UiPalette.TextDim,
        });

        // Věta, která uklidní: nic z toho hru neomezuje. Bez ní vypadá seznam
        // křížků jako seznam chyb.
        layout.Widgets.Add(new Label
        {
            Text = loc["online.noWorries"],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private static Widget Row(string caption, bool on, string meaning)
    {
        var row = new VerticalStackPanel
        {
            Spacing = 2,
            Width = PanelWidth - 40,
            Padding = new Thickness(10, 6),
            Background = new PanelBrush(UiPalette.Panel),
        };

        var header = new HorizontalStackPanel { Spacing = 8 };
        header.Widgets.Add(new Label
        {
            Text = on ? "●" : "○",
            TextColor = on ? UiPalette.Good : UiPalette.TextDim,
        });
        header.Widgets.Add(new Label { Text = caption, TextColor = UiPalette.TextBright });
        row.Widgets.Add(header);

        row.Widgets.Add(new Label
        {
            Text = meaning,
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 70,
        });

        return row;
    }
}
