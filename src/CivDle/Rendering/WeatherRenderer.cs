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

    /// <summary>Jak dlouho trvá, než počasí naplno nastoupí nebo odezní (v sekundách).</summary>
    private const float FadeSeconds = 3.5f;

    /// <summary>
    /// Nakolik je jev naplno (0–1). Bez tohohle skočil déšť z ničeho na plnou
    /// sílu mezi dvěma snímky — obloha se přebarvila jako přepnutý vypínač
    /// a bylo to to nejméně přirozené, co se v krajině dělo.
    /// </summary>
    private float _strength;

    /// <summary>Jev, ke kterému se odchází — dokud nevyprchá, kreslí se pořád on.</summary>
    private int _fadingOut = -1;

    /// <summary>Posune částice; při změně jevu přenastaví jejich počet a rychlost.</summary>
    public void Update(float dt, Simulation simulation, Viewport viewport)
    {
        int weather = simulation.CurrentWeatherIndex;

        if (weather != _activeWeather)
        {
            // Nový jev nenastoupí, dokud ten starý neodezní. Prolínat dva různé
            // efekty naráz by znamenalo déšť a sníh přes sebe.
            if (_strength > 0f && _fadingOut < 0)
            {
                _fadingOut = _activeWeather;
            }

            _strength -= dt / FadeSeconds;
            if (_strength <= 0f)
            {
                _strength = 0f;
                _fadingOut = -1;
                _activeWeather = weather;
                Reset(weather, viewport);
            }
        }
        else if (_strength < 1f)
        {
            _strength = Math.Min(1f, _strength + dt / FadeSeconds);
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
        // Dokud starý jev doznívá, kreslí se pořád on — jen slabší.
        int shown = _fadingOut >= 0 ? _fadingOut : _activeWeather;
        if (shown < 0 || _strength <= 0f)
        {
            return;
        }

        var def = _content.Weather[shown];
        spriteBatch.Begin();

        if (def.TintAlpha > 0)
        {
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height),
                def.TintColor.ToXna() * ((float)def.TintAlpha * _strength));
        }

        var (size, color) = def.Particle switch
        {
            "rain" => (new Point(1, 9), new Color(180, 205, 235) * (0.75f * _strength)),
            "snow" => (new Point(3, 3), Color.White * (0.85f * _strength)),
            "sand" => (new Point(4, 2), new Color(214, 180, 120) * (0.7f * _strength)),
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
