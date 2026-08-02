using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Reforestace: budovy, které krajinu vracejí zpátky.
///
/// <para>Proč to ve hře je: pily a lomy vytěží okolí rychleji, než samo doroste.
/// Hráč se tak dostane do stavu, se kterým nemůže nic dělat — kolem stojí holina
/// a jediná odpověď je stěhovat výrobu pořád dál. Lesní školka je ta druhá páka:
/// stojí místo, dělníky i provoz, ale les vrací, takže se dá zůstat.</para>
///
/// <para>Běží na <b>nízké frekvenci</b> a vrací <b>jednu dlaždici za kolo</b>
/// (CLAUDE.md, výkon): obnovit celý okruh naráz by byl skok, ne růst, a u stovek
/// školek by to znamenalo procházet desetitisíce dlaždic v jednom tiku.</para>
/// </summary>
internal sealed class ReforestSystem
{
    /// <summary>Jak často se sází (tiky). Pomalý systém, ne hot path.</summary>
    private const int IntervalTicks = 40;

    private readonly GameContent _content;

    /// <summary>Kam došlo hledání u které budovy — ať se nezačíná pokaždé od kraje.</summary>
    private int[] _cursor = Array.Empty<int>();

    public ReforestSystem(GameContent content) => _content = content;

    public void Tick(Simulation sim)
    {
        if (sim.TickCount % IntervalTicks != 0)
        {
            return;
        }

        var buildings = sim.BuildingsMutable;
        if (_cursor.Length < buildings.Length)
        {
            Array.Resize(ref _cursor, Math.Max(buildings.Length, _cursor.Length * 2 + 16));
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];
            if (!def.Reforests || !building.IsComplete)
            {
                continue;
            }

            Replant(sim, ref _cursor[i], building.X, building.Y, def.ReforestRadius);
        }
    }

    /// <summary>
    /// Obnoví první vytěženou dlaždici, na kterou kurzor narazí. Kurzor jde
    /// dokola po čtverci kolem budovy, takže se les vrací rovnoměrně a hledání
    /// stojí pár dlaždic za kolo, ne celý okruh.
    /// </summary>
    private static void Replant(Simulation sim, ref int cursor, int centerX, int centerY, int radius)
    {
        int side = radius * 2 + 1;
        int tiles = side * side;

        for (int step = 0; step < tiles; step++)
        {
            int index = (cursor + step) % tiles;
            int x = centerX - radius + index % side;
            int y = centerY - radius + index / side;

            if (sim.TryRestoreTerrain(x, y))
            {
                cursor = index + 1;
                return;
            }
        }

        cursor = 0; // v okolí není co obnovovat — příště se začne znovu od kraje
    }
}
