using CivDle.Core.Content;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;

namespace CivDle;

/// <summary>
/// Kompoziční kořen hry: okno, herní smyčka MonoGame a start aplikace.
/// Při startu načte herní obsah (fail-fast) a předá řízení zásobníku obrazovek —
/// sám žádnou herní logiku nedrží.
/// </summary>
public sealed class CivDleGame : Game
{
    private static readonly Color BackgroundColor = new(24, 26, 32);

    private readonly GraphicsDeviceManager _graphics;
    private ScreenManager? _screens;

    public CivDleGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720,
        };
        Window.Title = "CivDle";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    /// <summary>Sdílený SpriteBatch pro všechny obrazovky.</summary>
    public SpriteBatch SpriteBatch { get; private set; } = null!;

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        MyraEnvironment.Game = this;

        // Data leží vedle binárky — funguje pro `dotnet run` i pro publish jedním exe.
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));

        _screens = new ScreenManager(this, content);
        _screens.ReplaceAll(new MainMenuScreen(_screens));
    }

    protected override void Update(GameTime gameTime)
    {
        _screens!.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);
        _screens!.Draw(gameTime);
        base.Draw(gameTime);
    }
}
