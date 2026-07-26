using System.IO.Compression;
using System.Text;
using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Save;

/// <summary>
/// Kompatibilita savů. Idle hra se hraje týdny — každá aktualizace, která smaže
/// rozehranou hru, je horší než jakákoli chybějící funkce. Testuje se proto obojí:
/// že novější hra přečte STARŠÍ save, a že starší hra nepadne na sekci, kterou
/// ještě nezná.
/// </summary>
public sealed class SaveCompatibilityTests
{
    [Fact]
    public void SaveWithAnUnknownSection_LoadsAndIgnoresIt()
    {
        var content = TestData.LoadRealContent();
        var original = NewGame(content);
        original.AddResource(0, 25);
        for (int i = 0; i < 10; i++)
        {
            original.Tick();
        }

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, original, Metadata());

        // Sekce z „budoucí" verze hry přilepená na konec těla savu.
        var withExtra = AppendSection(stream.ToArray(), "quantum_tunnels", writer =>
        {
            writer.Write(42);
            writer.Write("něco, co tahle verze neumí");
        });

        var (loaded, _) = new SaveGameSerializer().Read(new MemoryStream(withExtra), content);

        Assert.Equal(original.Population, loaded.Population, 6);
        Assert.Equal(original.TickCount, loaded.TickCount);
        Assert.Equal(original.GetResource(0), loaded.GetResource(0), 6);
    }

    [Fact]
    public void SaveMissingAnOptionalSection_LoadsWithDefaults()
    {
        var content = TestData.LoadRealContent();
        var original = NewGame(content);
        original.AddResource(0, 40);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, original, Metadata());

        // Vyhodíme sekci se zásahy do světa — jako by save vznikl dřív, než vznikla.
        var without = RemoveSection(stream.ToArray(), "world");
        var (loaded, _) = new SaveGameSerializer().Read(new MemoryStream(without), content);

        Assert.Equal(original.GetResource(0), loaded.GetResource(0), 6);
        Assert.Equal(original.Population, loaded.Population, 6);
    }

    [Fact]
    public void SaveFromANewerGame_FailsWithAReadableMessage()
    {
        var content = TestData.LoadRealContent();
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, NewGame(content), Metadata());

        var bytes = stream.ToArray();
        BitConverter.GetBytes(SaveGameSerializer.FormatVersion + 5).CopyTo(bytes, 4);

        var error = Assert.Throws<SaveLoadException>(
            () => new SaveGameSerializer().Read(new MemoryStream(bytes), content));
        Assert.Contains("novější", error.Message);
    }

    /// <summary>Plný sklad nesmí po načtení „vyschnout" jen kvůli pořadí sekcí.</summary>
    [Fact]
    public void FullStorage_SurvivesTheRoundtrip()
    {
        var content = TestData.LoadRealContent();
        var original = NewGame(content);

        // Naplníme surovinu až po strop skladu — přesně ten případ, kdy dřív
        // pořadí sekcí zásoby uřízlo.
        original.AddResource(0, 1_000_000); // ořeže se na aktuální kapacitu
        double before = original.GetResource(0);
        Assert.True(before > 0, "Test nemá co ověřovat — sklad zůstal prázdný.");

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, original, Metadata());
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(before, loaded.GetResource(0), 6);
    }

    private static Simulation NewGame(GameContent content) =>
        new(content, new UniformTerrain(LandBiome(content)));

    private static byte LandBiome(GameContent content)
    {
        for (byte i = 0; i < content.Biomes.Count; i++)
        {
            if (!content.Biomes[i].IsWater)
            {
                return i;
            }
        }

        throw new InvalidOperationException("Obsah nemá pevninský biom.");
    }

    private static SaveMetadata Metadata() => new(12345, "medium", "continents", DateTime.UtcNow);

    /// <summary>Přilepí na konec těla savu novou sekci (simuluje save z novější hry).</summary>
    private static byte[] AppendSection(byte[] save, string name, Action<BinaryWriter> body)
    {
        var (header, sections) = SplitSave(save);

        var payload = new MemoryStream();
        using (var writer = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            body(writer);
        }

        var rebuilt = new MemoryStream();
        rebuilt.Write(sections, 0, sections.Length);
        using (var writer = new BinaryWriter(rebuilt, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(name);
            writer.Write((int)payload.Length);
            writer.Write(payload.GetBuffer(), 0, (int)payload.Length);
        }

        return Recompress(header, rebuilt.ToArray());
    }

    /// <summary>Vyhodí sekci daného jména (simuluje save ze starší hry).</summary>
    private static byte[] RemoveSection(byte[] save, string name)
    {
        var (header, sections) = SplitSave(save);

        var kept = new MemoryStream();
        using var reader = new BinaryReader(new MemoryStream(sections), Encoding.UTF8);
        using var writer = new BinaryWriter(kept, Encoding.UTF8, leaveOpen: true);

        // Metadata (čas, seed, dvě ID) leží před sekcemi — zkopírují se beze změny.
        writer.Write(reader.ReadInt64());
        writer.Write(reader.ReadInt64());
        writer.Write(reader.ReadString());
        writer.Write(reader.ReadString());

        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            string sectionName = reader.ReadString();
            int length = reader.ReadInt32();
            var payload = reader.ReadBytes(length);
            if (sectionName == name)
            {
                continue;
            }

            writer.Write(sectionName);
            writer.Write(length);
            writer.Write(payload);
        }

        writer.Flush();
        return Recompress(header, kept.ToArray());
    }

    /// <summary>Rozdělí save na nekomprimovanou hlavičku a rozbalené tělo.</summary>
    private static (byte[] Header, byte[] Body) SplitSave(byte[] save)
    {
        var header = save.Take(8).ToArray();
        using var compressed = new MemoryStream(save, 8, save.Length - 8);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        var body = new MemoryStream();
        gzip.CopyTo(body);
        return (header, body.ToArray());
    }

    private static byte[] Recompress(byte[] header, byte[] body)
    {
        var result = new MemoryStream();
        result.Write(header, 0, header.Length);
        using (var gzip = new GZipStream(result, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(body, 0, body.Length);
        }

        return result.ToArray();
    }
}
