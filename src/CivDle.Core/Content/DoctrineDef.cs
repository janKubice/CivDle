namespace CivDle.Core.Content;

/// <summary>
/// Jeden uzel doktríny — co se za body Vzestupu dá koupit.
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>doctrine.&lt;doktrína&gt;.&lt;uzel&gt;</c>).</param>
/// <param name="Effect">Behavior-ID bonusu — tentýž slovník jako Vzestup.</param>
/// <param name="Magnitude">Síla bonusu.</param>
/// <param name="Cost">Cena v bodech Vzestupu.</param>
/// <param name="PrerequisiteIndices">Uzly, které musí být koupené dřív (indexy v rámci téže doktríny).</param>
public sealed record DoctrineNodeDef(
    string Id,
    string Effect,
    double Magnitude,
    int Cost,
    IReadOnlyList<int> PrerequisiteIndices);

/// <summary>
/// Civilizační doktrína: cesta, kterou se tahle civilizace vydala.
///
/// <para>Proč to ve hře je: všechna ostatní vylepšení jdou nakoupit všechna,
/// jen po pořádku. Doktrína je první rozhodnutí, které <b>něco vylučuje</b> —
/// dvě civilizace se stejným počtem bodů můžou vypadat jinak.</para>
///
/// <para><b>Aktivní je vždycky jen jedna.</b> To není omezení implementace, to
/// je celá mechanika: kdyby se daly sbírat všechny, nebyla by to volba, ale
/// další seznam k odškrtání.</para>
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>doctrine.&lt;id&gt;</c>).</param>
/// <param name="Nodes">Uzly v pořadí z dat.</param>
public sealed record DoctrineDef(string Id, IReadOnlyList<DoctrineNodeDef> Nodes)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"doctrine.{Id}";

    /// <summary>Lokalizační klíč popisu.</summary>
    public string DescriptionKey => $"doctrine.{Id}.desc";

    /// <summary>Lokalizační klíč jména uzlu.</summary>
    public string NodeNameKey(int nodeIndex) => $"doctrine.{Id}.{Nodes[nodeIndex].Id}";

    /// <summary>Celková cena všech uzlů — podklad pro balanční test i pro UI.</summary>
    public int TotalCost
    {
        get
        {
            int total = 0;
            for (int i = 0; i < Nodes.Count; i++)
            {
                total += Nodes[i].Cost;
            }

            return total;
        }
    }

    /// <summary>
    /// Součet síly všech uzlů. Není to „kolik doktrína dá" do puntíku (efekty
    /// míří na různá čísla), ale je to jediné srovnatelné měřítko — a stačí
    /// na to, aby se poznala doktrína, která je proti ostatním dvakrát silnější.
    /// </summary>
    public double TotalMagnitude
    {
        get
        {
            double total = 0;
            for (int i = 0; i < Nodes.Count; i++)
            {
                total += Nodes[i].Magnitude;
            }

            return total;
        }
    }
}

/// <summary>Doktríny z <c>data/doctrines.json</c>. Prázdné = mechanika vypnutá.</summary>
/// <param name="Doctrines">Doktríny v pořadí z dat.</param>
public sealed record DoctrineCatalog(IReadOnlyList<DoctrineDef> Doctrines)
{
    /// <summary>Hra bez doktrín.</summary>
    public static DoctrineCatalog Empty { get; } = new(Array.Empty<DoctrineDef>());

    /// <summary>Dá se doktrína vůbec vybrat?</summary>
    public bool IsEnabled => Doctrines.Count > 0;

    public int Count => Doctrines.Count;

    public DoctrineDef this[int index] => Doctrines[index];

    /// <summary>Index podle ID, nebo −1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Doctrines.Count; i++)
        {
            if (string.Equals(Doctrines[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
