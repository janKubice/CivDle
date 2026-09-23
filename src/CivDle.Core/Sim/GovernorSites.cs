using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Kam postavit budovu, která nemůže stát kdekoli.
///
/// <para><b>Proč to existuje:</b> guvernér hledal místo do šesti dlaždic kolem
/// náhodné budovy ve městě. Jenže dřevorubec smí stát jen v lese, lom jen na
/// skále a rybárna jen na pláži — a když město stálo na louce, neměl je kam
/// postavit vůbec. Z venku to vypadalo, že „neumí stavět pily": chyběla prkna,
/// na pilu chybělo dřevo, na dřevo chyběl dřevorubec a ten se nevešel nikam.</para>
///
/// <para>Dvě hledání:</para>
/// <list type="bullet">
/// <item><b>Těžba z okolí</b> (<see cref="TryFindHarvestSite"/>): najde nejbližší
/// les či skálu, kterou ještě nevytěžuje jiná budova téhož druhu, a v ní místo
/// s nejvíc uzly v dosahu. Díky tomu se dřevorubci rozkládají po lesích, místo
/// aby se tlačili na jednom vykáceném paloučku.</item>
/// <item><b>Záložní hledání</b> (<see cref="TryFindAnySite"/>): kruhy od středu
/// města k prvnímu místu, kam budova smí — pro pole, rybárny a vše, co se kolem
/// zástavby nevešlo.</item>
/// </list>
///
/// <para>Výkon: běží jen tehdy, když guvernér takovou budovu opravdu chce, ne
/// každý interval. Hledání je omezené poloměrem a řídkým vzorkováním.</para>
///
/// <para>Vrstva: čistá simulace, deterministická (žádná náhoda, pořadí kruhů je
/// pevné).</para>
/// </summary>
internal sealed class GovernorSites
{
    /// <summary>Jak daleko od středu města se hledá les nebo skála.</summary>
    private const int HarvestSearchRadius = 40;

    /// <summary>Kolik použitelných shluků surovin stačí najít, než se vybere nejlepší.</summary>
    private const int ClustersToInspect = 6;

    /// <summary>
    /// Kolik shluků se nejvýš vyzkouší (i neúspěšně). Strop drží cenu hledání,
    /// když kolem města nic použitelného není.
    /// </summary>
    private const int MaxClusterAttempts = 24;

    /// <summary>Jak daleko od středu města se hledá místo pro ostatní budovy.</summary>
    private const int AnySiteRadius = 28;

    /// <summary>Uzel, který dává přesně to, co budova vyrábí, se počítá dvakrát.</summary>
    private const int MatchingNodeWeight = 2;

    /// <summary>Pod tolik bodů uzlů se místo nevyplatí — dřevorubec by za chvíli stál znovu.</summary>
    private const int MinimumNodeScore = 6;

    private readonly GameContent _content;

    /// <summary>Pomocný seznam ploch, které už někdo téhož druhu vytěžuje (drží se mezi voláními).</summary>
    private readonly List<(int X, int Y)> _covered = new();

    public GovernorSites(GameContent content) => _content = content;

    /// <summary>
    /// Najde místo pro budovu, která těží z okolí (dřevorubec, lom, lovci).
    /// </summary>
    /// <param name="sim">Simulace.</param>
    /// <param name="defIndex">Co se staví.</param>
    /// <param name="ignoreBuilding">Budova, která se stěhuje (její plocha se nepočítá jako obsazená); −1 = žádná.</param>
    /// <param name="forMove">Hledá se místo pro přesun (cena se neplatí, kontroluje se jen půda).</param>
    /// <param name="x">Nalezené místo.</param>
    /// <param name="y">Nalezené místo.</param>
    public bool TryFindHarvestSite(Simulation sim, int defIndex, int ignoreBuilding, bool forMove, out int x, out int y)
    {
        var def = _content.Buildings[defIndex];
        int radius = Math.Max(1, def.TerrainHarvestRadius);
        CollectCovered(sim, defIndex, ignoreBuilding);

        // Napřed jen uzly toho, co budova vyrábí (dřevorubec hledá stromy, lom
        // skálu). Dřív se bral první uzel jakéhokoli druhu: když byly blíž
        // k městu hory, všech šest pokusů padlo na skálu, kam dřevorubec nesmí,
        // a les o kus dál už se nezkusil — guvernér pak stavěl pilu za pilou,
        // protože dřevorubce „neměl kam dát". Jiné uzly jsou jen záloha pro
        // budovy, jejichž výrobek na mapě jako uzel neroste.
        return TryFindHarvestSite(sim, def, defIndex, radius, ignoreBuilding, forMove, matchingOnly: true, out x, out y)
            || TryFindHarvestSite(sim, def, defIndex, radius, ignoreBuilding, forMove, matchingOnly: false, out x, out y);
    }

