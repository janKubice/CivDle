using CivDle.Core.Sim;
using CivDle.Rendering;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Vyrenderuje natočený záběr do sekvence PNG — mimo reálný čas, v plném
/// rozlišení a bez LOD.
///
/// <para>Proč sekvence a ne rovnou video: zakódovat H.264 znamená přibalit
/// kodek, tedy další závislost proti pravidlu „no balast". Sekvence je navíc
/// bezztrátová, takže si autor sám zvolí kodek i kvalitu — příkaz pro ffmpeg
/// hra vypíše hotový (<see cref="VideoTiming.FfmpegCommand"/>).</para>
///
/// <para>Renderuje se <b>po snímcích</b>, ne v jedné smyčce: jeden 4K snímek
/// bez LOD může trvat půl vteřiny a tisícovka takových by okno na deset minut
/// zmrazila — přesně ta chyba, kvůli které padalo Continue. Volající si říká,
/// kolik snímků stihne, a mezi nimi kreslí ukazatel postupu.</para>
///
/// <para>Simulace je <b>vlastní kopie</b>. Kdyby se renderovalo nad hráčovou
/// hrou, posunul by mu render svět o délku videa dopředu — natočit si záběr by
/// znamenalo změnit si rozehranou partii.</para>
/// </summary>
public sealed class VideoRender : IDisposable
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly CameraTake _take;
    private readonly ShareCardOptions _options;
    private readonly WorldScene _scene;
    private readonly Camera2D _camera = new();
    private readonly IDisposable _fullDetail;
    private readonly RenderTarget2D _target;
    private readonly Color[] _buffer;

    private bool _disposed;

    /// <param name="simulation">Kopie světa, do které se smí tikat.</param>
    public VideoRender(
        ScreenManager screens,
        Simulation simulation,
        CameraTake take,
        ShareCardOptions options,
        string directory)
    {
        _screens = screens;
        _simulation = simulation;
        _take = take;
        _options = options;
        Directory = directory;

        TotalFrames = VideoTiming.FrameCount(take.Duration);
        _scene = new WorldScene(screens, screens.Content, simulation.Seed);
        _fullDetail = DetailLevel.FullDetail();

        _target = new RenderTarget2D(screens.GraphicsDevice, options.Width, options.Height);
        _buffer = new Color[options.Width * options.Height];

        _camera.SetViewport(options.Width, options.Height);
        System.IO.Directory.CreateDirectory(directory);
    }

    /// <summary>Kam se snímky ukládají.</summary>
    public string Directory { get; }

    /// <summary>Kolik snímků video celkem má.</summary>
    public int TotalFrames { get; }

    /// <summary>Kolik jich je hotových.</summary>
    public int DoneFrames { get; private set; }

    /// <summary>Přerušil to autor?</summary>
    public bool WasCancelled { get; private set; }

    /// <summary>Postup 0–1 pro ukazatel.</summary>
    public double Progress => TotalFrames <= 0 ? 1.0 : Math.Clamp(DoneFrames / (double)TotalFrames, 0, 1);

    /// <summary>Hotovo (dorenderováno, nebo přerušeno)?</summary>
    public bool IsDone => WasCancelled || DoneFrames >= TotalFrames;

    /// <summary>Příkaz, kterým se ze sekvence udělá video.</summary>
    public string FfmpegCommand => VideoTiming.FfmpegCommand(Directory);

    /// <summary>Autor nechce čekat. Co je vyrenderované, zůstane na disku.</summary>
    public void Cancel() => WasCancelled = true;

    /// <summary>
    /// Vyrenderuje a uloží jeden snímek. Vrací false, když už není co dělat.
    /// </summary>
    public bool RenderNextFrame()
    {
        if (IsDone)
        {
            return false;
        }

        // Simulace se posouvá pevným krokem odvozeným z pořadí snímku, ne
        // z uplynulého času — jinak by video běželo podle výkonu počítače.
        int ticks = VideoTiming.TicksBeforeFrame(DoneFrames);
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        _scene.Update(1f / VideoTiming.Fps, _simulation);

        var key = _take.Sample(VideoTiming.TimeOfFrame(DoneFrames));
        _camera.Position = key.Position;
        _camera.SetCaptureZoom(_options.ZoomFor(key.Zoom, _screens.GraphicsDevice.Viewport.Height));

        var device = _screens.GraphicsDevice;
        device.SetRenderTarget(_target);
        device.Clear(new Color(16, 22, 28));
        _scene.Draw(_camera, _simulation, new Viewport(0, 0, _options.Width, _options.Height));
        device.SetRenderTarget(null);

        Save(DoneFrames);
        DoneFrames++;
        return true;
    }

    /// <summary>
    /// Uloží snímek jako <c>frame-000123.png</c>. Šestimístné číslo schválně:
    /// ffmpeg čte sekvenci podle vzoru a při kratším čísle by se po tisícovce
    /// snímků rozsypalo pořadí.
    /// </summary>
    private void Save(int frameIndex)
    {
        _target.GetData(_buffer);

        string path = Path.Combine(Directory, $"frame-{frameIndex:D6}.png");
        using var stream = File.Create(path);

        // Ukládá se přes pomocnou texturu, ne přímo z render targetu: ten je
        // pořád svázaný se zařízením a SaveAsPng z něj během kreslení zlobí.
        using var texture = new Texture2D(_screens.GraphicsDevice, _options.Width, _options.Height);
        texture.SetData(_buffer);
        texture.SaveAsPng(stream, _options.Width, _options.Height);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _target.Dispose();
        _scene.Dispose();
        _fullDetail.Dispose();
    }
}
