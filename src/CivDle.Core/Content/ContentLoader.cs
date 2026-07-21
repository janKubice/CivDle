using System.Text.Json;
using CivDle.Core.Content.Dto;
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

    /// <summary>Načte kompletní herní obsah ze složky s daty.</summary>
    public GameContent LoadFrom(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            throw new ContentLoadException(dataDirectory, $"Složka s herními daty '{dataDirectory}' neexistuje.");
        }

        // Suroviny první — odkazují na ně biomy (clickYield) i budovy (ceny, recepty).
        var resources = LoadResources(Path.Combine(dataDirectory, "resources.json"));
        var biomes = LoadBiomes(Path.Combine(dataDirectory, "biomes.json"), resources);
        var buildings = LoadBuildings(Path.Combine(dataDirectory, "buildings.json"), biomes, resources);
        var techs = LoadTech(Path.Combine(dataDirectory, "tech.json"), buildings, resources);
        var (prestige, prestigeUpgrades) = LoadPrestige(Path.Combine(dataDirectory, "prestige.json"), resources, buildings, techs);
        var worldGen = LoadWorldGen(Path.Combine(dataDirectory, "worldgen.json"), biomes);
        var gameplay = LoadGameplay(Path.Combine(dataDirectory, "gameplay.json"), resources);
        var languages = LoadLanguages(Path.Combine(dataDirectory, "lang"), biomes, resources, buildings, worldGen, techs, prestigeUpgrades);
        var settlementNames = LoadSettlementNames(Path.Combine(dataDirectory, "settlement-names.json"));
        var decorations = LoadDecorations(Path.Combine(dataDirectory, "decorations.json"), biomes);
        var fauna = LoadFauna(Path.Combine(dataDirectory, "fauna.json"), biomes);
        var devlog = LoadDevlog(Path.Combine(dataDirectory, "devlog.json"));

        return new GameContent(
            biomes, resources, buildings, techs, prestige, prestigeUpgrades,
            worldGen, gameplay, languages, settlementNames, decorations, fauna, devlog);
    }

    // ----- biomy -----

    private static BiomeRegistry LoadBiomes(string path, DefRegistry<Resource> resources)
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

            clickYield = new ClickYield(resourceIndex, dto.ClickYield.Amount);
        }

        return new Biome(id, color, (float)dto.ColorVariation, dto.IsWater, depth, elevation, moisture, clickYield);
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

    private static DefRegistry<Resource> LoadResources(string path)
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

    private static DefRegistry<BuildingDef> LoadBuildings(string path, BiomeRegistry biomes, DefRegistry<Resource> resources)
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
            buildings.Add(ValidateBuilding(path, file.Buildings[i], i, biomes, resources, idToIndex));
        }

        // Vylepšení musí mít stejný půdorys (mění se na místě) — kontrola po sestavení.
        foreach (var building in buildings)
        {
            if (building.HasUpgrade)
            {
                var target = buildings[building.UpgradesToIndex];
                if (target.FootprintWidth != building.FootprintWidth || target.FootprintHeight != building.FootprintHeight)
                {
                    throw new ContentLoadException(path, $"Budova '{building.Id}': vylepšení '{target.Id}' má jiný půdorys (vylepšuje se na místě).");
                }
            }
        }

        return new DefRegistry<BuildingDef>(buildings, b => b.Id, "budova");
    }

    private static BuildingDef ValidateBuilding(
        string path, BuildingDto dto, int index, BiomeRegistry biomes, DefRegistry<Resource> resources,
        Dictionary<string, int> idToIndex)
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

        return new BuildingDef(
            id, category, color, dto.Footprint[0], dto.Footprint[1],
            dto.WorkerSlots, dto.HousingCapacity, buildCost, recipe, mask,
            storageBonus, dto.AutoBuild, dto.Buildable ?? true, upgradesToIndex, upgradeCost);
    }

    // ----- tech tree -----

    private static DefRegistry<TechDef> LoadTech(string path, DefRegistry<BuildingDef> buildings, DefRegistry<Resource> resources)
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

            techs.Add(new TechDef(id, cost, prereqs, unlocks));
        }

        return new DefRegistry<TechDef>(techs, t => t.Id, "technologie", allowEmpty: true);
    }

    // ----- Vzestup (prestige) -----

    private static readonly HashSet<string> KnownPrestigeEffects = new(StringComparer.Ordinal)
    {
        "production_mult", "harvest_mult", "growth_mult", "housing_mult", "storage_mult", "start_resources",
    };

    private static (PrestigeConfig Config, DefRegistry<PrestigeUpgradeDef> Upgrades) LoadPrestige(
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

        var dtos = file.Upgrades ?? new List<PrestigeUpgradeDto>();
        var idToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < dtos.Count; i++)
        {
            string id = RequireId(path, dtos[i].Id, $"Upgrade Vzestupu na pozici {i}");
            if (!idToIndex.TryAdd(id, i))
            {
                throw new ContentLoadException(path, $"Duplicitní ID upgradu Vzestupu '{id}'.");
            }
        }

        var upgrades = new List<PrestigeUpgradeDef>(dtos.Count);
        for (int i = 0; i < dtos.Count; i++)
        {
            var dto = dtos[i];
            string id = dto.Id!.Trim();
            string effect = (dto.Effect ?? string.Empty).Trim();
            if (!KnownPrestigeEffects.Contains(effect))
            {
                throw new ContentLoadException(path, $"Upgrade '{id}': neznámý efekt '{dto.Effect}' (známé: {string.Join(", ", KnownPrestigeEffects)}).");
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

            upgrades.Add(new PrestigeUpgradeDef(id, effect, dto.Magnitude, dto.Cost, prereqs));
        }

        var config = new PrestigeConfig(requirement, pointsMetric, pointsParam, points.Divisor);
        return (config, new DefRegistry<PrestigeUpgradeDef>(upgrades, u => u.Id, "upgrade Vzestupu", allowEmpty: true));
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

    private static GameplayConfig LoadGameplay(string path, DefRegistry<Resource> resources)
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

        return new GameplayConfig(
            file.StartingPopulation,
            file.BaseHousingCapacity,
            file.PopulationGrowthPerSecond,
            file.FoodPerPersonPerSecond,
            foodIndex,
            new AutoBuildConfig(file.AutoBuild.IntervalTicks, file.AutoBuild.SearchRadius, file.AutoBuild.PopulationHeadroom),
            new RoadConfig(roadColor, file.Roads.MaxSearchDistance),
            new SettlementConfig(file.Settlements.MinBuildings, file.Settlements.ClusterDistance, file.Settlements.UpdateIntervalTicks),
            new DayNightConfig(
                file.DayNight.DayLengthSeconds,
                file.DayNight.StartTimeOfDay,
                nightColor,
                duskColor,
                file.DayNight.NightAlpha,
                file.DayNight.DuskAlpha));
    }

    // ----- devlog (volitelný obsah menu) -----

    private static IReadOnlyList<DevlogEntry> LoadDevlog(string path)
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
            if (string.IsNullOrWhiteSpace(dto.Version))
            {
                throw new ContentLoadException(path, "Záznam deníku nemá vyplněnou 'version'.");
            }

            var lines = (dto.Lines ?? new List<string>()).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            entries.Add(new DevlogEntry(dto.Version.Trim(), dto.Date?.Trim() ?? string.Empty, lines));
        }

        return entries;
    }

    // ----- dekorace a fauna (živá mapa) -----

    private static IReadOnlyList<DecorationDef> LoadDecorations(string path, BiomeRegistry biomes)
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

    private static IReadOnlyList<FaunaDef> LoadFauna(string path, BiomeRegistry biomes)
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

    private static IReadOnlyList<string> LoadSettlementNames(string path)
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

    private static DefRegistry<LanguageDef> LoadLanguages(
        string langDirectory,
        BiomeRegistry biomes,
        DefRegistry<Resource> resources,
        DefRegistry<BuildingDef> buildings,
        WorldGenCatalog worldGen,
        DefRegistry<TechDef> techs,
        DefRegistry<PrestigeUpgradeDef> prestigeUpgrades)
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

        ValidateContentKeys(langDirectory, languages[0], biomes, resources, buildings, worldGen, techs, prestigeUpgrades);
        ValidateKeySetsMatch(langDirectory, languages);
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
        DefRegistry<PrestigeUpgradeDef> prestigeUpgrades)
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
    private static void ValidateKeySetsMatch(string langDirectory, List<LanguageDef> languages)
    {
        var reference = languages[0];
        foreach (var language in languages.Skip(1))
        {
            var missing = reference.Strings.Keys.Where(k => !language.Strings.ContainsKey(k)).ToList();
            var extra = language.Strings.Keys.Where(k => !reference.Strings.ContainsKey(k)).ToList();
            if (missing.Count > 0 || extra.Count > 0)
            {
                var parts = new List<string>();
                if (missing.Count > 0)
                {
                    parts.Add($"chybí: {string.Join(", ", missing.Take(8))}" + (missing.Count > 8 ? "…" : ""));
                }

                if (extra.Count > 0)
                {
                    parts.Add($"přebývá: {string.Join(", ", extra.Take(8))}" + (extra.Count > 8 ? " …" : ""));
                }

                throw new ContentLoadException(
                    langDirectory,
                    $"Jazyk '{language.Id}' nemá stejné klíče jako '{reference.Id}' — {string.Join("; ", parts)}.");
            }
        }
    }

    // ----- worldgen -----

    private static WorldGenCatalog LoadWorldGen(string path, BiomeRegistry biomes)
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
        return new TerrainPreset(id, (float)dto.SeaLevel, fallbackIndex, elevation, moisture);
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

    private static T ReadFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new ContentLoadException(path, "Soubor nenalezen.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            return parsed ?? throw new ContentLoadException(path, "Soubor obsahuje jen 'null'.");
        }
        catch (JsonException ex)
        {
            throw new ContentLoadException(path, $"Neplatný JSON: {ex.Message}");
        }
    }
}
