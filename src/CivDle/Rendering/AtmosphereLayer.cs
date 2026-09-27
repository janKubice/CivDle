using CivDle.Core.Content;
using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vzduch světa (svety-design.md 5.1): co v něm poletuje (písek na Duně,
/// sníh na Mrazu, popel ve Výhni, spory na Xenu) a polární záře v noci.
/// Druh a hustota jsou v profilu atmosféry; tady je jen, jak to vypadá.
///
/// <para>Částice jsou tentýž bazén jako u ročních období
/// (<see cref="AmbientMotes"/>) — druhý exemplář, ať se atmosféra světa
/// a období nepřetahují o jedno pole. Záře je pár pruhů přes obrazovku, žádná
/// textura: v noci jde o náladu, ne o simulaci.</para>
/// </summary>
public sealed class AtmosphereLayer
{
    private readonly AmbientMotes _motes;
    private float _time;

    public AtmosphereLayer(long seed)
    {
        _motes = new AmbientMotes(seed ^ 0x5A17);
    }

    /// <summary>Jak vypadá a padá druh částic (barva, rychlost pádu, síla větru).</summary>
    public static (Color Color, float Fall, float Wind) Style(string kind) => kind switch
    {
        "sand" => (new Color(222, 190, 138), 0.12f, 2.2f),  // nese ho vítr, skoro nepadá
        "snow" => (new Color(245, 248, 255), 1.0f, 0.6f),
        "ash" => (new Color(128, 122, 118), 0.45f, 0.8f),
        "spores" => (new Color(196, 150, 255), -0.08f, 0.5f), // stoupají, jako by žily
        "pollen" => (new Color(240, 222, 120), 0.08f, 0.7f),
        "mist" => (new Color(236, 226, 208), 0.0f, 1.2f),
        _ => (Color.Transparent, 0f, 0f),
    };

    /// <summary>Posune částice podle profilu a větru.</summary>
    public void Update(float dt, AtmosphereProfile atmosphere, Vector2 min, Vector2 max, float windX, float windY)
    {
        _time += dt;
        if (!atmosphere.HasParticles)
        {
            _motes.Update(dt, min, max, 0, 0, 0, 0);
            return;
        }

        var (_, fall, wind) = Style(atmosphere.Particles);
        _motes.Update(dt, min, max, (float)atmosphere.ParticleDensity, fall, windX * wind, windY * wind);
    }

    /// <summary>Částice ve světě (pod mraky, nad městem).</summary>
    public void DrawParticles(SpriteBatch batch, Camera2D camera, Texture2D pixel, AtmosphereProfile atmosphere)
    {
        if (atmosphere.HasParticles)
        {
            _motes.Draw(batch, camera, pixel, Style(atmosphere.Particles).Color);
        }
    }

    /// <summary>
    /// Polární záře: vlnité zelenofialové pásy přes horní část obrazovky,
    /// jen v noci. Kreslí se přes osvětlenou scénu aditivně — záře je světlo,
    /// ne barva, kterou by noc ztmavila.
    /// </summary>
    public void DrawAurora(SpriteBatch batch, Texture2D pixel, Viewport viewport, AtmosphereProfile atmosphere, float night)
    {
        if (!atmosphere.Aurora || night < 0.05f)
        {
            return;
        }

        batch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.PointClamp);
        var colors = new[] { new Color(80, 255, 170), new Color(140, 120, 255), new Color(70, 220, 200) };
        for (int band = 0; band < colors.Length; band++)
        {
            float baseY = viewport.Height * (0.12f + band * 0.09f);
            float alpha = night * (0.10f - band * 0.02f);
            for (int x = 0; x < viewport.Width; x += 3)
            {
                float wave = MathF.Sin(x * 0.006f + _time * (0.25f + band * 0.07f) + band * 1.7f) * 38f
                    + MathF.Sin(x * 0.017f - _time * 0.4f) * 12f;
                float curtain = 0.6f + 0.4f * MathF.Sin(x * 0.03f + _time * 0.9f + band);
                int height = (int)(70 + 40 * curtain);
                batch.Draw(pixel, new Rectangle(x, (int)(baseY + wave), 3, height), colors[band] * (alpha * curtain));
            }
        }

        batch.End();
    }
}
