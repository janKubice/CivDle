using CivDle.Core;
using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Mapa galaxie (svety-design.md 5.4, 7.8): hvězdné pole s paralaxou, planety
/// jako otáčející se koule upečené z mapy světa, karta vybraného světa
/// a všechno, co se se světem dá dělat — odletět, postavit kolonizační loď,
/// vložit do ní přebytky, vybrat místo přistání.
///
/// <para>Vrstva: UI. Pravidla (kdy jde loď stavět, co stojí, kam jde odletět)
/// drží <see cref="GalaxySession"/>; obrazovka jen ukazuje a volá.</para>
/// </summary>
public sealed class GalaxyScreen : IScreen
{
    private const int PanelWidth = 440;

    /// <summary>Upečené povrchy — pečení stojí desítky milisekund na planetu, otevírá se často.</summary>
    private static readonly Dictionary<string, PlanetSurface> SurfaceCache = new(StringComparer.Ordinal);

    private readonly ScreenManager _screens;
    private readonly GalaxySession _session;
    private readonly InputManager _input = new();
    private readonly List<Planet> _planets = new();
    private Desktop _desktop = null!;
    private FontStashSharp.SpriteFontBase? _font;
    private string _selected;
    private string? _hovered;
    private float _time;

    /// <summary>Kdy se naposled přestavěla karta (trasy mění čísla za běhu).</summary>
    private float _cardBuiltAt;

    /// <summary>Jak často se karta s trasami obnovuje (sekundy reálného času).</summary>
    private const float CardRefreshSeconds = 2f;

    public GalaxyScreen(ScreenManager screens, GalaxySession session)
    {
        _screens = screens;
        _session = session;
        _session.State.Refresh(session.Active);
        _selected = session.State.ActiveWorldId;
        BuildPlanets();
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    /// <summary>Vybraný svět (smoke test a testy karty).</summary>
    internal string Selected => _selected;

    public void OnActivated()
    {
        _input.Resync();
        BuildUi(); // návrat z výběru přistání: loď mohla zmizet
    }

    public void Update(GameTime gameTime)
    {
        _input.Update();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _session.Update();

        if (_input.WasPressed(Keys.Escape) || _input.WasPressed(Keys.G))
        {
            _screens.Pop();
            return;
        }

        var viewport = _screens.GraphicsDevice.Viewport;
        Layout(viewport);
        var mouse = _input.MousePosition.ToVector2();
        _hovered = null;
        foreach (var planet in _planets)
        {
            planet.Disk.Update(dt, planet.World.IsHome ? 0.012f : 0.02f);
            if (Vector2.Distance(mouse, planet.Center) <= planet.Disk.Diameter / 2f + 6)
            {
                _hovered = planet.World.Id;
            }
        }

        if (_input.WasLeftPressed && _hovered is not null && _hovered != _selected
            && mouse.X < viewport.Width - PanelWidth * UiScale())
        {
            Select(_hovered);
        }

        if (_input.WasPressed(Keys.Right) || _input.WasPressed(Keys.Left))
        {
            int index = _planets.FindIndex(p => p.World.Id == _selected);
            int step = _input.WasPressed(Keys.Right) ? 1 : -1;
            Select(_planets[(index + step + _planets.Count) % _planets.Count].World.Id);
        }

        // Karta kolonie s trasami ukazuje živá čísla (tok, zboží na cestě).
        if (_time - _cardBuiltAt >= CardRefreshSeconds && _session.State.Trade.Routes.Count > 0)
        {
            BuildUi();
        }
    }

    public void Draw(GameTime gameTime)
    {
        var batch = _screens.SpriteBatch;
        var viewport = _screens.GraphicsDevice.Viewport;
        _font ??= Myra.Graphics2D.UI.Styles.Stylesheet.Current.LabelStyle.Font;
        Layout(viewport);

        batch.Begin(samplerState: SamplerState.PointClamp);
        DrawSky(batch, viewport);
        batch.End();

        batch.Begin(samplerState: SamplerState.PointClamp);
        foreach (var planet in _planets)
        {
            DrawPlanet(batch, planet);
        }

        batch.End();
        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        foreach (var planet in _planets)
        {
            planet.Disk.Dispose();
        }
    }

    /// <summary>Vybere svět (klik, šipky, smoke).</summary>
    internal void Select(string worldId)
    {
        _selected = worldId;
        BuildUi();
    }

    // ----- planety -----

    private sealed class Planet
    {
        public Planet(WorldDef world, PlanetDisk disk)
        {
            World = world;
            Disk = disk;
        }

        public WorldDef World { get; }

        public PlanetDisk Disk { get; }

        public Vector2 Center { get; set; }
    }

    private void BuildPlanets()
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var worlds = _session.Catalog.Worlds;
        float area = Math.Max(300, viewport.Width - PanelWidth * UiScale());
        float baseDiameter = Math.Min(area / Math.Max(1, worlds.Count) * 0.78f, viewport.Height * 0.24f);
        foreach (var world in worlds)
        {
            int diameter = (int)(baseDiameter * (float)Math.Clamp(world.Planet.Size, 0.5, 1.6));
            var look = world.Planet;
            var atmosphere = Color.Lerp(new Color(look.Accent.R, look.Accent.G, look.Accent.B), Color.White, 0.4f);
            _planets.Add(new Planet(world, new PlanetDisk(_screens.GraphicsDevice, SurfaceOf(world), diameter, atmosphere)));
        }
    }

