using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Obchodní karavana, která přijede po hráčových silnicích do města a zaplatí
/// za cestu. Klikáním ji hráč doprovází — každý klik zvedne výplatu.
///
/// <para>Je to ta „minihra", která nepotřebuje vlastní obrazovku: odehrává se
/// na mapě, nic nepozastaví a odměňuje pozornost, ne reflexy. Hlavně ale dává
/// smysl ručně stavěným silnicím — čím delší souvislou trasu karavana projede,
/// tím větší výplata.</para>
///
/// <para>Tahle třída řeší jen <b>kdy</b> karavana přijede a <b>jak se kreslí</b>;
/// samotná cesta a výplata jsou v <see cref="CaravanRun"/>, aby šly otestovat
/// bez grafiky.</para>
///
/// <para>Vrstva renderu (stejně jako sběrné bubliny): čte simulaci a zapíše do ní
/// jen výsledek jako příkaz hráče. Nic se neukládá — rozjetá karavana po načtení
/// savu prostě není, a to nikomu nechybí.</para>
/// </summary>
public sealed class CaravanSystem
{
    private const float SpawnIntervalSeconds = 95f;
    private const float SpeedTilesPerSecond = 2.2f;
    private const float MaxLifeSeconds = 70f;
    private const float ClickRadiusTiles = 1.1f;

    /// <summary>Kolik dlaždic silnice musí síť mít, aby mělo smysl karavanu poslat.</summary>
    private const int MinRoadTiles = 6;

    private readonly SpriteLibrary _sprites;
    private readonly Random _rng = new();

    private CaravanRun? _run;

    /// <summary>Který soused tuhle karavanu poslal; −1 = anonymní (data bez sousedů).</summary>
    private int _neighbourIndex = -1;
    private float _age;
    private float _stepTimer;
    private float _spawnTimer = SpawnIntervalSeconds * 0.4f; // první přijede dřív

    public CaravanSystem(SpriteLibrary sprites, GameContent content)
    {
        _sprites = sprites;
        _ = content; // obsah zatím netřeba; parametr drží tvar ostatních systémů
    }

    /// <summary>Je karavana zrovna na cestě?</summary>
    public bool IsActive => _run is not null;

    public void Update(float dt, Simulation simulation)
    {
        if (_run is not null)
        {
            Advance(dt, simulation);
            return;
        }

        _spawnTimer -= dt;
        if (_spawnTimer <= 0f)
        {
            _spawnTimer = SpawnIntervalSeconds;
            TrySpawn(simulation);
        }
    }

    /// <summary>
    /// Klik hráče na karavanu = doprovod. Vrací true, když se klik trefil —
    /// herní obrazovka pak nespustí ruční těžbu na téhle dlaždici.
    /// </summary>
    public bool TryEscort(Vector2 worldPosition, out Vector2 caravanPosition)
    {
        caravanPosition = Vector2.Zero;
        if (_run is null)
        {
            return false;
        }

        caravanPosition = _run.Position;
        float radius = ClickRadiusTiles * TerrainRenderer.TileSize;
        if (Vector2.DistanceSquared(worldPosition, caravanPosition) > radius * radius)
        {
            return false;
        }

        _run.Escort();
        return true;
    }

    /// <summary>
    /// Doputovala karavana do města? Vrací výplatu a místo, kde se vyplatila —
    /// herní obrazovka z toho udělá popup a připíše suroviny.
    /// </summary>
    public bool TryCollectArrival(
        Simulation simulation, out int resourceIndex, out int amount, out Vector2 position, out int neighbourIndex)
    {
        resourceIndex = -1;
        amount = 0;
        position = Vector2.Zero;
        neighbourIndex = _neighbourIndex;

        if (_run is null || !_run.HasArrived(simulation))
        {
            return false;
        }

        position = _run.Position;
        resourceIndex = CaravanRun.ScarcestKnownResource(simulation);
        amount = _run.Payout();
        _run = null;

        return resourceIndex >= 0 && amount > 0;
    }

    private void TrySpawn(Simulation simulation)
    {
        var roads = simulation.RoadTiles;
        if (roads.Count < MinRoadTiles)
        {
            return; // pár dlaždic není síť — karavana by neměla kudy jet
        }

        // Start co nejdál od města: cesta pak bude dlouhá a výplata za ni stojí.
        var best = roads[0];
        long bestDistance = -1;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var candidate = roads[_rng.Next(roads.Count)];
            long dx = candidate.X - simulation.CityCenterX;
            long dy = candidate.Y - simulation.CityCenterY;
            long distance = dx * dx + dy * dy;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        _run = new CaravanRun(best.X, best.Y);
        // Kdo ji posílá, rozhoduje simulace: vztahy jsou její stav, ne renderu.
        _neighbourIndex = simulation.PickNeighbour();
        _age = 0f;
        _stepTimer = 0f;
    }

    private void Advance(float dt, Simulation simulation)
    {
        _age += dt;
        if (_age >= MaxLifeSeconds)
        {
            _run = null; // zabloudila nebo síť nikam nevede — odjede bez placení
            return;
        }

        _stepTimer -= dt;
        if (_stepTimer > 0f)
        {
            return;
        }

        _stepTimer = 1f / SpeedTilesPerSecond;

        if (_run!.HasArrived(simulation))
        {
            return; // stojí u města a čeká na vyzvednutí
        }

        if (!_run.TryStepTowardsCity(simulation))
        {
            _run = null; // slepá ulička
        }
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_run is null)
        {
            return;
        }

        var sprite = _sprites.Get("agent.caravan") ?? _sprites.Get("agent.cart");
        if (sprite is null)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        // Doprovázená karavana zezlátne — hráč vidí, že jeho klikání něco dělá.
        float warmth = _run.EscortClicks / (float)CaravanRun.MaxEscortClicks;
        var tint = Color.Lerp(Color.White, new Color(255, 220, 140), warmth);
        var origin = new Vector2(sprite.Width * 0.5f, sprite.Height);
        var effect = _run.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        spriteBatch.Draw(sprite, _run.Position, null, tint, 0f, origin, 1f, effect, 0f);

        spriteBatch.End();
    }
}
