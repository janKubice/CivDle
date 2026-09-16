namespace CivDle.Core.Content;

/// <summary>
/// Kolik z hry ukázka obsahuje — a kolik jí zbývá.
///
/// <para><b>Proč se to počítá, a nepíše ručně:</b> je to text, který hráč vidí
/// na konci ukázky jako důvod ke koupi. Ručně zapsaná čísla by se rozešla
/// s obsahem při první přidané budově a z pozvánky by byla nepravda — což je
/// to poslední, co si od dema má člověk odnést.</para>
///
/// <para>Vrstva: čistá funkce nad obsahem. Žádná simulace, takže se dá ověřit
/// bez rozehrané hry.</para>
/// </summary>
/// <param name="Techs">Kolik technologií je v ukázce dostupných.</param>
/// <param name="TotalTechs">Kolik jich má celá hra.</param>
/// <param name="Buildings">Kolik budov se dá v ukázce postavit.</param>
/// <param name="TotalBuildings">Kolik se jich dá postavit v celé hře.</param>
public readonly record struct DemoScope(int Techs, int TotalTechs, int Buildings, int TotalBuildings)
{
    /// <summary>Kolik technologií ukázka nenabídne.</summary>
    public int TechsBeyond => Math.Max(0, TotalTechs - Techs);

    /// <summary>Kolik budov ukázka nenabídne.</summary>
    public int BuildingsBeyond => Math.Max(0, TotalBuildings - Buildings);

    /// <summary>
    /// Spočítá rozsah ukázky z obsahu.
    ///
    /// <para>Budova je v ukázce dostupná, když ji neodemyká žádná technologie
    /// (staví se od začátku), nebo ji odemyká aspoň jedna z těch, které ukázka
    /// nabízí.</para>
    /// </summary>
    public static DemoScope Measure(GameContent content)
    {
        var allowed = content.Demo.HasCuratedTechs
            ? DemoTechSelection.BuildFrom(content.Techs.All, content.Demo.TechIds)
            : DemoTechSelection.Build(
                content.Techs.All, content.Demo.TechCountFor(content.Techs.Count));

        int techs = 0;
        var unlockedByAnyTech = new HashSet<int>();
        var unlockedInDemo = new HashSet<int>();

        for (int i = 0; i < content.Techs.Count; i++)
        {
            bool open = allowed[i];
            if (open)
            {
                techs++;
            }

            foreach (int building in content.Techs[i].UnlockedBuildingIndices)
            {
                unlockedByAnyTech.Add(building);
                if (open)
                {
                    unlockedInDemo.Add(building);
                }
            }
        }

        int buildings = 0;
        int total = 0;
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (!content.Buildings[i].Buildable)
            {
                continue;
            }

            total++;
            if (!unlockedByAnyTech.Contains(i) || unlockedInDemo.Contains(i))
            {
                buildings++;
            }
        }

        return new DemoScope(techs, content.Techs.Count, buildings, total);
    }
}
