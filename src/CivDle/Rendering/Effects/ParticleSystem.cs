using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Jednoduché částice (třísky, prach) — čistě vizuální vrstva, simulace o nich neví.
/// Pevný pool struktur bez alokací za běhu (living-map.md: pooling, strop na počet);
/// mrtvá částice se odstraní prohozením s poslední aktivní.
/// </summary>
public sealed class ParticleSystem
{
    private const int MaxParticles = 256;
    private const float Gravity = 380f; // world px/s²

    private struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Age;
        public float Life;
        public float Size;
        public Color Color;
    }

    private readonly Particle[] _particles = new Particle[MaxParticles];
    private int _count;

    /// <summary>Vystřelí dávku částic z bodu do náhodných směrů (třísky po kliku, prach po stavbě).</summary>
    public void SpawnBurst(Vector2 center, Color color, int count, float minSpeed, float maxSpeed)
    {
        for (int i = 0; i < count && _count < MaxParticles; i++)
        {
            float angle = Random.Shared.NextSingle() * MathF.Tau;
            float speed = minSpeed + Random.Shared.NextSingle() * (maxSpeed - minSpeed);
            _particles[_count++] = new Particle
            {
                Position = center,
                // Mírný vzestupný bias, ať to „vystříkne" nahoru a padá gravitací.
                Velocity = new Vector2(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - maxSpeed * 0.4f),
                Age = 0f,
                Life = 0.45f + Random.Shared.NextSingle() * 0.35f,
                Size = 2.5f + Random.Shared.NextSingle() * 2.5f,
                Color = color,
            };
        }
    }

    public void Update(float dt)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            ref var particle = ref _particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.Life)
            {
                _particles[i] = _particles[--_count];
                continue;
            }

            particle.Velocity.Y += Gravity * dt;
            particle.Position += particle.Velocity * dt;
        }
    }

    /// <summary>Kreslí ve world souřadnicích (uvnitř transformace kamery).</summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < _count; i++)
        {
            ref readonly var particle = ref _particles[i];
            float alpha = 1f - particle.Age / particle.Life;
            int size = (int)particle.Size;
            spriteBatch.Draw(
                pixel,
                new Rectangle(
                    (int)(particle.Position.X - size * 0.5f),
                    (int)(particle.Position.Y - size * 0.5f),
                    size,
                    size),
                particle.Color * alpha);
        }

        spriteBatch.End();
    }
}
