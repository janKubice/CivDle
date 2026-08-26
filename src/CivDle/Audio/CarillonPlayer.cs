using CivDle.Core.Sim;

namespace CivDle.Audio;

/// <summary>
/// Přehrávač melodie zvonohry: tón po tónu, v čase.
///
/// <para>Proč vlastní třída a ne osm volání za sebou: tóny musí zaznít
/// <b>s odstupem</b>, jinak z melodie vznikne akord. Odstup znamená čas, čas
/// znamená stav mezi snímky — a to je přesně tolik zodpovědnosti, kolik unese
/// jedna malá třída.</para>
///
/// <para>Vrstva: audio. Simulaci jen <b>čte</b> (melodii a počítadlo zvonění),
/// nikdy do ní nezapisuje.</para>
/// </summary>
public sealed class CarillonPlayer
{
    private readonly GameSounds _sounds;
    private readonly double _noteSeconds;

    /// <summary>Na kolikátém zvonění stálo počítadlo simulace naposledy.</summary>
    private long _lastRing;

    /// <summary>Který tón melodie je na řadě; −1 = nehraje se.</summary>
    private int _note = -1;

    private double _sinceNote;
    private int[] _tune = Array.Empty<int>();

    public CarillonPlayer(GameSounds sounds, double noteSeconds)
    {
        _sounds = sounds;
        _noteSeconds = Math.Max(0.05, noteSeconds);
    }

    /// <summary>Hraje se právě? (UI podle toho zvýrazňuje tón.)</summary>
    public bool IsPlaying => _note >= 0;

    /// <summary>Který tón zrovna zní; −1 = žádný.</summary>
    public int CurrentNote => _note;

    /// <summary>
    /// Spustí melodii od začátku. Kopíruje se schválně: hráč smí melodii
    /// přepsat i během přehrávání a rozehraná písnička se tím nemá zamotat.
    /// </summary>
    public void Play(IReadOnlyList<int> tune)
    {
        if (tune.Count == 0)
        {
            return;
        }

        if (_tune.Length != tune.Count)
        {
            _tune = new int[tune.Count];
        }

        for (int i = 0; i < tune.Count; i++)
        {
            _tune[i] = tune[i];
        }

        _note = 0;
        _sinceNote = _noteSeconds; // první tón hned, ne až za dobu jednoho
    }

    /// <summary>
    /// Posune přehrávání a případně spustí nové, když simulace ohlásila
    /// zvonění (slavnost).
    /// </summary>
    /// <param name="deltaSeconds">Reálný čas od minulého snímku.</param>
    /// <param name="simulation">Odkud se čte melodie a počítadlo zvonění.</param>
    public void Update(double deltaSeconds, Simulation simulation)
    {
        if (simulation.CarillonRings != _lastRing)
        {
            _lastRing = simulation.CarillonRings;
            Play(simulation.Carillon.Notes);
        }

        if (_note < 0)
        {
            return;
        }

        _sinceNote += deltaSeconds;
        while (_note >= 0 && _sinceNote >= _noteSeconds)
        {
            _sinceNote -= _noteSeconds;
            _sounds.PlayBell(_tune[_note]);
            if (++_note >= _tune.Length)
            {
                _note = -1;
            }
        }
    }

    /// <summary>
    /// Srovná se se simulací bez hraní — po načtení savu.
    ///
    /// <para>Bez toho by zvonohra po každém načtení hry uvítala hráče melodií
    /// za slavnost, která proběhla před dvěma hodinami.</para>
    /// </summary>
    public void Resync(Simulation simulation)
    {
        _lastRing = simulation.CarillonRings;
        _note = -1;
    }
}
