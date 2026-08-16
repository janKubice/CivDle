using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Microsoft.Xna.Framework;

namespace CivDle.Capture;

/// <summary>
/// Postaví z <see cref="TownPlan"/> skutečný svět — takový, na jaký se dá
/// natočit záběr „postav si město svých snů".
///
/// <para>Proč vlastní kulisa vedle <see cref="CityFixture"/>: ten pěstuje
/// <b>velkoměsto</b> pro snímky do obchodu a nechává hrát pravidla hry, takže se
/// nedá říct „chci sem park a sem náměstí". Trailer potřebuje opak — malý,
/// prohlédnutelný výřez, kde je každá parcela záměr.</para>
///
/// <para>Technologie se odemykají <see cref="Simulation.DebugGrantTech"/>, ne
/// odehráním stromu: zásoby se ořezávají na kapacitu skladu, takže na park nebo
/// řadovku by kulisa musela napřed postavit sklady a odtikat hodiny hry. Odemyká
/// se jen to, co paleta opravdu potřebuje — město tak zůstane v době, do které
/// patří, a nevyskočí v něm mrakodrap.</para>
///
/// <para>Vrstva: nástroj nad simulací. Nic nekreslí a do rozehrané hry nesahá —
/// je to vlastní svět, který po natočení zahodíme.</para>
/// </summary>
internal sealed class ShowcaseTown
{
    /// <summary>Strana výřezu v dlaždicích. Čtyřicet se vejde do záběru celé.</summary>
    public const int Size = 40;

    /// <summary>Kolik lidí připadne na jedno místo k bydlení (ať nejsou ulice prázdné).</summary>
    private const double Occupancy = 0.85;

    private ShowcaseTown(Simulation simulation, Vector2 center)
    {
        Simulation = simulation;
        Center = center;
    }

    /// <summary>Hotový svět s městečkem.</summary>
    public Simulation Simulation { get; }

    /// <summary>Střed městečka ve světových pixelech — kam se má dívat kamera.</summary>
    public Vector2 Center { get; }

    /// <summary>Vypěstuje městečko daného semínka.</summary>
    public static ShowcaseTown Build(GameContent content, long seed)
    {
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
        var simulation = new Simulation(content, terrain, seed);

        // Průvodce je pro první minuty hry — na hotovém městečku by radil,
        // jak nasekat dvanáct dřev.
        simulation.SkipTutorial();

        var (originX, originY) = FindBuildableSpot(simulation, content, seed);
        UnlockPalette(simulation, content);

        // Na poledne a jasno se dotiká JEŠTĚ PŘED stavbou. Prázdný svět nemá co
        // růst (auto-stavba se bez první budovy vůbec nespustí), takže se tudy
        // půdorys nestihne rozjet dřív, než ho položíme.
        CityFixture.TickUntilPostcardMoment(simulation, content, from: 0.40, to: 0.58);

        FreezeLayout(simulation, content);

        var plan = TownPlanner.Plan(seed, Size);
        LayOut(plan, simulation, content, originX, originY);

        Settle(simulation, originX, originY);

        var center = new Vector2(
            (originX + Size * 0.5f) * TerrainRenderer.TileSize,
            (originY + Size * 0.5f) * TerrainRenderer.TileSize);

        return new ShowcaseTown(simulation, center);
    }

