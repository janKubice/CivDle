using CivDle.Core;
using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Muzeum světů (svety-design.md 2.6): galaktická sbírka hráče — za každý
/// svět hvězdy, viděná zvěř, rekord populace a divy.
///
/// <para>Proč to ve hře je: hvězdy odemykají světy, ale jakmile je svět
/// odemčený, hvězda zmizí v součtu. Muzeum z nich dělá sbírku, kterou jde
/// doplňovat i po letech (Nová hra+, archiv měst) — a zvěř, kterou hráč na
/// světě ještě neviděl, je důvod se tam znovu podívat.</para>
///
/// <para>Vrstva: UI. Čte jen profil (<see cref="PlayerProfile.Galaxy"/>)
/// a data světů; obsah kolonie se načte, až když ho hráč navštívil —
/// nenavštívený svět je jen silueta.</para>
/// </summary>
public sealed class MuseumScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public MuseumScreen(ScreenManager screens)
    {
        _screens = screens;
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
        var galaxy = _screens.Profile.Galaxy;
        var worlds = _screens.Galaxy.Catalog.Worlds;

        int stars = galaxy.Values.Sum(w => w.Stars.Count);
        int species = galaxy.Values.Sum(w => w.Fauna.Count);
        var list = new VerticalStackPanel { Spacing = 6 };
        list.Widgets.Add(Line(loc.Format("museum.summary", galaxy.Count, worlds.Count, stars, species), UiPalette.TextBright));

        foreach (var world in worlds)
        {
            list.Widgets.Add(new Panel { Height = 6 });
            list.Widgets.Add(Line(loc[world.NameKey], UiPalette.Accent));
            if (!galaxy.TryGetValue(world.Id, out var collection))
            {
                list.Widgets.Add(Line(loc["museum.unvisited"], UiPalette.TextDim));
                continue;
            }

            WorldCard(list, _screens.Galaxy.For(world.Id), collection);
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["museum.title"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 460, Width = 520 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>Jeden svět: hvězdy (splněné jasně, chybějící tlumeně), rekord, divy a zvěř.</summary>
    private void WorldCard(VerticalStackPanel list, GameContent content, WorldCollection collection)
    {
        var loc = _screens.Loc;
        foreach (var quest in content.Quests.All)
        {
            if (quest.Group is not (QuestGroup.Star or QuestGroup.StarMaster))
            {
                continue;
            }

            bool done = collection.Stars.Contains(quest.Id);
            list.Widgets.Add(Line(loc[quest.NameKey], done ? UiPalette.Text : UiPalette.TextFaint));
        }

        list.Widgets.Add(Line(loc.Format("museum.peak", Numbers.Format(collection.PeakPopulation)), UiPalette.Text));
        if (collection.Wonders > 0)
        {
            list.Widgets.Add(Line(loc.Format("museum.wonders", Numbers.Format(collection.Wonders)), UiPalette.Text));
        }

        // Zvěř: viděné druhy jménem, chybějící jen jako počet — sbírka má
        // lákat k návratu, ne prozradit, co přesně hledat.
        var fauna = content.Fauna;
        var seen = new List<string>();
        for (int i = 0; i < fauna.Count; i++)
        {
            if (collection.Fauna.Contains(fauna[i].Id))
            {
                seen.Add(loc[$"fauna.{fauna[i].Id}"]);
            }
        }

        list.Widgets.Add(Line(loc.Format("museum.fauna", seen.Count, fauna.Count), UiPalette.Text));
        if (seen.Count > 0)
        {
            list.Widgets.Add(Line(string.Join(", ", seen), UiPalette.TextDim));
        }
    }

    private static Label Line(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = 480,
    };
}
