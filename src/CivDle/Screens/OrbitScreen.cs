using CivDle.Core;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Oběžná dráha — koncová meta hry.
///
/// <para>Není to jiná mapa, je to <b>jiný pohled na tutéž planetu</b>: kotouč
/// uprostřed, kolem něj dráhy a na nich to, co hráč vypustil. Družice se
/// nestaví na dlaždici, takže by v běžném pohledu nebyly vidět vůbec — a věc,
/// která stojí půlku pozdní ekonomiky, musí být vidět.</para>
///
/// <para>Poloha družic je <b>funkce tiku</b> (<see cref="OrbitSystem.AngleAt"/>),
/// takže se obrázek nemá jak rozejít se savem a obrazovka si nemusí nic
/// pamatovat mezi otevřeními.</para>
///
/// <para>Vrstva: kreslí a volá příkazy simulace. Žádnou logiku nedrží —
/// pravidla („jeden start naráz", „nejdřív kosmodrom") jsou v simulaci.</para>
/// </summary>
public sealed class OrbitScreen : IScreen
{
    private const int PanelWidth = 460;

    /// <summary>Poloměr planety jako podíl kratší strany okna.</summary>
    private const float PlanetRadiusFraction = 0.17f;

    /// <summary>Zploštění drah — dráha zhora vypadá jako elipsa, ne kruh.</summary>
    private const float OrbitFlattening = 0.42f;

    /// <summary>Kolik teček tvoří naznačenou dráhu.</summary>
    private const int OrbitDots = 96;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public OrbitScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
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

    public void Draw(GameTime gameTime)
    {
        DrawSky();
        _desktop.Render();
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    /// <summary>Vypustí družici a přestaví panel — testovací vstup pro smoke běh.</summary>
    internal PlacementResult LaunchForSmoke(int satelliteIndex)
    {
        var result = _simulation.TryLaunchSatellite(satelliteIndex);
        BuildUi();
        return result;
    }

    private void DrawSky()
    {
        var batch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var viewport = _screens.GraphicsDevice.Viewport;

        var center = new Vector2(viewport.Width * 0.5f - (PanelWidth * 0.5f), viewport.Height * 0.5f);
        float planetRadius = Math.Min(viewport.Width, viewport.Height) * PlanetRadiusFraction;

        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(8, 10, 18));

        DrawStars(batch, pixel, viewport);

        // Dráhy se kreslí PŘED planetou i družicemi: jsou to vodicí čáry,
        // ne objekty. Kdyby ležely nahoře, přeškrtávaly by planetu.
        var orbit = _simulation.Orbit;
        var satellites = _screens.Content.Orbit.Satellites;
        for (int i = 0; i < satellites.Count; i++)
        {
            DrawOrbitPath(batch, pixel, center, planetRadius, satellites[i].Altitude);
        }

        if (_screens.Sprites.Get("orbit.planet") is { } planet)
        {
            int size = (int)(planetRadius * 2f);
            batch.Draw(
                planet,
                new Rectangle((int)(center.X - planetRadius), (int)(center.Y - planetRadius), size, size),
                Color.White);
        }

        for (int i = 0; i < satellites.Count; i++)
        {
            DrawSatellites(batch, center, planetRadius, i, orbit.CountOf(i));
        }

        batch.End();
    }

    /// <summary>
    /// Hvězdy v pozadí. Deterministické z indexu, ne náhodné: při každém
    /// otevření obrazovky mají být tytéž — jinak by obloha „skákala".
    /// </summary>
    private static void DrawStars(SpriteBatch batch, Texture2D pixel, Viewport viewport)
    {
        for (int i = 0; i < 220; i++)
        {
            uint hash = (uint)(i * 2654435761u);
            int x = (int)(hash % (uint)Math.Max(1, viewport.Width));
            int y = (int)((hash >> 11) % (uint)Math.Max(1, viewport.Height));
            float brightness = 0.25f + ((hash >> 24) & 0x3F) / 160f;
            batch.Draw(pixel, new Rectangle(x, y, 1 + (int)((hash >> 7) & 1), 1), Color.White * brightness);
        }
    }

