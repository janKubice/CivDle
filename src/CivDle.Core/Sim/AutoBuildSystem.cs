using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Automatický růst zástavby (fáze 2 roadmapy: „domy se staví samy dle poptávky").
/// Když se populace blíží kapacitě bydlení, civilizace si sama postaví budovu
/// označenou <c>autoBuild</c> poblíž existující zástavby — za normální cenu,
/// takže růst táhne poptávku po surovinách (dřevo → prkna).
///
/// Běží na nízké frekvenci (intervalTicks), ne každý tik (CLAUDE.md, výkon).
/// „Náhoda" je bezstavový hash (seed, tik) — deterministická a přežívá save/load
/// bez ukládání stavu RNG.
/// </summary>
internal sealed class AutoBuildSystem
{
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;

    /// <summary>Ofsety kandidátních míst kolem kotvy, od nejbližších — domy se lepí k sobě (organická vesnice).</summary>
    private readonly (int X, int Y)[] _searchOffsets;

    public AutoBuildSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
        _needs = new GovernorNeeds(content);
        _capabilities = GovernorNeeds.Capabilities(content);

        int radius = content.Gameplay.AutoBuild.SearchRadius;
        var offsets = new List<(int X, int Y)>();
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius && (x != 0 || y != 0))
                {
                    offsets.Add((x, y));
                }
            }
        }

        offsets.Sort((a, b) =>
        {
            int distance = (a.X * a.X + a.Y * a.Y).CompareTo(b.X * b.X + b.Y * b.Y);
            return distance != 0 ? distance : (a.Y, a.X).CompareTo((b.Y, b.X));
        });
        _searchOffsets = offsets.ToArray();
    }

    public void Tick(Simulation sim)
    {
        // Interval čte ze simulace, ne z dat: bonus autobuild_speed ho zkracuje,
        // takže po Vzestupu je zrychlení růstu vidět přímo na mapě.
        if (sim.TickCount % sim.AutoBuildInterval != 0)
        {
            return;
        }

        // Bez zástavby není kde růst — první budovu musí položit hráč.
        if (sim.Buildings.Length == 0)
        {
            return;
        }

        // Guvernér: slučování bloků má vlastní přepínač i technologii. Mění
        // půdorys města, takže se nemá zapnout nepozorovaně s vylepšováním.
        if (sim.AutoMerge)
        {
            TryAutoMerge(sim);
        }

        // Guvernér: vylepšování běží NEZÁVISLE na tlaku bydlení — hráč si nastavil,
        // že se o modernizaci města stará sám. Míra = počet vylepšení za interval.
        for (int i = 0; i < sim.AutoUpgradeLevel; i++)
        {
            if (!TryAutoUpgrade(sim))
            {
                break; // není co (nebo na co) vylepšit
            }
        }

        // Politika „build_pace" i bonus autobuild_speed zvyšují počet akcí za
        // interval (jinak 1 — pozvolný růst).
        int budget = sim.AutoBuildBudget;

        // Mimo cyklus: stackalloc uvnitř by při vyšším rozpočtu staveb narůstal
        // po každé otáčce (CA2014).
        Span<CityNeed> needs = stackalloc CityNeed[4];
        for (int b = 0; b < budget; b++)
        {
            // Co město doopravdy potřebuje. Dřív se tady stál jen dotaz na tlak
            // bydlení — a protože značku 'autoBuild' měla jediná budova (chalupa),
            // rostla populace, kterou nikdo nekrmil ani neobsluhoval. Město tak
            // rostlo do hladu a pak stálo.
            int needCount = _needs.AssessAll(sim, needs);
            if (needCount == 0)
            {
                return;
            }

            // Projít potřeby od nejnaléhavější a zastavit se u první, kterou jde
            // opravdu pokrýt. Bez toho se guvernér zasekl na potřebě, na kterou
            // neuměl postavit nic (vyschlá surovina bez auto-stavitelné výrobny)
            // a město pak stálo na stropu bydlení s plným skladem jídla.
            bool acted = false;
            for (int n = 0; n < needCount && !acted; n++)
            {
                // Politika „housing_density": u tlaku na bydlení nejdřív povýšit
                // existující domy (víc lidí na stejném místě) místo stavby dalších.
                if (needs[n] == CityNeed.Housing && sim.PreferHousingDensity && TryDensify(sim))
                {
                    acted = true;
                    break;
                }

                acted = TryGrowOnce(sim, b, needs[n]);
            }

            if (!acted)
            {
                return; // ani jedna potřeba nejde pokrýt → konec
            }
        }
    }

    /// <summary>
    /// Jeden krok růstu: kotva + nejvhodnější auto-budova pro danou potřebu.
    /// Vrací true při úspěchu.
    /// </summary>
    private bool TryGrowOnce(Simulation sim, int nonce, CityNeed need)
    {
        // Deterministická volba kotvy: hash (seed, tik, pořadí v dávce) — žádný stav k ukládání.
        var rng = new SplitMix64(unchecked(
            (ulong)_seed ^ ((ulong)sim.TickCount * 0x9E3779B97F4A7C15UL) ^ ((ulong)nonce * 0xBF58476D1CE4E5B9UL)));

        // Kandidáti seřazení podle toho, jak dobře řeší potřebu — ne podle pořadí
        // v souboru. Zkouší se od nejlepšího: co se nevejde nebo na co nejsou
        // suroviny, přenechá místo dalšímu.
        Span<int> ranked = stackalloc int[_content.Buildings.Count];
        int count = RankCandidates(sim, need, ranked);
        if (count == 0)
        {
            return false;
        }

        // Víc kotev, ne jedna. Dřív si guvernér vybral jednu náhodnou budovu,
        // a když kolem ní nebylo místo (v hustém shluku skoro vždycky), vzdal
        // celý interval — město pak stálo na stropu bydlení s plným skladem,
        // i když vědělo, že mu dům chybí. Změřeno: pop 22, kapacita 22,
        // potřeba [Housing], dřevo 170, a nic se nepostavilo.
        int buildingCount = sim.Buildings.Length;
        int tries = Math.Min(AnchorAttempts, buildingCount);
        for (int attempt = 0; attempt < tries; attempt++)
        {
            var anchor = sim.Buildings[(int)(rng.Next() % (ulong)buildingCount)];
            for (int i = 0; i < count; i++)
            {
                if (TryBuildNear(sim, ranked[i], anchor.X, anchor.Y))
                {
                    return true;
                }
            }
        }

        // Nepostavilo se — možná proto, že na to město nemá. Pak je skutečná
        // potřeba jinde: ne „další dům", ale „něco, co vyrobí, z čeho se dům
        // staví". Bez tohohle kroku město čekalo na prkna, která nikdo nedělal.
        int missing = _needs.MissingBuildMaterial(sim, ranked[0]);
        return missing >= 0 && TrySupply(sim, missing, rng, buildingCount);
    }

    /// <summary>
    /// Postaví výrobnu chybějící suroviny (a sama ji nespotřebovává, ať se
    /// nezaloží kruh). Tohle je ten článek, který guvernérovi chyběl: spojuje
    /// „co chci postavit" s „z čeho se to staví".
    /// </summary>
    private bool TrySupply(Simulation sim, int resourceIndex, SplitMix64 rng, int buildingCount)
    {
        // Strop na počet výroben JEDNÉ suroviny podle velikosti města: víc pil,
        // než má město lidí, nevyrobí víc prken — jen spolknou materiál a zůstanou
        // prázdné. Změřeno: 158 prázdných budov a 444 pracovních míst na 126
        // obyvatel, protože tuhle cestu nic neomezovalo.
        //
        // Zkoušel jsem i jemnější pravidlo (strop výroben na obyvatele místo
        // vazby na prázdné budovy) — měřením vyšlo hůř: tři stagnující seedy
        // místo dvou. Zůstává tedy tohle, hrubší, ale prokazatelně lepší.
        if (sim.IdleBuildings > 0 && ProducerCount(sim, resourceIndex) > 0)
        {
            return false;
        }

        Span<int> ranked = stackalloc int[_content.Buildings.Count];
        int count = 0;
        Span<int> scores = stackalloc int[_content.Buildings.Count];

        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            if (!_content.Buildings[defIndex].AutoBuild || !sim.IsBuildingUnlocked(defIndex))
            {
                continue;
            }

            var capability = _capabilities[defIndex];
            if (!capability.Outputs.Contains(resourceIndex) || capability.NeedsInputs.Contains(resourceIndex))
            {
                continue;
            }

            // Přednost té, která si vystačí s tím, co město má — jinak by se
            // stavěl řetěz výroben, ze kterých ani jedna nemá z čeho brát.
            int score = _needs.MissingBuildMaterial(sim, defIndex) < 0 ? 2 : 1;
            int at = count++;
            while (at > 0 && scores[at - 1] < score)
            {
                scores[at] = scores[at - 1];
                ranked[at] = ranked[at - 1];
                at--;
            }

            scores[at] = score;
            ranked[at] = defIndex;
        }

        int tries = Math.Min(AnchorAttempts, buildingCount);
        for (int attempt = 0; attempt < tries; attempt++)
        {
            var anchor = sim.Buildings[(int)(rng.Next() % (ulong)buildingCount)];
            for (int i = 0; i < count; i++)
            {
                if (TryBuildNear(sim, ranked[i], anchor.X, anchor.Y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Kolik dostavěných budov tuhle surovinu vyrábí.</summary>
    private int ProducerCount(Simulation sim, int resourceIndex)
    {
        var buildings = sim.Buildings;
        int count = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].IsComplete && _capabilities[buildings[i].DefIndex].Outputs.Contains(resourceIndex))
            {
                count++;
            }
        }

        return count;
    }


    /// <summary>
    /// Nemělo by stavět prázdnou výrobnu? Když už teď zůstávají budovy bez lidí,
    /// další jen spolkne materiál, který chybí na domy a služby — a nevyrobí nic.
    ///
    /// <para>Změřeno: 158 prázdných budov, 444 pracovních míst na 126 obyvatel,
    /// spokojenost 0.30. Guvernér stavěl výrobny místo trhů, protože mu nikdo
    /// neřekl, že je nemá kdo obsadit.</para>
    ///
    /// <para>Dvě výjimky, obě zjištěné měřením:</para>
    /// <list type="bullet">
    /// <item><b>Hlad</b> — bez jídla město neporoste nikdy, takže na pole se staví,
    /// i když je lidí málo; jinak by se z nedostatku lidí nešlo dostat.</item>
    /// <item><b>Doplnění chybějícího materiálu</b> (<see cref="TrySupply"/>) — ta
    /// stavba je cílená na konkrétní překážku. Když se zakázala i ona, město
    /// uvázlo na 22 obyvatelích: nesmělo postavit pilu, tím pádem nebyla prkna,
    /// tím pádem ani domy.</item>
    /// </list>
    /// </summary>
    private bool IsPointlessNow(Simulation sim, int defIndex, CityNeed need)
    {
        if (need == CityNeed.Food)
        {
            return false;
        }

        int slots = _content.Buildings[defIndex].WorkerSlots;
        if (slots <= 0)
        {
            return false; // domy, sklady a služby lidi nepotřebují
        }

        return sim.TotalWorkerSlots + slots > sim.Population * WorkerSlotsPerPerson;
    }

    /// <summary>
    /// Kolik pracovních míst na obyvatele je ještě rozumné. Nad tím už jen přibývá
    /// prázdných budov — trochu rezervy je v pořádku, město roste.
    /// </summary>
    private const double WorkerSlotsPerPerson = 2.5;

    /// <summary>
    /// Kolik různých kotev se zkusí, než to guvernér pro tenhle interval vzdá.
    /// Nízké číslo drží náklad malý (běží jednou za interval, ne každý tik) a
    /// zároveň spolehlivě najde okraj zástavby.
    /// </summary>
    private const int AnchorAttempts = 8;

    /// <summary>
    /// Seřadí auto-stavitelné budovy podle toho, jak dobře pokrývají danou
    /// potřebu (nejlepší první). Vrací, kolik jich do výběru vůbec patří.
    ///
    /// <para>Insertion sort nad desítkami definic, jednou za interval — proti
    /// alokaci seznamu a komparátoru je to levnější a čitelnější.</para>
    /// </summary>
    private int RankCandidates(Simulation sim, CityNeed need, Span<int> ranked)
    {
        Span<int> scores = stackalloc int[_content.Buildings.Count];
        int count = 0;
        int missingInput = need == CityNeed.Inputs ? _needs.DriedUpInput(sim) : -1;

        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            if (!_content.Buildings[defIndex].AutoBuild || !sim.IsBuildingUnlocked(defIndex))
            {
                continue;
            }

            int score = ScoreFor(defIndex, need, missingInput);
            if (score <= 0 || IsPointlessNow(sim, defIndex, need))
            {
                continue; // tuhle potřebu neřeší (nebo by ji stejně neobsloužil)
            }

            int at = count++;
            while (at > 0 && scores[at - 1] < score)
            {
                scores[at] = scores[at - 1];
                ranked[at] = ranked[at - 1];
                at--;
            }

            scores[at] = score;
            ranked[at] = defIndex;
        }

        return count;
    }

    /// <summary>
    /// Jak dobře budova pokrývá potřebu. 0 = vůbec, takže se nepostaví — díky
    /// tomu se z guvernéra nestane stavitel všeho, co je zrovna k mání.
    /// </summary>
    private int ScoreFor(int defIndex, CityNeed need, int missingInput)
    {
        var capability = _capabilities[defIndex];
        switch (need)
        {
            case CityNeed.Food:
                return capability.ProducesFood ? 100 : 0;

            case CityNeed.Inputs:
                // Budova, která chybějící surovinu vyrábí — a sama ji nepotřebuje,
                // jinak by se postavila jen další hladový krk ve stejném řetězci.
                if (missingInput < 0 || !capability.Outputs.Contains(missingInput))
                {
                    return 0;
                }

                return capability.NeedsInputs.Contains(missingInput) ? 0 : 90;

            case CityNeed.Services:
                return capability.Services;

            case CityNeed.Housing:
                return capability.Housing;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Guvernérovo vylepšení: povýší první budovu, kterou nastavená míra pokrývá
    /// a na kterou jsou suroviny. Vyšší stupeň zabírá víc kategorií i víc vylepšení
    /// za interval (viz <see cref="Simulation.AutoUpgradeCovers"/>).
    /// </summary>
    private bool TryAutoUpgrade(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.HasUpgrade && sim.AutoUpgradeCovers(def.Category) && sim.CanUpgrade(i) == PlacementResult.Ok)
            {
                return sim.TryUpgradeBuilding(i) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>
    /// Sloučí první blok 2×2, na který guvernér narazí. Jeden za interval —
    /// slučování je vidět a je nevratné, takže má město měnit postupně, ne
    /// přestavět se hráči pod rukama během jednoho tiku.
    /// </summary>
    private static bool TryAutoMerge(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (sim.TryFindMergeGroup(buildings[i].X, buildings[i].Y, out var group)
                && sim.CanMerge(group) == PlacementResult.Ok)
            {
                return sim.TryMerge(group.X, group.Y) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>Povýší první bydlení, které má vylepšení a hráč na něj má — hustota bez záboru místa.</summary>
    private bool TryDensify(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].HousingCapacity > 0
                && sim.CanUpgrade(i) == PlacementResult.Ok)
            {
                return sim.TryUpgradeBuilding(i) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>
    /// Kolik volných míst se ohodnotí, než se z nich vybere to nejhezčí.
    /// Ofsety jsou seřazené od nejbližších, takže tahle hrstka pokryje okolí
    /// kotvy; procházet celý kruh by jen stálo čas na horších místech.
    /// </summary>
    private const int PlacementCandidates = 20;

    private bool TryBuildNear(Simulation sim, int defIndex, int anchorX, int anchorY)
    {
        // Rezerva guvernéra: co si hráč schoval, automatika neutratí. Kontrola je
        // před hledáním místa — cena na místě nezávisí.
        if (!sim.AutomationCanSpend(_content.Buildings[defIndex].BuildCost))
        {
            return false;
        }

        int bestX = 0, bestY = 0, bestScore = int.MinValue;
        int seen = 0;

        foreach (var (offsetX, offsetY) in _searchOffsets)
        {
            int x = anchorX + offsetX;
            int y = anchorY + offsetY;
            var result = sim.CanPlace(defIndex, x, y);
            if (result == PlacementResult.NotEnoughResources)
            {
                // Cena nezávisí na místě — další hledání nemá smysl.
                return false;
            }

            if (result != PlacementResult.Ok)
            {
                continue;
            }

            int score = PlacementScore(sim, x, y) - (offsetX * offsetX + offsetY * offsetY);
            if (score > bestScore)
            {
                bestScore = score;
                bestX = x;
                bestY = y;
            }

            if (++seen >= PlacementCandidates)
            {
                break;
            }
        }

        return bestScore != int.MinValue
            && sim.TryPlaceBuilding(defIndex, bestX, bestY) == PlacementResult.Ok;
    }

    /// <summary>
    /// Jak hezky místo zapadne do města.
    ///
    /// <para>Dřív se bralo první volné políčko od kotvy, takže město rostlo jako
    /// beztvará skvrna — přesně ta „náhodná změť", kvůli které auto-stavba
    /// vypadala jako chyba, ne jako civilizace. Tři pravidla stačí, aby z toho
    /// bylo město: <b>stavět k cestám</b> (ulice vznikají samy), <b>lepit se
    /// k sousedům</b> (zástavba je celistvá, ne roztroušená) a <b>nezazdít se</b>
    /// (domek obklopený ze všech stran nemá kudy ven a vypadá to špatně).</para>
    ///
    /// <para>Běží jen na hrstce kandidátů jednou za interval, ne v tikové smyčce.</para>
    /// </summary>
    private static int PlacementScore(Simulation sim, int x, int y)
    {
        int roads = 0;
        int neighbours = 0;

        // Jen čtyři ortogonální sousedé: ulice a fronta domů se poznají podle
        // stran, ne podle rohů.
        if (sim.HasRoadAt(x - 1, y)) { roads++; } else if (sim.IsOccupied(x - 1, y)) { neighbours++; }
        if (sim.HasRoadAt(x + 1, y)) { roads++; } else if (sim.IsOccupied(x + 1, y)) { neighbours++; }
        if (sim.HasRoadAt(x, y - 1)) { roads++; } else if (sim.IsOccupied(x, y - 1)) { neighbours++; }
        if (sim.HasRoadAt(x, y + 1)) { roads++; } else if (sim.IsOccupied(x, y + 1)) { neighbours++; }

        int score = (roads * RoadWeight) + (neighbours * NeighbourWeight);

        // Zazděné místo: ani jedna volná strana. Takový dům je slepý a shluk
        // z nich vypadá jako slitek, ne jako čtvrť.
        if (roads + neighbours == 4)
        {
            score -= EnclosedPenalty;
        }

        return score;
    }

    /// <summary>Cesta u domu váží nejvíc — kolem ní vznikají ulice.</summary>
    private const int RoadWeight = 9;

    /// <summary>Soused drží zástavbu pohromadě, ale slabšeji než cesta.</summary>
    private const int NeighbourWeight = 3;

    /// <summary>Sráží místo bez jediné volné strany.</summary>
    private const int EnclosedPenalty = 14;
}
