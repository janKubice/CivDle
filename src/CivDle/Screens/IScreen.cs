using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Jedna obrazovka v zásobníku <see cref="ScreenManager"/> (menu, hra, pauza…).
/// Aktualizuje se vždy jen vrchní obrazovka — překrytí hry pauzou tím pádem
/// automaticky pozastaví simulaci.
/// </summary>
public interface IScreen : IDisposable
{
    /// <summary>
    /// Overlay se kreslí přes obrazovku pod sebou (pauza ztmaví běžící hru);
    /// ne-overlay obrazovka vše pod sebou zakrývá.
    /// </summary>
    bool IsOverlay { get; }

    /// <summary>Logika obrazovky — volá se jen, když je obrazovka navrchu.</summary>
    void Update(GameTime gameTime);

    /// <summary>Vykreslení — může se volat i pod overlayem.</summary>
    void Draw(GameTime gameTime);

    /// <summary>
    /// Obrazovka se (znovu) stala vrchní. Slouží k resynchronizaci vstupu,
    /// aby po návratu z pauzy „nedojel" starý stav myši/klávesnice.
    /// </summary>
    void OnActivated()
    {
    }
}
