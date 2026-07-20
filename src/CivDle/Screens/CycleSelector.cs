using FontStashSharp.RichText;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Přepínač hodnot „&lt; hodnota &gt;" — sdílený prvek menu nové hry a nastavení.
/// Popisek se získává funkcí, takže po změně jazyka stačí <see cref="Refresh"/>.
/// </summary>
public sealed class CycleSelector
{
    private readonly int _count;
    private readonly Func<int, string> _labelFor;
    private readonly Label _label;

    public CycleSelector(int count, int initialIndex, Func<int, string> labelFor)
    {
        _count = count;
        _labelFor = labelFor;
        Index = Math.Clamp(initialIndex, 0, count - 1);

        _label = new Label
        {
            Width = 220,
            TextAlign = TextHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        Widget = new HorizontalStackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        Widget.Widgets.Add(UiFactory.SmallButton("<", () => Move(-1)));
        Widget.Widgets.Add(_label);
        Widget.Widgets.Add(UiFactory.SmallButton(">", () => Move(+1)));
        Refresh();
    }

    /// <summary>Kořenový widget k vložení do layoutu.</summary>
    public HorizontalStackPanel Widget { get; }

    /// <summary>Index aktuálně zvolené hodnoty.</summary>
    public int Index { get; private set; }

    /// <summary>Vyvolá se po přepnutí hodnoty uživatelem.</summary>
    public event Action<int>? SelectionChanged;

    /// <summary>Znovu vykreslí popisek (po změně jazyka).</summary>
    public void Refresh() => _label.Text = _labelFor(Index);

    private void Move(int delta)
    {
        Index = (Index + delta + _count) % _count;
        Refresh();
        SelectionChanged?.Invoke(Index);
    }
}
