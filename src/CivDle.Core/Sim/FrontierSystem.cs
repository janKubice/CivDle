using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jeden útočník. Struktura v plochém poli — jde o stovky entit v tikové smyčce.</summary>
public struct Attacker
{
    /// <summary>Poloha ve dlaždicích (zlomková, pohybuje se plynule).</summary>
    public float X;

    /// <summary>Poloha ve dlaždicích.</summary>
    public float Y;

    /// <summary>Kolik zdraví zbývá; 0 = mrtvý a místo se recykluje.</summary>
    public int Health;

    /// <summary>Index druhu v <see cref="FrontierConfig.Attackers"/>.</summary>
    public int TypeIndex;

    /// <summary>Kolik tiků do dalšího úderu.</summary>
    public int AttackCooldown;
}

/// <summary>
/// Frontier Defense: vlny útočníků a věže, které je sestřelují.
///
/// <para><b>Proč je to poslední položka plánu:</b> je to jediná věc, která
/// přidává novou entitu do tikové smyčky. Proto je taky celá data-oriented —
/// <see cref="Attacker"/> je struktura v plochém poli, pole se recykluje
/// a v tiku se nealokuje nic.</para>
///
/// <para><b>Žádný pathfinding.</b> Útočník jde přímo k těžišti města, a když
/// mu v cestě stojí budova, pustí se do ní. To není zjednodušení, to je ta
/// mechanika: hradba z budov opravdu zdrží, protože se musí prokousat.</para>
///
/// <para><b>Nic není náhoda.</b> Kdy vlna přijde, kolik jich je i odkud
/// vyrazí, plyne z čísla vlny — tentýž seed a tentýž počet tiků dá tutéž
/// bitvu. Bez toho by se rozešel save i test.</para>
///
/// <para><b>Zásah město nezničí.</b> Budova se poškodí a na chvíli vypadne
/// z výroby, pak se opraví sama. Trvalá ztráta postupu je v idle hře trest za
/// to, že šel hráč spát.</para>
/// </summary>
public sealed class FrontierSystem
{
    private readonly FrontierConfig _config;

    private Attacker[] _attackers = new Attacker[64];
    private int _count;

    private int _nextWave;
    private int _killed;
    private int _reachedCity;

    public FrontierSystem(FrontierConfig config) => _config = config;

    /// <summary>Kolik útočníků je právě na mapě.</summary>
    public int Count => _count;

    /// <summary>Kolikátá vlna přijde jako další (0 = ještě žádná nebyla).</summary>
    public int NextWave => _nextWave;

    /// <summary>Kolik jich hráč za celý běh sestřelil.</summary>
    public int Killed => _killed;

    /// <summary>Kolikrát se útočník prokousal až do města.</summary>
    public int ReachedCity => _reachedCity;

    /// <summary>Živí útočníci pro render. Čte se, nezapisuje.</summary>
    public ReadOnlySpan<Attacker> Attackers => _attackers.AsSpan(0, _count);

    /// <summary>V kterém tiku dorazí další vlna.</summary>
    public long NextWaveTick => _config.TickOfWave(_nextWave);

    /// <summary>
    /// Posune bitvu o tik. Vrací true, když se něco stalo, co stojí za
    /// překreslení nebo za zvuk — volající si to nemusí odvozovat z počtů.
    /// </summary>
    public bool Tick(Simulation sim)
    {
        if (!_config.IsAvailable)
        {
            return false;
        }

        Repair(sim);
        bool spawned = SpawnDueWave(sim);
        bool fought = MoveAndStrike(sim);
        bool shot = FireDefenses(sim);
        return spawned || fought || shot;
    }

