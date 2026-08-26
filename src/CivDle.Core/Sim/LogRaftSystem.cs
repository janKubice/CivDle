using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>Kláda plující po řece.</summary>
/// <param name="X">Dlaždice vodorovně.</param>
/// <param name="Y">Dlaždice svisle.</param>
/// <param name="ResourceIndex">Co veze.</param>
/// <param name="Amount">Kolik toho veze.</param>
/// <param name="TicksLeft">Kolik tiků jí zbývá, než se rozpadne.</param>
public readonly record struct FloatingLog(int X, int Y, int ResourceIndex, double Amount, int TicksLeft);

/// <summary>
/// Plavení dřeva: splav pouští klády do řeky, proud je nese a česle je vytahují.
///
/// <para>Proč to ve hře stojí za to: dřevo se dá vozit po zemi vždycky a všude.
/// Řeka je jediná cesta, která je <b>zadarmo, ale jen když ji máš</b> — a tím
/// z kusu krajiny dělá důvod, proč stavět zrovna tam.</para>
///
/// <para><b>Kláda musí umět skončit.</b> To je celé riziko téhle mechaniky:
/// tok končí v jezeře nebo v prohlubni, a kdyby tam kláda jen stála, hromadily
/// by se donekonečna a s nimi paměť. Proto platí dvě pravidla naráz —
/// kláda, která nemá kam plout, se rozpadne <b>hned</b>, a každá má navíc
/// pevnou životnost jako pojistka pro případ, že by se proud stočil do kruhu.
/// </para>
///
/// <para>Vrstva: čistá simulace. Ploché pole struktur o pevné velikosti,
/// žádné alokace v tiku (viz CLAUDE.md).</para>
/// </summary>
public sealed class LogRaftSystem
{
    /// <summary>
    /// Kolik klád může být na vodě naráz. Pevný strop, ne rostoucí pole:
    /// klády jsou kulisa s užitkem, ne ekonomika — a pevné pole nemůže
    /// vyhladovět paměť ani při stovce splavů.
    /// </summary>
    public const int Capacity = 128;

    /// <summary>Kolik tiků trvá kládě poplout o jednu dlaždici.</summary>
    private const int TicksPerTile = 4;

    /// <summary>Pojistka proti proudu, který se stočí do kruhu.</summary>
    private const int MaxLifeTicks = 4000;

    private struct Log
    {
        public int X;
        public int Y;
        public int ResourceIndex;
        public double Amount;
        public int TicksLeft;
        public int MoveCooldown;
        public bool Alive;
    }

    private readonly Log[] _logs = new Log[Capacity];
    private readonly ITerrain _terrain;

    public LogRaftSystem(ITerrain terrain) => _terrain = terrain;

    /// <summary>Kolik klád je právě na vodě.</summary>
    public int Count
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _logs.Length; i++)
            {
                if (_logs[i].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Klády na vodě — pro render a pro save.</summary>
    public IEnumerable<FloatingLog> Logs
    {
        get
        {
            for (int i = 0; i < _logs.Length; i++)
            {
                if (_logs[i].Alive)
                {
                    yield return new FloatingLog(
                        _logs[i].X, _logs[i].Y, _logs[i].ResourceIndex, _logs[i].Amount, _logs[i].TicksLeft);
                }
            }
        }
    }

    /// <summary>
    /// Pustí kládu na vodu. Vrací false, když je plno nebo tam řeka neteče —
    /// splav u stojaté vody nemá co plavit.
    /// </summary>
    public bool TryDrop(int x, int y, int resourceIndex, double amount)
    {
        if (!_terrain.TryRiverFlow(x, y, out _, out _))
        {
            return false;
        }

        for (int i = 0; i < _logs.Length; i++)
        {
            if (_logs[i].Alive)
            {
                continue;
            }

            _logs[i] = new Log
            {
                X = x,
                Y = y,
                ResourceIndex = resourceIndex,
                Amount = amount,
                TicksLeft = MaxLifeTicks,
                MoveCooldown = TicksPerTile,
                Alive = true,
            };

            return true;
        }

        return false;
    }

    /// <summary>
    /// Posune klády po proudu a nabídne je česlím.
    /// </summary>
    /// <param name="catcher">
    /// Zeptá se, jestli na téhle dlaždici někdo klády vytahuje. Vrací násobič,
    /// kterým se náklad vynásobí, nebo 0, když tu nikdo není. Rozhodnutí „kdo
    /// je česle" patří simulaci, ne tomuhle systému — ten umí jen vodu.
    /// </param>
    /// <param name="deliver">Předá vytažený náklad do skladu.</param>
    public void Tick(Func<int, int, double> catcher, Action<int, double> deliver)
    {
        for (int i = 0; i < _logs.Length; i++)
        {
            if (!_logs[i].Alive)
            {
                continue;
            }

            if (--_logs[i].TicksLeft <= 0)
            {
                _logs[i].Alive = false;
                continue;
            }

            if (--_logs[i].MoveCooldown > 0)
            {
                continue;
            }

            _logs[i].MoveCooldown = TicksPerTile;

            // Kláda, která nemá kam plout, doplavala: konec toku, jezero,
            // prohlubeň. Rozpadne se tady a teď — právě tohle drží počet klád
            // konečný.
            if (!_terrain.TryRiverFlow(_logs[i].X, _logs[i].Y, out int dx, out int dy))
            {
                _logs[i].Alive = false;
                continue;
            }

            _logs[i].X += dx;
            _logs[i].Y += dy;

            double multiplier = catcher(_logs[i].X, _logs[i].Y);
            if (multiplier > 0)
            {
                deliver(_logs[i].ResourceIndex, _logs[i].Amount * multiplier);
                _logs[i].Alive = false;
            }
        }
    }

    /// <summary>Vyprázdní řeku (Vzestup, nový svět).</summary>
    public void Reset() => Array.Clear(_logs);

    /// <summary>Obnova ze savu.</summary>
    public void Restore(IEnumerable<FloatingLog> logs)
    {
        Array.Clear(_logs);
        int slot = 0;
        foreach (var log in logs)
        {
            if (slot >= _logs.Length)
            {
                break;
            }

            _logs[slot++] = new Log
            {
                X = log.X,
                Y = log.Y,
                ResourceIndex = log.ResourceIndex,
                Amount = log.Amount,
                TicksLeft = log.TicksLeft,
                MoveCooldown = TicksPerTile,
                Alive = true,
            };
        }
    }
}
