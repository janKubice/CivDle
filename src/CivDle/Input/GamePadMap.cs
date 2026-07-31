using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Input;

/// <summary>
/// Překlad stavu ovladače na to, co hra umí: pohyb kamery, kurzor, klikání,
/// zoom a pár zkratek.
///
/// <para>Proč vlastní třída a ne pár řádků v <see cref="InputManager"/>:
/// mapování je rozhodnutí, které se ladí (mrtvá zóna, rychlost kurzoru,
/// které tlačítko je „potvrdit"), a takhle se dá ověřit testem bez okna
/// i bez připojeného ovladače — stačí sestavit <see cref="GamePadState"/>.</para>
///
/// <para>Steam Deck: hlavní vstup je pravý stick jako kurzor a A jako klik.
/// Deck sice umí mapovat trackpad na myš sám, ale hra se nemá spoléhat na to,
/// že si hráč ovládání nastaví — z krabice musí jít hrát palci.</para>
/// </summary>
public static class GamePadMap
{
    /// <summary>Pod touhle výchylkou se stick považuje za klidný (drift levných sticků).</summary>
    public const float DeadZone = 0.22f;

    /// <summary>Rychlost kurzoru pravým stickem (pixely za sekundu při plné výchylce).</summary>
    public const float CursorPixelsPerSecond = 900f;

    /// <summary>Jak rychle spouště přibližují (násobek zoomu za sekundu).</summary>
    public const float ZoomPerSecond = 1.9f;

    /// <summary>
    /// Ošetří mrtvou zónu a znovu roztáhne zbytek na 0–1, takže hned za hranou
    /// mrtvé zóny nezačíná pohyb skokem.
    /// </summary>
    public static Vector2 ApplyDeadZone(Vector2 stick)
    {
        float length = stick.Length();
        if (length <= DeadZone)
        {
            return Vector2.Zero;
        }

        float scaled = (length - DeadZone) / (1f - DeadZone);
        return stick / length * Math.Min(1f, scaled);
    }

    /// <summary>
    /// Kam posunout kameru. Osa Y se obrací: stick nahoru znamená „koukat výš",
    /// tedy směrem k menším Y ve světě.
    /// </summary>
    public static Vector2 CameraMove(GamePadState pad)
    {
        var stick = ApplyDeadZone(new Vector2(pad.ThumbSticks.Left.X, -pad.ThumbSticks.Left.Y));

        // Křížový ovladač je druhá cesta pro toho, komu stick nesedí.
        if (pad.DPad.Left == ButtonState.Pressed) stick.X -= 1f;
        if (pad.DPad.Right == ButtonState.Pressed) stick.X += 1f;
        if (pad.DPad.Up == ButtonState.Pressed) stick.Y -= 1f;
        if (pad.DPad.Down == ButtonState.Pressed) stick.Y += 1f;

        return stick.LengthSquared() > 1f ? Vector2.Normalize(stick) : stick;
    }

    /// <summary>O kolik pixelů se má za tenhle snímek posunout kurzor.</summary>
    public static Vector2 CursorMove(GamePadState pad, float dt)
    {
        var stick = ApplyDeadZone(new Vector2(pad.ThumbSticks.Right.X, -pad.ThumbSticks.Right.Y));
        return stick * CursorPixelsPerSecond * dt;
    }

    /// <summary>
    /// Násobič zoomu za snímek: pravá spoušť přibližuje, levá oddaluje.
    /// 1.0 = beze změny, takže se dá rovnou vynásobit.
    /// </summary>
    public static float ZoomFactor(GamePadState pad, float dt)
    {
        float amount = pad.Triggers.Right - pad.Triggers.Left;
        if (MathF.Abs(amount) < DeadZone)
        {
            return 1f;
        }

        return MathF.Pow(ZoomPerSecond, amount * dt);
    }

    /// <summary>Potvrzení / položení (A) — protějšek levého tlačítka myši.</summary>
    public static bool Confirm(GamePadState pad) => pad.Buttons.A == ButtonState.Pressed;

    /// <summary>Zrušení / zpět (B) — protějšek pravého tlačítka myši a Escape.</summary>
    public static bool Cancel(GamePadState pad) => pad.Buttons.B == ButtonState.Pressed;

    /// <summary>Otevřít stavební menu (Y).</summary>
    public static bool BuildMenu(GamePadState pad) => pad.Buttons.Y == ButtonState.Pressed;

    /// <summary>Přepnout násobič hromadné stavby (X) — protějšek klávesy Tab.</summary>
    public static bool CycleBatch(GamePadState pad) => pad.Buttons.X == ButtonState.Pressed;

    /// <summary>Pauza / nabídka (Start).</summary>
    public static bool Pause(GamePadState pad) => pad.Buttons.Start == ButtonState.Pressed;
}