    /// <summary>
    /// Položí ulice a domy. Kandidáti se zkoušejí v pořadí — první, který se na
    /// dané dlaždice vejde (biom, voda, obsazenost), vyhrává.
    /// </summary>
    private static void LayOut(
        TownPlan plan, Simulation simulation, GameContent content, int originX, int originY)
    {
        foreach (var (x, y) in plan.Roads)
        {
            simulation.AddRoadTileForTest(originX + x, originY + y);
        }

        foreach (var lot in plan.Lots)
        {
            foreach (string id in lot.Candidates)
            {
                int defIndex = Resolve(content, id, lot);
                if (simulation.TryPlaceBuildingFree(defIndex, originX + lot.X, originY + lot.Y)
                    == PlacementResult.Ok)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Najde v datech budovu podle ID a ověří, že má půdorys, se kterým plán
    /// počítal.
    ///
    /// <para>Fail-fast schválně: kdyby v JSON někdo zvětšil řadovku na 3×2,
    /// plán by tiše přestal sedět a v ulici by se objevily díry. Lepší je
    /// spadnout hned a s jasnou hláškou.</para>
    /// </summary>
    private static int Resolve(GameContent content, string id, TownLot lot)
    {
        if (!content.Buildings.TryIndexOf(id, out int defIndex))
        {
            throw new InvalidOperationException(
                $"Ukázkové městečko chce budovu '{id}', ta ale v datech není.");
        }

        var def = content.Buildings[defIndex];
        if (def.FootprintWidth != lot.Width || def.FootprintHeight != lot.Height)
        {
            throw new InvalidOperationException(
                $"Budova '{id}' má v datech půdorys {def.FootprintWidth}×{def.FootprintHeight}, "
                + $"ale plán s ní počítá jako s {lot.Width}×{lot.Height}.");
        }

        return defIndex;
    }

    /// <summary>
    /// Odemkne technologie, které paleta plánu potřebuje — i s jejich předpoklady.
    /// Nic navíc: kdyby se odemklo všechno, stálo by v idylickém městečku
    /// robotické překladiště.
    /// </summary>
    private static void UnlockPalette(Simulation simulation, GameContent content)
    {
        var wanted = new HashSet<int>();
        foreach (string id in TownPlanner.AllBuildingIds)
        {
            if (!content.Buildings.TryIndexOf(id, out int defIndex))
            {
                continue; // chybějící ID nahlásí až Resolve, a to srozumitelněji
            }

            for (int techIndex = 0; techIndex < content.Techs.Count; techIndex++)
            {
                if (content.Techs[techIndex].UnlockedBuildingIndices.Contains(defIndex))
                {
                    AddWithPrerequisites(content, techIndex, wanted);
                }
            }
        }

        // Ve stromovém pořadí, ať předpoklad padne dřív než to, co na něm visí.
        foreach (int techIndex in wanted.Order())
        {
            simulation.DebugGrantTech(techIndex);
        }
    }

    /// <summary>
    /// Řekne guvernérovi, ať nechá město být.
    ///
    /// <para>Bez tohohle je záběr k ničemu a je to dobře vidět: auto-stavba je
    /// jádro hry, takže za pár vteřin přisype vlastní domy a k nim cesty — a z
    /// vyskládaných bloků s dvorky je najednou souvislá mřížka. Kulisa má
    /// ukazovat <b>rozvržení</b>, ne růst.</para>
    ///
    /// <para>Používají se na to normální ovládací prvky guvernéra, ne zadní
    /// vrátka: zakázané kategorie a vypnuté vylepšování i slučování. Co si
    /// takhle může nastavit hráč, na to nepotřebuje kulisa výjimku.</para>
    /// </summary>
    private static void FreezeLayout(Simulation simulation, GameContent content)
    {
        simulation.SetAutoUpgradeLevel(0);
        simulation.SetAutoMerge(false);
        simulation.Plan.SetFocus(GovernorFocus.Growth); // růst = 0 vylepšení…

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            simulation.Plan.SetCategoryAllowed(content.Buildings[i].Category, false); // …a nic ke stavbě
        }
    }

    private static void AddWithPrerequisites(GameContent content, int techIndex, HashSet<int> into)
    {
        if (!into.Add(techIndex))
        {
            return;
        }

        foreach (int prereq in content.Techs[techIndex].PrerequisiteIndices)
        {
            AddWithPrerequisites(content, prereq, into);
        }
    }

    /// <summary>
    /// Nastěhuje lidi, naplní sklady a nechá svět chvíli běžet.
    ///
    /// <para>Bez tohohle je městečko kulisa bez života: nikdo nechodí po ulicích,
    /// z komínů se nekouří a vozy stojí. Právě ten pohyb dělá ze screenshotu
    /// záběr.</para>
    /// </summary>
    private static void Settle(Simulation simulation, int originX, int originY)
    {
        // Mlha by z městečka udělala černý čtverec — kulisa má být vidět celá.
        simulation.Fog.Reveal(originX + Size / 2, originY + Size / 2, Size);

        simulation.DebugFillStorages();
        simulation.DebugAddPopulation(simulation.HousingCapacity * Occupancy);

        // Krátké usazení: rozjede se výroba, z komínů se zakouří, po ulicích
        // vyrazí lidi. Dýl to být nemusí — život kreslí renderer, ne simulace.
        for (int tick = 0; tick < 60; tick++)
        {
            simulation.Tick();
        }
    }

    /// <summary>Kolik míst se prohledá nahrubo a kolik se pak doladí kolem nejlepšího.</summary>
    private const int CoarseAttempts = 1500;
    private const int RefineAttempts = 200;

    /// <summary>
    /// Najde plochu, na které městečko opravdu vyroste.
    ///
    /// <para>Dvoukolově: napřed hrubý sken celého okolí, pak doladění v okruhu
    /// kolem nejlepšího nálezu. Náhodné body samy o sobě nestačí — svět je
    /// z velké části oceán, džungle a hory, takže se do souvislé zelené pláně
    /// trefí jen občas a záběr by pak vyšel jednou z pěti.</para>
    /// </summary>
    private static (int X, int Y) FindBuildableSpot(Simulation simulation, GameContent content, long seed)
    {
        var rng = new Random((int)(seed & 0x7FFFFFFF) ^ 0x5EED);
        (int X, int Y) best = (-Size / 2, -Size / 2);
        int bestScore = int.MinValue;

        for (int attempt = 0; attempt < CoarseAttempts; attempt++)
        {
            int x = rng.Next(-3000, 3000);
            int y = rng.Next(-3000, 3000);
            int score = ScoreSpot(simulation, content, x, y);
            if (score > bestScore)
            {
                bestScore = score;
                best = (x, y);
            }
        }

        for (int attempt = 0; attempt < RefineAttempts; attempt++)
        {
            int x = best.X + rng.Next(-Size, Size + 1);
            int y = best.Y + rng.Next(-Size, Size + 1);
            int score = ScoreSpot(simulation, content, x, y);
            if (score > bestScore)
            {
                bestScore = score;
                best = (x, y);
            }
        }

        return best;
    }

    /// <summary>Kolik dlaždic kolem městečka se ještě počítá do krajiny v záběru.</summary>
    private const int SurroundingsReach = 14;

    /// <summary>
    /// Ohodnotí čtverec pod budoucí město.
    ///
    /// <para>Rozhoduje jediná otázka: <b>postaví se tam dům?</b> Plán o terénu
    /// neví, takže každá dlaždice, kam bydlení nesmí, je v ulici díra. První
    /// verze měřila místo toho „hezkost" a mezi hezké biomy počítala i les —
    /// jenže do lesa se nesmí stavět skoro nic, takže se z plánu postavila
    /// sotva polovina a bloky zůstaly poloprázdné.</para>
    ///
    /// <para>Co dělá krajinu, se hodnotí až <b>kolem</b> městečka: les nebo
    /// jezero za poslední ulicí je v záběru krásné, uprostřed náměstí ne.</para>
    ///
    /// <para>Známka je <b>spojitá</b>, ne „vyhovuje / nevyhovuje". Pevná hranice
    /// se ukázala jako past: když ji ve světě plném oceánu a hor nikdo
    /// nepřekročil, propadly všechny nálezy a městečko skončilo na nouzové
    /// souřadnici — uprostřed moře. Takhle vyhraje nejlepší, co svět nabízí,
    /// a stavitelnost přebíjí všechno ostatní.</para>
    /// </summary>
    private static int ScoreSpot(Simulation simulation, GameContent content, int x, int y)
    {
        if (!content.Buildings.TryIndexOf("house", out int houseIndex))
        {
            return 0; // bez domu se stejně nedá nic postavit; ohlásí to až Resolve
        }

        var house = content.Buildings[houseIndex];
        int tiles = 0, buildable = 0, green = 0;

        for (int ty = y; ty < y + Size; ty += 2)
        {
            for (int tx = x; tx < x + Size; tx += 2)
            {
                tiles++;
                int biome = simulation.BiomeAt(tx, ty);
                if (house.IsBiomeAllowed(biome))
                {
                    buildable++;
                }

                if (Array.IndexOf(Green, content.Biomes[biome].Id) >= 0)
                {
                    green++;
                }
            }
        }

        if (tiles == 0)
        {
            return int.MinValue;
        }

        // Stavitelnost váží desetkrát víc než zeleň: raději hezké městečko na
        // savaně než děravé na louce. Zeleň ale rozhoduje mezi pláněmi, které
        // jsou z hlediska stavby stejně dobré — a poušť vypadá na záběru jinak
        // než luční kobercem prorostlé městečko s parky.
        return buildable * 5000 / tiles
            + green * 1200 / tiles
            + Surroundings(simulation, content, x, y)
            - NeighborPenalty(simulation, x, y);
    }

    /// <summary>Biomy, na kterých je zeleň — kvůli nim se v městečku pozná park.</summary>
    private static readonly string[] Green = { "grassland", "forest", "taiga", "highlands" };

    /// <summary>Jak daleko musí být cizí město, aby do záběru nezasáhlo.</summary>
    private const int NeighborClearance = Size + 15;

    /// <summary>
    /// Postih za cizí město poblíž.
    ///
    /// <para>Není to estetika, je to porucha: cizí město, které hráčova
    /// zástavba obestaví, se po chvíli <b>tiše pohltí</b> a jeho domy přejdou
    /// do říše. V záběru to vypadalo, jako by městečko samo od sebe vyrazilo
    /// sto osmdesát budov — na kraji plánu se z ničeho nic objevila druhá,
    /// cizí čtvrť. Cizí dlaždice navíc blokují stavbu, takže se do nich část
    /// plánu ani nepostaví.</para>
    ///
    /// <para>Postih je velký, ale konečný. Kdyby to byla tvrdá podmínka a ve
    /// světě zrovna nebylo místo bez souseda, propadly by všechny nálezy
    /// a městečko by skončilo na nouzové souřadnici uprostřed moře.</para>
    /// </summary>
    private static int NeighborPenalty(Simulation simulation, int x, int y)
    {
        int neighbors = simulation
            .CitiesNear(x + Size / 2, y + Size / 2, NeighborClearance)
            .Count();

        return neighbors * 4000;
    }

    /// <summary>
    /// Co je vidět kolem městečka: kus vody a pár různých biomů. Bez toho
    /// stojí město na jednolitém koberci a záběr nemá kam dýchat.
    /// </summary>
    private static int Surroundings(Simulation simulation, GameContent content, int x, int y)
    {
        var seen = new HashSet<int>();
        int water = 0;

        for (int ty = y - SurroundingsReach; ty < y + Size + SurroundingsReach; ty += 3)
        {
            for (int tx = x - SurroundingsReach; tx < x + Size + SurroundingsReach; tx += 3)
            {
                bool inside = tx >= x && tx < x + Size && ty >= y && ty < y + Size;
                if (inside)
                {
                    continue;
                }

                int biome = simulation.BiomeAt(tx, ty);
                seen.Add(biome);
                if (content.Biomes[biome].IsWater)
                {
                    water++;
                }
            }
        }

        // Voda se cení, ale jen jako lem — od velkého jezera už je to záběr na
        // vodu s městem v rohu.
        int shoreline = water is > 4 and < 90 ? 60 : 0;
        return seen.Count * 14 + shoreline;
    }
}
