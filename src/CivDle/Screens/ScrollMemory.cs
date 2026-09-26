using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Pamatuje si, kam byl seznam odrolovaný, přes přestavbu obrazovky.
///
/// <para>Proč to existuje: obrazovky s nákupy a přepínači se po každém kliknutí
/// staví celé znovu (ceny, stavy tlačítek) — a s nimi i scroller. Nový scroller
/// začíná nahoře, takže hráč po každé koupi hledal, kde v seznamu byl. Dřív to
/// uměla jen obrazovka Vzestupu, každá po svém; tohle je ten jeden postup pro
/// všechny.</para>
///
/// <para>Použití: v přestavbě obal nový scroller <see cref="Track"/> (vezme si
/// pozici ze starého dřív, než zmizí) a po vytvoření desktopu zavolej
/// <see cref="Restore"/>.</para>
/// </summary>
internal sealed class ScrollMemory
{
    private ScrollViewer? _current;
    private Point _position;

    /// <summary>Převezme nový scroller. Pozici starého si uloží, než ho přestavba zahodí.</summary>
    public ScrollViewer Track(ScrollViewer viewer)
    {
        if (_current is not null && !ReferenceEquals(_current, viewer))
        {
            _position = _current.ScrollPosition;
        }

        _current = viewer;
        return viewer;
    }

    /// <summary>
    /// Vrátí pozici nad hotovým rozvržením.
    ///
    /// <para>Rozvržení se vynutí hned, ne až při prvním vykreslení: scroller do
    /// té doby nezná svou výšku a pozici by uřízl na nulu — a hráč by jeden
    /// snímek viděl seznam odrolovaný nahoru, což je právě to škubnutí.</para>
    /// </summary>
    public void Restore(Desktop desktop)
    {
        if (_current is null || _position == Point.Zero)
        {
            return;
        }

        desktop.UpdateLayout();
        _current.ScrollPosition = _position;
    }
}
