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
/// </summary>
internal sealed class SettlementSystem
{
    private readonly GameContent _content;
    private readonly long _seed;
    private int[] _parent = Array.Empty<int>();

    public SettlementSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
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
        if (buildings.Length == 0)
        {
            return;
        }

        if (_parent.Length < buildings.Length)
        {
            _parent = new int[Math.Max(buildings.Length, 64)];
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            _parent[i] = i;
        }

        // O(n²) stačí — běží zřídka a budov jsou stovky, ne miliony.
        for (int i = 0; i < buildings.Length; i++)
        {
            for (int j = i + 1; j < buildings.Length; j++)
            {
                if (FootprintGap(buildings[i], buildings[j]) <= config.ClusterDistance)
                {
                    Union(i, j);
                }
            }
        }

        // Agregace po kořenech: počet, těžiště, nejstarší budova (určuje jméno).
        for (int root = 0; root < buildings.Length; root++)
        {
            if (Find(root) != root)
            {
                continue;
            }

            int count = 0;
            int oldest = int.MaxValue;
            float sumX = 0f;
            float sumY = 0f;
            for (int i = 0; i < buildings.Length; i++)
            {
                if (Find(i) != root)
                {
                    continue;
                }

                var def = _content.Buildings[buildings[i].DefIndex];
                count++;
                oldest = Math.Min(oldest, i);
                sumX += buildings[i].X + def.FootprintWidth * 0.5f;
                sumY += buildings[i].Y + def.FootprintHeight * 0.5f;
            }

            if (count < config.MinBuildings)
            {
                continue;
            }

            var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)oldest * 0xBF58476D1CE4E5B9UL)));
            int nameIndex = (int)(rng.Next() % (ulong)_content.SettlementNames.Count);
            int rankIndex = _content.SettlementRanks.RankFor(count);
            result.Add(new Settlement(sumX / count, sumY / count, count, nameIndex, rankIndex));

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

    /// <summary>Čebyševova mezera mezi půdorysy (0 = dotýkají se nebo překrývají).</summary>
    private int FootprintGap(in BuildingInstance a, in BuildingInstance b)
    {
        var defA = _content.Buildings[a.DefIndex];
        var defB = _content.Buildings[b.DefIndex];

        int gapX = Math.Max(0, Math.Max(a.X - (b.X + defB.FootprintWidth - 1), b.X - (a.X + defA.FootprintWidth - 1)) - 1);
        int gapY = Math.Max(0, Math.Max(a.Y - (b.Y + defB.FootprintHeight - 1), b.Y - (a.Y + defA.FootprintHeight - 1)) - 1);
        return Math.Max(gapX, gapY);
    }

    private int Find(int i)
    {
        while (_parent[i] != i)
        {
            _parent[i] = _parent[_parent[i]];
            i = _parent[i];
        }

        return i;
    }

    private void Union(int a, int b)
    {
        int rootA = Find(a);
        int rootB = Find(b);
        if (rootA != rootB)
        {
            // Nižší kořen vyhrává → stabilní reprezentant (nejstarší budova).
            if (rootA < rootB)
            {
                _parent[rootB] = rootA;
            }
            else
            {
                _parent[rootA] = rootB;
            }
        }
    }
}
