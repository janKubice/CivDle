using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Noční světla města (fáze 5: „v noci se město rozsvítí" — wow moment
/// z game-feel-wow.md). Každá budova dostane teplou aditivní zář a pár
/// rozsvícených okének; síla roste s hloubkou noci. Při oddálení se z okének
/// stávají světelné body — pohled na rozsvícenou civilizaci shora.
/// </summary>
public sealed class LightsRenderer
{
    private static readonly Color GlowColor = new(255, 190, 100);
    private static readonly Color WindowColor = new(255, 233, 168);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    /// <summary>Čas pro mihotání oken. Jediný stav rendereru.</summary>
    private float _time;

    public LightsRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>
    /// Posune mihotání oken.
    ///
    /// <para>Bez pohybu byla noc jen statická tečkovaná mapa. Skutečné město
    /// v noci žije: někde svítí televize, někde někdo zhasne. Stačí, aby se
    /// pár oken pomalu měnilo, a z obrázku je scéna.</para>
    /// </summary>
    public void Update(float dt) => _time += dt;

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, float nightFactor)
    {
        if (nightFactor <= 0.02f)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        var buildings = simulation.Buildings;

        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];
            int width = def.FootprintWidth * tileSize;
            int height = def.FootprintHeight * tileSize;
            int x = building.X * tileSize;
            int y = building.Y * tileSize;
            if (x + width < min.X - 32 || x > max.X + 32 || y + height < min.Y - 32 || y > max.Y + 32)
            {
                continue;
            }

            // Měkká zář: tři soustředné vrstvy s klesající intenzitou.
            DrawCenteredGlow(spriteBatch, x + width / 2, y + height / 2, width + 26, height + 26, 0.07f * nightFactor);
            DrawCenteredGlow(spriteBatch, x + width / 2, y + height / 2, width + 12, height + 12, 0.12f * nightFactor);
            DrawCenteredGlow(spriteBatch, x + width / 2, y + height / 2, width, height, 0.18f * nightFactor);

            // Rozsvícená okénka. Klíč je POLOHA, ne index v poli: index se při
            // bourání přerovná a celá ulice by v tu chvíli přeblikla.
            int key = building.X * 73856093 ^ building.Y * 19349663;
            int windows = 2 + (int)(Hash(key, 0) % 3);
            for (int w = 0; w < windows; w++)
            {
                int windowX = x + 3 + (int)(Hash(key, w * 2 + 1) % (ulong)Math.Max(1, width - 6));
                int windowY = y + 3 + (int)(Hash(key, w * 2 + 2) % (ulong)Math.Max(1, height - 6));
                float alpha = WindowAlpha(key + w, _time) * nightFactor;
                if (alpha <= 0.01f)
                {
                    continue;
                }

                spriteBatch.Draw(_pixel, new Rectangle(windowX, windowY, 2, 2), WindowColor * alpha);
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Jak silně svítí jedno okno v daném čase (0–1).
    ///
    /// <para>Skládá se ze dvou pomalých vln s nesoudělnými periodami. Výsledek
    /// nikdy nezhasne úplně a nikdy se necyklí tak krátce, aby si hráč všiml
    /// opakování — což je celý rozdíl mezi „město v noci" a „blikající LEDka".
    /// Fáze je odvozená ze seedu, takže dvě okna vedle sebe nepulzují spolu.</para>
    ///
    /// <para>Statické a bez stavu schválně: je to křivka, která se ladí, a
    /// takhle jde ověřit bez grafiky.</para>
    /// </summary>
    public static float WindowAlpha(int seed, float time)
    {
        float phase = (Hash(seed, 7) % 1000) / 1000f * MathF.Tau;

        // Nosná vlna: pomalé „doma se něco děje".
        float slow = 0.78f + 0.16f * MathF.Sin(time * 0.35f + phase);

        // Druhá, rychlejší a slabší — televize za oknem.
        float fast = 0.06f * MathF.Sin(time * 1.9f + phase * 2.3f);

        return Math.Clamp(slow + fast, 0f, 1f);
    }

    private void DrawCenteredGlow(SpriteBatch spriteBatch, int centerX, int centerY, int width, int height, float alpha)
    {
        spriteBatch.Draw(
            _pixel,
            new Rectangle(centerX - width / 2, centerY - height / 2, width, height),
            GlowColor * alpha);
    }

    private static ulong Hash(int building, int salt)
    {
        ulong h = (ulong)building * 0x9E3779B97F4A7C15UL ^ ((ulong)salt + 1) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        return h ^ (h >> 31);
    }
}
