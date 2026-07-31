using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Jedno místo v plánu hromadné stavby: kam by budova šla a jestli tam projde.
/// </summary>
/// <param name="X">Levý horní roh půdorysu.</param>
/// <param name="Y">Levý horní roh půdorysu.</param>
/// <param name="Result">Proč to tam (ne)jde — UI z toho barví ducha.</param>
public readonly record struct BulkSlot(int X, int Y, PlacementResult Result)
{
    /// <summary>Postaví se tenhle kus, až hráč pustí tlačítko?</summary>
    public bool WillBuild => Result == PlacementResult.Ok;
}

/// <summary>
/// Hromadné stavění: jedno gesto místo dvaceti kliků.
///
/// <para>Proč to ve hře je: idle hra stojí na tom, že se opakovaná akce dá
/// udělat naráz. Klikat dvacetkrát za sebou na stejnou chalupu není rozhodnutí,
/// jen daň — a čím větší město, tím vyšší. Tažením se staví řada nebo blok,
/// násobičem (×5, ×25) jedním klikem celá skupina.</para>
///
/// <para>Vrstva: čistě OOP nad simulací (CLAUDE.md) — neběží v tikové smyčce,
/// jen když hráč drží myš. Zná jen veřejné rozhraní <see cref="Simulation"/>,
/// takže se dá testovat bez MonoGame a stejná logika slouží ghostu i stavbě.</para>
///
/// <para>Klíčové pravidlo: <b>plán se nejdřív celý spočítá, teprve pak se staví.</b>
/// Díky tomu vidí hráč přesně to, co dostane — včetně toho, kde mu dojdou
/// suroviny. Rozpočet se proto v plánu vede dopředu, ne až při placení.</para>
/// </summary>
public sealed class BulkBuilder
{
    /// <summary>
    /// Kolik dlaždic se nejvýš prohledá při hledání míst pro ×N. Bez stropu by
    /// „postav 25 přístavů" na vnitrozemské mapě prohledávalo mapu donekonečna.
    /// </summary>
    private const int SearchCellCap = 4000;

    private readonly Simulation _simulation;
    private readonly GameContent _content;

    /// <summary>
    /// Kolik surovin má už plán utracené. Drží se mezi voláními, ať se při tažení
    /// nealokuje každý snímek znovu (plán se počítá při každém pohybu myši).
    /// </summary>
    private readonly double[] _spent;

    public BulkBuilder(Simulation simulation, GameContent content)
    {
        _simulation = simulation;
        _content = content;
        _spent = new double[content.Resources.Count];
    }

    /// <summary>Nastavení hromadné stavby z dat (násobiče a strop).</summary>
    public BulkBuildConfig Config => _content.Gameplay.BulkBuild;

    /// <summary>
    /// Kolik kusů daného typu by hráč zaplatil ze současných zásob. Slouží HUD
    /// („×25" zešedne, když na tolik není) i stropu ×Max.
    /// </summary>
    public int Affordable(int defIndex)
    {
        var cost = _content.Buildings[defIndex].BuildCost;
        if (cost.Count == 0)
        {
            return Config.MaxPerAction;
        }

        int affordable = Config.MaxPerAction;
        for (int i = 0; i < cost.Count; i++)
        {
            double amount = cost[i].Amount;
            if (amount <= 0)
            {
                continue;
            }

            int fits = (int)(_simulation.GetResource(cost[i].ResourceIndex) / amount);
            affordable = Math.Min(affordable, fits);
        }

        return Math.Max(0, affordable);
    }

