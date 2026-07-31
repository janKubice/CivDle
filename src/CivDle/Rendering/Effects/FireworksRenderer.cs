using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Ohňostroj nad městem: světlice vystoupá, praskne a rozsype se do jisker.
///
/// <para>Proč to ve hře je: milníky se dosud hlásily nápisem a toastem —
/// text, který zmizí, a nic víc. Ohňostroj je ten okamžik, kdy se hráč
/// <b>zastaví a kouká</b>: je nad městem, které postavil, trvá pár vteřin
/// a nic po něm nezbyde. Přesně tak má vypadat odměna za dlouhou práci
/// (game-feel-wow.md).</para>
///
/// <para>Vrstva renderu: čte, že simulace ohlásila milník, a nic jí nevrací.
/// Kreslí se ve world-space nad budovami — ohňostroj patří nad město, ne
/// do rohu obrazovky.</para>
///
/// <para>Výkon: dva pevné pooly (světlice a jiskry) bez alokací; když je plno,
/// další salva se prostě nespustí. I deset milníků naráz proto stojí stejně
/// jako jeden.</para>
/// </summary>
public sealed class FireworksRenderer
{
    private const int MaxShells = 12;
    private const int MaxSparks = 420;

    /// <summary>Kolik světlic vyletí za jednu oslavu.</summary>
    private const int ShellsPerBurst = 5;

    /// <summary>Kolik jisker se rozletí z jedné světlice.</summary>
    private const int SparksPerShell = 26;

    private const float ShellRiseSeconds = 0.9f;
    private const float SparkLifeSeconds = 1.5f;

    /// <summary>Jak vysoko nad město světlice vystoupá (world pixely).</summary>
    private const float RiseHeight = 190f;

    /// <summary>Jak daleko od těžiště města salva zabírá (world pixely).</summary>
    private const float SpreadRadius = 260f;

    /// <summary>Rychlost rozletu jisker (world pixely za sekundu).</summary>
    private const float SparkSpeed = 120f;

    /// <summary>Tíže, která jiskry stáhne dolů — bez ní by výbuch byl placka.</summary>
    private const float SparkGravity = 90f;

    private struct Shell
    {
        public Vector2 Origin;
        public float Age;
        public float Delay;
        public Color Color;
    }

    private struct Spark
    {
        public Vector2 Origin;
        public Vector2 Velocity;
        public float Age;
        public Color Color;
    }

    /// <summary>Paleta salv. Pevná, ne náhodná — ohňostroj má vypadat naaranžovaně.</summary>
    private static readonly Color[] Palette =
    {
        new(255, 214, 110),
        new(255, 128, 140),
        new(150, 210, 255),
        new(190, 255, 170),
        new(220, 160, 255),
    };

    private readonly Shell[] _shells = new Shell[MaxShells];
    private readonly Spark[] _sparks = new Spark[MaxSparks];
    private int _shellCount;
    private int _sparkCount;

    /// <summary>Vypnuto přístupnostní volbou „omezit pohyb" (stejně jako oslava).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Lítá zrovna něco? (Pro testy a pro ztlumení jiných efektů.)</summary>
    public bool IsPlaying => _shellCount > 0 || _sparkCount > 0;

    /// <summary>Kolik světlic je právě ve vzduchu.</summary>
    public int ShellCount => _shellCount;

    /// <summary>Kolik jisker právě hoří.</summary>
    public int SparkCount => _sparkCount;

    /// <summary>
    /// Odpálí salvu nad daným místem (obvykle těžiště města).
    ///
    /// <para>Světlice startují se zpožděním, ne naráz — salva, která praskne
    /// v jednom okamžiku, vypadá jako chyba, ne jako oslava.</para>
    /// </summary>
    public void Burst(Vector2 worldCenter, int seed)
    {
        if (!Enabled)
        {
            return;
        }

        var rng = new Random(seed);
        for (int i = 0; i < ShellsPerBurst && _shellCount < MaxShells; i++)
        {
            float angle = rng.NextSingle() * MathF.Tau;
            float distance = rng.NextSingle() * SpreadRadius;

            _shells[_shellCount++] = new Shell
            {
                Origin = worldCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.5f) * distance,
                Age = 0f,
                Delay = i * 0.18f + rng.NextSingle() * 0.12f,
                Color = Palette[rng.Next(Palette.Length)],
            };
        }
    }

    public void Update(float dt)
    {
        for (int i = _shellCount - 1; i >= 0; i--)
        {
            ref var shell = ref _shells[i];
            if (shell.Delay > 0f)
            {
                shell.Delay -= dt;
                continue;
            }

            shell.Age += dt;
            if (shell.Age >= ShellRiseSeconds)
            {
                Explode(shell);
                _shells[i] = _shells[--_shellCount];
            }
        }

        for (int i = _sparkCount - 1; i >= 0; i--)
        {
            _sparks[i].Age += dt;
            if (_sparks[i].Age >= SparkLifeSeconds)
            {
                _sparks[i] = _sparks[--_sparkCount];
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        if (!IsPlaying)
        {
            return;
        }

        // Aditivní míchání: jiskry mají svítit, ne překrývat město barevnými čtverci.
        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        for (int i = 0; i < _shellCount; i++)
        {
            ref readonly var shell = ref _shells[i];
            if (shell.Delay > 0f)
            {
                continue;
            }

            float t = shell.Age / ShellRiseSeconds;
            var position = shell.Origin - new Vector2(0f, RiseHeight * EaseOut(t));

            // Ohon za stoupající světlicí — bez něj by to byl jen poskakující bod.
            spriteBatch.Draw(pixel, new Rectangle((int)position.X, (int)position.Y, 2, 6), shell.Color * 0.75f);
        }

        for (int i = 0; i < _sparkCount; i++)
        {
            ref readonly var spark = ref _sparks[i];
            float life = spark.Age / SparkLifeSeconds;
            var position = spark.Origin
                + spark.Velocity * spark.Age
                + new Vector2(0f, 0.5f * SparkGravity * spark.Age * spark.Age);

            // Dohasínání do ztracena; ke konci jiskra i zdrobní.
            float fade = 1f - life * life;
            int size = life < 0.6f ? 2 : 1;
            spriteBatch.Draw(
                pixel, new Rectangle((int)position.X, (int)position.Y, size, size), spark.Color * fade);
        }

        spriteBatch.End();
    }

    /// <summary>Zhasne všechno naráz — po Vzestupu se starý svět nemá dosvítit nad novým.</summary>
    public void Clear()
    {
        _shellCount = 0;
        _sparkCount = 0;
    }

    private void Explode(in Shell shell)
    {
        var center = shell.Origin - new Vector2(0f, RiseHeight);
        var rng = new Random(shell.Color.PackedValue.GetHashCode() ^ (int)(center.X * 31 + center.Y));

        for (int i = 0; i < SparksPerShell && _sparkCount < MaxSparks; i++)
        {
            // Rovnoměrně po kruhu s drobným rozhozem — pravidelná hvězdice
            // vypadá uměle, čirá náhoda zas jako chuchvalec.
            float angle = MathF.Tau * i / SparksPerShell + rng.NextSingle() * 0.25f;
            float speed = SparkSpeed * (0.55f + rng.NextSingle() * 0.65f);

            _sparks[_sparkCount++] = new Spark
            {
                Origin = center,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.6f) * speed,
                Age = 0f,
                Color = shell.Color,
            };
        }
    }

    /// <summary>Stoupání zpomaluje k vrcholu — světlice má na okamžik viset.</summary>
    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
