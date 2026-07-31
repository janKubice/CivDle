using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Co ředitel zrovna doporučuje ukázat.</summary>
public enum DirectorCue
{
    /// <summary>Nic — teď je hráč v pohodě, nech ho hrát.</summary>
    None,

    /// <summary>Vyskočí událost s volbami.</summary>
    Event,

    /// <summary>Tichý tip v rohu (toast), který nic nepřerušuje.</summary>
    Hint,
}

/// <summary>
/// Co ředitel vybral. Struktura, ne třída: skládá se často a je maličká.
/// </summary>
/// <param name="Cue">Druh doporučení.</param>
/// <param name="EventIndex">Index události v registru (jen u <see cref="DirectorCue.Event"/>).</param>
/// <param name="HintKey">Lokalizační klíč tipu (jen u <see cref="DirectorCue.Hint"/>).</param>
public readonly record struct DirectorDecision(DirectorCue Cue, int EventIndex = -1, string HintKey = "")
{
    /// <summary>Nedoporučuje nic.</summary>
    public static DirectorDecision Nothing { get; } = new(DirectorCue.None);
}

/// <summary>
/// Ředitel obsahu: rozhoduje, <b>co</b> hráči ukázat a <b>kdy</b>.
///
/// <para>Proč to vzniklo: hra měla přes třicet událostí a přes dvacet úkolů, ale
/// události chodily podle pevného časovače a bez ohledu na to, co se ve městě
/// zrovna děje. Obsah tam byl — jen nepůsobil, že reaguje na hráče. Tenhle
/// systém nepřidává ani řádek nového obsahu; jen ho vybírá podle situace.</para>
///
/// <para>Dvě vrstvy s jiným tempem:</para>
/// <list type="bullet">
/// <item><b>Tipy</b> chodí často, ale jen když je opravdu na co upozornit
/// (stojící výroba, plný sklad, smog nad městem, hotová zakázka). Nic
/// nepřerušují — jsou to toasty.</item>
/// <item><b>Události</b> zůstávají řídké, protože vyskakovací okno bere hráči
/// kontrolu. Nově se ale vybírá ta, jejíž odměna zrovna dává smysl: kupec přijde
/// s tím, čeho je ve městě málo.</item>
/// </list>
///
/// <para>Vrstva Core, ne UI: rozhodnutí je datové (<see cref="DirectorDecision"/>),
/// takže jde testovat bez grafiky. Zobrazit ho je věc obrazovky — simulace ani
/// ředitel o oknech nic nevědí (CLAUDE.md, vrstvy).</para>
/// </summary>
public sealed class ContentDirector
{
    /// <summary>Nejkratší rozestup mezi dvěma tipy — jinak by z nich byl spam.</summary>
    private const double HintGapSeconds = 50;

    /// <summary>Jak dlouho se stejný tip neopakuje. Tentýž problém dokola je otrava.</summary>
    private const double SameHintCooldownSeconds = 420;

    /// <summary>Nejkratší rozestup událostí. Okno bere kontrolu — má být vzácné.</summary>
    private const double EventMinGapSeconds = 420;

    /// <summary>Nejdelší rozestup událostí.</summary>
    private const double EventMaxGapSeconds = 780;

    /// <summary>Pod tímhle naplněním skladu se surovina bere jako nedostatková.</summary>
    private const double ScarceBelow = 0.2;

    /// <summary>Nad tímhle naplněním se sklad hlásí jako plný.</summary>
    private const double FullAbove = 0.995;

    private readonly GameContent _content;
    private readonly Random _rng;
    private readonly Dictionary<string, double> _hintSeenAt = new(StringComparer.Ordinal);

    private double _elapsed;
    private double _nextHintAt = HintGapSeconds;
    private double _nextEventAt;

    public ContentDirector(GameContent content, long seed)
    {
        _content = content;
        _rng = new Random((int)(seed ^ 0x0D1EEC70));
        _nextEventAt = EventMinGapSeconds;
    }

