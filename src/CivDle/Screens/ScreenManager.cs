using CivDle.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Zásobník obrazovek: menu → nová hra → hra, pauza jako overlay navrchu.
/// Aktualizuje jen vrchní obrazovku; kreslí od nejvyšší ne-overlay obrazovky nahoru.
/// Obrazovkám zároveň půjčuje sdílené závislosti (obsah, grafiku) — DI bez singletonů.
/// </summary>
public sealed class ScreenManager
{
    private readonly List<IScreen> _screens = new();

    public ScreenManager(CivDleGame game, GameContent content)
    {
        Game = game;
        Content = content;
    }

    /// <summary>Herní aplikace (kvůli ukončení a přístupu ke grafice).</summary>
    public CivDleGame Game { get; }

    /// <summary>Načtený herní obsah — obrazovky ho jen čtou.</summary>
    public GameContent Content { get; }

    public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

    public SpriteBatch SpriteBatch => Game.SpriteBatch;

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
