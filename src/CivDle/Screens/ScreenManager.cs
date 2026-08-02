using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Save;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Zásobník obrazovek: menu → nová hra → hra, pauza jako overlay navrchu.
/// Aktualizuje jen vrchní obrazovku; kreslí od nejvyšší ne-overlay obrazovky nahoru.
/// Obrazovkám zároveň půjčuje sdílené závislosti (obsah, lokalizaci, grafiku,
/// nastavení) — DI bez singletonů.
/// </summary>
public sealed class ScreenManager
{
    private readonly List<IScreen> _screens = new();

    public ScreenManager(CivDleGame game, GameContent content, Localization localization, SaveStore saves)
    {
        Game = game;
        Content = content;
        Loc = localization;
        Saves = saves;
    }

    /// <summary>Úložiště uložené hry (jeden slot, MVP).</summary>
    public SaveStore Saves { get; }

    /// <summary>Herní aplikace (kvůli ukončení a přístupu ke grafice).</summary>
    public CivDleGame Game { get; }

    /// <summary>Načtený herní obsah — obrazovky ho jen čtou.</summary>
    public GameContent Content { get; }

    /// <summary>Překlady — obrazovky se přes event přestavují po změně jazyka.</summary>
    public Localization Loc { get; }

    /// <summary>Aktuální uživatelská nastavení.</summary>
    public GameSettings Settings => Game.Settings;

    /// <summary>Účet-wide profil hráče (odemčené achievementy).</summary>
    public PlayerProfile Profile => Game.Profile;

    /// <summary>Uloží profil na disk (po odemčení achievementu).</summary>
    public void SaveProfile() => Game.SaveProfile();

    /// <summary>
    /// Vyrobí Myra plochu se zvětšením UI z nastavení. Obrazovky si Desktop
    /// nevytvářejí samy, aby zvětšení platilo všude stejně a nešlo ho někde
    /// zapomenout — Myra hit-testuje přes inverzní transformaci, takže se
    /// zvětšeným UI zůstává klikání přesné.
    /// </summary>
    public Myra.Graphics2D.UI.Desktop NewDesktop(Myra.Graphics2D.UI.Widget root)
    {
        float scale = Settings.SafeUiScale;
        root.Scale = new Vector2(scale, scale);
        return new Myra.Graphics2D.UI.Desktop { Root = root };
    }

    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

    public SpriteBatch SpriteBatch => Game.SpriteBatch;

    /// <summary>Sdílená 1×1 bílá textura na obdélníky (overlay, budovy).</summary>
    public Texture2D WhitePixel => Game.WhitePixel;

    /// <summary>Procedurální sprity a ikony (suroviny, budovy, objekty, agenti).</summary>
    public Rendering.Sprites.SpriteLibrary Sprites => Game.Sprites;

    /// <summary>Sdílené zvuky akcí — cinknutí, žuchnutí, seknutí.</summary>
    public Audio.GameSounds Sounds => Game.Sounds;

    private MenuBackground? _menuBackground;

    /// <summary>Sdílené živé město na pozadí menu (líně vytvořené, roste napříč obrazovkami menu).</summary>
    public MenuBackground MenuBackground => _menuBackground ??= new MenuBackground(this);

    /// <summary>Uvolní pozadí menu (volá herní obrazovka, aby se pod hrou netikala druhá simulace).</summary>
    public void DisposeMenuBackground()
    {
        _menuBackground?.Dispose();
        _menuBackground = null;
    }

    /// <summary>Uloží a aplikuje nastavení (grafiku hned; jazyk přes <see cref="Loc"/>).</summary>
    public void ApplySettings(GameSettings settings)
    {
        // Zvětšení UI a barevná vodítka se propisují až do rozvržení widgetů,
        // takže se obrazovky musí přestavět. Jazyk má vlastní event; tenhle je
        // pro nastavení vzhledu, aby se změna projevila hned a ne až po restartu.
        bool uiChanged = Settings.SafeUiScale != settings.SafeUiScale || Settings.ColorCues != settings.ColorCues;
        Game.ApplySettings(settings);
        if (uiChanged)
        {
            UiSettingsChanged?.Invoke();
        }
    }

    /// <summary>Změnilo se nastavení, které mění vzhled UI — obrazovky se mají přestavět.</summary>
    public event Action? UiSettingsChanged;

    /// <summary>Je obrazovka navrchu zásobníku (a má tedy dostávat vstup)?</summary>
    public bool IsTop(IScreen screen) => _screens.Count > 0 && ReferenceEquals(_screens[^1], screen);

    /// <summary>
    /// Vykreslí desktop obrazovky — a vstup mu předá, jen když je navrchu.
    ///
    /// <para>Tohle je oprava celé třídy chyb „tlačítko nefunguje": obrazovka pod
    /// overlayem dál volala <c>Desktop.Render()</c>, který zpracovává i myš.
    /// Klik na „Zpět" v návodu tak zároveň klikl do menu POD ním — to znovu
    /// otevřelo tentýž návod a vypadalo to, že tlačítko nedělá nic. Vstup patří
    /// jen vrchní obrazovce; spodní se smí jen kreslit.</para>
    /// </summary>
    public void RenderDesktop(IScreen owner, Myra.Graphics2D.UI.Desktop desktop)
    {
        if (IsTop(owner))
        {
            desktop.Render();
            return;
        }

        desktop.UpdateLayout();
        desktop.RenderVisual();
    }

    /// <summary>Položí obrazovku navrch zásobníku.</summary>
    public void Push(IScreen screen)
    {
        _screens.Add(screen);
        screen.OnActivated();
    }

    /// <summary>Sundá vrchní obrazovku a aktivuje tu pod ní.</summary>
    public void Pop()
    {
        if (_screens.Count == 0)
        {
            return;
        }

        var top = _screens[^1];
        _screens.RemoveAt(_screens.Count - 1);
        top.Dispose();

        if (_screens.Count > 0)
        {
            _screens[^1].OnActivated();
        }
    }

    /// <summary>Zahodí celý zásobník (např. návrat do hlavního menu) a začne novou obrazovkou.</summary>
    public void ReplaceAll(IScreen screen)
    {
        foreach (var s in _screens)
        {
            s.Dispose();
        }

        _screens.Clear();
        Push(screen);
    }

    /// <summary>Ukončí hru.</summary>
    public void ExitGame() => Game.Exit();

    public void Update(GameTime gameTime)
    {
        if (_screens.Count > 0)
        {
            _screens[^1].Update(gameTime);
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_screens.Count == 0)
        {
            return;
        }

        int first = _screens.Count - 1;
        while (first > 0 && _screens[first].IsOverlay)
        {
            first--;
        }

        for (int i = first; i < _screens.Count; i++)
        {
            _screens[i].Draw(gameTime);
        }
    }
}
