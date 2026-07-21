using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Jemná vinětace: rohy obrazu decentně ztmaví, aby pohled táhlo ke středu scény
/// a celek působil sevřeněji (levný, ale znatelný zvednutí „vzhledu"). Textura se
/// vygeneruje jednou a roztáhne přes celý viewport. Čistě render, kreslí se pod HUD.
/// </summary>
public sealed class VignetteRenderer : IDisposable
{
    private const int Resolution = 256;
    private const float InnerRadius = 0.55f; // dokud sem, žádné ztmavení
    private const float MaxAlpha = 0.42f;    // síla v rozích

    private readonly Texture2D _texture;

    public VignetteRenderer(GraphicsDevice device)
    {
        var data = new Color[Resolution * Resolution];
        float center = Resolution * 0.5f;
        float half = Resolution * 0.5f;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float dx = (x + 0.5f - center) / half;
                float dy = (y + 0.5f - center) / half;
                float d = MathF.Min(1f, MathF.Sqrt(dx * dx + dy * dy));
                float a = MathF.Max(0f, (d - InnerRadius) / (1f - InnerRadius));
                data[y * Resolution + x] = new Color(0, 0, 0, (int)(a * a * MaxAlpha * 255f));
            }
        }

        _texture = new Texture2D(device, Resolution, Resolution);
        _texture.SetData(data);
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        spriteBatch.Begin();
        spriteBatch.Draw(_texture, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.White);
        spriteBatch.End();
    }

    public void Dispose() => _texture.Dispose();
}
