using System.IO.Compression;
using System.Text;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;

namespace CivDle.Core.Save;

/// <summary>
/// Binární (de)serializace uložené hry — vlastní writer dle tech-stack.md
/// („plná kontrola, žádný balast"), verzovaná hlavička od prvního savu.
///
/// Definice se ukládají přes STABILNÍ STRING ID (tabulky v hlavičce bloků),
/// ne přes runtime indexy — přeuspořádání datových souborů (modding, patch)
/// tak save nerozbije; smazané ID je jasná chyba při načtení.
/// Mapa se ukládá celá (ne jen seed): změna dat generátoru nesmí hráči
/// posunout terén pod už postavenými budovami.
/// </summary>
public sealed class SaveGameSerializer
{
    private const string Magic = "CIVD";

    /// <summary>Verze formátu — zvýšit při každé změně struktury (a napsat migraci).</summary>
    public const int FormatVersion = 1;

    /// <summary>Zapíše hru do streamu (hlavička nekomprimovaná, tělo gzip).</summary>
    public void Write(Stream stream, Simulation simulation, SaveMetadata metadata)
    {
        using var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        header.Write(Encoding.ASCII.GetBytes(Magic));
        header.Write(FormatVersion);
        header.Flush();

        using var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
        using var writer = new BinaryWriter(gzip, Encoding.UTF8);

        writer.Write(metadata.SavedAtUtc.Ticks);
        writer.Write(metadata.Seed);
        writer.Write(metadata.SizeId);
        writer.Write(metadata.PresetId);

        writer.Write(simulation.TickCount);
        writer.Write(simulation.Population);

        WriteResources(writer, simulation);
        WriteMap(writer, simulation);
        WriteBuildings(writer, simulation);
    }

    /// <summary>Načte hru ze streamu a sestaví simulaci nad aktuálním obsahem.</summary>
    public (Simulation Simulation, SaveMetadata Metadata) Read(Stream stream, GameContent content)
    {
        try
        {
            using var header = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            var magic = header.ReadBytes(4);
            if (magic.Length != 4 || Encoding.ASCII.GetString(magic) != Magic)
            {
                throw new SaveLoadException("Soubor není uložená hra CivDle (chybí hlavička).");
            }

            int version = header.ReadInt32();
            if (version != FormatVersion)
            {
                throw new SaveLoadException($"Nepodporovaná verze savu {version} — tahle verze hry umí verzi {FormatVersion}.");
            }

            using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new BinaryReader(gzip, Encoding.UTF8);

            var savedAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            long seed = reader.ReadInt64();
            string sizeId = reader.ReadString();
            string presetId = reader.ReadString();
            var metadata = new SaveMetadata(seed, sizeId, presetId, savedAt);

            long tickCount = reader.ReadInt64();
            double population = reader.ReadDouble();

            double[] resources = ReadResources(reader, content);
            var map = ReadMap(reader, content);

            var simulation = new Simulation(content, map);
            simulation.RestoreState(resources, population, tickCount);
            ReadBuildings(reader, content, simulation);

            return (simulation, metadata);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException)
        {
            throw new SaveLoadException("Uložená hra je poškozená nebo neúplná.", ex);
        }
    }

    // ----- zápis -----

    private static void WriteResources(BinaryWriter writer, Simulation simulation)
    {
        var resources = simulation.Resources;
        writer.Write(resources.Length);
        for (int i = 0; i < resources.Length; i++)
        {
            writer.Write(SimContent(simulation).Resources[i].Id);
            writer.Write(resources[i]);
        }
    }

