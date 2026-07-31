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
///
/// <para><b>Od verze 14 je tělo savu SEKČNÍ.</b> Každá část nese svůj název a délku,
/// takže čtečka umí přeskočit sekci, kterou nezná, a chybějící sekci nechat na
/// výchozí hodnotě. Do verze 13 byl formát lineární — každá nová mechanika tak
/// znehodnotila rozehranou hru, což je u idle hry hraté týdny to nejhorší, co
/// se může stát. Starší savy (v12, v13) proto pořád umíme přečíst starou cestou;
/// nové už nikdy „nevyprší".</para>
/// </summary>
public sealed class SaveGameSerializer
{
    private const string Magic = "CIVD";

    /// <summary>
    /// Verze formátu. v6: + úkoly. v7: + skrýše. v8: + zasazené uzly. v9: + zóny.
    /// v10: + politiky. v11: + guvernér. v12: + známé suroviny. v13: + zásahy do světa.
    /// <b>v14: sekční formát</b> — od téhle verze se čísluje jen kvůli přehledu,
    /// přidání sekce už kompatibilitu neruší.
    /// </summary>
    public const int FormatVersion = 14;

    /// <summary>První verze se sekčním tělem (starší se čtou lineárně).</summary>
    private const int FirstSectionedVersion = 14;

    /// <summary>Nejstarší verze, jejíž rozvržení ještě umíme přečíst.</summary>
    private const int OldestReadableVersion = 12;

    // Názvy sekcí. Jsou součástí formátu — nikdy nepřejmenovávat, jen přidávat.
    private const string SectionCore = "core";
    private const string SectionResources = "resources";
    private const string SectionBuildings = "buildings";
    private const string SectionRoads = "roads";
    private const string SectionTech = "tech";
    private const string SectionPrestige = "prestige";
    private const string SectionQuests = "quests";
    private const string SectionDiscoveries = "discoveries";
    private const string SectionPlanted = "planted";
    private const string SectionZones = "zones";
    private const string SectionPolicies = "policies";
    private const string SectionGovernor = "governor";
    private const string SectionKnownResources = "known";
    private const string SectionWorldChanges = "world";
    private const string SectionTutorial = "tutorial";
    private const string SectionChallenges = "challenges";
    private const string SectionElection = "election";
    private const string SectionMilestones = "milestones";
    private const string SectionConstruction = "construction";
    private const string SectionNodes = "nodes";
    private const string SectionPollution = "pollution";
    private const string SectionContracts = "contracts";
    private const string SectionCitizens = "citizens";
    private const string SectionNeighbours = "neighbours";
    private const string SectionRuns = "runs";

