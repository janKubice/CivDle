using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Stránka „jak se to hraje": šest vět o tom, co je tohle za hru a kam vede.
///
/// <para>Existuje kvůli zpětné vazbě „tváří se to jako city builder, ale je to
/// idle, a já nevím, co mám chtít". Průvodce v HUD vede krok za krokem, tohle
/// dává tu druhou půlku — <em>proč</em> ty kroky děláš a co je konec cesty.
/// Záměrně je to jedna krátká obrazovka, ne manuál.</para>
///
/// <para>Otevírá se z hlavního menu i z pauzy — hráč ji potřebuje před první
/// hrou i uprostřed, když se ztratí.</para>
/// </summary>
public sealed class HowToPlayScreen : IScreen
{
    /// <summary>Kolik číslovaných bodů má text (klíče <c>howto.1</c>…).</summary>
    private const int StepCount = 6;

    private readonly ScreenManager _screens;
    private readonly bool _dimBackground;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <param name="dimBackground">
    /// Ztmavit scénu pod sebou? V pauze ano (překrývá běžící hru), z hlavního
    /// menu ne — tam už podklad tvoří menu samo.
    /// </param>
    public HowToPlayScreen(ScreenManager screens, bool dimBackground = true)
    {
        _screens = screens;
        _dimBackground = dimBackground;
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

    public void Draw(GameTime gameTime)
    {
        if (_dimBackground)
        {
            var viewport = _screens.GraphicsDevice.Viewport;
            var spriteBatch = _screens.SpriteBatch;
            spriteBatch.Begin();
            spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
            spriteBatch.End();
        }

        _desktop.Render();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
    }

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
            Text = loc["howto.title"],
            TextColor = UiFactory.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = loc["howto.intro"], TextColor = new Color(200, 210, 224) });
        layout.Widgets.Add(new Label { Text = " " });

        for (int i = 1; i <= StepCount; i++)
        {
            layout.Widgets.Add(new Label { Text = loc[$"howto.{i}"] });
        }

        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["settings.back"], _screens.Pop));

        _desktop = new Desktop { Root = UiFactory.MenuBackdrop(layout) };
    }
}
