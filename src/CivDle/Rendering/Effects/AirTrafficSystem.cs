using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Létající kulisa: balony, vzducholodě, letadla a raketoplány nad mapou
/// (bod 42 — „letadla a balony létají po mapě, jako rybářské lodičky").
///
/// <para>Proč vlastní systém a ne další druh v <see cref="AgentSystem"/>:
/// letoun se neptá na terén ani na silnice, letí rovně přes všechno a kreslí
/// se <b>nad</b> zástavbou i se stínem na zemi. Vecpat to do pozemního agenta
/// by znamenalo v každé jeho metodě větev „a co když je to letadlo".</para>
///
/// <para>Čistě render vrstva: simulace o letadlech neví a nikdy vědět nebude
/// (CLAUDE.md — render čte ze simulace, nikdy do ní nezapisuje).</para>
/// </summary>
public sealed class AirTrafficSystem
{
    /// <summary>Strop počtu letounů. Nebe má být živé, ne ucpané.</summary>
    private const int MaxAircraft = 12;

    /// <summary>Pod tímhle zoomem se nekreslí — z výšky jsou to jen tečky.</summary>
    private const float MinZoom = 0.4f;

    private const float SpawnCooldownSeconds = 1.5f;

    /// <summary>Jak daleko za okrajem pohledu letoun zmizí.</summary>
    private const float DespawnMargin = 400f;

    /// <summary>Jak daleko od domovské budovy si letoun volí cíl.</summary>
    private const float CruiseTiles = 40f;

    private struct Aircraft
    {
        public Vector2 Position;
        public Vector2 Target;
        public int DefIndex;
        public float Phase;
        public bool FaceLeft;
    }

    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;
    private readonly Aircraft[] _aircraft = new Aircraft[MaxAircraft];
    private int _count;
    private float _spawnTimer;

    public AirTrafficSystem(GameContent content, SpriteLibrary sprites)
    {
        _content = content;
        _sprites = sprites;
    }

    /// <summary>Kolik strojů je zrovna ve vzduchu (pro testy a ladění).</summary>
    public int Count => _count;

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        if (_content.Aircraft.Count == 0 || camera.Zoom < MinZoom)
        {
            _count = 0;
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        UpdateAircraft(dt, min, max);
        TrySpawn(dt, simulation, min, max);
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
            ref readonly var craft = ref _aircraft[i];
            var def = _content.Aircraft[craft.DefIndex];
            var sprite = _sprites.Get(def.SpriteId);
            if (sprite is null)
            {
                continue;
            }

            var origin = new Vector2(sprite.Width * 0.5f, sprite.Height * 0.5f);
            var effect = craft.FaceLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Stín na zemi je to, co dělá výšku. Bez něj letadlo vypadá jako
            // podivně rychlá kára, která projíždí domy.
            spriteBatch.Draw(sprite, craft.Position, null, new Color(0, 0, 0, 70),
                0f, origin, 0.8f, effect, 0f);

            // Vlastní stroj o „výšku" výš; balon se pomalu pohupuje.
            float bob = MathF.Sin(craft.Phase * 0.9f) * 2.5f;
            spriteBatch.Draw(sprite, new Vector2(craft.Position.X, craft.Position.Y - def.Altitude + bob),
                null, Color.White, 0f, origin, 1f, effect, 0f);
        }

        spriteBatch.End();
    }

    private void UpdateAircraft(float dt, Vector2 min, Vector2 max)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var craft = ref _aircraft[i];
            var def = _content.Aircraft[craft.DefIndex];
            craft.Phase += dt;

            var toTarget = craft.Target - craft.Position;
            float distance = toTarget.Length();
            if (distance > 0.01f)
            {
                var step = toTarget / distance * def.Speed * dt;
                craft.Position += step;
                craft.FaceLeft = step.X < 0f;
            }

            // Letoun necouvá a neotáčí se na místě: doletí a zmizí za obzorem.
            // Nový vzlétne od budovy, což vypadá jako provoz, ne jako kolotoč.
            bool arrived = distance < 8f;
            bool outOfView = craft.Position.X < min.X - DespawnMargin || craft.Position.X > max.X + DespawnMargin
                || craft.Position.Y < min.Y - DespawnMargin || craft.Position.Y > max.Y + DespawnMargin;
            if (arrived || outOfView)
            {
                _aircraft[i] = _aircraft[--_count];
            }
        }
    }

    private void TrySpawn(float dt, Simulation simulation, Vector2 min, Vector2 max)
    {
        _spawnTimer -= dt;
        if (_spawnTimer > 0f || _count >= MaxAircraft)
        {
            return;
        }

        _spawnTimer = SpawnCooldownSeconds;

        var buildings = simulation.Buildings;
        if (buildings.Length == 0)
        {
            return;
        }

        int eraIndex = simulation.CurrentEraIndex;
        int era = eraIndex >= 0 ? _content.Eras[eraIndex].Order : 0;

        // Od náhodné budovy dokola: hledá se ta, která zrovna umí něco vypustit.
        // Letišť je pár mezi stovkami domů, takže rovnoměrný los by na ně
        // prakticky nedosáhl (stejná past jako kdysi u rybářských loděk).
        int start = Random.Shared.Next(buildings.Length);
        for (int step = 0; step < buildings.Length; step++)
        {
            ref readonly var building = ref buildings[(start + step) % buildings.Length];
            var from = new Vector2(
                (building.X + 0.5f) * TerrainRenderer.TileSize,
                (building.Y + 0.5f) * TerrainRenderer.TileSize);
            if (from.X < min.X || from.X > max.X || from.Y < min.Y || from.Y > max.Y)
            {
                continue;
            }

            int defIndex = PickAircraftFor(building.DefIndex, era);
            if (defIndex < 0)
            {
                continue;
            }

            Launch(defIndex, from);
            return;
        }
    }

    /// <summary>
    /// Který letoun smí vzlétnout od téhle budovy v téhle éře. −1 = žádný.
    /// </summary>
    private int PickAircraftFor(int buildingDefIndex, int era)
    {
        // Losuje se mezi VŠEMI vhodnými, ne první nalezený: jinak by od letiště
        // vzlétal pořád ten samý typ a nebe by bylo jednotvárné.
        Span<int> matches = stackalloc int[MaxAircraft];
        int found = 0;
        for (int i = 0; i < _content.Aircraft.Count && found < matches.Length; i++)
        {
            var def = _content.Aircraft[i];
            if (!def.FitsEra(era))
            {
                continue;
            }

            if (def.NeedsHomeBuilding && def.HomeBuildingIndex != buildingDefIndex)
            {
                continue;
            }

            matches[found++] = i;
        }

        return found == 0 ? -1 : matches[Random.Shared.Next(found)];
    }

    private void Launch(int defIndex, Vector2 from)
    {
        // Cíl je bod za obzorem ve zvoleném směru — letoun proletí a odletí.
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        var target = from + new Vector2(MathF.Cos(angle), MathF.Sin(angle))
            * CruiseTiles * TerrainRenderer.TileSize;

        _aircraft[_count++] = new Aircraft
        {
            Position = from,
            Target = target,
            DefIndex = defIndex,
            Phase = Random.Shared.NextSingle() * 10f,
        };
    }
}
