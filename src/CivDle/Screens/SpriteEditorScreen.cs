using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Kreslítko spritů pro tvůrce obsahu (bod 2, krok 3).
///
/// <para>Mřížka 32×32 — stejné rozlišení, v jakém hra kreslí budovy — paleta,
/// štětec, kbelík a guma. Výsledek se uloží jako PNG do <c>sprites/</c> modu
/// a hra ho při dalším startu použije místo procedurálního modelu.</para>
///
/// <para>Proč přímo ve hře: mod bez obrázku je barevný čtvereček, a nutit
/// hráče kvůli jedné budově do externího editoru je přesně ta překážka, po
/// které nikdo nic neudělá.</para>
///
/// <para>Vrstva: UI. Model je pole barev, zápis je obyčejný PNG.</para>
/// </summary>
public sealed class SpriteEditorScreen : IScreen
{
    /// <summary>Rozlišení kresby. Musí sedět s <c>SpriteLibrary.SpriteSize</c>.</summary>
    private const int Size = 32;

    /// <summary>Kolik pixelů obrazovky zabírá jeden pixel kresby.</summary>
    private const int Scale = 14;

    private static readonly Color[] Palette =
    {
        new(0, 0, 0, 0),          // průhledná = guma
        new(30, 30, 36), new(90, 90, 100), new(150, 152, 158), new(226, 230, 236),
        new(120, 78, 44), new(176, 122, 62), new(214, 176, 110),
        new(70, 120, 60), new(120, 190, 90), new(180, 220, 120),
        new(60, 100, 160), new(90, 160, 220), new(150, 210, 245),
        new(160, 50, 50), new(220, 100, 70), new(240, 190, 90),
        new(120, 70, 150), new(200, 130, 220), new(240, 240, 250),
    };

    private readonly ScreenManager _screens;
    private readonly string _targetPath;
    private readonly InputManager _input = new();
    private readonly Color[] _pixels = new Color[Size * Size];

    private Desktop _desktop = null!;
    private int _color = 1;
    private bool _fillMode;
    private string _status = string.Empty;

    /// <param name="screens">Správce obrazovek.</param>
    /// <param name="targetPath">Kam se PNG uloží (soubor, ne složka).</param>
    public SpriteEditorScreen(ScreenManager screens, string targetPath)
    {
        _screens = screens;
        _targetPath = targetPath;
        LoadExisting();
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    /// <summary>Rozkreslený obrázek se při návratu do editoru načte zpátky.</summary>
    private void LoadExisting()
    {
        try
        {
            if (!File.Exists(_targetPath))
            {
                return;
            }

            using var stream = File.OpenRead(_targetPath);
            using var texture = Texture2D.FromStream(_screens.GraphicsDevice, stream);
            if (texture.Width == Size && texture.Height == Size)
            {
                texture.GetData(_pixels);
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            // Nečitelný obrázek = začíná se na prázdném plátně.
        }
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };

        layout.Widgets.Add(new Label
        {
            Text = loc["spriteedit.title"],
            TextColor = new Color(240, 205, 110),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["spriteedit.hint"],
            TextColor = Color.LightGray,
            Wrap = true,
            Width = Size * Scale,
        });

        var tools = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        tools.Widgets.Add(UiFactory.SmallButton(
            _fillMode ? loc["spriteedit.brush"] : loc["spriteedit.fill"],
            () =>
            {
                _fillMode = !_fillMode;
                BuildUi();
            }));
        tools.Widgets.Add(UiFactory.SmallButton(loc["spriteedit.clear"], () =>
        {
            Array.Clear(_pixels);
            BuildUi();
        }));
        tools.Widgets.Add(UiFactory.SmallButton(loc["spriteedit.save"], Save));
        tools.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        layout.Widgets.Add(tools);

        if (_status.Length > 0)
        {
            layout.Widgets.Add(new Label { Text = _status, TextColor = new Color(150, 220, 150) });
        }

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Top;
        panel.Margin = new Myra.Graphics2D.Thickness(0, 12, 0, 0);

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
            return;
        }

        var viewport = _screens.GraphicsDevice.Viewport;
        var origin = CanvasOrigin(viewport);
        var mouse = _input.MousePosition;

        // Paleta pod plátnem: klik na políčko vybere barvu (první je guma).
        var paletteOrigin = new Point(origin.X, origin.Y + Size * Scale + 10);
        if (_input.WasLeftPressed && mouse.Y >= paletteOrigin.Y && mouse.Y < paletteOrigin.Y + 24)
        {
            int index = (mouse.X - paletteOrigin.X) / 24;
            if (index >= 0 && index < Palette.Length)
            {
                _color = index;
                return;
            }
        }

        if (!_input.IsLeftDown && !_input.IsRightDown)
        {
            return;
        }

        int x = (mouse.X - origin.X) / Scale;
        int y = (mouse.Y - origin.Y) / Scale;
        if (x < 0 || y < 0 || x >= Size || y >= Size)
        {
            return;
        }

        // Pravé tlačítko maže vždycky — nejčastější oprava nemá stát za výběr
        // gumy v paletě.
        var color = _input.IsRightDown ? Color.Transparent : Palette[_color];
        if (_fillMode && _input.WasLeftPressed)
        {
            Fill(x, y, color);
            return;
        }

        _pixels[y * Size + x] = color;
    }

