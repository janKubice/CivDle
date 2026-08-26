using CivDle.Core.Content;
using CivDle.Core.Sim;
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
    private readonly SoundEffect? _chime;

    /// <summary>
    /// Tóny zvonohry, jeden na stupeň stupnice.
    ///
    /// <para>Vyrobí se jednou při startu, ne při každém zvonění: melodie hraje
    /// osm tónů za sebou a generovat kvůli tomu osm bufferů by znamenalo
    /// alokovat půl megabajtu uprostřed slavnosti.</para>
    /// </summary>
    private readonly SoundEffect?[] _bells = new SoundEffect?[Carillon.Degrees];

    public GameSounds()
    {
        try
        {
            _chop = CreateChop();
            _place = CreateThud();
            _chime = CreateChime();
        }
        catch (Exception)
        {
            // Headless stroj / bez audio ovladače — hra poběží potichu.
            _chop = null;
            _place = null;
            _chime = null;
        }
    }

    /// <summary>
    /// Naladí zvonohru podle dat. Volá se po načtení obsahu — základní
    /// frekvence i délka tónu jsou obsah, ne konstanta v kódu.
    /// </summary>
    public void TuneCarillon(CarillonConfig config)
    {
        if (!config.IsEnabled)
        {
            return;
        }

        try
        {
            for (int degree = 0; degree < _bells.Length; degree++)
            {
                _bells[degree]?.Dispose();
                _bells[degree] = CreateBell(
                    (float)(config.BaseFrequency * Math.Pow(2, MajorScale[degree] / 12.0)),
                    (float)config.NoteSeconds);
            }
        }
        catch (Exception)
        {
            // Bez zvukového zařízení zvonohra jen mlčí — hra běží dál.
            Array.Clear(_bells);
        }
    }

    /// <summary>Zahraje jeden tón zvonohry (stupeň stupnice). Mimo rozsah = ticho.</summary>
    public void PlayBell(int degree)
    {
        if (degree >= 0 && degree < _bells.Length)
        {
            // Bez náhodného kolísání výšky: melodie musí být pokaždé stejná,
            // jinak by z osmi tónů byla osmkrát jiná písnička.
            _bells[degree]?.Play(0.35f, 0f, 0f);
        }
    }

    /// <summary>Půltóny durové stupnice — osm stupňů včetně horní oktávy.</summary>
    private static readonly int[] MajorScale = { 0, 2, 4, 5, 7, 9, 11, 12 };

    /// <summary>Seknutí při ruční těžbě.</summary>
    public void PlayChop() => Play(_chop, volume: 0.35f);

    /// <summary>Žuchnutí při položení budovy.</summary>
    public void PlayPlace() => Play(_place, volume: 0.5f);

    /// <summary>Příjemné cinknutí — dobrá zpráva (splněný úkol, achievement, Vzestup).</summary>
    public void PlayChime() => Play(_chime, volume: 0.4f);

    public void Dispose()
    {
        _chop?.Dispose();
        _place?.Dispose();
        _chime?.Dispose();
        for (int i = 0; i < _bells.Length; i++)
        {
            _bells[i]?.Dispose();
        }
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

    /// <summary>Zvonkohra: základ + dvě vyšší harmonické s dozvukem — jasné „ding".</summary>
    private static SoundEffect CreateChime()
    {
        int samples = (int)(SampleRate * 0.55);
        var data = new float[samples];
        // Durový akord (C5 + E5 + G5), postupné rozeznění zdola nahoru.
        float[] freqs = { 523.25f, 659.25f, 783.99f };
        float[] delays = { 0f, 0.06f, 0.12f };
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float value = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float td = t - delays[n];
                if (td <= 0f)
                {
                    continue;
                }

                value += MathF.Sin(MathF.Tau * freqs[n] * td) * MathF.Exp(-td * 6.5f);
            }

            data[i] = value / freqs.Length;
        }

        return ToSoundEffect(data);
    }

    /// <summary>
    /// Zvon: základní tón plus nepřesná vyšší harmonická, obojí s dlouhým
    /// dozvukem.
    ///
    /// <para>Ta „nepřesnost" (2,76× místo 3×) je celý rozdíl mezi zvonem a
    /// pípnutím — skutečné zvony mají harmonické mimo celé násobky a právě
    /// z toho pochází ten kovový svit.</para>
    /// </summary>
    private static SoundEffect CreateBell(float frequency, float seconds)
    {
        int samples = (int)(SampleRate * seconds);
        var data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / SampleRate;
            float body = MathF.Sin(MathF.Tau * frequency * t) * MathF.Exp(-t * 5.5f);
            float shine = MathF.Sin(MathF.Tau * frequency * 2.76f * t) * MathF.Exp(-t * 11f) * 0.4f;
            data[i] = (body + shine) * 0.7f;
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
