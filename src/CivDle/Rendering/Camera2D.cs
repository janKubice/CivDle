using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// 2D kamera nad NEKONEČNOU mapou: pozice (střed pohledu ve world pixelech) + zoom.
/// Pan není nijak omezený (svět nemá hranice), zoom má pevné meze. Čistě render
/// vrstva — simulace o kameře neví.
/// </summary>
public sealed class Camera2D
{
    private const float MaxZoom = 6f;
    private const float MinZoom = 0.12f;

    private int _viewportWidth = 1;
    private int _viewportHeight = 1;

    /// <summary>Střed pohledu ve world pixelech.</summary>
    public Vector2 Position { get; set; }

    /// <summary>Měřítko: world pixely × zoom = pixely obrazovky.</summary>
    public float Zoom { get; private set; } = 1.6f;

    /// <summary>Transformace pro SpriteBatch (svět → obrazovka).</summary>
    public Matrix Transform =>
        Matrix.CreateTranslation(-Position.X, -Position.Y, 0f)
        * Matrix.CreateScale(Zoom, Zoom, 1f)
        * Matrix.CreateTranslation(_viewportWidth * 0.5f, _viewportHeight * 0.5f, 0f);

    /// <summary>Aktualizuje rozměry viewportu — volat každý snímek (okno jde zvětšovat).</summary>
    public void SetViewport(int width, int height)
    {
        _viewportWidth = Math.Max(1, width);
        _viewportHeight = Math.Max(1, height);
    }

    /// <summary>Vycentruje pohled na daný bod ve world pixelech se zvoleným zoomem.</summary>
    public void CenterOn(Vector2 worldPosition, float zoom)
    {
        Position = worldPosition;
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
    }

    /// <summary>Převod bodu obrazovky na world souřadnice.</summary>
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        return (screen - center) / Zoom + Position;
    }

    /// <summary>Převod world souřadnic na bod obrazovky (screen-space popupy).</summary>
    public Vector2 WorldToScreen(Vector2 world)
    {
        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        return (world - Position) * Zoom + center;
    }

    /// <summary>Viditelný výřez světa (pro culling — kreslí se jen, co je vidět).</summary>
    public (Vector2 Min, Vector2 Max) VisibleWorldBounds()
    {
        var min = ScreenToWorld(Vector2.Zero);
        var max = ScreenToWorld(new Vector2(_viewportWidth, _viewportHeight));
        return (min, max);
    }

    /// <summary>Pan tažením myši: svět se drží pod kurzorem, proto se pozice posouvá proti deltě.</summary>
    public void Pan(Vector2 screenDelta) => Position -= screenDelta / Zoom;

    /// <summary>Pan o world delta (klávesy).</summary>
    public void PanWorld(Vector2 worldDelta) => Position += worldDelta;

    /// <summary>Přiblíží/oddálí tak, aby bod pod kurzorem zůstal na místě.</summary>
    public void ZoomAt(Vector2 screenPoint, float factor)
    {
        var worldUnderCursor = ScreenToWorld(screenPoint);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);

        var center = new Vector2(_viewportWidth * 0.5f, _viewportHeight * 0.5f);
        Position = worldUnderCursor - (screenPoint - center) / Zoom;
    }
}
