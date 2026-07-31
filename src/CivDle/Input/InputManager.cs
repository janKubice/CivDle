using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Input;

/// <summary>
/// Snímkový stav vstupu: drží minulý a aktuální stav klávesnice, myši a ovladače,
/// aby šlo rozlišit „drženo" od „právě stisknuto" a počítat delty (tažení myší,
/// kolečko).
///
/// <para>Ovladač se <b>vlévá do stejných vlastností</b> jako myš a klávesnice —
/// A se chová jako levé tlačítko, B jako pravé, pravý stick hýbe kurzorem.
/// Díky tomu funguje s ovladačem celá hra včetně obrazovek, které o něm nevědí,
/// místo aby se gamepad musel dopisovat do každé zvlášť (Steam Deck má být
/// hratelný z krabice, ne po nastavení).</para>
/// </summary>
public sealed class InputManager
{
    private KeyboardState _previousKeyboard;
    private KeyboardState _currentKeyboard;
    private MouseState _previousMouse;
    private MouseState _currentMouse;
    private GamePadState _previousPad;
    private GamePadState _currentPad;

    /// <summary>Zbytek posunu kurzoru pod jeden pixel — bez něj by pomalé míření trhalo.</summary>
    private Vector2 _cursorRemainder;

    /// <summary>Sejme nový stav vstupu — volat jednou na začátku Update obrazovky.</summary>
    public void Update() => Update(0f);

    /// <summary>
    /// Jako <see cref="Update()"/>, ale s délkou snímku — teprve s ní umí ovladač
    /// hýbat kurzorem a zoomem plynule (rychlost v pixelech za sekundu).
    /// </summary>
    public void Update(float dt)
    {
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _previousPad = _currentPad;
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
        _currentPad = GamePad.GetState(PlayerIndex.One);

        MoveCursorWithStick(dt);
    }

    /// <summary>
    /// Posune systémový kurzor pravým stickem. Přes systémový kurzor schválně:
    /// tím se ovladač propíše i do UI, které o něm nic neví.
    ///
    /// <para>Když stick stojí, nesahá se na nic — jinak by ovladač zápasil
    /// s myší u hráče, který používá obojí.</para>
    /// </summary>
    private void MoveCursorWithStick(float dt)
    {
        if (!_currentPad.IsConnected || dt <= 0f)
        {
            return;
        }

        var move = GamePadMap.CursorMove(_currentPad, dt) + _cursorRemainder;
        int dx = (int)move.X;
        int dy = (int)move.Y;
        _cursorRemainder = new Vector2(move.X - dx, move.Y - dy);

        if (dx == 0 && dy == 0)
        {
            return;
        }

        var target = _currentMouse.Position + new Point(dx, dy);
        Mouse.SetPosition(target.X, target.Y);
        _currentMouse = Mouse.GetState();
    }

    /// <summary>Je připojený ovladač? (HUD podle toho může napovídat tlačítka.)</summary>
    public bool IsGamePadConnected => _currentPad.IsConnected;

    /// <summary>Stav ovladače pro obrazovky, které chtějí vlastní mapování.</summary>
    public GamePadState PadState => _currentPad;

    /// <summary>Posun kamery ovladačem (−1..1 na osu); nulový bez ovladače.</summary>
    public Vector2 PadCameraMove => _currentPad.IsConnected ? GamePadMap.CameraMove(_currentPad) : Vector2.Zero;

    /// <summary>Násobič zoomu ze spouští za tenhle snímek (1 = beze změny).</summary>
    public float PadZoomFactor(float dt) =>
        _currentPad.IsConnected ? GamePadMap.ZoomFactor(_currentPad, dt) : 1f;

    /// <summary>Tlačítko ovladače bylo v tomto snímku nově stisknuto.</summary>
    public bool WasPadPressed(Func<GamePadState, bool> button) =>
        _currentPad.IsConnected && button(_currentPad) && !button(_previousPad);

    /// <summary>
    /// Srovná minulý stav s přítomností. Volat po návratu obrazovky navrch,
    /// jinak by se delta myši/kolečka spočítala přes celou dobu pauzy.
    /// </summary>
    public void Resync()
    {
        _currentKeyboard = Keyboard.GetState();
        _currentMouse = Mouse.GetState();
        _currentPad = GamePad.GetState(PlayerIndex.One);
        _previousKeyboard = _currentKeyboard;
        _previousMouse = _currentMouse;
        _previousPad = _currentPad;
        _cursorRemainder = Vector2.Zero;
    }

    /// <summary>Klávesa je právě držená.</summary>
    public bool IsDown(Keys key) => _currentKeyboard.IsKeyDown(key);

    /// <summary>
    /// Klávesa byla v tomto snímku nově stisknuta. Escape poslouchá i na B
    /// ovladače, aby se dalo ze všech obrazovek vycouvat palcem.
    /// </summary>
    public bool WasPressed(Keys key)
    {
        bool keyboard = _currentKeyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);
        if (keyboard)
        {
            return true;
        }

        return key switch
        {
            Keys.Escape => WasPadPressed(GamePadMap.Cancel),
            Keys.Space or Keys.Enter => WasPadPressed(GamePadMap.Confirm),
            Keys.Tab => WasPadPressed(GamePadMap.CycleBatch),
            _ => false,
        };
    }

    /// <summary>Pozice kurzoru v pixelech okna.</summary>
    public Point MousePosition => _currentMouse.Position;

    /// <summary>Posun kurzoru od minulého snímku.</summary>
    public Vector2 MouseDelta => (_currentMouse.Position - _previousMouse.Position).ToVector2();

    /// <summary>Změna kolečka myši od minulého snímku (120 na „cvaknutí").</summary>
    public int ScrollDelta => _currentMouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

    public bool IsLeftDown =>
        _currentMouse.LeftButton == ButtonState.Pressed
        || (_currentPad.IsConnected && GamePadMap.Confirm(_currentPad));

    public bool IsMiddleDown => _currentMouse.MiddleButton == ButtonState.Pressed;

    public bool IsRightDown =>
        _currentMouse.RightButton == ButtonState.Pressed
        || (_currentPad.IsConnected && GamePadMap.Cancel(_currentPad));

    /// <summary>Levé tlačítko (nebo A na ovladači) bylo v tomto snímku nově stisknuto.</summary>
    public bool WasLeftPressed =>
        (_currentMouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released)
        || WasPadPressed(GamePadMap.Confirm);

    /// <summary>Levé tlačítko (nebo A) bylo v tomto snímku puštěno.</summary>
    public bool WasLeftReleased =>
        (_currentMouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed)
        || (_currentPad.IsConnected && !GamePadMap.Confirm(_currentPad) && GamePadMap.Confirm(_previousPad));

    /// <summary>Pravé tlačítko (nebo B) bylo v tomto snímku nově stisknuto.</summary>
    public bool WasRightPressed =>
        (_currentMouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released)
        || WasPadPressed(GamePadMap.Cancel);

    /// <summary>Pravé tlačítko (nebo B) bylo v tomto snímku puštěno.</summary>
    public bool WasRightReleased =>
        (_currentMouse.RightButton == ButtonState.Released && _previousMouse.RightButton == ButtonState.Pressed)
        || (_currentPad.IsConnected && !GamePadMap.Cancel(_currentPad) && GamePadMap.Cancel(_previousPad));
}
