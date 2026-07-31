using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Vizuální odezva na to, co dělá simulace sama: jiskry z budov, které právě
/// něco vyrobily, a krátké „naskočení" nově postavených budov, sloučených bloků
/// a položených silnic.
///
/// <para>Vzniklo z toho, že VŠECHNY efekty ve hře visely na kliknutí. Hra tedy
/// odměňovala klikání a mlčela přesně ve chvíli, kdy dělala to, co idle hra
/// slibuje — pracovala sama. Tohle je ta chybějící půlka.</para>
///
/// <para>Vrstva renderu: čte frontu událostí ze simulace, nikdy do ní nepíše.
/// Pool má pevnou velikost a nejstarší efekt se přepíše, takže při velkém městě
/// nic neroste a nic se nealokuje.</para>
/// </summary>
public sealed class CityPulseRenderer
{
    private const int MaxSparks = 320;
    private const int MaxPops = 96;
    private const float SparkLife = 1.05f;
    private const float PopLife = 0.5f;

    /// <summary>Kolik jisker vyletí z jednoho dokončeného cyklu.</summary>
    private const int SparksPerCycle = 3;

    /// <summary>Jak vysoko jiskra vystoupá, než ji stáhne tíže (world pixely).</summary>
    private const float SparkRise = 34f;

    /// <summary>Zrychlení, které jiskru stahuje zpátky — z rovné čáry dělá oblouk.</summary>
    private const float SparkGravity = 46f;

    /// <summary>Jiskra letící od budovy vzhůru k hornímu okraji (směr k pruhu surovin).</summary>
    private struct Spark
    {
        public Vector2 Origin;
        public float Age;
        public float Drift;
        public float Rise;
        public float Spin;
        public Color Color;
    }

    /// <summary>Krátké zvětšení a záblesk na dlaždici, kde něco vzniklo.</summary>
    private struct Pop
    {
        public Vector2 Center;
        public float Age;
        public float Size;
        public int Debris;
        public float Seed;
        public Color Color;
    }

    private readonly GameContent _content;
    private readonly Texture2D _pixel;
    private readonly Spark[] _sparks = new Spark[MaxSparks];
    private readonly Pop[] _pops = new Pop[MaxPops];
    private int _sparkCount;
    private int _popCount;

    public CityPulseRenderer(Texture2D pixel, GameContent content)
    {
        _pixel = pixel;
        _content = content;
    }

    /// <summary>Vypnuto přístupnostní volbou „omezit pohyb".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Vyzvedne události ze simulace a posune běžící efekty. Frontu vždycky
    /// vyprázdní — i když jsou efekty vypnuté, jinak by se hromadily.
    /// </summary>
    public void Update(float dt, Simulation simulation)
    {
        var queue = simulation.VisualEvents;
        for (int i = 0; i < queue.Count && Enabled; i++)
        {
            Spawn(queue[i]);
        }

        queue.Clear();

        Advance(dt);
    }

    private void Spawn(in VisualEvent visualEvent)
    {
        int tileSize = TerrainRenderer.TileSize;
        var center = new Vector2((visualEvent.X + 0.5f) * tileSize, (visualEvent.Y + 0.5f) * tileSize);

        switch (visualEvent.Kind)
        {
            case VisualEventKind.Produced:
                // Hrst jisker místo jedné: jedna tečka vypadá jako chyba
                // vykreslení, tři vypadají jako že se něco povedlo.
                var color = ResourceColor(visualEvent.ResourceIndex);
                for (int i = 0; i < SparksPerCycle; i++)
                {
                    AddSpark(center, color);
                }

                break;

            case VisualEventKind.BuildingPlaced:
                AddPop(center, tileSize * 1.3f, new Color(235, 226, 200), debris: 7);
                break;

            case VisualEventKind.BuildingUpgraded:
                AddPop(center, tileSize * 1.5f, new Color(150, 220, 255), debris: 5);
                for (int i = 0; i < 4; i++)
                {
                    AddSpark(center, new Color(180, 235, 255)); // vylepšení stoupá vzhůru
                }

                break;

            case VisualEventKind.BuildingMerged:
                // Sloučení zabírá 2×2, takže i záblesk musí být přes celý blok.
                var blockCenter = center + new Vector2(tileSize * 0.5f, tileSize * 0.5f);
                AddPop(blockCenter, tileSize * 2.6f, new Color(255, 214, 120), debris: 12);
                for (int i = 0; i < 8; i++)
                {
                    AddSpark(blockCenter, new Color(255, 226, 150));
                }

                break;

            case VisualEventKind.RoadBuilt:
                // Silnice si nezaslouží prstenec — jen obláček prachu pod nohama.
                AddPop(center, tileSize * 0.55f, new Color(215, 210, 195), debris: 4);
                break;

            case VisualEventKind.MilestoneReached:
                // Pulz celé civilizace: široký prstenec z těžiště města. Doprovází
                // ohňostroj — ten hraje nahoře, tohle na zemi, ať je vidět, že se
                // to týká města, ne oblohy.
                AddPop(center, tileSize * 14f, new Color(255, 226, 150), debris: 0);
                for (int i = 0; i < 14; i++)
                {
                    AddSpark(center, new Color(255, 214, 120));
                }

                break;
        }
    }

    private Color ResourceColor(int resourceIndex) =>
        resourceIndex >= 0 && resourceIndex < _content.Resources.Count
            ? _content.Resources[resourceIndex].MapColor.ToXna()
            : Color.White;

