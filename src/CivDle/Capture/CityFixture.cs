using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;

namespace CivDle.Capture;

/// <summary>
/// Vypěstuje město, které stojí za snímek.
///
/// <para>Není to hráč ani balanční model — je to kulisa. A kulisa musí být
/// <b>čitelná</b>: první verze rozhazovala budovy náhodně kolem středu a výsledek
/// vypadal jako rozsypaný čaj, ne jako město. Teď se staví po blocích do mřížky
/// s ulicemi mezi nimi — auto-silnice mezery vyplní a vznikne půdorys, který se
/// dá na první pohled přečíst.</para>
///
/// <para>Co kam patří, rozhoduje terén: pila k lesu, lom k horám, rybolov
/// k vodě, domy doprostřed. Je to totéž, co by udělal hráč — a díky bonusu za
/// okolí je to i správně zahrané.</para>
///
/// <para>Růst populace se zrychluje, aby se stav, na který by hráč došel za
/// několik hodin, dal nasimulovat za pár sekund. Je to <b>stejný</b> stav, jen dřív.</para>
/// </summary>
internal static class CityFixture
{
    /// <summary>Kolikrát rychleji roste populace, aby bylo město do minuty velké.</summary>
    private const double GrowthSpeedUp = 12.0;

    /// <summary>Velikost bloku zástavby (dlaždice). Kolem něj vede ulice.</summary>
    private const int BlockSize = 3;

    /// <summary>Rozteč bloků = blok + ulice.</summary>
    private const int BlockPitch = BlockSize + 1;

    /// <summary>Kolik bloků na stranu se rozestaví (liché číslo → město má střed).</summary>
    private const int BlocksPerSide = 11;

    /// <summary>
    /// Sestaví svět s městem připraveným k focení.
    ///
    /// <para><paramref name="ascensions"/> = kolikrát před stavbou projít Vzestupem.
    /// Megastruktury se odemykají až měřítkem, takže div světa nejde vyfotit
    /// v první vesnici — musí se k němu dojít stejnou cestou jako ve hře.</para>
    /// </summary>
    public static Simulation Grow(GameContent content, long seed, double minutes, int ascensions = 0)
    {
        var fast = content.WithGameplay(content.Gameplay with
        {
            PopulationGrowthPerSecond = content.Gameplay.PopulationGrowthPerSecond * GrowthSpeedUp,
        });

        var preset = fast.WorldGen.Presets[fast.WorldGen.DefaultPresetIndex];
        var terrain = new ProceduralTerrain(fast.Biomes, preset, seed);
        var sim = new Simulation(fast, terrain, seed);

        // Průvodce je pro první minuty hry; na snímku rozrostlého města by radil,
        // jak nasekat prvních dvanáct dřev.
        sim.SkipTutorial();

        var (originX, originY) = FindPrettySpot(sim, fast, seed);
        AscendTimes(sim, fast, ascensions);

        // Napřed pár set ručních sběrů na volném terénu: úvodní úkoly („nasekej
        // dřevo", „nalámej kámen") tím odsýpají a na snímku pak nevisí nad
        // velkoměstem. Po zástavbě už není kam kliknout.
        HarvestAround(sim, originX, originY);

        TopUp(sim, fast, 1.0);
        ResearchEverythingAffordable(sim, fast);
        LayOutTown(sim, fast, originX, originY);

        // Odtikat: doroste populace, dostaví se silnice, rozjede se výroba.
        long ticks = (long)(minutes * 60 * Simulation.TicksPerSecond);
        for (long tick = 1; tick <= ticks; tick++)
        {
            if (tick % 200 == 0)
            {
                TopUp(sim, fast, 0.55);
                ResearchEverythingAffordable(sim, fast);
            }

            sim.Tick();
        }

        TopUp(sim, fast, 0.55);
        for (int i = 0; i < 120; i++)
        {
            sim.Tick(); // krátké usazení, ať čísla v HUD nevypadají naklikaná
        }

        return sim;
    }

