using System.Diagnostics;
using CivDle.Core.Sim;
using CivDle.Screens;
using Microsoft.Xna.Framework;

namespace CivDle.Capture;

/// <summary>
/// Změří, kolik stojí vykreslení snímku při různém přiblížení.
///
/// <para>Existuje kvůli hlášení „když jen kousek oddálím, extrémně to laguje".
/// Bez čísel by se optimalizovalo od oka — a u vykreslování je od oka skoro
/// vždycky špatně. Tohle vypíše tabulku „zoom → ms na snímek", takže je vidět,
/// kde přesně to spadne a jestli oprava pomohla.</para>
///
/// <para>Spouští se přes <c>--perf</c>.</para>
/// </summary>
public sealed class PerfRun
{
    /// <summary>Přiblížení, na kterých se měří. 0.5 je práh agregátního pohledu.</summary>
    private static readonly float[] Zooms = { 4.0f, 2.5f, 1.6f, 1.2f, 0.9f, 0.7f, 0.55f, 0.45f, 0.3f };

    /// <summary>Kolik snímků se v každém kroku zahodí, než se začne měřit.</summary>
    private const int WarmupFrames = 8;

    /// <summary>Kolik snímků se změří a zprůměruje.</summary>
    private const int MeasuredFrames = 24;

    /// <summary>Vypěstuje město velké tak, aby mělo cenu ho měřit.</summary>
    public static Simulation BuildScene(ScreenManager screens) =>
        CityFixture.Grow(screens.Content, seed: 20260728, minutes: 20);

    public void Run(GameplayScreen screen, Simulation sim, GameTime time)
    {
        var focus = new Vector2(
            (sim.CityCenterX + 0.5f) * Rendering.TerrainRenderer.TileSize,
            (sim.CityCenterY + 0.5f) * Rendering.TerrainRenderer.TileSize);

        Console.WriteLine();
        Console.WriteLine($"=== výkon vykreslování ({sim.Buildings.Length} budov) ===");
        Console.WriteLine($"{"zoom",6} {"ms/snímek",12} {"FPS",8}");

        foreach (float zoom in Zooms)
        {
            screen.FocusForCapture(focus, zoom);

            for (int i = 0; i < WarmupFrames; i++)
            {
                screen.Update(time);
                screen.Draw(time);
            }

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < MeasuredFrames; i++)
            {
                screen.Update(time);
                screen.Draw(time);
            }

            watch.Stop();
            double ms = watch.Elapsed.TotalMilliseconds / MeasuredFrames;
            Console.WriteLine($"{zoom,6:0.00} {ms,12:0.00} {1000.0 / Math.Max(0.001, ms),8:0}");
        }

        Console.WriteLine();
        Console.WriteLine("Cíl: pod 16 ms (60 FPS). Nad 33 ms je to znát jako sekání.");
    }
}
