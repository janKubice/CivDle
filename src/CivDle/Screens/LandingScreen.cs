using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Kam přistane kolonizační loď (svety-design.md 2.2): tři místa na cizí
/// planetě, každé s náhledem okolí a jmény krajin, ze kterých bude kolonie
/// žít — stejně jako výběr startu v úvodu hry.
///
/// <para>Náhled je skutečný terén světa (stejný seed jako kolonie), ne
/// ilustrace: co hráč vidí, na to přistane.</para>
/// </summary>
public sealed class LandingScreen : IScreen
{
    /// <summary>Kolik dlaždic kolem místa náhled ukáže (na stranu).</summary>
    private const int PreviewTiles = 96;

    private readonly ScreenManager _screens;
    private readonly GalaxySession _session;
    private readonly InputManager _input = new();
    private readonly List<Texture2D> _previews = new();
    private readonly IReadOnlyList<(int X, int Y)> _sites;
    private readonly GameContent _content;
    private readonly ITerrain _terrain;
    private Desktop _desktop = null!;

    public LandingScreen(ScreenManager screens, GalaxySession session)
    {
        _screens = screens;
        _session = session;
        var world = session.ShipTarget!;
        _content = session.Contents.For(world.Id);
        _terrain = WorldTerrain.Create(
            _content, _content.WorldGen.Presets[_content.World.PresetIndex], session.ColonySeed(world.Id));
        _sites = session.LandingSites(world.Id);
        foreach (var (x, y) in _sites)
        {
            _previews.Add(Preview(x, y));
        }

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    /// <summary>Nabídnutá místa (smoke test).</summary>
    internal IReadOnlyList<(int X, int Y)> Sites => _sites;

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
        var batch = _screens.SpriteBatch;
        var viewport = _screens.GraphicsDevice.Viewport;
        batch.Begin();
        batch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(6, 8, 18));
        batch.End();
        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        foreach (var preview in _previews)
        {
            preview.Dispose();
        }
    }

    /// <summary>Přistane na místě s daným pořadím (smoke test).</summary>
    internal void LandAt(int index) => WorldTravel.Land(_screens, _session, _sites[index].X, _sites[index].Y);

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var world = _session.ShipTarget!;
        var layout = new VerticalStackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("landing.title", loc[world.NameKey]),
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (_sites.Count == 0)
        {
            layout.Widgets.Add(new Label { Text = loc["landing.none"], TextColor = UiPalette.Bad });
        }

        var row = new HorizontalStackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center };
        for (int i = 0; i < _sites.Count; i++)
        {
            int index = i;
            var card = new VerticalStackPanel { Spacing = 6, Width = 200 };
            card.Widgets.Add(new Label { Text = loc.Format("landing.site", i + 1), TextColor = UiPalette.Accent });
            card.Widgets.Add(UiFactory.Icon(_previews[i], 192));
            card.Widgets.Add(new Label
            {
                Text = loc.Format("landing.terrain", string.Join(", ", Landscapes(_sites[i].X, _sites[i].Y))),
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = 196,
            });
            card.Widgets.Add(UiFactory.MenuButton(loc["landing.land"], () => LandAt(index)));
            row.Widgets.Add(UiFactory.DarkPanel(card));
        }

        layout.Widgets.Add(row);
        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.back"], _screens.Pop));
        var root = new Panel();
        layout.VerticalAlignment = VerticalAlignment.Center;
        root.Widgets.Add(layout);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>Náhled okolí: dlaždice po dlaždici v barvách biomů, modul uprostřed.</summary>
    private Texture2D Preview(int cx, int cy)
    {
        const int size = PreviewTiles;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var biome = _content.Biomes[_terrain.BiomeAt(cx - size / 2 + x, cy - size / 2 + y)];
                pixels[y * size + x] = new Color(biome.MapColor.R, biome.MapColor.G, biome.MapColor.B);
            }
        }

        var module = _content.Buildings[_content.World.LandingModuleIndex];
        for (int y = 0; y < module.FootprintHeight + 2; y++)
        {
            for (int x = 0; x < module.FootprintWidth + 2; x++)
            {
                int px = size / 2 - 1 + x;
                int py = size / 2 - 1 + y;
                bool edge = x == 0 || y == 0 || x == module.FootprintWidth + 1 || y == module.FootprintHeight + 1;
                pixels[py * size + px] = edge ? Color.White : new Color(230, 90, 60);
            }
        }

        var texture = new Texture2D(_screens.GraphicsDevice, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Tři nejčastější krajiny v okolí místa — z čeho bude kolonie žít.</summary>
    private IEnumerable<string> Landscapes(int cx, int cy)
    {
        var counts = new Dictionary<int, int>();
        for (int y = -24; y <= 24; y += 3)
        {
            for (int x = -24; x <= 24; x += 3)
            {
                int biome = _terrain.BiomeAt(cx + x, cy + y);
                counts[biome] = counts.GetValueOrDefault(biome) + 1;
            }
        }

        return counts.OrderByDescending(c => c.Value).Take(3).Select(c => _screens.Loc[_content.Biomes[c.Key].NameKey]);
    }
}
