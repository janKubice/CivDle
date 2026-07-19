using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// 2D kamera nad mapou: pozice (střed pohledu ve world pixelech) + zoom.
/// Umí pan, zoom ke kurzoru a drží se v mezích světa. Čistě render vrstva —
/// simulace o kameře neví.
/// </summary>
public sealed class Camera2D
{
    private const float MaxZoom = 8f;

    /// <summary>Rezerva pod „zoom na celou mapu", ať jde mapu vidět celou i s okrajem.</summary>
    private const float MinZoomFitFactor = 0.85f;

    private float _worldWidth = 1f;
    private float _worldHeight = 1f;
    private int _viewportWidth = 1;
    private int _viewportHeight = 1;

    /// <summary>Střed pohledu ve world pixelech.</summary>
    public Vector2 Position { get; private set; }

    /// <summary>Měřítko: world pixely × zoom = pixely obrazovky.</summary>
    public float Zoom { get; private set; } = 1f;

    /// <summary>Transformace pro SpriteBatch (svět → obrazovka).</summary>
    public Matrix Transform =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(_viewportWidth * 0.5f, _viewportHeight * 0.5f, 0f);

    /// <summary>Nastaví rozměry světa v pixelech (mez pro pan a minimální zoom).</summary>
    public void SetWorldBounds(float worldWidth, float worldHeight)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        ClampZoomAndPosition();
    }

    /// <summary>Aktualizuje rozměry viewportu — volat každý snímek (okno jde zvětšovat).</summary>
    public void SetViewport(int width, int height)
    {
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
        ClampZoomAndPosition();
    }

    /// <summary>Vycentruje pohled na střed světa se zoomem „celá mapa na obrazovce".</summary>
    public void CenterOnWorld()
    {
        Position = new Vector2(_worldWidth * 0.5f, _worldHeight * 0.5f);
        Zoom = FitZoom();
        ClampZoomAndPosition();
    }

    /// <summary>Převod bodu obrazovky na world souřadnice.</summary>
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        return (screen - center) / Zoom + Position;
    }

    /// <summary>Pan tažením myši: svět se drží pod kurzorem, proto se pozice posouvá proti deltě.</summary>
    public void Pan(Vector2 screenDelta)
    {
        Position -= screenDelta / Zoom;
        ClampZoomAndPosition();
    }

    /// <summary>Pan o world delta (klávesy).</summary>
    public void PanWorld(Vector2 worldDelta)
    {
        Position += worldDelta;
        ClampZoomAndPosition();
    }

    /// <summary>Přiblíží/oddálí tak, aby bod pod kurzorem zůstal na místě.</summary>
    public void ZoomAt(Vector2 screenPoint, float factor)
    {
        var worldUnderCursor = ScreenToWorld(screenPoint);
        Zoom = Math.Clamp(Zoom * factor, MinZoom(), MaxZoom);

        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        Position = worldUnderCursor - (screenPoint - center) / Zoom;
        ClampZoomAndPosition();
    }

    private float FitZoom() =>
        MathF.Min(_viewportWidth / _worldWidth, _viewportHeight / _worldHeight);

    private float MinZoom() => MathF.Min(FitZoom() * MinZoomFitFactor, MaxZoom);

    private void ClampZoomAndPosition()
    {
        Zoom = Math.Clamp(Zoom, MinZoom(), MaxZoom);
        Position = new Vector2(
            Math.Clamp(Position.X, 0f, _worldWidth),
            Math.Clamp(Position.Y, 0f, _worldHeight));
    }
}
