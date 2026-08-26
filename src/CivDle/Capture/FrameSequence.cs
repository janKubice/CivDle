using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Capture;

/// <summary>
/// Sekvence snímků na disku — <c>frame-000123.png</c> ve složce.
///
/// <para>Proč sekvence a ne rovnou video: zakódovat H.264 znamená přibalit
/// kodek, tedy další závislost proti pravidlu „no balast". Sekvence je navíc
/// bezztrátová, takže si autor sám zvolí kodek i kvalitu — příkaz pro ffmpeg
/// vypíše <see cref="VideoTiming.FfmpegCommand"/> hotový.</para>
///
/// <para>Šestimístné číslo schválně: ffmpeg čte sekvenci podle vzoru a při
/// kratším čísle by se po tisícovce snímků rozsypalo pořadí.</para>
///
/// <para>Ukládá se přes pomocnou texturu, ne přímo z render targetu: ten je
/// pořád svázaný se zařízením a <c>SaveAsPng</c> z něj během kreslení zlobí.
/// Textura je <b>jedna na celou sekvenci</b>, ne nová na každý snímek — ve
/// Full HD má osm megabajtů a ovladač ji nemusí uvolnit hned, takže se při
/// dvoutisícovce snímků nasčítaly gigabajty a natáčení tiše skončilo v půlce
/// posledního záběru.</para>
/// </summary>
public sealed class FrameSequence : IDisposable
{
    private readonly Color[] _buffer;
    private readonly Texture2D _staging;
    private bool _disposed;

    public FrameSequence(GraphicsDevice device, string directory, int width, int height)
    {
        _buffer = new Color[width * height];
        _staging = new Texture2D(device, width, height);
        Directory = directory;
        Width = width;
        Height = height;
        System.IO.Directory.CreateDirectory(directory);
    }

    /// <summary>Kam se snímky ukládají.</summary>
    public string Directory { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Kolik snímků už na disku je.</summary>
    public int Count { get; private set; }

    /// <summary>Příkaz, kterým se ze sekvence udělá video.</summary>
    public string FfmpegCommand => VideoTiming.FfmpegCommand(Directory);

    /// <summary>Uloží obsah render targetu jako další snímek v pořadí.</summary>
    public void Save(RenderTarget2D target)
    {
        SaveAs(target, Count);
        Count++;
    }

    /// <summary>Uloží snímek s konkrétním pořadovým číslem.</summary>
    public void SaveAs(RenderTarget2D target, int frameIndex)
    {
        target.GetData(_buffer);
        _staging.SetData(_buffer);

        string path = Path.Combine(Directory, $"frame-{frameIndex:D6}.png");
        using var stream = File.Create(path);
        _staging.SaveAsPng(stream, Width, Height);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _staging.Dispose();
        }
    }
}
