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
public sealed class AgentSystem
{
    /// <summary>
    /// Strop počtu chodců. Zvednutý proti původním 48: velkoměsto vypadalo stejně
    /// živě jako vesnice o dvanácti domech, protože počet vycházel jen z budov
    /// a strop se vyčerpal hned. Kreslí se pár desítek spritů — to hru nestojí nic.
    /// </summary>
    private const int MaxAgents = 96;

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
    }

    /// <summary>Kategorie budov, od kterých vyplouvají lodě (rybolov, přístavy).</summary>
    private const string WaterCategory = "production";

    private const float BoatSpeed = 30f;

    /// <summary>Jaká část chodců se drží silnic (zbytek chodí po svém).</summary>
    private const float PedestrianRoadShare = 0.5f;

    /// <summary>Jak často se u přístavní budovy místo chodce objeví loďka.</summary>
    private const float BoatChance = 0.55f;

    private struct Agent
    {
        public Vector2 Position;
        public Vector2 Target;

        /// <summary>Drží se tenhle agent silnic? Vozy vždy, lidé zhruba půl na půl.</summary>
        public bool FollowsRoads;
        public Kind Kind;
        public float Speed;
        public float Phase;
        public bool FaceLeft;
    }

    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;
    private readonly Agent[] _agents = new Agent[MaxAgents];
    private int _count;
    private float _spawnTimer;

    public AgentSystem(GameContent content, SpriteLibrary sprites)
    {
        _content = content;
        _sprites = sprites;
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < MinZoom || simulation.Buildings.Length == 0)
        {
            _count = 0; // oddáleno nebo prázdný svět → nikdo tu není
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        UpdateAgents(dt, simulation, min, max);
        TrySpawn(dt, camera, simulation, min, max);
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
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var agent = ref _agents[i];
            var sprite = _sprites.Get(agent.Kind switch
            {
                Kind.Person => "agent.person",
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

    private void UpdateAgents(float dt, Simulation simulation, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var agent = ref _agents[i];
            agent.Phase += dt;

            var toTarget = agent.Target - agent.Position;
            float distance = toTarget.Length();
            if (distance < 3f)
            {
                // Směr, kterým agent zrovna míří — nový cíl se hledá přednostně
                // dopředu, aby vůz na silnici neposkakoval tam a zpět.
                var heading = distance > 0.01f ? toTarget / distance : Vector2.Zero;
                // Loďka hledá cíl po vodě, ostatní po souši — jiné prostředí,
                // jiná pravidla pohybu.
                agent.Target = agent.Kind == Kind.Boat
                    ? PickWaterTarget(simulation, agent.Position, heading)
                    : PickTarget(simulation, agent.Position, agent.FollowsRoads, heading);
                toTarget = agent.Target - agent.Position;
                distance = toTarget.Length();
            }

            if (distance > 0.01f)
            {
                var step = toTarget / distance * agent.Speed * dt;
                agent.Position += step;
                agent.FaceLeft = step.X < 0f;
            }

            bool outOfView = agent.Position.X < min.X - DespawnMargin || agent.Position.X > max.X + DespawnMargin
                || agent.Position.Y < min.Y - DespawnMargin || agent.Position.Y > max.Y + DespawnMargin;
            if (outOfView)
            {
                _agents[i] = _agents[--_count];
            }
        }
    }

    /// <summary>
    /// Vybere dům, u kterého se někdo objeví. Střídá hráčovy budovy a domy
    /// objevených cizích měst — obojí je zástavba, ve které se má chodit.
    /// </summary>
    private static bool TryPickAnchor(Simulation simulation, out Vector2 center, out NpcTownBuilding anchor)
    {
        const int tileSize = TerrainRenderer.TileSize;
        center = Vector2.Zero;
        anchor = default;

        // Nejdřív se zkusí cizí město: hráčových budov bývá o řády víc, takže by
        // se na cizí při rovnoměrném losování skoro nikdy nedostalo.
        if (Random.Shared.Next(3) == 0)
        {
            foreach (var town in simulation.DiscoveredTownsNear(
                simulation.CityCenterX, simulation.CityCenterY, NpcCityMap.CellTiles * 2))
            {
                anchor = town.Buildings[Random.Shared.Next(town.Buildings.Count)];
                center = new Vector2((anchor.X + 0.5f) * tileSize, (anchor.Y + 0.5f) * tileSize);
                return true;
            }
        }

        var buildings = simulation.Buildings;
        if (buildings.Length == 0)
        {
            return false;
        }

        ref readonly var building = ref buildings[Random.Shared.Next(buildings.Length)];
        anchor = new NpcTownBuilding(building.DefIndex, building.X, building.Y);
        center = new Vector2((anchor.X + 0.5f) * tileSize, (anchor.Y + 0.5f) * tileSize);
        return true;
    }

    private void TrySpawn(float dt, Camera2D camera, Simulation simulation, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        // Cíl počtu roste s městem — jak zástavbou, tak lidmi. Bez populace by
        // aglomerace o milionu vypadala stejně prázdně jako první osada.
        int desired = Math.Min(
            MaxAgents,
            4 + simulation.Buildings.Length + (int)(simulation.Population / PeoplePerAgent));
        if (_spawnTimer > 0f || _count >= desired)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        // Spawn poblíž náhodné budovy ve výřezu — vlastní i cizí. Objevené cizí
        // město bez lidí vypadá jako vyhořelé; když už z něj stojí domy, mají
        // v nich někoho mít.
        if (!TryPickAnchor(simulation, out var center, out var anchor))
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

        // Přístavní budova = šance na loďku. Vyplouvá z vody vedle budovy, takže
        // je hned vidět, odkud jede — právě ten detail dělá pobřeží živým.
        if (WorksOnWater(_content, _content.Buildings[anchor.DefIndex])
            && Random.Shared.NextSingle() < BoatChance
            && TryFindWaterNear(simulation, anchor, out var launch))
        {
            _agents[_count++] = new Agent
            {
                Position = launch,
                Target = PickWaterTarget(simulation, launch, Vector2.Zero),
                Kind = Kind.Boat,
                FollowsRoads = false,
                Speed = BoatSpeed,
                Phase = Random.Shared.NextSingle() * 10f,
            };
            return;
        }

        // Vozíky jen když je kam jet (existují cesty); jinak chodci.
        bool cart = simulation.RoadTiles.Count > 0 && Random.Shared.NextSingle() < 0.25f;

        // Vůz po silnici jezdí vždycky — jinak by cesty neměly smysl ani opticky.
        // Chodec se silnice drží zhruba v polovině případů: město tak působí živě,
        // ale ne jako mravenčí kolona po jedné lince.
        bool followsRoads = cart
            || (simulation.RoadTiles.Count > 0 && Random.Shared.NextSingle() < PedestrianRoadShare);

        _agents[_count++] = new Agent
        {
            Position = pos,
            Target = PickTarget(simulation, pos, followsRoads, Vector2.Zero),
            Kind = cart ? Kind.Cart : Kind.Person,
            FollowsRoads = followsRoads,
            Speed = cart ? CartSpeed : PersonSpeed,
            Phase = Random.Shared.NextSingle() * 10f,
        };
    }

    /// <summary>
    /// Vybere další cíl. Kdo se drží silnic, míří na SOUSEDNÍ dlaždici cesty —
    /// krátkými kroky po síti, takže je vidět, že jede po silnici, a ne přes pole.
    /// Ostatní se toulají volně jako dřív.
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
            if (!simulation.IsRoad(x + dx, y + dy))
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

    private bool TryFindWaterNear(Simulation simulation, in NpcTownBuilding building, out Vector2 position)
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
        && !simulation.IsRoad(x, y)
        && !simulation.IsOccupied(x, y);

    private bool IsPassable(Simulation simulation, Vector2 worldPos)
    {
        int tileX = (int)MathF.Floor(worldPos.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(worldPos.Y / TerrainRenderer.TileSize);
        if (simulation.IsOccupied(tileX, tileY))
        {
            return false;
        }

        return !_content.Biomes[simulation.BiomeAt(tileX, tileY)].IsWater;
    }
}
