using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Orbitální těžební paprsek: sloup světla z nebe do místa, kam hráč míří.
///
/// <para>Proč to ve hře je: klikat na jednotlivé stromy je v hodině páté stejná
/// činnost jako v minutě první — jen míň zajímavá. Laser tu činnost nezruší, jen
/// ji promění: hráč místo klikání táhne paprsek přes krajinu. Je to odměna za
/// dojití daleko, ne nová mechanika k naučení.</para>
///
/// <para>Vrstva renderu: jen kreslí a drží dohasínání. Kolik se čeho vytěží,
/// rozhoduje simulace — tahle třída nic neví o surovinách.</para>
/// </summary>
public sealed class LaserRenderer
{
    /// <summary>Odkud paprsek přilétá (world pixely nad cílem).</summary>
    private const float BeamHeight = 900f;

    /// <summary>Jak dlouho paprsek dosvítí po puštění tlačítka.</summary>
    private const float FadeSeconds = 0.18f;

    /// <summary>Šířka jádra paprsku v pixelech; kolem něj se kreslí měkčí halo.</summary>
    private const int CoreWidth = 2;

    private static readonly Color CoreColor = new(210, 255, 255);
    private static readonly Color HaloColor = new(90, 210, 255);

    private Vector2 _target;
    private float _intensity;
    private float _phase;

    /// <summary>Vypnuto přístupnostní volbou „omezit pohyb".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Svítí paprsek (nebo aspoň dohasíná)?</summary>
    public bool IsFiring => _intensity > 0.01f;

    /// <summary>Namíří paprsek na místo v mapě. Volá se každý snímek, kdy hráč drží tlačítko.</summary>
    public void Aim(Vector2 worldTarget)
    {
        if (!Enabled)
        {
            return;
        }

        _target = worldTarget;
        _intensity = 1f;
    }

    public void Update(float dt)
    {
        _phase += dt;

        // Dohasínání místo tvrdého zhasnutí — paprsek, který zmizí v jednom
        // snímku, vypadá jako chyba vykreslení.
        if (_intensity > 0f)
        {
            _intensity = Math.Max(0f, _intensity - dt / FadeSeconds);
        }
    }

    /// <summary>Zhasne paprsek okamžitě (přepnutí nástroje, konec hry).</summary>
    public void Clear() => _intensity = 0f;

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Camera2D camera)
    {
        if (!IsFiring)
        {
            return;
        }

        // Aditivně: paprsek má svítit skrz krajinu, ne ji přemalovat.
        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        // Lehké chvění tloušťky, ať paprsek působí živě a ne jako nalepený obdélník.
        float flicker = 0.85f + 0.15f * MathF.Sin(_phase * 24f);
        int top = (int)(_target.Y - BeamHeight);
        int height = (int)BeamHeight;

        // Halo nejdřív (širší, slabší), pak jádro — pořadí dělá ten „glow".
        spriteBatch.Draw(
            pixel,
            new Rectangle((int)_target.X - CoreWidth * 3, top, CoreWidth * 6, height),
            HaloColor * (0.18f * _intensity * flicker));
        spriteBatch.Draw(
            pixel,
            new Rectangle((int)_target.X - CoreWidth, top, CoreWidth * 2, height),
            CoreColor * (0.75f * _intensity * flicker));

        // Dopad: zář na zemi, aby bylo vidět, kde paprsek pracuje.
        int glow = (int)(TerrainRenderer.TileSize * (0.9f + 0.2f * flicker));
        spriteBatch.Draw(
            pixel,
            new Rectangle((int)_target.X - glow / 2, (int)_target.Y - glow / 2, glow, glow),
            HaloColor * (0.35f * _intensity));

        spriteBatch.End();
    }
}
