using Microsoft.Xna.Framework.Audio;

namespace CivDle.Audio;

/// <summary>
/// Klidná ambientní smyčka generovaná proceduálně (žádné assety, „no balast"):
/// tichý táhlý akord s pomalým vlnobitím hlasitosti pro relaxační jádro hry.
/// Smyčka je bezešvá (všechny tóny i LFO mají celý počet cyklů za délku smyčky).
/// Respektuje globální hlasitost (<see cref="SoundEffect.MasterVolume"/>). Bez
/// audio zařízení se tiše vypne — zvuk nikdy nesmí shodit hru.
/// </summary>
public sealed class AmbientMusic : IDisposable
{
    private const int SampleRate = 22050;
    private const int LoopSeconds = 8; // frekvence jsou násobky 1/8 Hz → bezešvé

    private readonly SoundEffect? _pad;
    private readonly SoundEffectInstance? _instance;

    public AmbientMusic()
    {
        try
        {
            _pad = CreatePad();
            _instance = _pad.CreateInstance();
            _instance.IsLooped = true;
            _instance.Volume = 0.55f;
        }
        catch (Exception)
        {
            _pad = null;
            _instance = null;
        }
    }

    /// <summary>Spustí smyčku (pokud už neběží).</summary>
    public void Play()
    {
        try
        {
            if (_instance is { State: not SoundState.Playing })
            {
                _instance.Play();
            }
        }
        catch (Exception)
        {
            // Bez audio zařízení nebo při chybě přehrávání jen mlčíme.
        }
    }

    /// <summary>Zastaví smyčku.</summary>
    public void Stop()
    {
        try
        {
            _instance?.Stop();
        }
        catch (Exception)
        {
            // ignorováno
        }
    }

    public void Dispose()
    {
        _instance?.Dispose();
        _pad?.Dispose();
    }

    private static SoundEffect CreatePad()
    {
        int samples = SampleRate * LoopSeconds;
        var data = new float[samples];

        // A-moll pad (A2, C3, E3), zaokrouhleno na mřížku 0.125 Hz kvůli bezešvosti.
        float[] freqs = { 110f, 130.5f, 164.75f };
        const float lfoHz = 0.125f; // jedno vlnobití za smyčku (8 s)

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float chord = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                // Základ + tichá oktáva pro teplo.
                chord += MathF.Sin(MathF.Tau * freqs[n] * t) + 0.35f * MathF.Sin(MathF.Tau * 2f * freqs[n] * t);
            }

            chord /= freqs.Length;
            float swell = 0.72f + 0.28f * MathF.Sin(MathF.Tau * lfoHz * t);
            data[i] = chord * swell * 0.20f;
        }

        var pcm = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            short value = (short)(Math.Clamp(data[i], -1f, 1f) * short.MaxValue * 0.8f);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }
}
