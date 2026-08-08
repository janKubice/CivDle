using System.Text.Json;
using CivDle.Core.Content.Dto;
using CivDle.Core.Content.Mods;
using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Načte JSON definice ze složky <c>data/</c> a fail-fast je zvaliduje:
/// chybný odkaz nebo hodnota = <see cref="ContentLoadException"/> hned při startu
/// se jménem souboru a srozumitelnou hláškou (viz data-driven-content.md, sekce 8).
/// Součástí validace jsou i jazyky: všechny musí mít shodnou sadu klíčů
/// a pokrývat jména veškerého obsahu (biomy, suroviny, budovy, presety).
/// </summary>
public sealed class ContentLoader
{
    /// <summary>Verze schématu, které tento loader rozumí. Starší/novější data jsou chyba.</summary>
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Mody, jejichž data se vrství na základní hru. Drží se v instanci, protože
    /// všechny <c>Load*</c> metody čtou přes jediné místo (<see cref="ReadFile"/>)
    /// a nemá smysl protahovat seznam všemi.
    /// </summary>
    private IReadOnlyList<ModPackage> _mods = Array.Empty<ModPackage>();

    /// <summary>Načte kompletní herní obsah ze složky s daty.</summary>
    public GameContent LoadFrom(string dataDirectory) => LoadFrom(dataDirectory, Array.Empty<ModPackage>());

    /// <summary>
    /// Načte obsah a navrství na něj mody. Mod nemusí dodat celý soubor — stačí
    /// mu položka, kterou přidává nebo mění (viz <see cref="JsonOverlay"/>).
    /// </summary>
    public GameContent LoadFrom(string dataDirectory, IReadOnlyList<ModPackage> mods)
    {
        if (!Directory.Exists(dataDirectory))
        {
            throw new ContentLoadException(dataDirectory, $"Složka s herními daty '{dataDirectory}' neexistuje.");
        }

        _mods = mods;

        // Suroviny první — odkazují na ně biomy (clickYield) i budovy (ceny, recepty).
        var resources = LoadResources(Path.Combine(dataDirectory, "resources.json"));
        var biomes = LoadBiomes(Path.Combine(dataDirectory, "biomes.json"), resources);
        // Žebříček sídel před budovami: budova může vyžadovat stupeň sídla,
        // takže loader musí znát ID stupňů dřív, než je začne překládat.
        var settlementRanks = LoadSettlementRanks(Path.Combine(dataDirectory, "settlement-ranks.json"));
        var buildings = LoadBuildings(Path.Combine(dataDirectory, "buildings.json"), biomes, resources, settlementRanks);
        var techs = LoadTech(Path.Combine(dataDirectory, "tech.json"), buildings, resources);
        var (prestige, prestigeUpgrades) = LoadPrestige(Path.Combine(dataDirectory, "prestige.json"), resources, buildings, techs);
        var (legacy, legacyUpgrades) = LoadLegacy(Path.Combine(dataDirectory, "legacy.json"), resources, buildings, techs);
        var (quests, questsDynamic) = LoadQuests(Path.Combine(dataDirectory, "quests.json"), resources, buildings, techs);
        var achievements = LoadAchievements(Path.Combine(dataDirectory, "achievements.json"), resources, buildings, techs);
        var events = LoadEvents(Path.Combine(dataDirectory, "events.json"), resources, buildings, techs);
        var eras = LoadEras(Path.Combine(dataDirectory, "eras.json"));
        var zoneTypes = LoadZoneTypes(Path.Combine(dataDirectory, "zones.json"), buildings);
        var policies = LoadPolicies(Path.Combine(dataDirectory, "policies.json"));
        var tiers = LoadAscensionTiers(Path.Combine(dataDirectory, "ascension-tiers.json"), buildings);
        var weather = LoadWeather(Path.Combine(dataDirectory, "weather.json"), biomes);
        var landmarks = LoadLandmarks(Path.Combine(dataDirectory, "landmarks.json"), biomes, resources);
        var features = LoadFeatures(Path.Combine(dataDirectory, "features.json"), resources, buildings, techs);
        var ufo = LoadUfo(Path.Combine(dataDirectory, "ufo.json"));
        var ambience = LoadAmbience(Path.Combine(dataDirectory, "ambience.json"), biomes, weather);
        var terraform = LoadTerraform(Path.Combine(dataDirectory, "terraform.json"), biomes, resources, techs);
        var milestones = LoadMilestones(Path.Combine(dataDirectory, "milestones.json"), resources, buildings, techs);
        var elections = LoadElections(Path.Combine(dataDirectory, "elections.json"));
        var challenges = LoadChallenges(Path.Combine(dataDirectory, "challenges.json"), resources, buildings, techs);
        var contracts = LoadContracts(Path.Combine(dataDirectory, "contracts.json"), resources, buildings, techs);
        var districts = LoadDistricts(Path.Combine(dataDirectory, "districts.json"), buildings);
        var citizens = LoadCitizens(Path.Combine(dataDirectory, "citizens.json"), resources, buildings, techs);
        var seasons = LoadSeasons(Path.Combine(dataDirectory, "seasons.json"), resources);
        var tutorial = LoadTutorial(Path.Combine(dataDirectory, "tutorial.json"), resources, buildings, techs);
        var worldGen = LoadWorldGen(Path.Combine(dataDirectory, "worldgen.json"), biomes);
        var gameplay = LoadGameplay(Path.Combine(dataDirectory, "gameplay.json"), resources, buildings, techs);
        var devlog = LoadDevlog(Path.Combine(dataDirectory, "devlog.json"));
        var languages = LoadLanguages(Path.Combine(dataDirectory, "lang"), biomes, resources, buildings, worldGen, techs, prestigeUpgrades, legacyUpgrades, quests, achievements, events, eras, zoneTypes, policies, tiers, weather, landmarks, features, devlog, terraform, tutorial, challenges, contracts, districts, settlementRanks, citizens, elections, milestones, seasons);
        var settlementNames = LoadSettlementNames(Path.Combine(dataDirectory, "settlement-names.json"));
        var decorations = LoadDecorations(Path.Combine(dataDirectory, "decorations.json"), biomes);
        var fauna = LoadFauna(Path.Combine(dataDirectory, "fauna.json"), biomes);
        var vehicles = LoadVehicles(Path.Combine(dataDirectory, "vehicles.json"));
        var faith = LoadFaith(Path.Combine(dataDirectory, "faith.json"), resources);
        var npcCities = LoadNpcCities(Path.Combine(dataDirectory, "npc-cities.json"), resources, buildings, settlementNames);
        var grandWork = LoadGrandWork(Path.Combine(dataDirectory, "grandwork.json"), resources);

        return new GameContent(
            biomes, resources, buildings, techs, prestige, prestigeUpgrades, quests, questsDynamic, achievements, events, eras,
            worldGen, gameplay, languages, settlementNames, decorations, fauna, devlog, zoneTypes, policies, tiers, weather, landmarks, features, ufo, ambience, terraform, tutorial, challenges, contracts, districts, settlementRanks, citizens, elections, milestones, seasons, faith, npcCities, vehicles, mods,
            grandWork, legacy, legacyUpgrades);
    }

    // ----- cizí města -----

    /// <summary>
    /// Načte pravidla soužití s cizími městy. Chybějící soubor není chyba —
    /// mechanika je volitelná a hra (i starší mody) musí naběhnout bez ní.
    /// </summary>
    private NpcCityCatalog LoadNpcCities(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings,
        IReadOnlyList<string> settlementNames)
    {
        if (!File.Exists(path))
        {
            return NpcCityCatalog.Empty;
        }

        var file = ReadFile<NpcCitiesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var archetypes = new List<NpcCityArchetype>();
        foreach (var dto in file.Archetypes ?? new List<NpcArchetypeDto>())
        {
            string id = dto.Id?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                throw new ContentLoadException(path, "Druh cizího města bez 'id'.");
            }

            // Z čeho město stojí. Odkaz na neexistující budovu je chyba obsahu —
            // fail-fast při startu, ne prázdné město po hodině hraní.
            var palette = new List<int>();
            foreach (string buildingId in dto.Buildings ?? new List<string>())
            {
                if (!buildings.TryIndexOf(buildingId.Trim(), out int buildingIndex))
                {
                    throw new ContentLoadException(path,
                        $"Cizí město '{id}' staví z neexistující budovy '{buildingId}'.");
                }

                palette.Add(buildingIndex);
            }

            archetypes.Add(new NpcCityArchetype(
                id,
                ParseColor(path, dto.MapColor, $"cizí město '{id}'"),
                Math.Max(0, dto.Population),
                ParseResourceAmounts(path, id, "trade", dto.Trade, resources),
                palette));
        }

        // Jména se berou ze STEJNÉ množiny jako hráčova sídla. Vlastní seznam
        // dělal z cizích měst jiný svět; sdílená množina je drží ve stejném.
        var names = settlementNames;
        if (archetypes.Count > 0 && names.Count == 0)
        {
            throw new ContentLoadException(path, "Cizí města nemají odkud vzít jména (settlement-names.json je prázdný).");
        }

