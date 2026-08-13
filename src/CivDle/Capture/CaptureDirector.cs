using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Režim „nafoť obchod": hra se místo menu spustí, sama si vypěstuje město,
/// postaví kameru a uloží sadu snímků do složky. Pak skončí.
///
/// <para>Proč to je v samotné hře a ne ve zvláštním nástroji: snímky musí projít
/// <b>skutečným</b> renderem — stejnými sprity, stejným HUD, stejnými efekty,
/// jaké uvidí hráč. Cokoli nakresleného mimo hru by byl obrázek hry, ne hra.</para>
///
/// <para>Spouští se přes <c>--capture &lt;složka&gt;</c>; běžný start hry se tím
/// nemění.</para>
/// </summary>
public sealed class CaptureDirector
{
    /// <summary>Kolik snímků se nechá proběhnout, než se ten ostrý uloží.</summary>
    private const int WarmupFrames = 12;

    private readonly string _outputDirectory;
    private readonly List<StoreShot> _shots;

    /// <summary>
    /// Focení běží bez LOD. Na obrazovce je odbourávání detailu správně —
    /// z výšky jsou stromy pod rozlišením a platit za ně plnou cenu nemá smysl.
    /// Na obrázku, který visí v obchodě, ale nikdo nikam nespěchá a chybějící
    /// detail je jediná věc, které si zákazník všimne.
    /// </summary>
    private readonly IDisposable _fullDetail;

    private int _shotIndex;
    private int _frame;
    private Color[]? _buffer;

    public CaptureDirector(string outputDirectory)
    {
        _outputDirectory = outputDirectory;
        _shots = DefaultShots();
        _fullDetail = Rendering.DetailLevel.FullDetail();
    }

    /// <summary>Jsou všechny snímky hotové (a hra může skončit)?</summary>
    public bool Finished => _shotIndex >= _shots.Count;

    /// <summary>
    /// Sada záběrů do obchodu. Každý ukazuje něco jiného — deset variant téhož
    /// města je horší než pět, na kterých je pokaždé vidět jiná mechanika.
    /// </summary>
    private static List<StoreShot> DefaultShots() => new()
    {
        new StoreShot("01-city", ShotSubject.City, Minutes: 14, Zoom: 3.6f, Seed: 20260728),
        new StoreShot("02-night", ShotSubject.Night, Minutes: 14, Zoom: 3.6f, Seed: 20260728),
        new StoreShot("03-coast", ShotSubject.Coast, Minutes: 14, Zoom: 3.2f, Seed: 20260728),
        new StoreShot("04-golden-hour", ShotSubject.GoldenHour, Minutes: 14, Zoom: 3.8f, Seed: 20260728),
        new StoreShot("05-winter", ShotSubject.Winter, Minutes: 12, Zoom: 3.4f, Seed: 4711),
        new StoreShot("06-scale", ShotSubject.Scale, Minutes: 16, Zoom: 0.6f, Seed: 20260728),
        new StoreShot("07-night-scale", ShotSubject.NightScale, Minutes: 16, Zoom: 0.35f, Seed: 20260728),
        new StoreShot("08-tech-tree", ShotSubject.Tech, Minutes: 10, Zoom: 3.0f, Seed: 991),
        new StoreShot("09-achievements", ShotSubject.Achievements, Minutes: 12, Zoom: 3.2f, Seed: 20260728),
    };

    /// <summary>Připraví scénu dalšího snímku a vrátí obrazovku, kterou má hra ukázat.</summary>
    public IScreen PrepareNextShot(ScreenManager screens)
    {
        var shot = _shots[_shotIndex];
        var sim = CityFixture.Grow(screens.Content, shot.Seed, shot.Minutes);

        var focus = new Vector2(
            (sim.CityCenterX + 0.5f) * Rendering.TerrainRenderer.TileSize,
            (sim.CityCenterY + 0.5f) * Rendering.TerrainRenderer.TileSize);

        // Denní záběry chtějí polední světlo — ve hře se stmívá a scéna přes
        // noční overlay ztmavne tak, že z města není nic vidět.
        if (shot.Subject is ShotSubject.City or ShotSubject.Winter or ShotSubject.Scale)
        {
            CityFixture.TickUntilPostcardMoment(sim, screens.Content, from: 0.40, to: 0.58);
        }

        switch (shot.Subject)
        {
            case ShotSubject.Night:
            case ShotSubject.NightScale:
                CityFixture.TickUntilTimeOfDay(sim, from: 0.90, to: 0.98);
                break;

            case ShotSubject.Winter:
                CityFixture.TickUntilSeason(sim, "winter");
                CityFixture.TickUntilPostcardMoment(sim, screens.Content, from: 0.40, to: 0.58);
                break;

            case ShotSubject.Coast:
                // Podvečer, ne poledne: nízké světlo dá vodě odlesk a budovám
                // dlouhé stíny. V poledne je z pobřeží jen modrá plocha.
                CityFixture.TickUntilTimeOfDay(sim, from: 0.72, to: 0.78);
                if (CityFixture.TryFindShore(sim, out int shoreX, out int shoreY))
                {
                    focus = new Vector2(
                        (shoreX + 0.5f) * Rendering.TerrainRenderer.TileSize,
                        (shoreY + 0.5f) * Rendering.TerrainRenderer.TileSize);
                }

                break;

            case ShotSubject.GoldenHour:
                CityFixture.TickUntilTimeOfDay(sim, from: 0.76, to: 0.82);
                break;
        }

        var gameplay = new GameplayScreen(screens, sim, new WorldInfo(shot.Seed, "medium", "continents"));
        gameplay.FocusForCapture(focus, shot.Zoom);
        _frame = 0;

        return shot.Subject switch
        {
            ShotSubject.Tech => new TechScreen(screens, sim),
            ShotSubject.Achievements => new AchievementsScreen(screens, sim),
            _ => gameplay,
        };
    }

    /// <summary>
    /// Zavolá se po každém vykreslení. Pár snímků se nechá proběhnout naprázdno
    /// (Myra si dopočítá rozvržení, efekty se rozběhnou), teprve pak se ukládá.
    /// </summary>
    /// <returns>true, když byl snímek uložen a je čas připravit další.</returns>
    public bool AfterDraw(GraphicsDevice device)
    {
        if (Finished || ++_frame < WarmupFrames)
        {
            return false;
        }

        Save(device, _shots[_shotIndex].FileName);
        _shotIndex++;
        if (Finished)
        {
            _fullDetail.Dispose();
        }

        return true;
    }

    /// <summary>Uloží obsah okna jako PNG ve skutečném rozlišení backbufferu.</summary>
    private void Save(GraphicsDevice device, string fileName)
    {
        Directory.CreateDirectory(_outputDirectory);

        int width = device.PresentationParameters.BackBufferWidth;
        int height = device.PresentationParameters.BackBufferHeight;
        int pixels = width * height;
        if (_buffer is null || _buffer.Length < pixels)
        {
            _buffer = new Color[pixels];
        }

        device.GetBackBufferData(_buffer, 0, pixels);

        using var texture = new Texture2D(device, width, height);
        texture.SetData(_buffer, 0, pixels);

        string path = Path.Combine(_outputDirectory, fileName + ".png");
        using var stream = File.Create(path);
        texture.SaveAsPng(stream, width, height);
        Console.WriteLine($"snímek: {path} ({width}×{height})");
    }
}