    private static void DrawOrbitPath(
        SpriteBatch batch, Texture2D pixel, Vector2 center, float planetRadius, double altitude)
    {
        float radius = planetRadius * (1.35f + (float)altitude * 1.6f);
        for (int i = 0; i < OrbitDots; i++)
        {
            double angle = Math.Tau * i / OrbitDots;
            int x = (int)(center.X + Math.Cos(angle) * radius);
            int y = (int)(center.Y + Math.Sin(angle) * radius * OrbitFlattening);
            batch.Draw(pixel, new Rectangle(x, y, 1, 1), new Color(120, 160, 200) * 0.28f);
        }
    }

    private void DrawSatellites(SpriteBatch batch, Vector2 center, float planetRadius, int kind, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var def = _screens.Content.Orbit[kind];
        var sprite = _screens.Sprites.Get(def.Sprite);
        if (sprite is null)
        {
            return;
        }

        float radius = planetRadius * (1.35f + (float)def.Altitude * 1.6f);
        int size = Math.Max(12, (int)(planetRadius * 0.22f));

        for (int i = 0; i < count; i++)
        {
            double angle = _simulation.Orbit.AngleAt(kind, i, _simulation.TickCount, Simulation.TicksPerSecond);
            float x = center.X + (float)Math.Cos(angle) * radius;
            float y = center.Y + (float)Math.Sin(angle) * radius * OrbitFlattening;

            // Družice za planetou je matnější — bez toho vypadá dráha placatě.
            bool behind = Math.Sin(angle) < 0;
            batch.Draw(
                sprite,
                new Rectangle((int)(x - size / 2f), (int)(y - size / 2f), size, size),
                Color.White * (behind ? 0.45f : 1f));
        }
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var catalog = _screens.Content.Orbit;

        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth - 40 };
        layout.Widgets.Add(new Label
        {
            Text = loc["orbit.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (!_simulation.HasLaunchSite)
        {
            layout.Widgets.Add(Note(loc["orbit.needsLaunchSite"], UiPalette.Warn));
        }
        else if (_simulation.Orbit.UnderConstruction is var building && building >= 0)
        {
            layout.Widgets.Add(Note(
                loc.Format(
                    "orbit.building",
                    loc[$"satellite.{catalog[building].Id}"],
                    (int)Math.Round(_simulation.Orbit.Progress * 100)),
                UiPalette.Accent));
        }
        else if (_simulation.Orbit.TotalLaunched == 0)
        {
            layout.Widgets.Add(Note(loc["orbit.empty"], UiPalette.TextDim));
        }

        for (int i = 0; i < catalog.Count; i++)
        {
            layout.Widgets.Add(SatelliteRow(i));
        }

        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget SatelliteRow(int index)
    {
        var loc = _screens.Loc;
        var def = _screens.Content.Orbit[index];
        int count = _simulation.Orbit.CountOf(index);

        var box = new VerticalStackPanel { Spacing = 3, Width = PanelWidth - 60 };
        box.Widgets.Add(new Label
        {
            Text = $"{loc[$"satellite.{def.Id}"]}   {loc.Format("orbit.inOrbit", count, def.MaxCount)}",
            TextColor = count > 0 ? UiPalette.Good : Color.White,
        });
        box.Widgets.Add(Note(loc[$"satellite.{def.Id}.desc"], UiPalette.TextDim));

        var row = new HorizontalStackPanel { Spacing = 8 };

        if (!_simulation.Orbit.IsFull(index))
        {
            row.Widgets.Add(new Label
            {
                Text = CostFormat.Line(_screens.Content, loc, _simulation.Orbit.NextCost(index)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            var launch = UiFactory.SmallButton(loc["orbit.launch"], () =>
            {
                _simulation.TryLaunchSatellite(index);
                BuildUi();
            });

            // Zhasnuté tlačítko říká „teď to nejde" beze slov; proč, stojí nad
            // seznamem (chybí kosmodrom / jiný start běží / nemáš suroviny).
            launch.Enabled = _simulation.CanLaunchSatellite(index) == PlacementResult.Ok;
            row.Widgets.Add(launch);
        }
        else
        {
            row.Widgets.Add(new Label { Text = loc["orbit.full"], TextColor = UiPalette.TextDim });
        }

        if (count > 0)
        {
            row.Widgets.Add(UiFactory.SmallButton(loc["orbit.dismantle"], () =>
            {
                _simulation.TryDismantleSatellite(index);
                BuildUi();
            }));
        }

        box.Widgets.Add(row);
        return box;
    }

    private Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 60,
    };
}
