using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>Rozlišení a délka natáčení. Náhled je totéž o půlce, ne něco jiného.</summary>
/// <param name="Width">Šířka snímku v pixelech.</param>
/// <param name="Height">Výška snímku v pixelech.</param>
/// <param name="Cities">Kolik městeček se v přehlídce vystřídá.</param>
/// <param name="FrameStep">
/// Ukládá se každý N-tý snímek. Náhled si vystačí s každým druhým (30 fps místo
/// 60) a je za desetinu času — na posouzení kompozice to stačí.
/// </param>
internal readonly record struct TrailerPreset(int Width, int Height, int Cities, int FrameStep)
{
    /// <summary>Ostrá verze do editoru videa: 1080p, 60 fps.</summary>
    public static TrailerPreset Full => new(1920, 1080, 4, 1);

    /// <summary>Náhled: půlka rozlišení, půlka snímků, dvě města.</summary>
    public static TrailerPreset Preview => new(960, 540, 2, 2);
}

/// <summary>
/// Natočí záběry do traileru a uloží je jako sekvence PNG.
///
/// <para>Dva záběry, protože zadání mělo dvě části: <b>přehlídka spritů</b>
/// s tím, kolik toho hra má, a <b>přehlídka městeček</b> s titulkem. Každý jde
/// do vlastní složky, aby se v editoru daly stříhat zvlášť.</para>
///
/// <para>Renderuje se mimo reálný čas, snímek po snímku. Hotové video hra
/// nevyrábí schválně: kodek by byl další závislost proti pravidlu „no balast",
/// a takhle si autor sám zvolí kvalitu — příkaz pro ffmpeg vypíšeme hotový.</para>
///
/// <para>Vrstva: nástroj nad rendererem. Do rozehrané hry nesahá; každé městečko
/// je vlastní svět, který po natočení zahodíme.</para>
/// </summary>
internal sealed class TrailerDirector
{
    /// <summary>
    /// Titulek přes první městečko. Anglicky, protože trailer je pro obchod —
    /// je to text nástroje, ne herní obsah, takže do lokalizace nepatří.
    /// </summary>
    private const string CityCaption = "Design your dream city";

    /// <summary>
    /// Semínka městeček. Pevná, aby šel týž záběr natočit znovu — a vybraná,
    /// ne náhodná: každé z nich staví na pláni, kde se z plánu postaví přes
    /// devadesát procent parcel. Na horším semínku zůstanou v ulicích díry.
    /// </summary>
    private static readonly long[] CitySeeds = { 20260816, 30313, 777001, 4242 };

    private readonly string _outputDirectory;
    private readonly TrailerPreset _preset;

    public TrailerDirector(string outputDirectory, TrailerPreset preset)
    {
        _outputDirectory = outputDirectory;
        _preset = preset;
    }

    /// <summary>Natočí a uloží všechny záběry. Volá se jednou, pak hra končí.</summary>
    public void RenderAll(ScreenManager screens)
    {
        var device = screens.GraphicsDevice;
        var canvas = new TrailerCanvas(screens.SpriteBatch, screens.WhitePixel, _preset.Width, _preset.Height);

        Directory.CreateDirectory(_outputDirectory);
        Console.WriteLine($"trailer: {_preset.Width}×{_preset.Height}, {VideoTiming.Fps / _preset.FrameStep} fps");

        foreach (var shot in BuildShots(canvas, screens))
        {
            using (shot)
            {
                Render(shot, device, canvas);
            }
        }

        Console.WriteLine();
        Console.WriteLine("Sekvence jsou hotové. Video z nich uděláš takhle:");
        Console.WriteLine($"  {VideoTiming.FfmpegCommand(Path.Combine(_outputDirectory, "01-prehlidka"))}");
    }

    /// <summary>
    /// Záběry v pořadí, ve kterém se natáčejí.
    ///
    /// <para>Vyrábějí se líně (<c>yield</c>) schválně: každé městečko si drží
    /// vlastní simulaci i vlastní renderery, a kdyby vznikla všechna naráz,
    /// držela by se v paměti čtyři města, ze kterých se zrovna kreslí jedno.</para>
    /// </summary>
    private IEnumerable<ITrailerShot> BuildShots(TrailerCanvas canvas, ScreenManager screens)
    {
        yield return new SpriteParadeShot(canvas, screens.Sprites, screens.Content);

        int cities = Math.Min(_preset.Cities, CitySeeds.Length);
        for (int i = 0; i < cities; i++)
        {
            // Titulek nese jen první město; na dalších by se opakoval a přebíjel
            // to, na co se má divák dívat.
            yield return new CityShowcaseShot(
                canvas, screens, i + 2, CitySeeds[i], i == 0 ? CityCaption : null);
        }
    }

    /// <summary>Vyrenderuje jeden záběr do vlastní složky.</summary>
    private void Render(ITrailerShot shot, GraphicsDevice device, TrailerCanvas canvas)
    {
        string directory = Path.Combine(_outputDirectory, shot.Name);
        var frames = new FrameSequence(device, directory, _preset.Width, _preset.Height);

        using var target = new RenderTarget2D(device, _preset.Width, _preset.Height);
        for (int frame = 0; frame < shot.FrameCount; frame++)
        {
            device.SetRenderTarget(target);
            device.Clear(new Color(10, 13, 18));
            shot.DrawFrame(frame);
            device.SetRenderTarget(null);

            // Simulace i animace musí projít KAŽDÝM snímkem, i když se neukládá —
            // jinak by náhled běžel dvakrát rychleji než ostrá verze a nedal by
            // se podle něj posoudit tempo.
            if (frame % _preset.FrameStep == 0)
            {
                frames.Save(target);
            }
        }

        Console.WriteLine($"záběr: {directory} ({frames.Count} snímků)");
    }
}
