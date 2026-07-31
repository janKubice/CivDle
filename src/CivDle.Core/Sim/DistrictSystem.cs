using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Jedna rozpoznaná čtvrť: druh, obálka na mapě a kolik budov ji tvoří.
/// Odvozený stav — neukládá se, přepočítá se ze zástavby jako osady.
/// </summary>
/// <param name="TypeIndex">Druh čtvrti v registru.</param>
/// <param name="MinX">Levý okraj obálky v dlaždicích.</param>
/// <param name="MinY">Horní okraj obálky v dlaždicích.</param>
/// <param name="MaxX">Pravý okraj obálky (včetně).</param>
/// <param name="MaxY">Dolní okraj obálky (včetně).</param>
/// <param name="BuildingCount">Kolik budov ve čtvrti stojí.</param>
public readonly record struct District(
    int TypeIndex, int MinX, int MinY, int MaxX, int MaxY, int BuildingCount)
{
    /// <summary>Střed čtvrti — sem patří cedule se jménem.</summary>
    public float CenterX => (MinX + MaxX + 1) * 0.5f;

    /// <summary>Střed čtvrti — sem patří cedule se jménem.</summary>
    public float CenterY => (MinY + MaxY + 1) * 0.5f;
}

/// <summary>
/// Detekce čtvrtí (living-city.md §5): shluk budov stejného ražení dostane
/// jméno, tvář na mapě a synergii — spolu se stinnou stránkou.
///
/// <para>Proč to ve hře je: shlukovat stejné budovy bylo do teď kosmetika. Teď
/// je to rozhodnutí — pět továren vedle sebe vyrábí líp, ale i víc dýmá, takže
/// si průmyslová čtvrť sama řekne o park nebo čističku. A hráč se při oddálení
/// dívá na místa se jménem, ne na anonymní kaši budov.</para>
///
/// <para>Union-find jako u osad, jen po druzích: každý druh čtvrti si hledá
/// vlastní shluky mezi budovami svých kategorií. Běží na nízké frekvenci a jen
/// po změně zástavby (CLAUDE.md, výkon).</para>
///
/// <para>Výsledek si každá budova nese nacachovaný
/// (<see cref="BuildingInstance.DistrictMult"/> pro výrobu,
/// <see cref="BuildingInstance.DistrictIndex"/> pro znečištění a UI), takže se
/// v tikové smyčce nic nedohledává.</para>
/// </summary>
internal sealed class DistrictSystem
{
    /// <summary>Jak často se čtvrti přepočítávají (pomalý systém, ne hot path).</summary>
    private const int IntervalTicks = 60;

    private readonly GameContent _content;

    /// <summary>Kategorie budovy podle indexu definice — ať se v cyklu nesahá na registr.</summary>
    private readonly string[] _categories;

    private int[] _parent = Array.Empty<int>();
    private int[] _members = Array.Empty<int>();

    public DistrictSystem(GameContent content)
    {
        _content = content;
        var defs = content.Buildings.All;
        _categories = new string[defs.Count];
        for (int i = 0; i < defs.Count; i++)
        {
            _categories[i] = defs[i].Category;
        }
    }

    public void Tick(Simulation sim)
    {
        if (!_content.Districts.IsEnabled || sim.TickCount % IntervalTicks != 0 || !sim.DistrictsDirty)
        {
            return;
        }

        sim.DistrictsDirty = false;
        Recompute(sim);
    }

    private void Recompute(Simulation sim)
    {
        var districts = sim.DistrictsMutable;
        districts.Clear();

        var buildings = sim.BuildingsMutable;
        if (buildings.Length == 0)
        {
            return;
        }

        if (_parent.Length < buildings.Length)
        {
            _parent = new int[Math.Max(buildings.Length, 64)];
            _members = new int[Math.Max(buildings.Length, 64)];
        }

        // Čistý štít: budova, která z čtvrti vypadla (zbourala se sousedka), musí
        // přijít i o bonus. Bez tohohle by po ní zůstal navždy.
        for (int i = 0; i < buildings.Length; i++)
        {
            buildings[i].DistrictIndex = -1;
            buildings[i].DistrictMult = 1f;
        }

        var types = _content.Districts.Types;
        for (int typeIndex = 0; typeIndex < types.Count; typeIndex++)
        {
            FindClusters(sim, buildings, typeIndex, types[typeIndex]);
        }
    }

    /// <summary>Najde shluky jednoho druhu čtvrti a zapíše je i s bonusy do budov.</summary>
    private void FindClusters(
        Simulation sim, Span<BuildingInstance> buildings, int typeIndex, DistrictTypeDef type)
    {
        // Kandidáti: jen budovy kategorií, které tenhle druh čtvrti bere.
        int count = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (type.Accepts(_categories[buildings[i].DefIndex]))
            {
                _members[count++] = i;
            }
        }

        if (count < type.MinBuildings)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            _parent[i] = i;
        }

        // O(n²) nad kandidáty jednoho druhu. Běží zřídka a budov jsou stovky;
        // stejná úvaha jako u osad.
        for (int i = 0; i < count; i++)
        {
            for (int j = i + 1; j < count; j++)
            {
                if (FootprintGap(buildings[_members[i]], buildings[_members[j]]) <= type.ClusterDistance)
                {
                    Union(i, j);
                }
            }
        }

        var districts = sim.DistrictsMutable;
        for (int root = 0; root < count; root++)
        {
            if (Find(root) != root)
            {
                continue;
            }

            int size = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (Find(i) != root)
                {
                    continue;
                }

                ref var building = ref buildings[_members[i]];
                var def = _content.Buildings[building.DefIndex];
                size++;
                minX = Math.Min(minX, building.X);
                minY = Math.Min(minY, building.Y);
                maxX = Math.Max(maxX, building.X + def.FootprintWidth - 1);
                maxY = Math.Max(maxY, building.Y + def.FootprintHeight - 1);
            }

            if (size < type.MinBuildings)
            {
                continue;
            }

            int districtIndex = districts.Count;
            districts.Add(new District(typeIndex, minX, minY, maxX, maxY, size));

            float synergy = (float)type.SynergyFor(size);
            for (int i = 0; i < count; i++)
            {
                if (Find(i) == root)
                {
                    ref var building = ref buildings[_members[i]];
                    building.DistrictIndex = districtIndex;
                    building.DistrictMult = synergy;
                }
            }
        }
    }

    /// <summary>Čebyševova mezera mezi půdorysy (0 = dotýkají se nebo překrývají).</summary>
    private int FootprintGap(in BuildingInstance a, in BuildingInstance b)
    {
        var defA = _content.Buildings[a.DefIndex];
        var defB = _content.Buildings[b.DefIndex];

        int gapX = Math.Max(0, Math.Max(a.X - (b.X + defB.FootprintWidth - 1), b.X - (a.X + defA.FootprintWidth - 1)) - 1);
        int gapY = Math.Max(0, Math.Max(a.Y - (b.Y + defB.FootprintHeight - 1), b.Y - (a.Y + defA.FootprintHeight - 1)) - 1);
        return Math.Max(gapX, gapY);
    }

    private int Find(int i)
    {
        while (_parent[i] != i)
        {
            _parent[i] = _parent[_parent[i]];
            i = _parent[i];
        }

        return i;
    }

    private void Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA == rootB)
        {
            return;
        }

        // Nižší kořen vyhrává → stabilní reprezentant (nejstarší budova shluku).
        if (rootA < rootB)
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootA] = rootB;
        }
    }
}
