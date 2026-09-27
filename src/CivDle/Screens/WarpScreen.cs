using CivDle.Core.Content;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Screens;

/// <summary>
/// Krátký přelet mezi světy (svety-design.md 5.5): hvězdy se roztáhnou do
/// čar, uprostřed vyroste cílová planeta a obraz přejde do jejích mraků.
///
/// <para>Trvá necelé dvě sekundy a klik nebo Escape ho přeskočí — přepínání
/// mezi světy se dělá často a nesmí zdržovat. Nová herní obrazovka vzniká až
/// na konci (<c>next</c>), takže přelet běží, i když se svět načítá.</para>
/// </summary>
public sealed class WarpScreen : IScreen
{
    /// <summary>Délka přeletu v sekundách.</summary>
    public const float DurationSeconds = 1.6f;

    private const int StarCount = 260;

    private readonly ScreenManager _screens;
    private readonly WorldDef _target;
    private readonly Func<IScreen> _next;
    private readonly InputManager _input = new();
    private readonly PlanetDisk _planet;
    private float _time;
    private bool _done;

    public WarpScreen(ScreenManager screens, WorldDef target, Func<IScreen> next)
    {
        _screens = screens;
        _target = target;
        _next = next;
        var look = target.Planet;
        _planet = new PlanetDisk(
            screens.GraphicsDevice, PlanetSurface.FromLook(look, target.Order * 7919L), 256,
            new Color(look.Accent.R, look.Accent.G, look.Accent.B));
    }

    public bool IsOverlay => false;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _time += dt;
        _planet.Update(dt, 0.05f);
        if (!_done && (_time >= DurationSeconds || _input.WasPressed(Keys.Escape) || _input.WasLeftPressed))
        {
            _done = true;
            _screens.ReplaceAll(_next());
        }
    }

    public void Draw(GameTime gameTime)
    {
        var batch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var viewport = _screens.GraphicsDevice.Viewport;
        var center = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        float t = Math.Clamp(_time / DurationSeconds, 0f, 1f);

        batch.Begin(samplerState: SamplerState.PointClamp);
        batch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(4, 6, 14));

        // Hvězdy se natahují od středu — rychlost roste v první polovině
        // a v druhé zase klesá, jak loď brzdí u cíle.
        float speed = MathF.Sin(t * MathF.PI);
        for (int i = 0; i < StarCount; i++)
        {
            uint hash = (uint)(i * 2654435761u);
            float angle = (hash % 6283) / 1000f;
            float distance = ((hash >> 12) % 1000) / 1000f;
            float travel = (distance + _time * (0.3f + speed * 1.6f)) % 1f;
            float radius = travel * travel * Math.Max(viewport.Width, viewport.Height) * 0.7f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var start = center + dir * radius;
            float length = 2f + speed * 40f * travel;
            DrawLine(batch, pixel, start, start + dir * length, Color.White * (0.25f + travel * 0.7f));
        }

        // Cílová planeta roste z tečky do celé obrazovky, na konci ji přikryjí mraky světa.
        float grow = t * t;
        float diameter = 16f + grow * Math.Max(viewport.Width, viewport.Height) * 1.4f;
        float scale = diameter / _planet.Diameter;
        var look = _target.Planet;
        batch.End();

        batch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix:
            Matrix.CreateTranslation(-center.X, -center.Y, 0) * Matrix.CreateScale(scale) * Matrix.CreateTranslation(center.X, center.Y, 0));
        _planet.Draw(batch, center);
        batch.End();

        batch.Begin(samplerState: SamplerState.PointClamp);
        float cloud = Math.Clamp((t - 0.7f) / 0.3f, 0f, 1f);
        if (cloud > 0)
        {
            var mist = Color.Lerp(new Color(look.Accent.R, look.Accent.G, look.Accent.B), Color.White, 0.5f);
            batch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), mist * cloud);
        }

        batch.End();
    }

    public void Dispose() => _planet.Dispose();

    private static void DrawLine(SpriteBatch batch, Texture2D pixel, Vector2 from, Vector2 to, Color color)
    {
        var delta = to - from;
        float length = delta.Length();
        if (length < 0.5f)
        {
            return;
        }

        batch.Draw(pixel, from, null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(length, 1f), SpriteEffects.None, 0f);
    }
}