    /// <summary>
    /// Poškozené budovy se samy opravují.
    ///
    /// <para>Vlastní průchod, ne přílepek k výrobě: opravovat se musí i domy
    /// a sklady, které žádný recept nemají a výrobní smyčka je přeskakuje.
    /// Běží jen v zapnutém režimu, takže běžnou hru nestojí nic.</para>
    /// </summary>
    private static void Repair(Simulation sim)
    {
        var buildings = sim.BuildingsMutable;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DisabledTicks > 0)
            {
                buildings[i].DisabledTicks--;
            }
        }
    }

    /// <summary>
    /// Přeskočí vlny, které měly přijít dřív, než se režim zapnul.
    ///
    /// <para>Bez tohohle se rozvrh „dohání": režim zapnutý v rozehrané hře
    /// spustí v každém tiku jednu zmeškanou vlnu a za pár vteřin stojí na mapě
    /// sedmdesát útočníků naráz. Ve smoke běhu se to stalo hned napoprvé.</para>
    ///
    /// <para>Dohánění při normálním běhu (i offline) tím netrpí: tam se tiká
    /// po jednom a vlny chodí v pořadí, jak mají.</para>
    /// </summary>
    public void SkipToTick(long tick)
    {
        if (!_config.IsAvailable)
        {
            return;
        }

        while (_config.TickOfWave(_nextWave) <= tick)
        {
            _nextWave++;
        }
    }

    /// <summary>
    /// Ladicí: pošle další vlnu hned teď.
    ///
    /// <para>Pro nástroje — smoke běh a snímky. Dotikat k vlně poctivě znamená
    /// až dva a půl tisíce tiků nad rozrostlým městem, a na tom, co se má
    /// ověřit (že bitva proběhne a nakreslí se), to nic nemění.</para>
    /// </summary>
    public void DebugForceWave(Simulation sim)
    {
        if (_config.IsAvailable)
        {
            SpawnDueWaveNow(sim);
        }
    }

    /// <summary>Vyprázdní bojiště (Vzestup, nový svět).</summary>
    public void Reset()
    {
        _count = 0;
        _nextWave = 0;
        _killed = 0;
        _reachedCity = 0;
    }

    /// <summary>Obnova ze savu.</summary>
    public void Restore(int nextWave, int killed, int reachedCity, ReadOnlySpan<Attacker> attackers)
    {
        _nextWave = Math.Max(0, nextWave);
        _killed = Math.Max(0, killed);
        _reachedCity = Math.Max(0, reachedCity);

        EnsureCapacity(attackers.Length);
        attackers.CopyTo(_attackers);
        _count = attackers.Length;
    }

    private bool SpawnDueWave(Simulation sim)
    {
        if (sim.TickCount < _config.TickOfWave(_nextWave))
        {
            return false;
        }

        SpawnDueWaveNow(sim);
        return true;
    }

    private void SpawnDueWaveNow(Simulation sim)
    {
        var wave = _config.WaveAt(_nextWave);
        for (int i = 0; i < wave.Count; i++)
        {
            int count = _config.CountInWave(_nextWave, wave[i]);
            for (int n = 0; n < count; n++)
            {
                Spawn(sim, wave[i].AttackerIndex, n, count);
            }
        }

        _nextWave++;
    }

    /// <summary>
    /// Postaví útočníka na kruh kolem města. Úhel plyne z čísla vlny a pořadí —
    /// jedna vlna tedy přichází z jedné strany a ne ze všech naráz, což je
    /// čitelnější a dá se na to reagovat.
    /// </summary>
    private void Spawn(Simulation sim, int typeIndex, int ordinal, int total)
    {
        EnsureCapacity(_count + 1);

        double waveAngle = Hash(_nextWave) * Math.Tau;
        double spread = (ordinal - (total - 1) / 2.0) * 0.06;
        double angle = waveAngle + spread;

        ref var attacker = ref _attackers[_count++];
        attacker.X = (float)(sim.CityCenterX + Math.Cos(angle) * _config.SpawnDistance);
        attacker.Y = (float)(sim.CityCenterY + Math.Sin(angle) * _config.SpawnDistance);
        attacker.Health = _config.Attackers[typeIndex].Health;
        attacker.TypeIndex = typeIndex;
        attacker.AttackCooldown = 0;
    }

    /// <summary>
    /// Krok útočníků: buď se jde k městu, nebo se mlátí do budovy, která stojí
    /// v cestě.
    /// </summary>
    private bool MoveAndStrike(Simulation sim)
    {
        bool anything = false;

        for (int i = 0; i < _count; i++)
        {
            ref var attacker = ref _attackers[i];
            var def = _config.Attackers[attacker.TypeIndex];

            if (attacker.AttackCooldown > 0)
            {
                attacker.AttackCooldown--;
            }

            double dx = sim.CityCenterX - attacker.X;
            double dy = sim.CityCenterY - attacker.Y;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));

            int aheadX = (int)Math.Round(attacker.X + (distance > 0 ? dx / distance : 0));
            int aheadY = (int)Math.Round(attacker.Y + (distance > 0 ? dy / distance : 0));

            if (sim.TryGetBuildingAt(aheadX, aheadY, out int building))
            {
                if (attacker.AttackCooldown == 0)
                {
                    sim.DamageBuilding(building, def.Damage);
                    attacker.AttackCooldown = def.AttackIntervalTicks;
                    anything = true;
                }

                continue; // do budovy se nevejde, tudy se dál nejde
            }

            if (distance <= 1.0)
            {
                // Došel až doprostřed. Nic se neničí — jen se to počítá, aby
                // hráč viděl, jak si vedl.
                _reachedCity++;
                RemoveAt(i--);
                anything = true;
                continue;
            }

            attacker.X += (float)(dx / distance * def.SpeedTilesPerTick);
            attacker.Y += (float)(dy / distance * def.SpeedTilesPerTick);
        }

        return anything;
    }

    /// <summary>
    /// Věže střílejí. Cíl se vybírá podle vzdálenosti a při shodě podle
    /// indexu — ne podle pořadí ve slovníku, které by se mezi běhy lišilo
    /// a rozbilo determinismus.
    /// </summary>
    private bool FireDefenses(Simulation sim)
    {
        if (_count == 0)
        {
            return false;
        }

        bool fired = false;
        var buildings = sim.BuildingsMutable;

        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var def = sim.ContentRef.Buildings[building.DefIndex];
            if (!def.IsArmed || !building.IsComplete || building.DisabledTicks > 0)
            {
                continue;
            }

            if (building.ReloadTicks > 0)
            {
                building.ReloadTicks--;
                continue;
            }

            var rule = def.Defense!;
            int target = NearestTarget(building.X, building.Y, def, rule.Range);
            if (target < 0)
            {
                continue;
            }

            _attackers[target].Health -= rule.Damage;
            building.ReloadTicks = (short)Math.Min(short.MaxValue, rule.IntervalTicks);
            fired = true;

            if (_attackers[target].Health <= 0)
            {
                _killed++;
                RemoveAt(target);
            }
        }

        return fired;
    }

    private int NearestTarget(int x, int y, BuildingDef def, int range)
    {
        // Střílí se ze středu půdorysu: u velké věže by roh znamenal, že na
        // jedné straně dostřelí o dvě dlaždice dál než na druhé.
        double centerX = x + (def.FootprintWidth - 1) / 2.0;
        double centerY = y + (def.FootprintHeight - 1) / 2.0;

        int best = -1;
        double bestDistance = range * (double)range;

        for (int i = 0; i < _count; i++)
        {
            double dx = _attackers[i].X - centerX;
            double dy = _attackers[i].Y - centerY;
            double distance = (dx * dx) + (dy * dy);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Swap-remove: mrtvý se přepíše posledním. Pořadí nikdo nepotřebuje.</summary>
    private void RemoveAt(int index)
    {
        _attackers[index] = _attackers[--_count];
    }

    private void EnsureCapacity(int needed)
    {
        if (needed <= _attackers.Length)
        {
            return;
        }

        int size = _attackers.Length;
        while (size < needed)
        {
            size *= 2;
        }

        Array.Resize(ref _attackers, size);
    }

    /// <summary>Deterministické „náhodné" číslo 0–1 z čísla vlny.</summary>
    private static double Hash(int wave)
    {
        ulong h = (ulong)wave * 0x9E3779B97F4A7C15UL;
        h ^= h >> 30;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 32;
        return (h >> 11) * (1.0 / (1UL << 53));
    }
}
