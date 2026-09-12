namespace CivDle.Rendering.Effects;

/// <summary>
/// Kudy se chodí — a proto kde se zem ošlape na hlínu.
///
/// <para><b>K čemu to je:</b> chodci jsou kulisa, která zmizí, jakmile se hráč
/// podívá jinam. Vyšlapaná cesta je stopa, kterou po sobě nechají: i prázdné
/// náměstí pak vypadá jako místo, kudy někdo chodí. Je to nejlevnější způsob,
/// jak dát chování <i>paměť</i>.</para>
///
/// <para><b>Kde to žije:</b> v renderu, ne v simulaci. Šlápoty nic neovlivňují
/// a neukládají se — kdyby je viděla simulace, byl by z vizuální kulisy herní
/// stav, který se musí ukládat a nesmí se rozejít se savem.</para>
///
/// <para><b>Proč slábne:</b> bez zarůstání by se po hodině hraní ošlapala celá
/// mapa a z cest by zase byla jednolitá plocha — jen hnědá místo zelené.</para>
/// </summary>
public sealed class FootfallMap
{
    /// <summary>O kolik ztmavne dlaždice jedním došlapem.</summary>
    private const float StepGain = 0.035f;

    /// <summary>Jak často se stezky přepočítávají (sekundy). Nízká frekvence, viz CLAUDE.md.</summary>
    private const float DecayIntervalSeconds = 2f;

    /// <summary>Kolik z ošlapání zůstane po jednom přepočtu.</summary>
    private const float DecayKeep = 0.94f;

    /// <summary>Pod touhle mírou už stezka není vidět a zahodí se.</summary>
    private const float Forget = 0.02f;

    /// <summary>
    /// Strop počtu zapamatovaných dlaždic. Svět je nekonečný, paměť ne —
    /// a stezky se stejně kreslí jen u kamery.
    /// </summary>
    private const int MaxCells = 4096;

    private readonly Dictionary<long, float> _wear = new();
    private float _sinceDecay;

    /// <summary>Kolik dlaždic je zrovna ošlapaných.</summary>
    public int Count => _wear.Count;

    /// <summary>Někdo tudy šel.</summary>
    public void Step(int tileX, int tileY)
    {
        long key = Key(tileX, tileY);
        _wear.TryGetValue(key, out float wear);
        _wear[key] = MathF.Min(1f, wear + StepGain);
    }

    /// <summary>Jak je dlaždice ošlapaná (0 = netknutá, 1 = vyšlapaná na hlínu).</summary>
    public float WearAt(int tileX, int tileY) =>
        _wear.TryGetValue(Key(tileX, tileY), out float wear) ? wear : 0f;

    /// <summary>
    /// Nechá stezky zarůst. Volá se každý snímek, ale pracuje jen občas —
    /// procházet slovník šedesátkrát za vteřinu by nic nepřidalo.
    /// </summary>
    public void Update(float dt)
    {
        _sinceDecay += dt;
        if (_sinceDecay < DecayIntervalSeconds)
        {
            return;
        }

        _sinceDecay = 0f;
        Fade();

        if (_wear.Count > MaxCells)
        {
            DropFaintest();
        }
    }

    /// <summary>Zapomene všechno (nová hra, načtení savu).</summary>
    public void Clear()
    {
        _wear.Clear();
        _sinceDecay = 0f;
    }

    private void Fade()
    {
        // Kopie klíčů by se alokovala každé dvě vteřiny; tenhle seznam se
        // půjčuje pořád dokola.
        _expired.Clear();

        foreach (var pair in _wear)
        {
            float faded = pair.Value * DecayKeep;
            if (faded < Forget)
            {
                _expired.Add(pair.Key);
            }
            else
            {
                _pending.Add((pair.Key, faded));
            }
        }

        foreach (var (key, value) in _pending)
        {
            _wear[key] = value;
        }

        foreach (long key in _expired)
        {
            _wear.Remove(key);
        }

        _pending.Clear();
    }

    /// <summary>Když se paměť přeplní, jdou pryč ty nejslabší stopy.</summary>
    private void DropFaintest()
    {
        float threshold = 0f;
        for (int pass = 0; pass < 8 && _wear.Count > MaxCells; pass++)
        {
            threshold += 0.1f;
            _expired.Clear();

            foreach (var pair in _wear)
            {
                if (pair.Value <= threshold)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (long key in _expired)
            {
                _wear.Remove(key);
            }
        }
    }

    private readonly List<long> _expired = new();
    private readonly List<(long Key, float Value)> _pending = new();

    /// <summary>Dlaždice do jednoho klíče. Svět je nekonečný na obě strany, proto posun.</summary>
    private static long Key(int tileX, int tileY) =>
        ((long)(uint)tileX << 32) | (uint)tileY;
}
