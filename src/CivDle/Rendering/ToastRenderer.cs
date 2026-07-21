using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vyskakovací zprávy (toasty) nahoře uprostřed: splněný úkol, achievement,
/// milník, Vzestup. Sim je jen vyrobí jako data; tenhle renderer si drží krátký
/// život + fade a stohuje je pod sebe. Čistě render (žádný zápis do simulace).
/// </summary>
public sealed class ToastRenderer
{
    private const float LifeSeconds = 4.5f;
    private const float FadeInSeconds = 0.25f;
    private const float FadeOutSeconds = 0.7f;
    private const int Width = 340;
    private const int Height = 40;
    private const int Gap = 6;

    private sealed class Toast
    {
        public string Text = string.Empty;
        public Color Accent;
        public float Age;
    }

    private readonly List<Toast> _toasts = new();
    private readonly List<Toast> _expired = new();
    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;

    public ToastRenderer(Texture2D whitePixel, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _font = font;
    }

    /// <summary>Přidá toast navrch (nejnovější je nahoře).</summary>
    public void Add(string text, Color accent)
    {
        _toasts.Insert(0, new Toast { Text = text, Accent = accent });
    }

    public void Update(float dt)
    {
        _expired.Clear();
        foreach (var toast in _toasts)
        {
            toast.Age += dt;
            if (toast.Age >= LifeSeconds)
            {
                _expired.Add(toast);
            }
        }

        foreach (var toast in _expired)
        {
            _toasts.Remove(toast);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        int x = (viewport.Width - Width) / 2;
        int top = 70;

        spriteBatch.Begin();
        for (int i = 0; i < _toasts.Count; i++)
        {
            var toast = _toasts[i];
            float alpha = Fade(toast.Age);
            int y = top + i * (Height + Gap);

            spriteBatch.Draw(_pixel, new Rectangle(x, y, Width, Height), new Color(16, 20, 28) * (0.92f * alpha));
            spriteBatch.Draw(_pixel, new Rectangle(x, y, 4, Height), toast.Accent * alpha); // barevný pruh vlevo

            var textPos = new Vector2(x + 14, y + Height * 0.5f - _font.MeasureString(toast.Text).Y * 0.5f);
            spriteBatch.DrawString(_font, toast.Text, textPos + new Vector2(1f, 1f), Color.Black * (0.7f * alpha));
            spriteBatch.DrawString(_font, toast.Text, textPos, Color.White * alpha);
        }

        spriteBatch.End();
    }

    private static float Fade(float age)
    {
        if (age < FadeInSeconds)
        {
            return age / FadeInSeconds;
        }

        float remaining = LifeSeconds - age;
        if (remaining < FadeOutSeconds)
        {
            return MathF.Max(0f, remaining / FadeOutSeconds);
        }

        return 1f;
    }
}