    private void AddSpark(Vector2 origin, Color color)
    {
        if (_sparkCount == MaxSparks)
        {
            _sparkCount--; // pool plný → nejstarší ustoupí, nová jiskra je zajímavější
        }

        _sparks[_sparkCount++] = new Spark
        {
            Origin = origin,
            Age = 0f,
            Drift = (Random.Shared.NextSingle() - 0.5f) * 34f,
            Rise = SparkRise * (0.75f + Random.Shared.NextSingle() * 0.6f),
            Spin = Random.Shared.NextSingle() * MathF.Tau,
            Color = color,
        };
    }

    private void AddPop(Vector2 center, float size, Color color, int debris)
    {
        if (_popCount == MaxPops)
        {
            _popCount--;
        }

        _pops[_popCount++] = new Pop
        {
            Center = center,
            Age = 0f,
            Size = size,
            Debris = debris,
            Seed = Random.Shared.NextSingle() * MathF.Tau,
            Color = color,
        };
    }

    private void Advance(float dt)
    {
        for (int i = _sparkCount - 1; i >= 0; i--)
        {
            _sparks[i].Age += dt;
            if (_sparks[i].Age >= SparkLife)
            {
                _sparks[i] = _sparks[--_sparkCount];
            }
        }

        for (int i = _popCount - 1; i >= 0; i--)
        {
            _pops[i].Age += dt;
            if (_pops[i].Age >= PopLife)
            {
                _pops[i] = _pops[--_popCount];
            }
        }
    }

    /// <summary>Kreslí ve world souřadnicích — volající si otevře dávku s kamerou.</summary>
    public void Draw(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < _sparkCount; i++)
        {
            ref readonly var spark = ref _sparks[i];
            float t = spark.Age / SparkLife;

            // Balistický oblouk místo rovné čáry: jiskra vyletí, zpomalí a začne
            // padat. Rovnoměrný pohyb vypadá mechanicky, oblouk má váhu.
            float height = spark.Rise * t - SparkGravity * t * t * 0.5f;
            var position = spark.Origin + new Vector2(spark.Drift * t, -height);

            // Vyskočí do plné velikosti a pak se scvrkne — „lupnutí" na začátku
            // je to, co dělá efekt uspokojivým.
            float pop = t < 0.12f ? t / 0.12f : 1f;
            float shrink = 1f - MathF.Max(0f, (t - 0.5f) / 0.5f) * 0.75f;
            float size = 5f * pop * shrink;
            float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;

            // Měkké halo pod ostrým jádrem — dva obdélníky, žádná nová textura.
            float halo = size * 2.1f;
            spriteBatch.Draw(_pixel,
                new Rectangle((int)(position.X - halo / 2f), (int)(position.Y - halo / 2f), (int)halo, (int)halo),
                spark.Color * (alpha * 0.22f));

            // Jemné pulzování jádra, ať hrst jisker nevypadá jako jedna věc.
            float core = size * (0.85f + MathF.Sin(spark.Spin + spark.Age * 18f) * 0.15f);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)(position.X - core / 2f), (int)(position.Y - core / 2f),
                    Math.Max(1, (int)core), Math.Max(1, (int)core)),
                Color.Lerp(spark.Color, Color.White, 0.35f) * alpha);
        }

        for (int i = 0; i < _popCount; i++)
        {
            ref readonly var pop = ref _pops[i];
            float t = pop.Age / PopLife;
            float ease = 1f - (1f - t) * (1f - t); // rychle ven, pak dojezd
            float alpha = 1f - t;

            // Bílý záblesk v prvním okamžiku — oko ho zaregistruje dřív než tvar
            // a dá to události „náraz".
            if (t < 0.22f)
            {
                float flash = pop.Size * 0.55f * (1f - t / 0.22f);
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)(pop.Center.X - flash / 2f), (int)(pop.Center.Y - flash / 2f),
                        (int)flash, (int)flash),
                    Color.White * (0.6f * (1f - t / 0.22f)));
            }

            float size = pop.Size * (0.35f + ease * 0.95f);
            DrawRing(spriteBatch, pop.Center, size * 0.5f, pop.Color * alpha);

            // Prach: pár úlomků do stran, které padají — ring sám o sobě je
            // moc čistý na to, aby vypadal jako stavba.
            for (int d = 0; d < pop.Debris; d++)
            {
                float angle = pop.Seed + d * MathF.Tau / pop.Debris;
                float distance = pop.Size * 0.35f * ease;
                float fall = pop.Size * 0.30f * t * t;
                var dust = pop.Center + new Vector2(MathF.Cos(angle) * distance, MathF.Sin(angle) * distance * 0.55f + fall);
                float dustSize = Math.Max(1f, 3f * (1f - t));
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)dust.X, (int)dust.Y, (int)dustSize, (int)dustSize),
                    pop.Color * (alpha * 0.8f));
            }
        }
    }

    /// <summary>Prstenec ze čtyř obdélníků — levnější než kruh a v pixel-artu vypadá líp.</summary>
    private void DrawRing(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        int thickness = 2;
        int size = (int)(radius * 2f);
        int left = (int)(center.X - radius);
        int top = (int)(center.Y - radius);

        spriteBatch.Draw(_pixel, new Rectangle(left, top, size, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(left, top + size - thickness, size, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(left, top, thickness, size), color);
        spriteBatch.Draw(_pixel, new Rectangle(left + size - thickness, top, thickness, size), color);
    }
}