    /// <summary>
    /// Povrch planety: založený svět se peče z vlastního terénu a zástavby
    /// (planeta je doopravdy tvoje mapa), ostatní z barev v datech.
    /// </summary>
    private PlanetSurface SurfaceOf(WorldDef world)
    {
        if (!_session.State.Records.TryGetValue(world.Id, out var record))
        {
            return Cached($"{world.Id}:look", () => PlanetSurface.FromLook(world.Planet, world.Order * 7919L));
        }

        bool active = world.Id == _session.State.ActiveWorldId;
        double population = active ? _session.Active.Population : record.EstimatedPopulation(_session.NowSeconds);
        int bucket = (int)Math.Log10(population + 1); // světla se přepečou až s řádem obyvatel
        return Cached($"{world.Id}:{record.Seed}:{bucket}", () =>
        {
            var content = _session.Contents.For(world.Id);
            var terrain = active ? _session.Active.Terrain : WorldTerrain.Create(content, PresetOf(content, record), record.Seed);
            int x = active ? _session.Active.CityCenterX : Math.Max(0, record.LandingX);
            int y = active ? _session.Active.CityCenterY : Math.Max(0, record.LandingY);
            return PlanetSurface.FromTerrain(content, terrain, world.Planet, x, y, population);
        });
    }

    private static TerrainPreset PresetOf(GameContent content, WorldRecord record)
    {
        foreach (var preset in content.WorldGen.Presets)
        {
            if (preset.Id == record.PresetId)
            {
                return preset;
            }
        }

        return content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
    }

    private static PlanetSurface Cached(string key, Func<PlanetSurface> bake)
    {
        if (!SurfaceCache.TryGetValue(key, out var surface))
        {
            if (SurfaceCache.Count > 32)
            {
                SurfaceCache.Clear();
            }

            surface = bake();
            SurfaceCache[key] = surface;
        }

        return surface;
    }

    /// <summary>Planety na mírném oblouku zleva doprava v pořadí světů.</summary>
    private void Layout(Viewport viewport)
    {
        float area = Math.Max(300, viewport.Width - PanelWidth * UiScale());
        for (int i = 0; i < _planets.Count; i++)
        {
            float x = area * (i + 0.5f) / _planets.Count;
            float y = viewport.Height * (0.5f + MathF.Sin(i * 1.3f + 0.4f) * 0.16f);
            _planets[i].Center = new Vector2(x, y);
        }
    }

    // ----- kreslení -----

