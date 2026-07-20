using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Automatický růst zástavby (fáze 2 roadmapy: „domy se staví samy dle poptávky").
/// Když se populace blíží kapacitě bydlení, civilizace si sama postaví budovu
/// označenou <c>autoBuild</c> poblíž existující zástavby — za normální cenu,
/// takže růst táhne poptávku po surovinách (dřevo → prkna).
///
/// Běží na nízké frekvenci (intervalTicks), ne každý tik (CLAUDE.md, výkon).
/// „Náhoda" je bezstavový hash (seed, tik) — deterministická a přežívá save/load
/// bez ukládání stavu RNG.
/// </summary>
internal sealed class AutoBuildSystem
{
    private readonly GameContent _content;
    private readonly long _seed;

    /// <summary>Ofsety kandidátních míst kolem kotvy, od nejbližších — domy se lepí k sobě (organická vesnice).</summary>
    private readonly (int X, int Y)[] _searchOffsets;

    public AutoBuildSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;

        int radius = content.Gameplay.AutoBuild.SearchRadius;
        var offsets = new List<(int X, int Y)>();
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius && (x != 0 || y != 0))
                {
                    offsets.Add((x, y));
                }
            }
        }

        offsets.Sort((a, b) =>
        {
            int distance = (a.X * a.X + a.Y * a.Y).CompareTo(b.X * b.X + b.Y * b.Y);
            return distance != 0 ? distance : (a.Y, a.X).CompareTo((b.Y, b.X));
        });
        _searchOffsets = offsets.ToArray();
    }

    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.AutoBuild;
        if (sim.TickCount % config.IntervalTicks != 0)
        {
            return;
        }

        // Bez zástavby není kde růst — první budovu musí položit hráč.
        if (sim.Buildings.Length == 0)
        {
            return;
        }

        // Poptávka po bydlení: populace dorůstá kapacitu.
        if (sim.Population < sim.HousingCapacity - config.PopulationHeadroom)
        {
            return;
        }

        // Deterministická volba kotvy: hash (seed, tik), žádný stav k ukládání.
        var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)sim.TickCount * 0x9E3779B97F4A7C15UL)));
        var anchor = sim.Buildings[(int)(rng.Next() % (ulong)sim.Buildings.Length)];

        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            if (!_content.Buildings[defIndex].AutoBuild)
            {
                continue;
            }

            if (TryBuildNear(sim, defIndex, anchor.X, anchor.Y))
            {
                return; // max jedna budova za interval — růst má být pozvolný
            }
        }
    }

    private bool TryBuildNear(Simulation sim, int defIndex, int anchorX, int anchorY)
    {
        foreach (var (offsetX, offsetY) in _searchOffsets)
        {
            var result = sim.CanPlace(defIndex, anchorX + offsetX, anchorY + offsetY);
            if (result == PlacementResult.Ok)
            {
                return sim.TryPlaceBuilding(defIndex, anchorX + offsetX, anchorY + offsetY) == PlacementResult.Ok;
            }

            if (result == PlacementResult.NotEnoughResources)
            {
                // Cena nezávisí na místě — další hledání nemá smysl.
                return false;
            }
        }

        return false;
    }
}
