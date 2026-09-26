using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Velký nápis přes horní třetinu obrazovky pro okamžiky, které si hráč má
/// zapamatovat: „Tohle bude tvoje město", „Město teď pracuje samo", první noc.
///
/// <para>Toast u pravého okraje je na zprávy, které se dají přehlédnout. Tohle
/// je opak — věta, kvůli které hráč zůstane. Proto velké písmo uprostřed,
/// pomalý nájezd a sama zmizí; hru nezastaví a na klik nečeká.</para>
///
/// <para>Kreslí se ručně (DrawString se škálováním) jako <see cref="MightBanner"/> —
/// Myra velikost písma měnit neumí.</para>
/// </summary>
internal sealed class MomentBanner
{
    private const float TitleScale = 2.2f;
    private const float FadeInSeconds = 0.6f;
    private const float FadeOutSeconds = 1.0f;

    /// <summary>Nejmenší odstup od horního okraje — pod lištou surovin.</summary>
    private const float TopMargin = 90f;

    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;

    private string _title = string.Empty;
    private string _subtitle = string.Empty;
    private Color _accent;
    private float _age;
    private float _life;

    public MomentBanner(Texture2D whitePixel, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _font = font;
    }

    /// <summary>Právě se něco ukazuje? (Další okamžik počká, ať se nepřekřikují.)</summary>
    public bool IsShowing => _age < _life;

    /// <summary>Ukáže nápis; předchozí nahradí.</summary>
    public void Show(string title, string subtitle, Color accent, float seconds)
    {
        _title = title;
        _subtitle = subtitle;
        _accent = accent;
        _age = 0f;
        _life = seconds;
    }

    /// <summary>Ukončí nápis hned (hráč už jedná, nemá mu stát v cestě).</summary>
    public void Dismiss() => _age = Math.Max(_age, _life - FadeOutSeconds);

    public void Update(float dt)
    {
        if (IsShowing)
        {
            _age += dt;
        }
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport)
    {
        if (!IsShowing)
        {
            return;
        }

        float alpha = _age < FadeInSeconds
            ? _age / FadeInSeconds
            : _age > _life - FadeOutSeconds ? Math.Max(0f, (_life - _age) / FadeOutSeconds) : 1f;
        float rise = (1f - Math.Min(1f, _age / FadeInSeconds)) * 14f; // nájezd zdola

        var titleSize = _font.MeasureString(_title) * TitleScale;
        var subtitleSize = _font.MeasureString(_subtitle);
        float width = Math.Max(titleSize.X, subtitleSize.X) + 60f;
        float height = titleSize.Y + (_subtitle.Length > 0 ? subtitleSize.Y + 10f : 0f) + 28f;
        float x = (viewport.Width - width) * 0.5f;
        // Těsně pod horní lištou surovin, ne ve třetině výšky: tam leží okolí
        // táboráku a nápis by zakryl strom, na který zrovna ukazuje šipka.
        float y = MathF.Max(TopMargin, viewport.Height * 0.085f) + rise;

        spriteBatch.Begin();
        spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)y, (int)width, (int)height), new Color(12, 16, 24) * (0.78f * alpha));
        spriteBatch.Draw(_pixel, new Rectangle((int)x, (int)(y + height - 3), (int)width, 3), _accent * alpha);

        var titlePos = new Vector2((viewport.Width - titleSize.X) * 0.5f, y + 12f);
        var scale = new Vector2(TitleScale, TitleScale);
        spriteBatch.DrawString(_font, _title, titlePos + new Vector2(2f, 2f), Color.Black * (0.6f * alpha), 0f, Vector2.Zero, scale);
        spriteBatch.DrawString(_font, _title, titlePos, _accent * alpha, 0f, Vector2.Zero, scale);

        if (_subtitle.Length > 0)
        {
            var subtitlePos = new Vector2((viewport.Width - subtitleSize.X) * 0.5f, titlePos.Y + titleSize.Y + 8f);
            spriteBatch.DrawString(_font, _subtitle, subtitlePos + new Vector2(1f, 1f), Color.Black * (0.6f * alpha));
            spriteBatch.DrawString(_font, _subtitle, subtitlePos, Color.White * alpha);
        }

        spriteBatch.End();
    }
}
