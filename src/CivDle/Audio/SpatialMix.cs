using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Audio;

/// <summary>Jak nahlas a odkud má znít jeden druh zvuku.</summary>
/// <param name="Volume">Hlasitost 0–1.</param>
/// <param name="Pan">Panorama −1 (vlevo) … 1 (vpravo).</param>
public readonly record struct SoundMix(float Volume, float Pan)
{
    /// <summary>Ticho.</summary>
    public static SoundMix Silent { get; } = new(0f, 0f);

    /// <summary>Je vůbec co hrát?</summary>
    public bool IsAudible => Volume > 0.01f;
}

/// <summary>
/// Míchání prostorového zvuku: z budov v okolí kamery udělá pro každý druh
/// jednu hlasitost a jedno panorama.
///
/// <para><b>Jeden hlas na druh, ne na budovu.</b> Sto pil by jinak znělo jako
/// sto pil — tedy jako kaše, ze které si hráč vypne zvuk. Takhle je slyšet
/// „mlýnská čtvrť" jako jedna věc, která je hlasitější, když je mlýnů víc,
/// a chodí zprava doleva, jak hráč jede kamerou.</para>
///
/// <para>Čistá statická funkce: mixování je rozhodnutí o hře („co je slyšet"),
/// ne o zvukové kartě, a má se dát ověřit bez reproduktoru.</para>
/// </summary>
public static class SpatialMix
{
    /// <summary>
    /// Kolik budov téhož druhu ještě přidá na hlasitosti. Nad tím už je to
    /// jen víc téhož — a kdyby se sčítalo dál, přebilo by průmyslové město
    /// všechno ostatní.
    /// </summary>
    public const int MaxStack = 6;

    /// <summary>
    /// Spočte mix pro každý druh zvuku podle budov kolem bodu.
    /// </summary>
    /// <param name="content">Definice budov (kvůli bloku <c>sound</c>).</param>
    /// <param name="buildings">Budovy k posouzení (typicky výřez kamery).</param>
    /// <param name="indices">Které z nich (indexy do <paramref name="buildings"/>).</param>
    /// <param name="listenerX">Kde hráč poslouchá, v dlaždicích.</param>
    /// <param name="listenerY">Kde hráč poslouchá, v dlaždicích.</param>
    /// <param name="results">Sem se zapíše mix pro každý druh (pole velikosti počtu druhů).</param>
    public static void Compute(
        GameContent content,
        ReadOnlySpan<BuildingInstance> buildings,
        IReadOnlyList<int> indices,
        double listenerX,
        double listenerY,
        SoundMix[] results)
    {
        Span<float> volume = stackalloc float[results.Length];
        Span<float> pan = stackalloc float[results.Length];
        Span<int> counted = stackalloc int[results.Length];

        for (int slot = 0; slot < indices.Count; slot++)
        {
            int index = indices[slot];
            if (index >= buildings.Length)
            {
                continue;
            }

            var def = content.Buildings[buildings[index].DefIndex];
            if (def.Sound is not { } sound
                || buildings[index].Stall == BuildingStall.UnderConstruction)
            {
                continue;
            }

            int kind = (int)sound.Loop;
            if (counted[kind] >= MaxStack)
            {
                continue;
            }

            double dx = buildings[index].X - listenerX;
            double dy = buildings[index].Y - listenerY;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance >= sound.RadiusTiles)
            {
                continue;
            }

            // Lineární útlum. Fyzikálně by to mělo klesat s druhou mocninou,
            // jenže tady nejde o fyziku, ale o to, aby zvuk nezmizel dřív, než
            // budova sjede z obrazovky.
            float contribution = (float)(sound.Volume * (1.0 - (distance / sound.RadiusTiles)));
            volume[kind] += contribution;

            // Panorama váží hlasitost: bližší budova určuje směr víc než
            // vzdálená, jinak by dva mlýny na opačných stranách znamenaly, že
            // ani jeden není slyšet ze své strany.
            pan[kind] += contribution * (float)Math.Clamp(dx / Math.Max(1.0, sound.RadiusTiles), -1.0, 1.0);
            counted[kind]++;
        }

        for (int kind = 0; kind < results.Length; kind++)
        {
            if (volume[kind] <= 0)
            {
                results[kind] = SoundMix.Silent;
                continue;
            }

            float total = Math.Min(1f, volume[kind]);
            results[kind] = new SoundMix(total, Math.Clamp(pan[kind] / volume[kind], -1f, 1f));
        }
    }
}
