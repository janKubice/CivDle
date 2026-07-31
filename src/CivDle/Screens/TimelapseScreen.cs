using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Časosběr: přehrávka celého růstu města od první chalupy po metropoli.
///
/// <para>Proč to ve hře je: hráč staví hodiny a pak Vzestupem všechno smaže.
/// Zůstane mu číslo v bilanci — ale ne <b>obraz</b> toho, co postavil. Tohle je
/// ta jediná věc, která z dlouhé tiché práce dělá příběh, na který jde ukázat
/// prstem: „tady jsem měl tři chalupy, tady už tohle".</para>
///
/// <para>Vrstva: čte hotové snímky ze simulace a nic nepočítá ani nemění.
/// Kreslí ve screen-space vlastní mřížkou, takže nezávisí na kameře ani na tom,
/// kde hráč zrovna je.</para>
/// </summary>
public sealed class TimelapseScreen : IScreen
{
    /// <summary>Kolik snímků za sekundu se přehrává.</summary>
    private const float FramesPerSecond = 12f;

    /// <summary>Jak dlouho se drží poslední snímek, než se přehrávka zopakuje.</summary>
    private const float HoldSeconds = 1.8f;

    private readonly ScreenManager _screens;
    private readonly CityHistory _history;
    private readonly InputManager _input = new();

    private Desktop _desktop = null!;
    private Label _caption = null!;
    private float _position;
    private float _holdTimer;
    private bool _playing = true;

    public TimelapseScreen(ScreenManager screens, CityHistory history)
    {
        _screens = screens;
        _history = history;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
            return;
        }

        if (_input.WasPressed(Keys.Space))
        {
            _playing = !_playing;
        }

        if (_history.Count == 0 || !_playing)
        {
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Na konci se chvíli počká a pak se jede znovu — hotové město si zaslouží,
        // aby na něj šlo koukat, a smyčka ušetří tlačítko „přehrát znovu".
        if (_position >= _history.Count - 1)
        {
            _holdTimer += dt;
            if (_holdTimer >= HoldSeconds)
            {
                _holdTimer = 0f;
                _position = 0f;
            }
        }
        else
        {
            _position = Math.Min(_history.Count - 1, _position + dt * FramesPerSecond);
        }

        UpdateCaption();
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.82f);
        DrawFrame(spriteBatch, pixel, viewport);
        spriteBatch.End();

        _desktop.Render();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    /// <summary>
    /// Nakreslí půdorys aktuálního snímku jako čtvercovou mapu uprostřed obrazovky.
    ///
    /// <para>Předchozí snímek se kreslí pod ním slabě — je pak vidět, <b>co</b>
    /// zrovna přibylo, ne jen že se něco změnilo.</para>
    /// </summary>
    private void DrawFrame(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        if (_history.Count == 0)
        {
            return;
        }

        int index = Math.Clamp((int)_position, 0, _history.Count - 1);
        int previous = Math.Max(0, index - 1);

        int size = Math.Min(viewport.Width, viewport.Height) - 240;
        int cell = Math.Max(2, size / CityHistory.GridSize);
        size = cell * CityHistory.GridSize;
        int left = (viewport.Width - size) / 2;
        int top = (viewport.Height - size) / 2 - 20;

        // Podklad mapy, ať je vidět, kde končí zaznamenaná plocha.
        spriteBatch.Draw(pixel, new Rectangle(left, top, size, size), new Color(22, 26, 32));

        var grown = new Color(255, 226, 150);
        var settled = new Color(120, 196, 150);
        for (int cy = 0; cy < CityHistory.GridSize; cy++)
        {
            for (int cx = 0; cx < CityHistory.GridSize; cx++)
            {
                if (!_history.IsOccupied(index, cx, cy))
                {
                    continue;
                }

                bool isNew = !_history.IsOccupied(previous, cx, cy);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(left + cx * cell, top + cy * cell, cell - 1, cell - 1),
                    isNew ? grown : settled);
            }
        }

        // Pruh postupu pod mapou — hráč vidí, kde v příběhu je.
        int barY = top + size + 12;
        float progress = _history.Count <= 1 ? 1f : _position / (_history.Count - 1);
        spriteBatch.Draw(pixel, new Rectangle(left, barY, size, 4), new Color(60, 66, 74));
        spriteBatch.Draw(
            pixel, new Rectangle(left, barY, (int)(size * progress), 4), new Color(255, 226, 150));
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["timelapse.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(255, 226, 150),
        });

        _caption = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        };
        layout.Widgets.Add(_caption);

        var bottom = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 24),
        };

        // Prázdná kronika není chyba — na začátku hry ještě není co přehrávat.
        if (_history.Count == 0)
        {
            bottom.Widgets.Add(new Label
            {
                Text = loc["timelapse.empty"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 160, 175),
            });
        }

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.toggle"], () => _playing = !_playing));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.restart"], () =>
        {
            _position = 0f;
            _holdTimer = 0f;
            _playing = true;
        }));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        bottom.Widgets.Add(buttons);

        var root = new Panel();
        root.Widgets.Add(layout);
        root.Widgets.Add(bottom);
        _desktop = _screens.NewDesktop(root);
        UpdateCaption();
    }

    private void UpdateCaption()
    {
        if (_history.Count == 0)
        {
            _caption.Text = string.Empty;
            return;
        }

        var frame = _history.FrameAt(Math.Clamp((int)_position, 0, _history.Count - 1));
        _caption.Text = _screens.Loc.Format(
            "timelapse.caption",
            DurationFormat.Human(frame.Seconds),
            CivDle.Core.Numbers.Format(frame.Population),
            frame.Buildings);
    }
}
