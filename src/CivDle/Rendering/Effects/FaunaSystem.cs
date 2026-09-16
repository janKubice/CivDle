using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Ambientní fauna (living-map.md): tvorové existují JEN u kamery — render je
/// spawnuje v viditelném výřezu, hýbe jimi a mimo obraz je ruší (LOD). Kulisa,
/// ne simulace: náhoda tady nevadí, determinismus světa se jí nedotýká.
/// Pevný pool bez alokací za běhu; denní/noční druhy podle času simulace.
/// </summary>
public sealed class FaunaSystem
{
    /// <summary>
    /// Strop počtu tvorů. Zvednutý z osmnácti: jakmile chodí zvířata ve
    /// stádech, spolklo by jedno stádo srnců skoro celý původní strop a na
    /// zbytek krajiny by nezbylo nic.
    /// </summary>
    private const int MaxCritters = 40;

    /// <summary>Na jakou vzdálenost plaché zvíře zaregistruje člověka (world pixely).</summary>
    private const float FlightRadius = TerrainRenderer.TileSize * 5f;

    /// <summary>Kolikrát rychleji zvíře utíká, než se pase.</summary>
    private const float FlightSpeedup = 2.6f;

    /// <summary>Jak dlouho po vyplašení ještě běží.</summary>
    private const float FlightSeconds = 2.2f;
    private const float MinZoom = 0.55f;
    private const float SpawnCooldownSeconds = 0.5f;
    private const float DespawnMargin = 96f;

    private struct Critter
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public int DefIndex;
        public float DirectionTimer;
        public float Phase;

        /// <summary>
        /// Kolik sekund ještě utíká. Dokud je kladné, drží zvíře směr od toho,
        /// kdo ho vyplašil, a běží rychleji.
        /// </summary>
        public float FleeSeconds;

