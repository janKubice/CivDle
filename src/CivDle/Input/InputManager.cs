using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Input;

/// <summary>
/// Snímkový stav vstupu: drží minulý a aktuální stav klávesnice/myši, aby šlo
/// rozlišit „drženo" od „právě stisknuto" a počítat delty (tažení myší, kolečko).
/// </summary>
public sealed class InputManager
{
    private KeyboardState _previousKeyboard;
    private KeyboardState _currentKeyboard;
    private MouseState _previousMouse;
    private MouseState _currentMouse;

    /// <summary>Sejme nový stav vstupu — volat jednou na začátku Update obrazovky.</summary>
    public void Update()
    {
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
    }

    /// <summary>
    /// Srovná minulý stav s přítomností. Volat po návratu obrazovky navrch,
    /// jinak by se delta myši/kolečka spočítala přes celou dobu pauzy.
    /// </summary>
    public void Resync()
    {
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
    }

    /// <summary>Klávesa je právě držená.</summary>
    public bool IsDown(Keys key) => _currentKeyboard.IsKeyDown(key);

    /// <summary>Klávesa byla v tomto snímku nově stisknuta.</summary>
    public bool WasPressed(Keys key) => _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    /// <summary>Pozice kurzoru v pixelech okna.</summary>
    public Point MousePosition => _currentMouse.Position;

    /// <summary>Posun kurzoru od minulého snímku.</summary>
    public Vector2 MouseDelta => (_currentMouse.Position - _previousMouse.Position).ToVector2();

    /// <summary>Změna kolečka myši od minulého snímku (120 na „cvaknutí").</summary>
    public int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public bool IsLeftDown => _currentMouse.LeftButton == ButtonState.Pressed;

    public bool IsMiddleDown => _currentMouse.MiddleButton == ButtonState.Pressed;
}