    /// <summary>
    /// Naplánuje řadu či blok mezi dvěma dlaždicemi (tažení myší). Mřížka je
    /// ukotvená v místě, kde tažení začalo — jinak by budovy pod kurzorem
    /// poskakovaly a hráč by netrefil, co chce.
    /// </summary>
    /// <returns>Kolik kusů se z plánu opravdu postaví.</returns>
    public int PlanArea(int defIndex, int fromX, int fromY, int toX, int toY, List<BulkSlot> into)
    {
        into.Clear();
        Array.Clear(_spent);

        var def = _content.Buildings[defIndex];
        int stepX = Math.Max(1, def.FootprintWidth);
        int stepY = Math.Max(1, def.FootprintHeight);
        int dirX = toX >= fromX ? 1 : -1;
        int dirY = toY >= fromY ? 1 : -1;
        int columns = Math.Abs(toX - fromX) / stepX + 1;
        int rows = Math.Abs(toY - fromY) / stepY + 1;

        int buildable = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (into.Count >= Config.MaxPerAction)
                {
                    return buildable;
                }

                int x = fromX + dirX * column * stepX;
                int y = fromY + dirY * row * stepY;
                var result = Evaluate(defIndex, def, x, y);
                into.Add(new BulkSlot(x, y, result));
                if (result == PlacementResult.Ok)
                {
                    buildable++;
                }
            }
        }

        return buildable;
    }

    /// <summary>
    /// Naplánuje až <paramref name="count"/> kusů co nejblíž kotvě (násobiče
    /// ×5, ×25). Hledá se ve čtvercových prstencích ven, takže skupina vyroste
    /// kompaktně kolem místa, kam hráč klikl, a ne v náhodném chuchvalci.
    ///
    /// <para>Do plánu jdou jen místa, kde se stavět dá — nepovedené dlaždice se
    /// přeskakují. Hromadná stavba by jinak skončila na první skále.</para>
    /// </summary>
    /// <returns>Kolik kusů se z plánu opravdu postaví (může být míň než <paramref name="count"/>).</returns>
    public int PlanNear(int defIndex, int anchorX, int anchorY, int count, List<BulkSlot> into)
    {
        into.Clear();
        Array.Clear(_spent);

        count = Math.Min(count, Config.MaxPerAction);
        if (count <= 0)
        {
            return 0;
        }

        var def = _content.Buildings[defIndex];
        int stepX = Math.Max(1, def.FootprintWidth);
        int stepY = Math.Max(1, def.FootprintHeight);

        int examined = 0;
        for (int ring = 0; into.Count < count && examined < SearchCellCap; ring++)
        {
            bool ringHadCell = false;
            for (int oy = -ring; oy <= ring && into.Count < count; oy++)
            {
                for (int ox = -ring; ox <= ring && into.Count < count; ox++)
                {
                    // Jen okraj prstence — vnitřek už prošel v předchozích kolech.
                    if (Math.Max(Math.Abs(ox), Math.Abs(oy)) != ring)
                    {
                        continue;
                    }

                    ringHadCell = true;
                    examined++;
                    int x = anchorX + ox * stepX;
                    int y = anchorY + oy * stepY;
                    if (Evaluate(defIndex, def, x, y) == PlacementResult.Ok)
                    {
                        into.Add(new BulkSlot(x, y, PlacementResult.Ok));
                    }
                }
            }

            if (!ringHadCell)
            {
                break; // pojistka proti nekonečnu, kdyby prstenec zůstal prázdný
            }
        }

        return into.Count;
    }

    /// <summary>
    /// Postaví, co je v plánu označené jako proveditelné. Vrací počet skutečně
    /// postavených kusů — placení se dělá znovu přes
    /// <see cref="Simulation.TryPlaceBuilding"/>, takže plán nemůže obejít
    /// pravidla ani utratit víc, než hráč má.
    /// </summary>
    public int Build(int defIndex, IReadOnlyList<BulkSlot> plan)
    {
        int placed = 0;
        for (int i = 0; i < plan.Count; i++)
        {
            if (!plan[i].WillBuild)
            {
                continue;
            }

            if (_simulation.TryPlaceBuilding(defIndex, plan[i].X, plan[i].Y) == PlacementResult.Ok)
            {
                placed++;
            }
        }

        return placed;
    }

    /// <summary>
    /// Posoudí jedno místo s ohledem na to, co už plán utratil. Bez téhle
    /// průběžné útraty by plán sliboval dvacet budov za peníze na jednu.
    /// </summary>
    private PlacementResult Evaluate(int defIndex, BuildingDef def, int x, int y)
    {
        var result = _simulation.CanPlace(defIndex, x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var cost = def.BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            int resource = cost[i].ResourceIndex;
            if (_simulation.GetResource(resource) - _spent[resource] < cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        for (int i = 0; i < cost.Count; i++)
        {
            _spent[cost[i].ResourceIndex] += cost[i].Amount;
        }

        return PlacementResult.Ok;
    }
}
