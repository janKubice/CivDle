using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Skryté ladicí menu (Ctrl+Shift+D).
///
/// <para>Je to <b>nástroj pro vývoj a testování</b>, ne herní obsah: dostat se
/// k pozdní fázi hry poctivě trvá hodiny, takže bez tohohle se nedá vyzkoušet
/// nic, co se děje po pár Vzestupech. Proto je schované za klávesovou zkratkou
/// a v žádné nabídce na něj nevede tlačítko — hráč, který ho nehledá, na něj
/// nenarazí a nezkazí si tím vlastní postup.</para>
///
/// <para>Nesahá si do vnitřností: používá jen veřejné příkazy simulace, tytéž,
/// které volá běžné UI. Nemůže tedy uvést hru do stavu, do jakého by se
/// normální hrou dostat nešlo.</para>
/// </summary>
public sealed class DebugScreen : IScreen
{
    private const int PanelWidth = 520;

    /// <summary>Kolik se přisype jedním kliknutím.</summary>
    private const double ResourceGrant = 10_000;

    /// <summary>Jak velký kus mapy odhalí jedno kliknutí (v dlaždicích).</summary>
    private const int RevealRadius = 220;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly InputManager _input = new();
    private Desktop? _desktop;
    private Label? _status;

    public DebugScreen(ScreenManager screens, Simulation simulation, Camera2D camera)
    {
        _screens = screens;
        _simulation = simulation;
        _camera = camera;
        BuildUi();
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    private void BuildUi()
    {
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = "DEBUG",
            TextColor = new Color(255, 160, 160),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = "Ladicí nástroje pro testování. Nepatří k hraní.",
            TextColor = Color.Gray,
            Wrap = true,
            Width = PanelWidth - 20,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(Row("+10k od každé suroviny", GrantResources));
        layout.Widgets.Add(Row("+100 bodů Vzestupu", () => GrantAscensionPoints(100)));
        layout.Widgets.Add(Row("+10 000 bodů Vzestupu", () => GrantAscensionPoints(10_000)));
        layout.Widgets.Add(Row("Vyzkoumat vše, na co dosáhnu", ResearchReachable));
        layout.Widgets.Add(Row("Odhalit mapu v okolí", RevealMap));

        _status = new Label
        {
            Text = string.Empty,
            TextColor = new Color(150, 220, 150),
            Wrap = true,
            Width = PanelWidth - 20,
        };
        layout.Widgets.Add(_status);

        layout.Widgets.Add(UiFactory.SmallButton(_screens.Loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget Row(string label, Action action)
    {
        var button = UiFactory.SmallButton(label, action);
        button.HorizontalAlignment = HorizontalAlignment.Center;
        return button;
    }

    private void GrantResources()
    {
        for (int i = 0; i < _simulation.ResourceCount; i++)
        {
            _simulation.AddResource(i, ResourceGrant);
        }

        Report($"přidáno {ResourceGrant:0} od každé z {_simulation.ResourceCount} surovin");
    }

    /// <summary>
    /// Vyzkoumá všechno, co jde zaplatit — opakovaně, protože každý hotový uzel
    /// odemkne další.
    /// </summary>
    private void ResearchReachable()
    {
        int done = 0;
        bool progressed = true;
        while (progressed)
        {
            progressed = false;
            for (int i = 0; i < _screens.Content.Techs.Count; i++)
            {
                if (_simulation.TryResearch(i) == PlacementResult.Ok)
                {
                    done++;
                    progressed = true;
                }
            }
        }

        Report($"vyzkoumáno {done} technologií");
    }

    /// <summary>
    /// Přidá body Vzestupu.
    ///
    /// <para>Bez tohohle se prestižní vrstva testovala jedině tak, že se hra
    /// odehrála až k Vzestupu — a to je u každé změny v nákupech vylepšení
    /// desítky minut. Body jsou obyčejné číslo, takže je stačí přičíst.</para>
    /// </summary>
    private void GrantAscensionPoints(long amount)
    {
        _simulation.DebugGrantPrestigePoints(amount);
        Report($"+{amount} bodů Vzestupu (celkem {_simulation.PrestigePoints})");
    }

    private void RevealMap()
    {
        int tileX = (int)(_camera.Position.X / TerrainRenderer.TileSize);
        int tileY = (int)(_camera.Position.Y / TerrainRenderer.TileSize);
        _simulation.Fog.Reveal(tileX, tileY, RevealRadius);
        Report($"odhaleno {RevealRadius} dlaždic kolem {tileX},{tileY}");
    }

    private void Report(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

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
        _desktop?.Render();
    }

    public void Dispose()
    {
    }
}
