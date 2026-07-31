using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Doprava po silnicích: káry, povozy a náklaďáky, které jezdí po tom, co hráč
/// postavil.
///
/// <para>Proč to ve hře je: silnice byly do téhle chvíle statická textura.
/// Město, kde se něco hýbe, vypadá živé — a je to ta nejlevnější zpětná vazba,
/// jakou zástavba může dát. Hustota provozu roste s populací, takže je na první
/// pohled poznat rozdíl mezi vesnicí a velkoměstem, a vozidla se s érou mění
/// (kára → povoz → náklaďák → vznášedlo), takže je vidět i postup v čase.</para>
///
/// <para>Vrstva: čistě render (stejně jako fauna, viz living-map.md). Do
/// simulace <b>nezapisuje</b> a nic nevozí — kdyby vozidlo doopravdy něco
/// převáželo, patřilo by do simulace. Vozidla existují jen u kamery: spawnují se
/// ve viditelném výřezu a mimo obraz se ruší (LOD + culling z CLAUDE.md).</para>
///
/// <para>Výkon: pevný pool struktur, žádné alokace za běhu. Trasa se nehledá —
/// vozidlo jede rovně a na křižovatce si vybere z volných směrů, takže jeden
/// snímek stojí pár dotazů na dlaždici, ne prohledávání grafu silnic.</para>
/// </summary>
public sealed class TrafficSystem
{
    /// <summary>Strop poolu. Víc aut naráz stejně nikdo nerozezná, a rám by trpěl.</summary>
    private const int MaxVehicles = 40;

    /// <summary>Pod tímhle přiblížením je vozidlo pixel — nemá smysl ho počítat.</summary>
    private const float MinZoom = 0.5f;

    private const float SpawnCooldownSeconds = 0.25f;
    private const float DespawnMargin = 64f;

    /// <summary>Na kolik obyvatel připadá jedno vozidlo v provozu.</summary>
    private const double PeoplePerVehicle = 40.0;

    /// <summary>Kolik dlaždic silnice musí síť mít, aby mělo smysl něco vypouštět.</summary>
    private const int MinRoadTiles = 3;

    private struct Vehicle
    {
        public Vector2 Position;

        /// <summary>Jednotkový směr jízdy (čtyřsměrný — po silnici se nejezdí šikmo).</summary>
        public Point Direction;

        public int DefIndex;

        /// <summary>Odstín korby, ať nejsou všechny stejné.</summary>
        public float Shade;
    }

    private readonly GameContent _content;
    private readonly Vehicle[] _vehicles = new Vehicle[MaxVehicles];
    private readonly List<int> _eligibleDefs = new();
    private int _count;
    private float _spawnTimer;

    /// <summary>
    /// Éra, pro kterou je <see cref="_eligibleDefs"/> spočítaný. Éra se mění
    /// jednou za hodiny hraní; přebírat kvůli tomu katalog každý snímek by byla
    /// zbytečná práce.
    /// </summary>
    private int _eligibleEra = int.MinValue;

    public TrafficSystem(GameContent content)
    {
        _content = content;
    }

