using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Megastruktury, které něco dělají: z kosmodromu startují rakety, urychlovač
/// bliskne, orbitální prstenec pulzuje, huť vyšlehne.
///
/// <para>Proč to ve hře je: div světa se stavěl desítky minut a pak jen stál.
/// Odměna, která se odehraje jednou při dostavbě, je odměna, kterou hráč zažije
/// jednou. Tohle z megastruktury dělá místo, kam se vyplatí koukat i potom.</para>
///
/// <para>Vrstva renderu: <b>nic si nepamatuje</b>. Fáze každé podívané je čistá
/// funkce herního času a polohy budovy, takže se nic neukládá, nic se
/// nerozsynchronizuje po načtení savu a systém neplýtvá pamětí u velkoměsta.
/// Rozestavěná megastruktura mlčí — podívaná je odměna za dostavbu.</para>
///
/// <para>Který efekt a jak často, je v datech (<c>spectacle</c> v buildings.json);
/// tahle třída umí jen to <b>jak nakreslit</b> — behavior-ID hook z
/// data-driven-content.md.</para>
/// </summary>
public sealed class SpectacleRenderer
{
    /// <summary>Pod tímhle přiblížením je podívaná pár pixelů — nemá smysl ji kreslit.</summary>
    private const float MinZoom = 0.5f;

    /// <summary>Jak dlouho jeden výstup trvá (sekundy). Zbytek intervalu je klid.</summary>
    private const float ShowSeconds = 2.6f;

    private readonly GameContent _content;

    public SpectacleRenderer(GameContent content)
    {
        _content = content;
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < MinZoom)
        {
            return;
        }

        const int ts = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        var buildings = simulation.Buildings;
        double now = simulation.TickCount / (double)Simulation.TicksPerSecond;

