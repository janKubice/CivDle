using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Doktríny: cesta, kterou se tahle civilizace vydala.
///
/// <para>Obrazovka má dvě podoby. Dokud není vybráno, ukazuje <b>cesty vedle
/// sebe</b> — to je ta chvíle, kdy se rozhoduje, a hráč potřebuje vidět, co
/// si tím zavře. Jakmile je vybráno, ukazuje jen <b>tu jednu</b> a její uzly;
/// nabízet dál i ostatní by slibovalo něco, co si už koupit nemůže.</para>
///
/// <para>Vrstva: UI. Pravidla („aktivní je jen jedna", „prerekvizity", „cena")
/// jsou v simulaci.</para>
/// </summary>
public sealed class DoctrinesScreen : IScreen
{
    private const int PanelWidth = 760;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public DoctrinesScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["doctrines.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["doctrines.desc"],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("doctrines.points", CivDle.Core.Numbers.Format(_simulation.PrestigePoints)),
            TextColor = UiPalette.Accent,
        });

        var list = new VerticalStackPanel { Spacing = 8, Width = PanelWidth - 20 };

        if (_simulation.Doctrine is { } chosen)
        {
            list.Widgets.Add(Card(chosen, _simulation.DoctrineIndex, showNodes: true));
        }
        else
        {
            var catalog = _screens.Content.Doctrines;
            for (int i = 0; i < catalog.Count; i++)
            {
                list.Widgets.Add(Card(catalog[i], i, showNodes: false));
            }
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Width = PanelWidth - 10, Height = 380 });

        // Věta o vracení bodů je tady schválně: bez ní vypadá volba jako past
        // a hráč si ji radši nechá „na potom", což znamená napořád.
        layout.Widgets.Add(new Label
        {
            Text = loc["doctrines.resetHint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget Card(DoctrineDef doctrine, int index, bool showNodes)
    {
        var loc = _screens.Loc;
        var card = new VerticalStackPanel
        {
            Spacing = 4,
            Width = PanelWidth - 50,
            Padding = new Thickness(12, 8),
            Background = new PanelBrush(UiPalette.PanelDeep),
        };

        card.Widgets.Add(new Label { Text = loc[doctrine.NameKey], TextColor = UiPalette.TextBright });
        card.Widgets.Add(new Label
        {
            Text = loc[doctrine.DescriptionKey],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 90,
        });

        if (!showNodes)
        {
            card.Widgets.Add(UiFactory.SmallButton(loc["doctrines.choose"], () =>
            {
                _simulation.TryChooseDoctrine(index);
                BuildUi();
            }));

            return card;
        }

        card.Widgets.Add(new Label { Text = loc["doctrines.chosen"], TextColor = UiPalette.Good });

        for (int i = 0; i < doctrine.Nodes.Count; i++)
        {
            card.Widgets.Add(NodeRow(doctrine, i));
        }

        return card;
    }

    private Widget NodeRow(DoctrineDef doctrine, int nodeIndex)
    {
        var loc = _screens.Loc;
        var node = doctrine.Nodes[nodeIndex];
        var row = new HorizontalStackPanel { Spacing = 8 };

        row.Widgets.Add(new Label
        {
            Text = loc[doctrine.NodeNameKey(nodeIndex)],
            TextColor = _simulation.IsDoctrineNodeOwned(nodeIndex) ? UiPalette.Good : UiPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
        });

        if (_simulation.IsDoctrineNodeOwned(nodeIndex))
        {
            row.Widgets.Add(new Label
            {
                Text = loc["doctrines.owned"],
                TextColor = UiPalette.Good,
                VerticalAlignment = VerticalAlignment.Center,
            });

            return row;
        }

        // Nesplněná prerekvizita se řekne jménem. „Zamčeno" bez důvodu je ta
        // nejhorší podoba stromu — hráč pak zkouší klikat naslepo.
        if (MissingPrerequisite(doctrine, node) is { } missing)
        {
            row.Widgets.Add(new Label
            {
                Text = loc.Format("doctrines.needs", loc[missing]),
                TextColor = UiPalette.TextDim,
                VerticalAlignment = VerticalAlignment.Center,
            });

            return row;
        }

        var button = UiFactory.SmallButton(loc.Format("doctrines.buy", node.Cost), () =>
        {
            _simulation.TryBuyDoctrineNode(nodeIndex);
            BuildUi();
        });
        button.Enabled = _simulation.CanBuyDoctrineNode(nodeIndex);
        row.Widgets.Add(button);
        return row;
    }

    /// <summary>Klíč jména prvního nekoupeného předchůdce, nebo <c>null</c>.</summary>
    private string? MissingPrerequisite(DoctrineDef doctrine, DoctrineNodeDef node)
    {
        for (int i = 0; i < node.PrerequisiteIndices.Count; i++)
        {
            int required = node.PrerequisiteIndices[i];
            if (!_simulation.IsDoctrineNodeOwned(required))
            {
                return doctrine.NodeNameKey(required);
            }
        }

        return null;
    }
}