        return new NpcCityCatalog(
            ParseResourceAmounts(path, "npc", "giftCost", file.GiftCost, resources),
            Math.Max(0, file.GiftRelation),
            ParseResourceAmounts(path, "npc", "roadCost", file.RoadCost, resources),
            (int)Math.Round(file.TradeIntervalSeconds * 10),
            Math.Clamp(file.BuyRelation, 0, 100),
            ParseResourceAmounts(path, "npc", "buyCost", file.BuyCost, resources),
            Math.Max(1, file.SurroundRadius),
            Math.Max(1, file.SurroundBuildings),
            Math.Max(0, file.TradeRelation),
            Math.Max(0.0, file.CaravanBonusAtFullRelation),
            new DefRegistry<NpcCityArchetype>(archetypes, a => a.Id, "cizí město", allowEmpty: true),
            names);
    }

    // ----- víra -----

    /// <summary>
    /// Načte modlitby. Chybějící soubor <b>není chyba</b> — víra je volitelná
    /// mechanika a hra bez ní musí naběhnout (a všechny starší mody taky).
    /// </summary>
    /// <summary>
    /// Načte Velké dílo. Chybějící soubor <b>není chyba</b> — je to volitelná
    /// mechanika a hra bez ní běží dál (stejně jako víra).
    /// </summary>
    private GrandWorkConfig LoadGrandWork(string path, DefRegistry<Resource> resources)
    {
        if (!File.Exists(path))
        {
            return new GrandWorkConfig(Array.Empty<GrandWorkStage>(), 1.0, 0);
        }

        var file = ReadFile<GrandWorkFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        double growth = file.CostGrowth <= 0 ? 1.5 : file.CostGrowth;
        if (growth is < 1.0 or > 10.0)
        {
            throw new ContentLoadException(path, $"'costGrowth' musí být 1–10, je {growth}.");
        }

        var stages = new List<GrandWorkStage>();
        foreach (var dto in file.Stages ?? new List<GrandWorkStageDto>())
        {
            string effect = dto.Effect?.Trim() ?? string.Empty;
            if (effect.Length == 0)
            {
                throw new ContentLoadException(path, "Stupeň Velkého díla bez 'effect'.");
            }

            // Stupně sahají na tytéž násobiče jako Vzestup, takže i slovník je
            // společný — jinak by překlep v efektu tiše nedělal nic.
            if (!KnownPrestigeEffects.Contains(effect))
            {
                throw new ContentLoadException(
                    path, $"Stupeň Velkého díla: neznámý efekt '{effect}' (známé: {string.Join(", ", KnownPrestigeEffects)}).");
            }

            if (dto.Magnitude <= 0)
            {
                throw new ContentLoadException(path, $"Stupeň s efektem '{effect}': 'magnitude' musí být kladná.");
            }

            var cost = ParseResourceAmounts(path, effect, "cost", dto.Cost, resources);
            if (cost.Count == 0)
            {
                throw new ContentLoadException(path, $"Stupeň s efektem '{effect}' nic nestojí — sink bez ceny nic neodebere.");
            }

            stages.Add(new GrandWorkStage(cost, effect, dto.Magnitude));
        }

        return new GrandWorkConfig(stages, growth, Math.Max(0, file.UnlockAscensionLevel));
    }

    private FaithCatalog LoadFaith(string path, DefRegistry<Resource> resources)
    {
        if (!File.Exists(path))
        {
            return FaithCatalog.Empty;
        }

        var file = ReadFile<FaithFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (string.IsNullOrWhiteSpace(file.FaithResource)
            || !resources.TryIndexOf(file.FaithResource.Trim(), out int faithIndex))
        {
            throw new ContentLoadException(
                path, $"'faithResource' odkazuje na neexistující surovinu '{file.FaithResource}'.");
        }

        var prayers = new List<PrayerDef>();
        foreach (var dto in file.Prayers ?? new List<PrayerDto>())
        {
            string id = dto.Id?.Trim() ?? string.Empty;
            if (id.Length == 0)
            {
                throw new ContentLoadException(path, "Modlitba bez 'id'.");
            }

            if (dto.BaseChance is <= 0 or > 1)
            {
                throw new ContentLoadException(path, $"Modlitba '{id}': 'baseChance' musí být 0–1, je {dto.BaseChance}.");
            }

            if (dto.BaseCost <= 0)
            {
                throw new ContentLoadException(path, $"Modlitba '{id}': 'baseCost' musí být kladný.");
            }

            // Účinek se NEvaliduje proti seznamu — behavior-ID hook, data smí
            // předběhnout kód (neznámý účinek = nevyslyšená modlitba).
            prayers.Add(new PrayerDef(
                id, dto.Effect?.Trim() ?? string.Empty, dto.BaseCost, dto.BaseChance,
                dto.ChanceFalloff, dto.Magnitude, Math.Max(0, dto.Radius)));
        }

        return new FaithCatalog(
            faithIndex, new DefRegistry<PrayerDef>(prayers, p => p.Id, "modlitba", allowEmpty: true));
    }

    // ----- roční období -----

    /// <summary>
    /// Načte kalendář ročních období. Volitelný soubor — bez něj hra běží
    /// v jednom nekonečném létě jako dřív.
    /// </summary>
    private SeasonCalendar LoadSeasons(string path, DefRegistry<Resource> resources)
    {
        if (!File.Exists(path))
        {
            return SeasonCalendar.Disabled;
        }

        var file = ReadFile<SeasonsFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Seasons ?? new List<SeasonDto>();
        if (dtos.Count == 0)
        {
            return SeasonCalendar.Disabled;
        }

        if (file.DaysPerSeason <= 0)
        {
            throw new ContentLoadException(path, $"'daysPerSeason' musí být kladný, je {file.DaysPerSeason}.");
        }

        // Palivo je povinné, jen když se v některém období topí — jinak by data
        // slibovala mechaniku, kterou nemají čím zaplatit.
        int fuelIndex = -1;
        if (!string.IsNullOrWhiteSpace(file.FuelResource))
        {
            if (!resources.TryIndexOf(file.FuelResource.Trim(), out fuelIndex))
            {
                throw new ContentLoadException(path, $"'fuelResource' odkazuje na neexistující surovinu '{file.FuelResource}'.");
            }
        }

        var seasons = new List<SeasonDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Období na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID období '{id}'.");
            }

            CheckSeasonMultiplier(path, id, "foodProductionMult", dto.FoodProductionMult);
            CheckSeasonMultiplier(path, id, "harvestMult", dto.HarvestMult);
            CheckSeasonMultiplier(path, id, "growthMult", dto.GrowthMult);
            CheckSeasonMultiplier(path, id, "coldGrowthMult", dto.ColdGrowthMult);

            if (dto.FuelPerPersonPerSecond < 0)
            {
                throw new ContentLoadException(path, $"Období '{id}': 'fuelPerPersonPerSecond' nesmí být záporná.");
            }

            if (dto.FuelPerPersonPerSecond > 0 && fuelIndex < 0)
            {
                throw new ContentLoadException(path,
                    $"Období '{id}' topí, ale soubor nemá 'fuelResource' — není čím.");
            }

            if (dto.TintAlpha is < 0 or > 1)
            {
                throw new ContentLoadException(path, $"Období '{id}': 'tintAlpha' musí být 0–1, je {dto.TintAlpha}.");
            }

            var tint = dto.TintAlpha > 0
                ? ParseColor(path, dto.TintColor, $"Období '{id}' ('tintColor')")
                : new RgbColor(0, 0, 0);

            seasons.Add(new SeasonDef(
                id, tint, dto.TintAlpha,
                dto.FoodProductionMult, dto.HarvestMult, dto.GrowthMult,
                dto.FuelPerPersonPerSecond, dto.ColdGrowthMult));
        }

        return new SeasonCalendar(seasons, file.DaysPerSeason, fuelIndex);
    }

    /// <summary>
    /// Násobič období musí být kladný. Nula by znamenala „úplně vypnuto", což je
    /// tvrdý trest — a ten hra zásadně nedělá (soft pressure).
    /// </summary>
    private static void CheckSeasonMultiplier(string path, string id, string field, double value)
    {
        if (value is <= 0 or > 10)
        {
            throw new ContentLoadException(path, $"Období '{id}': '{field}' musí být větší než 0 a nejvýš 10, je {value}.");
        }
    }

    // ----- milníky -----

    /// <summary>Načte milníky postupu. Volitelný soubor — bez něj se nic neslaví.</summary>
    private IReadOnlyList<MilestoneDef> LoadMilestones(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<MilestoneDef>();
        }

        var file = ReadFile<MilestoneFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Milestones ?? new List<MilestoneDto>();
        var result = new List<MilestoneDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Milník na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID milníku '{id}'.");
            }

            if (dto.Condition is null)
            {
                throw new ContentLoadException(path, $"Milník '{id}' nemá 'condition'.");
            }

            result.Add(new MilestoneDef(id, ParseCondition(path, $"milník '{id}'", dto.Condition, resources, buildings, techs)));
        }

        return result;
    }

    // ----- volby -----

    /// <summary>
    /// Načte volební programy. Volitelný soubor — bez něj hra běží bez voleb.
    /// </summary>
    private ElectionConfig LoadElections(string path)
    {
        if (!File.Exists(path))
        {
            return ElectionConfig.Disabled;
        }

        var file = ReadFile<ElectionFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Candidates ?? new List<ElectionCandidateDto>();
        var result = new List<ElectionCandidateDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Volební program na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID volebního programu '{id}'.");
            }

            if (dto.Magnitude is <= 0 or > 10)
            {
                throw new ContentLoadException(path, $"Program '{id}': 'magnitude' musí být 0–10, je {dto.Magnitude}.");
            }

            result.Add(new ElectionCandidateDef(id, ParseElectionEffect(path, id, dto.Effect), dto.Magnitude));
        }

        if (result.Count == 0)
        {
            return ElectionConfig.Disabled;
        }

        if (file.TermDays < 1)
        {
            throw new ContentLoadException(path, $"'termDays' musí být aspoň 1, je {file.TermDays}.");
        }

        if (file.BallotSize < 2 || file.BallotSize > result.Count)
        {
            throw new ContentLoadException(
                path, $"'ballotSize' musí být 2–{result.Count} (počet programů), je {file.BallotSize}.");
        }

        return new ElectionConfig(result, file.TermDays, file.BallotSize);
    }

    private static ElectionEffect ParseElectionEffect(string path, string id, string? effect) =>
        (effect ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "production" => ElectionEffect.Production,
            "growth" => ElectionEffect.Growth,
            "harvest" => ElectionEffect.Harvest,
            "research" => ElectionEffect.Research,
            "happiness" => ElectionEffect.Happiness,
            _ => throw new ContentLoadException(path, $"Program '{id}': neznámý 'effect' '{effect}'."),
        };

    // ----- denní výzvy -----

    /// <summary>
    /// Načte fond denních výzev. Volitelný soubor — bez něj hra běží bez výzev.
    /// </summary>
    /// <summary>
    /// Pojmenovaní obyvatelé a jejich prosby. Soubor je volitelný — bez něj se
    /// nikdo neozve a hraje se jako dřív.
    /// </summary>
    private CitizenCatalog LoadCitizens(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        if (!File.Exists(path))
        {
            return CitizenCatalog.Empty;
        }

        var file = ReadFile<CitizensFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var firstNames = file.FirstNames ?? new List<string>();
        var surnames = file.Surnames ?? new List<string>();
        var dtos = file.Requests ?? new List<CitizenRequestDto>();
        if (dtos.Count == 0)
        {
            return CitizenCatalog.Empty;
        }

        // Prosby bez jmen by byly anonymní — a to je přesně to, co tahle
        // mechanika měla odstranit.
        if (firstNames.Count == 0 || surnames.Count == 0)
        {
            throw new ContentLoadException(path,
                "Obyvatelé mají prosby, ale chybí jim jména ('firstNames' nebo 'surnames').");
        }

        if (file.GapSeconds < 5)
        {
            throw new ContentLoadException(path,
                $"'gapSeconds' musí být aspoň 5, je {file.GapSeconds} — jinak by se lidé ozývali bez přestání.");
        }

        var result = new List<CitizenRequestDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Prosba obyvatele na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID prosby '{id}'.");
            }

            if (!buildings.TryIndexOf(dto.Building ?? string.Empty, out int buildingIndex))
            {
                throw new ContentLoadException(path,
                    $"Prosba '{id}' chce budovu '{dto.Building}', která neexistuje.");
            }

            var cost = ParseResourceAmounts(path, id, "cost", dto.Cost, resources);
            if (cost.Count == 0)
            {
                throw new ContentLoadException(path,
                    $"Prosba '{id}' nic nestojí — pak to není prosba, ale dárek.");
            }

            if (dto.DurationSeconds < 10 || dto.DurationSeconds > 3600)
            {
                throw new ContentLoadException(path,
                    $"Prosba '{id}': 'durationSeconds' musí být 10–3600, je {dto.DurationSeconds}.");
            }

            var requirement = dto.Requires is null
                ? (GoalCondition?)null
                : ParseCondition(path, $"prosba '{id}'", dto.Requires, resources, buildings, techs);

            result.Add(new CitizenRequestDef(id, buildingIndex, cost, dto.DurationSeconds, requirement));
        }

        return new CitizenCatalog(
            firstNames, surnames,
            new DefRegistry<CitizenRequestDef>(result, r => r.Id, "prosba obyvatele"),
            file.GapSeconds);
    }

    /// <summary>
    /// Stupně sídel. Soubor je volitelný — bez něj sídla stupně nemají a hraje
    /// se jako dřív. Pořadí v souboru je pořadím hierarchie, takže se hlídá, že
    /// prahy rostou: sestupný žebříček by tiše znamenal, že vyšší stupeň nikdy
    /// nenastane.
    /// </summary>
    private SettlementRankLadder LoadSettlementRanks(string path)
    {
        if (!File.Exists(path))
        {
            return SettlementRankLadder.Empty;
        }

        var file = ReadFile<SettlementRanksFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Ranks ?? new List<SettlementRankDto>();
        if (dtos.Count == 0)
        {
            return SettlementRankLadder.Empty;
        }

        var result = new List<SettlementRankDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        int previousThreshold = 0;
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Stupeň sídla na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID stupně sídla '{id}'.");
            }

            if (dto.MinBuildings < 1)
            {
                throw new ContentLoadException(path,
                    $"Stupeň '{id}': 'minBuildings' musí být aspoň 1, je {dto.MinBuildings}.");
            }

            if (i > 0 && dto.MinBuildings <= previousThreshold)
            {
                throw new ContentLoadException(path,
                    $"Stupeň '{id}' má práh {dto.MinBuildings}, což není víc než předchozí ({previousThreshold}) — " +
                    "žebříček musí růst, jinak se na vyšší stupeň nikdy nedojde.");
            }

            previousThreshold = dto.MinBuildings;
            result.Add(new SettlementRankDef(id, dto.MinBuildings));
        }

        return new SettlementRankLadder(result);
    }

    /// <summary>
    /// Druhy čtvrtí. Soubor je volitelný — bez něj se shluky budov nijak
    /// nerozpoznávají a hraje se jako dřív.
    /// </summary>
    private DistrictCatalog LoadDistricts(string path, DefRegistry<BuildingDef> buildings)
    {
        if (!File.Exists(path))
        {
            return DistrictCatalog.Empty;
        }

        var file = ReadFile<DistrictsFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Districts ?? new List<DistrictTypeDto>();
        if (dtos.Count == 0)
        {
            return DistrictCatalog.Empty;
        }

        // Kategorie nejsou samostatná data, jsou to řetězce na budovách. Ověřit,
        // že aspoň jedna budova takovou kategorii má, je jediný způsob, jak
        // odhalit překlep dřív, než se čtvrť tiše nikdy nevytvoří.
        var knownCategories = new HashSet<string>(buildings.All.Select(b => b.Category), StringComparer.Ordinal);

        var result = new List<DistrictTypeDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Čtvrť na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID čtvrti '{id}'.");
            }

            var categories = dto.Categories ?? Array.Empty<string>();
            if (categories.Length == 0)
            {
                throw new ContentLoadException(path, $"Čtvrť '{id}' nemá 'categories' — nemá z čeho vzniknout.");
            }

            foreach (string category in categories)
            {
                if (!knownCategories.Contains(category))
                {
                    throw new ContentLoadException(path,
                        $"Čtvrť '{id}' čeká kategorii '{category}', kterou nemá žádná budova.");
                }
            }

            // Čtvrť o jedné budově není čtvrť; o padesáti se nikdy nesejde.
            if (dto.MinBuildings is < 2 or > 50)
            {
                throw new ContentLoadException(path,
                    $"Čtvrť '{id}': 'minBuildings' musí být 2–50, je {dto.MinBuildings}.");
            }

            if (dto.ClusterDistance is < 1 or > 20)
            {
                throw new ContentLoadException(path,
                    $"Čtvrť '{id}': 'clusterDistance' musí být 1–20, je {dto.ClusterDistance}.");
            }

            if (dto.SynergyPerBuilding < 0 || dto.SynergyMax < 0)
            {
                throw new ContentLoadException(path,
                    $"Čtvrť '{id}': synergie nesmí být záporná — čtvrť má odměňovat, ne trestat.");
            }

            // Bonus bez stropu se dá škálovat donekonečna jedním obřím blokem.
            if (dto.SynergyMax > 5)
            {
                throw new ContentLoadException(path,
                    $"Čtvrť '{id}': 'synergyMax' musí být nejvýš 5, je {dto.SynergyMax}.");
            }

            if (dto.PollutionMult is < 1 or > 5)
            {
                throw new ContentLoadException(path,
                    $"Čtvrť '{id}': 'pollutionMult' musí být 1–5, je {dto.PollutionMult}.");
            }

            var color = ParseColor(path, dto.MapColor, $"čtvrť '{id}'");
            result.Add(new DistrictTypeDef(
                id, categories, dto.MinBuildings, dto.ClusterDistance,
                dto.SynergyPerBuilding, dto.SynergyMax, dto.PollutionMult, color));
        }

        return new DistrictCatalog(new DefRegistry<DistrictTypeDef>(result, d => d.Id, "druh čtvrti"));
    }

    /// <summary>
    /// Nástěnka zakázek. Soubor je volitelný — bez něj se hraje jako dřív, jen
    /// bez krátkých objednávek.
    /// </summary>
    private ContractCatalog LoadContracts(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        if (!File.Exists(path))
        {
            return ContractCatalog.Empty;
        }

        var file = ReadFile<ContractsFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Contracts ?? new List<ContractDto>();
        var result = new List<ContractDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Zakázka na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID zakázky '{id}'.");
            }

            if (!resources.TryIndexOf(dto.Resource ?? string.Empty, out int demandIndex))
            {
                throw new ContentLoadException(path,
                    $"Zakázka '{id}' chce surovinu '{dto.Resource}', která neexistuje.");
            }

            if (dto.Amount < 1)
            {
                throw new ContentLoadException(path,
                    $"Zakázka '{id}': 'amount' musí být aspoň 1, je {dto.Amount}.");
            }

            var reward = ParseResourceAmounts(path, id, "reward", dto.Reward, resources);
            if (reward.Count == 0)
            {
                throw new ContentLoadException(path, $"Zakázka '{id}' nemá odměnu — pak nemá proč existovat.");
            }

            // Zakázka placená tím, co si objednala, je jen složitý způsob, jak
            // nedat nic. Tohle je tichá chyba obsahu, ne „skoro správně".
            if (reward.Count == 1 && reward[0].ResourceIndex == demandIndex)
            {
                throw new ContentLoadException(path,
                    $"Zakázka '{id}' platí toutéž surovinou, kterou chce — to hráči nic nedá.");
            }

            if (dto.DurationSeconds < 5 || dto.DurationSeconds > 3600)
            {
                throw new ContentLoadException(path,
                    $"Zakázka '{id}': 'durationSeconds' musí být 5–3600, je {dto.DurationSeconds}.");
            }

            var requirement = dto.Requires is null
                ? (GoalCondition?)null
                : ParseCondition(path, $"zakázka '{id}'", dto.Requires, resources, buildings, techs);

            result.Add(new ContractDef(id, demandIndex, dto.Amount, reward, dto.DurationSeconds, requirement));
        }

        var boardDto = file.Board;
        if (result.Count == 0 || boardDto is null)
        {
            return ContractCatalog.Empty;
        }

        if (boardDto.Slots is < 1 or > 12)
        {
            throw new ContentLoadException(path, $"'board.slots' musí být 1–12, je {boardDto.Slots}.");
        }

        if (boardDto.RestockSeconds < 1)
        {
            throw new ContentLoadException(path,
                $"'board.restockSeconds' musí být aspoň 1, je {boardDto.RestockSeconds}.");
        }

        // Růst pod 1 by nabídky s hraním zmenšoval; nad 1.5 by za deset zakázek
        // vyletěly do nesmyslů. Obojí je překlep, ne záměr.
        if (boardDto.ScaleGrowth is < 1.0 or > 1.5)
        {
            throw new ContentLoadException(path,
                $"'board.scaleGrowth' musí být 1.0–1.5, je {boardDto.ScaleGrowth}.");
        }

        if (boardDto.MaxScale < 1.0)
        {
            throw new ContentLoadException(path, $"'board.maxScale' musí být aspoň 1, je {boardDto.MaxScale}.");
        }

        var board = new ContractBoardConfig(
            boardDto.Slots, boardDto.RestockSeconds, boardDto.ScaleGrowth, boardDto.MaxScale);
        return new ContractCatalog(board, new DefRegistry<ContractDef>(result, c => c.Id, "zakázka"));
    }

    private ChallengeCatalog LoadChallenges(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        if (!File.Exists(path))
        {
            return ChallengeCatalog.Empty;
        }

        var file = ReadFile<ChallengeFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Challenges ?? new List<ChallengeDto>();
        var result = new List<ChallengeDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Výzva na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID výzvy '{id}'.");
            }

            if (dto.Condition is null)
            {
                throw new ContentLoadException(path, $"Výzva '{id}' nemá 'condition'.");
            }

            var condition = ParseCondition(path, $"výzva '{id}'", dto.Condition, resources, buildings, techs);
            var reward = ParseResourceAmounts(path, id, "reward", dto.Reward, resources);
            if (reward.Count == 0)
            {
                throw new ContentLoadException(path, $"Výzva '{id}' nemá odměnu — pak nemá proč existovat.");
            }

            result.Add(new ChallengeDef(id, condition, reward));
        }

        if (result.Count > 0 && file.DailyCount is < 1)
        {
            throw new ContentLoadException(path, $"'dailyCount' musí být aspoň 1, je {file.DailyCount}.");
        }

        if (file.DailyCount > result.Count)
        {
            throw new ContentLoadException(
                path, $"'dailyCount' ({file.DailyCount}) je víc než výzev ve fondu ({result.Count}).");
        }

        return new ChallengeCatalog(result, file.DailyCount);
    }

    // ----- průvodce prvními kroky -----

    /// <summary>
    /// Načte kroky průvodce. Pořadí v souboru JE pořadí kroků (v savu se drží
    /// index), takže se nesmí přehazovat — proto se validuje jen unikátnost ID
    /// a to, že cíl „ukaž mi" existuje.
    /// </summary>
    private IReadOnlyList<TutorialStepDef> LoadTutorial(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        // Průvodce je volitelný — bez souboru se hra prostě spustí bez vedení.
        if (!File.Exists(path))
        {
            return Array.Empty<TutorialStepDef>();
        }

        var file = ReadFile<TutorialFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Steps ?? new List<TutorialStepDto>();
        var result = new List<TutorialStepDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Krok průvodce na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID kroku průvodce '{id}'.");
            }

            if (dto.Condition is null)
            {
                throw new ContentLoadException(path, $"Krok průvodce '{id}' nemá 'condition' (kdy je hotový).");
            }

            var condition = ParseCondition(path, $"krok průvodce '{id}'", dto.Condition, resources, buildings, techs);
            result.Add(new TutorialStepDef(id, condition, ParseFocus(path, id, dto.Focus, buildings)));
        }

        return result;
    }

    private static FocusHint ParseFocus(string path, string ownerId, FocusHintDto? dto, DefRegistry<BuildingDef> buildings)
    {
        if (dto is null)
        {
            return FocusHint.None;
        }

        string target = dto.Target?.Trim() ?? string.Empty;
        switch ((dto.Kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "":
            case "none":
                return FocusHint.None;

            case "map":
                return new FocusHint(FocusKind.Map, -1, string.Empty);

            case "build":
                if (dto.Building is null || !buildings.TryIndexOf(dto.Building.Trim(), out int buildingIndex))
                {
                    throw new ContentLoadException(path, $"Krok průvodce '{ownerId}': 'focus.building' odkazuje na neexistující budovu '{dto.Building}'.");
                }

                return new FocusHint(FocusKind.Build, buildingIndex, string.Empty);

            case "tool":
            case "screen":
                if (target.Length == 0)
                {
                    throw new ContentLoadException(path, $"Krok průvodce '{ownerId}': 'focus.target' musí říct, co otevřít.");
                }

                var kind = dto.Kind!.Trim().Equals("tool", StringComparison.OrdinalIgnoreCase) ? FocusKind.Tool : FocusKind.Screen;
                return new FocusHint(kind, -1, target);

            default:
                throw new ContentLoadException(path, $"Krok průvodce '{ownerId}': neznámý 'focus.kind' '{dto.Kind}'.");
        }
    }

    // ----- éry -----

    private DefRegistry<EraDef> LoadEras(string path)
    {
        var file = ReadFile<ErasFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Eras ?? new List<EraDto>();
        var result = new List<EraDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenOrders = new HashSet<int>();
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Éra na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID éry '{id}'.");
            }

            if (dto.Order is < 0 or > 100)
            {
                throw new ContentLoadException(path, $"Éra '{id}': 'order' musí být 0–100, je {dto.Order}.");
            }

            if (!seenOrders.Add(dto.Order))
            {
                throw new ContentLoadException(path, $"Éra '{id}': pořadí {dto.Order} už má jiná éra.");
            }

            // UnlockTech se ZÁMĚRNĚ nevaliduje (řeší se za běhu) — jde tak éry
            // definovat dřív, než jejich technologie vzniknou.
            result.Add(new EraDef(id, dto.Order, dto.UnlockTech?.Trim() ?? string.Empty));
        }

        return new DefRegistry<EraDef>(result, e => e.Id, "éra", allowEmpty: true);
    }

    // ----- zóny (automatizace) -----

    private DefRegistry<ZoneTypeDef> LoadZoneTypes(string path, DefRegistry<BuildingDef> buildings)
    {
        // Zóny jsou volitelný obsah — bez souboru je registr prázdný (žádná automatizace zón).
        if (!File.Exists(path))
        {
            return new DefRegistry<ZoneTypeDef>(Array.Empty<ZoneTypeDef>(), z => z.Id, "typ zóny", allowEmpty: true);
        }

        var file = ReadFile<ZonesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Zones ?? new List<ZoneTypeDto>();
        var result = new List<ZoneTypeDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Typ zóny na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID typu zóny '{id}'.");
            }

            var color = ParseColor(path, dto.MapColor, $"Zóna '{id}'");

            if (dto.Buildings is not { Count: > 0 })
            {
                throw new ContentLoadException(path, $"Zóna '{id}' nemá vyplněný seznam 'buildings' (čím se zaplňuje).");
            }

            var buildingIndices = new List<int>(dto.Buildings.Count);
            foreach (var buildingId in dto.Buildings)
            {
                if (buildingId is null || !buildings.TryIndexOf(buildingId.Trim(), out int buildingIndex))
                {
                    throw new ContentLoadException(path, $"Zóna '{id}' odkazuje v 'buildings' na neexistující budovu '{buildingId}'.");
                }

                buildingIndices.Add(buildingIndex);
            }

            result.Add(new ZoneTypeDef(id, color, buildingIndices));
        }

        return new DefRegistry<ZoneTypeDef>(result, z => z.Id, "typ zóny", allowEmpty: true);
    }

    private DefRegistry<GrowthPolicyDef> LoadPolicies(string path)
    {
        // Politiky jsou volitelný obsah — bez souboru je registr prázdný (žádná stupeň-4 automatizace).
        if (!File.Exists(path))
        {
            return new DefRegistry<GrowthPolicyDef>(Array.Empty<GrowthPolicyDef>(), p => p.Id, "politika", allowEmpty: true);
        }

        var file = ReadFile<PoliciesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Policies ?? new List<PolicyDto>();
        var result = new List<GrowthPolicyDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Politika na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID politiky '{id}'.");
            }

            if (string.IsNullOrWhiteSpace(dto.Effect))
            {
                throw new ContentLoadException(path, $"Politika '{id}' nemá vyplněný 'effect' (behavior-ID).");
            }

            // Efekt se ZÁMĚRNĚ nevaliduje proti seznamu — neznámý se za běhu tiše ignoruje
            // (data smí předběhnout kód, konzistentní s behavior-ID hooky).
            result.Add(new GrowthPolicyDef(id, dto.Effect.Trim(), dto.Magnitude));
        }

        return new DefRegistry<GrowthPolicyDef>(result, p => p.Id, "politika", allowEmpty: true);
    }

    // ----- odemykatelné funkce -----

    private DefRegistry<FeatureDef> LoadFeatures(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        // Volitelný obsah — bez souboru je vše dostupné od začátku (žádné gatování).
        if (!File.Exists(path))
        {
            return new DefRegistry<FeatureDef>(Array.Empty<FeatureDef>(), f => f.Id, "funkce", allowEmpty: true);
        }

        var file = ReadFile<FeaturesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Features ?? new List<FeatureDto>();
        var result = new List<FeatureDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Funkce na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID funkce '{id}'.");
            }

            if (dto.Unlock is null)
            {
                throw new ContentLoadException(path, $"Funkce '{id}' nemá vyplněnou podmínku 'unlock'.");
            }

            var condition = ParseCondition(path, $"Funkce '{id}'", dto.Unlock, resources, buildings, techs);
            result.Add(new FeatureDef(id, condition));
        }

        return new DefRegistry<FeatureDef>(result, f => f.Id, "funkce", allowEmpty: true);
    }

    // ----- přetváření krajiny -----

    private DefRegistry<TerraformDef> LoadTerraform(
        string path, BiomeRegistry biomes, DefRegistry<Resource> resources, DefRegistry<TechDef> techs)
    {
        // Volitelný obsah — bez souboru se krajina prostě přetvářet nedá.
        if (!File.Exists(path))
        {
            return new DefRegistry<TerraformDef>(Array.Empty<TerraformDef>(), t => t.Id, "terraformace", allowEmpty: true);
        }

        var file = ReadFile<TerraformFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var result = new List<TerraformDef>();
        foreach (var (dto, i) in (file.Terraform ?? new List<TerraformDto>()).Select((d, i) => (d, i)))
        {
            string id = RequireId(path, dto.Id, $"Terraformace na pozici {i}");
            if (dto.To is null || !biomes.TryIndexOf(dto.To.Trim(), out int target))
            {
                throw new ContentLoadException(path, $"Terraformace '{id}' odkazuje na neexistující biom '{dto.To}' v 'to'.");
            }

            if (biomes[target].IsWater)
            {
                throw new ContentLoadException(path, $"Terraformace '{id}': cílem nesmí být vodní biom — utopila by město.");
            }

            var sources = new List<int>();
            foreach (string? from in dto.From ?? Array.Empty<string>())
            {
                if (from is null || !biomes.TryIndexOf(from.Trim(), out int source))
                {
                    throw new ContentLoadException(path, $"Terraformace '{id}' odkazuje na neexistující biom '{from}' ve 'from'.");
                }

                sources.Add(source);
            }

            var cost = ParseResourceAmounts(path, id, "cost", dto.Cost, resources);
            if (cost.Count == 0)
            {
                throw new ContentLoadException(path, $"Terraformace '{id}' musí něco stát — zadarmo by přetvořila celý svět.");
            }

            int unlockTech = -1;
            if (!string.IsNullOrWhiteSpace(dto.UnlockTech) && !techs.TryIndexOf(dto.UnlockTech.Trim(), out unlockTech))
            {
                throw new ContentLoadException(path, $"Terraformace '{id}' odkazuje na neexistující technologii '{dto.UnlockTech}'.");
            }

            result.Add(new TerraformDef(id, target, sources, cost, unlockTech));
        }

        return new DefRegistry<TerraformDef>(result, t => t.Id, "terraformace", allowEmpty: true);
    }

    // ----- ambientní kulisa -----

    private IReadOnlyList<AmbienceDef> LoadAmbience(
        string path, BiomeRegistry biomes, DefRegistry<WeatherDef> weather)
    {
        // Volitelný obsah — bez souboru hraje jen hudba, hra běží dál.
        if (!File.Exists(path))
        {
            return Array.Empty<AmbienceDef>();
        }

        var file = ReadFile<AmbienceFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var result = new List<AmbienceDef>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (dto, i) in (file.Ambience ?? new List<AmbienceDto>()).Select((a, i) => (a, i)))
        {
            string id = RequireId(path, dto.Id, $"Kulisa na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID kulisy '{id}'.");
            }

            var biomeIndices = ResolveIndices(path, id, "biomes", dto.Biomes, biomes, "biom");
            var weatherIndices = ResolveIndices(path, id, "weather", dto.Weather, weather, "počasí");

            if (dto.NoiseLevel is < 0 or > 1 || dto.ToneLevel is < 0 or > 1 || dto.Volume is < 0 or > 1)
            {
                throw new ContentLoadException(path, $"Kulisa '{id}': 'noiseLevel', 'toneLevel' i 'volume' musí být 0–1.");
            }

            if (dto.ToneHz is < 0 or > 20000 || dto.PulseHz is < 0 or > 20)
            {
                throw new ContentLoadException(path, $"Kulisa '{id}': 'toneHz' musí být 0–20000 a 'pulseHz' 0–20.");
            }

            result.Add(new AmbienceDef(
                id, biomeIndices, weatherIndices,
                dto.NoiseLevel, dto.ToneHz, dto.ToneLevel, dto.PulseHz, dto.Volume));
        }

        return result;
    }

    /// <summary>Přeloží seznam ID na indexy registru; prázdný seznam = bez omezení.</summary>
    private static IReadOnlyList<int> ResolveIndices<T>(
        string path, string owner, string field, string[]? ids, DefRegistry<T> registry, string kind)
        where T : class
    {
        if (ids is null || ids.Length == 0)
        {
            return Array.Empty<int>();
        }

        var indices = new List<int>(ids.Length);
        foreach (string? id in ids)
        {
            if (id is null || !registry.TryIndexOf(id.Trim(), out int index))
            {
                throw new ContentLoadException(path, $"Kulisa '{owner}' odkazuje v '{field}' na neexistující {kind} '{id}'.");
            }

            indices.Add(index);
        }

        return indices;
    }

    // ----- UFO (živá mapa) -----

    /// <summary>Behavior-ID zásahů UFO — překlep v datech spadne hned při startu.</summary>
    private static readonly HashSet<string> KnownUfoBehaviors = new(StringComparer.Ordinal)
    {
        "abduct", "demolish", "plant", "terraform", "gift", "none",
    };

    private UfoConfig LoadUfo(string path)
    {
        // Volitelný obsah — bez souboru UFO ve hře prostě není.
        if (!File.Exists(path))
        {
            return UfoConfig.Disabled;
        }

        var file = ReadFile<UfoFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);
        if (file.Ufo is null)
        {
            return UfoConfig.Disabled;
        }

        var dto = file.Ufo;
        if (dto.WindowSeconds is < 1 or > 86400)
        {
            throw new ContentLoadException(path, $"'ufo.windowSeconds' musí být 1–86400, je {dto.WindowSeconds}.");
        }

        if (dto.Chance is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'ufo.chance' musí být 0–1, je {dto.Chance}.");
        }

        if (dto.VisitSeconds < 0 || dto.VisitSeconds > dto.WindowSeconds)
        {
            throw new ContentLoadException(path, "'ufo.visitSeconds' musí být 0 až 'windowSeconds' (návštěva se nesmí překrývat s další).");
        }

        if (dto.Radius is < 0 or > 10000)
        {
            throw new ContentLoadException(path, $"'ufo.radius' musí být 0–10000, je {dto.Radius}.");
        }

        var actions = new List<UfoActionDef>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (action, i) in (dto.Actions ?? new List<UfoActionDto>()).Select((a, i) => (a, i)))
        {
            string id = RequireId(path, action.Id, $"Zásah UFO na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID zásahu UFO '{id}'.");
            }

            string behavior = (action.Behavior ?? string.Empty).Trim();
            if (!KnownUfoBehaviors.Contains(behavior))
            {
                throw new ContentLoadException(path,
                    $"Zásah UFO '{id}': neznámé chování '{behavior}' (známé: {string.Join(", ", KnownUfoBehaviors)}).");
            }

            if (action.Weight <= 0)
            {
                throw new ContentLoadException(path, $"Zásah UFO '{id}': 'weight' musí být kladná, je {action.Weight}.");
            }

            actions.Add(new UfoActionDef(id, behavior, action.Weight, action.Magnitude));
        }

        return new UfoConfig(dto.WindowSeconds, dto.Chance, dto.VisitSeconds, dto.Radius, actions);
    }

    // ----- landmarky (živá mapa) -----

    private DefRegistry<LandmarkDef> LoadLandmarks(string path, BiomeRegistry biomes, DefRegistry<Resource> resources)
    {
        // Landmarky jsou volitelný obsah — bez souboru je mapa jen bez bodů zájmu.
        if (!File.Exists(path))
        {
            return new DefRegistry<LandmarkDef>(Array.Empty<LandmarkDef>(), l => l.Id, "landmark", allowEmpty: true);
        }

        var file = ReadFile<LandmarksFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Landmarks ?? new List<LandmarkDto>();
        var result = new List<LandmarkDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Landmark na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID landmarku '{id}'.");
            }

            if (dto.Biomes is not { Length: > 0 })
            {
                throw new ContentLoadException(path, $"Landmark '{id}' nemá vyplněné 'biomes'.");
            }

            var mask = new bool[biomes.Count];
            foreach (var biomeId in dto.Biomes)
            {
                if (biomeId is null || !biomes.TryIndexOf(biomeId.Trim(), out int biomeIndex))
                {
                    throw new ContentLoadException(path, $"Landmark '{id}' odkazuje na neexistující biom '{biomeId}'.");
                }

                mask[biomeIndex] = true;
            }

            if (dto.Rarity is < 1 or > 1_000_000)
            {
                throw new ContentLoadException(path, $"Landmark '{id}': 'rarity' musí být 1–1000000, je {dto.Rarity}.");
            }

            if (dto.Size is < 1 or > 64)
            {
                throw new ContentLoadException(path, $"Landmark '{id}': 'size' musí být 1–64, je {dto.Size}.");
            }

            var color = ParseColor(path, dto.MapColor, $"Landmark '{id}'");
            ClickYield? yield = null;
            if (dto.ClickYield is { } cy)
            {
                if (cy.Resource is null || !resources.TryIndexOf(cy.Resource.Trim(), out int resourceIndex))
                {
                    throw new ContentLoadException(path, $"Landmark '{id}' odkazuje na neexistující surovinu '{cy.Resource}'.");
                }

                if (cy.Amount < 1)
                {
                    throw new ContentLoadException(path, $"Landmark '{id}': 'clickYield.amount' musí být kladný.");
                }

                yield = new ClickYield(resourceIndex, cy.Amount);
            }

            // Půdorys 0 v datech znamená „nezadáno" → jedna dlaždice. Větší
            // landmark (vrak, ruiny) tak jde nafouknout bez zásahu do kódu.
            int footprint = dto.Footprint <= 0 ? 1 : dto.Footprint;
            if (footprint > 3)
            {
                throw new ContentLoadException(path, $"Landmark '{id}': 'footprint' smí být 1–3, je {footprint}.");
            }

            result.Add(new LandmarkDef(
                id, mask, color, dto.Size, dto.Rarity, yield, dto.Sprite?.Trim(), footprint));
        }

        return new DefRegistry<LandmarkDef>(result, l => l.Id, "landmark", allowEmpty: true);
    }

    // ----- počasí (živá mapa) -----

    private DefRegistry<WeatherDef> LoadWeather(string path, BiomeRegistry biomes)
    {
        // Počasí je volitelný obsah — bez souboru je registr prázdný (mapa bez počasí).
        if (!File.Exists(path))
        {
            return new DefRegistry<WeatherDef>(Array.Empty<WeatherDef>(), w => w.Id, "počasí", allowEmpty: true);
        }

        var file = ReadFile<WeatherFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Weather ?? new List<WeatherDto>();
        var result = new List<WeatherDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Počasí na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID počasí '{id}'.");
            }

            if (dto.Biomes is not { Length: > 0 })
            {
                throw new ContentLoadException(path, $"Počasí '{id}' nemá vyplněné 'biomes'.");
            }

            var mask = new bool[biomes.Count];
            foreach (var biomeId in dto.Biomes)
            {
                if (biomeId is null || !biomes.TryIndexOf(biomeId.Trim(), out int biomeIndex))
                {
                    throw new ContentLoadException(path, $"Počasí '{id}' odkazuje na neexistující biom '{biomeId}'.");
                }

                mask[biomeIndex] = true;
            }

            if (dto.Weight <= 0)
            {
                throw new ContentLoadException(path, $"Počasí '{id}': 'weight' musí být kladná, je {dto.Weight}.");
            }

            if (dto.DurationSeconds is <= 0 or > 3600)
            {
                throw new ContentLoadException(path, $"Počasí '{id}': 'durationSeconds' musí být 0–3600, je {dto.DurationSeconds}.");
            }

            // Výchozí 1.0 = bez vlivu; extrémní jev smí flow jen SNÍŽIT, nikdy nezničit.
            double productionMult = dto.ProductionMult <= 0 ? 1.0 : dto.ProductionMult;
            if (productionMult is < 0.1 or > 1.0)
            {
                throw new ContentLoadException(
                    path, $"Počasí '{id}': 'productionMult' musí být 0.1–1.0 (počasí flow jen zpomaluje), je {productionMult}.");
            }

            if (dto.TintAlpha is < 0 or > 1)
            {
                throw new ContentLoadException(path, $"Počasí '{id}': 'tintAlpha' musí být 0–1, je {dto.TintAlpha}.");
            }

            var tint = ParseColor(path, dto.Tint, $"Počasí '{id}'");
            result.Add(new WeatherDef(
                id, mask, dto.Extreme, productionMult, dto.DurationSeconds, dto.Weight,
                tint, dto.TintAlpha, string.IsNullOrWhiteSpace(dto.Particle) ? "none" : dto.Particle.Trim()));
        }

        return new DefRegistry<WeatherDef>(result, w => w.Id, "počasí", allowEmpty: true);
    }

    // ----- stupně měřítka (Vzestup) -----

    private DefRegistry<AscensionTierDef> LoadAscensionTiers(string path, DefRegistry<BuildingDef> buildings)
    {
        // Stupně měřítka jsou volitelný obsah — bez souboru je registr prázdný (žádný strop).
        if (!File.Exists(path))
        {
            return new DefRegistry<AscensionTierDef>(Array.Empty<AscensionTierDef>(), t => t.Id, "stupeň měřítka", allowEmpty: true);
        }

        var file = ReadFile<AscensionTiersFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Tiers ?? new List<AscensionTierDto>();
        var result = new List<AscensionTierDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenOrders = new HashSet<int>();
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Stupeň měřítka na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID stupně měřítka '{id}'.");
            }

            if (dto.Order is < 0 or > 100)
            {
                throw new ContentLoadException(path, $"Stupeň '{id}': 'order' musí být 0–100, je {dto.Order}.");
            }

            if (!seenOrders.Add(dto.Order))
            {
                throw new ContentLoadException(path, $"Stupeň '{id}': pořadí {dto.Order} už má jiný stupeň.");
            }

            if (dto.PopulationCap <= 0)
            {
                throw new ContentLoadException(path, $"Stupeň '{id}': 'populationCap' musí být kladný, je {dto.PopulationCap}.");
            }

            var unlocked = new List<int>();
            foreach (var buildingId in dto.Unlocks ?? new List<string>())
            {
                if (buildingId is null || !buildings.TryIndexOf(buildingId.Trim(), out int buildingIndex))
                {
                    throw new ContentLoadException(path, $"Stupeň '{id}' odkazuje v 'unlocks' na neexistující budovu '{buildingId}'.");
                }

                unlocked.Add(buildingIndex);
            }

            result.Add(new AscensionTierDef(id, dto.Order, dto.PopulationCap, unlocked));
        }

        return new DefRegistry<AscensionTierDef>(result, t => t.Id, "stupeň měřítka", allowEmpty: true);
    }

    // ----- biomy -----

    private BiomeRegistry LoadBiomes(string path, DefRegistry<Resource> resources)
    {
        var file = ReadFile<BiomesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Biomes is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádný biom.");
        }

        if (file.Biomes.Count > BiomeRegistry.MaxBiomes)
        {
            throw new ContentLoadException(path, $"Příliš mnoho biomů ({file.Biomes.Count}), maximum je {BiomeRegistry.MaxBiomes}.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var biomes = new List<Biome>(file.Biomes.Count);
        for (int i = 0; i < file.Biomes.Count; i++)
        {
            var biome = ValidateBiome(path, file.Biomes[i], i, resources);
            if (!seenIds.Add(biome.Id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID biomu '{biome.Id}'.");
            }

            biomes.Add(biome);
        }

        ValidateWaterDepthCoverage(path, biomes);
        return new BiomeRegistry(biomes);
    }

    private static Biome ValidateBiome(string path, BiomeDto dto, int index, DefRegistry<Resource> resources)
    {
        string id = RequireId(path, dto.Id, $"Biom na pozici {index}");
        var color = ParseColor(path, dto.MapColor, $"Biom '{id}'");

        if (dto.ColorVariation is < 0 or > 0.5)
        {
            throw new ContentLoadException(path, $"Biom '{id}': 'colorVariation' musí být v rozsahu 0–0.5, je {dto.ColorVariation}.");
        }

        ValueRange depth = ValueRange.Full;
        ValueRange elevation = ValueRange.Full;
        ValueRange moisture = ValueRange.Full;

        // Teplota platí pro vodu i pevninu — teplé moře má útesy, studené zamrzá.
        ValueRange temperature = ParseRange(path, id, "temperatureRange", dto.TemperatureRange, required: false);

        if (dto.IsWater)
        {
            depth = ParseRange(path, id, "depthRange", dto.DepthRange, required: true);
        }
        else
        {
            elevation = ParseRange(path, id, "elevationRange", dto.ElevationRange, required: true);
            moisture = ParseRange(path, id, "moistureRange", dto.MoistureRange, required: false);
        }

        ClickYield? clickYield = null;
        if (dto.ClickYield is not null)
        {
            if (dto.ClickYield.Resource is null || !resources.TryIndexOf(dto.ClickYield.Resource.Trim(), out int resourceIndex))
            {
                throw new ContentLoadException(path, $"Biom '{id}': 'clickYield' odkazuje na neexistující surovinu '{dto.ClickYield.Resource}'.");
            }

            if (dto.ClickYield.Amount is < 1 or > 1000)
            {
                throw new ContentLoadException(path, $"Biom '{id}': 'clickYield.amount' musí být 1–1000, je {dto.ClickYield.Amount}.");
            }

            if (dto.ClickYield.Charges is < 0 or > 10_000)
            {
                throw new ContentLoadException(path, $"Biom '{id}': 'clickYield.charges' musí být 0–10000, je {dto.ClickYield.Charges}.");
            }

            if (dto.ClickYield.RegrowSeconds is < 0 or > 100_000)
            {
                throw new ContentLoadException(path, $"Biom '{id}': 'clickYield.regrowSeconds' musí být 0–100000.");
            }

            // Dorůstání bez vyčerpatelnosti nedává smysl — uzel, který nezmizí,
            // nemá co dorůstat. Tichá past v datech, ne drobnost.
            if (dto.ClickYield.RegrowSeconds > 0 && dto.ClickYield.Charges <= 0)
            {
                throw new ContentLoadException(path,
                    $"Biom '{id}': 'clickYield.regrowSeconds' bez 'charges' — uzel se nevyčerpá, tak nemá co dorůstat.");
            }

            clickYield = new ClickYield(
                resourceIndex, dto.ClickYield.Amount, dto.ClickYield.Charges, dto.ClickYield.RegrowSeconds);
        }

        // Výchozí 1.0 = neutrální biom. Rozsah drží identitu biomů v rozumných mezích
        // (biom smí ekonomiku naklonit, ne rozbít).
        double productionMult = dto.ProductionMult <= 0 ? 1.0 : dto.ProductionMult;
        if (productionMult is < 0.25 or > 3.0)
        {
            throw new ContentLoadException(path, $"Biom '{id}': 'productionMult' musí být 0.25–3.0, je {productionMult}.");
        }

        return new Biome(id, color, (float)dto.ColorVariation, dto.IsWater,
            depth, elevation, moisture, temperature, clickYield, productionMult);
    }

    private static ValueRange ParseRange(string path, string biomeId, string field, double[]? values, bool required)
    {
        if (values is null)
        {
            return required
                ? throw new ContentLoadException(path, $"Biom '{biomeId}' nemá vyplněné pole '{field}'.")
                : ValueRange.Full;
        }

        if (values.Length != 2)
        {
            throw new ContentLoadException(path, $"Biom '{biomeId}': pole '{field}' musí mít přesně 2 hodnoty [min, max].");
        }

        double min = values[0], max = values[1];
        if (min < 0 || max > 1 || min > max)
        {
            throw new ContentLoadException(path, $"Biom '{biomeId}': '{field}' = [{min}, {max}] musí splňovat 0 ≤ min ≤ max ≤ 1.");
        }

        return new ValueRange((float)min, (float)max);
    }

    /// <summary>
    /// Vodní biomy musí hloubkami pokrýt celý interval 0–1, jinak by generátor pro některou
    /// hloubku neměl co vybrat. Kontroluje se tady, aby díra v datech spadla při startu.
    /// </summary>
    private static void ValidateWaterDepthCoverage(string path, List<Biome> biomes)
    {
        var water = biomes.Where(b => b.IsWater).OrderBy(b => b.DepthRange.Min).ToList();
        if (water.Count == 0)
        {
            throw new ContentLoadException(path, "Chybí vodní biom (isWater = true) — moře by nemělo jak vypadat.");
        }

        const float epsilon = 0.0001f;
        if (water[0].DepthRange.Min > epsilon)
        {
            throw new ContentLoadException(path, $"Hloubky vodních biomů nepokrývají mělčinu: nejnižší 'depthRange' začíná na {water[0].DepthRange.Min}, musí od 0.");
        }

        float covered = water[0].DepthRange.Max;
        foreach (var biome in water.Skip(1))
        {
            if (biome.DepthRange.Min > covered + epsilon)
            {
                throw new ContentLoadException(path, $"Díra v pokrytí hloubek vodních biomů mezi {covered} a {biome.DepthRange.Min} (biom '{biome.Id}').");
            }

            covered = MathF.Max(covered, biome.DepthRange.Max);
        }

        if (covered < 1f - epsilon)
        {
            throw new ContentLoadException(path, $"Hloubky vodních biomů pokrývají jen 0–{covered}, hlubina až do 1 chybí.");
        }
    }

    // ----- suroviny -----

    private DefRegistry<Resource> LoadResources(string path)
    {
        var file = ReadFile<ResourcesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Resources is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádnou surovinu.");
        }

        var resources = new List<Resource>(file.Resources.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < file.Resources.Count; i++)
        {
            var dto = file.Resources[i];
            string id = RequireId(path, dto.Id, $"Surovina na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID suroviny '{id}'.");
            }

            var color = ParseColor(path, dto.MapColor, $"Surovina '{id}'");
            if (dto.StartAmount < 0)
            {
                throw new ContentLoadException(path, $"Surovina '{id}': 'startAmount' nesmí být záporný, je {dto.StartAmount}.");
            }

            if (dto.BaseStorage <= 0)
            {
                throw new ContentLoadException(path, $"Surovina '{id}': 'baseStorage' musí být kladný, je {dto.BaseStorage}.");
            }

            if (dto.StartAmount > dto.BaseStorage)
            {
                throw new ContentLoadException(path, $"Surovina '{id}': 'startAmount' ({dto.StartAmount}) se nevejde do 'baseStorage' ({dto.BaseStorage}).");
            }

            resources.Add(new Resource(id, color, dto.StartAmount, dto.BaseStorage));
        }

        return new DefRegistry<Resource>(resources, r => r.Id, "surovina");
    }

    // ----- budovy -----

    private DefRegistry<BuildingDef> LoadBuildings(
        string path, BiomeRegistry biomes, DefRegistry<Resource> resources, SettlementRankLadder ranks)
    {
        var file = ReadFile<BuildingsFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Buildings is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádnou budovu.");
        }

        // Dvouprůchodově: nejdřív ID → index (kvůli 'upgradesTo', které míří na jinou budovu).
        var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < file.Buildings.Count; i++)
        {
            string id = RequireId(path, file.Buildings[i].Id, $"Budova na pozici {i}");
            if (!idToIndex.TryAdd(id, i))
            {
                throw new ContentLoadException(path, $"Duplicitní ID budovy '{id}'.");
            }
        }

        var buildings = new List<BuildingDef>(file.Buildings.Count);
        for (int i = 0; i < file.Buildings.Count; i++)
        {
            buildings.Add(ValidateBuilding(path, file.Buildings[i], i, biomes, resources, idToIndex, ranks));
        }

        // Vylepšení smí půdorys ZVĚTŠIT, ale nikdy zmenšit — kontrola po sestavení.
        //
        // Růst dává smysl: nejvyšší stupeň bydlení je arkologie 3×3 a bylo by
        // divné, kdyby zabírala jednu dlaždici jako chalupa. Simulace před
        // vylepšením ověří, že jsou nové dlaždice volné (viz HasRoomToGrow).
        //
        // Zmenšení naopak nedává smysl žádný a tiše by rozbilo mapu obsazenosti:
        // dlaždice, které budova opustí, by v ní zůstaly zabrané navždy.
        foreach (var building in buildings)
        {
            if (building.HasUpgrade)
            {
                var target = buildings[building.UpgradesToIndex];
                if (target.FootprintWidth < building.FootprintWidth
                    || target.FootprintHeight < building.FootprintHeight)
                {
                    throw new ContentLoadException(path, $"Budova '{building.Id}': vylepšení '{target.Id}' má menší půdorys (vylepšením se budova nesmí zmenšit).");
                }
            }

            // Sloučení dává smysl jen pro 1×1 → 2×2: blok čtyř budov zabírá přesně
            // dvě dlaždice na stranu, takže cíl musí sednout na stejné místo.
            if (building.CanMergeIntoBigger)
            {
                var target = buildings[building.MergesToIndex];
                if (building.FootprintWidth != 1 || building.FootprintHeight != 1)
                {
                    throw new ContentLoadException(path, $"Budova '{building.Id}' má 'mergesTo', ale není 1×1 — slučovat jde jen bloky 2×2 z jednodlaždicových budov.");
                }

                if (target.FootprintWidth != 2 || target.FootprintHeight != 2)
                {
                    throw new ContentLoadException(path, $"Budova '{building.Id}': cíl sloučení '{target.Id}' musí být 2×2, je {target.FootprintWidth}×{target.FootprintHeight}.");
                }
            }
        }

        return new DefRegistry<BuildingDef>(buildings, b => b.Id, "budova");
    }

    private static BuildingDef ValidateBuilding(
        string path, BuildingDto dto, int index, BiomeRegistry biomes, DefRegistry<Resource> resources,
        Dictionary<string, int> idToIndex, SettlementRankLadder ranks)
    {
        string id = RequireId(path, dto.Id, $"Budova na pozici {index}");
        var color = ParseColor(path, dto.MapColor, $"Budova '{id}'");
        string category = string.IsNullOrWhiteSpace(dto.Category) ? "other" : dto.Category.Trim();

        if (dto.Footprint is not { Length: 2 } || dto.Footprint[0] < 1 || dto.Footprint[1] < 1
            || dto.Footprint[0] > 8 || dto.Footprint[1] > 8)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'footprint' musí být [šířka, výška] v rozsahu 1–8.");
        }

        if (dto.WorkerSlots is < 0 or > 100)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'workerSlots' musí být 0–100, je {dto.WorkerSlots}.");
        }

        if (dto.HousingCapacity is < 0 or > 10_000)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'housingCapacity' musí být 0–10000, je {dto.HousingCapacity}.");
        }

        var buildCost = ParseResourceAmounts(path, id, "buildCost", dto.BuildCost, resources);
        if (buildCost.Count == 0)
        {
            throw new ContentLoadException(path, $"Budova '{id}' nemá vyplněnou cenu 'buildCost'.");
        }

        Recipe? recipe = null;
        if (dto.Recipe is not null)
        {
            var inputs = ParseResourceAmounts(path, id, "recipe.input", dto.Recipe.Input, resources);
            var outputs = ParseResourceAmounts(path, id, "recipe.output", dto.Recipe.Output, resources);
            if (outputs.Count == 0)
            {
                throw new ContentLoadException(path, $"Budova '{id}': recept musí mít aspoň jeden výstup.");
            }

            if (dto.Recipe.TimeTicks is < 1 or > 100_000)
            {
                throw new ContentLoadException(path, $"Budova '{id}': 'recipe.timeTicks' musí být 1–100000, je {dto.Recipe.TimeTicks}.");
            }

            recipe = new Recipe(inputs, outputs, dto.Recipe.TimeTicks);
        }

        if (dto.AllowedBiomes is not { Length: > 0 })
        {
            throw new ContentLoadException(path, $"Budova '{id}' nemá vyplněné 'allowedBiomes'.");
        }

        var mask = new bool[biomes.Count];
        foreach (var biomeId in dto.AllowedBiomes)
        {
            if (biomeId is null || !biomes.TryIndexOf(biomeId.Trim(), out int biomeIndex))
            {
                throw new ContentLoadException(path, $"Budova '{id}' odkazuje v 'allowedBiomes' na neexistující biom '{biomeId}'.");
            }

            if (biomes[biomeIndex].IsWater)
            {
                throw new ContentLoadException(path, $"Budova '{id}': biom '{biomeId}' v 'allowedBiomes' je vodní — na vodě se zatím stavět nedá.");
            }

            mask[biomeIndex] = true;
        }

        var storageBonus = ParseResourceAmounts(path, id, "storage", dto.Storage, resources);

        int upgradesToIndex = -1;
        IReadOnlyList<ResourceAmount> upgradeCost = Array.Empty<ResourceAmount>();
        if (!string.IsNullOrWhiteSpace(dto.UpgradesTo))
        {
            if (!idToIndex.TryGetValue(dto.UpgradesTo.Trim(), out upgradesToIndex))
            {
                throw new ContentLoadException(path, $"Budova '{id}' odkazuje ve 'upgradesTo' na neexistující budovu '{dto.UpgradesTo}'.");
            }

            if (upgradesToIndex == index)
            {
                throw new ContentLoadException(path, $"Budova '{id}' se nemůže vylepšit sama na sebe.");
            }

            upgradeCost = ParseResourceAmounts(path, id, "upgradeCost", dto.UpgradeCost, resources);
            if (upgradeCost.Count == 0)
            {
                throw new ContentLoadException(path, $"Budova '{id}' má 'upgradesTo', ale chybí 'upgradeCost'.");
            }
        }

        if (dto.PowerSupply is < 0 or > 1_000_000 || dto.PowerDemand is < 0 or > 1_000_000)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'powerSupply' i 'powerDemand' musí být 0–1000000.");
        }

        if (dto.ServiceValue is < 0 or > 1_000_000)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'serviceValue' musí být 0–1000000, je {dto.ServiceValue}.");
        }

        var pollution = ParseBuildingPollution(path, id, dto.Pollution);

        // Údržba musí mít protihodnotu, jinak je to jen daň za nic. Legitimní
        // důvody: budova obsluhuje lidi (serviceValue), čistí okolí (čističku taky
        // nemá smysl postavit a zapomenout na ni), nebo hlídá obzor — pátrací
        // stanice nic nevyrábí a přesto se vyplatí ji držet v provozu.
        var upkeep = ParseResourceAmounts(path, id, "upkeep", dto.Upkeep, resources);
        if (upkeep.Count > 0 && dto.ServiceValue <= 0 && pollution?.IsCleaner != true
            && dto.ScoutRadius <= 0)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}' má 'upkeep', ale nulový 'serviceValue', nic nečistí a nic nehlídá — "
                + "platila by se údržba za nic.");
        }

        // Sloučení bloku 2×2: cíl se řeší přes ID → index stejně jako vylepšení.
        // Technologie se sem NEuvádí — cíl se odemyká běžným 'unlocks' v tech.json
        // a slučovat jde, až když je odemčený. Jeden mechanismus místo dvou.
        int mergesToIndex = -1;
        var mergeCost = Array.Empty<ResourceAmount>() as IReadOnlyList<ResourceAmount>;
        if (!string.IsNullOrWhiteSpace(dto.MergesTo))
        {
            if (!idToIndex.TryGetValue(dto.MergesTo.Trim(), out mergesToIndex))
            {
                throw new ContentLoadException(path, $"Budova '{id}' odkazuje v 'mergesTo' na neexistující budovu '{dto.MergesTo}'.");
            }

            if (mergesToIndex == index)
            {
                throw new ContentLoadException(path, $"Budova '{id}' se nemůže sloučit sama na sebe.");
            }

            mergeCost = ParseResourceAmounts(path, id, "mergeCost", dto.MergeCost, resources);
        }

        var adjacency = ParseAdjacency(path, id, dto.Adjacency, recipe, biomes);

        if (dto.TerrainHarvestRadius is < 0 or > 32)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'terrainHarvestRadius' musí být 0–32, je {dto.TerrainHarvestRadius}.");
        }

        // Těžit z krajiny může jen budova, která něco vyrábí bez dovezených vstupů.
        // Jinak by data slibovala mechaniku, která se nemá čeho chytit.
        if (dto.TerrainHarvestRadius > 0 && recipe is null)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}' má 'terrainHarvestRadius', ale nic nevyrábí — nemá co z krajiny brát.");
        }

        // Dohled: příliš velký okruh by jednou budovou odhalil půl světa a mlha
        // by přestala být důvod někam jít.
        if (dto.ScoutRadius is < 0 or > 120)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'scoutRadius' musí být 0–120, je {dto.ScoutRadius}.");
        }

        // Reforestace: příliš velký okruh by z jedné školky udělal nekonečný les
        // a těžba by přestala mít cenu.
        if (dto.ReforestRadius is < 0 or > 24)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'reforestRadius' musí být 0–24, je {dto.ReforestRadius}.");
        }

        // Doba stavby: strop je tu proto, aby překlep v datech neudělal budovu,
        // která se staví déle, než kdo kdy bude hrát.
        if (dto.BuildTicks is < 0 or > 1_000_000)
        {
            throw new ContentLoadException(path, $"Budova '{id}': 'buildTicks' musí být 0–1000000, je {dto.BuildTicks}.");
        }

        return new BuildingDef(
            id, category, color, dto.Footprint[0], dto.Footprint[1],
            dto.WorkerSlots, dto.HousingCapacity, buildCost, recipe, mask,
            storageBonus, dto.AutoBuild, dto.Buildable ?? true, upgradesToIndex, upgradeCost,
            dto.PowerSupply, dto.PowerDemand, dto.RequiresAdjacentWater,
            dto.ServiceValue, upkeep, mergesToIndex, mergeCost, adjacency, dto.BuildTicks,
            dto.TerrainHarvestRadius, pollution, ParseMinSettlementRank(path, id, dto.MinSettlementRank, ranks),
            ParseMilestones(path, id, dto.Milestones),
            ParseSpectacle(path, id, dto.Spectacle),
            dto.ReforestRadius,
            dto.ScoutRadius);
    }

    /// <summary>
    /// Podívaná megastruktury. Blok je volitelný; neznámý efekt je tichá chyba
    /// obsahu — budova by se tvářila, že něco umí, a nikdy nic neudělala.
    /// </summary>
    private static BuildingSpectacle? ParseSpectacle(string path, string id, BuildingSpectacleDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        var effect = dto.Effect?.Trim().ToLowerInvariant() switch
        {
            "rocket_launch" => SpectacleEffect.RocketLaunch,
            "particle_beam" => SpectacleEffect.ParticleBeam,
            "ring_pulse" => SpectacleEffect.RingPulse,
            "forge_flare" => SpectacleEffect.ForgeFlare,
            "spire_beacon" => SpectacleEffect.SpireBeacon,
            _ => throw new ContentLoadException(path,
                $"Budova '{id}': 'spectacle.effect' zná jen rocket_launch, particle_beam, ring_pulse, "
                + $"forge_flare a spire_beacon, je '{dto.Effect}'."),
        };

        // Příliš častá podívaná přestane být podívanou a stane se blikáním.
        if (dto.IntervalSeconds is < 1 or > 3600)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'spectacle.intervalSeconds' musí být 1–3600, je {dto.IntervalSeconds}.");
        }

        return new BuildingSpectacle(effect, dto.IntervalSeconds);
    }

    /// <summary>
    /// Milníky za počet budov. Blok je volitelný; když v datech je, musí dávat
    /// smysl — milník „každou nultou budovu" nebo s nulovým bonusem je tichá
    /// chyba obsahu, ne „skoro správně" (fail-fast, CLAUDE.md).
    /// </summary>
    private static BuildingMilestones? ParseMilestones(string path, string id, BuildingMilestonesDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.Every is < 1 or > 10_000)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'milestones.every' musí být 1–10000, je {dto.Every}.");
        }

        if (dto.BonusPerStep <= 0 || dto.BonusPerStep > 5)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'milestones.bonusPerStep' musí být větší než 0 a nejvýš 5, je {dto.BonusPerStep}.");
        }

        // Bez stropu by šla výroba škálovat donekonečna jedním typem budovy.
        if (dto.MaxSteps is < 1 or > 100)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'milestones.maxSteps' musí být 1–100, je {dto.MaxSteps}.");
        }

        return new BuildingMilestones(dto.Every, dto.BonusPerStep, dto.MaxSteps);
    }

    /// <summary>
    /// Přeloží požadovaný stupeň sídla z ID na index. Neznámé ID je tichá chyba
    /// obsahu — budova by se dala postavit kdekoli, i když data slibují opak.
    /// </summary>
    private static int ParseMinSettlementRank(string path, string id, string? rankId, SettlementRankLadder ranks)
    {
        if (string.IsNullOrWhiteSpace(rankId))
        {
            return -1;
        }

        int index = ranks.IndexOf(rankId.Trim());
        if (index < 0)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}' vyžaduje stupeň sídla '{rankId}', který v settlement-ranks.json neexistuje.");
        }

        return index;
    }

    /// <summary>
    /// Dopad budovy na okolí. Blok je volitelný (drtivá většina budov okolí neřeší),
    /// ale když v datech je, musí něco dělat — prázdný slib je tichá chyba obsahu.
    /// </summary>
    private static PollutionOutput? ParseBuildingPollution(string path, string id, BuildingPollutionDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        var output = new PollutionOutput(dto.Air, dto.Water, dto.Soil);
        if (output.IsNeutral)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}' má blok 'pollution', ale samé nuly — buď ho vyplň, nebo smaž.");
        }

        // Strop je proti překlepu v datech: budova, která za sekundu vyrobí tisíc
        // jednotek špíny, by mapu zamořila dřív, než ji hráč stihne uvidět.
        foreach (double value in new[] { dto.Air, dto.Water, dto.Soil })
        {
            if (Math.Abs(value) > 100)
            {
                throw new ContentLoadException(path,
                    $"Budova '{id}': hodnoty v 'pollution' musí být v rozsahu −100 až 100, je {value}.");
            }
        }

        return output;
    }

    /// <summary>
    /// Načte pravidlo bonusu za okolí. Blok je volitelný; když ale v datech je,
    /// musí dávat smysl — bonus bez výroby ani bonus s nulovým stropem není
    /// „skoro správně", je to tichá chyba obsahu (fail-fast, CLAUDE.md).
    /// </summary>
    private static AdjacencyRule? ParseAdjacency(
        string path, string id, AdjacencyDto? dto, Recipe? recipe, BiomeRegistry biomes)
    {
        if (dto is null)
        {
            return null;
        }

        if (recipe is null)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}' má 'adjacency', ale nic nevyrábí — bonus za okolí by se neprojevil.");
        }

        var biomeMask = ParseBiomeMask(path, $"Budova '{id}' v 'adjacency'", dto.Biomes, biomes);

        if (dto.Radius is < 1 or > 8)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'adjacency.radius' musí být 1–8, je {dto.Radius}.");
        }

        if (dto.PerTile <= 0 || dto.PerTile > 1)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'adjacency.perTile' musí být větší než 0 a nejvýš 1, je {dto.PerTile}.");
        }

        if (dto.Max <= 0 || dto.Max > 10)
        {
            throw new ContentLoadException(path,
                $"Budova '{id}': 'adjacency.max' musí být větší než 0 a nejvýš 10, je {dto.Max}.");
        }

        return new AdjacencyRule(biomeMask, dto.Radius, dto.PerTile, dto.Max);
    }

    // ----- tech tree -----

    private DefRegistry<TechDef> LoadTech(string path, DefRegistry<BuildingDef> buildings, DefRegistry<Resource> resources)
    {
        // Tech tree je volitelný — bez souboru je registr prázdný a vše je odemčené.
        if (!File.Exists(path))
        {
            return new DefRegistry<TechDef>(Array.Empty<TechDef>(), t => t.Id, "technologie", allowEmpty: true);
        }

        var file = ReadFile<TechFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Techs ?? new List<TechDto>();
        var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            string id = RequireId(path, dtos[i].Id, $"Technologie na pozici {i}");
            if (!idToIndex.TryAdd(id, i))
            {
                throw new ContentLoadException(path, $"Duplicitní ID technologie '{id}'.");
            }
        }

        var techs = new List<TechDef>(dtos.Count);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = dto.Id!.Trim();

            var cost = ParseResourceAmounts(path, id, "cost", dto.Cost, resources);
            if (cost.Count == 0)
            {
                throw new ContentLoadException(path, $"Technologie '{id}' nemá vyplněnou cenu 'cost'.");
            }

            var prereqs = new List<int>();
            foreach (var prereqId in dto.Prerequisites ?? Array.Empty<string>())
            {
                if (prereqId is null || !idToIndex.TryGetValue(prereqId.Trim(), out int prereqIndex))
                {
                    throw new ContentLoadException(path, $"Technologie '{id}' odkazuje na neexistující prerekvizitu '{prereqId}'.");
                }

                if (prereqIndex == i)
                {
                    throw new ContentLoadException(path, $"Technologie '{id}' nemůže být svou vlastní prerekvizitou.");
                }

                prereqs.Add(prereqIndex);
            }

            var unlocks = new List<int>();
            foreach (var buildingId in dto.Unlocks ?? Array.Empty<string>())
            {
                if (buildingId is null || !buildings.TryIndexOf(buildingId.Trim(), out int buildingIndex))
                {
                    throw new ContentLoadException(path, $"Technologie '{id}' odemyká neexistující budovu '{buildingId}'.");
                }

                unlocks.Add(buildingIndex);
            }

            // Cíl efektu je naopak ODKAZ — překlep v názvu suroviny by znamenal
            // vylepšení, které tiše nedělá nic (CLAUDE.md: fail-fast).
            int targetResource = -1;
            if (!string.IsNullOrWhiteSpace(dto.TargetResource)
                && !resources.TryIndexOf(dto.TargetResource.Trim(), out targetResource))
            {
                throw new ContentLoadException(
                    path, $"Technologie '{id}' míří na neexistující surovinu '{dto.TargetResource}'.");
            }

            // Efekt se NEvaliduje proti seznamu — neznámý se za běhu tiše ignoruje
            // (behavior-ID hook, data smí předběhnout kód).
            techs.Add(new TechDef(
                id, cost, prereqs, unlocks, dto.Effect?.Trim() ?? string.Empty, dto.Magnitude, targetResource));
        }

        return new DefRegistry<TechDef>(techs, t => t.Id, "technologie", allowEmpty: true);
    }

    // ----- Vzestup (prestige) -----

    /// <summary>
    /// Behavior-ID trvalých bonusů (Vzestup i pasivní efekty technologií). Seznam je
    /// tu proto, aby překlep v datech spadl hned při startu, ne aby se za hodinu
    /// hraní tiše nic nedělo. Přidání nového efektu = zápis sem + větev v
    /// <c>Simulation.RecomputeBonuses</c>.
    /// </summary>
    private static readonly HashSet<string> KnownPrestigeEffects = new(StringComparer.Ordinal)
    {
        "production_mult", "harvest_mult", "growth_mult", "housing_mult", "storage_mult", "start_resources", "offline_mult",
        "crit_chance", "jackpot_chance", "discovery_luck", "festival_power", "research_discount", "autobuild_speed",
    };

    /// <summary>
    /// Efekty Odkazu: všechno co Vzestup, plus dva navíc, které míří na
    /// <b>samotné vzestupování</b>. Ty dva jsou důvod, proč druhá vrstva vůbec
    /// existuje — kdyby uměla jen „ještě víc výroby", byl by Odkaz jen dražší
    /// Vzestup a hráč by neměl důvod ho udělat.
    /// </summary>
    private static readonly HashSet<string> KnownLegacyEffects =
        new(KnownPrestigeEffects, StringComparer.Ordinal) { "ascension_points_mult", "ascension_discount" };

    private (PrestigeConfig Config, DefRegistry<PrestigeUpgradeDef> Upgrades) LoadPrestige(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var file = ReadFile<PrestigeFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Ascension?.Requirement is null || file.Ascension.Points is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'ascension' s 'requirement' a 'points'.");
        }

        var requirement = ParseCondition(path, "ascension.requirement", file.Ascension.Requirement, resources, buildings, techs);
        var points = file.Ascension.Points;
        if (points.Divisor < 1)
        {
            throw new ContentLoadException(path, $"'ascension.points.divisor' musí být ≥ 1, je {points.Divisor}.");
        }

        var (pointsMetric, pointsParam) = ParseMetric(
            path, "ascension.points", points.Metric, points.Resource, building: null, tech: null, resources, buildings, techs);

        var upgrades = ParsePermanentUpgrades(
            path, file.Upgrades, KnownPrestigeEffects, keyPrefix: "prestige", what: "Upgrade Vzestupu");

        // Bez zadaného růstu se práh nemění (zpětně kompatibilní starší data).
        double requirementGrowth = file.Ascension.RequirementGrowth <= 0 ? 1.0 : file.Ascension.RequirementGrowth;
        if (requirementGrowth is < 1.0 or > 100.0)
        {
            throw new ContentLoadException(path, $"'ascension.requirementGrowth' musí být 1–100, je {requirementGrowth}.");
        }

        // Nezadaná mocnina = lineárně (stará data).
        double pointsExponent = points.Exponent <= 0 ? 1.0 : points.Exponent;
        if (pointsExponent is < 0.1 or > 2.0)
        {
            throw new ContentLoadException(path, $"'ascension.points.exponent' musí být 0,1–2, je {pointsExponent}.");
        }

        var config = new PrestigeConfig(
            requirement, pointsMetric, pointsParam, points.Divisor, requirementGrowth, pointsExponent);
        return (config, new DefRegistry<PrestigeUpgradeDef>(upgrades, u => u.Id, "upgrade Vzestupu", allowEmpty: true));
    }

    /// <summary>
    /// Načte trvalé upgrady jedné prestižní vrstvy. Vzestup i Odkaz mají přesně
    /// stejný tvar dat (efekt, síla, cena, prereky, opakovatelnost) a liší se jen
    /// slovníkem povolených efektů a jmenným prostorem textů — proto jedna metoda
    /// a ne dvě skoro stejné kopie.
    /// </summary>
    private List<PrestigeUpgradeDef> ParsePermanentUpgrades(
        string path,
        List<PrestigeUpgradeDto>? dtoList,
        HashSet<string> knownEffects,
        string keyPrefix,
        string what)
    {
        var dtos = dtoList ?? new List<PrestigeUpgradeDto>();
        var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            string id = RequireId(path, dtos[i].Id, $"{what} na pozici {i}");
            if (!idToIndex.TryAdd(id, i))
            {
                throw new ContentLoadException(path, $"Duplicitní ID: '{id}'.");
            }
        }

        var upgrades = new List<PrestigeUpgradeDef>(dtos.Count);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = dto.Id!.Trim();
            string effect = (dto.Effect ?? string.Empty).Trim();
            if (!knownEffects.Contains(effect))
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': neznámý efekt '{dto.Effect}' (známé: {string.Join(", ", knownEffects)}).");
            }

            if (dto.Magnitude is <= 0 or > 100)
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': 'magnitude' musí být v (0, 100], je {dto.Magnitude}.");
            }

            if (dto.Cost is < 1 or > 100_000)
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': 'cost' musí být 1–100000, je {dto.Cost}.");
            }

            var prereqs = new List<int>();
            foreach (var prereqId in dto.Prerequisites ?? Array.Empty<string>())
            {
                if (prereqId is null || !idToIndex.TryGetValue(prereqId.Trim(), out int prereqIndex))
                {
                    throw new ContentLoadException(path, $"Upgrade '{id}' odkazuje na neexistující prerekvizitu '{prereqId}'.");
                }

                if (prereqIndex == i)
                {
                    throw new ContentLoadException(path, $"Upgrade '{id}' nemůže být svou vlastní prerekvizitou.");
                }

                prereqs.Add(prereqIndex);
            }

            // Nezadané maximum = jednorázový upgrade (zpětná kompatibilita se
            // staršími daty, kde opakování neexistovalo).
            int maxLevel = dto.MaxLevel <= 0 ? 1 : dto.MaxLevel;
            if (maxLevel > 1000)
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': 'maxLevel' smí být nejvýš 1000, je {maxLevel}.");
            }

            double costGrowth = dto.CostGrowth <= 0 ? 1.0 : dto.CostGrowth;
            if (costGrowth is < 1.0 or > 10.0)
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': 'costGrowth' musí být 1–10, je {costGrowth}.");
            }

            upgrades.Add(new PrestigeUpgradeDef(id, effect, dto.Magnitude, dto.Cost, prereqs, maxLevel, costGrowth, keyPrefix));
        }

        return upgrades;
    }

    /// <summary>
    /// Načte Odkaz. Chybějící soubor <b>není chyba</b> — je to volitelná vrstva
    /// a hra bez ní běží dál (stejně jako Velké dílo nebo víra).
    /// </summary>
    private (LegacyConfig Config, DefRegistry<PrestigeUpgradeDef> Upgrades) LoadLegacy(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var empty = new DefRegistry<PrestigeUpgradeDef>(
            Array.Empty<PrestigeUpgradeDef>(), u => u.Id, "upgrade Odkazu", allowEmpty: true);
        if (!File.Exists(path))
        {
            return (LegacyConfig.Disabled, empty);
        }

        var file = ReadFile<LegacyFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Leave?.Requirement is null || file.Leave.Points is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'leave' s 'requirement' a 'points'.");
        }

        var requirement = ParseCondition(path, "leave.requirement", file.Leave.Requirement, resources, buildings, techs);
        var points = file.Leave.Points;
        if (points.Divisor < 1)
        {
            throw new ContentLoadException(path, $"'leave.points.divisor' musí být ≥ 1, je {points.Divisor}.");
        }

        var (pointsMetric, pointsParam) = ParseMetric(
            path, "leave.points", points.Metric, points.Resource, building: null, tech: null, resources, buildings, techs);

        double requirementGrowth = file.Leave.RequirementGrowth <= 0 ? 1.0 : file.Leave.RequirementGrowth;
        if (requirementGrowth is < 1.0 or > 100.0)
        {
            throw new ContentLoadException(path, $"'leave.requirementGrowth' musí být 1–100, je {requirementGrowth}.");
        }

        // Na rozdíl od Vzestupu se tu povoluje mocnina > 1: metrika (počet
        // Vzestupů) roste s časem mnohem pomaleji než populace.
        double pointsExponent = points.Exponent <= 0 ? 1.0 : points.Exponent;
        if (pointsExponent is < 0.1 or > 4.0)
        {
            throw new ContentLoadException(path, $"'leave.points.exponent' musí být 0,1–4, je {pointsExponent}.");
        }

        var upgrades = ParsePermanentUpgrades(
            path, file.Upgrades, KnownLegacyEffects, keyPrefix: "legacy", what: "Upgrade Odkazu");

        var config = new LegacyConfig(
            requirement, requirementGrowth, pointsMetric, pointsParam, points.Divisor, pointsExponent);
        return (config, new DefRegistry<PrestigeUpgradeDef>(upgrades, u => u.Id, "upgrade Odkazu", allowEmpty: true));
    }

    // ----- úkoly (quests) -----

    private (DefRegistry<QuestDef> Quests, DynamicQuestConfig Dynamic) LoadQuests(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var file = ReadFile<QuestFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Quests ?? new List<QuestDto>();
        var quests = new List<QuestDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Úkol na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID úkolu '{id}'.");
            }

            if (dto.Condition is null)
            {
                throw new ContentLoadException(path, $"Úkol '{id}' nemá 'condition'.");
            }

            var condition = ParseCondition(path, $"úkol '{id}'", dto.Condition, resources, buildings, techs);
            var reward = ParseResourceAmounts(path, id, "reward", dto.Reward, resources);
            quests.Add(new QuestDef(id, condition, reward));
        }

        if (file.Dynamic?.Condition is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'dynamic' s 'condition' (dynamické úkoly).");
        }

        var dynCondition = ParseCondition(path, "dynamic", file.Dynamic.Condition, resources, buildings, techs);
        if (file.Dynamic.TargetGrowth <= 1.0)
        {
            throw new ContentLoadException(path, $"'dynamic.targetGrowth' musí být > 1, je {file.Dynamic.TargetGrowth}.");
        }

        if (file.Dynamic.RewardGrowth < 1.0)
        {
            throw new ContentLoadException(path, $"'dynamic.rewardGrowth' musí být ≥ 1, je {file.Dynamic.RewardGrowth}.");
        }

        var dynReward = ParseResourceAmounts(path, "dynamic", "reward", file.Dynamic.Reward, resources);
        var dynamic = new DynamicQuestConfig(dynCondition, file.Dynamic.TargetGrowth, dynReward, file.Dynamic.RewardGrowth);
        return (new DefRegistry<QuestDef>(quests, q => q.Id, "úkol", allowEmpty: true), dynamic);
    }

    // ----- achievementy -----

    private DefRegistry<AchievementDef> LoadAchievements(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var file = ReadFile<AchievementFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Achievements ?? new List<AchievementDto>();
        var result = new List<AchievementDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Achievement na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID achievementu '{id}'.");
            }

            if (dto.Condition is null)
            {
                throw new ContentLoadException(path, $"Achievement '{id}' nemá 'condition'.");
            }

            var condition = ParseCondition(path, $"achievement '{id}'", dto.Condition, resources, buildings, techs);
            result.Add(new AchievementDef(id, condition, dto.Hidden));
        }

        return new DefRegistry<AchievementDef>(result, a => a.Id, "achievement", allowEmpty: true);
    }

    // ----- události -----

    private DefRegistry<EventDef> LoadEvents(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var file = ReadFile<EventFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var dtos = file.Events ?? new List<EventDto>();
        var result = new List<EventDef>(dtos.Count);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = RequireId(path, dto.Id, $"Událost na pozici {i}");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID události '{id}'.");
            }

            if (dto.Choices is not { Count: >= 1 })
            {
                throw new ContentLoadException(path, $"Událost '{id}' musí mít aspoň jednu volbu.");
            }

            var choices = new List<EventChoiceDef>(dto.Choices.Count);
            var seenChoiceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var choiceDto in dto.Choices)
            {
                string choiceId = RequireId(path, choiceDto.Id, $"Volba události '{id}'");
                if (!seenChoiceIds.Add(choiceId))
                {
                    throw new ContentLoadException(path, $"Událost '{id}': duplicitní ID volby '{choiceId}'.");
                }

                var cost = ParseResourceAmounts(path, id, $"{choiceId}.cost", choiceDto.Cost, resources);
                var gain = ParseResourceAmounts(path, id, $"{choiceId}.gain", choiceDto.Gain, resources);
                choices.Add(new EventChoiceDef($"event.{id}.{choiceId}", cost, gain));
            }

            // Podmínka je volitelná — bez ní je událost dostupná od začátku.
            GoalCondition? requirement = dto.Requires is null
                ? null
                : ParseCondition(path, $"událost '{id}'", dto.Requires, resources, buildings, techs);

            result.Add(new EventDef(id, choices, requirement));
        }

        return new DefRegistry<EventDef>(result, e => e.Id, "událost", allowEmpty: true);
    }

    /// <summary>
    /// Přeloží podmínku (metrika + práh + odkaz) z JSON na typovaný <see cref="GoalCondition"/>.
    /// Sdílené: Vzestup, úkoly, achievementy. Data říkají „co", kód „jak" — žádná logika v JSON.
    /// </summary>
    private static GoalCondition ParseCondition(
        string path, string owner, GoalConditionDto dto,
        DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        var (kind, param) = ParseMetric(path, owner, dto.Metric, dto.Resource, dto.Building, dto.Tech, resources, buildings, techs);

        // Výzkum je binární (0/1) — bez explicitního prahu se bere 1.
        long target = kind == MetricKind.ResearchedTech && dto.Target <= 0 ? 1 : dto.Target;
        if (target < 1)
        {
            throw new ContentLoadException(path, $"{owner}: 'target' musí být ≥ 1, je {dto.Target}.");
        }

        return new GoalCondition(kind, param, target);
    }

    private static (MetricKind Kind, int Param) ParseMetric(
        string path, string owner, string? metric, string? resource, string? building, string? tech,
        DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings, DefRegistry<TechDef> techs)
    {
        switch ((metric ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "population": return (MetricKind.Population, -1);
            case "housing": return (MetricKind.HousingCapacity, -1);
            case "buildings": return (MetricKind.TotalBuildings, -1);
            case "ascension": return (MetricKind.AscensionLevel, -1);
            case "day": return (MetricKind.DayNumber, -1);
            case "planted": return (MetricKind.PlantedNodes, -1);
            case "terraformed": return (MetricKind.TerraformedTiles, -1);
            case "merged": return (MetricKind.MergedBuildings, -1);
            case "wonders": return (MetricKind.WondersCompleted, -1);
            case "prayers": return (MetricKind.Prayers, -1);
            case "cities": return (MetricKind.CitiesJoined, -1);
            case "explored": return (MetricKind.Explored, -1);
            case "harvested": return (MetricKind.Harvested, ResolveRef(path, owner, "resource", resource, resources));
            case "resource": return (MetricKind.ResourceStock, ResolveRef(path, owner, "resource", resource, resources));
            case "building": return (MetricKind.BuildingOfType, ResolveRef(path, owner, "building", building, buildings));
            case "research": return (MetricKind.ResearchedTech, ResolveRef(path, owner, "tech", tech, techs));
            default: throw new ContentLoadException(path, $"{owner}: neznámá metrika '{metric}'.");
        }
    }

    private static int ResolveRef<T>(string path, string owner, string field, string? id, DefRegistry<T> registry)
        where T : class
    {
        if (id is null || !registry.TryIndexOf(id.Trim(), out int index))
        {
            throw new ContentLoadException(path, $"{owner}: metrika vyžaduje platné '{field}', ale '{id}' neexistuje.");
        }

        return index;
    }

    private static IReadOnlyList<ResourceAmount> ParseResourceAmounts(
        string path, string ownerId, string field, Dictionary<string, int>? amounts, DefRegistry<Resource> resources)
    {
        if (amounts is null || amounts.Count == 0)
        {
            return Array.Empty<ResourceAmount>();
        }

        var result = new List<ResourceAmount>(amounts.Count);
        foreach (var (resourceId, amount) in amounts)
        {
            if (!resources.TryIndexOf(resourceId, out int resourceIndex))
            {
                throw new ContentLoadException(path, $"Budova '{ownerId}': '{field}' odkazuje na neexistující surovinu '{resourceId}'.");
            }

            if (amount <= 0)
            {
                throw new ContentLoadException(path, $"Budova '{ownerId}': '{field}.{resourceId}' musí být kladné, je {amount}.");
            }

            result.Add(new ResourceAmount(resourceIndex, amount));
        }

        return result;
    }

    // ----- gameplay -----

    private GameplayConfig LoadGameplay(
        string path, DefRegistry<Resource> resources, DefRegistry<BuildingDef> buildings,
        DefRegistry<TechDef> techs)
    {
        var file = ReadFile<GameplayFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.StartingPopulation is < 0 or > 1_000_000)
        {
            throw new ContentLoadException(path, $"'startingPopulation' musí být 0–1000000, je {file.StartingPopulation}.");
        }

        if (file.BaseHousingCapacity is < 0 or > 1_000_000)
        {
            throw new ContentLoadException(path, $"'baseHousingCapacity' musí být 0–1000000, je {file.BaseHousingCapacity}.");
        }

        if (file.PopulationGrowthPerSecond is <= 0 or > 1_000)
        {
            throw new ContentLoadException(path, $"'populationGrowthPerSecond' musí být kladný, je {file.PopulationGrowthPerSecond}.");
        }

        if (file.FoodPerPersonPerSecond is < 0 or > 1_000)
        {
            throw new ContentLoadException(path, $"'foodPerPersonPerSecond' nesmí být záporný, je {file.FoodPerPersonPerSecond}.");
        }

        if (string.IsNullOrWhiteSpace(file.FoodResource))
        {
            throw new ContentLoadException(path, "Chybí 'foodResource' — která surovina je jídlo.");
        }

        // Startovní budovy: překlep v ID by znamenal prázdnou mapu bez vysvětlení,
        // proto se odkaz ověří hned při načtení (CLAUDE.md: fail-fast).
        var startingBuildings = new List<int>();
        foreach (string id in file.StartingBuildings ?? Array.Empty<string>())
        {
            if (!buildings.TryIndexOf(id.Trim(), out int buildingIndex))
            {
                throw new ContentLoadException(
                    path, $"'startingBuildings' odkazuje na neexistující budovu '{id}'.");
            }

            startingBuildings.Add(buildingIndex);
        }

        if (!resources.TryIndexOf(file.FoodResource.Trim(), out int foodIndex))
        {
            throw new ContentLoadException(path, $"'foodResource' odkazuje na neexistující surovinu '{file.FoodResource}'.");
        }

        if (file.AutoBuild is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'autoBuild' (interval, radius, headroom automatického růstu).");
        }

        if (file.AutoBuild.IntervalTicks is < 1 or > 100_000)
        {
            throw new ContentLoadException(path, $"'autoBuild.intervalTicks' musí být 1–100000, je {file.AutoBuild.IntervalTicks}.");
        }

        if (file.AutoBuild.SearchRadius is < 1 or > 64)
        {
            throw new ContentLoadException(path, $"'autoBuild.searchRadius' musí být 1–64, je {file.AutoBuild.SearchRadius}.");
        }

        if (file.AutoBuild.PopulationHeadroom is < 0 or > 10_000)
        {
            throw new ContentLoadException(path, $"'autoBuild.populationHeadroom' musí být 0–10000, je {file.AutoBuild.PopulationHeadroom}.");
        }

        if (file.Roads is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'roads' (barva a dosah auto-silnic).");
        }

        var roadColor = ParseColor(path, file.Roads.MapColor, "Blok 'roads'");
        if (file.Roads.MaxSearchDistance is < 1 or > 1000)
        {
            throw new ContentLoadException(path, $"'roads.maxSearchDistance' musí být 1–1000, je {file.Roads.MaxSearchDistance}.");
        }

        if (file.Roads.MaxBridgeSpan is < 0 or > 64)
        {
            throw new ContentLoadException(path, $"'roads.maxBridgeSpan' musí být 0–64, je {file.Roads.MaxBridgeSpan}.");
        }

        // Nezadaná hodnota = silnice bez mechanického dopadu (zpětně kompatibilní).
        double disconnectedMult = file.Roads.DisconnectedProductionMult <= 0
            ? 1.0
            : file.Roads.DisconnectedProductionMult;
        if (disconnectedMult > 1.0)
        {
            throw new ContentLoadException(path,
                $"'roads.disconnectedProductionMult' musí být 0–1 (napojení nesmí výrobu snižovat), je {disconnectedMult}.");
        }

        if (file.Settlements is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'settlements' (detekce osad).");
        }

        if (file.Settlements.MinBuildings is < 2 or > 1000)
        {
            throw new ContentLoadException(path, $"'settlements.minBuildings' musí být 2–1000, je {file.Settlements.MinBuildings}.");
        }

        if (file.Settlements.ClusterDistance is < 1 or > 64)
        {
            throw new ContentLoadException(path, $"'settlements.clusterDistance' musí být 1–64, je {file.Settlements.ClusterDistance}.");
        }

        if (file.Settlements.UpdateIntervalTicks is < 1 or > 100_000)
        {
            throw new ContentLoadException(path, $"'settlements.updateIntervalTicks' musí být 1–100000, je {file.Settlements.UpdateIntervalTicks}.");
        }

        if (file.DayNight is null)
        {
            throw new ContentLoadException(path, "Chybí blok 'dayNight' (denní/noční cyklus).");
        }

        if (file.DayNight.DayLengthSeconds is < 10 or > 86_400)
        {
            throw new ContentLoadException(path, $"'dayNight.dayLengthSeconds' musí být 10–86400, je {file.DayNight.DayLengthSeconds}.");
        }

        if (file.DayNight.StartTimeOfDay is < 0 or >= 1)
        {
            throw new ContentLoadException(path, $"'dayNight.startTimeOfDay' musí být v [0, 1), je {file.DayNight.StartTimeOfDay}.");
        }

        var nightColor = ParseColor(path, file.DayNight.NightColor, "Blok 'dayNight' (nightColor)");
        var duskColor = ParseColor(path, file.DayNight.DuskColor, "Blok 'dayNight' (duskColor)");
        if (file.DayNight.NightAlpha is < 0 or > 1 || file.DayNight.DuskAlpha is < 0 or > 1)
        {
            throw new ContentLoadException(path, "'dayNight.nightAlpha' i 'duskAlpha' musí být 0–1.");
        }

        // Slavnost a kritický sběr jsou volitelné bloky s rozumnými výchozími hodnotami.
        var boost = file.Boost is null
            ? new BoostConfig(30, 120, 2.0)
            : new BoostConfig(file.Boost.DurationSeconds, file.Boost.CooldownSeconds, file.Boost.Multiplier);
        if (boost.DurationSeconds is < 1 or > 3600 || boost.CooldownSeconds < boost.DurationSeconds || boost.Multiplier is <= 1 or > 100)
        {
            throw new ContentLoadException(path, "'boost' musí mít 1≤duration≤cooldown a multiplier v (1, 100].");
        }

        var harvest = file.Harvest is null
            ? new HarvestConfig(0.12, 5.0)
            : new HarvestConfig(file.Harvest.CritChance, file.Harvest.CritMultiplier,
                file.Harvest.JackpotMultiplier <= 0 ? 25.0 : file.Harvest.JackpotMultiplier);
        if (harvest.CritChance is < 0 or > 1 || harvest.CritMultiplier is < 1 or > 1000)
        {
            throw new ContentLoadException(path, "'harvest.critChance' musí být 0–1 a 'critMultiplier' 1–1000.");
        }

        if (harvest.JackpotMultiplier is < 1 or > 10000)
        {
            throw new ContentLoadException(path, $"'harvest.jackpotMultiplier' musí být 1–10000, je {harvest.JackpotMultiplier}.");
        }

        var dailyReward = new DailyRewardConfig(
            ParseResourceAmounts(path, "gameplay", "dailyReward.reward", file.DailyReward?.Reward, resources),
            file.DailyReward is { StreakCap: > 0 } ? file.DailyReward.StreakCap : 7);

        var planting = ParsePlanting(path, file.Planting, resources, techs);

        return new GameplayConfig(
            file.StartingPopulation,
            startingBuildings,
            file.BaseHousingCapacity,
            file.PopulationGrowthPerSecond,
            file.FoodPerPersonPerSecond,
            foodIndex,
            new AutoBuildConfig(file.AutoBuild.IntervalTicks, file.AutoBuild.SearchRadius, file.AutoBuild.PopulationHeadroom),
            new RoadConfig(roadColor, file.Roads.MaxSearchDistance, file.Roads.MaxBridgeSpan, disconnectedMult),
            new SettlementConfig(file.Settlements.MinBuildings, file.Settlements.ClusterDistance, file.Settlements.UpdateIntervalTicks),
            new DayNightConfig(
                file.DayNight.DayLengthSeconds,
                file.DayNight.StartTimeOfDay,
                nightColor,
                duskColor,
                file.DayNight.NightAlpha,
                file.DayNight.DuskAlpha),
            boost,
            harvest,
            dailyReward,
            planting,
            ParseHappiness(path, file.Happiness),
            ParseStaffing(path, file.Staffing),
            ParseHaul(path, file.Haul),
            ParseTools(path, file.Tools, resources),
            ParseCombo(path, file.Combo),
            ParsePollution(path, file.Pollution),
            ParseBulkBuild(path, file.BulkBuild),
            ParseLaser(path, file.Laser),
            ParseHistory(path, file.History),
            ParseResearch(path, file.Research));
    }

    /// <summary>
    /// Nastavení časosběru. Chybí-li blok, nic se nezaznamenává a save zůstává
    /// stejně velký jako dřív.
    /// </summary>
    /// <summary>
    /// Škálování cen výzkumu. Chybí-li blok, platí ceny přesně tak, jak jsou
    /// v tech.json — starší data a mody tím nic neztratí.
    /// </summary>
    private static ResearchConfig? ParseResearch(string path, ResearchDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.CostMultiplier is < 0.1 or > 100)
        {
            throw new ContentLoadException(path,
                $"'research.costMultiplier' musí být 0.1–100, je {dto.CostMultiplier}.");
        }

        // Strop je tu proti překlepu: 0.5 místo 0.05 by po padesáti výzkumech
        // udělalo z dalšího uzlu nedosažitelnou zeď.
        if (dto.CostGrowthPerTech is < 0 or > 1)
        {
            throw new ContentLoadException(path,
                $"'research.costGrowthPerTech' musí být 0–1, je {dto.CostGrowthPerTech}.");
        }

        return new ResearchConfig(dto.CostMultiplier, dto.CostGrowthPerTech);
    }

    /// <summary>
    /// Co všechno jde zasadit. Starší data mají jen jeden druh psaný přímo
    /// v bloku (<c>cost/resource/amount</c>) — ten se bere jako první druh, aby
    /// se hra i mody s takovými daty chovaly jako dřív.
    /// </summary>
    private static PlantingConfig ParsePlanting(
        string path, PlantingDto? dto, DefRegistry<Resource> resources, DefRegistry<TechDef> techs)
    {
        var species = new List<PlantSpecies>();

        foreach (var speciesDto in dto?.Species ?? new List<PlantSpeciesDto>())
        {
            string id = RequireId(path, speciesDto.Id, "Druh k zasazení");
            if (!resources.TryIndexOf((speciesDto.Resource ?? string.Empty).Trim(), out int resourceIndex))
            {
                throw new ContentLoadException(path,
                    $"'planting.species[{id}].resource' odkazuje na neexistující surovinu '{speciesDto.Resource}'.");
            }

            int techIndex = -1;
            if (speciesDto.RequiresTech is { Length: > 0 } techId
                && !techs.TryIndexOf(techId.Trim(), out techIndex))
            {
                throw new ContentLoadException(path,
                    $"'planting.species[{id}].requiresTech' odkazuje na neexistující technologii '{techId}'.");
            }

            species.Add(new PlantSpecies(
                id,
                ParseResourceAmounts(path, "gameplay", $"planting.species[{id}].cost", speciesDto.Cost, resources),
                resourceIndex,
                Math.Max(1, speciesDto.Amount),
                techIndex));
        }

        if (species.Count > 0)
        {
            return new PlantingConfig(species);
        }

        // Starý tvar bloku.
        int legacyResource = 0;
        if (dto?.Resource is { } legacyId && !resources.TryIndexOf(legacyId.Trim(), out legacyResource))
        {
            throw new ContentLoadException(path, $"'planting.resource' odkazuje na neexistující surovinu '{legacyId}'.");
        }

        return new PlantingConfig(new[]
        {
            new PlantSpecies(
                "grove",
                ParseResourceAmounts(path, "gameplay", "planting.cost", dto?.Cost, resources),
                legacyResource,
                dto is { Amount: > 0 } ? dto.Amount : 2),
        });
    }

    private static HistoryConfig? ParseHistory(string path, HistoryDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        // Příliš hustý záznam by nafoukl save, příliš řídký by přehrávku udělal
        // trhanou — obojí je tichá chyba obsahu.
        if (dto.IntervalSeconds is < 1 or > 3600)
        {
            throw new ContentLoadException(path,
                $"'history.intervalSeconds' musí být 1–3600, je {dto.IntervalSeconds}.");
        }

        if (dto.MaxFrames is < 2 or > 2000)
        {
            throw new ContentLoadException(path, $"'history.maxFrames' musí být 2–2000, je {dto.MaxFrames}.");
        }

        return new HistoryConfig(dto.IntervalSeconds, dto.MaxFrames);
    }

    /// <summary>
    /// Nastavení těžebního laseru. Chybí-li blok, je vrstva vypnutá a ruční sběr
    /// zůstává klikáním jako dřív.
    /// </summary>
    private static LaserConfig? ParseLaser(string path, LaserDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        // Příliš rychlý paprsek by z krajiny udělal jednorázovou zásobárnu —
        // strop je tu proti tiché chybě obsahu, ne proti hráči.
        if (dto.HarvestsPerSecond is <= 0 or > 60)
        {
            throw new ContentLoadException(path,
                $"'laser.harvestsPerSecond' musí být v (0, 60], je {dto.HarvestsPerSecond}.");
        }

        if (dto.RadiusTiles is < 0 or > 8)
        {
            throw new ContentLoadException(path, $"'laser.radiusTiles' musí být 0–8, je {dto.RadiusTiles}.");
        }

        if (string.IsNullOrWhiteSpace(dto.Feature))
        {
            throw new ContentLoadException(path,
                "'laser.feature' musí odkazovat na funkci z features.json — bez brány by laser platil od první minuty.");
        }

        return new LaserConfig(dto.HarvestsPerSecond, dto.RadiusTiles, dto.Feature.Trim());
    }

    /// <summary>
    /// Nastavení hromadné stavby. Chybí-li blok, platí výchozí násobiče — starší
    /// data se načtou a hráč o funkci nepřijde.
    /// </summary>
    private static BulkBuildConfig? ParseBulkBuild(string path, BulkBuildDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.MaxPerAction is < 1 or > 10_000)
        {
            throw new ContentLoadException(path,
                $"'bulkBuild.maxPerAction' musí být 1–10000, je {dto.MaxPerAction}.");
        }

        var batches = dto.Batches;
        if (batches is null || batches.Count == 0)
        {
            throw new ContentLoadException(path, "'bulkBuild.batches' nesmí být prázdné.");
        }

        for (int i = 0; i < batches.Count; i++)
        {
            if (batches[i] < 1)
            {
                throw new ContentLoadException(path,
                    $"'bulkBuild.batches' smí obsahovat jen kladná čísla, je tam {batches[i]}.");
            }

            // Rostoucí řada: lišta násobičů se čte zleva doprava a přeházená čísla
            // by z ní udělaly hádanku.
            if (i > 0 && batches[i] <= batches[i - 1])
            {
                throw new ContentLoadException(path,
                    $"'bulkBuild.batches' musí růst, {batches[i]} následuje po {batches[i - 1]}.");
            }
        }

        return new BulkBuildConfig(batches.ToArray(), dto.MaxPerAction);
    }

    /// <summary>
    /// Nastavení znečištění. Chybí-li blok, je vrstva vypnutá a krajina se chová
    /// jako dřív — nic se nekazí a čističky nemají co dělat.
    /// </summary>
    private static PollutionConfig? ParsePollution(string path, PollutionDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.IntervalTicks is < 1 or > 100_000)
        {
            throw new ContentLoadException(path,
                $"'pollution.intervalTicks' musí být 1–100000, je {dto.IntervalTicks}.");
        }

        // Rozliv nad 1 by z buňky odsál víc, než v ní je — hodnoty by se rozešly
        // do záporu a mechanika by tiše přestala dávat smysl.
        if (dto.SpreadRate is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'pollution.spreadRate' musí být 0–1, je {dto.SpreadRate}.");
        }

        if (dto.DecayRate is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'pollution.decayRate' musí být 0–1, je {dto.DecayRate}.");
        }

        if (dto.FullEffectAt <= 0)
        {
            throw new ContentLoadException(path,
                $"'pollution.fullEffectAt' musí být větší než 0, je {dto.FullEffectAt}.");
        }

        if (dto.HappinessPenalty is < 0 or > 1)
        {
            throw new ContentLoadException(path,
                $"'pollution.happinessPenalty' musí být 0–1, je {dto.HappinessPenalty}.");
        }

        // Plný trest 1.0 by zamořenou budovu úplně zastavil. Znečištění má brzdit,
        // ne zabíjet — jinak by hráč přišel o výrobu dřív, než stihne postavit čističku.
        if (dto.ProductionPenalty is < 0 or >= 1)
        {
            throw new ContentLoadException(path,
                $"'pollution.productionPenalty' musí být 0 až <1, je {dto.ProductionPenalty}.");
        }

        return new PollutionConfig(
            dto.IntervalTicks,
            dto.SpreadRate,
            dto.DecayRate,
            dto.FullEffectAt,
            dto.HappinessPenalty,
            dto.ProductionPenalty);
    }

    /// <summary>
    /// Nastavení klikacího komba. Chybí-li blok, je vrstva vypnutá a klikání se
    /// chová jako dřív.
    /// </summary>
    private static ComboConfig? ParseCombo(string path, ComboDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.WindowSeconds is <= 0 or > 60)
        {
            throw new ContentLoadException(path, $"'combo.windowSeconds' musí být větší než 0 a nejvýš 60, je {dto.WindowSeconds}.");
        }

        if (dto.BonusPerStep is <= 0 or > 1)
        {
            throw new ContentLoadException(path, $"'combo.bonusPerStep' musí být větší než 0 a nejvýš 1, je {dto.BonusPerStep}.");
        }

        if (dto.MaxSteps is < 1 or > 100)
        {
            throw new ContentLoadException(path, $"'combo.maxSteps' musí být 1–100, je {dto.MaxSteps}.");
        }

        return new ComboConfig(dto.WindowSeconds, dto.BonusPerStep, dto.MaxSteps);
    }

    /// <summary>
    /// Nastavení nástrojů. Chybí-li blok, je vrstva vypnutá a nástroje zůstávají
    /// jednorázovou měnou jako dřív — starší data se načtou beze změny chování.
    /// </summary>
    private static ToolsConfig? ParseTools(string path, ToolsDto? dto, DefRegistry<Resource> resources)
    {
        if (dto is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(dto.Resource) || !resources.TryIndexOf(dto.Resource.Trim(), out int index))
        {
            throw new ContentLoadException(path, $"'tools.resource' odkazuje na neexistující surovinu '{dto.Resource}'.");
        }

        if (dto.PerPerson <= 0)
        {
            throw new ContentLoadException(path, $"'tools.perPerson' musí být kladné, je {dto.PerPerson}.");
        }

        if (dto.WearPerWorkerPerSecond < 0)
        {
            throw new ContentLoadException(path, "'tools.wearPerWorkerPerSecond' nesmí být záporné.");
        }

        // Bonus bez opotřebení by znamenal, že se nástroje jednou vyrobí a navždy
        // platí — přesně ta slepá větev, kvůli které tahle vrstva vznikla.
        if (dto.WearPerWorkerPerSecond <= 0 && (dto.ProductionBonus > 0 || dto.HarvestBonus > 0))
        {
            throw new ContentLoadException(path,
                "'tools' dávají bonus, ale nemají opotřebení — nástroje by se vyrobily jednou a platily navždy.");
        }

        if (dto.ProductionBonus is < 0 or > 10 || dto.HarvestBonus is < 0 or > 10)
        {
            throw new ContentLoadException(path, "'tools.productionBonus' i 'tools.harvestBonus' musí být 0–10.");
        }

        return new ToolsConfig(index, dto.PerPerson, dto.WearPerWorkerPerSecond, dto.ProductionBonus, dto.HarvestBonus);
    }

    /// <summary>
    /// Nastavení svozu do skladu. Chybí-li blok, je vrstva vypnutá a hra se chová
    /// jako dřív (zboží se „teleportuje") — starší data se načtou beze změny.
    /// </summary>
    private static HaulConfig? ParseHaul(string path, HaulDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.FreeDistance < 0)
        {
            throw new ContentLoadException(path, $"'haul.freeDistance' nesmí být záporná, je {dto.FreeDistance}.");
        }

        if (dto.Range <= 0)
        {
            throw new ContentLoadException(path, $"'haul.range' musí být kladný, je {dto.Range}.");
        }

        if (dto.MinMultiplier is <= 0 or > 1)
        {
            throw new ContentLoadException(path,
                $"'haul.minMultiplier' musí být větší než 0 a nejvýš 1, je {dto.MinMultiplier}.");
        }

        return new HaulConfig(dto.FreeDistance, dto.Range, dto.MinMultiplier);
    }

    /// <summary>
    /// Nastavení přidělování dělníků. Chybí-li blok, platí výchozí práh —
    /// starší data se načtou beze změny chování.
    /// </summary>
    private static StaffingConfig? ParseStaffing(string path, StaffingDto? dto)
    {
        if (dto is null)
        {
            return null;
        }

        if (dto.ScarcityThreshold is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'staffing.scarcityThreshold' musí být 0–1, je {dto.ScarcityThreshold}.");
        }

        return new StaffingConfig(dto.ScarcityThreshold);
    }

    /// <summary>
    /// Nastavení spokojenosti. Chybí-li blok v datech, je vrstva vypnutá — starší
    /// obsah (a testy) se tak chová jako dřív.
    /// </summary>
    private static HappinessConfig? ParseHappiness(string path, HappinessDto? dto)
    {
        if (dto is null || dto.IntervalTicks <= 0)
        {
            return null;
        }

        if (dto.BaseHappiness is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'happiness.baseHappiness' musí být 0–1, je {dto.BaseHappiness}.");
        }

        if (dto.ServiceWeight is < 0 or > 1 || dto.OvercrowdingPenalty is < 0 or > 1)
        {
            throw new ContentLoadException(path, "'happiness.serviceWeight' i 'overcrowdingPenalty' musí být 0–1.");
        }

        if (dto.PeoplePerServicePoint <= 0)
        {
            throw new ContentLoadException(path,
                $"'happiness.peoplePerServicePoint' musí být kladné, je {dto.PeoplePerServicePoint}.");
        }

        if (dto.GrowthFloor is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"'happiness.growthFloor' musí být 0–1, je {dto.GrowthFloor}.");
        }

        if (dto.FreePopulation < 0)
        {
            throw new ContentLoadException(path, $"'happiness.freePopulation' nesmí být záporné, je {dto.FreePopulation}.");
        }

        return new HappinessConfig(
            dto.IntervalTicks, dto.BaseHappiness, dto.ServiceWeight,
            dto.OvercrowdingPenalty, dto.PeoplePerServicePoint, dto.GrowthFloor, dto.FreePopulation);
    }

    // ----- devlog (volitelný obsah menu) -----

    private IReadOnlyList<DevlogEntry> LoadDevlog(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<DevlogEntry>(); // deník je volitelný — jeho absence hru neblokuje
        }

        var file = ReadFile<DevlogFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var entries = new List<DevlogEntry>();
        foreach (var dto in file.Entries ?? new List<DevlogEntryDto>())
        {
            // Text deníku žije v jazycích (aby byl i anglicky) — data drží jen ID,
            // datum a počet řádků.
            string id = RequireId(path, dto.Id, "Záznam deníku");
            if (dto.LineCount is < 1 or > 50)
            {
                throw new ContentLoadException(path, $"Záznam deníku '{id}': 'lineCount' musí být 1–50, je {dto.LineCount}.");
            }

            entries.Add(new DevlogEntry(id, dto.Date?.Trim() ?? string.Empty, dto.LineCount));
        }

        return entries;
    }

    // ----- dekorace a fauna (živá mapa) -----

    private IReadOnlyList<DecorationDef> LoadDecorations(string path, BiomeRegistry biomes)
    {
        var file = ReadFile<DecorationsFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var result = new List<DecorationDef>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Decorations ?? new List<DecorationDto>())
        {
            string id = RequireId(path, dto.Id, "Dekorace");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID dekorace '{id}'.");
            }

            if (dto.Density is <= 0 or > 0.6)
            {
                throw new ContentLoadException(path, $"Dekorace '{id}': 'density' musí být v (0, 0.6], je {dto.Density}.");
            }

            if (dto.MinSize is < 1 or > 8 || dto.MaxSize is < 1 or > 8 || dto.MinSize > dto.MaxSize)
            {
                throw new ContentLoadException(path, $"Dekorace '{id}': velikosti musí splňovat 1 ≤ minSize ≤ maxSize ≤ 8.");
            }

            if (dto.Colors is not { Length: > 0 })
            {
                throw new ContentLoadException(path, $"Dekorace '{id}' nemá žádnou barvu ('colors').");
            }

            var colors = new List<RgbColor>(dto.Colors.Length);
            foreach (var colorText in dto.Colors)
            {
                colors.Add(ParseColor(path, colorText, $"Dekorace '{id}'"));
            }

            result.Add(new DecorationDef(
                id, ParseBiomeMask(path, $"Dekorace '{id}'", dto.Biomes, biomes),
                colors, (float)dto.Density, dto.MinSize, dto.MaxSize));
        }

        return result;
    }

    private IReadOnlyList<FaunaDef> LoadFauna(string path, BiomeRegistry biomes)
    {
        var file = ReadFile<FaunaFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var result = new List<FaunaDef>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Fauna ?? new List<FaunaDto>())
        {
            string id = RequireId(path, dto.Id, "Fauna");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID fauny '{id}'.");
            }

            var color = ParseColor(path, dto.Color, $"Fauna '{id}'");
            if (dto.Size is < 1 or > 8)
            {
                throw new ContentLoadException(path, $"Fauna '{id}': 'size' musí být 1–8, je {dto.Size}.");
            }

            if (dto.Speed is <= 0 or > 500)
            {
                throw new ContentLoadException(path, $"Fauna '{id}': 'speed' musí být v (0, 500], je {dto.Speed}.");
            }

            var time = dto.TimeOfDay?.Trim().ToLowerInvariant() switch
            {
                null or "any" => FaunaTime.Any,
                "day" => FaunaTime.Day,
                "night" => FaunaTime.Night,
                _ => throw new ContentLoadException(path, $"Fauna '{id}': 'timeOfDay' musí být 'day', 'night' nebo 'any', je '{dto.TimeOfDay}'."),
            };

            result.Add(new FaunaDef(
                id, ParseBiomeMask(path, $"Fauna '{id}'", dto.Biomes, biomes),
                color, dto.Size, (float)dto.Speed, time, dto.Glow));
        }

        return result;
    }

    /// <summary>
    /// Vozidla pro dopravu po silnicích. Soubor je volitelný — bez něj se prostě
    /// nic nehýbe a hra běží jako dřív (kulisa nesmí být podmínkou spuštění).
    /// </summary>
    private IReadOnlyList<VehicleDef> LoadVehicles(string path)
    {
        if (!File.Exists(path))
        {
            return Array.Empty<VehicleDef>();
        }

        var file = ReadFile<VehiclesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        var result = new List<VehicleDef>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Vehicles ?? new List<VehicleDto>())
        {
            string id = RequireId(path, dto.Id, "Vozidlo");
            if (!seenIds.Add(id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID vozidla '{id}'.");
            }

            var color = ParseColor(path, dto.Color, $"Vozidlo '{id}'");
            if (dto.Width is < 1 or > 16)
            {
                throw new ContentLoadException(path, $"Vozidlo '{id}': 'width' musí být 1–16, je {dto.Width}.");
            }

            if (dto.Length is < 1 or > 32)
            {
                throw new ContentLoadException(path, $"Vozidlo '{id}': 'length' musí být 1–32, je {dto.Length}.");
            }

            if (dto.Speed is <= 0 or > 500)
            {
                throw new ContentLoadException(path, $"Vozidlo '{id}': 'speed' musí být v (0, 500], je {dto.Speed}.");
            }

            if (dto.MinEra < 0)
            {
                throw new ContentLoadException(path, $"Vozidlo '{id}': 'minEra' nesmí být záporná, je {dto.MinEra}.");
            }

            // Chybějící maxEra = jezdí navždy; převrácený rozsah je tichá chyba
            // obsahu — vozidlo by se nikdy neobjevilo.
            int maxEra = dto.MaxEra ?? -1;
            if (maxEra >= 0 && maxEra < dto.MinEra)
            {
                throw new ContentLoadException(path,
                    $"Vozidlo '{id}': 'maxEra' ({maxEra}) je menší než 'minEra' ({dto.MinEra}) — nikdy by nejezdilo.");
            }

            result.Add(new VehicleDef(id, color, dto.Width, dto.Length, (float)dto.Speed, dto.MinEra, maxEra, dto.Glow));
        }

        return result;
    }

    private static bool[] ParseBiomeMask(string path, string owner, string[]? biomeIds, BiomeRegistry biomes)
    {
        if (biomeIds is not { Length: > 0 })
        {
            throw new ContentLoadException(path, $"{owner} nemá vyplněné 'biomes'.");
        }

        var mask = new bool[biomes.Count];
        foreach (var biomeId in biomeIds)
        {
            if (biomeId is null || !biomes.TryIndexOf(biomeId.Trim(), out int index))
            {
                throw new ContentLoadException(path, $"{owner} odkazuje v 'biomes' na neexistující biom '{biomeId}'.");
            }

            mask[index] = true;
        }

        return mask;
    }

    // ----- jména osad -----

    private IReadOnlyList<string> LoadSettlementNames(string path)
    {
        var file = ReadFile<SettlementNamesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Names is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádné jméno osady ('names').");
        }

        if (file.Names.Count > 10_000)
        {
            throw new ContentLoadException(path, $"Příliš mnoho jmen osad ({file.Names.Count}), maximum je 10000.");
        }

        var names = new List<string>(file.Names.Count);
        for (int i = 0; i < file.Names.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(file.Names[i]))
            {
                throw new ContentLoadException(path, $"Jméno osady na pozici {i} je prázdné.");
            }

            names.Add(file.Names[i].Trim());
        }

        return names;
    }

    // ----- jazyky -----

    private DefRegistry<LanguageDef> LoadLanguages(
        string langDirectory,
        BiomeRegistry biomes,
        DefRegistry<Resource> resources,
        DefRegistry<BuildingDef> buildings,
        WorldGenCatalog worldGen,
        DefRegistry<TechDef> techs,
        DefRegistry<PrestigeUpgradeDef> prestigeUpgrades,
        DefRegistry<PrestigeUpgradeDef> legacyUpgrades,
        DefRegistry<QuestDef> quests,
        DefRegistry<AchievementDef> achievements,
        DefRegistry<EventDef> events,
        DefRegistry<EraDef> eras,
        DefRegistry<ZoneTypeDef> zoneTypes,
        DefRegistry<GrowthPolicyDef> policies,
        DefRegistry<AscensionTierDef> tiers,
        DefRegistry<WeatherDef> weather,
        DefRegistry<LandmarkDef> landmarks,
        DefRegistry<FeatureDef> features,
        IReadOnlyList<DevlogEntry> devlog,
        DefRegistry<TerraformDef> terraform,
        IReadOnlyList<TutorialStepDef> tutorial,
        ChallengeCatalog challenges,
        ContractCatalog contracts,
        DistrictCatalog districts,
        SettlementRankLadder settlementRanks,
        CitizenCatalog citizens,
        ElectionConfig elections,
        IReadOnlyList<MilestoneDef> milestones,
        SeasonCalendar seasons)
    {
        if (!Directory.Exists(langDirectory))
        {
            throw new ContentLoadException(langDirectory, "Složka s jazyky 'data/lang' neexistuje.");
        }

        // Řazení podle jména souboru → deterministické pořadí jazyků v menu.
        var files = Directory.GetFiles(langDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
        {
            throw new ContentLoadException(langDirectory, "Ve složce 'data/lang' není žádný jazyk (*.json).");
        }

        var languages = new List<LanguageDef>(files.Length);
        foreach (var file in files)
        {
            var dto = ReadFile<LanguageFileDto>(file);
            CheckSchemaVersion(file, dto.SchemaVersion);

            string id = RequireId(file, dto.Id, "Jazyk");
            if (string.IsNullOrWhiteSpace(dto.NativeName))
            {
                throw new ContentLoadException(file, $"Jazyk '{id}' nemá vyplněné 'nativeName'.");
            }

            if (dto.Strings is not { Count: > 0 })
            {
                throw new ContentLoadException(file, $"Jazyk '{id}' nemá žádné řetězce ('strings').");
            }

            languages.Add(new LanguageDef(id, dto.NativeName.Trim(), dto.Strings));
        }

        ValidateContentKeys(langDirectory, languages[0], biomes, resources, buildings, worldGen, techs, prestigeUpgrades, legacyUpgrades, quests, achievements, events, eras, zoneTypes, policies, tiers, weather, landmarks, features, devlog, terraform, tutorial, challenges, contracts, districts, settlementRanks, citizens, elections, milestones, seasons);
        FillGapsFromBaseLanguage(langDirectory, languages);
        return new DefRegistry<LanguageDef>(languages, l => l.Id, "jazyk");
    }

    /// <summary>Každý kus obsahu musí mít jméno — kontroluje se první jazyk, shodu sad řeší <see cref="ValidateKeySetsMatch"/>.</summary>
    private static void ValidateContentKeys(
        string langDirectory,
        LanguageDef language,
        BiomeRegistry biomes,
        DefRegistry<Resource> resources,
        DefRegistry<BuildingDef> buildings,
        WorldGenCatalog worldGen,
        DefRegistry<TechDef> techs,
        DefRegistry<PrestigeUpgradeDef> prestigeUpgrades,
        DefRegistry<PrestigeUpgradeDef> legacyUpgrades,
        DefRegistry<QuestDef> quests,
        DefRegistry<AchievementDef> achievements,
        DefRegistry<EventDef> events,
        DefRegistry<EraDef> eras,
        DefRegistry<ZoneTypeDef> zoneTypes,
        DefRegistry<GrowthPolicyDef> policies,
        DefRegistry<AscensionTierDef> tiers,
        DefRegistry<WeatherDef> weather,
        DefRegistry<LandmarkDef> landmarks,
        DefRegistry<FeatureDef> features,
        IReadOnlyList<DevlogEntry> devlog,
        DefRegistry<TerraformDef> terraform,
        IReadOnlyList<TutorialStepDef> tutorial,
        ChallengeCatalog challenges,
        ContractCatalog contracts,
        DistrictCatalog districts,
        SettlementRankLadder settlementRanks,
        CitizenCatalog citizens,
        ElectionConfig elections,
        IReadOnlyList<MilestoneDef> milestones,
        SeasonCalendar seasons)
    {
        var required = new List<string>();
        required.AddRange(biomes.All.Select(b => b.NameKey));
        required.AddRange(resources.All.Select(r => r.NameKey));
        required.AddRange(buildings.All.Select(b => b.NameKey));
        required.AddRange(worldGen.Sizes.Select(s => s.NameKey));
        required.AddRange(worldGen.Presets.Select(p => p.NameKey));
        required.AddRange(techs.All.Select(t => t.NameKey));
        required.AddRange(techs.All.Select(t => t.DescriptionKey));
        required.AddRange(prestigeUpgrades.All.Select(u => u.NameKey));
        required.AddRange(prestigeUpgrades.All.Select(u => u.DescriptionKey));
        required.AddRange(legacyUpgrades.All.Select(u => u.NameKey));
        required.AddRange(legacyUpgrades.All.Select(u => u.DescriptionKey));
        required.AddRange(quests.All.Select(q => q.NameKey));
        required.AddRange(quests.All.Select(q => q.DescriptionKey));
        required.AddRange(achievements.All.Select(a => a.NameKey));
        required.AddRange(achievements.All.Select(a => a.DescriptionKey));
        foreach (var gameEvent in events.All)
        {
            required.Add(gameEvent.NameKey);
            required.Add(gameEvent.DescriptionKey);
            required.AddRange(gameEvent.Choices.Select(c => c.LabelKey));
        }

        required.AddRange(eras.All.Select(e => e.NameKey));
        required.AddRange(zoneTypes.All.Select(z => z.NameKey));
        required.AddRange(policies.All.Select(p => p.NameKey));
        required.AddRange(policies.All.Select(p => p.DescriptionKey));
        required.AddRange(terraform.All.Select(t => t.NameKey));
        required.AddRange(terraform.All.Select(t => t.DescriptionKey));
        foreach (var entry in devlog)
        {
            required.Add(entry.TitleKey);
            for (int i = 0; i < entry.LineCount; i++)
            {
                required.Add(entry.LineKey(i));
            }
        }
        required.AddRange(tiers.All.Select(t => t.NameKey));
        required.AddRange(weather.All.Select(w => w.NameKey));
        required.AddRange(landmarks.All.Select(l => l.NameKey));
        required.AddRange(features.All.Select(f => f.NameKey));
        required.AddRange(tutorial.Select(t => t.NameKey));
        required.AddRange(tutorial.Select(t => t.HintKey));
        required.AddRange(challenges.Challenges.Select(c => c.NameKey));
        required.AddRange(challenges.Challenges.Select(c => c.DescriptionKey));
        required.AddRange(contracts.Contracts.All.Select(c => c.NameKey));
        required.AddRange(districts.Types.All.Select(d => d.NameKey));
        required.AddRange(settlementRanks.Ranks.Select(r => r.NameKey));
        required.AddRange(citizens.Requests.All.Select(r => r.TextKey));
        required.AddRange(elections.Candidates.Select(c => c.NameKey));
        required.AddRange(elections.Candidates.Select(c => c.DescriptionKey));
        required.AddRange(milestones.Select(m => m.NameKey));
        required.AddRange(seasons.Seasons.Select(x => x.NameKey));
        required.AddRange(seasons.Seasons.Select(x => x.DescriptionKey));

        var missing = required.Where(key => !language.Strings.ContainsKey(key)).ToList();
        if (missing.Count > 0)
        {
            throw new ContentLoadException(
                langDirectory,
                $"Jazyku '{language.Id}' chybí jména obsahu: {string.Join(", ", missing.Take(10))}" +
                (missing.Count > 10 ? $" (+{missing.Count - 10} dalších)" : string.Empty));
        }
    }

    /// <summary>Všechny jazyky musí mít stejnou sadu klíčů — chybějící překlad se pozná při startu, ne ve hře.</summary>
    /// <summary>
    /// Doplní částečným jazykům chybějící klíče ze základního a spočítá pokrytí.
    ///
    /// <para>Klíč navíc (ve jazyce, ale ne v základu) je pořád chyba: je to skoro
    /// vždycky překlep, na který by se jinak nikdy nepřišlo, protože hra si takový
    /// řetězec nikdy nevyžádá.</para>
    /// </summary>
    private static void FillGapsFromBaseLanguage(string langDirectory, List<LanguageDef> languages)
    {
        var reference = languages[0];
        for (int i = 1; i < languages.Count; i++)
        {
            var language = languages[i];
            var extra = language.Strings.Keys.Where(k => !reference.Strings.ContainsKey(k)).ToList();
            if (extra.Count > 0)
            {
                throw new ContentLoadException(
                    langDirectory,
                    $"Jazyk '{language.Id}' má klíče, které základní jazyk '{reference.Id}' nezná — "
                    + $"{string.Join(", ", extra.Take(8))}{(extra.Count > 8 ? " …" : string.Empty)}. "
                    + "Nejspíš překlep: hra si takový řetězec nikdy nevyžádá.");
            }

            var merged = new Dictionary<string, string>(reference.Strings, StringComparer.Ordinal);
            int translated = 0;
            foreach (var pair in language.Strings)
            {
                merged[pair.Key] = pair.Value;
                translated++;
            }

            languages[i] = language with
            {
                Strings = merged,
                Coverage = reference.Strings.Count == 0 ? 1.0 : translated / (double)reference.Strings.Count,
            };
        }
    }

    // ----- worldgen -----

    private WorldGenCatalog LoadWorldGen(string path, BiomeRegistry biomes)
    {
        var file = ReadFile<WorldGenFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Sizes is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádnou velikost světa ('sizes').");
        }

        if (file.Presets is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádný preset generátoru ('presets').");
        }

        var sizes = new List<WorldSize>(file.Sizes.Count);
        var sizeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Sizes)
        {
            sizes.Add(ValidateSize(path, dto, sizeIds));
        }

        var presets = new List<TerrainPreset>(file.Presets.Count);
        var presetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Presets)
        {
            presets.Add(ValidatePreset(path, dto, presetIds, biomes));
        }

        int defaultSize = ResolveDefault(path, "defaultSize", file.DefaultSize, sizes, s => s.Id);
        int defaultPreset = ResolveDefault(path, "defaultPreset", file.DefaultPreset, presets, p => p.Id);
        return new WorldGenCatalog(sizes, presets, defaultSize, defaultPreset);
    }

    private static WorldSize ValidateSize(string path, WorldSizeDto dto, HashSet<string> seenIds)
    {
        string id = RequireId(path, dto.Id, "Velikost světa");
        if (!seenIds.Add(id))
        {
            throw new ContentLoadException(path, $"Duplicitní ID velikosti světa '{id}'.");
        }

        if (dto.Width is < 16 or > 4096 || dto.Height is < 16 or > 4096)
        {
            throw new ContentLoadException(path, $"Velikost světa '{id}': rozměry {dto.Width}×{dto.Height} musí být v rozsahu 16–4096.");
        }

        return new WorldSize(id, dto.Width, dto.Height);
    }

    private static TerrainPreset ValidatePreset(string path, TerrainPresetDto dto, HashSet<string> seenIds, BiomeRegistry biomes)
    {
        string id = RequireId(path, dto.Id, "Preset generátoru");
        if (!seenIds.Add(id))
        {
            throw new ContentLoadException(path, $"Duplicitní ID presetu '{id}'.");
        }

        if (dto.SeaLevel is <= 0 or >= 1)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'seaLevel' musí být mezi 0 a 1 (bez krajů), je {dto.SeaLevel}.");
        }

        if (string.IsNullOrWhiteSpace(dto.FallbackBiome))
        {
            throw new ContentLoadException(path, $"Preset '{id}' nemá vyplněný 'fallbackBiome'.");
        }

        if (!biomes.TryIndexOf(dto.FallbackBiome.Trim(), out var fallbackIndex))
        {
            throw new ContentLoadException(path, $"Preset '{id}' odkazuje na neexistující biom '{dto.FallbackBiome}' ve 'fallbackBiome'.");
        }

        if (biomes[fallbackIndex].IsWater)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'fallbackBiome' ('{dto.FallbackBiome}') musí být pevninský biom, ne vodní.");
        }

        var elevation = ValidateNoise(path, id, "elevationNoise", dto.ElevationNoise);
        var moisture = ValidateNoise(path, id, "moistureNoise", dto.MoistureNoise);

        // Řeky jsou volitelné: bez 'riverNoise' (nebo s nulovou šířkou) se negenerují.
        NoiseSpec? river = dto.RiverNoise is null ? null : ValidateNoise(path, id, "riverNoise", dto.RiverNoise);
        if (dto.RiverWidth is < 0 or > 0.2)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'riverWidth' musí být 0–0.2, je {dto.RiverWidth}.");
        }

        if (dto.RiverMaxElevation is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'riverMaxElevation' musí být 0–1, je {dto.RiverMaxElevation}.");
        }

        // Klima je volitelné: bez 'temperatureNoise' se biomy vybírají jen podle
        // výšky a vlhkosti (starší obsah tak zůstává platný).
        NoiseSpec? temperature = dto.TemperatureNoise is null
            ? null
            : ValidateNoise(path, id, "temperatureNoise", dto.TemperatureNoise);

        if (dto.TemperatureBandTiles < 0)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'temperatureBandTiles' nesmí být záporné, je {dto.TemperatureBandTiles}.");
        }

        if (dto.TemperatureLapse is < 0 or > 1)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'temperatureLapse' musí být 0–1, je {dto.TemperatureLapse}.");
        }

        int riverBiome = -1;
        if (!string.IsNullOrWhiteSpace(dto.RiverBiome))
        {
            if (!biomes.TryIndexOf(dto.RiverBiome.Trim(), out riverBiome))
            {
                throw new ContentLoadException(path, $"Preset '{id}' odkazuje na neexistující biom '{dto.RiverBiome}' v 'riverBiome'.");
            }

            if (!biomes[riverBiome].IsWater)
            {
                throw new ContentLoadException(path, $"Preset '{id}': 'riverBiome' ('{dto.RiverBiome}') musí být vodní biom.");
            }
        }

        float riverMaxElevation = dto.RiverMaxElevation <= 0 ? 1f : (float)dto.RiverMaxElevation;
        return new TerrainPreset(
            id, (float)dto.SeaLevel, fallbackIndex, elevation, moisture,
            river, (float)dto.RiverWidth, riverMaxElevation,
            temperature, (float)dto.TemperatureBandTiles, (float)dto.TemperatureLapse, riverBiome);
    }

    private static NoiseSpec ValidateNoise(string path, string presetId, string field, NoiseDto? dto)
    {
        if (dto is null)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}' nemá vyplněný blok '{field}'.");
        }

        if (dto.Frequency is <= 0 or > 100)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'frequency' musí být v (0, 100], je {dto.Frequency}.");
        }

        if (dto.Octaves is < 1 or > 10)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'octaves' musí být 1–10, je {dto.Octaves}.");
        }

        if (dto.Persistence is <= 0 or > 1)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'persistence' musí být v (0, 1], je {dto.Persistence}.");
        }

        if (dto.Lacunarity is < 1 or > 8)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'lacunarity' musí být 1–8, je {dto.Lacunarity}.");
        }

        return new NoiseSpec((float)dto.Frequency, dto.Octaves, (float)dto.Persistence, (float)dto.Lacunarity);
    }

    // ----- společné pomůcky -----

    private static int ResolveDefault<T>(string path, string field, string? id, List<T> items, Func<T, string> idSelector)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        var wanted = id.Trim();
        for (int i = 0; i < items.Count; i++)
        {
            if (idSelector(items[i]) == wanted)
            {
                return i;
            }
        }

        throw new ContentLoadException(path, $"'{field}' odkazuje na neexistující ID '{id}'.");
    }

    private static string RequireId(string path, string? id, string what)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ContentLoadException(path, $"{what} nemá vyplněné 'id'.");
        }

        return id.Trim();
    }

    private static RgbColor ParseColor(string path, string? value, string owner)
    {
        if (!RgbColor.TryParse(value, out var color))
        {
            throw new ContentLoadException(path, $"{owner} má neplatnou barvu 'mapColor' = '{value}' (očekávám '#RRGGBB').");
        }

        return color;
    }

    private static void CheckSchemaVersion(string path, int version)
    {
        if (version != SupportedSchemaVersion)
        {
            throw new ContentLoadException(path, $"Nepodporovaná verze schématu {version}, tato verze hry rozumí verzi {SupportedSchemaVersion}.");
        }
    }

    /// <summary>
    /// Načte datový soubor a navrství na něj stejnojmenné soubory z modů.
    ///
    /// <para>Tohle je jediné místo, kudy do hry tečou data — proto tu vrstvení
    /// modů stojí. Kdyby se řešilo v každé <c>Load*</c> metodě zvlášť, půlka
    /// obsahu by se moddovat nedala a nikdo by nepřišel na to proč.</para>
    /// </summary>
    private T ReadFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new ContentLoadException(path, "Soubor nenalezen.");
        }

        string text = File.ReadAllText(path);
        var overlays = OverlaysFor(path);

        try
        {
            string json = JsonOverlay.Merge(text, overlays);
            var parsed = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return parsed ?? throw new ContentLoadException(path, "Soubor obsahuje jen 'null'.");
        }
        catch (JsonException ex)
        {
            // Výpis provinilého řádku je tu proto, že hlášku od .NET nikdo
            // nepřečte jako návod. „Expected either ',', '}', or ']'" je pravda,
            // ale co s tím, pozná až ten, kdo ten řádek vidí — a přes celý
            // soubor o půldruhém tisíci řádcích ho hledá zbytečně dlouho.
            //
            // Čárka chybí na řádku PŘED tím, který parser ohlásí, takže se
            // ukazují oba.
            string excerpt = overlays.Count == 0 ? Excerpt(text, ex.LineNumber) : string.Empty;
            throw new ContentLoadException(path, $"Neplatný JSON: {ex.Message}{excerpt}");
        }
    }

    /// <summary>
    /// Provinilý řádek a ten před ním, očíslované od jedné (parser čísluje od nuly).
    /// Prázdný řetězec, když se řádek určit nedá.
    /// </summary>
    private static string Excerpt(string text, long? zeroBasedLine)
    {
        if (zeroBasedLine is not { } line || line < 0)
        {
            return string.Empty;
        }

        var lines = text.Split('\n');
        int index = (int)Math.Min(line, lines.Length - 1);
        int from = Math.Max(0, index - 1);

        var builder = new System.Text.StringBuilder();
        for (int i = from; i <= index; i++)
        {
            builder.Append(Environment.NewLine)
                .Append("  řádek ").Append(i + 1).Append(": ")
                .Append(lines[i].TrimEnd('\r'))
                .Append(i == index ? "   ← tady" : string.Empty);
        }

        return builder.ToString();
    }

    /// <summary>Obsah stejnojmenných souborů z modů, v pořadí uplatnění.</summary>
    private List<string> OverlaysFor(string path)
    {
        var overlays = new List<string>();
        if (_mods.Count == 0)
        {
            return overlays;
        }

        // Modová cesta je stejná jako základní, jen s jiným kořenem — díky tomu
        // se modu nedá omylem podstrčit soubor odjinud.
        string fileName = Path.GetFileName(path);
        string parent = Path.GetFileName(Path.GetDirectoryName(path) ?? string.Empty);
        bool inSubfolder = parent == "lang";

        foreach (var mod in _mods)
        {
            string candidate = inSubfolder
                ? Path.Combine(mod.Directory, parent, fileName)
                : Path.Combine(mod.Directory, fileName);
            if (File.Exists(candidate))
            {
                overlays.Add(File.ReadAllText(candidate));
            }
        }

        return overlays;
    }
}