    /// <summary>Kbelík: přebarví souvislou plochu stejné barvy (vlnou, bez rekurze).</summary>
    private void Fill(int startX, int startY, Color color)
    {
        var from = _pixels[startY * Size + startX];
        if (from == color)
        {
            return;
        }

        var queue = new Queue<Point>();
        queue.Enqueue(new Point(startX, startY));
        while (queue.Count > 0)
        {
            var point = queue.Dequeue();
            if (point.X < 0 || point.Y < 0 || point.X >= Size || point.Y >= Size)
            {
                continue;
            }

            int index = point.Y * Size + point.X;
            if (_pixels[index] != from)
            {
                continue;
            }

            _pixels[index] = color;
            queue.Enqueue(new Point(point.X + 1, point.Y));
            queue.Enqueue(new Point(point.X - 1, point.Y));
            queue.Enqueue(new Point(point.X, point.Y + 1));
            queue.Enqueue(new Point(point.X, point.Y - 1));
        }
    }

    private void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_targetPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var texture = new Texture2D(_screens.GraphicsDevice, Size, Size);
            texture.SetData(_pixels);
            using var stream = File.Create(_targetPath);
            texture.SaveAsPng(stream, Size, Size);
            _status = _screens.Loc.Format("spriteedit.saved", Path.GetFileName(_targetPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status = ex.Message;
        }

        BuildUi();
    }

    private static Point CanvasOrigin(Viewport viewport) =>
        new((viewport.Width - Size * Scale) / 2, 150);

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        var origin = CanvasOrigin(viewport);
        var white = _screens.WhitePixel;

        spriteBatch.Begin();
        spriteBatch.Draw(white, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.8f);

        // Šachovnice pod kresbou: bez ní nejde poznat průhledný pixel od bílého.
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var cell = new Rectangle(origin.X + x * Scale, origin.Y + y * Scale, Scale, Scale);
                spriteBatch.Draw(white, cell, (x + y) % 2 == 0 ? new Color(60, 62, 70) : new Color(48, 50, 58));

                var color = _pixels[y * Size + x];
                if (color.A > 0)
                {
                    spriteBatch.Draw(white, cell, color);
                }
            }
        }

        // Paleta.
        var paletteOrigin = new Point(origin.X, origin.Y + Size * Scale + 10);
        for (int i = 0; i < Palette.Length; i++)
        {
            var box = new Rectangle(paletteOrigin.X + i * 24, paletteOrigin.Y, 22, 22);
            spriteBatch.Draw(white, box, Palette[i].A > 0 ? Palette[i] : new Color(90, 90, 100));
            if (i == _color)
            {
                spriteBatch.Draw(white, new Rectangle(box.X, box.Bottom, box.Width, 3), new Color(255, 220, 120));
            }
        }

        spriteBatch.End();
        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;
}