    /// <summary>
    /// Posune hodiny a řekne, jestli je zrovna něco na řadě. Volá se každý snímek
    /// s uplynulým časem; sama o sobě nic nemění na simulaci.
    /// </summary>
    public DirectorDecision Advance(Simulation sim, double deltaSeconds)
    {
        _elapsed += deltaSeconds;

        // Událost má přednost před tipem: když už se má vyskočit okno, ať se
        // nestane, že těsně předtím vyjede toast a hráč přehlédne obojí.
        if (_elapsed >= _nextEventAt && PickRelevantEvent(sim) is var index && index >= 0)
        {
            _nextEventAt = _elapsed + EventMinGapSeconds
                + _rng.NextDouble() * (EventMaxGapSeconds - EventMinGapSeconds);
            _nextHintAt = Math.Max(_nextHintAt, _elapsed + HintGapSeconds);
            return new DirectorDecision(DirectorCue.Event, index);
        }

        if (_elapsed < _nextHintAt)
        {
            return DirectorDecision.Nothing;
        }

        if (FindHint(sim) is not { } hint)
        {
            // Není na co upozorňovat — to je dobrá zpráva. Zkusíme to zas za chvíli.
            _nextHintAt = _elapsed + HintGapSeconds;
            return DirectorDecision.Nothing;
        }

        _hintSeenAt[hint] = _elapsed;
        _nextHintAt = _elapsed + HintGapSeconds;
        return new DirectorDecision(DirectorCue.Hint, HintKey: hint);
    }

    /// <summary>
    /// Najde nejpalčivější věc, na kterou se vyplatí upozornit — nebo <c>null</c>,
    /// když je všechno v pořádku.
    ///
    /// <para>Pořadí je pořadím naléhavosti: hotová zakázka je odměna čekající na
    /// vyzvednutí, stojící výroba je problém, který sám nezmizí, a smog s
    /// nespokojeností jsou pomalé jevy, co snesou počkat.</para>
    /// </summary>
    private string? FindHint(Simulation sim)
    {
        if (HasDeliverableContract(sim) && IsFresh("hint.contractReady"))
        {
            return "hint.contractReady";
        }

        if (sim.IdleBuildings > 0 && IsFresh("hint.idleBuildings"))
        {
            return "hint.idleBuildings";
        }

        if (FullStorageResource(sim) >= 0 && IsFresh("hint.storageFull"))
        {
            return "hint.storageFull";
        }

        if (sim.AirPollutionSeverity > 0.45 && IsFresh("hint.smog"))
        {
            return "hint.smog";
        }

        if (_content.Gameplay.Happiness.IsEnabled && sim.Happiness < 0.4 && IsFresh("hint.unhappy"))
        {
            return "hint.unhappy";
        }

        return null;
    }

    /// <summary>Neukazoval se tenhle tip nedávno?</summary>
    private bool IsFresh(string key) =>
        !_hintSeenAt.TryGetValue(key, out double seenAt) || _elapsed - seenAt >= SameHintCooldownSeconds;

    private static bool HasDeliverableContract(Simulation sim)
    {
        for (int i = 0; i < sim.ContractSlots.Length; i++)
        {
            if (sim.CanFulfilContract(i))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Index suroviny, jejíž sklad přetéká (−1 = žádná).</summary>
    private static int FullStorageResource(Simulation sim)
    {
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            if (!sim.IsResourceKnown(i))
            {
                continue;
            }

            double cap = sim.GetStorageCap(i);
            if (cap > 0 && sim.GetResource(i) >= cap * FullAbove)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Vybere událost, která do situace zapadá: přednost má ta, jejíž odměna
    /// obsahuje surovinu, které je ve městě zrovna málo.
    ///
    /// <para>Tohle je celý smysl ředitele. Kupec, co přinese dřevo zrovna když
    /// dřevo došlo, působí, že hra hráče sleduje — a přitom je to tentýž kupec
    /// z týchž dat, jaký tam byl vždycky.</para>
    /// </summary>
    private int PickRelevantEvent(Simulation sim)
    {
        var events = _content.Events;
        int best = -1;
        int bestScore = int.MinValue;
        int seenWithBest = 0;

        for (int i = 0; i < events.Count; i++)
        {
            var candidate = events[i];
            if (candidate.Requirement is { } requirement
                && sim.EvaluateMetric(requirement.Kind, requirement.Param) < requirement.Target)
            {
                continue;
            }

            int score = ScarcityScore(sim, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = i;
                seenWithBest = 1;
                continue;
            }

            // Shoda skóre: náhodně mezi rovnocennými, ať se nenabízí pořád totéž.
            if (score == bestScore)
            {
                seenWithBest++;
                if (_rng.Next(seenWithBest) == 0)
                {
                    best = i;
                }
            }
        }

        return best;
    }

    /// <summary>Kolik nedostatkových surovin událost nabízí (víc = padne líp do situace).</summary>
    private static int ScarcityScore(Simulation sim, EventDef candidate)
    {
        int score = 0;
        for (int c = 0; c < candidate.Choices.Count; c++)
        {
            var gain = candidate.Choices[c].Gain;
            for (int g = 0; g < gain.Count; g++)
            {
                int index = gain[g].ResourceIndex;
                double cap = sim.GetStorageCap(index);
                if (cap > 0 && sim.GetResource(index) < cap * ScarceBelow)
                {
                    score++;
                }
            }
        }

        return score;
    }
}
