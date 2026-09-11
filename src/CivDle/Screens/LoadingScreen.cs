using System.Diagnostics;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
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

    /// <summary>
    /// Kolik milisekund snímku smí ukrojit dohánění offline času. Zbytek snímku
    /// patří překreslení a vstupu — právě jejich absence dělala z počítání
    /// „zamrzlou hru", kterou Windows po kliknutí odstřelily.
    /// </summary>
    private const double CatchUpMillisPerFrame = 6.0;

    /// <summary>Kolik tiků se zkusí naráz, než se znovu kouknem na hodiny.</summary>
    private const int CatchUpChunk = 250;

    /// <summary>
    /// Jak rychle běží přehrávka kroniky za panelem. Rychleji než v přehrávači
    /// v menu — tady je to kulisa, kterou hráč kouká pár sekund, ne film,
    /// u kterého sedí.
    /// </summary>
    private const float PlaybackFramesPerSecond = 18f;

    private readonly ScreenManager _screens;
    private readonly Func<OfflineSummary?, IScreen> _next;
    private readonly Desktop _desktop;
    private readonly ProgressBar _bar;
    private readonly Label _status;
    private readonly OfflineCatchUp? _catchUp;
    private readonly Stopwatch _frameClock = new();

    /// <summary>Přehrávka kroniky za načítacím panelem; <c>null</c> u nové hry.</summary>
    private readonly HistoryPlayback? _playback;
    private readonly CityHistory? _history;
    private readonly ITerrain? _terrain;
    private readonly Camera2D _camera = new();

    private double _elapsed;
    private bool _handedOver;
    private float _frame;

    /// <param name="titleKey">Lokalizační klíč hlášky („Tvořím svět", „Načítám hru").</param>
    /// <param name="next">
    /// Co po načtení. Volá se <b>až</b> po uplynutí minima — a jen jednou.
    /// </param>
    /// <param name="catchUp">
    /// Dohánění offline času, když se načítá rozehraná hra; <c>null</c> u nové.
    /// Obrazovka ho posouvá po dávkách, aby zůstala živá.
    /// </param>
    /// <param name="playbackOf">
    /// Rozehraná hra, jejíž kronika se má za panelem přehrát. <c>null</c>
    /// u nové hry — tam ještě žádná historie není.
    /// </param>
    public LoadingScreen(
        ScreenManager screens, string titleKey, Func<OfflineSummary?, IScreen> next,
        OfflineCatchUp? catchUp = null, Simulation? playbackOf = null)
    {
        _screens = screens;
        _next = next;
        _catchUp = catchUp is { TotalTicks: > 0 } ? catchUp : null;

        // Místo prázdné černé obrazovky se přehraje, jak město rostlo. Dohánění
        // offline času umí trvat pár sekund a tohle je přesně ta chvíle, kdy má
        // hráč vidět, co se mezitím stalo — ne ukazatel postupu v prázdnu.
        if (playbackOf is { HistoryEnabled: true } sim && sim.History.Count > 1)
        {
            _history = sim.History;
            _terrain = sim.Terrain;
            _playback = new HistoryPlayback(
                screens.GraphicsDevice,
                screens.Content.Biomes,
                sim.Seed,
                screens.WhitePixel,
                screens.Content.Gameplay.Roads.MapColor.ToXna());

            var viewport = screens.GraphicsDevice.Viewport;
            HistoryPlayback.FrameCity(_camera, _history, viewport.Width, viewport.Height);
        }

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

        // Řádek pod ukazatelem říká, CO se počítá. Bez něj hráč u delšího
        // dohonu neví, jestli se něco děje, nebo hra chcípla.
        _status = new Label
        {
            Text = _catchUp is null ? string.Empty : loc["loading.catchingUp"],
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        layout.Widgets.Add(_status);

        if (_catchUp is not null)
        {
            layout.Widgets.Add(UiFactory.SmallButton(loc["loading.skipCatchUp"], () => _catchUp.Skip()));
        }

        var root = new Panel();
        root.Widgets.Add(layout);
        _desktop = screens.NewDesktop(root);
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
        _elapsed += gameTime.ElapsedGameTime.TotalSeconds;

        // Přehrávka běží pořád dokola: načítání může skončit dřív, než se
        // kronika dohraje, a useknutý film je horší než žádný.
        if (_history is not null)
        {
            _frame += (float)(gameTime.ElapsedGameTime.TotalSeconds * PlaybackFramesPerSecond);
            if (_frame >= _history.Count)
            {
                _frame = 0f;
            }
        }

        // Dohon po dávkách: ukroji se kousek snímku, zbytek patří obrazovce.
        if (_catchUp is { IsDone: false })
        {
            _frameClock.Restart();
            while (!_catchUp.IsDone && _frameClock.Elapsed.TotalMilliseconds < CatchUpMillisPerFrame)
            {
                _catchUp.Advance(CatchUpChunk);
            }

            _status.Text = _screens.Loc.Format(
                "loading.catchingUpPercent", (int)Math.Round(_catchUp.Progress * 100));
        }

        // Ukazatel bere to pomalejší z obojího — jinak by doběhl na konec
        // a pak by hra „bez důvodu" stála dál.
        double progress = _catchUp is null
            ? _elapsed / MinimumSeconds
            : Math.Min(_elapsed / MinimumSeconds, _catchUp.Progress);
        _bar.SetProgress(progress);

        if (_handedOver || _elapsed < MinimumSeconds || _catchUp is { IsDone: false })
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
            next = _next(_catchUp?.Finish());
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

        if (_playback is not null && _history is not null && _terrain is not null)
        {
            _playback.Draw(_screens.SpriteBatch, _camera, _terrain, _history, (int)_frame);

            // Ztmavení pod panelem: text načítání musí zůstat čitelný i nad
            // světlým terénem.
            var viewport = _screens.GraphicsDevice.Viewport;
            _screens.SpriteBatch.Begin();
            _screens.SpriteBatch.Draw(
                _screens.WhitePixel,
                new Rectangle(0, 0, viewport.Width, viewport.Height),
                Color.Black * 0.45f);
            _screens.SpriteBatch.End();
        }

        _desktop.Render();
    }

    public void Dispose() => _playback?.Dispose();
}