    private static void WriteMap(BinaryWriter writer, Simulation simulation)
    {
        var map = simulation.Map;
        var biomes = SimContent(simulation).Biomes;

        writer.Write(map.Width);
        writer.Write(map.Height);

        // Tabulka ID biomů: byte v dlaždicích = index do téhle tabulky.
        writer.Write(biomes.Count);
        for (int i = 0; i < biomes.Count; i++)
        {
            writer.Write(biomes[i].Id);
        }

        writer.Write(map.BiomeIndices);
        for (int i = 0; i < map.Elevation.Length; i++)
        {
            writer.Write(map.Elevation[i]);
        }

        for (int i = 0; i < map.Moisture.Length; i++)
        {
            writer.Write(map.Moisture[i]);
        }
    }

    private static void WriteBuildings(BinaryWriter writer, Simulation simulation)
    {
        var buildingDefs = SimContent(simulation).Buildings;
        var buildings = simulation.Buildings;
        writer.Write(buildings.Length);
        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            writer.Write(buildingDefs[building.DefIndex].Id);
            writer.Write(building.X);
            writer.Write(building.Y);
            writer.Write(building.Progress);
        }
    }

    // ----- čtení -----

    private static double[] ReadResources(BinaryReader reader, GameContent content)
    {
        var amounts = new double[content.Resources.Count];
        int count = ReadCount(reader, max: 10_000, what: "surovin");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            double amount = reader.ReadDouble();
            if (!content.Resources.TryIndexOf(id, out int index))
            {
                throw new SaveLoadException($"Save odkazuje na surovinu '{id}', která v aktuálních datech neexistuje.");
            }

            amounts[index] = amount;
        }

        return amounts;
    }

    private static WorldMap ReadMap(BinaryReader reader, GameContent content)
    {
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        if (width is < 1 or > 8192 || height is < 1 or > 8192)
        {
            throw new SaveLoadException($"Save má nesmyslné rozměry mapy {width}×{height}.");
        }

        // Remap: save-index biomu → ID → aktuální runtime index.
        int biomeCount = ReadCount(reader, max: BiomeRegistry.MaxBiomes, what: "biomů");
        var biomeRemap = new byte[biomeCount];
        for (int i = 0; i < biomeCount; i++)
        {
            string id = reader.ReadString();
            if (!content.Biomes.TryIndexOf(id, out int index))
            {
                throw new SaveLoadException($"Save odkazuje na biom '{id}', který v aktuálních datech neexistuje.");
            }

            biomeRemap[i] = (byte)index;
        }

        var map = new WorldMap(width, height);
        int tiles = width * height;

        var savedBiomes = ReadExactly(reader, tiles);
        for (int i = 0; i < tiles; i++)
        {
            byte saved = savedBiomes[i];
            if (saved >= biomeCount)
            {
                throw new SaveLoadException($"Save obsahuje neplatný index biomu {saved}.");
            }

            map.BiomeIndices[i] = biomeRemap[saved];
        }

        for (int i = 0; i < tiles; i++)
        {
            map.Elevation[i] = reader.ReadSingle();
        }

        for (int i = 0; i < tiles; i++)
        {
            map.Moisture[i] = reader.ReadSingle();
        }

        return map;
    }

    private static void ReadBuildings(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 1_000_000, what: "budov");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            float progress = reader.ReadSingle();

            if (!content.Buildings.TryIndexOf(id, out int defIndex))
            {
                throw new SaveLoadException($"Save odkazuje na budovu '{id}', která v aktuálních datech neexistuje.");
            }

            try
            {
                simulation.RestoreBuilding(defIndex, x, y, progress);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new SaveLoadException(ex.Message, ex);
            }
        }
    }

    private static int ReadCount(BinaryReader reader, int max, string what)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > max)
        {
            throw new SaveLoadException($"Save má nesmyslný počet {what}: {count}.");
        }

        return count;
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        var buffer = reader.ReadBytes(count);
        if (buffer.Length != count)
        {
            throw new SaveLoadException("Uložená hra je poškozená nebo neúplná.");
        }

        return buffer;
    }

    /// <summary>Obsah, nad kterým simulace běží (interní přístup v rámci assembly).</summary>
    private static GameContent SimContent(Simulation simulation) => simulation.ContentRef;
}
