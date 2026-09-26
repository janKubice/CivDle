using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Jedna rozpoznaná osada: střed shluku (v dlaždicích), velikost, index jména
/// v <c>GameContent.SettlementNames</c> a stupeň v hierarchii sídel.
/// Odvozený stav — neukládá se, přepočítává se.
/// </summary>
/// <param name="CenterX">Těžiště shluku v dlaždicích.</param>
/// <param name="CenterY">Těžiště shluku v dlaždicích.</param>
/// <param name="BuildingCount">Kolik budov sídlo tvoří.</param>
/// <param name="NameIndex">Index jména v katalogu jmen.</param>
/// <param name="RankIndex">Stupeň v hierarchii sídel; −1 = žebříček je vypnutý.</param>
public readonly record struct Settlement(
    float CenterX, float CenterY, int BuildingCount, int NameIndex, int RankIndex = -1);

/// <summary>
/// Detekce osad (fáze 4: „shluk se pozná jako osada s jménem"). Union-find nad
/// budovami: dvě budovy patří k sobě, když je mezera mezi jejich půdorysy
/// nejvýš clusterDistance. Jméno určuje nejstarší budova shluku (nejnižší index)
/// hashem se seedem — je stabilní, i když osada roste nebo se slučuje.
/// Běží na nízké frekvenci a jen po změně zástavby (CLAUDE.md, výkon).
///
/// <para>Shluky hledá <see cref="FootprintClusters"/> přes prostorovou mřížku
/// a součty se dělají jedním průchodem. Dřív to bylo porovnání každé budovy
/// s každou a pak ještě průchod celým polem pro každý shluk: u města o 24 000
/// budovách stál jeden přepočet skoro dvě sekundy a dohánění offline času
/// kvůli tomu trvalo hodiny.</para>
/// </summary>
internal sealed class SettlementSystem
{
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly FootprintClusters _clusters;

    // Pomocná pole po kořenech shluků; rostou s městem, jinak se nealokuje.
    private int[] _all = Array.Empty<int>();
    private int[] _count = Array.Empty<int>();
    private float[] _sumX = Array.Empty<float>();
    private float[] _sumY = Array.Empty<float>();

    public SettlementSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
        _clusters = new FootprintClusters(content);
    }

    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.Settlements;
        if (sim.TickCount % config.UpdateIntervalTicks != 0 || !sim.SettlementsDirty)
        {
            return;
        }

        sim.SettlementsDirty = false;
        Recompute(sim, config);
    }

    private void Recompute(Simulation sim, SettlementConfig config)
    {
        var buildings = sim.Buildings;
        var result = sim.SettlementsMutable;
        result.Clear();
        int n = buildings.Length;
        if (n == 0)
        {
            return;
        }

        Ensure(n);
        for (int i = 0; i < n; i++)
        {
            _all[i] = i;
            _count[i] = 0;
            _sumX[i] = 0f;
            _sumY[i] = 0f;
        }

        _clusters.Build(buildings, _all.AsSpan(0, n), config.ClusterDistance);

        // Agregace jedním průchodem po kořenech. Pořadí sčítání (vzestupně podle
        // indexu) je stejné jako dřív, takže i těžiště vychází na bit stejně.
        for (int i = 0; i < n; i++)
        {
            int root = _clusters.Root(i);
            var def = _content.Buildings[buildings[i].DefIndex];
            _count[root]++;
            _sumX[root] += buildings[i].X + def.FootprintWidth * 0.5f;
            _sumY[root] += buildings[i].Y + def.FootprintHeight * 0.5f;
        }

        for (int root = 0; root < n; root++)
        {
            // Kořen je nejnižší index shluku = nejstarší budova (určuje jméno).
            if (_clusters.Root(root) != root)
            {
                continue;
            }

            int count = _count[root];
            if (count < config.MinBuildings)
            {
                continue;
            }

            int oldest = root;
            float centerX = _sumX[root] / count;
            float centerY = _sumY[root] / count;

            // Sídlo vyrostlé na pohlceném cizím městě si nechá jeho jméno —
            // hráč dostal město, ne stavební parcelu. Až když tam žádné nebylo,
            // losuje se ze seedu jako u každé jiné osady.
            int nameIndex = sim.InheritedNameAt(centerX, centerY);
            if (nameIndex < 0)
            {
                var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)oldest * 0xBF58476D1CE4E5B9UL)));
                nameIndex = (int)(rng.Next() % (ulong)_content.SettlementNames.Count);
            }

            int rankIndex = _content.SettlementRanks.RankFor(count);
            result.Add(new Settlement(centerX, centerY, count, nameIndex, rankIndex));

            // Povýšení se hlásí jen tehdy, když je to pro celou hru poprvé.
            // Kdyby se ohlašovalo u každého sídla zvlášť, hráč by při rozrůstání
            // dostal deset stejných hlášek za sebou — a z události by byl šum.
            if (rankIndex > sim.HighestSettlementRank)
            {
                sim.HighestSettlementRank = rankIndex;
                sim.EnqueueNotification(new GameNotification(
                    NotificationKind.Milestone,
                    "toast.settlementRank",
                    _content.SettlementRanks.Ranks[rankIndex].NameKey));
            }
        }
    }

    private void Ensure(int n)
    {
        if (_all.Length >= n)
        {
            return;
        }

        int size = Math.Max(n, Math.Max(64, _all.Length * 2));
        _all = new int[size];
        _count = new int[size];
        _sumX = new float[size];
        _sumY = new float[size];
    }
}
