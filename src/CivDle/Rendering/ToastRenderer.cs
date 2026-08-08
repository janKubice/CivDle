using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vyskakovací zprávy (toasty) jako <b>úzký seznam u pravého okraje</b>:
/// splněný úkol, achievement, sloučený blok, Vzestup. Sim je jen vyrobí jako
/// data; tenhle renderer si drží krátký život + fade a stohuje je pod sebe.
/// Čistě render (žádný zápis do simulace).
///
/// <para>Bývaly nahoře uprostřed a bylo to špatně: guvernér zprávy vyrábí
/// sám a pořád, takže při zapnuté automatice stál hráči uprostřed obrazovky
/// trvalý sloupec cedulí. U pravého okraje se dají číst i ignorovat — a
/// hlavně nestojí přes město.</para>
///
/// <para>Víc než <see cref="MaxVisible"/> najednou se nekreslí. Bez stropu
/// uměla dávka hlášek z rychlé automatiky zaplnit celou výšku obrazovky.</para>
/// </summary>
public sealed class ToastRenderer
{
    private const float LifeSeconds = 4.5f;
    private const float FadeInSeconds = 0.25f;
    private const float FadeOutSeconds = 0.7f;
    private const int Width = 300;
    private const int Height = 34;
    private const int Gap = 5;

    /// <summary>Odsazení seznamu od pravého okraje.</summary>
    private const int RightMargin = 10;

    /// <summary>Kolik zpráv se nanejvýš kreslí naráz; zbytek dožije neviditelně.</summary>
    private const int MaxVisible = 6;

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

    /// <summary>Zahodí všechny běžící toasty najednou (režim focení do obchodu).</summary>
    public void Clear() => _toasts.Clear();

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

    /// <param name="top">
    /// Odkud dolů se seznam kreslí. Volající sem dává spodek pravého panelu se
    /// stavem světa — ten je různě vysoký podle toho, co má hráč odemčené, a
    /// pevné číslo by mu buď lezlo pod něj, nebo nechávalo díru.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, Viewport viewport, int top)
    {
        if (_toasts.Count == 0)
        {
            return;
        }

        int x = viewport.Width - Width - RightMargin;
        int visible = Math.Min(_toasts.Count, MaxVisible);

        spriteBatch.Begin();
        for (int i = 0; i < visible; i++)
        {
            var toast = _toasts[i];
            float alpha = Fade(toast.Age);
            int y = top + i * (Height + Gap);

            spriteBatch.Draw(_pixel, new Rectangle(x, y, Width, Height), new Color(16, 20, 28) * (0.92f * alpha));
            spriteBatch.Draw(_pixel, new Rectangle(x, y, 4, Height), toast.Accent * alpha); // barevný pruh vlevo

            // Dlouhé jméno budovy se do užšího pruhu nevejde — radši ho zkrátit
            // třemi tečkami než ho nechat vytéct přes okraj obrazovky.
            string text = Fit(toast.Text, Width - 22);
            var textPos = new Vector2(x + 14, y + Height * 0.5f - _font.MeasureString(text).Y * 0.5f);
            spriteBatch.DrawString(_font, text, textPos + new Vector2(1f, 1f), Color.Black * (0.7f * alpha));
            spriteBatch.DrawString(_font, text, textPos, Color.White * alpha);
        }

        spriteBatch.End();
    }

    /// <summary>Zkrátí text tak, aby se vešel do dané šířky (s „…" na konci).</summary>
    private string Fit(string text, float maxWidth)
    {
        if (_font.MeasureString(text).X <= maxWidth)
        {
            return text;
        }

        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length] + "…";
            if (_font.MeasureString(candidate).X <= maxWidth)
            {
                return candidate;
            }
        }

        return "…";
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
