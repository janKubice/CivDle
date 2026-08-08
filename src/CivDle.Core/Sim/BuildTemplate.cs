using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jedna budova v šabloně: typ a posun proti levému hornímu rohu.</summary>
/// <param name="BuildingId">Stabilní ID budovy (ne index — šablona přežije změnu dat i mody).</param>
/// <param name="Dx">Posun v dlaždicích doprava.</param>
/// <param name="Dy">Posun v dlaždicích dolů.</param>
public sealed record TemplatePart(string BuildingId, int Dx, int Dy);

/// <summary>
/// Uložený kus zástavby, který jde postavit znovu (bod 44).
///
/// <para>Proč vůbec: rozvržení, které si hráč vymyslí — čtyři domy, sklad
/// a ulice mezi nimi — se v pozdní hře staví pořád dokola. Šablona z toho
/// dělá jedno kliknutí místo dvaceti, a přitom to <b>není</b> automatika:
/// hráč pořád vybírá, kam to položí, a platí plnou cenu.</para>
///
/// <para>Uvnitř jsou <b>stringová ID</b>, ne indexy. Šablona se ukládá do
/// profilu hráče (přežije Vzestup i novou hru), takže musí zůstat čitelná
/// i po přeuspořádání dat nebo zapnutí modu — budova, která zmizela, se při
/// pokládání prostě přeskočí.</para>
/// </summary>
/// <param name="Name">Jméno, které si hráč zvolil.</param>
/// <param name="Buildings">Budovy v šabloně.</param>
/// <param name="Roads">Dlaždice silnic v šabloně.</param>
public sealed record BuildTemplate(
    string Name,
    IReadOnlyList<TemplatePart> Buildings,
    IReadOnlyList<(int Dx, int Dy)> Roads)
{
    /// <summary>Prázdná šablona (nic se nepoloží).</summary>
    public static BuildTemplate Empty { get; } =
        new(string.Empty, Array.Empty<TemplatePart>(), Array.Empty<(int, int)>());

    /// <summary>Má šablona vůbec co pokládat?</summary>
    public bool IsEmpty => Buildings.Count == 0 && Roads.Count == 0;

    /// <summary>Šířka v dlaždicích (podle nejvzdálenější části, ne podle půdorysů).</summary>
    public int Width => Span(part => part.Dx, road => road.Dx);

    /// <summary>Výška v dlaždicích.</summary>
    public int Height => Span(part => part.Dy, road => road.Dy);

    private int Span(Func<TemplatePart, int> ofBuilding, Func<(int Dx, int Dy), int> ofRoad)
    {
        int max = -1;
        foreach (var part in Buildings)
        {
            max = Math.Max(max, ofBuilding(part));
        }

        foreach (var road in Roads)
        {
            max = Math.Max(max, ofRoad(road));
        }

        return max + 1;
    }
}

/// <summary>
/// Sejmutí a pokládání šablon. Statická pomůcka nad simulací — nedrží stav,
/// takže patří k <see cref="Simulation"/> stejně jako <see cref="BulkBuilder"/>:
/// překládá gesto hráče na posloupnost obyčejných příkazů.
/// </summary>
public static class TemplateTool
{
    /// <summary>
    /// Sejme obdélník mapy do šablony. Bere jen budovy, jejichž <b>levý horní
    /// roh</b> leží uvnitř — jinak by šablona nesla půlku sousedovy továrny.
    /// </summary>
    public static BuildTemplate Capture(
        Simulation simulation, GameContent content, string name, int x0, int y0, int x1, int y1)
    {
        int minX = Math.Min(x0, x1), maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1), maxY = Math.Max(y0, y1);

        var buildings = new List<TemplatePart>();
        var all = simulation.Buildings;
        for (int i = 0; i < all.Length; i++)
        {
            ref readonly var building = ref all[i];
            if (building.X < minX || building.X > maxX || building.Y < minY || building.Y > maxY)
            {
                continue;
            }

            buildings.Add(new TemplatePart(
                content.Buildings[building.DefIndex].Id, building.X - minX, building.Y - minY));
        }

        var roads = new List<(int Dx, int Dy)>();
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (simulation.HasRoadAt(x, y))
                {
                    roads.Add((x - minX, y - minY));
                }
            }
        }

        return new BuildTemplate(name, buildings, roads);
    }

    /// <summary>
    /// Kolik budov ze šablony by na daném místě šlo postavit. Slouží náhledu:
    /// hráč vidí dopředu, jestli se to tam vejde.
    /// </summary>
    public static int CountPlaceable(
        Simulation simulation, GameContent content, BuildTemplate template, int originX, int originY)
    {
        int count = 0;
        foreach (var part in template.Buildings)
        {
            if (content.Buildings.TryIndexOf(part.BuildingId, out int defIndex)
                && simulation.CanPlace(defIndex, originX + part.Dx, originY + part.Dy) == PlacementResult.Ok)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Postaví šablonu. Co nejde (obsazeno, nedostatek surovin, zamčená budova),
    /// se <b>přeskočí</b> — celá šablona se nezahazuje kvůli jednomu domku na
    /// skále. Vrací počet skutečně postavených budov.
    ///
    /// <para>Silnice se kladou až po budovách: cesta pod plánovanou budovou by
    /// jinak zabrala dlaždici a stavba by neprošla.</para>
    /// </summary>
    public static int Place(
        Simulation simulation, GameContent content, BuildTemplate template, int originX, int originY)
    {
        int built = 0;

        // Dávkové pokládání: přepočet napojení na silnice se udělá jednou na
        // konci, ne po každém domku (u velké šablony jinak zbytečná práce).
        simulation.BeginBatchPlacement();
        try
        {
            foreach (var part in template.Buildings)
            {
                if (!content.Buildings.TryIndexOf(part.BuildingId, out int defIndex))
                {
                    continue; // budova z dat zmizela (jiná verze, vypnutý mod)
                }

                if (simulation.TryPlaceBuilding(defIndex, originX + part.Dx, originY + part.Dy) == PlacementResult.Ok)
                {
                    built++;
                }
            }

            foreach (var (dx, dy) in template.Roads)
            {
                simulation.TryBuildRoad(originX + dx, originY + dy);
            }
        }
        finally
        {
            simulation.EndBatchPlacement();
        }

        return built;
    }
}
