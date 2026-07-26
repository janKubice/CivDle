using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework.Audio;

namespace CivDle.Audio;

/// <summary>
/// Ambientní kulisa podle toho, KDE město stojí a JAKÉ je počasí: šum větru,
/// příboj, déšť, ticho zasněžených plání. Relaxační jádro hry stálo doteď skoro
/// jen na obraze — zvuk odvede pro atmosféru víc než další desítka budov.
///
/// <para>Zvuk se syntetizuje z parametrů v <c>data/ambience.json</c>, žádné audio
/// assety se nevozí (stejný přístup jako <see cref="AmbientMusic"/>, „no balast").
/// Každá kulisa je bezešvá smyčka; při změně biomu nebo počasí se stará ztiší
/// a nová naběhne, aby zvuk nepřeskakoval.</para>
///
/// <para>Bez zvukového zařízení se celá vrstva tiše vypne — kulisa nikdy nesmí
/// shodit hru.</para>
/// </summary>
public sealed class AmbientSoundscape : IDisposable
{
    private const int SampleRate = 22050;
    private const int LoopSeconds = 4; // pulseHz jsou násobky 0.125 Hz → bezešvé
    private const float FadeSecondsPerUnit = 1.5f;

    private readonly IReadOnlyList<AmbienceDef> _defs;
    private readonly SoundEffect?[] _effects;
    private readonly SoundEffectInstance?[] _instances;
    private readonly float[] _volume;

    private int _active = -1;
    private bool _enabled = true;

    public AmbientSoundscape(GameContent content)
    {
        _defs = content.Ambience;
        _effects = new SoundEffect?[_defs.Count];
        _instances = new SoundEffectInstance?[_defs.Count];
        _volume = new float[_defs.Count];
    }

    /// <summary>
    /// Vybere kulisu podle stavu světa a plynule na ni přejde. Volá se každý snímek;
    /// samotná syntéza proběhne líně, teprve až je kulisa poprvé potřeba.
    /// </summary>
    public void Update(float dt, Simulation simulation)
    {
        if (!_enabled || _defs.Count == 0)
        {
            return;
        }

        int wanted = Select(simulation.CityBiome, simulation.CurrentWeatherIndex);
        if (wanted != _active)
        {
            _active = wanted;
        }

        for (int i = 0; i < _defs.Count; i++)
        {
            float target = i == _active ? (float)_defs[i].Volume : 0f;
            float step = dt / FadeSecondsPerUnit;
            _volume[i] = target > _volume[i]
                ? MathF.Min(target, _volume[i] + step)
                : MathF.Max(target, _volume[i] - step);

            Apply(i);
        }
    }

    /// <summary>Ztiší a zastaví vše (odchod z herní obrazovky).</summary>
    public void Stop()
    {
        for (int i = 0; i < _instances.Length; i++)
        {
            _volume[i] = 0f;
            TryDo(() => _instances[i]?.Stop());
        }

        _active = -1;
    }

    public void Dispose()
    {
        for (int i = 0; i < _instances.Length; i++)
        {
            _instances[i]?.Dispose();
            _effects[i]?.Dispose();
        }
    }

    /// <summary>
    /// Která kulisa sedí. Jevy počasí mají přednost před biomem — v lese, kde se
    /// žene bouřka, má být slyšet bouřka.
    /// </summary>
    private int Select(int biomeIndex, int weatherIndex)
    {
        for (int i = 0; i < _defs.Count; i++)
        {
            if (_defs[i].IsWeatherBound && _defs[i].Matches(biomeIndex, weatherIndex))
            {
                return i;
            }
        }

        for (int i = 0; i < _defs.Count; i++)
        {
            if (!_defs[i].IsWeatherBound && _defs[i].Matches(biomeIndex, weatherIndex))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Nastaví hlasitost instance; zticha ji zastaví, ať nežere hlasy.</summary>
    private void Apply(int index)
    {
        if (_volume[index] <= 0.001f)
        {
            TryDo(() => _instances[index]?.Stop());
            return;
        }

        var instance = EnsureInstance(index);
        if (instance is null)
        {
            return;
        }

        TryDo(() =>
        {
            instance.Volume = Math.Clamp(_volume[index], 0f, 1f);
            if (instance.State != SoundState.Playing)
            {
                instance.Play();
            }
        });
    }

    /// <summary>Syntéza proběhne líně — kulisy, kam se hráč nedostane, se nikdy nespočítají.</summary>
    private SoundEffectInstance? EnsureInstance(int index)
    {
        if (_instances[index] is { } existing)
        {
            return existing;
        }

        try
        {
            _effects[index] = Synthesize(_defs[index]);
            var instance = _effects[index]!.CreateInstance();
            instance.IsLooped = true;
            _instances[index] = instance;
            return instance;
        }
        catch (Exception)
        {
            _enabled = false; // bez audio zařízení celou vrstvu vypneme
            return null;
        }
    }

    /// <summary>
    /// Složí bezešvou smyčku: filtrovaný šum (vítr, déšť, příboj) plus táhlý tón,
    /// obojí pod pomalým vlnobitím hlasitosti. Šum se vyhlazuje jednoduchým
    /// průměrováním — syrový bílý šum zní jako porucha, ne jako příroda.
    /// </summary>
    private static SoundEffect Synthesize(AmbienceDef def)
    {
        int samples = SampleRate * LoopSeconds;
        var data = new float[samples];
        var random = new Random(def.Id.GetHashCode());

        float smoothed = 0f;
        for (int i = 0; i < samples; i++)
        {
            double time = i / (double)SampleRate;

            float white = (float)(random.NextDouble() * 2 - 1);
            smoothed += (white - smoothed) * 0.08f; // dolní propust → šumění, ne praskot
            double value = smoothed * def.NoiseLevel;

            if (def.ToneHz > 0)
            {
                value += Math.Sin(2 * Math.PI * def.ToneHz * time) * def.ToneLevel;
            }

            // Vlnobití hlasitosti; celý počet cyklů za smyčku drží šev neslyšitelný.
            double pulse = def.PulseHz > 0
                ? 0.75 + 0.25 * Math.Sin(2 * Math.PI * def.PulseHz * time)
                : 1.0;

            data[i] = (float)(value * pulse);
        }

        // Křížový přechod konce do začátku — šum sám o sobě bezešvý není.
        int blend = SampleRate / 4;
        for (int i = 0; i < blend; i++)
        {
            float t = i / (float)blend;
            data[i] = data[i] * t + data[samples - blend + i] * (1 - t);
        }

        var pcm = new byte[samples * 2];
        for (int i = 0; i < samples; i++)
        {
            short sample = (short)(Math.Clamp(data[i], -1f, 1f) * short.MaxValue * 0.6f);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    private static void TryDo(Action action)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            // Zvuk nikdy nesmí shodit hru.
        }
    }
}
