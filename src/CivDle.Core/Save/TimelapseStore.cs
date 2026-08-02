using System.IO.Compression;
using System.Text;
using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Core.Save;

/// <summary>
/// Uložený časosběr, který se dá přehrát i bez rozehrané hry.
/// </summary>
/// <param name="History">Snímky kroniky (i s paletou barev).</param>
/// <param name="Seed">Seed světa — z něj se rekonstruuje terén pod přehrávkou.</param>
/// <param name="SizeId">Velikost světa (informativně).</param>
/// <param name="PresetId">Preset světa — druhá půlka rekonstrukce terénu.</param>
/// <param name="SavedAtUtc">Kdy byl časosběr uložen (řadí seznam v menu).</param>
public sealed record SavedTimelapse(
    CityHistory History,
    long Seed,
    string SizeId,
    string PresetId,
    DateTime SavedAtUtc);

/// <summary>
/// Ukládání a načítání časosběrů jako samostatných souborů.
///
/// <para>Proč to existuje: Vzestup kroniku maže — celý příběh běhu zmizel ve
/// chvíli, kdy byl nejcennější. Uložený časosběr je suvenýr: dá se přehrát
/// z hlavního menu kdykoli později, i po smazání savu.</para>
///
/// <para>Formát je stejné „tělo" jako sekce v savu (<see cref="WriteBody"/> /
/// <see cref="ReadBody"/> sdílí save i export — jeden formát, dva obaly),
/// jen s vlastní hlavičkou a seedem světa navíc, aby šel pod přehrávku
/// zrekonstruovat skutečný terén.</para>
/// </summary>
public sealed class TimelapseStore
{
    private const string Magic = "CDTL";
    private const int FormatVersion = 1;

    /// <summary>Verze těla kroniky (sdílená se sekcí savu) — formát se ještě vyvíjí.</summary>
    private const int BodyVersion = 4;

    private readonly string _directory;

    /// <param name="directory">Složka, kam se časosběry ukládají (v profilu hráče).</param>
    public TimelapseStore(string directory)
    {
        _directory = directory;
    }

    /// <summary>Složka s časosběry — menu z ní čte seznam.</summary>
    public string Directory => _directory;

    /// <summary>Cesty uložených časosběrů, nejnovější první.</summary>
    public IReadOnlyList<string> ListFiles()
    {
        if (!System.IO.Directory.Exists(_directory))
        {
            return Array.Empty<string>();
        }

        return System.IO.Directory.GetFiles(_directory, "*.civtl")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Uloží časosběr. Vrací cestu k souboru, nebo null, když zápis selhal —
    /// plný disk nesmí shodit Vzestup, kvůli kterému se ukládá.
    /// </summary>
    public string? TrySave(CityHistory history, long seed, string sizeId, string presetId)
    {
        if (history.Count < 2)
        {
            return null; // z jednoho snímku není co přehrávat
        }

        try
        {
            System.IO.Directory.CreateDirectory(_directory);
            string path = Path.Combine(_directory, $"{DateTime.Now:yyyyMMdd-HHmmss}.civtl");

            using var stream = File.Create(path);
            using var writer = new BinaryWriter(new GZipStream(stream, CompressionLevel.Optimal), Encoding.UTF8);
            writer.Write(Encoding.ASCII.GetBytes(Magic));
            writer.Write(FormatVersion);
            writer.Write(seed);
            writer.Write(sizeId);
            writer.Write(presetId);
            writer.Write(DateTime.UtcNow.Ticks);
            WriteBody(writer, history);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Časosběr se nepovedlo uložit: {ex.Message}");
            return null;
        }
    }

    /// <summary>Načte časosběr; null = soubor je poškozený nebo z jiné verze.</summary>
    public SavedTimelapse? TryLoad(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(new GZipStream(stream, CompressionMode.Decompress), Encoding.UTF8);
            var magic = reader.ReadBytes(4);
            if (magic.Length != 4 || Encoding.ASCII.GetString(magic) != Magic || reader.ReadInt32() != FormatVersion)
            {
                return null;
            }

            long seed = reader.ReadInt64();
            string sizeId = reader.ReadString();
            string presetId = reader.ReadString();
            var savedAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);

            // Kapacita = tolik, kolik je snímků: načtený časosběr už neroste,
            // takže se nemá co prořeďovat.
            var history = new CityHistory(maxFrames: int.MaxValue);
            ReadBody(reader, history);
            return history.Count < 2 ? null : new SavedTimelapse(history, seed, sizeId, presetId, savedAt);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Soubor z disku je vstup: poškozený exemplář zmizí ze seznamu,
            // ale nesmí shodit menu.
            Console.Error.WriteLine($"Časosběr '{Path.GetFileName(path)}' se nepovedlo načíst: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Zapíše tělo kroniky (paleta + snímky). Sdílené se sekcí savu — jeden
    /// formát, dva obaly, žádné rozjíždění.
    /// </summary>
    internal static void WriteBody(BinaryWriter writer, CityHistory history)
    {
        writer.Write(BodyVersion);

        writer.Write(history.Palette.Count);
        foreach (var color in history.Palette)
        {
            writer.Write(color.R);
            writer.Write(color.G);
            writer.Write(color.B);
        }

        writer.Write(history.Count);
        for (int i = 0; i < history.Count; i++)
        {
            var frame = history.FrameAt(i);
            writer.Write(frame.Tick);
            writer.Write(frame.Population);
            writer.Write(frame.Buildings);
            writer.Write(frame.EraIndex);
            writer.Write(frame.Happiness);
            writer.Write(frame.HousingCapacity);
            writer.Write(frame.Pollution);
            writer.Write(frame.Settlements);
            writer.Write(history.CellsAt(i));
        }
    }

    /// <summary>Přečte tělo kroniky. Starší verze se přeskočí — kronika je příběh, ne stav.</summary>
    internal static void ReadBody(BinaryReader reader, CityHistory history)
    {
        history.Clear();
        if (reader.ReadInt32() != BodyVersion)
        {
            return;
        }

        int paletteCount = reader.ReadInt32();
        if (paletteCount is < 0 or > CityHistory.MaxPaletteColors)
        {
            return;
        }

        var palette = new List<RgbColor>(paletteCount);
        for (int i = 0; i < paletteCount; i++)
        {
            palette.Add(new RgbColor(reader.ReadByte(), reader.ReadByte(), reader.ReadByte()));
        }

        history.RestorePalette(palette);

        int count = reader.ReadInt32();
        for (int i = 0; i < count; i++)
        {
            var frame = new HistoryFrame(
                reader.ReadInt64(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32(),
                reader.ReadDouble(), reader.ReadInt64(), reader.ReadDouble(), reader.ReadInt32());
            var cells = reader.ReadBytes(CityHistory.CellBytes);
            if (cells.Length != CityHistory.CellBytes)
            {
                return; // useknutý soubor — co se přečetlo, to platí
            }

            history.Add(frame, cells);
        }
    }
}
