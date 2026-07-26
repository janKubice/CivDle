using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení počasí přes scénu (living-map.md §7 — „počasí je levné, jen musí být
/// cílené"): barevný závoj podle jevu plus pooled částice (déšť / sníh / písek).
/// Částice se recyklují v pevném poli — žádné alokace za běhu.
///
/// Čte jen ze simulace (jaký jev běží), nikdy do ní nezapisuje.
/// </summary>
public sealed class WeatherRenderer
{
    private const int MaxParticles = 320;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Vector2[] _positions = new Vector2[MaxParticles];
    private readonly Vector2[] _velocities = new Vector2[MaxParticles];
    private int _activeCount;
    private int _activeWeather = -1;

    public WeatherRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Posune částice; při změně jevu přenastaví jejich počet a rychlost.</summary>
    public void Update(float dt, Simulation simulation, Viewport viewport)
    {
        int weather = simulation.CurrentWeatherIndex;
        if (weather != _activeWeather)
        {
            _activeWeather = weather;
            Reset(weather, viewport);
        }

        for (int i = 0; i < _activeCount; i++)
        {
            _positions[i] += _velocities[i] * dt;

            // Recyklace: co vypadne dole nebo po straně, se vrátí nahoru (pooling).
            if (_positions[i].Y > viewport.Height || _positions[i].X > viewport.Width || _positions[i].X < -20)
            {
                _positions[i] = new Vector2(Random.Shared.Next(-40, viewport.Width + 40), -10);
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_activeWeather < 0)
        {
            return;
        }

        var def = _content.Weather[_activeWeather];
        spriteBatch.Begin();

        if (def.TintAlpha > 0)
        {
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height),
                def.TintColor.ToXna() * (float)def.TintAlpha);
        }

        var (size, color) = def.Particle switch
        {
            "rain" => (new Point(1, 9), new Color(180, 205, 235) * 0.75f),
            "snow" => (new Point(3, 3), Color.White * 0.85f),
            "sand" => (new Point(4, 2), new Color(214, 180, 120) * 0.7f),
            _ => (Point.Zero, Color.Transparent),
        };

        if (size != Point.Zero)
        {
            for (int i = 0; i < _activeCount; i++)
            {
                spriteBatch.Draw(_pixel,
                    new Rectangle((int)_positions[i].X, (int)_positions[i].Y, size.X, size.Y), color);
            }
        }

        spriteBatch.End();
    }

    private void Reset(int weather, Viewport viewport)
    {
        if (weather < 0)
        {
            _activeCount = 0;
            return;
        }

        var def = _content.Weather[weather];
        // Hustota a směr podle druhu srážek — déšť padá rychle a šikmo, sníh se snáší.
        (_activeCount, var velocity) = def.Particle switch
        {
            "rain" => (def.Extreme ? MaxParticles : 160, new Vector2(-90f, 900f)),
            "snow" => (def.Extreme ? MaxParticles : 120, new Vector2(-30f, 120f)),
            "sand" => (MaxParticles, new Vector2(-520f, 90f)),
            _ => (0, Vector2.Zero),
        };

        for (int i = 0; i < _activeCount; i++)
        {
            _positions[i] = new Vector2(
                Random.Shared.Next(-40, viewport.Width + 40),
                Random.Shared.Next(-viewport.Height, viewport.Height));
            float jitter = 0.75f + (float)Random.Shared.NextDouble() * 0.5f;
            _velocities[i] = velocity * jitter;
        }
    }
}
