using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Živý svět (bod „chci vidět lidičky chodit / vozidla"): chodci a vozíky se
/// pohybují kolem budov a po cestách. Čistě vizuální kulisa — existují jen
/// u kamery (LOD), spawnují se poblíž zástavby, mimo pohled se ruší. Simulace
/// o nich neví (render do ní nezapisuje). Počet škáluje s počtem viditelných
/// budov, ať rušné město opravdu žije.
/// </summary>
/// <summary>
/// Meze systému chodců. Vytažené ven, aby si na ně mohl sáhnout test — strop
/// poolu je věc, kterou má smysl ověřovat, ne opisovat.
/// </summary>
public static class AgentSystemLimits
{
    /// <summary>
    /// Strop počtu agentů na scéně. Zvednutý proti původním 48: velkoměsto
    /// vypadalo stejně živě jako vesnice o dvanácti domech.
    /// </summary>
    public const int MaxAgents = 96;
}

public sealed class AgentSystem
{
    /// <summary>
    /// Strop počtu chodců. Zvednutý proti původním 48: velkoměsto vypadalo stejně
    /// živě jako vesnice o dvanácti domech, protože počet vycházel jen z budov
    /// a strop se vyčerpal hned. Kreslí se pár desítek spritů — to hru nestojí nic.
    /// </summary>
    private const int MaxAgents = AgentSystemLimits.MaxAgents;

    /// <summary>Kolik obyvatel přidá jednoho chodce navíc (nad počet z budov).</summary>
    private const double PeoplePerAgent = 25.0;
    private const float MinZoom = 0.6f;
    private const float SpawnCooldownSeconds = 0.25f;
    private const float DespawnMargin = 120f;
    private const float PersonSpeed = 22f;
    private const float CartSpeed = 40f;

    private enum Kind
    {
        Person,
        Cart,
        Boat,

        /// <summary>Někdo, kdo se nikam nežene — postává, sedí, kouká.</summary>
        Idler,

        /// <summary>Rybář u vody s prutem. Nehýbe se, jen pokyvuje.</summary>
        Fisherman,
    }

    /// <summary>Kategorie budov, od kterých vyplouvají lodě (rybolov, přístavy).</summary>
    private const string WaterCategory = "production";

    private const float BoatSpeed = 30f;

    /// <summary>Jaká část chodců se drží silnic (zbytek chodí po svém).</summary>
    private const float PedestrianRoadShare = 0.5f;

    /// <summary>Jak často se u přístavní budovy místo chodce objeví loďka.</summary>
    /// <summary>
    /// Jak často se místo chodce zkusí vypustit loďka.
    ///
    /// <para>Bývalo 0,55 — tedy víc než polovina všech spawnů. Když se lodě
    /// začaly hledat cíleně (dřív se čekalo na náhodný los), znamenalo to moře
    /// plné lodí. Loďka je koření, ne hlavní chod.</para>
    /// </summary>
    private const float BoatChance = 0.1f;

    /// <summary>Kolik lodí smí být na hladině naráz.</summary>
    private const int MaxBoats = 6;

    private struct Agent
    {
        public Vector2 Position;

        /// <summary>Nejbližší bod, ke kterému agent zrovna jde (krok po silnici).</summary>
        public Vector2 Target;

        /// <summary>
        /// Kam má namířeno doopravdy — práh budovy na druhém konci ulice.
        ///
        /// <para>Tohle je celý rozdíl mezi „lidi se hýbou" a „lidi někam jdou".
        /// Dřív se losoval bod do deseti dlaždic kolem, takže se nikdo nikdy
        /// nikam nedostal; jen se to hemžilo. Cíl drží krok za krokem směr
        /// a hlavně má konec: když se dojde, chodec se zastaví a chvíli
        /// postojí.</para>
        /// </summary>
        public Vector2 Destination;

        /// <summary>Má vůbec kam jít? Bez cíle se agent toulá jako dřív.</summary>
        public bool HasDestination;

        /// <summary>
        /// Na které dlaždici agent stál minule.
        ///
        /// <para>Došlap se počítá při <b>vstupu na novou dlaždici</b>, ne každý
        /// snímek. Přechod přes jednu dlaždici trvá chodci skoro vteřinu, takže
        /// se po snímcích počítal jako dvacet došlapů — jedno přejití vyšlapalo
        /// cestu a náves byla do minuty celá hnědá. Je to počítadlo kroků, ne
        /// stopky.</para>
        /// </summary>
        public int LastTileX;

        /// <summary>Na které dlaždici agent stál minule. Viz <see cref="LastTileX"/>.</summary>
        public int LastTileY;

        /// <summary>Drží se tenhle agent silnic? Vozy vždy, lidé zhruba půl na půl.</summary>
        public bool FollowsRoads;
        public Kind Kind;
        public float Speed;
        public float Phase;
        public bool FaceLeft;

        /// <summary>
        /// Kolik sekund tu ještě postojí. Dokud je kladné, agent se nehýbe.
        ///
        /// <para>Tohle je celý rozdíl mezi „město, kterým někdo prochází"
        /// a „město, ve kterém někdo žije": pár lidí musí zůstat stát.</para>
        /// </summary>
        public float LingerSeconds;
    }

    /// <summary>Jak často se místo chodce objeví někdo, kdo jen postává.</summary>
    private const float IdlerChance = 0.22f;

    /// <summary>Jak dlouho postává, než se zase vydá dál.</summary>
    private const float MinLingerSeconds = 4f;
    private const float MaxLingerSeconds = 12f;

    private readonly GameContent _content;
    private readonly SpriteLibrary? _sprites;
    private readonly Agent[] _agents = new Agent[MaxAgents];
    private int _count;
    private float _spawnTimer;

    public AgentSystem(GameContent content, SpriteLibrary sprites)
    {
        _content = content;
        _sprites = sprites;
    }

    /// <summary>
    /// Systém bez knihovny spritů: dá se aktualizovat, ale ne kreslit.
    ///
    /// <para>Existuje kvůli testům. Chování agentů — hlavně to, že se vracejí
    /// do poolu a nepřestanou se objevovat — je logika, která s kreslením
    /// nesouvisí, a testovat ji přes grafickou kartu by znamenalo netestovat
    /// ji vůbec.</para>
    /// </summary>
    internal AgentSystem(GameContent content)
    {
        _content = content;
        _sprites = null;
    }

    /// <summary>
    /// Kudy se tu chodí. Kreslí to <see cref="WornPathRenderer"/> — agenti sem
    /// jen hlásí došlapy, o kreslení nevědí.
    /// </summary>
    public FootfallMap Footfall { get; } = new();

    /// <summary>
    /// Místo, kam se zrovna stojí za to jít — trh, slavnost, cokoli, co se
    /// v tu chvíli děje. <c>null</c> = nic zvláštního.
    ///
    /// <para>Bez tohohle je z události jen obrázek: stánky by stály na návsi
    /// a lidi by chodili dál po svém, jako by se nic nedělo. Dav, který se
    /// sbíhá, je na události to jediné, co je opravdu vidět.</para>
    /// </summary>
    public Vector2? Attraction { get; set; }

    /// <summary>Jak často chodec upřednostní událost před svou pochůzkou.</summary>
    private const float AttractionShare = 0.5f;

    /// <summary>Za jakou dálku se už za trhem nechodí (v dlaždicích).</summary>
    private const int AttractionRangeTiles = 22;

    /// <summary>
    /// Kde zrovna stojí lidé. Čte to fauna, aby před nimi plachá zvířata
    /// utíkala.
    ///
    /// <para>Seznam se přepisuje jednou za <see cref="Update"/>, ne při každém
    /// sáhnutí: kdyby se plnil až tady, vymazal by se komukoli, kdo ho zrovna
    /// prochází, jen proto, že si o něj řekl někdo druhý.</para>
    /// </summary>
    public IReadOnlyList<Vector2> People => _people;

    private readonly List<Vector2> _people = new();

    /// <summary>Přepíše seznam pozic pro faunu. Bez alokace — pořád tentýž seznam.</summary>
    private void RefreshPeople()
    {
        _people.Clear();
        for (int i = 0; i < _count; i++)
        {
            _people.Add(_agents[i].Position);
        }
    }

    /// <summary>Kolik agentů je právě na scéně. Pro testy poolu.</summary>
    internal int CountForTests => _count;

    /// <summary>
    /// Kolikrát už někdo došel tam, kam šel.
    ///
    /// <para>Existuje kvůli testu, a je to ta jediná věc, která se na celé téhle
    /// vrstvě dá smysluplně ověřit: dřív se losoval bod pár dlaždic daleko, takže
    /// se sice pořád někdo hýbal, ale <b>nikdo nikdy nikam nedošel</b>. To se
    /// zvenčí nepozná jinak než tím, že se příchody počítají.</para>
    /// </summary>
    internal int ArrivalsForTests => _arrivals;

    /// <summary>Kde všude agenti zrovna stojí. Pro testy, které měří, kam se dav stáhne.</summary>
    internal IEnumerable<Vector2> PositionsForTests
    {
        get
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _agents[i].Position;
            }
        }
    }

    private int _arrivals;

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        // Stezky zarůstají, i když se hráč dívá jinam nebo je oddálený — jinak by
        // se po návratu ke městu objevily přesně tak, jak je opustil, i po hodině.
        Footfall.Update(dt);

        if (camera.Zoom < DetailLevel.Scale(MinZoom) || simulation.Buildings.Length == 0)
        {
            _count = 0; // oddáleno nebo prázdný svět → nikdo tu není
            _people.Clear();
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        var errand = DayRhythm.ErrandAt(simulation.TimeOfDay01);
        UpdateAgents(dt, simulation, min, max, errand);
        TrySpawn(dt, camera, simulation, min, max, errand);
        RefreshPeople();
    }

    /// <summary>Klik na obyvatele poblíž bodu — vrací jeho pozici (herní obrazovka pak ukáže myšlenku).</summary>
    public bool TryPokeAgent(Vector2 world, float radius, out Vector2 position)
    {
        float radiusSquared = radius * radius;
        for (int i = 0; i < _count; i++)
        {
            float dx = _agents[i].Position.X - world.X;
            float dy = _agents[i].Position.Y - world.Y;
            if (dx * dx + dy * dy <= radiusSquared)
            {
                position = _agents[i].Position;
                return true;
            }
        }

        position = Vector2.Zero;
        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_count == 0 || _sprites is null)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var agent = ref _agents[i];
            var sprite = _sprites.Get(agent.Kind switch
            {
                Kind.Person or Kind.Idler => "agent.person",
                Kind.Fisherman => "agent.fisherman",
                Kind.Boat => "agent.boat",
                _ => "agent.cart",
            });
            if (sprite is null)
            {
                continue;
            }

            // Chodci lehce poskakují, vozíky drncají, lodě se pomalu houpou na vlnách.
            float bob = agent.Kind switch
            {
                Kind.Person => MathF.Abs(MathF.Sin(agent.Phase * 8f)) * 1.5f,
                // Kdo stojí, ten se jen mírně přenáší z nohy na nohu; rybář
                // pokyvuje s prutem. Bez pohybu by z nich byly cedule.
                Kind.Idler => MathF.Sin(agent.Phase * 1.6f) * 0.7f,
                Kind.Fisherman => MathF.Sin(agent.Phase * 1.1f) * 0.5f,
                Kind.Boat => MathF.Sin(agent.Phase * 2.2f) * 1.6f,
                _ => MathF.Sin(agent.Phase * 14f) * 0.6f,
            };
            var origin = new Vector2(sprite.Width * 0.5f, sprite.Height);
            var effect = agent.FaceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            // Stín + tělo.
            spriteBatch.Draw(sprite, new Vector2(agent.Position.X, agent.Position.Y - bob), null,
                Color.White, 0f, origin, 1f, effect, 0f);
        }

        spriteBatch.End();
    }

    private void UpdateAgents(float dt, Simulation simulation, Vector2 min, Vector2 max, Errand errand)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var agent = ref _agents[i];
            agent.Phase += dt;

            // Kdo postává, ten se nehýbe — jen mu ubývá čas. Když dojde,
            // vydá se dál jako každý jiný chodec; z rybáře se stane kolemjdoucí
            // a místo u vody se uvolní pro dalšího.
            if (agent.LingerSeconds > 0f)
            {
                agent.LingerSeconds -= dt;
                if (agent.LingerSeconds <= 0f)
                {
                    agent.Kind = Kind.Person;
                    agent.Speed = PersonSpeed;
                    agent.HasDestination =
                        TryPickDestination(simulation, agent.Position, errand, out agent.Destination);
                    agent.Target = NextStep(simulation, ref agent, Vector2.Zero);
                }

                if (IsOutOfSight(agent.Position, min, max))
                {
                    _agents[i] = _agents[--_count]; // i ten, kdo stojí, musí zmizet za obzorem
                }

                continue;
            }

            // Došel až k prahu? Pak se tu chvíli zdrží — a to je ten okamžik,
            // který z pohybu dělá pochůzku. Bez zastavení by chodec u dveří
            // jen otočil a šel dál a nikdo by nepoznal, že někam došel.
            if (agent.HasDestination
                && agent.Kind != Kind.Boat
                && Vector2.DistanceSquared(agent.Position, agent.Destination) < ArrivalRadiusSquared)
            {
                agent.HasDestination = false;
                agent.LingerSeconds = MinLingerSeconds
                    + (Random.Shared.NextSingle() * (MaxLingerSeconds - MinLingerSeconds));
                _arrivals++;
                continue;
            }

            var toTarget = agent.Target - agent.Position;
            float distance = toTarget.Length();
            if (distance < 3f)
            {
                // Směr, kterým agent zrovna míří — nový krok se hledá přednostně
                // dopředu, aby vůz na silnici neposkakoval tam a zpět.
                var heading = distance > 0.01f ? toTarget / distance : Vector2.Zero;

                if (agent.Kind == Kind.Boat)
                {
                    // Loďka hledá cíl po vodě — jiné prostředí, jiná pravidla.
                    agent.Target = PickWaterTarget(simulation, agent.Position, heading);
                }
                else
                {
                    if (!agent.HasDestination)
                    {
                        agent.HasDestination =
                            TryPickDestination(simulation, agent.Position, errand, out agent.Destination);
                    }

                    agent.Target = NextStep(simulation, ref agent, heading);
                }

                toTarget = agent.Target - agent.Position;
                distance = toTarget.Length();
            }

            if (distance > 0.01f)
            {
                var step = toTarget / distance * agent.Speed * dt;
                agent.Position += step;
                agent.FaceLeft = step.X < 0f;

                // Došlap se počítá při vstupu na NOVOU dlaždici — viz Agent.LastTileX.
                //
                // A jen tam, kde žádná cesta není: stezka vzniká tím, kudy lidi
                // chodí NAVZDORY tomu, že tudy cesta nevede. Ošlapávat dlažbu by
                // znamenalo kreslit hlínu přes silnici.
                int tileX = (int)MathF.Floor(agent.Position.X / TerrainRenderer.TileSize);
                int tileY = (int)MathF.Floor(agent.Position.Y / TerrainRenderer.TileSize);
                if ((tileX != agent.LastTileX || tileY != agent.LastTileY)
                    && agent.Kind != Kind.Boat
                    && !simulation.HasRoadAt(tileX, tileY))
                {
                    Footfall.Step(tileX, tileY);
                }

                agent.LastTileX = tileX;
                agent.LastTileY = tileY;
            }

            bool outOfView = agent.Position.X < min.X - DespawnMargin || agent.Position.X > max.X + DespawnMargin
                || agent.Position.Y < min.Y - DespawnMargin || agent.Position.Y > max.Y + DespawnMargin;
            if (outOfView)
            {
                _agents[i] = _agents[--_count];
            }
        }
    }

    /// <summary>Kolik lodí je zrovna na vodě — nad strop se další nepouští.</summary>
    private int CountBoats()
    {
        int boats = 0;
        for (int i = 0; i < _count; i++)
        {
            if (_agents[i].Kind == Kind.Boat)
            {
                boats++;
            }
        }

        return boats;
    }

    /// <summary>
    /// Vypustí loďku od budovy, která pracuje na vodě.
    ///
    /// <para>Hledá se cíleně mezi budovami u vody — těch je pár, kdežto domů
    /// stovky, takže rovnoměrné losování na ně prakticky nedosáhlo. Prochází se
    /// jen viditelné budovy, takže to nic nestojí ani u velkého města.</para>
    /// </summary>
    private bool TrySpawnBoat(Simulation simulation, Vector2 min, Vector2 max)
    {
        const int tileSize = TerrainRenderer.TileSize;

        for (int pass = 0; pass < 2; pass++)
        {
            var buildings = pass == 0 ? simulation.Buildings : simulation.NpcBuildings;
            if (buildings.Length == 0)
            {
                continue;
            }

            // Od náhodného místa dokola, ať se nevybírá pořád tatáž chatrč.
            int start = Random.Shared.Next(buildings.Length);
            for (int step = 0; step < buildings.Length; step++)
            {
                ref readonly var building = ref buildings[(start + step) % buildings.Length];
                float x = (building.X + 0.5f) * tileSize;
                float y = (building.Y + 0.5f) * tileSize;
                if (x < min.X || x > max.X || y < min.Y || y > max.Y)
                {
                    continue;
                }

                if (!WorksOnWater(_content, _content.Buildings[building.DefIndex])
                    || !TryFindWaterNear(simulation, building, out var launch))
                {
                    continue;
                }

                _agents[_count++] = new Agent
                {
                    Position = launch,
                    Target = PickWaterTarget(simulation, launch, Vector2.Zero),
                    Kind = Kind.Boat,
                    FollowsRoads = false,
                    Speed = BoatSpeed,
                    Phase = Random.Shared.NextSingle() * 10f,
                };
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Vybere dům, u kterého se někdo objeví. Střídá hráčovy budovy a domy
    /// objevených cizích měst — obojí je zástavba, ve které se má chodit.
    /// </summary>
    private static bool TryPickAnchor(Simulation simulation, out Vector2 center, out BuildingInstance anchor)
    {
        const int tileSize = TerrainRenderer.TileSize;
        center = Vector2.Zero;
        anchor = default;

        // Nejdřív se zkusí cizí město: hráčových budov bývá o řády víc, takže by
        // se na cizí při rovnoměrném losování skoro nikdy nedostalo.
        var foreign = simulation.NpcBuildings;
        var buildings = Random.Shared.Next(3) == 0 && foreign.Length > 0
            ? foreign
            : simulation.Buildings;

        if (buildings.Length == 0)
        {
            return false;
        }

        anchor = buildings[Random.Shared.Next(buildings.Length)];
        center = new Vector2((anchor.X + 0.5f) * tileSize, (anchor.Y + 0.5f) * tileSize);
        return true;
    }

    private void TrySpawn(float dt, Camera2D camera, Simulation simulation, Vector2 min, Vector2 max, Errand errand)
    {
        _spawnTimer -= dt;
        // Cíl počtu roste s městem — jak zástavbou, tak lidmi. Bez populace by
        // aglomerace o milionu vypadala stejně prázdně jako první osada.
        // Cíl počtu se násobí denním rytmem: ve tři ráno má být ulice prázdná
        // skoro, ne úplně. Dolní mez drží pár lidí venku i v hluboké noci —
        // vylidněné město vypadá jako vypnutá hra, ne jako spící město.
        int full = 4 + simulation.Buildings.Length + (int)(simulation.Population / PeoplePerAgent);
        int desired = Math.Min(
            MaxAgents,
            Math.Max(3, (int)(full * DayRhythm.CrowdAt(simulation.TimeOfDay01))));
        if (_spawnTimer > 0f || _count >= desired)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        // Loďky se řeší ZVLÁŠŤ a jako první. Dřív se čekalo, až náhodný los
        // trefí zrovna rybářskou chatrč mezi stovkami budov — a i pak se spawn
        // stihl utnout dřív, protože se testovala průchodnost SOUŠE u budovy,
        // která stojí na pláži obklopené vodou. Výsledek: hráč lodě nikdy
        // neviděl, ačkoli byly celou dobu naprogramované.
        if (Random.Shared.NextSingle() < BoatChance
            && CountBoats() < MaxBoats
            && TrySpawnBoat(simulation, min, max))
        {
            return;
        }

        // Spawn poblíž náhodné budovy ve výřezu — vlastní i cizí. Objevené cizí
        // město bez lidí vypadá jako vyhořelé; když už z něj stojí domy, mají
        // v nich někoho mít.
        if (!TryPickAnchor(simulation, out var center, out _))
        {
            return;
        }

        if (center.X < min.X || center.X > max.X || center.Y < min.Y || center.Y > max.Y)
        {
            return; // budova mimo pohled — spawn počká na jinou
        }

        var pos = center + new Vector2(
            (Random.Shared.NextSingle() - 0.5f) * 6f * TerrainRenderer.TileSize,
            (Random.Shared.NextSingle() - 0.5f) * 6f * TerrainRenderer.TileSize);
        if (!IsPassable(simulation, pos))
        {
            return;
        }

        // Vozíky jen když je kam jet (existují cesty); jinak chodci. Ulice cizích
        // měst se počítají taky — i tam se má jezdit.
        bool anyRoads = simulation.RoadTiles.Count > 0 || simulation.NpcRoadTiles.Count > 0;
        bool cart = anyRoads && Random.Shared.NextSingle() < 0.25f;

        // Vůz po silnici jezdí vždycky — jinak by cesty neměly smysl ani opticky.
        // Chodec se silnice drží zhruba v polovině případů: město tak působí živě,
        // ale ne jako mravenčí kolona po jedné lince.
        bool followsRoads = cart || (anyRoads && Random.Shared.NextSingle() < PedestrianRoadShare);

        // Část lidí se nikam nežene: postává u domu, sedí, kouká. Kdo bydlí
        // u vody a je zrovna u rybářské budovy, chytá ryby. Bez pár stojících
        // postav vypadá i velkoměsto jako průchoďák.
        var kind = cart ? Kind.Cart : Kind.Person;
        float linger = 0f;
        if (!cart && Random.Shared.NextSingle() < IdlerChance)
        {
            kind = IsNextToWater(simulation, pos) ? Kind.Fisherman : Kind.Idler;
            linger = MinLingerSeconds
                + (Random.Shared.NextSingle() * (MaxLingerSeconds - MinLingerSeconds));
        }

        // Kdo postává nebo rybaří, nikam nejde — cíl by mu jen ležel v datech.
        var destination = Vector2.Zero;
        bool hasDestination = kind != Kind.Idler
            && kind != Kind.Fisherman
            && TryPickDestination(simulation, pos, errand, out destination);

        _agents[_count++] = new Agent
        {
            Position = pos,
            Destination = destination,
            HasDestination = hasDestination,

            // Dlaždice, na které se objevil, se za došlap nepočítá — jinak by
            // každý nový chodec ošlapal zem už tím, že se ukázal.
            LastTileX = (int)MathF.Floor(pos.X / TerrainRenderer.TileSize),
            LastTileY = (int)MathF.Floor(pos.Y / TerrainRenderer.TileSize),
            Target = PickTarget(simulation, pos, followsRoads, Vector2.Zero),
            Kind = kind,
            FollowsRoads = followsRoads,
            Speed = kind == Kind.Cart ? CartSpeed : PersonSpeed,
            Phase = Random.Shared.NextSingle() * 10f,
            LingerSeconds = linger,
            FaceLeft = Random.Shared.Next(2) == 0,
        };
    }

    /// <summary>
    /// Je agent za obzorem? Společné pravidlo pro chodce i pro ty, kdo stojí —
    /// kdyby platilo jen pro chodce, postávající by se v poolu nasčítali
    /// a nové by nebylo kam dát.
    /// </summary>
    private static bool IsOutOfSight(Vector2 position, Vector2 min, Vector2 max) =>
        position.X < min.X - DespawnMargin || position.X > max.X + DespawnMargin
        || position.Y < min.Y - DespawnMargin || position.Y > max.Y + DespawnMargin;

    /// <summary>Stojí to místo u vody? Rybář jinam nepatří.</summary>
    private static bool IsNextToWater(Simulation simulation, Vector2 position)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int tileX = (int)MathF.Floor(position.X / tileSize);
        int tileY = (int)MathF.Floor(position.Y / tileSize);

        return simulation.IsWaterAt(tileX + 1, tileY)
            || simulation.IsWaterAt(tileX - 1, tileY)
            || simulation.IsWaterAt(tileX, tileY + 1)
            || simulation.IsWaterAt(tileX, tileY - 1);
    }

    /// <summary>Jak blízko k prahu se počítá za „došel". Zhruba polovina dlaždice.</summary>
    private const float ArrivalRadiusSquared =
        (TerrainRenderer.TileSize * 0.6f) * (TerrainRenderer.TileSize * 0.6f);

    /// <summary>
    /// Další krok agenta.
    ///
    /// <para>Když má cíl, míří se k němu: kdo se drží silnic, jde po síti, ale
    /// ze sousedních dlaždic si vybere tu, která ho k cíli přiblíží — proto se
    /// dojde i křivolakou ulicí. Kdo silnice neřeší, jde rovnou.</para>
    ///
    /// <para>Bez cíle zbývá staré toulání: je to kulisa, ne rozvrh, a chodec
    /// bez cíle je pořád lepší než chodec stojící na místě.</para>
    /// </summary>
    private Vector2 NextStep(Simulation simulation, ref Agent agent, Vector2 heading)
    {
        if (!agent.HasDestination)
        {
            return PickTarget(simulation, agent.Position, agent.FollowsRoads, heading);
        }

        var toGoal = agent.Destination - agent.Position;
        if (toGoal.LengthSquared() > 0.01f)
        {
            toGoal.Normalize();
        }

        if (agent.FollowsRoads && TryStepAlongRoad(simulation, agent.Position, toGoal, out var roadStep))
        {
            return roadStep;
        }

        // Přímá cesta: krok o dlaždici k cíli. Když do ní nelze vstoupit (voda,
        // sráz), cíl se zahodí a chodec se vydá jinam — obcházení překážek by
        // z kulisy udělalo hledání cesty, a to je na ambientní chodce moc.
        var step = agent.Position + (toGoal * TerrainRenderer.TileSize);
        if (IsPassable(simulation, step))
        {
            return step;
        }

        agent.HasDestination = false;
        return PickTarget(simulation, agent.Position, agent.FollowsRoads, heading);
    }

    /// <summary>Hledání cílové budovy si půjčuje tenhle seznam, aby se každý krok nealokovalo.</summary>
    private readonly List<int> _nearby = new();

    /// <summary>
    /// Jak daleko (v dlaždicích) se hledá cílová budova. Dost na to, aby byla
    /// cesta vidět, ne tak daleko, aby chodec mizel za obzorem dřív, než dojde.
    /// </summary>
    private const int ErrandRadiusTiles = 14;

    /// <summary>
    /// Vybere budovu, ke které se chodec vydá — podle toho, co je za denní dobu.
    ///
    /// <para>Prochází se jen okolí agenta přes chunkový index zástavby, takže
    /// to nestojí víc ani ve městě o statisících budov. Když se nic vhodného
    /// nenajde (ráno v průmyslové čtvrti bez dílen), vrátí <c>false</c>
    /// a chodec se prostě toulá jako dřív — je to kulisa, ne rozvrh.</para>
    /// </summary>
    private bool TryPickDestination(Simulation simulation, Vector2 from, Errand errand, out Vector2 destination)
    {
        const int tile = TerrainRenderer.TileSize;
        int x = (int)MathF.Floor(from.X / tile);
        int y = (int)MathF.Floor(from.Y / tile);

        _nearby.Clear();
        simulation.BuildingsIn(
            x - ErrandRadiusTiles, y - ErrandRadiusTiles,
            x + ErrandRadiusTiles, y + ErrandRadiusTiles,
            _nearby);

        destination = Vector2.Zero;

        // Děje-li se poblíž něco, jde půlka lidí tam — ať je na události vidět,
        // že o ni někdo stojí.
        if (Attraction is { } attraction
            && Random.Shared.NextSingle() < AttractionShare
            && Vector2.DistanceSquared(from, attraction)
                < (AttractionRangeTiles * tile) * (AttractionRangeTiles * tile))
        {
            // Rozptyl kolem stánků: dav se má shluknout, ne stát v jednom bodě.
            destination = attraction + new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 3f * tile,
                (Random.Shared.NextSingle() - 0.5f) * 3f * tile);
            return true;
        }

        if (_nearby.Count == 0)
        {
            return false;
        }

        // Losuje se z těch, co k pochůzce sedí; rezervou je kterákoli budova,
        // ať chodec nezůstane stát jen proto, že v okolí není dílna.
        int chosen = -1;
        int matches = 0;
        int fallback = _nearby[Random.Shared.Next(_nearby.Count)];

        for (int i = 0; i < _nearby.Count; i++)
        {
            int index = _nearby[i];
            if (index < 0 || index >= simulation.Buildings.Length)
            {
                continue;
            }

            if (!Suits(errand, _content.Buildings[simulation.Buildings[index].DefIndex]))
            {
                continue;
            }

            // Reservoir sampling: jeden průchod, žádný druhý seznam.
            matches++;
            if (Random.Shared.Next(matches) == 0)
            {
                chosen = index;
            }
        }

        if (chosen < 0)
        {
            chosen = fallback;
            if (chosen < 0 || chosen >= simulation.Buildings.Length)
            {
                return false;
            }
        }

        ref readonly var building = ref simulation.Buildings[chosen];

        // Práh, ne střed: k budově se dochází zvenčí, a stát uprostřed sprite
        // vypadá, jako by chodec prošel zdí.
        destination = new Vector2(
            (building.X + 0.5f) * tile,
            (building.Y + 1.15f) * tile);
        return true;
    }

    /// <summary>Hodí se tahle budova na tuhle pochůzku?</summary>
    private static bool Suits(Errand errand, BuildingDef def) => errand switch
    {
        Errand.Home => def.HousingCapacity > 0,
        Errand.Work => def.HousingCapacity == 0 && def.WorkerSlots > 0,
        _ => true,
    };

    /// <summary>
    /// Vybere další cíl. Kdo se drží silnic, míří na SOUSEDNÍ dlaždici cesty —
    /// krátkými kroky po síti, takže je vidět, že jede po silnici, a ne přes pole.
    /// Ostatní jdou rovnou k cíli, a když žádný nemají, toulají se jako dřív.
    /// </summary>
    private Vector2 PickTarget(Simulation simulation, Vector2 from, bool followsRoads, Vector2 heading)
    {
        if (followsRoads && TryStepAlongRoad(simulation, from, heading, out var roadTarget))
        {
            return roadTarget;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            var candidate = from + new Vector2(
                (Random.Shared.NextSingle() - 0.5f) * 10f * TerrainRenderer.TileSize,
                (Random.Shared.NextSingle() - 0.5f) * 10f * TerrainRenderer.TileSize);

            if (IsPassable(simulation, candidate))
            {
                return candidate;
            }
        }

        return from; // nic vhodného → počkej na místě
    }

    /// <summary>
    /// Krok po silniční síti: ze čtyř sousedů vybere ten, který je cesta, a
    /// upřednostní pokračování v dosavadním směru (jinak by se vůz na křižovatce
    /// otáčel dokola). Vrací false, když poblíž žádná cesta není.
    /// </summary>
    private static bool TryStepAlongRoad(Simulation simulation, Vector2 from, Vector2 heading, out Vector2 target)
    {
        const int tile = TerrainRenderer.TileSize;
        int x = (int)MathF.Floor(from.X / tile);
        int y = (int)MathF.Floor(from.Y / tile);

        Span<(int Dx, int Dy)> steps = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        float bestScore = float.MinValue;
        (int Dx, int Dy) best = default;
        bool found = false;

        foreach (var (dx, dy) in steps)
        {
            if (!simulation.HasRoadAt(x + dx, y + dy))
            {
                continue;
            }

            // Skóre: shoda se směrem jízdy (dopředu nejlíp, zpět nejhůř) + špetka
            // náhody, aby se doprava na křižovatkách rozdělovala.
            float score = heading.LengthSquared() > 0.01f
                ? Vector2.Dot(heading, new Vector2(dx, dy))
                : 0f;
            score += Random.Shared.NextSingle() * 0.35f;

            if (score > bestScore)
            {
                bestScore = score;
                best = (dx, dy);
                found = true;
            }
        }

        if (!found)
        {
            target = from;
            return false;
        }

        // Střed sousední dlaždice, s malým rozptylem napříč cestou, ať agenti
        // nejedou přesně v jedné lince.
        float jitter = (Random.Shared.NextSingle() - 0.5f) * tile * 0.35f;
        target = new Vector2(
            (x + best.Dx + 0.5f) * tile + (best.Dx == 0 ? jitter : 0f),
            (y + best.Dy + 0.5f) * tile + (best.Dy == 0 ? jitter : 0f));
        return true;
    }

    /// <summary>Najde vodní dlaždici hned vedle budovy — odtud loďka vyplouvá.</summary>
    /// <summary>
    /// Pracuje budova na vodě? Poznává se to z dat, ne ze seznamu ID.
    ///
    /// <para>Dřív stačilo <c>requiresAdjacentWater</c> — jenže rybářská chatrč
    /// ho nemá (stojí na pláži), takže z ní nikdy žádná loď nevyplula, i když je
    /// to ta nejrybářštější budova ve hře. Druhé kritérium je proto „smí stát jen
    /// na pár pobřežních biomech".</para>
    /// </summary>
    private static bool WorksOnWater(GameContent content, BuildingDef def)
    {
        if (def.NeedsWaterAccess)
        {
            return true;
        }

        int allowed = 0;
        bool shore = false;
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (!def.IsBiomeAllowed(i))
            {
                continue;
            }

            allowed++;
            shore |= content.Biomes[i].Id is "beach" or "mangrove";
        }

        return shore && allowed <= 2;
    }

    private bool TryFindWaterNear(Simulation simulation, in BuildingInstance building, out Vector2 position)
    {
        var def = _content.Buildings[building.DefIndex];
        for (int y = building.Y - 1; y <= building.Y + def.FootprintHeight; y++)
        {
            for (int x = building.X - 1; x <= building.X + def.FootprintWidth; x++)
            {
                if (IsOpenWater(simulation, x, y))
                {
                    position = new Vector2((x + 0.5f) * TerrainRenderer.TileSize, (y + 0.5f) * TerrainRenderer.TileSize);
                    return true;
                }
            }
        }

        position = Vector2.Zero;
        return false;
    }

    /// <summary>
    /// Kam loďka popluje: náhodný bod poblíž, ale jen po vodě. Několik pokusů
    /// stačí — když se žádný netrefí, loď zůstane stát a zkusí to příští cíl.
    /// </summary>
    private Vector2 PickWaterTarget(Simulation simulation, Vector2 from, Vector2 heading)
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = heading == Vector2.Zero
                ? Random.Shared.NextSingle() * MathF.Tau
                : MathF.Atan2(heading.Y, heading.X) + (Random.Shared.NextSingle() - 0.5f) * 1.2f;
            float distance = (2f + Random.Shared.NextSingle() * 4f) * TerrainRenderer.TileSize;
            var candidate = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            int tileX = (int)MathF.Floor(candidate.X / TerrainRenderer.TileSize);
            int tileY = (int)MathF.Floor(candidate.Y / TerrainRenderer.TileSize);
            if (IsOpenWater(simulation, tileX, tileY))
            {
                return candidate;
            }
        }

        return from;
    }

    /// <summary>Volná voda (ne most, ne budova) — loď po mostě neplave.</summary>
    private bool IsOpenWater(Simulation simulation, int x, int y) =>
        _content.Biomes[simulation.BiomeAt(x, y)].IsWater
        && !simulation.HasRoadAt(x, y)
        && !simulation.IsOccupied(x, y)
        && !simulation.IsNpcOccupied(x, y);

    private bool IsPassable(Simulation simulation, Vector2 worldPos)
    {
        int tileX = (int)MathF.Floor(worldPos.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(worldPos.Y / TerrainRenderer.TileSize);
        if (simulation.IsOccupied(tileX, tileY) || simulation.IsNpcOccupied(tileX, tileY))
        {
            return false;
        }

        return !_content.Biomes[simulation.BiomeAt(tileX, tileY)].IsWater;
    }
}