    /// <summary>Zapíše hru do streamu (hlavička nekomprimovaná, tělo gzip a sekční).</summary>
    public void Write(Stream stream, Simulation simulation, SaveMetadata metadata)
    {
        using var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        header.Write(Encoding.ASCII.GetBytes(Magic));
        header.Write(FormatVersion);
        header.Flush();

        using var gzip = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
        using var writer = new BinaryWriter(gzip, Encoding.UTF8);

        // Metadata leží před sekcemi: bez seedu a presetu nejde postavit terén,
        // takže je čtečka potřebuje dřív, než začne sekce vůbec procházet.
        writer.Write(metadata.SavedAtUtc.Ticks);
        writer.Write(metadata.Seed);
        writer.Write(metadata.SizeId);
        writer.Write(metadata.PresetId);

        WriteSection(writer, SectionCore, w =>
        {
            w.Write(simulation.TickCount);
            w.Write(simulation.Population);
        });
        WriteSection(writer, SectionResources, w => WriteResources(w, simulation));
        WriteSection(writer, SectionBuildings, w => WriteBuildings(w, simulation));
        WriteSection(writer, SectionRoads, w => WriteRoads(w, simulation));
        WriteSection(writer, SectionTech, w => WriteTech(w, simulation));
        WriteSection(writer, SectionPrestige, w => WritePrestige(w, simulation));
        WriteSection(writer, SectionQuests, w => WriteQuests(w, simulation));
        WriteSection(writer, SectionDiscoveries, w => WriteDiscoveries(w, simulation));
        WriteSection(writer, SectionPlanted, w => WritePlanted(w, simulation));
        WriteSection(writer, SectionZones, w => WriteZones(w, simulation));
        WriteSection(writer, SectionPolicies, w => WritePolicies(w, simulation));
        WriteSection(writer, SectionGovernor, w =>
        {
            w.Write(simulation.AutoUpgradeLevelRaw);
            w.Write(simulation.AutoMergeRaw);
        });
        WriteSection(writer, SectionKnownResources, w => WriteKnownResources(w, simulation));
        WriteSection(writer, SectionWorldChanges, w => WriteWorldChanges(w, simulation));
        WriteSection(writer, SectionTutorial, w => w.Write(simulation.TutorialStep));
        WriteSection(writer, SectionChallenges, w => WriteChallenges(w, simulation));
        WriteSection(writer, SectionMilestones, w => WriteMilestones(w, simulation));
        WriteSection(writer, SectionConstruction, w => WriteConstruction(w, simulation));
        WriteSection(writer, SectionNodes, w => WriteNodes(w, simulation));
        WriteSection(writer, SectionPollution, w => WritePollution(w, simulation));
        WriteSection(writer, SectionContracts, w => WriteContracts(w, simulation));
        WriteSection(writer, SectionCitizens, w => WriteCitizens(w, simulation));
        WriteSection(writer, SectionNeighbours, w => WriteNeighbours(w, content: null, simulation));
        WriteSection(writer, SectionElection, w =>
        {
            w.Write(simulation.ElectionTerm);
            w.Write(simulation.ElectedCandidate);
        });

        // Vrchol běhu a nejlepší běh vůbec — bez nich by se bilance po Vzestupu
        // po restartu hry poměřovala s nulou.
        WriteSection(writer, SectionRuns, w =>
        {
            w.Write(simulation.PeakPopulation);
            w.Write(simulation.BestRunPopulation);
        });
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
            if (version > FormatVersion)
            {
                throw new SaveLoadException(
                    $"Save je z novější verze hry (formát {version}, tahle hra umí {FormatVersion}). Aktualizuj hru.");
            }

            if (version < OldestReadableVersion)
            {
                throw new SaveLoadException(
                    $"Save je z příliš staré verze hry (formát {version}, nejstarší čitelný je {OldestReadableVersion}).");
            }

            using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var reader = new BinaryReader(gzip, Encoding.UTF8);

            var savedAt = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
            long seed = reader.ReadInt64();
            string sizeId = reader.ReadString();
            string presetId = reader.ReadString();
            var metadata = new SaveMetadata(seed, sizeId, presetId, savedAt);

            // Terén se rekonstruuje z presetu + seedu — bit za bit stejný jako při uložení.
            var preset = FindPreset(content, presetId);
            var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
            var simulation = new Simulation(content, terrain, seed);

            if (version >= FirstSectionedVersion)
            {
                ReadSections(reader, content, simulation);
            }
            else
            {
                ReadLegacyBody(reader, content, simulation, version);
            }

            simulation.FinalizeLoad(); // bonusy Vzestupu → přepočet bydlení/skladů + politik
            return (simulation, metadata);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or IOException)
        {
            throw new SaveLoadException("Uložená hra je poškozená nebo neúplná.", ex);
        }
    }

    /// <summary>
    /// Zapíše jednu sekci: název, délka v bajtech, obsah. Obsah se nejdřív složí
    /// do paměti, protože gzip stream neumí zpětně doplnit délku — a bez délky by
    /// čtečka neznámou sekci nedokázala přeskočit.
    /// </summary>
    /// <summary>Dnešní sada výzev: den, indexy do fondu, výchozí metriky a splněnost.</summary>
    /// <summary>Dosažené milníky podle ID — přeskládání dat tak nezpůsobí opakovanou oslavu.</summary>
    private static void WriteMilestones(BinaryWriter writer, Simulation simulation)
    {
        var milestones = SimContent(simulation).Milestones;
        var reached = simulation.ReachedMilestoneIndices().ToList();
        writer.Write(reached.Count);
        foreach (int index in reached)
        {
            writer.Write(milestones[index].Id);
        }
    }

    private static void ReadMilestones(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 10_000, what: "milníků");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            for (int m = 0; m < content.Milestones.Count; m++)
            {
                if (content.Milestones[m].Id == id)
                {
                    simulation.RestoreMilestone(m);
                    break;
                }
            }

            // Smazaný milník v datech se prostě přeskočí.
        }
    }

    private static void ReadGovernor(BinaryReader reader, Simulation simulation)
    {
        simulation.RestoreAutoUpgradeLevel(reader.ReadInt32());

        // Automatické slučování přibylo později — starší sekce ho nemá.
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            simulation.RestoreAutoMerge(reader.ReadBoolean());
        }
    }

    private static void WriteChallenges(BinaryWriter writer, Simulation simulation)
    {
        var active = simulation.ActiveChallenges;
        writer.Write(simulation.ChallengeDay);
        writer.Write(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            writer.Write(active[i]);
            writer.Write(simulation.ChallengeBaselines[i]);
            writer.Write(simulation.ChallengeDoneFlags[i]);
        }
    }

    private static void ReadChallenges(BinaryReader reader, Simulation simulation)
    {
        string day = reader.ReadString();
        int count = reader.ReadInt32();
        var indices = new List<int>(count);
        var baselines = new List<long>(count);
        var done = new List<bool>(count);
        for (int i = 0; i < count; i++)
        {
            indices.Add(reader.ReadInt32());
            baselines.Add(reader.ReadInt64());
            done.Add(reader.ReadBoolean());
        }

        simulation.RestoreChallenges(day, indices, baselines, done);
    }

    private static void WriteSection(BinaryWriter writer, string name, Action<BinaryWriter> body)
    {
        var buffer = new MemoryStream();
        using (var sectionWriter = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            body(sectionWriter);
        }

        writer.Write(name);
        writer.Write((int)buffer.Length);
        writer.Write(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>
    /// Projde sekce až do konce streamu. Neznámou sekci přeskočí (save z novější
    /// hry se stejným hlavním formátem), chybějící sekce prostě zůstane výchozí.
    /// </summary>
    private static void ReadSections(BinaryReader reader, GameContent content, Simulation simulation)
    {
        while (true)
        {
            string name;
            int length;
            try
            {
                name = reader.ReadString();
                length = reader.ReadInt32();
            }
            catch (EndOfStreamException)
            {
                return; // konec těla — všechny sekce přečtené
            }

            if (length < 0)
            {
                throw new SaveLoadException($"Poškozená sekce '{name}' (záporná délka).");
            }

            var payload = reader.ReadBytes(length);
            if (payload.Length != length)
            {
                throw new SaveLoadException($"Poškozená sekce '{name}' (neúplná data).");
            }

            using var section = new BinaryReader(new MemoryStream(payload), Encoding.UTF8);
            ApplySection(name, section, content, simulation);
        }
    }

    private static void ApplySection(string name, BinaryReader section, GameContent content, Simulation simulation)
    {
        switch (name)
        {
            case SectionCore:
                long tickCount = section.ReadInt64();       // pořadí musí sedět se zápisem
                simulation.RestoreCore(section.ReadDouble(), tickCount);
                break;
            case SectionResources: simulation.RestoreResources(ReadResources(section, content)); break;
            case SectionBuildings: ReadBuildings(section, content, simulation); break;
            case SectionRoads: ReadRoads(section, simulation); break;
            case SectionTech: ReadTech(section, content, simulation); break;
            case SectionPrestige: ReadPrestige(section, content, simulation); break;
            case SectionQuests: ReadQuests(section, content, simulation); break;
            case SectionDiscoveries: ReadDiscoveries(section, simulation); break;
            case SectionPlanted: ReadPlanted(section, content, simulation); break;
            case SectionZones: ReadZones(section, content, simulation); break;
            case SectionPolicies: ReadPolicies(section, content, simulation); break;
            case SectionGovernor: ReadGovernor(section, simulation); break;
            case SectionKnownResources: ReadKnownResources(section, content, simulation); break;
            case SectionWorldChanges: ReadWorldChanges(section, content, simulation); break;
            case SectionTutorial: simulation.RestoreTutorialStep(section.ReadInt32()); break;
            case SectionChallenges: ReadChallenges(section, simulation); break;
            case SectionConstruction: ReadConstruction(section, simulation); break;
            case SectionNodes: ReadNodes(section, simulation); break;
            case SectionPollution: ReadPollution(section, simulation); break;
            case SectionContracts: ReadContracts(section, content, simulation); break;
            case SectionCitizens: ReadCitizens(section, simulation); break;
            case SectionNeighbours: ReadNeighbours(section, content, simulation); break;
            case SectionElection: simulation.RestoreElection(section.ReadInt64(), section.ReadInt32()); break;
            case SectionMilestones: ReadMilestones(section, content, simulation); break;
            case SectionRuns:
                simulation.PeakPopulation = section.ReadInt64();  // pořadí musí sedět se zápisem
                simulation.BestRunPopulation = section.ReadInt64();
                break;
            default: break; // neznámá sekce z novější hry — přeskočit, ne spadnout
        }
    }

    /// <summary>
    /// Čtení starého LINEÁRNÍHO těla (v12, v13). Sekce se musí brát v přesném pořadí,
    /// v jakém je tehdejší writer zapsal — proto zůstává jako oddělená cesta, aby
    /// sekční čtečku nezaplevelila výjimkami z minulosti.
    /// </summary>
    private static void ReadLegacyBody(BinaryReader reader, GameContent content, Simulation simulation, int version)
    {
        long tickCount = reader.ReadInt64();
        double population = reader.ReadDouble();
        simulation.RestoreState(ReadResources(reader, content), population, tickCount);

        ReadBuildings(reader, content, simulation);
        ReadRoads(reader, simulation);
        ReadTech(reader, content, simulation);
        ReadPrestige(reader, content, simulation);
        ReadQuests(reader, content, simulation);
        ReadDiscoveries(reader, simulation);
        ReadPlanted(reader, content, simulation);
        ReadZones(reader, content, simulation);
        ReadPolicies(reader, content, simulation);
        simulation.RestoreAutoUpgradeLevel(reader.ReadInt32());
        ReadKnownResources(reader, content, simulation);

        if (version >= 13)
        {
            ReadWorldChanges(reader, content, simulation);
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

    /// <summary>
    /// Rozestavěné budovy: jen ty, které zrovna stojí na lešení, a jako index do
    /// pořadí ze sekce budov. Vlastní sekce místo dalšího pole u každé budovy —
    /// staré savy ji prostě nemají a jejich města se načtou hotová, což je
    /// správně (dřív se stavělo okamžitě).
    /// </summary>
    private static void WriteConstruction(BinaryWriter writer, Simulation simulation)
    {
        var buildings = simulation.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                count++;
            }
        }

        writer.Write(count);
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                writer.Write(i);
                writer.Write(buildings[i].BuildTicksRemaining);
            }
        }

        writer.Write(simulation.WondersCompleted);
    }

    private static void ReadConstruction(BinaryReader reader, Simulation simulation)
    {
        int count = ReadCount(reader, max: 5_000_000, what: "staveniště");
        for (int i = 0; i < count; i++)
        {
            int buildingIndex = reader.ReadInt32();
            int remaining = reader.ReadInt32();
            simulation.RestoreConstruction(buildingIndex, remaining);
        }

        // Počet dostavěných divů dorazil do sekce později — starší savy ho nemají.
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            simulation.RestoreWondersCompleted(reader.ReadInt64());
        }
    }

    /// <summary>
    /// Vytěžené dlaždice. Ukládají se jen ty dotčené — na nekonečné mapě není jak
    /// (ani proč) si pamatovat každý strom. Starší savy sekci nemají a načtou se
    /// s nedotčenou krajinou, což je správně: dřív se nic nevytěžilo.
    /// </summary>
    private static void WriteNodes(BinaryWriter writer, Simulation simulation)
    {
        var entries = simulation.Nodes.Entries().ToList();
        writer.Write(entries.Count);
        foreach (var (x, y, chargesLeft, depletedTick) in entries)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(chargesLeft);
            writer.Write(depletedTick);
        }
    }

    /// <summary>
    /// Zamořené buňky. Ukládají se jen ty špinavé — čistá mapa nezabere nic.
    /// Starší savy sekci nemají a načtou se s nedotčenou krajinou, což je správně:
    /// dřív se nedalo nic zamořit.
    /// </summary>
    private static void WritePollution(BinaryWriter writer, Simulation simulation)
    {
        var entries = simulation.PollutionMap.Entries().ToList();
        writer.Write(entries.Count);
        foreach (var (cellX, cellY, air, water, soil) in entries)
        {
            writer.Write(cellX);
            writer.Write(cellY);
            writer.Write(air);
            writer.Write(water);
            writer.Write(soil);
        }
    }

    private static void ReadPollution(BinaryReader reader, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "zamořených buněk");
        for (int i = 0; i < count; i++)
        {
            int cellX = reader.ReadInt32();
            int cellY = reader.ReadInt32();
            double air = reader.ReadDouble();
            double water = reader.ReadDouble();
            double soil = reader.ReadDouble();
            simulation.PollutionMap.RestoreCell(cellX, cellY, air, water, soil);
        }
    }

    /// <summary>
    /// Nástěnka zakázek. Ukládá se přes stabilní ID šablony, ne přes index —
    /// aby přidání nové zakázky do dat nepřeházelo rozehrané nabídky.
    /// Starší savy sekci nemají a načtou se s prázdnou nástěnkou, která se sama
    /// zaplní; o nic se tím nepřijde.
    /// </summary>
    private static void WriteContracts(BinaryWriter writer, Simulation simulation)
    {
        writer.Write(simulation.ContractsCompleted);

        var slots = simulation.ContractSlots;
        writer.Write(slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            var def = simulation.ContractAt(i);
            writer.Write(def?.Id ?? string.Empty); // prázdný řetězec = volné místo
            writer.Write(slots[i].DemandAmount);
            writer.Write(slots[i].TicksLeft);
            writer.Write(slots[i].RewardScale);
        }
    }

    private static void ReadContracts(BinaryReader reader, GameContent content, Simulation simulation)
    {
        simulation.RestoreContractsCompleted(reader.ReadInt64());

        int count = ReadCount(reader, max: 64, what: "míst na nástěnce zakázek");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            long demand = reader.ReadInt64();
            int ticksLeft = reader.ReadInt32();
            double scale = reader.ReadDouble();

            // Zakázka, která z dat zmizela, se tiše zahodí — místo se doplní samo.
            // Padat kvůli obsahu, který se mezi verzemi mění, by byla krutost.
            if (id.Length == 0 || !content.Contracts.Contracts.TryIndexOf(id, out int defIndex))
            {
                continue;
            }

            simulation.RestoreContractSlot(i, defIndex, demand, ticksLeft, scale);
        }
    }

    /// <summary>
    /// Obyvatelé: běžící prosba a zakladatelé budov. Jména se ukládají jako dvojice
    /// indexů do seznamů — kratší než řetězec a odolné vůči přeložení hry.
    /// Starší savy sekci nemají a načtou se bez zakladatelů, což je správně.
    /// </summary>
    private static void WriteCitizens(BinaryWriter writer, Simulation simulation)
    {
        var request = simulation.PendingCitizenRequest;
        writer.Write(request.DefIndex);
        writer.Write(request.FirstNameIndex);
        writer.Write(request.SurnameIndex);
        writer.Write(request.TicksLeft);
        writer.Write(simulation.FoundedByCitizens);

        var founders = simulation.Founders().ToList();
        writer.Write(founders.Count);
        foreach (var (x, y, first, surname) in founders)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(first);
            writer.Write(surname);
        }
    }

    private static void ReadCitizens(BinaryReader reader, Simulation simulation)
    {
        int defIndex = reader.ReadInt32();
        int first = reader.ReadInt32();
        int surname = reader.ReadInt32();
        int ticksLeft = reader.ReadInt32();
        simulation.RestoreCitizenRequest(defIndex, first, surname, ticksLeft, cooldown: 0);
        simulation.RestoreFoundedByCitizens(reader.ReadInt64());

        int count = ReadCount(reader, max: 5_000_000, what: "zakladatelů budov");
        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            simulation.RestoreFounder(x, y, reader.ReadInt32(), reader.ReadInt32());
        }
    }

    /// <summary>
    /// Vztahy se sousedy. Ukládají se přes stabilní ID, ne index — přidání
    /// souseda do dat tak nepřehází, s kým už město obchodovalo. Starší savy
    /// sekci nemají a začnou jako cizinci, což je správně.
    /// </summary>
    private static void WriteNeighbours(BinaryWriter writer, GameContent? content, Simulation simulation)
    {
        _ = content; // katalog se bere ze simulace, parametr drží tvar ostatních zapisovačů
        var ids = simulation.NeighbourIds().ToList();
        writer.Write(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            writer.Write(ids[i]);
            writer.Write(simulation.NeighbourTrades(i));
        }
    }

    private static void ReadNeighbours(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 1000, what: "sousedů");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            long trades = reader.ReadInt64();

            // Soused, který z dat zmizel, se tiše přeskočí — padat kvůli obsahu,
            // co se mezi verzemi mění, by byla krutost.
            if (content.Neighbours.Neighbours.TryIndexOf(id, out int index))
            {
                simulation.RestoreNeighbourTrades(index, trades);
            }
        }
    }

    private static void ReadNodes(BinaryReader reader, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "vytěžených dlaždic");
        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int chargesLeft = reader.ReadInt32();
            long depletedTick = reader.ReadInt64();
            simulation.Nodes.RestoreEntry(x, y, chargesLeft, depletedTick);
        }
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

    private static void WritePrestige(BinaryWriter writer, Simulation simulation)
    {
        writer.Write(simulation.AscensionLevel);
        writer.Write(simulation.PrestigePoints);

        var upgradeDefs = SimContent(simulation).PrestigeUpgrades;
        var purchased = simulation.PurchasedUpgradeIndices().ToList();
        writer.Write(purchased.Count);
        foreach (int index in purchased)
        {
            writer.Write(upgradeDefs[index].Id);
        }
    }

    private static void ReadPrestige(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int ascensionLevel = reader.ReadInt32();
        long points = reader.ReadInt64();
        if (ascensionLevel < 0 || points < 0)
        {
            throw new SaveLoadException($"Save má nesmyslný stav Vzestupu (úroveň {ascensionLevel}, body {points}).");
        }

        simulation.RestoreAscension(ascensionLevel, points);

        int count = ReadCount(reader, max: 100_000, what: "upgradů Vzestupu");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            if (content.PrestigeUpgrades.TryIndexOf(id, out int index))
            {
                simulation.RestoreUpgrade(index);
            }

            // Smazaný upgrade v datech se ze savu tiše přeskočí.
        }
    }

    private static void WriteQuests(BinaryWriter writer, Simulation simulation)
    {
        writer.Write(simulation.DynamicQuestTier);

        var questDefs = SimContent(simulation).Quests;
        var completed = simulation.CompletedQuestIndices().ToList();
        writer.Write(completed.Count);
        foreach (int index in completed)
        {
            writer.Write(questDefs[index].Id);
        }
    }

    private static void ReadQuests(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int dynamicTier = reader.ReadInt32();
        if (dynamicTier < 0)
        {
            throw new SaveLoadException($"Save má nesmyslný tier dynamického úkolu: {dynamicTier}.");
        }

        simulation.DynamicQuestTier = dynamicTier;

        int count = ReadCount(reader, max: 1_000_000, what: "úkolů");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            if (content.Quests.TryIndexOf(id, out int index))
            {
                simulation.RestoreQuestCompleted(index);
            }

            // Smazaný úkol v datech se ze savu tiše přeskočí.
        }
    }

    private static void WriteDiscoveries(BinaryWriter writer, Simulation simulation)
    {
        var claimed = simulation.ClaimedDiscoveries().ToList();
        writer.Write(claimed.Count);
        foreach (var (x, y) in claimed)
        {
            writer.Write(x);
            writer.Write(y);
        }
    }

    private static void ReadDiscoveries(BinaryReader reader, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "skrýší");
        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            simulation.RestoreDiscovery(x, y);
        }
    }

    private static void WritePlanted(BinaryWriter writer, Simulation simulation)
    {
        var resourceDefs = SimContent(simulation).Resources;
        var planted = simulation.PlantedNodes().ToList();
        writer.Write(planted.Count);
        foreach (var (x, y, resourceIndex, amount) in planted)
        {
            writer.Write(x);
            writer.Write(y);
            writer.Write(resourceDefs[resourceIndex].Id); // stabilní ID, ne index
            writer.Write(amount);
        }
    }

    private static void ReadPlanted(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "zasazených uzlů");
        for (int i = 0; i < count; i++)
        {
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            string id = reader.ReadString();
            int amount = reader.ReadInt32();
            if (content.Resources.TryIndexOf(id, out int resourceIndex))
            {
                simulation.RestorePlantedNode(x, y, resourceIndex, amount);
            }

            // Smazaná surovina v datech = zasazený uzel se tiše přeskočí.
        }
    }

    /// <summary>
    /// Zásahy do světa, které nejsou funkcí seedu: terraformované dlaždice a poslední
    /// okno, ve kterém už UFO zasáhlo. Bez toho druhého by se po načtení savu tentýž
    /// zásah provedl znovu.
    /// </summary>
    private static void WriteWorldChanges(BinaryWriter writer, Simulation simulation)
    {
        var biomeDefs = SimContent(simulation).Biomes;
        var overrides = simulation.BiomeOverrides().ToList();
        writer.Write(overrides.Count);
        foreach (var (tile, biomeIndex) in overrides)
        {
            writer.Write(tile);
            writer.Write(biomeDefs[biomeIndex].Id); // stabilní ID, ne index
        }

        writer.Write(simulation.LastUfoWindow);
        writer.Write(simulation.TerraformedTiles);
        writer.Write(simulation.MergedBuildings);
    }

    private static void ReadWorldChanges(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 20_000_000, what: "terraformovaných dlaždic");
        for (int i = 0; i < count; i++)
        {
            long tile = reader.ReadInt64();
            string id = reader.ReadString();
            if (content.Biomes.TryIndexOf(id, out int biomeIndex))
            {
                simulation.RestoreBiomeOverride(tile, (byte)biomeIndex);
            }

            // Smazaný biom v datech = dlaždice se vrátí k původnímu terénu.
        }

        simulation.RestoreLastUfoWindow(reader.ReadInt64());

        // Počítadla terraformace a slučování přibyla později — starší sekce je
        // nemají a to nevadí, sekční formát čte, co tam je.
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            simulation.RestoreTerraformedTiles(reader.ReadInt64());
        }

        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            simulation.RestoreMergedBuildings(reader.ReadInt64());
        }
    }

    private static void WriteZones(BinaryWriter writer, Simulation simulation)
    {
        var zoneTypeDefs = SimContent(simulation).ZoneTypes;
        var zones = simulation.Zones;
        writer.Write(zones.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            writer.Write(zoneTypeDefs[zone.TypeIndex].Id); // stabilní ID, ne index
            writer.Write(zone.X);
            writer.Write(zone.Y);
            writer.Write(zone.Width);
            writer.Write(zone.Height);
        }
    }

    private static void ReadZones(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: Simulation.MaxZones, what: "zón");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            int x = reader.ReadInt32();
            int y = reader.ReadInt32();
            int width = reader.ReadInt32();
            int height = reader.ReadInt32();
            if (content.ZoneTypes.TryIndexOf(id, out int typeIndex))
            {
                simulation.RestoreZone(typeIndex, x, y, width, height);
            }

            // Smazaný typ zóny v datech = zóna se tiše přeskočí.
        }
    }

    private static void WritePolicies(BinaryWriter writer, Simulation simulation)
    {
        var policyDefs = SimContent(simulation).Policies;
        var active = simulation.ActivePolicyIndices().ToList();
        writer.Write(active.Count);
        foreach (int index in active)
        {
            writer.Write(policyDefs[index].Id); // stabilní ID, ne index
        }
    }

    private static void ReadPolicies(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 100_000, what: "politik");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            if (content.Policies.TryIndexOf(id, out int index))
            {
                simulation.RestorePolicyActive(index);
            }

            // Smazaná politika v datech se ze savu tiše přeskočí.
        }
    }

    private static void WriteKnownResources(BinaryWriter writer, Simulation simulation)
    {
        var defs = SimContent(simulation).Resources;
        var known = simulation.KnownResourceIndices().ToList();
        writer.Write(known.Count);
        foreach (int index in known)
        {
            writer.Write(defs[index].Id); // stabilní ID, ne index
        }
    }

    private static void ReadKnownResources(BinaryReader reader, GameContent content, Simulation simulation)
    {
        int count = ReadCount(reader, max: 10_000, what: "známých surovin");
        for (int i = 0; i < count; i++)
        {
            string id = reader.ReadString();
            if (content.Resources.TryIndexOf(id, out int index))
            {
                simulation.MarkResourceKnown(index);
            }

            // Smazaná surovina v datech se tiše přeskočí.
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