    /// <summary>
    /// Projde zadaným počtem Vzestupů. Každý město smete a zvedne měřítko —
    /// je to jediná cesta, jak se ve hře dostat k megastrukturám, a snímek nemá
    /// ukazovat nic, k čemu se hráč nedostane taky.
    /// </summary>
    private static void AscendTimes(Simulation sim, GameContent content, int times)
    {
        for (int step = 0; step < times; step++)
        {
            for (int guard = 0; guard < 200_000 && !sim.CanAscend(); guard++)
            {
                TopUp(sim, content, 1.0);
                sim.Tick();
            }

            if (sim.TryAscend() != PlacementResult.Ok)
            {
                return; // dál to nejde — snímek pak ukáže, co se stihlo
            }
        }
    }

    /// <summary>Nakliká pár set ručních sběrů kolem budoucího města.</summary>
    private static void HarvestAround(Simulation sim, int originX, int originY)
    {
        int reach = BlocksPerSide / 2 * BlockPitch + 8;
        for (int i = 0; i < 400; i++)
        {
            int x = originX + i * 7 % (reach * 2) - reach;
            int y = originY + i * 13 % (reach * 2) - reach;
            sim.TryHarvest(x, y, out _, out _);
        }
    }

    /// <summary>Postaví rozestavěný div světa na kraj města — kvůli záběru na staveniště.</summary>
    public static bool PlaceWonder(Simulation sim, GameContent content, out int x, out int y)
    {
        x = y = 0;
        for (int defIndex = 0; defIndex < content.Buildings.Count; defIndex++)
        {
            if (!content.Buildings[defIndex].TakesTimeToBuild)
            {
                continue;
            }

            int reach = BlocksPerSide / 2 * BlockPitch + 6;
            for (int radius = 4; radius <= reach; radius += 2)
            {
                for (int angle = 0; angle < 24; angle++)
                {
                    int tx = sim.CityCenterX + (int)(radius * Math.Cos(angle * Math.PI / 12));
                    int ty = sim.CityCenterY + (int)(radius * Math.Sin(angle * Math.PI / 12));
                    if (sim.TryPlaceBuildingFree(defIndex, tx, ty) == PlacementResult.Ok)
                    {
                        x = tx;
                        y = ty;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Odtiká, dokud denní čas nespadne do zadaného pásma (poledne, noc…).</summary>
    public static void TickUntilTimeOfDay(Simulation sim, double from, double to)
    {
        for (int i = 0; i < 100_000; i++)
        {
            double t = sim.TimeOfDay01;
            if (from <= to ? t >= from && t < to : t >= from || t < to)
            {
                return;
            }

            sim.Tick();
        }
    }

    /// <summary>Odtiká, dokud nenastane období daného ID.</summary>
    public static void TickUntilSeason(Simulation sim, string seasonId)
    {
        for (int i = 0; i < 200_000; i++)
        {
            if (sim.CurrentSeason?.Id == seasonId)
            {
                return;
            }

            sim.Tick();
        }
    }

    /// <summary>
    /// Najde místo, kde je na co se dívat: pevnina s pestrým okolím (les, voda,
    /// kopce). Plochá step je taky mapa, ale na snímku nemá co nabídnout.
    /// </summary>
    private static (int X, int Y) FindPrettySpot(Simulation sim, GameContent content, long seed)
    {
        var rng = new Random((int)(seed & 0x7FFFFFFF));
        (int X, int Y) best = (0, 0);
        int bestScore = int.MinValue;

        for (int attempt = 0; attempt < 400; attempt++)
        {
            int cx = rng.Next(-4000, 4000);
            int cy = rng.Next(-4000, 4000);
            int score = ScoreSpot(sim, content, cx, cy);
            if (score > bestScore)
            {
                bestScore = score;
                best = (cx, cy);
            }
        }

        return best;
    }

    /// <summary>Biomy, na kterých město vypadá dobře — zeleň, pole, kousek pobřeží.</summary>
    private static readonly string[] Photogenic = { "grassland", "forest", "savanna", "beach", "taiga" };

    /// <summary>
    /// Biomy, kterých chceme málo. Nejde o ošklivost, ale o čitelnost: hory
    /// a badlands pokrývá hustý koberec balvanů, ve kterém zanikne i velké město,
    /// a poušť je jedna plocha bez kontrastu.
    /// </summary>
    private static readonly string[] Noisy = { "mountains", "snow_peaks", "badlands", "volcanic", "desert", "glacier" };

    /// <summary>
    /// Ohodnotí okolí. Chceme převahu zelené stavitelné souše, k tomu kousek
    /// vody a pár různých biomů — ne kamenné pole a ne jednolitou step.
    /// </summary>
    private static int ScoreSpot(Simulation sim, GameContent content, int cx, int cy)
    {
        int reach = BlocksPerSide / 2 * BlockPitch;
        var seen = new HashSet<int>();
        int land = 0, water = 0, pretty = 0, noisy = 0;

        for (int y = cy - reach; y <= cy + reach; y += 2)
        {
            for (int x = cx - reach; x <= cx + reach; x += 2)
            {
                int index = sim.BiomeAt(x, y);
                string id = content.Biomes[index].Id;
                seen.Add(index);

                if (content.Biomes[index].IsWater)
                {
                    water++;
                }
                else
                {
                    land++;
                }

                if (Array.IndexOf(Photogenic, id) >= 0)
                {
                    pretty++;
                }
                else if (Array.IndexOf(Noisy, id) >= 0)
                {
                    noisy++;
                }
            }
        }

        int total = land + water;
        if (total == 0 || land < total * 0.72 || pretty < total * 0.5)
        {
            return int.MinValue; // moc vody nebo moc kamení — město by tam zaniklo
        }

        // Kousek skal je vítaný: dává kámen na těžbu a scéně kontrast. Teprve
        // když jich je moc, zanikne v balvanech celé město.
        int rocks = noisy > 0 && noisy < total * 0.12 ? 25 : -noisy * 6;
        int shoreline = water > 0 && water < total * 0.22 ? 40 : 0;
        return pretty * 3 + rocks + seen.Count * 8 + shoreline;
    }

    /// <summary>
    /// Odtiká na okamžik, kdy je zároveň poledne a jasno.
    ///
    /// <para>Obojí zvlášť nestačí: počasí i denní čas jsou funkce tiku, takže
    /// dotočením na poledne se rozprší a dotočením na jasno je zase noc. Musí se
    /// čekat na chvíli, kdy platí obě podmínky naráz.</para>
    /// </summary>
    public static void TickUntilPostcardMoment(Simulation sim, GameContent content, double from, double to)
    {
        for (int i = 0; i < 500_000; i++)
        {
            double t = sim.TimeOfDay01;
            bool daylight = from <= to ? t >= from && t < to : t >= from || t < to;
            int weather = sim.CurrentWeatherIndex;
            bool clear = weather < 0 || content.Weather[weather].Id == "clear";
            if (daylight && clear)
            {
                return;
            }

            sim.Tick();
        }
    }

    /// <summary>
    /// Rozestaví město do mřížky bloků. V každém bloku stojí jeden druh budovy
    /// vybraný podle okolí — vzniknou tím čtvrti (obytná uprostřed, dřevařská
    /// u lesa, přístavní u vody) místo splácaniny.
    /// </summary>
    private static void LayOutTown(Simulation sim, GameContent content, int originX, int originY)
    {
        int half = BlocksPerSide / 2;
        for (int blockY = -half; blockY <= half; blockY++)
        {
            for (int blockX = -half; blockX <= half; blockX++)
            {
                int x = originX + blockX * BlockPitch;
                int y = originY + blockY * BlockPitch;

                // Uprostřed se bydlí, na okrajích pracuje — město pak má čitelný
                // střed a ne rovnoměrnou kaši. Šachovnicí se navíc střídají
                // kategorie, aby okraj nebyl souvislý lán stejných budov.
                bool core = Math.Max(Math.Abs(blockX), Math.Abs(blockY)) <= half / 2;
                bool flip = (blockX + blockY) % 2 == 0;
                FillBlock(sim, content, x, y, core ^ flip && !core);
            }
        }
    }

    /// <summary>Zaplní jeden blok stejným druhem budovy (co se na dané dlaždice vejde).</summary>
    private static void FillBlock(Simulation sim, GameContent content, int x, int y, bool core)
    {
        int defIndex = PickForBlock(sim, content, x, y, core);
        if (defIndex < 0)
        {
            return;
        }

        var def = content.Buildings[defIndex];
        for (int ty = y; ty < y + BlockSize; ty += def.FootprintHeight)
        {
            for (int tx = x; tx < x + BlockSize; tx += def.FootprintWidth)
            {
                // Placenou cestou, ne zdarma: silnice staví jen TryPlaceBuilding,
                // a město bez ulic vypadá jako tabulka, ne jako město.
                if (sim.TryPlaceBuilding(defIndex, tx, ty) == PlacementResult.NotEnoughResources)
                {
                    TopUp(sim, content, 1.0);
                    sim.TryPlaceBuilding(defIndex, tx, ty);
                }
            }
        }
    }

    /// <summary>
    /// Co postavit na daném místě: z odemčených budov, které se sem vejdou, ta
    /// s nejlepším bonusem za okolí. Uvnitř města se preferuje bydlení a služby,
    /// venku výroba.
    /// </summary>
    private static int PickForBlock(Simulation sim, GameContent content, int x, int y, bool core)
    {
        string[] preferred = core
            ? new[] { "housing", "civic", "storage", "production" }
            : new[] { "production", "storage", "civic", "housing" };

        foreach (string category in preferred)
        {
            int best = -1;
            for (int defIndex = 0; defIndex < content.Buildings.Count; defIndex++)
            {
                var def = content.Buildings[defIndex];
                if (def.Category != category
                    || !sim.IsBuildingBuildable(defIndex)
                    || def.FootprintWidth > BlockSize
                    || def.FootprintHeight > BlockSize
                    || sim.CanPlace(defIndex, x, y) != PlacementResult.Ok)
                {
                    continue;
                }

                // Pila skončí u lesa, lom u hor — přesně to, co by hráč udělal ručně.
                if (best < 0 || sim.AdjacencyMultiplierAt(defIndex, x, y) > sim.AdjacencyMultiplierAt(best, x, y))
                {
                    best = defIndex;
                }
            }

            if (best >= 0)
            {
                return best;
            }
        }

        return -1;
    }

    /// <summary>
    /// Kolik technologií se smí vyzkoumat. Schválně ne všechny: s kompletním
    /// stromem se odemkne tolik nástrojů, že se spodní lišta nevejde na obrazovku.
    /// Snímek má ukazovat rozehranou hru, ne dohranou.
    /// </summary>
    private const int TechBudget = 14;

    /// <summary>Vyzkoumá pár prvních technologií — město tak má pestřejší zástavbu.</summary>
    private static void ResearchEverythingAffordable(Simulation sim, GameContent content)
    {
        for (int round = 0; round < 4; round++)
        {
            bool any = false;
            for (int techIndex = 0; techIndex < content.Techs.Count && ResearchedCount(sim, content) < TechBudget; techIndex++)
            {
                if (sim.TryResearch(techIndex) == PlacementResult.Ok)
                {
                    any = true;
                }
            }

            if (!any)
            {
                return;
            }

            TopUp(sim, content, 1.0);
        }
    }

    private static int ResearchedCount(Simulation sim, GameContent content)
    {
        int count = 0;
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (sim.IsTechResearched(i))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Doplní sklady na daný podíl kapacity.</summary>
    private static void TopUp(Simulation sim, GameContent content, double fraction)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            double target = sim.GetStorageCap(i) * fraction;
            if (sim.GetResource(i) < target)
            {
                sim.AddResource(i, target - sim.GetResource(i));
            }
        }
    }
}
