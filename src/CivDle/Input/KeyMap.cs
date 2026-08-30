using CivDle.Core.Config;
using Microsoft.Xna.Framework.Input;

namespace CivDle.Input;

/// <summary>
/// Které klávesy patří ke kterým akcím.
///
/// <para>Proč to ve hře je: klávesy byly natvrdo v kódu a nikde vypsané.
/// Hráč, který si nepamatuje, že inspektor je na B, ho nemá jak najít — a kdo
/// má jiné rozložení klávesnice než QWERTY, nemá jak si ho přendat.</para>
///
/// <para>Uloženými hodnotami jsou <b>jména kláves</b>, ne čísla: soubor
/// s nastavením má zůstat čitelný a jádro hry nesmí znát MonoGame.</para>
///
/// <para>Neznámé jméno (překlep v souboru, klávesa z jiné verze) se tiše
/// nahradí výchozí — nastavení nikdy nesmí hráči zamknout ovládání.</para>
/// </summary>
public sealed class KeyMap
{
    /// <summary>Výchozí rozložení. Druhá klávesa je alternativa (šipky vedle WASD).</summary>
    private static readonly (GameAction Action, Keys Primary, Keys? Alternate)[] Defaults =
    {
        (GameAction.CameraUp, Keys.W, Keys.Up),
        (GameAction.CameraDown, Keys.S, Keys.Down),
        (GameAction.CameraLeft, Keys.A, Keys.Left),
        (GameAction.CameraRight, Keys.D, Keys.Right),
        (GameAction.Bottlenecks, Keys.B, null),
        (GameAction.PowerOverlay, Keys.E, null),
        (GameAction.SubseaOverlay, Keys.M, null),
        (GameAction.Undo, Keys.Z, null),
        (GameAction.CycleBatch, Keys.Tab, null),
        (GameAction.HideHud, Keys.F11, null),
        (GameAction.ShareCard, Keys.F12, null),
    };

    private readonly Dictionary<GameAction, Keys> _keys = new();
    private readonly Dictionary<GameAction, Keys?> _alternates = new();

    public KeyMap(PlayerProfile profile)
    {
        foreach (var (action, primary, alternate) in Defaults)
        {
            _keys[action] = primary;
            _alternates[action] = alternate;
        }

        foreach (var (id, keyName) in profile.KeyBindings)
        {
            if (Enum.TryParse<GameAction>(id, ignoreCase: true, out var action)
                && Enum.TryParse<Keys>(keyName, ignoreCase: true, out var key))
            {
                _keys[action] = key;

                // Vlastní volba nahradí i alternativu: hráč, který si posun
                // vlevo dal na J, nechce, aby mu pořád fungovala i šipka —
                // to by při přemapování dvou akcí na kříž znamenalo konflikt,
                // o kterém by nevěděl.
                _alternates[action] = null;
            }
        }
    }

    /// <summary>Klávesa akce.</summary>
    public Keys KeyFor(GameAction action) => _keys[action];

    /// <summary>Druhá klávesa akce, pokud ji výchozí rozložení má.</summary>
    public Keys? AlternateFor(GameAction action) => _alternates[action];

    /// <summary>Je akce právě držená?</summary>
    public bool IsDown(InputManager input, GameAction action) =>
        input.IsDown(_keys[action])
        || (_alternates[action] is { } alternate && input.IsDown(alternate));

    /// <summary>Byla akce právě stisknuta?</summary>
    public bool WasPressed(InputManager input, GameAction action) =>
        input.WasPressed(_keys[action])
        || (_alternates[action] is { } alternate && input.WasPressed(alternate));

    /// <summary>Kterou akci už tahle klávesa obsluhuje? <c>null</c> = žádnou.</summary>
    public GameAction? ConflictOf(Keys key, GameAction ignore)
    {
        foreach (var (action, bound) in _keys)
        {
            if (action != ignore && bound == key)
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>Je tohle výchozí nastavení akce?</summary>
    public static Keys DefaultFor(GameAction action)
    {
        foreach (var (candidate, primary, _) in Defaults)
        {
            if (candidate == action)
            {
                return primary;
            }
        }

        return Keys.None;
    }

    /// <summary>
    /// Vyrobí nový slovník do profilu. Ukládají se <b>jen změny</b> proti
    /// výchozímu rozložení — soubor pak nese to, co hráč opravdu udělal, a při
    /// změně výchozích kláves v nové verzi se mu nezakonzervují ty staré.
    /// </summary>
    public Dictionary<string, string> ToSettings()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (action, key) in _keys)
        {
            if (key != DefaultFor(action))
            {
                result[action.ToString()] = key.ToString();
            }
        }

        return result;
    }

    /// <summary>Přemapuje akci.</summary>
    public void Rebind(GameAction action, Keys key)
    {
        _keys[action] = key;
        _alternates[action] = null;
    }

    /// <summary>Vrátí akci na výchozí klávesu i s alternativou.</summary>
    public void Reset(GameAction action)
    {
        foreach (var (candidate, primary, alternate) in Defaults)
        {
            if (candidate == action)
            {
                _keys[action] = primary;
                _alternates[action] = alternate;
                return;
            }
        }
    }

    /// <summary>Vrátí všechno na výchozí.</summary>
    public void ResetAll()
    {
        foreach (var (action, primary, alternate) in Defaults)
        {
            _keys[action] = primary;
            _alternates[action] = alternate;
        }
    }
}