    private bool TryFindHarvestSite(
        Simulation sim, BuildingDef def, int defIndex, int radius, int ignoreBuilding, bool forMove, bool matchingOnly,
        out int x, out int y)
    {
        x = y = 0;
        int bestScore = int.MinValue;
        int found = 0;
        int attempts = 0;
        int centerX = sim.CityCenterX;
        int centerY = sim.CityCenterY;
        var outputs = def.Recipe?.Outputs;

        // Kruhy od středu města s krokem 2: les se najde spolehlivě, a přitom se
        // nesahá na každou dlaždici v okruhu čtyřiceti. Počítají se jen shluky,
        // kde budova opravdu může stát — neúspěšný pokus hledání nezastaví,
        // dokud nedojde strop pokusů.
        int coveredBefore = _covered.Count;
        for (int ring = 0; ring <= HarvestSearchRadius && found < ClustersToInspect && attempts < MaxClusterAttempts; ring += 2)
        {
            for (int i = 0; i < RingLength(ring) && found < ClustersToInspect && attempts < MaxClusterAttempts; i += 2)
            {
                RingTile(ring, i, out int dx, out int dy);
                int nodeX = centerX + dx;
                int nodeY = centerY + dy;
                if (!sim.TryPeekNode(nodeX, nodeY, out int resource)
                    || (matchingOnly && !Produces(outputs, resource))
                    || IsCovered(nodeX, nodeY, radius))
                {
                    continue;
                }

                attempts++;
                if (TryBestPlacementAround(sim, def, defIndex, nodeX, nodeY, radius, ignoreBuilding, forMove,
                        out int siteX, out int siteY, out int score))
                {
                    found++;

                    // Blízko města je lepší: dlouhá cesta znamená pomalý svoz.
                    score -= ring / 2;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        x = siteX;
                        y = siteY;
                    }
                }

                // Shluk už je prozkoumaný — další vzorek o kus dál, ať se nezkoumá tentýž les.
                _covered.Add((nodeX, nodeY));
            }
        }

