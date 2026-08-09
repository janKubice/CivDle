using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Budovy, které přetvářejí krajinu samy.
///
/// <para>Proč to ve hře je: ruční teraformace je až u vyspělé civilizace, ale
/// suchou půdu uprostřed pouště nebo pevnou zem místo bažiny chce hráč dávno
/// předtím. Zavlažovací dílo a spol. jsou ta dřívější, pomalejší cesta —
/// zaberou místo i dělníky, ale okolí mění bez jediného kliknutí.</para>
///
/// <para>Běží na <b>nízké frekvenci</b> a mění <b>jednu dlaždici za kolo</b>
/// (CLAUDE.md, výkon): přetvořit celý okruh naráz by byl skok, ne růst, a u
/// stovek stanic by to znamenalo projít desetitisíce dlaždic v jednom tiku.
/// Je to schválně pomalejší než ruční zásah — automat má šetřit klikání,
/// ne nahradit hráče.</para>
///
/// <para>Odemčení zásahu se systém <b>neptá</b>: budova sama je to odemčení.
/// Výzkum na ni by jinak neměl smysl — hráč by ji postavil a ona by čekala na
/// technologii, kvůli které ji vůbec stavěl.</para>
/// </summary>
internal sealed class AutoTerraformSystem
{
    /// <summary>Jak často se přetváří (tiky). Pomalý systém, ne hot path.</summary>
    private const int IntervalTicks = 60;

    private readonly GameContent _content;

    /// <summary>Kam došlo hledání u které budovy — ať se nezačíná pokaždé od kraje.</summary>
    private int[] _cursor = Array.Empty<int>();

    public AutoTerraformSystem(GameContent content) => _content = content;

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
            if (!def.Terraforms || !building.IsComplete)
            {
                continue;
            }

            Reshape(sim, ref _cursor[i], def.TerraformActionIndex, building.X, building.Y, def.TerraformRadius);
        }
    }

    /// <summary>
    /// Přetvoří první vhodnou dlaždici, na kterou kurzor narazí. Kurzor jde
    /// dokola po čtverci kolem budovy, takže se okolí mění rovnoměrně a hledání
    /// stojí pár dlaždic za kolo, ne celý okruh.
    /// </summary>
    private static void Reshape(
        Simulation sim, ref int cursor, int actionIndex, int centerX, int centerY, int radius)
    {
        int side = radius * 2 + 1;
        int tiles = side * side;

        for (int step = 0; step < tiles; step++)
        {
            int index = (cursor + step) % tiles;
            int x = centerX - radius + index % side;
            int y = centerY - radius + index / side;

            if (sim.TryAutoTerraform(actionIndex, x, y) == PlacementResult.Ok)
            {
                cursor = index + 1;
                return;
            }
        }

        cursor = 0; // v okolí není co měnit — příště se začne znovu od kraje
    }
}