        /// <summary>
        /// Kolem kterého bodu se pase. Zvířata se dřív toulala každé po svém
        /// náhodným směrem — ze stáda by tak během půl minuty byly rozprchlé
        /// tečky. Střed drží skupinu pohromadě, aniž by se musela počítat
        /// vzájemná přitažlivost.
        /// </summary>
        public Vector2 Anchor;
    }

    private readonly GameContent _content;
    private readonly Critter[] _critters = new Critter[MaxCritters];
    private readonly List<int> _eligibleDefs = new();
    private int _count;
    private float _spawnTimer;

    public FaunaSystem(GameContent content)
    {
        _content = content;
    }

    /// <summary>Kolik tvorů je na scéně. Pro testy stád.</summary>
    internal int CountForTests => _count;

    /// <summary>
    /// Kolik zvířat zrovna utíká. Přímá odpověď na „vyplašilo je něco?" —
    /// počítat sousedy kolem bodu je na tuhle otázku okliku, protože zvířata
    /// se kolem něj beztak procházejí sem a tam.
    /// </summary>
    internal int FleeingForTests
    {
        get
        {
            int fleeing = 0;
            for (int i = 0; i < _count; i++)
            {
                if (_critters[i].FleeSeconds > 0f)
                {
                    fleeing++;
                }
            }

            return fleeing;
        }
    }

    /// <summary>
    /// Kde tvorové jsou a jestli jsou plaší. Test útěku musí člověka postavit
    /// k <b>plachému</b> druhu — jinak měří jen to, který tvor se zrovna objevil
    /// první, a s každým novým neplachým zvířetem v datech začne být vrtkavý.
    /// </summary>
    internal IEnumerable<(Vector2 Position, bool Shy)> CrittersForTests
    {
        get
        {
            for (int i = 0; i < _count; i++)
            {
                yield return (_critters[i].Position, _content.Fauna[_critters[i].DefIndex].Shy);
            }
        }
    }

    /// <summary>Kde tvorové zrovna jsou. Pro testy, které měří útěk.</summary>
    internal IEnumerable<Vector2> PositionsForTests
    {
        get
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _critters[i].Position;
            }
        }
    }

    /// <summary>
    /// Kde tvor je a ke kterému stádu patří (střed, kolem něhož se pase).
    /// Pro test soudržnosti: v obraze je stád víc naráz, takže měřit rozptyl
    /// přes všechna zvířata dohromady by neměřilo nic.
    /// </summary>
    internal IEnumerable<(Vector2 Position, Vector2 Anchor)> HerdsForTests
    {
        get
        {
            for (int i = 0; i < _count; i++)
            {
                yield return (_critters[i].Position, _critters[i].Anchor);
            }
        }
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < DetailLevel.Scale(MinZoom) || _content.Fauna.Count == 0)
        {
            _count = 0; // oddáleno → fauna zmizí (z dálky ji nikdo nevidí)
            return;
        }

        bool isNight = DayNightCycle.NightFactor(simulation.TimeOfDay01) > 0.5f;
        var (min, max) = camera.VisibleWorldBounds();

        UpdateCritters(dt, simulation, isNight, min, max);
        TrySpawn(dt, simulation, isNight, min, max);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            // Světlušky pulzují; ostatní tvorové jsou plné tečky.
            float alpha = def.Glow ? 0.45f + 0.55f * MathF.Abs(MathF.Sin(critter.Phase * 3f)) : 1f;
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    (int)(critter.Position.X - def.Size * 0.5f),
                    (int)(critter.Position.Y - def.Size * 0.5f),
                    def.Size,
                    def.Size),
                def.Color.ToXna() * alpha);
        }

        spriteBatch.End();
    }

    private void UpdateCritters(float dt, Simulation simulation, bool isNight, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            critter.Position += critter.Velocity * dt;
            critter.Phase += dt;
            critter.DirectionTimer -= dt;

            if (critter.FleeSeconds > 0f)
            {
                // Útěk: směr se nepřehazuje, dokud zvíře neuklidní. Kdyby si
                // v běhu losovalo nový, běželo by do kruhu a vypadalo by to
                // spíš vyděšeně než plaše.
                critter.FleeSeconds -= dt;
                if (critter.FleeSeconds <= 0f)
                {
                    // Střed stáda se po útěku NEPŘEPISUJE. Kdyby se každé zvíře
                    // zakotvilo tam, kam doběhlo, rozpadla by se skupina při
                    // prvním kolemjdoucím natrvalo; takhle se rozprchne a zase
                    // se sejde, což je přesně to, co dělá stádo.
                    critter.Velocity = RandomDirection() * def.Speed;
                    critter.DirectionTimer = GrazeSeconds();
                }
            }
            else
            {
                if (def.Shy && NearestPersonWithin(critter.Position, FlightRadius, out var threat))
                {
                    var away = critter.Position - threat;
                    if (away.LengthSquared() < 0.01f)
                    {
                        away = RandomDirection();
                    }

                    away.Normalize();
                    critter.Velocity = away * def.Speed * FlightSpeedup;
                    critter.FleeSeconds = FlightSeconds;
                }
                else if (critter.DirectionTimer <= 0f)
                {
                    // Pastva: krok se losuje, ale zpátky ke středu stáda —
                    // jinak by se skupina během půl minuty rozprchla na tečky.
                    var home = critter.Anchor - critter.Position;
                    bool strayed = home.LengthSquared() > HerdLeashSquared;

                    var direction = strayed
                        ? Vector2.Normalize(home)
                        : Vector2.Normalize(RandomDirection() + (home * HerdPull));

                    critter.Velocity = direction * def.Speed;

                    // Kdo se zatoulal, rozmýšlí se častěji. S běžným intervalem
                    // ušel i na cestě zpátky další kus jiným směrem a stádo se
                    // pomalu roztahovalo, dokud z něj nebyly rozseté tečky.
                    critter.DirectionTimer = strayed ? ReturnSeconds() : GrazeSeconds();
                }
            }

            int tileX = (int)MathF.Floor(critter.Position.X / TerrainRenderer.TileSize);
            int tileY = (int)MathF.Floor(critter.Position.Y / TerrainRenderer.TileSize);
            bool outOfView = critter.Position.X < min.X - DespawnMargin || critter.Position.X > max.X + DespawnMargin
                || critter.Position.Y < min.Y - DespawnMargin || critter.Position.Y > max.Y + DespawnMargin;
            bool wrongTime = def.Time == FaunaTime.Day && isNight || def.Time == FaunaTime.Night && !isNight;
            bool badTile = !def.BiomeMask[simulation.BiomeAt(tileX, tileY)]
                || simulation.IsOccupied(tileX, tileY);

            if (outOfView || wrongTime || badTile)
            {
                _critters[i] = _critters[--_count];
            }
        }
    }

    private void TrySpawn(float dt, Simulation simulation, bool isNight, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f || _count >= MaxCritters)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        float x = min.X + Random.Shared.NextSingle() * (max.X - min.X);
        float y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
        int tileX = (int)MathF.Floor(x / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(y / TerrainRenderer.TileSize);
        if (simulation.IsOccupied(tileX, tileY))
        {
            return;
        }

        byte biome = simulation.BiomeAt(tileX, tileY);
        _eligibleDefs.Clear();
        for (int i = 0; i < _content.Fauna.Count; i++)
        {
            var def = _content.Fauna[i];
            bool timeOk = def.Time == FaunaTime.Any || (def.Time == FaunaTime.Night) == isNight;
            if (timeOk && def.BiomeMask[biome])
            {
                _eligibleDefs.Add(i);
            }
        }

        if (_eligibleDefs.Count == 0)
        {
            return;
        }

        int defIndex = _eligibleDefs[Random.Shared.Next(_eligibleDefs.Count)];
        var chosen = _content.Fauna[defIndex];

        // Stádo naráz, ne po jednom. Srnec sám uprostřed pláně je tečka;
        // skupina, která se táhne přes louku, je výjev. Kolik jich chodí
        // pohromadě, je v datech u druhu — je to vlastnost zvířete, ne algoritmu.
        var anchor = new Vector2(x, y);
        int herd = Math.Min(chosen.Herd, MaxCritters - _count);

        for (int i = 0; i < herd; i++)
        {
            var spot = anchor + new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * HerdSpread,
                (Random.Shared.NextSingle() - 0.5f) * HerdSpread);

            _critters[_count++] = new Critter
            {
                Position = spot,
                Anchor = anchor,
                Velocity = RandomDirection() * chosen.Speed,
                DefIndex = defIndex,
                DirectionTimer = GrazeSeconds(),
                Phase = Random.Shared.NextSingle() * 10f,
            };
        }
    }

    /// <summary>Jak daleko od sebe se stádo při příchodu rozprostře.</summary>
    private const float HerdSpread = TerrainRenderer.TileSize * 2.5f;

    /// <summary>Za jakou vzdálenost od středu už se zvíře vrací ke stádu.</summary>
    private const float HerdLeashSquared =
        (TerrainRenderer.TileSize * 3f) * (TerrainRenderer.TileSize * 3f);

    /// <summary>
    /// Jak silně táhne střed stáda do jinak náhodného kroku.
    ///
    /// <para>Slabý tah nestačí: s jemným popostrkováním se skupina během
    /// minuty roztáhla přes půl obrazovky, protože náhodný směr ho pokaždé
    /// přebil. Tady už se o střed opravdu opírá.</para>
    /// </summary>
    private const float HerdPull = 0.03f;

    /// <summary>Jak dlouho zvíře drží jeden směr, než se zase rozhodne.</summary>
    private static float GrazeSeconds() => 1.5f + (Random.Shared.NextSingle() * 3f);

    /// <summary>Jak dlouho drží směr zvíře na cestě zpátky ke stádu — kratčeji.</summary>
    private static float ReturnSeconds() => 0.5f + (Random.Shared.NextSingle() * 0.8f);

    /// <summary>
    /// Je poblíž člověk? Plachá zvířata z toho dělají to jediné, co na ambientní
    /// fauně opravdu vypadá živě — reakci.
    ///
    /// <para>Lidi sem hlásí <see cref="AgentSystem"/> přes <see cref="People"/>;
    /// fauna o něm neví a nesahá do něj, takže spolu ty dva systémy nejsou
    /// svázané.</para>
    /// </summary>
    private bool NearestPersonWithin(Vector2 from, float radius, out Vector2 person)
    {
        person = Vector2.Zero;
        if (People is null)
        {
            return false;
        }

        float bestSquared = radius * radius;
        bool found = false;

        foreach (var candidate in People)
        {
            float distanceSquared = Vector2.DistanceSquared(candidate, from);
            if (distanceSquared < bestSquared)
            {
                bestSquared = distanceSquared;
                person = candidate;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Kde zrovna stojí lidé. Plachá zvířata před nimi utíkají; <c>null</c>
    /// znamená „nikdo tu není" a fauna se pak chová jako dřív.
    /// </summary>
    public IReadOnlyList<Vector2>? People { get; set; }

    private static Vector2 RandomDirection()
    {
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }
}
