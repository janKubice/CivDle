using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Svoz zboží: budova daleko od skladu vyrábí míň, protože se z ní hůř odváží.
///
/// <para>Proč to v hře je: bez tohohle pravidla je jedno, jestli je důl kousek za
/// městem, nebo dvě stě dlaždic v horách — zboží se teleportuje. Se svozem má
/// sklad poprvé jiný smysl než „větší číslo kapacity": je to bod, kolem kterého
/// se dá stavět. Trest je měkký a má podlahu (<see cref="HaulConfig.MinMultiplier"/>) —
/// vzdálená kolonie zpomalí, nikdy neumře.</para>
///
/// <para>Výkon: násobič se cachuje na instanci
/// (<see cref="BuildingInstance.HaulMult"/>), v tikové smyčce se nepočítá. Přepočet
/// celého města se rozloží do slicu na tik — svoz je pomalá veličina a nikomu
/// nevadí, že se po postavení skladu srovná během pár sekund.</para>
/// </summary>
internal sealed class HaulSystem
{
    /// <summary>Kolik budov se přepočítá za tik. Rozložení místo jednoho záseku.</summary>
    private const int BuildingsPerTick = 2048;

    private readonly GameContent _content;

    /// <summary>
    /// Pozice sběrných míst (sklady + těžiště města). Pozice, ne indexy budov —
    /// bourání a slučování indexy přehazuje, souřadnice ne.
    /// </summary>
    private readonly List<(int X, int Y)> _stores = new();

    /// <summary>Kam došel rozložený přepočet. Rovná se počtu budov = hotovo.</summary>
    private int _cursor;

    public HaulSystem(GameContent content) => _content = content;

    public void Tick(Simulation sim)
    {
        if (!_content.Gameplay.Haul.IsEnabled)
        {
            return;
        }

        if (sim.HaulDirty)
        {
            CollectStores(sim);
            sim.HaulDirty = false;
            _cursor = 0;
        }

        var buildings = sim.BuildingsMutable;
        if (_cursor >= buildings.Length)
        {
            return;
        }

        int end = Math.Min(buildings.Length, _cursor + BuildingsPerTick);
        for (int i = _cursor; i < end; i++)
        {
            ref var building = ref buildings[i];
            building.HaulMult = (float)MultiplierAt(building.X, building.Y);
        }

        _cursor = end;
    }

    /// <summary>
    /// Násobič výroby pro budovu na daném místě. Veřejné uvnitř simulace, aby si
    /// nově položená budova nemusela počkat na rozložený přepočet — hráč má vidět
    /// správné číslo hned, ne za deset sekund.
    /// </summary>
    public double MultiplierAt(int x, int y)
    {
        var haul = _content.Gameplay.Haul;
        if (!haul.IsEnabled || _stores.Count == 0)
        {
            return 1.0;
        }

        int best = int.MaxValue;
        for (int i = 0; i < _stores.Count; i++)
        {
            // Manhattan, ne vzdušná čára: zboží jede po mřížce, ne přes pole.
            int distance = Math.Abs(_stores[i].X - x) + Math.Abs(_stores[i].Y - y);
            if (distance < best)
            {
                best = distance;
                if (best <= haul.FreeDistance)
                {
                    break; // blíž než zadarmo už to nebude
                }
            }
        }

        return haul.Multiplier(best);
    }

    /// <summary>
    /// Posbírá sběrná místa: každou budovu se skladovacím bonusem plus těžiště
    /// města. Těžiště je tam schválně — jinak by hra od první minuty trestala
    /// hráče za to, že ještě nemá sklad, který se odemyká mnohem později.
    /// </summary>
    private void CollectStores(Simulation sim)
    {
        _stores.Clear();
        _stores.Add((sim.CityCenterX, sim.CityCenterY));

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].StorageBonus.Count > 0)
            {
                _stores.Add((buildings[i].X, buildings[i].Y));
            }
        }
    }
}
