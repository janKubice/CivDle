using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Načítací obrazovka mezi menu a hrou.
///
/// <para>Vytvoření světa i načtení savu trvá zlomek sekundy, takže technicky
/// potřeba není. Potřeba je ale <b>psychologicky</b>: přechod z menu rovnou do
/// rozehrané mapy působí jako lag nebo chyba — hráč nestihne přepnout
/// pozornost. Krátká obrazovka s hláškou dá skoku rytmus a řekne, co se právě
/// děje.</para>
///
/// <para>Vlastní práci si obrazovka nedělá: dostane hotovou <b>továrnu</b> na
/// další obrazovku a zavolá ji, až uplyne <see cref="MinimumSeconds"/>. Kdyby
/// svět stavěla sama, musela by znát všechno, z čeho se skládá — a to není její
/// zodpovědnost.</para>
/// </summary>
public sealed class LoadingScreen : IScreen
{
    /// <summary>
    /// Jak dlouho obrazovka vydrží, i když je hotovo dřív. Pod sekundu by to
    /// bylo bliknutí, které si hráč přečíst nestihne.
    /// </summary>
    public const double MinimumSeconds = 1.0;

    private readonly ScreenManager _screens;
    private readonly Func<IScreen> _next;
    private readonly Desktop _desktop;
    private readonly ProgressBar _bar;

    private double _elapsed;
    private bool _handedOver;

    /// <param name="titleKey">Lokalizační klíč hlášky („Tvořím svět", „Načítám hru").</param>
    /// <param name="next">
    /// Co po načtení. Volá se <b>až</b> po uplynutí minima — a jen jednou.
    /// </param>
    public LoadingScreen(ScreenManager screens, string titleKey, Func<IScreen> next)
    {
        _screens = screens;
        _next = next;

        var loc = screens.Loc;
        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc[titleKey],
            TextColor = UiFactory.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Ukazatel postupu, ne tři tečky. Tečky se od zamrznutí nedají
        // rozeznat — hráč u nich neví, jestli se něco děje, nebo hra chcípla.
        _bar = new ProgressBar(320, height: 10);
        layout.Widgets.Add(_bar.Root);

        var root = new Panel();
        root.Widgets.Add(layout);
        _desktop = screens.NewDesktop(root);
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime.TotalSeconds;

        _bar.SetProgress(_elapsed / MinimumSeconds);

        if (_handedOver || _elapsed < MinimumSeconds)
        {
            return;
        }

        _handedOver = true;

        // Stavba další obrazovky je jediné místo, kde tahle třída volá cizí kód
        // — a to je přesně místo, kde se dřív hra rozsypala. Když se svět nebo
        // rozehraná hra postavit nedá, hráč se má vrátit do menu s hláškou,
        // ne přijít o celý proces.
        IScreen next;
        try
        {
            next = _next();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Načtení selhalo: {ex}");
            _screens.ReplaceAll(new MainMenuScreen(_screens, _screens.Loc["menu.loadFailed"]));
            return;
        }

        _screens.ReplaceAll(next);
    }

    public void Draw(GameTime gameTime)
    {
        _screens.GraphicsDevice.Clear(new Color(12, 16, 24));
        _desktop.Render();
    }

    public void Dispose()
    {
    }
}
