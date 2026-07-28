using CivDle.Core.Sim;
using CivDle.Screens;
using Microsoft.Xna.Framework;

namespace CivDle.Capture;

/// <summary>
/// Projede herní obrazovku všemi nástroji a zkusí je použít — bez okna a bez
/// člověka u myši.
///
/// <para>Existuje kvůli konkrétní bolesti: pády, které se objeví až po kliknutí
/// na nástroj, testy simulace nechytí (jsou v UI vrstvě) a ruční hraní je
/// nespolehlivé. Tohle projde všechny režimy za pár sekund a spadne stejně
/// hlasitě jako hra, takže je hned vidět, kde.</para>
///
/// <para>Spouští se přes <c>--smoke</c>. Vypíše, co prošlo; první výjimka
/// probublá ven a skončí v <c>crash.log</c> se stackem.</para>
/// </summary>
public sealed class SmokeRun
{
    private readonly List<string> _passed = new();

    /// <summary>Vytvoří scénu, na které má smysl nástroje zkoušet.</summary>
    public static Simulation BuildScene(ScreenManager screens) =>
        CityFixture.Grow(screens.Content, seed: 20260728, minutes: 6);

    /// <summary>
    /// Zapne postupně každý nástroj, nechá obrazovku pár snímků žít a zkusí
    /// příkazy, které nástroj provádí. Volá se z herní smyčky, aby měla Myra
    /// i grafika normální podmínky.
    /// </summary>
    public void Run(ScreenManager screens, GameplayScreen screen, Simulation sim, GameTime time)
    {
        Check("silnice: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Road));
        Frames(screen, time);
        Check("silnice: postavit", () => BuildRoadsAround(sim));

        Check("silnice: bourat", () => screen.ActivateToolForSmoke(SmokeTool.RoadErase));
        Frames(screen, time);
        Check("silnice: zbourat", () => RemoveRoadsAround(sim));

        Check("sloučení: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Merge));
        Frames(screen, time);
        Check("sloučení: sloučit", () => MergeAround(sim));

        Check("sázení: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Plant));
        Frames(screen, time);

        Check("nástroje: vypnout", () => screen.ActivateToolForSmoke(SmokeTool.None));
        Frames(screen, time);

        Console.WriteLine($"smoke OK ({_passed.Count} kroků): {string.Join(", ", _passed)}");
    }

    private void Check(string what, Action action)
    {
        action();
        _passed.Add(what);
    }

    private static void Frames(GameplayScreen screen, GameTime time)
    {
        for (int i = 0; i < 3; i++)
        {
            screen.Update(time);
            screen.Draw(time);
        }
    }

    private static void BuildRoadsAround(Simulation sim)
    {
        for (int i = -20; i <= 20; i++)
        {
            sim.TryBuildRoad(sim.CityCenterX + i, sim.CityCenterY);
            sim.TryBuildRoad(sim.CityCenterX, sim.CityCenterY + i);
        }
    }

    private static void RemoveRoadsAround(Simulation sim)
    {
        for (int i = -20; i <= 20; i++)
        {
            sim.TryRemoveRoad(sim.CityCenterX + i, sim.CityCenterY);
            sim.TryRemoveRoad(sim.CityCenterX, sim.CityCenterY + i);
        }
    }

    private static void MergeAround(Simulation sim)
    {
        for (int y = -14; y <= 14; y++)
        {
            for (int x = -14; x <= 14; x++)
            {
                sim.TryMerge(sim.CityCenterX + x, sim.CityCenterY + y);
            }
        }
    }
}

/// <summary>Nástroje, které smoke test prochází.</summary>
public enum SmokeTool
{
    None,
    Road,
    RoadErase,
    Merge,
    Plant,
}