    /// <summary>Tři vrstvy hvězd, každá pluje jinou rychlostí (paralaxa), a mlhovina.</summary>
    private void DrawSky(SpriteBatch batch, Viewport viewport)
    {
        var pixel = _screens.WhitePixel;
        batch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(6, 8, 18));

        // Mlhovina: pár měkkých skvrn v barvách světů — galaxie má barvu toho, co v ní je.
        for (int i = 0; i < _planets.Count; i++)
        {
            var look = _planets[i].World.Planet;
            var color = new Color(look.Accent.R, look.Accent.G, look.Accent.B) * 0.10f;
            int size = (int)(viewport.Height * 0.9f);
            float drift = MathF.Sin(_time * 0.05f + i) * 30f;
            _screens.SoftShadow.Draw(batch, new Rectangle(
                (int)(viewport.Width * (0.1f + 0.14f * i) - size / 2f + drift),
                (int)(viewport.Height * (i % 2 == 0 ? 0.3f : 0.7f) - size / 2f), size, size), color);
        }

        for (int layer = 0; layer < 3; layer++)
        {
            float speed = 2f + layer * 5f;
            for (int i = 0; i < 180; i++)
            {
                uint hash = (uint)((i + layer * 1000) * 2654435761u);
                float x = ((hash % (uint)Math.Max(1, viewport.Width)) + _time * speed) % viewport.Width;
                int y = (int)((hash >> 11) % (uint)Math.Max(1, viewport.Height));
                float twinkle = 0.75f + 0.25f * MathF.Sin(_time * (1f + (hash & 7)) + i);
                float brightness = (0.2f + layer * 0.22f + ((hash >> 24) & 0x1F) / 200f) * twinkle;
                int size = layer == 2 && (hash & 3) == 0 ? 2 : 1;
                batch.Draw(pixel, new Rectangle((int)x, y, size, size), Color.White * brightness);
            }
        }
    }

    private void DrawPlanet(SpriteBatch batch, Planet planet)
    {
        var world = planet.World;
        var availability = _session.State.AvailabilityOf(world);
        float radius = planet.Disk.Diameter / 2f;
        var pixel = _screens.WhitePixel;

        // Záře kolem založeného světa: tady žijí lidé.
        if (availability == WorldAvailability.Colony)
        {
            int glow = (int)(radius * 3.2f);
            _screens.SoftShadow.Draw(batch, new Rectangle(
                (int)(planet.Center.X - glow / 2f), (int)(planet.Center.Y - glow / 2f), glow, glow),
                new Color(255, 220, 160) * 0.12f);
        }

        if (world.Planet.Ring)
        {
            DrawRing(batch, pixel, planet.Center, radius, behind: true);
        }

        planet.Disk.Draw(batch, planet.Center, availability == WorldAvailability.Locked ? 0.28f : 1f);

        if (world.Planet.Ring)
        {
            DrawRing(batch, pixel, planet.Center, radius, behind: false);
        }

        // Výběr a najetí: tečkovaný kruh kolem planety.
        if (world.Id == _selected || world.Id == _hovered)
        {
            float r = radius + 8;
            var color = world.Id == _selected ? UiPalette.Accent : Color.White * 0.5f;
            for (int i = 0; i < 64; i++)
            {
                float a = MathF.Tau * i / 64 + _time * 0.3f;
                batch.Draw(pixel, new Rectangle((int)(planet.Center.X + MathF.Cos(a) * r), (int)(planet.Center.Y + MathF.Sin(a) * r), 2, 2), color);
            }
        }

        if (_font is null)
        {
            return;
        }

        var loc = _screens.Loc;
        string label = loc[world.NameKey];
        if (availability == WorldAvailability.Locked)
        {
            label += world.RequiresGate ? "  ⌾" : $"  ★{world.StarsRequired}";
        }

        var size = _font.MeasureString(label);
        var at = new Vector2(planet.Center.X - size.X / 2f, planet.Center.Y + radius + 12);
        batch.DrawString(_font, label, at + Vector2.One, Color.Black * 0.8f);
        batch.DrawString(_font, label, at, availability == WorldAvailability.Locked ? UiPalette.TextDim : UiPalette.TextBright);

        if (world.Id == _session.State.ActiveWorldId)
        {
            string here = loc["galaxy.here"];
            var hereSize = _font.MeasureString(here);
            batch.DrawString(_font, here, new Vector2(planet.Center.X - hereSize.X / 2f, at.Y + size.Y + 2), UiPalette.Accent);
        }
    }

    private void DrawRing(SpriteBatch batch, Texture2D pixel, Vector2 center, float radius, bool behind)
    {
        for (int i = 0; i < 180; i++)
        {
            float a = MathF.Tau * i / 180;
            float y = MathF.Sin(a);
            if ((y < 0) != behind)
            {
                continue; // zadní půlka pod planetou, přední přes ni
            }

            float x = MathF.Cos(a) * radius * 1.7f;
            batch.Draw(pixel, new Rectangle((int)(center.X + x), (int)(center.Y + y * radius * 0.35f), 2, 1),
                new Color(226, 214, 190) * 0.7f);
        }
    }

    private float UiScale() => _screens.Settings.UiScaleFor(_screens.GraphicsDevice.Viewport.Height);

    // ----- karta světa -----

    private void BuildUi()
    {
        _cardBuiltAt = _time;
        var loc = _screens.Loc;
        var world = _session.Catalog.Find(_selected) ?? _session.Catalog.Worlds[0];
        var availability = _session.State.AvailabilityOf(world);

        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth - 40 };
        layout.Widgets.Add(new Label
        {
            Text = loc["galaxy.title"],
            TextColor = UiPalette.TextDim,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var header = new HorizontalStackPanel { Spacing = 8 };
        if (_screens.Sprites.Get($"planet.{world.Id}") is { } icon)
        {
            header.Widgets.Add(UiFactory.Icon(icon, 24));
        }

        header.Widgets.Add(new Label { Text = loc[world.NameKey], TextColor = UiPalette.TextBright, VerticalAlignment = VerticalAlignment.Center });
        layout.Widgets.Add(header);
        layout.Widgets.Add(Note(loc[world.RuleKey], UiPalette.Accent));
        layout.Widgets.Add(Note(loc[world.DescriptionKey], UiPalette.Text));

        switch (availability)
        {
            case WorldAvailability.Colony:
                ColonyCard(layout, world);
                break;
            case WorldAvailability.Available:
                ShipCard(layout, world);
                break;
            default:
                layout.Widgets.Add(Note(
                    world.RequiresGate
                        ? loc["galaxy.lockedGate"]
                        : loc.Format("galaxy.locked", world.StarsRequired, _session.State.TotalStars()),
                    UiPalette.Warn));
                break;
        }

        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(Note(loc.Format("galaxy.totalStars", _session.State.TotalStars()), UiPalette.TextDim));
        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.VerticalAlignment = VerticalAlignment.Center;
        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private void ColonyCard(VerticalStackPanel layout, WorldDef world)
    {
        var loc = _screens.Loc;
        var record = _session.State.Records[world.Id];
        bool active = world.Id == _session.State.ActiveWorldId;
        double population = active ? _session.Active.Population : record.EstimatedPopulation(_session.NowSeconds);

        layout.Widgets.Add(Note(loc.Format("galaxy.population", Numbers.Format(population)), UiPalette.TextBright));
        layout.Widgets.Add(Note(loc.Format("galaxy.stars", Stars(record.Stars.Count)), UiPalette.Good));

        var content = _session.Contents.For(world.Id);
        if (content.World.ExportIndices.Count > 0)
        {
            string exports = string.Join(", ", content.World.ExportIndices.Select(r => loc[content.Resources[r].NameKey]));
            layout.Widgets.Add(Note(loc.Format("galaxy.exports", exports), UiPalette.Text));
        }

        if (_session.State.Records.Count > 1)
        {
            TradeSection(layout, world);
        }

        if (active)
        {
            layout.Widgets.Add(Note(loc["galaxy.youAreHere"], UiPalette.Accent));
            return;
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["galaxy.travel"], () => WorldTravel.Go(_screens, _session, world.Id)));
    }

    /// <summary>
    /// Obchod světa (svety-design.md 2.5): trasy, které tudy vedou, s tokem
    /// a stavem, a tlačítka pro nové trasy — vývoz světa k ostatním koloniím
    /// a jejich vývoz sem. Pravidla (co jde založit) drží <see cref="GalaxySession.CanOpenRoute"/>.
    /// </summary>
    private void TradeSection(VerticalStackPanel layout, WorldDef world)
    {
        var loc = _screens.Loc;
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(Note(loc["galaxy.trade.title"], UiPalette.TextBright));
        layout.Widgets.Add(Note(loc.Format("galaxy.trade.port",
            Numbers.Format(_session.TradeWorlds.PortCapacity(world.Id))), UiPalette.TextDim));

        var trade = _session.State.Trade;
        bool any = false;
        foreach (var route in trade.Routes.Where(r => r.FromWorldId == world.Id || r.ToWorldId == world.Id).ToList())
        {
            any = true;
            layout.Widgets.Add(Note(loc.Format("galaxy.trade.route",
                ResourceName(route.FromWorldId, route.ResourceId), WorldName(route.FromWorldId), WorldName(route.ToWorldId)),
                UiPalette.Text));
            layout.Widgets.Add(Note(loc.Format("galaxy.trade.rate",
                Numbers.Format(route.LastRate), Numbers.Format(_session.RouteCapacity(route)),
                Numbers.Format(trade.InTransitOn(route.Id)),
                Numbers.Format(_session.TravelSeconds(route.FromWorldId, route.ToWorldId))), UiPalette.TextDim));

            var status = new HorizontalStackPanel { Spacing = 8 };
            status.Widgets.Add(new Label
            {
                Text = loc[RouteStatusKey(route.Status)],
                TextColor = route.Status == TradeRouteStatus.Running ? UiPalette.Good : UiPalette.Warn,
                VerticalAlignment = VerticalAlignment.Center,
            });
            int id = route.Id;
            status.Widgets.Add(UiFactory.SmallButton(loc["galaxy.trade.close"], () =>
            {
                _session.CloseRoute(id);
                BuildUi();
            }));
            layout.Widgets.Add(status);
        }

        if (!any)
        {
            layout.Widgets.Add(Note(loc["galaxy.trade.none"], UiPalette.TextDim));
        }

        // Nové trasy: po dvou tlačítkách na řádek, ať karta nepřeteče.
        HorizontalStackPanel? row = null;
        foreach (var (from, to, resource) in PossibleRoutes(world.Id))
        {
            if (row is null || row.Widgets.Count >= 2)
            {
                row = new HorizontalStackPanel { Spacing = 6 };
                layout.Widgets.Add(row);
            }

            string f = from, t = to, r = resource;
            row.Widgets.Add(UiFactory.SmallButton(
                loc.Format("galaxy.trade.open", ResourceName(from, resource), WorldName(to)),
                () =>
                {
                    _session.OpenRoute(f, t, r);
                    BuildUi();
                }));
        }

        layout.Widgets.Add(Note(loc["galaxy.trade.hint"], UiPalette.TextDim));
    }

    /// <summary>Trasy, které jde se světem založit: jeho vývoz ven a vývoz ostatních k němu.</summary>
    private IEnumerable<(string From, string To, string Resource)> PossibleRoutes(string worldId)
    {
        foreach (string other in _session.State.Records.Keys.ToList())
        {
            if (other == worldId)
            {
                continue;
            }

            foreach (var (from, to) in new[] { (worldId, other), (other, worldId) })
            {
                var content = _session.Contents.For(from);
                foreach (int r in content.World.ExportIndices)
                {
                    string resource = content.Resources[r].Id;
                    if (_session.CanOpenRoute(from, to, resource) == TradeBlocker.None)
                    {
                        yield return (from, to, resource);
                    }
                }
            }
        }
    }

    private string WorldName(string worldId) =>
        _screens.Loc[_session.Catalog.Find(worldId)?.NameKey ?? worldId];

    private string ResourceName(string worldId, string resourceId)
    {
        var resources = _session.Contents.For(worldId).Resources;
        return resources.TryIndexOf(resourceId, out int r) ? _screens.Loc[resources[r].NameKey] : resourceId;
    }

    /// <summary>Lokalizační klíč stavu trasy.</summary>
    internal static string RouteStatusKey(TradeRouteStatus status) => status switch
    {
        TradeRouteStatus.WaitingForGoods => "galaxy.trade.status.waiting",
        TradeRouteStatus.DestinationFull => "galaxy.trade.status.full",
        TradeRouteStatus.NoPort => "galaxy.trade.status.noport",
        _ => "galaxy.trade.status.running",
    };

    private void ShipCard(VerticalStackPanel layout, WorldDef world)
    {
        var loc = _screens.Loc;
        var ship = _session.State.Ship;
        if (ship is not null && ship.TargetWorldId != world.Id)
        {
            string target = loc[_session.Catalog.Find(ship.TargetWorldId)?.NameKey ?? ship.TargetWorldId];
            layout.Widgets.Add(Note(loc.Format("galaxy.ship.elsewhere", target), UiPalette.Warn));
            return;
        }

        if (ship is null)
        {
            var blocker = _session.CanStartShip(world.Id);
            if (blocker == ShipBlocker.None)
            {
                layout.Widgets.Add(Note(loc.Format("galaxy.ship.stages", world.ColonyCost.Count), UiPalette.Text));
                layout.Widgets.Add(UiFactory.MenuButton(loc["galaxy.ship.start"], () =>
                {
                    _session.StartShip(world.Id);
                    BuildUi();
                }));
            }
            else
            {
                layout.Widgets.Add(Note(loc[BlockerKey(blocker)], UiPalette.Warn));
            }

            return;
        }

        if (_session.IsShipReady)
        {
            layout.Widgets.Add(Note(loc["galaxy.ship.ready"], UiPalette.Good));
            layout.Widgets.Add(UiFactory.MenuButton(loc["galaxy.ship.land"], () =>
                _screens.Push(new LandingScreen(_screens, _session))));
            return;
        }

        layout.Widgets.Add(Note(loc.Format("galaxy.ship.stage", ship.StageIndex + 1, world.ColonyCost.Count), UiPalette.TextBright));
        var home = _session.Contents.Home;
        foreach (var item in _session.ShipStageCost(world, ship.StageIndex))
        {
            double invested = _session.ShipInvested(item.ResourceIndex);
            layout.Widgets.Add(Note(
                $"{loc[home.Resources[item.ResourceIndex].NameKey]}  {Numbers.Format(invested)} / {Numbers.Format(item.Amount)}",
                invested >= item.Amount ? UiPalette.Good : UiPalette.Text));
        }

        if (_session.State.ActiveWorldId == WorldScope.HomeId)
        {
            layout.Widgets.Add(UiFactory.MenuButton(loc["galaxy.ship.invest"], () =>
            {
                _session.InvestInShip();
                BuildUi();
            }));
        }
        else
        {
            layout.Widgets.Add(Note(loc["galaxy.ship.notHome"], UiPalette.Warn));
        }
    }

    /// <summary>Hvězdy jako znaky — ★ za splněnou, ☆ za zbývající ze tří.</summary>
    internal static string Stars(int earned) =>
        new string('★', Math.Min(earned, 4)) + new string('☆', Math.Max(0, 3 - earned));

    internal static string BlockerKey(ShipBlocker blocker) => blocker switch
    {
        ShipBlocker.GalaxyClosed => "galaxy.lockedGate",
        ShipBlocker.NotHome => "galaxy.ship.notHome",
        ShipBlocker.NoSpaceport => "galaxy.ship.noSpaceport",
        ShipBlocker.ShipInProgress => "galaxy.ship.busy",
        ShipBlocker.AlreadyColony => "galaxy.colony",
        _ => "galaxy.lockedStars",
    };

    private Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 60,
    };
}
