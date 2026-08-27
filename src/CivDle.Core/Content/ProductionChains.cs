namespace CivDle.Core.Content;

/// <summary>
/// Jedna budova ve vztahu k surovině: kdo ji vyrábí nebo spotřebovává.
/// </summary>
/// <param name="BuildingIndex">Která budova.</param>
/// <param name="Amount">Kolik jí za jeden cyklus projde.</param>
/// <param name="TimeTicks">Jak dlouho ten cyklus trvá.</param>
public readonly record struct ChainStep(int BuildingIndex, int Amount, int TimeTicks)
{
    /// <summary>Kolik suroviny to udělá (nebo spotřebuje) za sekundu.</summary>
    public double PerSecond => TimeTicks <= 0 ? 0 : Amount / (TimeTicks / (double)Sim.Simulation.TicksPerSecond);
}

/// <summary>
/// Odkud se která surovina bere a kam jde.
///
/// <para>Proč to hra potřebuje: řetězců je ve hře přes stovku budov a hráč,
/// kterému dojdou prkna, nemá jak zjistit, <b>co postavit</b>. Musel by
/// procházet stavební menu budovu po budově a číst recepty. Tohle je otočený
/// pohled: začni u suroviny, kterou chceš, a hra ti řekne kdo ji dělá a z čeho.
/// </para>
///
/// <para>Spočítá se <b>jednou</b> nad obsahem — je to odvozený pohled na data,
/// ne stav hry, takže se nikam neukládá a se simulací nemá co do činění.</para>
/// </summary>
public sealed class ProductionChains
{
    private readonly List<ChainStep>[] _producers;
    private readonly List<ChainStep>[] _consumers;
    private readonly List<int>[] _terrainSources;

    public ProductionChains(GameContent content)
    {
        int count = content.Resources.Count;
        _producers = new List<ChainStep>[count];
        _consumers = new List<ChainStep>[count];
        _terrainSources = new List<int>[count];
        for (int i = 0; i < count; i++)
        {
            _producers[i] = new List<ChainStep>();
            _consumers[i] = new List<ChainStep>();
            _terrainSources[i] = new List<int>();
        }

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var def = content.Buildings[i];
            if (def.Recipe is not { } recipe)
            {
                continue;
            }

            foreach (var output in recipe.Outputs)
            {
                _producers[output.ResourceIndex].Add(new ChainStep(i, output.Amount, recipe.TimeTicks));
            }

            foreach (var input in recipe.Inputs)
            {
                _consumers[input.ResourceIndex].Add(new ChainStep(i, input.Amount, recipe.TimeTicks));
            }
        }
    }

    /// <summary>Kdo tuhle surovinu vyrábí.</summary>
    public IReadOnlyList<ChainStep> ProducersOf(int resourceIndex) => _producers[resourceIndex];

    /// <summary>Kdo tuhle surovinu spotřebovává.</summary>
    public IReadOnlyList<ChainStep> ConsumersOf(int resourceIndex) => _consumers[resourceIndex];

    /// <summary>
    /// Suroviny, které <b>nikdo nevyrábí</b> — berou se z terénu, nebo jen
    /// z odměn.
    ///
    /// <para>Je to užitečná odpověď sama o sobě: „nikdo ji nedělá" znamená
    /// „hledej ji na mapě", ne „ještě jsi neodemkl tu správnou budovu".</para>
    /// </summary>
    public bool IsRaw(int resourceIndex) => _producers[resourceIndex].Count == 0;

    /// <summary>
    /// Z čeho se surovina vyrábí — sjednocení vstupů všech jejích výrobců.
    ///
    /// <para>Sjednocení, ne seznam podle budov: hráč se ptá „co k tomu
    /// potřebuju sehnat", a tam je jedno, která z pil to zpracuje.</para>
    /// </summary>
    public IReadOnlyList<int> IngredientsOf(int resourceIndex, GameContent content)
    {
        var result = new List<int>();
        foreach (var step in _producers[resourceIndex])
        {
            if (content.Buildings[step.BuildingIndex].Recipe is not { } recipe)
            {
                continue;
            }

            foreach (var input in recipe.Inputs)
            {
                if (!result.Contains(input.ResourceIndex))
                {
                    result.Add(input.ResourceIndex);
                }
            }
        }

        return result;
    }
}
