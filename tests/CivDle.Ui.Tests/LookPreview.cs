using System.IO.Compression;
using CivDle.Core.Content;
using CivDle.Rendering.Sprites;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Náhled vzhledů z dat do PNG — nástroj pro autora, ne kontrola. Běží jen,
/// když je nastavená proměnná <c>LOOK_PREVIEW_DIR</c>; jinak se hned vrátí.
/// Kreslí na <see cref="PixelCanvas"/> bez grafické karty, takže jde spustit
/// i na serveru a obrázek si prohlédnout.
/// </summary>
public sealed class LookPreview
{
    [Fact]
    public void WritePreviewSheets()
    {
        string? directory = Environment.GetEnvironmentVariable("LOOK_PREVIEW_DIR");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var looks = content.Buildings.All.Where(b => b.Look is not null).ToList();

        // Každá budova v „nativní" velikosti, zvětšená ×3, na neutrálním podkladu.
        const int Cell = 96 * 3 + 8;
        int columns = 6;
        int rows = Math.Max(1, (looks.Count + columns - 1) / columns);
        var sheet = new Rgba[columns * Cell * rows * Cell * 2];
        int sheetW = columns * Cell, sheetH = rows * Cell * 2;
        Array.Fill(sheet, new Rgba(92, 120, 84, 255));
        for (int i = 0; i < looks.Count; i++)
        {
            var def = looks[i];
            var (w, h) = SpriteLibrary.LookCanvasSize(def);
            var canvas = new PixelCanvas(w, h);
            LookPainter.Paint(canvas, def.Look!);
            canvas.Outline(SpriteLibrary.OutlineStrength); // jako ve hře (SpriteLibrary.AddTall)
            canvas.SnapToPalette();
            int scale = Math.Max(1, (Cell - 8) / Math.Max(w, h / 2));
            int ox = (i % columns) * Cell + 4, oy = (i / columns) * Cell * 2 + 4;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = canvas.At(x, y);
                    if (c.A == 0)
                    {
                        continue;
                    }

                    for (int sy = 0; sy < scale; sy++)
                    {
                        for (int sx = 0; sx < scale; sx++)
                        {
                            int px = ox + x * scale + sx, py = oy + y * scale + sy;
                            if (px < sheetW && py < sheetH)
                            {
                                sheet[py * sheetW + px] = new Rgba(c.R, c.G, c.B, 255);
                            }
                        }
                    }
                }
            }
        }

        WritePng(Path.Combine(directory, "looks.png"), sheetW, sheetH, sheet);
        File.WriteAllLines(Path.Combine(directory, "looks.txt"), looks.Select((b, i) => $"{i}: {b.Id} ({b.Look!.Shape})"));
    }

    private readonly record struct Rgba(byte R, byte G, byte B, byte A);

    private static void WritePng(string path, int width, int height, Rgba[] pixels)
    {
        using var file = File.Create(path);
        file.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        WriteBigEndian(header, 0, width);
        WriteBigEndian(header, 4, height);
        header[8] = 8; // bitová hloubka
        header[9] = 6; // RGBA
        Chunk(file, "IHDR", header);

        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[1 + width * 4];
            for (int y = 0; y < height; y++)
            {
                row[0] = 0;
                for (int x = 0; x < width; x++)
                {
                    var p = pixels[y * width + x];
                    row[1 + x * 4] = p.R;
                    row[2 + x * 4] = p.G;
                    row[3 + x * 4] = p.B;
                    row[4 + x * 4] = p.A;
                }

                z.Write(row);
            }
        }

        Chunk(file, "IDAT", raw.ToArray());
        Chunk(file, "IEND", Array.Empty<byte>());
    }

    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, data.Length);
        stream.Write(length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        uint crc = Crc(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, (int)crc);
        stream.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in type.Concat(data))
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
        }

        return ~crc;
    }
}