        bool batchOpen = false;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];
            if (def.Spectacle is not { } spectacle || !building.IsComplete)
            {
                continue; // staveniště mlčí — podívaná je odměna za dostavbu
            }

            int x = building.X * ts;
            int y = building.Y * ts;
            int width = def.FootprintWidth * ts;
            int height = def.FootprintHeight * ts;
            if (x + width < min.X || x > max.X || y + height - 400 > max.Y || y > max.Y)
            {
                continue;
            }

            // Posun podle polohy: dvě stejné budovy vedle sebe nemají startovat
            // naráz, jinak to vypadá jako přehrávaná animace, ne jako svět.
            double offset = Offset(building.X, building.Y, spectacle.IntervalSeconds);
            double phase = (now + offset) % spectacle.IntervalSeconds;
            if (phase > ShowSeconds)
            {
                continue;
            }

            float t = (float)(phase / ShowSeconds);
            if (!batchOpen)
            {
                spriteBatch.Begin(
                    blendState: BlendState.Additive,
                    samplerState: SamplerState.PointClamp,
                    transformMatrix: camera.Transform);
                batchOpen = true;
            }

            var center = new Vector2(x + width * 0.5f, y + height * 0.5f);
            Draw(spriteBatch, pixel, spectacle.Effect, center, width, height, t);
        }

        if (batchOpen)
        {
            spriteBatch.End();
        }
    }

    /// <summary>Rozvrhne, kdy která budova hraje — deterministicky z její polohy.</summary>
    private static double Offset(int tileX, int tileY, double interval)
    {
        int hash = HashCode.Combine(tileX, tileY) & 0x7FFFFFFF;
        return hash % 1000 / 1000.0 * interval;
    }

    private static void Draw(
        SpriteBatch spriteBatch, Texture2D pixel, SpectacleEffect effect, Vector2 center, int width, int height, float t)
    {
        switch (effect)
        {
            case SpectacleEffect.RocketLaunch: RocketLaunch(spriteBatch, pixel, center, height, t); break;
            case SpectacleEffect.ParticleBeam: ParticleBeam(spriteBatch, pixel, center, width, t); break;
            case SpectacleEffect.RingPulse: RingPulse(spriteBatch, pixel, center, width, t); break;
            case SpectacleEffect.ForgeFlare: ForgeFlare(spriteBatch, pixel, center, height, t); break;
            case SpectacleEffect.SpireBeacon: SpireBeacon(spriteBatch, pixel, center, height, t); break;
        }
    }

    /// <summary>
    /// Start rakety: první třetina je odpal na zemi, zbytek stoupání s vlečkou.
    /// Rozdělení je schválně — bez té chvilky na zemi by raketa jen problikla.
    /// </summary>
    private static void RocketLaunch(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int height, float t)
    {
        const float IgnitionPart = 0.28f;

        if (t < IgnitionPart)
        {
            float glow = 1f - t / IgnitionPart;
            int size = (int)(height * (0.5f + 0.4f * glow));
            spriteBatch.Draw(
                pixel,
                new Rectangle((int)center.X - size / 2, (int)(center.Y - size / 4), size, size / 2),
                new Color(255, 170, 70) * (0.55f * glow));
            return;
        }

        float rise = (t - IgnitionPart) / (1f - IgnitionPart);
        float fade = 1f - rise * rise;
        float y = center.Y - rise * 620f;

        // Vlečka: pár kotoučů kouře pod raketou, řidších směrem dolů.
        for (int i = 1; i <= 5; i++)
        {
            float trailY = y + i * 26f;
            if (trailY > center.Y)
            {
                break;
            }

            int puff = 6 + i;
            spriteBatch.Draw(
                pixel,
                new Rectangle((int)center.X - puff / 2, (int)trailY, puff, puff),
                new Color(255, 190, 120) * (0.30f * fade / i));
        }

        spriteBatch.Draw(pixel, new Rectangle((int)center.X - 2, (int)y - 8, 4, 12), Color.White * fade);
        spriteBatch.Draw(pixel, new Rectangle((int)center.X - 3, (int)y + 4, 6, 8), new Color(255, 200, 110) * fade);
    }

    /// <summary>Urychlovač: prstenec se rozsvítí a na vrcholu bliskne.</summary>
    private static void ParticleBeam(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int width, float t)
    {
        // Náběh a doznění: sinus přes celý výstup, špička uprostřed.
        float power = MathF.Sin(t * MathF.PI);
        int radius = (int)(width * 0.45f);

        // Prstenec: čtyři oblouky naznačené obdélníky (na téhle velikosti stačí).
        var glow = new Color(140, 245, 210) * (0.45f * power);
        spriteBatch.Draw(pixel, new Rectangle((int)center.X - radius, (int)center.Y - 2, radius * 2, 4), glow);
        spriteBatch.Draw(pixel, new Rectangle((int)center.X - 2, (int)center.Y - radius, 4, radius * 2), glow);

        // Záblesk na vrcholu — to je ten okamžik, kvůli kterému se hráč otočí.
        if (power > 0.85f)
        {
            int flash = (int)(width * 1.1f * (power - 0.85f) / 0.15f);
            spriteBatch.Draw(
                pixel,
                new Rectangle((int)center.X - flash / 2, (int)center.Y - flash / 2, flash, flash),
                new Color(220, 255, 245) * 0.5f);
        }
    }

    /// <summary>Orbitální prstenec: kruhová vlna, která se rozjede přes okolí.</summary>
    private static void RingPulse(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int width, float t)
    {
        float fade = 1f - t;
        int size = (int)(width * (0.6f + t * 4.5f));

        // Obrys místo výplně: vlna má projít krajinou, ne ji zakrýt.
        var color = new Color(120, 220, 255) * (0.4f * fade);
        int left = (int)center.X - size / 2;
        int top = (int)center.Y - size / 4;
        spriteBatch.Draw(pixel, new Rectangle(left, top, size, 2), color);
        spriteBatch.Draw(pixel, new Rectangle(left, top + size / 2, size, 2), color);
        spriteBatch.Draw(pixel, new Rectangle(left, top, 2, size / 2), color);
        spriteBatch.Draw(pixel, new Rectangle(left + size - 2, top, 2, size / 2), color);
    }

    /// <summary>Huť světa: výšleh roztaveného kovu z komína.</summary>
    private static void ForgeFlare(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int height, float t)
    {
        float power = MathF.Sin(t * MathF.PI);
        int flame = (int)(height * 0.8f * power);
        if (flame < 2)
        {
            return;
        }

        spriteBatch.Draw(
            pixel,
            new Rectangle((int)center.X - 4, (int)(center.Y - height * 0.4f) - flame, 8, flame),
            new Color(255, 140, 60) * (0.55f * power));
        spriteBatch.Draw(
            pixel,
            new Rectangle((int)center.X - 2, (int)(center.Y - height * 0.4f) - flame, 4, flame),
            new Color(255, 230, 160) * (0.6f * power));
    }

    /// <summary>Maják: pomalé nadechnutí světla na vrcholu věže.</summary>
    private static void SpireBeacon(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int height, float t)
    {
        float power = MathF.Sin(t * MathF.PI);
        int size = (int)(height * (0.3f + 0.35f * power));
        float top = center.Y - height * 0.45f;

        spriteBatch.Draw(
            pixel,
            new Rectangle((int)center.X - size / 2, (int)top - size / 2, size, size),
            new Color(255, 225, 150) * (0.35f * power));
    }
}
