using CivDle.Input;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Screens;

/// <summary>Jedna položka kruhové nabídky.</summary>
/// <param name="Icon">Sprite ikony.</param>
/// <param name="Label">Popisek pod kruhem, když je položka vybraná.</param>
/// <param name="Action">Co se stane po puštění spouště.</param>
public sealed record RadialItem(Texture2D? Icon, string Label, Action Action);

/// <summary>
/// Kruhová nabídka na levou spoušť — hlavní ovládání na ovladači a Decku.
///
/// <para><b>Proč vůbec:</b> lišta nástrojů se myší proklikne za vteřinu, ale
/// na ovladači je to putování kurzorem přes půl obrazovky. Kruh dá všechno na
/// dosah palce: podrž spoušť, nakloň páčku, pusť.</para>
///
/// <para><b>Žádná nová logika.</b> Položky dělají přesně to, co tlačítka
/// v liště — tohle je jen jiná cesta k témuž. Kdyby měla vlastní chování,
/// rozejde se to při první změně nástrojů.</para>
///
/// <para><b>Výběr podle úhlu, ne podle vzdálenosti.</b> Páčka na Decku nemá
/// přesnou výchylku a trvat na tom, aby ji hráč dotlačil až na kraj, by
/// znamenalo nabídku, která se občas netrefí. Stačí naklonit.</para>
/// </summary>
public sealed class RadialMenu
{
    /// <summary>Od jaké výchylky páčky se považuje směr za zvolený.</summary>
    private const float PickThreshold = 0.35f;

    /// <summary>Poloměr kruhu jako podíl kratší strany okna.</summary>
    private const float RadiusFraction = 0.22f;

    private readonly List<RadialItem> _items = new();
    private int _selected = -1;

    /// <summary>Je nabídka otevřená (drží se spoušť)?</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Která položka je pod palcem; −1 = žádná.</summary>
    public int Selected => _selected;

    public int Count => _items.Count;

    /// <summary>Naplní nabídku. Volá se při přestavbě HUDu, ne každý snímek.</summary>
    public void SetItems(IEnumerable<RadialItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        if (_selected >= _items.Count)
        {
            _selected = -1;
        }
    }

    /// <summary>
    /// Která výseč odpovídá výchylce páčky. Veřejné a statické kvůli testům —
    /// „kam palec ukazuje" je rozhodnutí o ovládání, ne o kreslení, a má se
    /// dát ověřit bez ovladače.
    /// </summary>
    /// <returns>Index položky, nebo −1 při klidné páčce.</returns>
    public static int PickIndex(Vector2 stick, int count)
    {
        if (count <= 0 || stick.LengthSquared() < PickThreshold * PickThreshold)
        {
            return -1;
        }

        // Nula je nahoře a jde se po směru hodinových ručiček — tak, jak jsou
        // položky nakreslené.
        //
        // Pozor na osu Y: sem chodí výchylka páčky, kde je nahoře KLADNÉ Y
        // (obrazovka to má obráceně). Kdyby se to prohodilo, nabídka by
        // vybírala protilehlou položku a nikdo by nepoznal proč.
        double angle = Math.Atan2(stick.X, stick.Y);
        if (angle < 0)
        {
            angle += Math.Tau;
        }

        double slice = Math.Tau / count;
        return (int)Math.Floor((angle + (slice / 2)) / slice) % count;
    }

    /// <summary>
    /// Posune stav nabídky. Otevírá se držením levé spouště, potvrzuje jejím
    /// puštěním — tedy jedním pohybem, ne dvěma stisky.
    /// </summary>
    public void Update(InputManager input, GamePadState pad)
    {
        if (_items.Count == 0)
        {
            IsOpen = false;
            return;
        }

        bool held = pad.IsConnected && pad.Triggers.Left > 0.5f;
        if (held)
        {
            IsOpen = true;
            _selected = PickIndex(
                GamePadMap.ApplyDeadZone(new Vector2(pad.ThumbSticks.Left.X, pad.ThumbSticks.Left.Y)),
                _items.Count);
            return;
        }

        if (IsOpen)
        {
            IsOpen = false;
            if (_selected >= 0 && _selected < _items.Count)
            {
                _items[_selected].Action();
            }

            _selected = -1;
        }
    }

    /// <summary>Vykreslí kruh. Nedělá nic, když je zavřený.</summary>
    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport, SpriteFontBase font)
    {
        if (!IsOpen || _items.Count == 0)
        {
            return;
        }

        var center = new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f);
        float radius = Math.Min(viewport.Width, viewport.Height) * RadiusFraction;

        spriteBatch.Begin();

        // Ztmavení pod nabídkou: kruh musí být čitelný i nad rozsvíceným městem.
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.45f);

        for (int i = 0; i < _items.Count; i++)
        {
            double angle = Math.Tau * i / _items.Count;
            var at = center + new Vector2(
                (float)Math.Sin(angle) * radius,
                (float)-Math.Cos(angle) * radius);

            bool active = i == _selected;
            int size = active ? 64 : 48;
            var box = new Rectangle((int)(at.X - size / 2f), (int)(at.Y - size / 2f), size, size);

            spriteBatch.Draw(pixel, Inflate(box, 6), (active ? UiPalette.Accent : UiPalette.Panel) * 0.9f);
            if (_items[i].Icon is { } icon)
            {
                spriteBatch.Draw(icon, box, Color.White);
            }
        }

        // Jméno vybrané položky pod kruhem — ikony samy o sobě nestačí.
        if (_selected >= 0 && _selected < _items.Count)
        {
            string label = _items[_selected].Label;
            var size = font.MeasureString(label);
            spriteBatch.DrawString(
                font, label,
                new Vector2(center.X - (size.X * 0.5f), center.Y + radius + 40),
                UiPalette.TextBright);
        }

        spriteBatch.End();
    }

    private static Rectangle Inflate(Rectangle rect, int by) =>
        new(rect.X - by, rect.Y - by, rect.Width + (by * 2), rect.Height + (by * 2));
}
