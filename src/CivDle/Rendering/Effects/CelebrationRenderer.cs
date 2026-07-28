using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Velká oslava přes celou obrazovku: nápis, který přiletí, chvíli stojí
/// a odletí, plus prosvětlení scény.
///
/// <para>Milníky a Vzestup se dřív hlásily jen malým toastem v rohu — stejným
/// jako každá jiná drobnost. Tisící obyvatel a Vzestup si přitom zaslouží, aby
/// se hráč zastavil. Tohle je ta pauza.</para>
///
/// <para>Vrstva renderu, kreslí se ve screen-space nad mapou i HUD. Běží vždycky
/// jen jedna oslava — nová přepíše starou, aby se nápisy nekupily.</para>
/// </summary>
public sealed class CelebrationRenderer
{
    private const float FlyInSeconds = 0.35f;
    private const float HoldSeconds = 1.6f;
    private const float FlyOutSeconds = 0.5f;
    private const float TotalSeconds = FlyInSeconds + HoldSeconds + FlyOutSeconds;

    private string _text = string.Empty;
    private Color _color = Color.White;
    private float _age = TotalSeconds; // start bez běžící oslavy

    /// <summary>Vypnuto přístupnostní volbou „omezit pohyb".</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Běží zrovna oslava?</summary>
    public bool IsPlaying => _age < TotalSeconds;

    /// <summary>Spustí oslavu; případná běžící se přeruší.</summary>
    public void Show(string text, Color color)
    {
        if (!Enabled)
        {
            return;
        }

        _text = text;
        _color = color;
        _age = 0f;
    }

    public void Update(float dt)
    {
        if (IsPlaying)
        {
            _age += dt;
        }
    }

    /// <summary>Kreslí ve screen-space — volající si otevře vlastní dávku.</summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFontBase font, Viewport viewport)
    {
        if (!IsPlaying || _text.Length == 0)
        {
            return;
        }

        // Nápis přiletí zdola, chvíli stojí a odletí nahoru; průhlednost jde s ním.
        float offset, alpha;
        if (_age < FlyInSeconds)
        {
            float t = _age / FlyInSeconds;
            offset = (1f - EaseOut(t)) * 70f;
            alpha = t;
        }
        else if (_age < FlyInSeconds + HoldSeconds)
        {
            offset = 0f;
            alpha = 1f;
        }
        else
        {
            float t = (_age - FlyInSeconds - HoldSeconds) / FlyOutSeconds;
            offset = -EaseOut(t) * 50f;
            alpha = 1f - t;
        }

        var size = font.MeasureString(_text);
        float centerX = viewport.Width / 2f;
        float centerY = viewport.Height * 0.32f + offset;

        // Tmavý pruh za textem, aby byl čitelný nad jakoukoli mapou.
        int bandHeight = (int)(size.Y * 2.2f);
        spriteBatch.Draw(pixel,
            new Rectangle(0, (int)(centerY - bandHeight / 2f), viewport.Width, bandHeight),
            new Color(8, 10, 18) * (alpha * 0.72f));

        // Linky nad a pod pruhem v barvě oslavy — levné a hned to vypadá jako událost.
        spriteBatch.Draw(pixel, new Rectangle(0, (int)(centerY - bandHeight / 2f), viewport.Width, 2), _color * alpha);
        spriteBatch.Draw(pixel, new Rectangle(0, (int)(centerY + bandHeight / 2f - 2), viewport.Width, 2), _color * alpha);

        spriteBatch.DrawString(font, _text,
            new Vector2(centerX - size.X / 2f, centerY - size.Y / 2f), _color * alpha);
    }

    private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
}
