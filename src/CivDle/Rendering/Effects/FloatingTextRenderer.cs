using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Plovoucí popupy („+2") nad místem akce. Ukotvené ve world souřadnicích,
/// ale kreslené ve screen-space konstantní velikostí — čitelné při každém zoomu.
/// Pevný pool bez alokací za běhu; texty popupů si volající cachuje.
/// </summary>
public sealed class FloatingTextRenderer
{
    private const int MaxEntries = 64;
    private const float Life = 1.1f;
    private const float RisePixels = 42f; // screen px za život popupu

    private struct Entry
    {
        public Vector2 WorldPosition;
        public float Age;
        public string Text;
        public Color Color;
    }

    private readonly Entry[] _entries = new Entry[MaxEntries];
    private int _count;

    /// <summary>Přidá popup nad world pozicí (text má být z cache, ne skládaný per klik).</summary>
    public void Add(Vector2 worldPosition, string text, Color color)
    {
        if (_count == MaxEntries)
        {
            // Pool plný → zahodí se nejstarší (index 0 po prohozech ≈ nejstarší dost dobře).
            _entries[0] = _entries[--_count];
        }

        _entries[_count++] = new Entry
        {
            WorldPosition = worldPosition,
            Age = 0f,
            Text = text,
            Color = color,
        };
    }

    public void Update(float dt)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            _entries[i].Age += dt;
            if (_entries[i].Age >= Life)
            {
                _entries[i] = _entries[--_count];
            }
        }
    }

    /// <summary>Kreslí ve screen-space (volat po UI, ať jsou popupy vždy vidět).</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, SpriteFontBase font)
    {
        if (_count == 0)
        {
            return;
        }

        spriteBatch.Begin();
        for (int i = 0; i < _count; i++)
        {
            ref readonly var entry = ref _entries[i];
            float progress = entry.Age / Life;
            // Plná viditelnost první polovinu života, pak zhasínání.
            float alpha = progress < 0.5f ? 1f : 1f - (progress - 0.5f) * 2f;

            var screen = camera.WorldToScreen(entry.WorldPosition);
            screen.Y -= RisePixels * progress;
            var size = font.MeasureString(entry.Text);
            var position = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y);

            // Tmavý podklad pro čitelnost nad světlými biomy.
            spriteBatch.DrawString(font, entry.Text, position + new Vector2(1f, 1f), Color.Black * (alpha * 0.7f));
            spriteBatch.DrawString(font, entry.Text, position, entry.Color * alpha);
        }

        spriteBatch.End();
    }
}
