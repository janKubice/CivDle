using CivDle.Core.Content;
using CivDle.Rendering.Sprites;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Kreslí průvodce prvními minutami do světa: táborák na místě startu,
/// pulzující kroužek a poskakující šipku nad tím, na co má hráč kliknout
/// (první strom, místo pro první dům).
///
/// <para>Proč ve světě a ne v UI: „klikni na strom" v panelu vlevo nutí hráče
/// hledat, <em>který</em> strom. Šipka přímo nad ním tu otázku nepustí ke slovu.</para>
///
/// <para>Čistě render — co a kam ukázat, rozhoduje <see cref="OnboardingGuide"/>.</para>
/// </summary>
internal sealed class GuideRenderer
{
    private readonly SpriteLibrary _sprites;
    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private float _time;

    public GuideRenderer(SpriteLibrary sprites, Texture2D whitePixel, GameContent content)
    {
        _sprites = sprites;
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Posune animace (plamen, pulz, poskakování šipky).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Vykreslí táborák a ukazatel. <paramref name="calm"/> = hráč si vypnul
    /// pohyb: kroužek nepulzuje a šipka stojí, ale ukazuje dál.
    /// </summary>
    /// <param name="spriteBatch">Kreslení.</param>
    /// <param name="camera">Kamera.</param>
    /// <param name="guide">Co a kde ukázat.</param>
    /// <param name="showPointer">Kreslit ukazatel? (Po průvodci už ne — jen táborák.)</param>
    /// <param name="calm">Omezený pohyb.</param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, OnboardingGuide guide, bool showPointer, bool calm)
    {
        if (!guide.ShowCampfire && (!showPointer || guide.Target.Kind == GuidePointer.None))
        {
            return; // nic ke kreslení — ani Begin/End zbytečně
        }

        int tile = TerrainRenderer.TileSize;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        if (guide.ShowCampfire && _sprites.Get("fx.campfire") is { } fire)
        {
            var (x, y) = guide.StartTile;
            var center = new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile);

            // Záře pod ohněm: v noci je to jediné světlo v krajině.
            float flicker = calm ? 1f : 0.85f + 0.15f * MathF.Sin(_time * 11f) * MathF.Sin(_time * 4.3f);
            DrawGlow(spriteBatch, center, tile * 1.6f * flicker, new Color(255, 170, 70) * 0.18f);
            spriteBatch.Draw(fire, center, null, Color.White, 0f,
                new Vector2(fire.Width * 0.5f, fire.Height * 0.62f), tile * 1.1f / fire.Width, SpriteEffects.None, 0f);
        }

        var target = guide.Target;
        if (showPointer && target.Kind != GuidePointer.None)
        {
            float width = 1f, height = 1f;
            if (target.Kind == GuidePointer.Build && target.DefIndex >= 0)
            {
                var def = _content.Buildings[target.DefIndex];
                width = def.FootprintWidth;
                height = def.FootprintHeight;
                DrawFootprint(spriteBatch, target.X * tile, target.Y * tile, width * tile, height * tile, calm);
            }

            var center = new Vector2((target.X + width * 0.5f) * tile, (target.Y + height * 0.5f) * tile);
            DrawRing(spriteBatch, center, MathF.Max(width, height) * tile, calm);
            DrawArrow(spriteBatch, center - new Vector2(0f, height * tile * 0.5f), calm);
        }

        spriteBatch.End();
    }

    private void DrawRing(SpriteBatch spriteBatch, Vector2 center, float size, bool calm)
    {
        if (_sprites.Get("fx.ring") is not { } ring)
        {
            return;
        }

        // Dva kroužky rozbíhající se od sebe: pohyb chytí oko i v hustém lese.
        for (int i = 0; i < 2; i++)
        {
            float phase = calm ? 0.35f : (_time * 0.9f + i * 0.5f) % 1f;
            float scale = size * (1.1f + phase * 0.9f) / ring.Width;
            float alpha = calm ? 0.9f : 1f - phase;
            spriteBatch.Draw(ring, center, null, Color.White * alpha, 0f,
                new Vector2(ring.Width * 0.5f, ring.Height * 0.5f), scale, SpriteEffects.None, 0f);
        }
    }

    private void DrawArrow(SpriteBatch spriteBatch, Vector2 tip, bool calm)
    {
        if (_sprites.Get("fx.arrow") is not { } arrow)
        {
            return;
        }

        int tile = TerrainRenderer.TileSize;
        float bob = calm ? 0f : MathF.Abs(MathF.Sin(_time * 3.2f)) * tile * 0.45f;
        float size = tile * 1.1f;
        var position = tip - new Vector2(0f, tile * 0.25f + bob);
        spriteBatch.Draw(arrow, position, null, Color.White, 0f,
            new Vector2(arrow.Width * 0.5f, arrow.Height), size / arrow.Width, SpriteEffects.None, 0f);
    }

    /// <summary>Obrys doporučeného půdorysu — ať je vidět, kam přesně budova padne.</summary>
    private void DrawFootprint(SpriteBatch spriteBatch, float x, float y, float width, float height, bool calm)
    {
        float pulse = calm ? 0.8f : 0.55f + 0.45f * (0.5f + 0.5f * MathF.Sin(_time * 4f));
        var fill = new Color(255, 226, 130) * (0.18f * pulse);
        var edge = new Color(255, 226, 130) * pulse;
        const float line = 1.5f;
        spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), fill);
        spriteBatch.Draw(_pixel, new Vector2(x, y), null, edge, 0f, Vector2.Zero, new Vector2(width, line), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, new Vector2(x, y + height - line), null, edge, 0f, Vector2.Zero, new Vector2(width, line), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, new Vector2(x, y), null, edge, 0f, Vector2.Zero, new Vector2(line, height), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, new Vector2(x + width - line, y), null, edge, 0f, Vector2.Zero, new Vector2(line, height), SpriteEffects.None, 0f);
    }

    private void DrawGlow(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        if (_sprites.Mask("fx.shadow") is not { } soft)
        {
            return;
        }

        // Bílá maska měkkého stínu je radiální doběh alfy — obarvená na teplo dělá záři.
        spriteBatch.Draw(soft, center, null, color, 0f,
            new Vector2(soft.Width * 0.5f, soft.Height * 0.5f), radius * 2f / soft.Width, SpriteEffects.None, 0f);
    }
}
