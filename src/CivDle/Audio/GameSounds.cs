using Microsoft.Xna.Framework.Audio;

namespace CivDle.Audio;

/// <summary>
/// Placeholder zvuky generované proceduálně (krátký „sek" a tlumené „žuch") —
/// dokud nejsou audio assety, drží aspoň zvukovou odezvu akcí. Náhodné kolísání
/// výšky, ať se zvuk neomrzí (game-feel-wow.md / data-driven doc: pitchRange).
/// Bez audio zařízení se tiše vypne — zvuk nikdy nesmí shodit hru.
/// Až přijdou skutečné assety, nahradí tohle registr zvuků ze sounds.json.
/// </summary>
public sealed class GameSounds : IDisposable
{
    private const int SampleRate = 22050;

    private readonly SoundEffect? _chop;
    private readonly SoundEffect? _place;

    public GameSounds()
    {
        try
        {
            _chop = CreateChop();
            _place = CreateThud();
        }
        catch (Exception)
        {
            // Headless stroj / bez audio ovladače — hra poběží potichu.
            _chop = null;
            _place = null;
        }
    }

    /// <summary>Seknutí při ruční těžbě.</summary>
    public void PlayChop() => Play(_chop, volume: 0.35f);

    /// <summary>Žuchnutí při položení budovy.</summary>
    public void PlayPlace() => Play(_place, volume: 0.5f);

    public void Dispose()
    {
        _chop?.Dispose();
        _place?.Dispose();
    }

    private static void Play(SoundEffect? sound, float volume)
    {
        if (sound is null)
        {
            return;
        }

        float pitch = (Random.Shared.NextSingle() - 0.5f) * 0.24f;
        sound.Play(volume, pitch, pan: 0f);
    }

    /// <summary>Krátký tlumený šum s rychlým dozvukem — „seknutí do dřeva".</summary>
    private static SoundEffect CreateChop()
    {
        int samples = (int)(SampleRate * 0.09);
        var data = new float[samples];
        var rng = new Random(1234);
        float previous = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float noise = (float)(rng.NextDouble() * 2 - 1);
            // Jednoduchý low-pass (průměr se sousedem) × exponenciální dozvuk.
            float filtered = (noise + previous) * 0.5f;
            previous = noise;
            data[i] = filtered * MathF.Exp(-t * 45f);
        }

        return ToSoundEffect(data);
    }

    /// <summary>Klesající sinus s dozvukem — tlumené „žuch" stavby.</summary>
    private static SoundEffect CreateThud()
    {
        int samples = (int)(SampleRate * 0.14);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float frequency = 150f - 600f * t; // rychlý pokles do hloubky
            frequency = MathF.Max(frequency, 55f);
            data[i] = MathF.Sin(MathF.Tau * frequency * t) * MathF.Exp(-t * 24f);
        }

        return ToSoundEffect(data);
    }

    private static SoundEffect ToSoundEffect(float[] samples)
    {
        var pcm = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue * 0.8f);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }
}
