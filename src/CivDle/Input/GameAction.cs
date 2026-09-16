namespace CivDle.Input;

/// <summary>
/// Herní akce, kterou si hráč může přemapovat na jinou klávesu.
///
/// <para>Záměrně sem <b>nepatří</b> všechno, co má klávesu. Escape je
/// univerzální „zpět" a přemapovat ho znamená hru zamknout; ladicí zkratky
/// (F8, F9, Ctrl+Shift+D) jsou nástroje na testování, ne herní obsah, a hráč,
/// který je nehledá, na ně nemá narazit.</para>
/// </summary>
public enum GameAction
{
    /// <summary>Posun kamery nahoru.</summary>
    CameraUp,

    /// <summary>Posun kamery dolů.</summary>
    CameraDown,

    /// <summary>Posun kamery vlevo.</summary>
    CameraLeft,

    /// <summary>Posun kamery vpravo.</summary>
    CameraRight,

    /// <summary>Kde to vázne — obarví budovy podle toho, proč nepracují.</summary>
    Bottlenecks,

    /// <summary>Pokrytí proudem.</summary>
    PowerOverlay,

    /// <summary>Dosah podmořské sítě.</summary>
    SubseaOverlay,

    /// <summary>Vrátit poslední akci.</summary>
    Undo,

    /// <summary>Přepnout násobič hromadné stavby.</summary>
    CycleBatch,

    /// <summary>Schovat rozhraní (pro snímek obrazovky).</summary>
    HideHud,

    /// <summary>Uložit sdílitelnou kartu.</summary>
    ShareCard,

    // Panely: otevřít a zavřít týmž písmenem. Panel, který se otevírá klávesou
    // a zavírá Escapem, se ovládá hůř než ten, co má jedno tlačítko na obojí.

    /// <summary>Strom výzkumu.</summary>
    OpenTech,

    /// <summary>Úkoly.</summary>
    OpenQuests,

    /// <summary>Výrobní řetězce.</summary>
    OpenChains,

    /// <summary>Vzestup.</summary>
    OpenAscend,

    /// <summary>Osady.</summary>
    OpenSettlements,
}
