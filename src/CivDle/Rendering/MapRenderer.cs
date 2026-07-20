using CivDle.Core.Content;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení mapy světa. MVP přístup: mapa se jednou „upeče" do textury
/// 1 texel = 1 dlaždice (barvy biomů z JSON + deterministická variace jasu
/// proti repetici, living-map.md sekce 6) a kreslí se jedním draw callem
/// se škálováním — culling i LOD tím řeší GPU zadarmo. Až přibudou sprity,
/// nahradí tohle chunky + texture atlas dle tech-stack.md.
/// Render jen čte ze simulace, nikdy do ní nezapisuje.
/// </summary>
public sealed class MapRenderer : IDisposable
{
    /// <summary>Velikost dlaždice ve world pixelech (referenční měřítko kamery).</summary>
    public const int TileSize = 16;

    private readonly Texture2D _mapTexture;

    public MapRenderer(GraphicsDevice device, WorldMap map, BiomeRegistry biomes, long seed)
    {
        WorldPixelWidth = map.Width * TileSize;
        WorldPixelHeight = map.Height * TileSize;

        var pixels = new Color[map.Width * map.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            var biome = biomes[map.BiomeIndices[i]];
            int x = i % map.Width;
            int y = i / map.Width;

            // Jas ±colorVariation podle deterministického hashe dlaždice — stejný seed
            // dá stejnou mapu do posledního pixelu.
            float brightness = 1f + (HashToUnit(x, y, seed) * 2f - 1f) * biome.ColorVariation;
            pixels[i] = new Color(
                ClampByte(biome.MapColor.R * brightness),
                ClampByte(biome.MapColor.G * brightness),
                ClampByte(biome.MapColor.B * brightness));
        }

        _mapTexture = new Texture2D(device, map.Width, map.Height);
        _mapTexture.SetData(pixels);
    }

    /// <summary>Šířka světa v pixelech (pro meze kamery).</summary>
    public int WorldPixelWidth { get; }

    /// <summary>Výška světa v pixelech (pro meze kamery).</summary>
    public int WorldPixelHeight { get; }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        // PointClamp: dlaždice zůstávají ostré čtverce i při velkém přiblížení.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        spriteBatch.Draw(
            _mapTexture,
            Vector2.Zero,
            null,
            Color.White,
            rotation: 0f,
            origin: Vector2.Zero,
            scale: TileSize,
            SpriteEffects.None,
            layerDepth: 0f);
        spriteBatch.End();
    }

    public void Dispose() => _mapTexture.Dispose();

    private static byte ClampByte(float value) => (byte)Math.Clamp(value, 0f, 255f);

    /// <summary>Deterministický hash dlaždice → 0–1 (mix konstantami ze SplitMix64).</summary>
    private static float HashToUnit(int x, int y, long seed)
    {
        ulong h = unchecked((ulong)seed);
        h ^= (uint)x * 0x9E3779B97F4A7C15UL;
        h ^= (uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        h ^= h >> 31;
        return (h >> 40) / (float)(1 << 24);
    }
}