    /// <summary>Kolik vozidel je právě na silnicích (pro testy a ladění).</summary>
    public int ActiveCount => _count;

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < MinZoom || _content.Vehicles.Count == 0)
        {
            _count = 0; // oddáleno → provoz zmizí (z dálky ho stejně nikdo nevidí)
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        Advance(dt, simulation, min, max);
        TrySpawn(dt, simulation, min, max);
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera, float nightFactor)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var vehicle = ref _vehicles[i];
            var def = _content.Vehicles[vehicle.DefIndex];

            // Korba je delší po směru jízdy — i z obdélníčku je pak poznat,
            // kam vozidlo míří.
            bool horizontal = vehicle.Direction.X != 0;
            int width = horizontal ? def.Length : def.Width;
            int height = horizontal ? def.Width : def.Length;

            var body = def.Color.ToXna() * vehicle.Shade;
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    (int)(vehicle.Position.X - width * 0.5f),
                    (int)(vehicle.Position.Y - height * 0.5f),
                    width,
                    height),
                body);

            // Světla se rozsvítí až v noci; ve dne by z náklaďáku dělala vánoční stromek.
            if (def.Glow && nightFactor > 0.25f)
            {
                var head = new Vector2(
                    vehicle.Position.X + vehicle.Direction.X * width * 0.5f,
                    vehicle.Position.Y + vehicle.Direction.Y * height * 0.5f);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle((int)head.X - 1, (int)head.Y - 1, 2, 2),
                    new Color(255, 240, 190) * nightFactor);
            }
        }

        spriteBatch.End();
    }

    private void Advance(float dt, Simulation simulation, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var vehicle = ref _vehicles[i];
            var def = _content.Vehicles[vehicle.DefIndex];

            var before = TileOf(vehicle.Position);
            vehicle.Position += new Vector2(vehicle.Direction.X, vehicle.Direction.Y) * def.Speed * dt;
            var now = TileOf(vehicle.Position);

            // Rozhoduje se až při vjezdu do nové dlaždice — jinak by vozidlo
            // uprostřed políčka pořád přemýšlelo, kudy dál.
            if (now != before && !TurnIfNeeded(ref vehicle, simulation, now.X, now.Y))
            {
                _vehicles[i] = _vehicles[--_count];
                continue;
            }

            bool outOfView = vehicle.Position.X < min.X - DespawnMargin || vehicle.Position.X > max.X + DespawnMargin
                || vehicle.Position.Y < min.Y - DespawnMargin || vehicle.Position.Y > max.Y + DespawnMargin;
            if (outOfView)
            {
                _vehicles[i] = _vehicles[--_count];
            }
        }
    }

    /// <summary>
    /// Udrží vozidlo na silnici: rovně, dokud to jde, jinak zabočí. Vrací false,
    /// když je před ním slepá ulička — takové vozidlo se prostě zruší (je to
    /// kulisa, ne zásilka, kterou by někdo postrádal).
    /// </summary>
    private static bool TurnIfNeeded(ref Vehicle vehicle, Simulation simulation, int tileX, int tileY)
    {
        if (simulation.IsRoad(tileX, tileY))
        {
            return true;
        }

        // Zpátky se neotáčí jako první volba — couvající provoz vypadá rozbitě.
        Span<Point> options = stackalloc Point[4];
        int optionCount = 0;
        var back = new Point(-vehicle.Direction.X, -vehicle.Direction.Y);

        // Předchozí dlaždice, ze které vozidlo přijelo: odtud se hledají odbočky.
        int fromX = tileX - vehicle.Direction.X;
        int fromY = tileY - vehicle.Direction.Y;

        AddIfRoad(simulation, fromX, fromY, new Point(0, -1), back, options, ref optionCount);
        AddIfRoad(simulation, fromX, fromY, new Point(0, 1), back, options, ref optionCount);
        AddIfRoad(simulation, fromX, fromY, new Point(-1, 0), back, options, ref optionCount);
        AddIfRoad(simulation, fromX, fromY, new Point(1, 0), back, options, ref optionCount);

        if (optionCount == 0)
        {
            return false;
        }

        var chosen = options[Random.Shared.Next(optionCount)];
        vehicle.Direction = chosen;

        // Vrátit doprostřed dlaždice, ze které se odbočuje — jinak by vozidlo
        // po zatáčce jelo mimo silnici.
        const int ts = TerrainRenderer.TileSize;
        vehicle.Position = new Vector2(fromX * ts + ts * 0.5f, fromY * ts + ts * 0.5f);
        return true;
    }

    private static void AddIfRoad(
        Simulation simulation, int fromX, int fromY, Point direction, Point back,
        Span<Point> options, ref int count)
    {
        if (direction == back || !simulation.IsRoad(fromX + direction.X, fromY + direction.Y))
        {
            return;
        }

        options[count++] = direction;
    }

    private void TrySpawn(float dt, Simulation simulation, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        var roads = simulation.RoadTiles;
        if (roads.Count < MinRoadTiles || _count >= DesiredCount(simulation))
        {
            return;
        }

        RefreshEligible(simulation.CurrentEraIndex);
        if (_eligibleDefs.Count == 0)
        {
            return;
        }

        // Zkusí se pár náhodných dlaždic sítě; ta ve výřezu vyhraje. Procházet
        // celou síť by u velkoměsta znamenalo statisíce dlaždic za snímek.
        const int ts = TerrainRenderer.TileSize;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            var tile = roads[Random.Shared.Next(roads.Count)];
            float x = tile.X * ts + ts * 0.5f;
            float y = tile.Y * ts + ts * 0.5f;
            if (x < min.X || x > max.X || y < min.Y || y > max.Y)
            {
                continue;
            }

            var direction = FirstRoadDirection(simulation, tile);
            if (direction == Point.Zero)
            {
                continue; // osamocená dlaždice — není kudy vyjet
            }

            _vehicles[_count++] = new Vehicle
            {
                Position = new Vector2(x, y),
                Direction = direction,
                DefIndex = _eligibleDefs[Random.Shared.Next(_eligibleDefs.Count)],
                Shade = 0.8f + Random.Shared.NextSingle() * 0.4f,
            };
            return;
        }
    }

    /// <summary>Kolik vozidel má být v provozu — provoz roste s městem.</summary>
    private static int DesiredCount(Simulation simulation) =>
        Math.Min(MaxVehicles, (int)(simulation.Population / PeoplePerVehicle));

    private static Point FirstRoadDirection(Simulation simulation, RoadTile tile)
    {
        if (simulation.IsRoad(tile.X + 1, tile.Y)) return new Point(1, 0);
        if (simulation.IsRoad(tile.X - 1, tile.Y)) return new Point(-1, 0);
        if (simulation.IsRoad(tile.X, tile.Y + 1)) return new Point(0, 1);
        if (simulation.IsRoad(tile.X, tile.Y - 1)) return new Point(0, -1);
        return Point.Zero;
    }

    private void RefreshEligible(int eraIndex)
    {
        int order = eraIndex >= 0 ? _content.Eras[eraIndex].Order : 0;
        if (order == _eligibleEra)
        {
            return;
        }

        _eligibleEra = order;
        _eligibleDefs.Clear();
        for (int i = 0; i < _content.Vehicles.Count; i++)
        {
            if (_content.Vehicles[i].FitsEra(order))
            {
                _eligibleDefs.Add(i);
            }
        }
    }

    /// <summary>Dlaždice pod bodem — z ní se pozná, že vozidlo vjelo na další políčko.</summary>
    private static Point TileOf(Vector2 position)
    {
        const int ts = TerrainRenderer.TileSize;
        return new Point((int)MathF.Floor(position.X / ts), (int)MathF.Floor(position.Y / ts));
    }
}
