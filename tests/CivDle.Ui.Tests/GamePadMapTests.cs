using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Mapování ovladače. Testuje se to, co by na Steam Decku bylo hned poznat:
/// mrtvá zóna musí zabrat (levné sticky driftují), za ní se nesmí rozjet
/// skokem, osa Y musí sedět a spouště musí přibližovat správným směrem.
///
/// <para>Běží headless — <see cref="GamePadMap"/> je čistá funkce stavu,
/// takže není potřeba připojený ovladač.</para>
/// </summary>
public sealed class GamePadMapTests
{
    private static GamePadState Pad(
        Vector2 left = default,
        Vector2 right = default,
        float leftTrigger = 0f,
        float rightTrigger = 0f,
        Buttons buttons = 0) =>
        new(new GamePadThumbSticks(left, right), new GamePadTriggers(leftTrigger, rightTrigger),
            new GamePadButtons(buttons), new GamePadDPad());

    [Fact]
    public void ARestingStickIsIgnored()
    {
        // Levné sticky driftují; bez mrtvé zóny by kamera pomalu ujížděla sama.
        Assert.Equal(Vector2.Zero, GamePadMap.ApplyDeadZone(new Vector2(0.1f, -0.05f)));
    }

    [Fact]
    public void MovementStartsGentlyBeyondTheDeadZone()
    {
        // Hned za hranou mrtvé zóny se nesmí rozjet plnou rychlostí, jinak je
        // míření nepoužitelné.
        var justOver = GamePadMap.ApplyDeadZone(new Vector2(GamePadMap.DeadZone + 0.02f, 0f));

        Assert.True(justOver.Length() > 0f);
        Assert.True(justOver.Length() < 0.2f, $"Náběh je moc prudký: {justOver.Length():F2}.");
    }

    [Fact]
    public void FullDeflectionIsFullSpeed()
    {
        Assert.Equal(1f, GamePadMap.ApplyDeadZone(new Vector2(1f, 0f)).Length(), 3);
    }

    [Fact]
    public void PushingTheStickUpLooksUp()
    {
        // Stick nahoru = menší Y ve světě. Obrácená osa je klasická chyba,
        // kterou testem odchytím dřív než hráč.
        var move = GamePadMap.CameraMove(Pad(left: new Vector2(0f, 1f)));

        Assert.True(move.Y < 0f);
    }

    [Fact]
    public void DiagonalMovementIsNotFaster()
    {
        var move = GamePadMap.CameraMove(Pad(left: new Vector2(1f, 1f)));

        Assert.True(move.Length() <= 1.001f, $"Šikmo se jede rychleji: {move.Length():F2}.");
    }

    [Fact]
    public void TheCursorMovesWithTheRightStick()
    {
        var move = GamePadMap.CursorMove(Pad(right: new Vector2(1f, 0f)), dt: 1f);

        Assert.Equal(GamePadMap.CursorPixelsPerSecond, move.X, 1);
        Assert.Equal(0f, move.Y, 3);
    }

    [Fact]
    public void TriggersZoomInOppositeDirections()
    {
        float zoomIn = GamePadMap.ZoomFactor(Pad(rightTrigger: 1f), dt: 0.5f);
        float zoomOut = GamePadMap.ZoomFactor(Pad(leftTrigger: 1f), dt: 0.5f);

        Assert.True(zoomIn > 1f, "Pravá spoušť má přibližovat.");
        Assert.True(zoomOut < 1f, "Levá spoušť má oddalovat.");
    }

    [Fact]
    public void UntouchedTriggersLeaveTheZoomAlone()
    {
        Assert.Equal(1f, GamePadMap.ZoomFactor(Pad(), dt: 0.5f), 5);
    }

    [Fact]
    public void ButtonsMapToTheirActions()
    {
        Assert.True(GamePadMap.Confirm(Pad(buttons: Buttons.A)));
        Assert.True(GamePadMap.Cancel(Pad(buttons: Buttons.B)));
        Assert.True(GamePadMap.BuildMenu(Pad(buttons: Buttons.Y)));
        Assert.True(GamePadMap.CycleBatch(Pad(buttons: Buttons.X)));
        Assert.True(GamePadMap.Pause(Pad(buttons: Buttons.Start)));
    }

    [Fact]
    public void ConfirmAndCancelAreNotTheSameButton()
    {
        // Tohle je ta chyba, po které hráč omylem bourá místo staví.
        var a = Pad(buttons: Buttons.A);

        Assert.True(GamePadMap.Confirm(a));
        Assert.False(GamePadMap.Cancel(a));
    }
}
