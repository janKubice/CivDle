using CivDle.Core.Content.Mods;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Správce modů: co je nainstalované, co je zapnuté a co je rozbité.
///
/// <para>Ukazuje <b>i vadné mody</b> a důvod, proč nejdou načíst. Bez toho se
/// hráč o špatném modu dozví jedině tak, že mu hra nenaběhne — a nemá kde ho
/// vypnout, takže ho musí hledat v souborech.</para>
///
/// <para>Změny se projeví až po restartu a je to napsané nahoře. Načítat obsah
/// za běhu by znamenalo přestavět indexy, na které se odkazuje rozehraná
/// simulace — z toho by vznikly budovy, které ukazují na jinou definici, než
/// se kterou byly postavené.</para>
///
/// <para>Vrstva: UI. Sahá na soubory přes <see cref="ModInspector"/>, herní
/// logiku nezná.</para>
/// </summary>
public sealed class ModManagerScreen : IScreen
{
    private const int PanelWidth = 760;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private IReadOnlyList<ModInspection> _mods = Array.Empty<ModInspection>();

    /// <summary>Změnil hráč něco, co se projeví až po restartu?</summary>
    private bool _dirty;

    public ModManagerScreen(ScreenManager screens)
    {
        _screens = screens;
        Refresh();
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.66f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void Refresh()
    {
        _mods = ModInspector.Inspect(ModsDirectory, _screens.Platform.SubscribedModDirectories());
        BuildUi();
    }

    /// <summary>Složka s mody vedle hry — tam, kde je hledá i loader při startu.</summary>
    private static string ModsDirectory => Path.Combine(AppContext.BaseDirectory, "mods");

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc["mods.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (_dirty)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["mods.restartNeeded"],
                TextColor = UiPalette.TextBright,
                Wrap = true,
                Width = PanelWidth,
            });
        }

        if (_mods.Count == 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("mods.empty", ModsDirectory),
                TextColor = Color.LightGray,
                Wrap = true,
                Width = PanelWidth,
            });
        }
        else
        {
            var list = new VerticalStackPanel { Spacing = 6 };
            foreach (var mod in _mods)
            {
                list.Widgets.Add(ModRow(mod));
            }

            layout.Widgets.Add(new ScrollViewer { Content = list, Height = 340, Width = PanelWidth });
        }

        // Pořadí načítání je vidět dole, protože je to nejčastější zdroj
        // nedorozumění: dva mody sáhnou na tutéž budovu a vyhraje pozdější.
        layout.Widgets.Add(new Label
        {
            Text = loc["mods.orderHint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = PanelWidth,
        });

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["mods.refresh"], Refresh));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["mods.newMod"], () => _screens.Push(new ModEditorScreen(_screens))));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        layout.Widgets.Add(buttons);

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget ModRow(ModInspection mod)
    {
        var loc = _screens.Loc;
        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = PanelWidth - 24,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(mod.Status == ModStatus.Broken
                ? UiPalette.Panel
                : UiPalette.Panel),
        };

        var header = new HorizontalStackPanel { Spacing = 8 };
        header.Widgets.Add(new Label
        {
            Text = $"{mod.Name}  {mod.Version}",
            TextColor = mod.Status switch
            {
                ModStatus.Enabled => UiPalette.Good,
                ModStatus.Disabled => UiPalette.Text,
                _ => UiPalette.Warn,
            },
        });

        if (mod.FromWorkshop)
        {
            header.Widgets.Add(new Label { Text = loc["mods.fromWorkshop"], TextColor = UiPalette.Accent });
        }

        row.Widgets.Add(header);

        if (mod.Status == ModStatus.Broken || mod.Problem.Length > 0)
        {
            row.Widgets.Add(new Label
            {
                Text = mod.Problem,
                TextColor = UiPalette.Warn,
                Wrap = true,
                Width = PanelWidth - 60,
            });
        }

        if (mod.DataFiles.Count > 0)
        {
            row.Widgets.Add(new Label
            {
                Text = loc.Format("mods.overrides", string.Join(", ", mod.DataFiles)),
                TextColor = UiPalette.TextDim,
                Wrap = true,
                Width = PanelWidth - 60,
            });
        }

        // Vadný mod nemá co zapínat — tlačítko by jen slibovalo něco, co se
        // stejně nenačte.
        if (mod.Status != ModStatus.Broken)
        {
            bool enabled = mod.Status == ModStatus.Enabled;
            row.Widgets.Add(UiFactory.SmallButton(
                loc[enabled ? "mods.disable" : "mods.enable"],
                () =>
                {
                    if (ModInspector.SetEnabled(mod, !enabled))
                    {
                        _dirty = true;
                        Refresh();
                    }
                }));
        }

        return row;
    }
}