        // Prozkoumané shluky nejsou zabrané budovou — druhé kolo (a počítání
        // uzlů) je musí vidět znovu.
        _covered.RemoveRange(coveredBefore, _covered.Count - coveredBefore);
        return bestScore != int.MinValue;
    }

    /// <summary>
    /// Kruhy od středu města k prvnímu místu, kam budova smí a kde ji plán sídla
    /// nezakazuje. Záloha pro vše, co se nevešlo kolem zástavby (pole na louce
    /// za městem, rybárna na pláži).
    /// </summary>
    public bool TryFindAnySite(Simulation sim, int defIndex, out int x, out int y)
    {
        var def = _content.Buildings[defIndex];
        int centerX = sim.CityCenterX;
        int centerY = sim.CityCenterY;
        for (int ring = 1; ring <= AnySiteRadius; ring++)
        {
            for (int i = 0; i < RingLength(ring); i++)
            {
                RingTile(ring, i, out int dx, out int dy);
                x = centerX + dx;
                y = centerY + dy;
                if (IsBuildable(sim, def, defIndex, x, y, ignoreBuilding: -1, forMove: false))
                {
                    return true;
                }
            }
        }

        x = y = 0;
        return false;
    }

    /// <summary>
    /// Kolik uzlů má budova na daném místě v dosahu (vážené: vlastní surovina
    /// dvakrát). Uzly v dosahu jiné budovy téhož druhu se nepočítají — ty už
    /// někdo těží. Veřejné kvůli testům a nápovědě.
    /// </summary>
    public int NodeScore(Simulation sim, int defIndex, int x, int y)
    {
        CollectCovered(sim, defIndex, ignoreBuilding: -1);
        var def = _content.Buildings[defIndex];
        return CountNodes(sim, def, x, y, Math.Max(1, def.TerrainHarvestRadius), stride: 1);
    }

    private bool TryBestPlacementAround(
        Simulation sim, BuildingDef def, int defIndex, int nodeX, int nodeY, int radius,
        int ignoreBuilding, bool forMove, out int bestX, out int bestY, out int bestScore)
    {
        bestX = bestY = 0;
        bestScore = int.MinValue;

        // Místa se zkouší po dvou dlaždicích kolem nalezeného uzlu — stačí to na
        // nalezení dobrého místa a je to čtyřikrát levnější než každá dlaždice.
        for (int oy = -radius; oy <= radius; oy += 2)
        {
            for (int ox = -radius; ox <= radius; ox += 2)
            {
                int x = nodeX + ox;
                int y = nodeY + oy;
                if (!IsBuildable(sim, def, defIndex, x, y, ignoreBuilding, forMove))
                {
                    continue;
                }

                int nodes = CountNodes(sim, def, x, y, radius, stride: 2);
                if (nodes < MinimumNodeScore / 2) // řídké vzorkování vidí zhruba čtvrtinu
                {
                    continue;
                }

                int score = nodes * 4 - (ox * ox + oy * oy) / 4;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        return bestScore != int.MinValue;
    }

    private bool IsBuildable(Simulation sim, BuildingDef def, int defIndex, int x, int y, int ignoreBuilding, bool forMove)
    {
        var result = forMove ? sim.CanMoveBuilding(ignoreBuilding, x, y) : sim.CanPlace(defIndex, x, y);
        return result == PlacementResult.Ok
            && !CityLayout.IsReservedForStreet(x, y)
            && sim.PlanAt(x, y).AllowsCategory(def.Category);
    }

    private int CountNodes(Simulation sim, BuildingDef def, int x, int y, int radius, int stride)
    {
        var outputs = def.Recipe?.Outputs;
        int score = 0;
        for (int dy = -radius; dy <= radius; dy += stride)
        {
            for (int dx = -radius; dx <= radius; dx += stride)
            {
                int tx = x + dx;
                int ty = y + dy;
                if (!sim.TryPeekNode(tx, ty, out int resource) || IsCovered(tx, ty, radius))
                {
                    continue;
                }

                score += Produces(outputs, resource) ? MatchingNodeWeight : 1;
            }
        }

        return score;
    }

    private static bool Produces(IReadOnlyList<ResourceAmount>? outputs, int resource)
    {
        if (outputs is null)
        {
            return false;
        }

        for (int i = 0; i < outputs.Count; i++)
        {
            if (outputs[i].ResourceIndex == resource)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Zapamatuje si, kde už stojí budovy téhož druhu — jejich okolí je zabrané.</summary>
    private void CollectCovered(Simulation sim, int defIndex, int ignoreBuilding)
    {
        _covered.Clear();
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (i != ignoreBuilding && buildings[i].DefIndex == defIndex)
            {
                _covered.Add((buildings[i].X, buildings[i].Y));
            }
        }
    }

    /// <summary>Leží dlaždice v dosahu některé budovy téhož druhu (čtverec, jako těží výroba)?</summary>
    private bool IsCovered(int x, int y, int radius)
    {
        for (int i = 0; i < _covered.Count; i++)
        {
            if (Math.Abs(_covered[i].X - x) <= radius && Math.Abs(_covered[i].Y - y) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Kolik dlaždic má čtvercový kruh o daném poloměru (0 = jen střed).</summary>
    private static int RingLength(int ring) => ring == 0 ? 1 : ring * 8;

    /// <summary>
    /// <paramref name="index"/>-tá dlaždice čtvercového kruhu, po obvodu. Pevné pořadí
    /// — stejný svět dá vždy stejné místo.
    /// </summary>
    private static void RingTile(int ring, int index, out int dx, out int dy)
    {
        if (ring == 0)
        {
            dx = dy = 0;
            return;
        }

        int side = ring * 2;
        int edge = index / side;
        int offset = index % side;
        switch (edge)
        {
            case 0: dx = -ring + offset; dy = -ring; break;
            case 1: dx = ring; dy = -ring + offset; break;
            case 2: dx = ring - offset; dy = ring; break;
            default: dx = -ring; dy = ring - offset; break;
        }
    }
}
