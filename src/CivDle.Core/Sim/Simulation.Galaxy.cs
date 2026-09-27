using CivDle.Core.Content;
using CivDle.Core.Galaxy;

namespace CivDle.Core.Sim;

/// <summary>
/// Co simulace jednoho světa umí pro galaxii (svety-design.md 2.2–2.6):
/// přistání kolonie, vklad do kolonizační lodi, sdílený Odkaz a návrat ze
/// souhrnu. Simulace o galaxii nic neví — tohle jsou jen operace, které nad ní
/// volá <see cref="GalaxySession"/>. Samostatný soubor, ať se galaktické věci
/// nerozlézají po jádru, které je starší než galaxie.
/// </summary>
public sealed partial class Simulation
{
    /// <summary>Kde přistál přistávací modul kolonie; −1 = Domovina (nepřistávala).</summary>
    public int LandingX { get; private set; } = -1;

    /// <summary>Kde přistál přistávací modul kolonie; −1 = Domovina.</summary>
    public int LandingY { get; private set; } = -1;

    /// <summary>Je to kolonie, která už přistála?</summary>
    public bool HasLanded => LandingX != -1 || LandingY != -1;

    /// <summary>
    /// Přistání kolonie: přistávací modul stojí hotový (přiletěl, nestaví se),
    /// náklad lodi je ve skladu. Modul se ve hře postavit nedá, proto se
    /// umisťuje mimo běžné <see cref="CanPlace"/> — kontroluje se jen půda.
    /// </summary>
    /// <returns><see cref="PlacementResult.Ok"/>, nebo proč modul na místo nesedne.</returns>
    public PlacementResult Land(int x, int y)
    {
        int module = _content.World.LandingModuleIndex;
        if (module < 0)
        {
            return PlacementResult.NotUnlocked; // Domovina nepřistává
        }

        var result = PlaceModule(module, x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        foreach (var item in _content.World.StartingKit)
        {
            AddResource(item.ResourceIndex, item.Amount);
        }

        LandingX = x;
        LandingY = y;
        Fog.Reveal(x, y, FogRevealRadius * 2);
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Po Vzestupu kolonie (nová éra na čisté mapě) se přistání opakuje: modul
    /// znovu na původním místě, znovu výbava. Bez toho by kolonie po Vzestupu
    /// neměla kde bydlet a stála by.
    /// </summary>
    private void RelandAfterReset()
    {
        if (!HasLanded)
        {
            return;
        }

        int module = _content.World.LandingModuleIndex;
        for (int radius = 0; radius <= 24; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == radius
                        && PlaceModule(module, LandingX + dx, LandingY + dy) == PlacementResult.Ok)
                    {
                        foreach (var item in _content.World.StartingKit)
                        {
                            AddResource(item.ResourceIndex, item.Amount);
                        }

                        return;
                    }
                }
            }
        }
    }

    /// <summary>Obnova místa přistání ze savu.</summary>
    internal void RestoreLanding(int x, int y)
    {
        LandingX = x;
        LandingY = y;
    }

    private PlacementResult PlaceModule(int module, int x, int y)
    {
        var def = _content.Buildings[module];
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                if (!IsTileFree(tileX, tileY))
                {
                    return PlacementResult.Occupied;
                }

                if (!def.IsBiomeAllowed(_cachedTerrain.BiomeAt(tileX, tileY)))
                {
                    return PlacementResult.WrongBiome;
                }
            }
        }

        AddBuilding(module, x, y, progress: 0f);
        int index = _buildingCount - 1;
        if (_buildings[index].BuildTicksRemaining > 0)
        {
            _buildings[index].BuildTicksRemaining = 0;
            CompleteConstruction(index, def);
        }
        else
        {
            ApplyBuildingBonuses(def);
        }

        return PlacementResult.Ok;
    }

    /// <summary>
    /// Kolonisté si přivezou, co Domovina umí: technologie se stejným ID,
    /// které má i obsah kolonie (sklady, služby, věda — co se sdílí). Větev
    /// světa se zkoumá od začátku.
    /// </summary>
    /// <returns>Kolik technologií kolonie dostala.</returns>
    public int GrantKnownTechs(IEnumerable<string> techIds)
    {
        int granted = 0;
        foreach (string id in techIds)
        {
            if (_content.Techs.TryIndexOf(id, out int tech) && _techLevel[tech] == 0)
            {
                GrantTechFree(tech);
                granted++;
            }
        }

        return granted;
    }

    /// <summary>ID vyzkoumaných technologií (pro předání kolonii).</summary>
    public IEnumerable<string> ResearchedTechIds()
    {
        for (int i = 0; i < _techLevel.Length; i++)
        {
            if (_techLevel[i] > 0)
            {
                yield return _content.Techs[i].Id;
            }
        }
    }

    /// <summary>
    /// Vloží do cizí stavby (kolonizační loď) přebytky nad rezervou guvernéra —
    /// stejně jako vklad do projektu (<see cref="TryInvestInProject"/>): víc,
    /// než kolik stupeň chce, se nevezme, a rezerva na stavbu zůstane.
    /// </summary>
    /// <param name="cost">Cena stupně.</param>
    /// <param name="invested">Už vloženo, indexováno surovinou; doplní se.</param>
    /// <returns>Kolik se vložilo celkem.</returns>
    public double InvestSurplus(IReadOnlyList<ResourceAmount> cost, double[] invested)
    {
        double total = 0;
        for (int i = 0; i < cost.Count; i++)
        {
            int resource = cost[i].ResourceIndex;
            double missing = cost[i].Amount - invested[resource];
            double available = Sandbox ? missing : _resources[resource] - Claim.Amounts[resource];
            double put = Math.Min(missing, available);
            if (put <= 0)
            {
                continue;
            }

            if (!Sandbox)
            {
                _resources[resource] -= put;
                _ledger.RecordConsumed(resource, put, ConsumptionKind.Purchases);
            }

            invested[resource] += put;
            total += put;
        }

        return total;
    }

    /// <summary>
    /// Odkaz platí pro celou galaxii (svety-design.md 2.6): při přechodu na
    /// jiný svět si ho nový svět převezme — hloubku, body i úrovně. Úrovně
    /// jménem, ne indexem: vylepšení Odkazu jsou struktura sdílená všemi světy,
    /// ale pořadí se může lišit.
    /// </summary>
    public void CopyLegacyFrom(Simulation other)
    {
        _legacy.Restore(other.LegacyDepth, other.LegacyPoints);
        var theirs = other._content.LegacyUpgrades;
        foreach (int level in other.LegacyPurchasedLevels())
        {
            if (_content.LegacyUpgrades.TryIndexOf(theirs[level].Id, out int mine))
            {
                _legacy.RestoreLevel(mine);
            }
        }

        RecomputeBonuses();
    }

    /// <summary>
    /// Návrat na svět po krátké nepřítomnosti (svety-design.md 2.4): zásoby
    /// a lidé ze souhrnu posunutého o dobu nepřítomnosti. Zástavba zůstává —
    /// guvernér za krátkou dobu nestavěl (přepnutí musí být rychlé).
    /// </summary>
    internal void ResumeFrom(WorldSummary summary)
    {
        for (int r = 0; r < _resources.Length; r++)
        {
            double stock = summary.StockOf(_content.Resources[r].Id);
            _resources[r] = Math.Clamp(stock, 0, Math.Max(_storageCaps[r], _resources[r]));
        }

        Population = Math.Max(0, Math.Min(summary.Population, Math.Max(Population, Math.Min(HousingCapacity, PopulationCap))));
    }
}
