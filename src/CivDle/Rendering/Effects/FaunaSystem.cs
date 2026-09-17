using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
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

    /// <summary>Strop poolu — vystavený, aby test mohl mluvit o podílu, ne o čísle.</summary>
    public static int MaxActive => MaxCritters;

    /// <summary>
    /// Čtvereček pro druh, který nemá kresbu. Nemá nastat — hlídá to test
    /// pokrytí — ale zvíře, které se <b>nezobrazí</b>, je horší chyba než
    /// zvíře, které vypadá jako dřív.
    /// </summary>
    private const int FallbackSize = 3;

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
    /// <summary>
    /// Vysadí do světa dravce na dané místo. Čistě pro test útěku před šelmou:
    /// čekat, až se lev sám objeví vedle stáda gazel, by byl test o náhodě.
    ///
    /// <para>Vybírá se šelma, která na tom místě <b>vydrží</b> — se správným
    /// biomem a činná v kteroukoli denní dobu. Na prvního dravce ze seznamu
    /// to fungovat nemohlo: byl to vlk, tedy noční druh z tajgy, a na denní
    /// travnaté pláni se vyřadil dřív, než ho stačil kdokoli zahlédnout.</para>
    /// </summary>
    internal bool SpawnPredatorForTests(Simulation simulation, Vector2 position)
    {
        int tileX = (int)MathF.Floor(position.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(position.Y / TerrainRenderer.TileSize);
        byte biome = simulation.BiomeAt(tileX, tileY);

        for (int i = 0; i < _content.Fauna.Count; i++)
        {
            var candidate = _content.Fauna[i];
            if (!candidate.Predator || candidate.Time != FaunaTime.Any || !candidate.BiomeMask[biome])
            {
                continue;
            }

            // Po opravě vypouštění se pool na volné krajině zaplní na strop,
            // takže dravec nemá kam. Uvolní se mu poslední místo — test má
            // zkoumat útěk, ne kapacitu.
            int slot = _count < MaxCritters ? _count++ : _count - 1;

            _critters[slot] = new Critter
            {
                Position = position,
                Anchor = position,
                Velocity = Vector2.Zero,
                DefIndex = i,
                DirectionTimer = GrazeSeconds(),
            };

            return true;
        }

        return false;
    }

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

    /// <summary>
    /// Vykreslí zvěř.
    ///
    /// <para>Zvíře je sprite, ne čtvereček. Dokud byly druhy tři, dal se
    /// barevný bod omluvit; s osmačtyřiceti z toho byla mapa různobarevných
    /// teček, na které medvěd od lišky poznat nejde — a zvíře, které hráč
    /// nepozná, je zážitkově totéž jako zvíře, které tam vůbec není.</para>
    ///
    /// <para>Sprite chybí jen tehdy, když druh v datech nemá kresbu; hlídá to
    /// test pokrytí. I tak se kreslí náhradní čtvereček: zvíře, které se
    /// <b>nezobrazí</b>, je horší chyba než zvíře, které vypadá jako dřív.</para>
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, SpriteLibrary sprites, Texture2D pixel, Camera2D camera)
    {
        if (_count == 0)
        {
            return;
        }

        ResolveSprites(sprites);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            // Světlušky pulzují; ostatní tvorové jsou plní.
            float alpha = def.Glow ? 0.45f + 0.55f * MathF.Abs(MathF.Sin(critter.Phase * 3f)) : 1f;
            var sprite = _spriteByDef![critter.DefIndex];

            if (sprite is null)
            {
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(
                        (int)(critter.Position.X - (FallbackSize * 0.5f)),
                        (int)(critter.Position.Y - (FallbackSize * 0.5f)),
                        FallbackSize,
                        FallbackSize),
                    def.Color.ToXna() * alpha);
                continue;
            }

            // Kotva podle toho, jak je kresba postavená: čtyřnožec stojí
            // nohama na zemi, pták v letu visí středem. Postavit letícího
            // ptáka „na nohy" by ho posunulo o půl těla a letěl by pod sebou.
            var origin = _anchorByDef![critter.DefIndex] == FaunaAnchor.Ground
                ? new Vector2(sprite.Width * 0.5f, sprite.Height)
                : new Vector2(sprite.Width * 0.5f, sprite.Height * 0.5f);

            // Kdo jde doleva, dívá se doleva. Všechny kresby hledí doprava;
            // bez převrácení by se půlka zvěře pohybovala pozpátku.
            var effect = critter.Velocity.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Drobné houpání v kroku. Bez něj zvíře po krajině klouže jako
            // nálepka — je to týž trik, kterým chodí chodci.
            float bob = def.Glow ? 0f : MathF.Abs(MathF.Sin(critter.Phase * 5f)) * 0.8f;

            spriteBatch.Draw(
                sprite,
                new Vector2(critter.Position.X, critter.Position.Y - bob),
                null,
                Color.White * alpha,
                0f,
                origin,
                1f,
                effect,
                0f);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Sprite a kotva pro každý druh, spočítané jednou.
    ///
    /// <para>Hledat je při kreslení by znamenalo skládat ID
    /// (<c>"fauna." + id</c>) pro každého tvora v každém snímku — tedy tisíce
    /// řetězců za sekundu zahozených hned po použití. Tabulka se plní jednou
    /// a pak už se jen indexuje.</para>
    /// </summary>
    private Texture2D?[]? _spriteByDef;
    private FaunaAnchor[]? _anchorByDef;

    private void ResolveSprites(SpriteLibrary sprites)
    {
        if (_spriteByDef is not null)
        {
            return;
        }

        var textures = new Texture2D?[_content.Fauna.Count];
        var anchors = new FaunaAnchor[_content.Fauna.Count];
        for (int i = 0; i < _content.Fauna.Count; i++)
        {
            string id = _content.Fauna[i].Id;
            textures[i] = sprites.Get(FaunaSprites.IdFor(id));
            anchors[i] = FaunaSprites.AnchorFor(id);
        }

        _anchorByDef = anchors;
        _spriteByDef = textures;
    }

    private void UpdateCritters(float dt, Simulation simulation, bool isNight, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var critter = ref _critters[i];
            var def = _content.Fauna[critter.DefIndex];

            // Krok se nejdřív zkusí. Zvíře, které by šláplo na budovu, se
            // odrazí — nezmizí. Tohle bývalo obráceně a byla to ta pravá
            // příčina, proč ve městě zvěř nebyla: zrušila se při prvním
            // došlápnutí na barák, tedy ve městě skoro okamžitě. Přidávat
            // pokusy o vypuštění bylo marné, protože problém nebyl v rození,
            // ale v umírání.
            var step = critter.Position + (critter.Velocity * dt);
            if (IsFreeAt(simulation, step))
            {
                critter.Position = step;
            }
            else
            {
                // Odraz: nový směr a krátké rozmyšlení, ať se tvor nezasekne
                // čumákem ve zdi.
                critter.Velocity = RandomDirection() * def.Speed;
                critter.DirectionTimer = ReturnSeconds();
            }

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
                if (def.Shy && NearestThreat(i, critter.Position, out var threat))
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
            // Zastavěná dlaždice POD zvířetem znamená, že mu hráč postavil
            // dům přímo na hlavu — tam ať zmizí. Ale dům před nosem se řeší
            // odrazem výš, ne zrušením.
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

        if (!TryFindFreeSpot(simulation, min, max, out float x, out float y, out int tileX, out int tileY))
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
            // Každý kus stáda potřebuje volnou zem, ne jen kotva.
            //
            // Stádo se rozprostírá o dvě a půl dlaždice kolem středu. V městě
            // je devět dlaždic z deseti zastavěných, takže skoro celé stádo
            // dopadlo na budovy — a hned v dalším tiku se vyřadilo, protože
            // zvíře na baráku se ruší. Vypadalo to jako „zvěř se nerodí",
            // ale ona se rodila a okamžitě umírala.
            if (!TryScatter(simulation, anchor, out var spot))
            {
                continue;
            }

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

    /// <summary>
    /// Najde ve výřezu volné místo, kam se dá vypustit zvěř.
    ///
    /// <para><b>Proč se zkouší víckrát:</b> dřív se losoval <b>jeden</b> bod za
    /// pokus a když padl na zastavěnou dlaždici, celý pokus propadl. Jenže
    /// hráč se dívá hlavně na svoje město, a tam je většina výřezu zastavěná —
    /// takže právě tam, kde se kouká nejčastěji, se zvěř skoro nerodila.</para>
    ///
    /// <para>Změřeno: na obraze bývalo 1 až 13 kusů proti stropu
    /// <see cref="MaxCritters"/> = 40. Přibývající druhy v datech na tom nic
    /// nezměnily, protože je nebrzdila data, ale tenhle jeden losovaný bod.</para>
    ///
    /// <para>Osm pokusů je kompromis: na volné krajině uspěje první a nic to
    /// nestojí, v hustém městě dá zvěři reálnou šanci. Prohledávat celý výřez
    /// by byl kvůli kulise nesmysl.</para>
    /// </summary>
    private static bool TryFindFreeSpot(
        Simulation simulation,
        Vector2 min,
        Vector2 max,
        out float x,
        out float y,
        out int tileX,
        out int tileY)
    {
        for (int attempt = 0; attempt < SpawnAttempts; attempt++)
        {
            x = min.X + Random.Shared.NextSingle() * (max.X - min.X);
            y = min.Y + Random.Shared.NextSingle() * (max.Y - min.Y);
            tileX = (int)MathF.Floor(x / TerrainRenderer.TileSize);
            tileY = (int)MathF.Floor(y / TerrainRenderer.TileSize);

            if (!simulation.IsOccupied(tileX, tileY))
            {
                return true;
            }
        }

        x = 0f;
        y = 0f;
        tileX = 0;
        tileY = 0;
        return false;
    }

    /// <summary>Kolik míst se za jeden pokus zkusí, než to systém vzdá.</summary>
    private const int SpawnAttempts = 8;

    /// <summary>
    /// Najde místo pro jeden kus stáda kolem jeho středu — na volné zemi.
    ///
    /// <para>Kdo se nevejde, prostě nepřijde: v sevřeném městě se tak pase
    /// pár kusů po dvorcích místo celého stáda, které by se stejně hned
    /// vyřadilo. Na volné krajině se první pokus trefí a nic to nestojí.</para>
    /// </summary>
    private static bool TryScatter(Simulation simulation, Vector2 anchor, out Vector2 spot)
    {
        for (int attempt = 0; attempt < ScatterAttempts; attempt++)
        {
            spot = anchor + new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * HerdSpread,
                (Random.Shared.NextSingle() - 0.5f) * HerdSpread);

            int tileX = (int)MathF.Floor(spot.X / TerrainRenderer.TileSize);
            int tileY = (int)MathF.Floor(spot.Y / TerrainRenderer.TileSize);
            if (!simulation.IsOccupied(tileX, tileY))
            {
                return true;
            }
        }

        spot = Vector2.Zero;
        return false;
    }

    /// <summary>Kolik míst se zkusí pro jeden kus stáda.</summary>
    private const int ScatterAttempts = 6;

    /// <summary>Je na tomhle světovém bodě volná zem, kam se dá šlápnout?</summary>
    private static bool IsFreeAt(Simulation simulation, Vector2 position) =>
        !simulation.IsOccupied(
            (int)MathF.Floor(position.X / TerrainRenderer.TileSize),
            (int)MathF.Floor(position.Y / TerrainRenderer.TileSize));

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
    /// Co plaché zvíře zrovna vyplašilo — člověk, nebo dravec?
    ///
    /// <para><b>Proč i dravec:</b> v datech byl vlk, medvěd i lev, ale zvěř
    /// o nich nevěděla — gazela se pásla lvovi pod nosem. Stádo, které se dá
    /// na útěk před šelmou, udělá ze savany ekosystém místo zoo, a nestojí to
    /// nic: nikdo nikoho nechytá, jde jen o <b>reakci</b>.</para>
    ///
    /// <para>Dravec vlastní druh nevyplaší. Liška je zároveň plachá (před
    /// člověkem) i dravá (pro králíky) — což si neodporuje, jen se to týká
    /// někoho jiného. Kdyby se druh lekal sám sebe, rozprchla by se smečka
    /// vlků při prvním kroku.</para>
    /// </summary>
    private bool NearestThreat(int index, Vector2 from, out Vector2 threat)
    {
        if (NearestPersonWithin(from, FlightRadius, out threat))
        {
            return true;
        }

        int ownDef = _critters[index].DefIndex;
        float bestSquared = PredatorRadius * PredatorRadius;
        bool found = false;

        for (int i = 0; i < _count; i++)
        {
            if (i == index || _critters[i].DefIndex == ownDef)
            {
                continue;
            }

            if (!_content.Fauna[_critters[i].DefIndex].Predator)
            {
                continue;
            }

            float distanceSquared = Vector2.DistanceSquared(_critters[i].Position, from);
            if (distanceSquared < bestSquared)
            {
                bestSquared = distanceSquared;
                threat = _critters[i].Position;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Na jakou dálku zvěř zaregistruje šelmu. Kratší než u člověka: člověk
    /// se po krajině pohybuje hlučně a nápadně, šelma se plíží.
    /// </summary>
    private const float PredatorRadius = TerrainRenderer.TileSize * 3.5f;

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
