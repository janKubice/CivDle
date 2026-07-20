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
/// Definice se ukládají přes STABILNÍ STRING ID, ne přes runtime indexy —
/// přeuspořádání datových souborů (modding, patch) save nerozbije; smazané ID
/// je jasná chyba při načtení. Na nekonečné mapě se terén NEUKLÁDÁ: je to čistá
/// funkce (seed + preset), takže při načtení se přesně zrekonstruuje. Ukládají
/// se jen řídká data — budovy a síť cest.
/// </summary>
public sealed class SaveGameSerializer
{
    private const string Magic = "CIVD";

    /// <summary>Verze formátu — zvýšit při každé změně struktury. v4: + vyzkoumané technologie.</summary>
    public const int FormatVersion = 4;

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
        WriteBuildings(writer, simulation);
        WriteRoads(writer, simulation);
        WriteTech(writer, simulation);
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

            // Terén se rekonstruuje z presetu + seedu — bit za bit stejný jako při uložení.
            var preset = FindPreset(content, presetId);
            var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
            var simulation = new Simulation(content, terrain, seed);
            simulation.RestoreState(resources, population, tickCount);
            ReadBuildings(reader, content, simulation);
            ReadRoads(reader, simulation);
            ReadTech(reader, content, simulation);

            return (simulation, metadata);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException)
        {
            throw new SaveLoadException("Uložená hra je poškozená nebo neúplná.", ex);
        }
    }

    private static TerrainPreset FindPreset(GameContent content, string presetId)
    {
        foreach (var preset in content.WorldGen.Presets)
        {
            if (preset.Id == presetId)
            {
                return preset;
            }
        }

        throw new SaveLoadException($"Save používá typ světa '{presetId}', který v aktuálních datech neexistuje.");
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

    private static void WriteRoads(BinaryWriter writer, Simulation simulation)
    {
        var roadTiles = simulation.RoadTiles;
        writer.Write(roadTiles.Count);
        for (int i = 0; i < roadTiles.Count; i++)
        {
            writer.Write(roadTiles[i].X);
            writer.Write(roadTiles[i].Y);
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

    private static void ReadBuildings(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 5_000_000, what: "budov");
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

            simulation.RestoreBuilding(defIndex, x, y, progress);
        }
    }

    private static void ReadRoads(BinaryReader reader, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "silnic");
        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            simulation.AddRoadTile(x, y);
        }
    }

    private static void WriteTech(BinaryWriter writer, Simulation simulation)
    {
        var techDefs = SimContent(simulation).Techs;
        var researched = simulation.ResearchedTechIndices().ToList();
        writer.Write(researched.Count);
        foreach (int index in researched)
        {
            writer.Write(techDefs[index].Id);
        }
    }

    private static void ReadTech(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 100_000, what: "technologií");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            if (content.Techs.TryIndexOf(id, out int index))
            {
                simulation.RestoreTech(index);
            }

            // Smazaná technologie v datech se ze savu tiše přeskočí (odemčení
            // budov by stejně chybělo — nedělá se z toho chyba).
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

    /// <summary>Obsah, nad kterým simulace běží (interní přístup v rámci assembly).</summary>
    private static GameContent SimContent(Simulation simulation) => simulation.ContentRef;
}
