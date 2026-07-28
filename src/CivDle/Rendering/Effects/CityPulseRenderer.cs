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
    private const int MaxSparks = 160;
    private const int MaxPops = 96;
    private const float SparkLife = 0.85f;
    private const float PopLife = 0.35f;

    /// <summary>Jiskra letící od budovy vzhůru k hornímu okraji (směr k pruhu surovin).</summary>
    private struct Spark
    {
        public Vector2 Origin;
        public float Age;
        public float Drift;
        public Color Color;
    }

    /// <summary>Krátké zvětšení a záblesk na dlaždici, kde něco vzniklo.</summary>
    private struct Pop
    {
        public Vector2 Center;
        public float Age;
        public float Size;
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
                AddSpark(center, ResourceColor(visualEvent.ResourceIndex));
                break;

            case VisualEventKind.BuildingPlaced:
                AddPop(center, tileSize * 1.3f, new Color(235, 226, 200));
                break;

            case VisualEventKind.BuildingUpgraded:
                AddPop(center, tileSize * 1.5f, new Color(150, 220, 255));
                break;

            case VisualEventKind.BuildingMerged:
                // Sloučení zabírá 2×2, takže i záblesk musí být přes celý blok.
                AddPop(center + new Vector2(tileSize * 0.5f, tileSize * 0.5f), tileSize * 2.6f,
                    new Color(255, 214, 120));
                break;

            case VisualEventKind.RoadBuilt:
                AddPop(center, tileSize * 0.9f, new Color(215, 210, 195));
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
            Drift = (Random.Shared.NextSingle() - 0.5f) * 14f,
            Color = color,
        };
    }

    private void AddPop(Vector2 center, float size, Color color)
    {
        if (_popCount == MaxPops)
        {
            _popCount--;
        }

        _pops[_popCount++] = new Pop { Center = center, Age = 0f, Size = size, Color = color };
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

            // Stoupá a slábne; mírný boční drift, aby jiskry z jedné budovy
            // netvořily jednu čáru.
            var position = spark.Origin + new Vector2(spark.Drift * t, -26f * t);
            float size = 4f * (1f - t * 0.5f);
            spriteBatch.Draw(_pixel,
                new Rectangle((int)(position.X - size / 2f), (int)(position.Y - size / 2f), (int)size, (int)size),
                spark.Color * (1f - t));
        }

        for (int i = 0; i < _popCount; i++)
        {
            ref readonly var pop = ref _pops[i];
            float t = pop.Age / PopLife;

            // Prstenec se rozpíná a mizí — čitelné i přes barevný terén.
            float size = pop.Size * (0.4f + t * 0.9f);
            float alpha = 1f - t;
            DrawRing(spriteBatch, pop.Center, size * 0.5f, pop.Color * alpha);
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
