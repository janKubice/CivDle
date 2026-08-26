using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering;
using Microsoft.Xna.Framework.Audio;

namespace CivDle.Audio;

/// <summary>
/// Prostorový zvuk města: mlýn je slyšet, když k němu hráč zamíří kamerou,
/// a zprava, když je vpravo.
///
/// <para>Proč to hra potřebuje: relaxační jádro stálo skoro jen na obraze.
/// Kulisa podle biomu (<see cref="AmbientSoundscape"/>) říká, <b>kde</b> město
/// stojí; tohle říká, <b>co v něm je</b> — a je to ta věc, díky které působí
/// obydleně, i když se na něm zrovna nic nehýbe.</para>
///
/// <para><b>Jeden hlas na druh, ne na budovu.</b> Sto pil by znělo jako kaše,
/// ze které si hráč vypne zvuk. Mixování řeší <see cref="SpatialMix"/>; tahle
/// třída jen vyrobí smyčky a nastaví je.</para>
///
/// <para>Bez zvukového zařízení se celá vrstva tiše vypne — kulisa nikdy nesmí
/// shodit hru.</para>
/// </summary>
public sealed class SpatialSoundscape : IDisposable
{
    private const int SampleRate = 22050;

    /// <summary>Délka smyčky. Celé sekundy, aby na sebe konce navazovaly bez lupnutí.</summary>
    private const int LoopSeconds = 2;

    /// <summary>Jak rychle se hlasitost dorovnává. Skoky by cvakaly.</summary>
    private const float FadePerSecond = 2.2f;

    /// <summary>Jak často se přepočítá, co je slyšet. Ne každý snímek — město se za 200 ms nezmění.</summary>
    private const float RefreshSeconds = 0.2f;

    private static readonly int KindCount = Enum.GetValues<SoundLoop>().Length;

    private readonly GameContent _content;
    private readonly SoundEffect?[] _effects = new SoundEffect?[KindCount];
    private readonly SoundEffectInstance?[] _instances = new SoundEffectInstance?[KindCount];
    private readonly SoundMix[] _target = new SoundMix[KindCount];
    private readonly float[] _volume = new float[KindCount];
    private readonly List<int> _nearby = new();

    private bool _enabled = true;
    private float _sinceRefresh;

    public SpatialSoundscape(GameContent content) => _content = content;

    /// <summary>Hlasitost druhu, jak zrovna zní. Pro testy a diagnostiku.</summary>
    public float VolumeOf(SoundLoop loop) => _volume[(int)loop];

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (!_enabled)
        {
            return;
        }

        _sinceRefresh += dt;
        if (_sinceRefresh >= RefreshSeconds)
        {
            _sinceRefresh = 0f;
            Refresh(camera, simulation);
        }

        for (int kind = 0; kind < KindCount; kind++)
        {
            _volume[kind] += Math.Clamp(_target[kind].Volume - _volume[kind], -dt * FadePerSecond, dt * FadePerSecond);
            Apply((SoundLoop)kind);
        }
    }

    /// <summary>Zjistí, co je kolem kamery slyšet.</summary>
    private void Refresh(Camera2D camera, Simulation simulation)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();

        // Přes index zástavby (bod 1.1): u velkoměsta by průchod všemi budovami
        // pětkrát za sekundu byl znát.
        simulation.BuildingsIn(
            (int)Math.Floor(min.X / tileSize) - 1,
            (int)Math.Floor(min.Y / tileSize) - 1,
            (int)Math.Ceiling(max.X / tileSize) + 1,
            (int)Math.Ceiling(max.Y / tileSize) + 1,
            _nearby);

        SpatialMix.Compute(
            _content,
            simulation.Buildings,
            _nearby,
            camera.Position.X / tileSize,
            camera.Position.Y / tileSize,
            _target);
    }

    /// <summary>Nastaví (a případně poprvé vyrobí) smyčku daného druhu.</summary>
    private void Apply(SoundLoop loop)
    {
        int kind = (int)loop;
        float volume = _volume[kind];

        if (volume <= 0.01f)
        {
            _instances[kind]?.Stop();
            return;
        }

        var instance = Instance(loop);
        if (instance is null)
        {
            return;
        }

        instance.Volume = Math.Clamp(volume, 0f, 1f);
        instance.Pan = Math.Clamp(_target[kind].Pan, -1f, 1f);
        if (instance.State != SoundState.Playing)
        {
            instance.Play();
        }
    }

    /// <summary>
    /// Smyčka se vyrobí, teprve až je poprvé potřeba. Hráč, který nikdy
    /// nepostaví huť, za ni nezaplatí ani jeden buffer.
    /// </summary>
    private SoundEffectInstance? Instance(SoundLoop loop)
    {
        int kind = (int)loop;
        if (_instances[kind] is { } existing)
        {
            return existing;
        }

        try
        {
            _effects[kind] = Synthesize(loop);
            var instance = _effects[kind]!.CreateInstance();
            instance.IsLooped = true;
            _instances[kind] = instance;
            return instance;
        }
        catch (Exception)
        {
            // Headless stroj / bez audio ovladače — hra poběží potichu.
            _enabled = false;
            return null;
        }
    }

    /// <summary>
    /// Vyrobí smyčku. Každý druh je pár sinusů a šumu — žádné audio soubory
    /// („no balast"), a přitom je od sebe poznat.
    /// </summary>
    private static SoundEffect Synthesize(SoundLoop loop)
    {
        int samples = SampleRate * LoopSeconds;
        var data = new float[samples];
        var rng = new Random(1337 + (int)loop);
        float previous = 0f;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;

            // Fáze pro rytmus musí na konci smyčky sedět na začátek, jinak by
            // to na švu lupalo — proto celé počty period za LoopSeconds.
            float cycle = t / LoopSeconds;
            float noise = (float)((rng.NextDouble() * 2) - 1);
            float filtered = (noise + previous) * 0.5f;
            previous = noise;

            data[i] = loop switch
            {
                SoundLoop.Mill =>
                    (MathF.Sin(MathF.Tau * 2 * cycle) * 0.35f * MathF.Sin(MathF.Tau * 92f * t))
                    + (filtered * 0.10f),
                SoundLoop.Forge =>
                    (MathF.Sin(MathF.Tau * 58f * t) * 0.30f)
                    + (filtered * 0.22f * (0.6f + (0.4f * MathF.Sin(MathF.Tau * 1 * cycle)))),
                SoundLoop.Water =>
                    filtered * 0.30f * (0.7f + (0.3f * MathF.Sin(MathF.Tau * 1 * cycle))),
                SoundLoop.Market =>
                    (filtered * 0.22f) + (MathF.Sin(MathF.Tau * 210f * t) * 0.05f),
                _ =>
                    (MathF.Sin(MathF.Tau * 4 * cycle) * 0.28f * MathF.Sin(MathF.Tau * 140f * t))
                    + (filtered * 0.08f),
            };
        }

        var pcm = new byte[data.Length * 2];
        for (int i = 0; i < data.Length; i++)
        {
            short value = (short)(Math.Clamp(data[i], -1f, 1f) * short.MaxValue * 0.7f);
            pcm[i * 2] = (byte)(value & 0xFF);
            pcm[(i * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
    }

    /// <summary>Ztiší všechno (odchod z hry do menu).</summary>
    public void Stop()
    {
        for (int i = 0; i < _instances.Length; i++)
        {
            _instances[i]?.Stop();
            _volume[i] = 0f;
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < _instances.Length; i++)
        {
            _instances[i]?.Dispose();
            _effects[i]?.Dispose();
        }
    }
}
