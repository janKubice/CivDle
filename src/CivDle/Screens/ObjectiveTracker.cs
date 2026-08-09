using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Panel „co teď" v levé části HUD. Vlastní celý obsah boxu s cíli: nahoře jeden
/// zvýrazněný hlavní cíl, pod ním drobně to, co běží zároveň.
///
/// <para>Vzniklo z hráčovy zpětné vazby „cítím se zmateně, nevím, co po mně hra
/// chce". Tři rovnocenné úkoly vedle sebe totiž neřeknou, čím začít, a žádný
/// z nich neřekne <em>jak</em> na to. Hlavní cíl proto nese i větu s návodem
/// a tlačítko, které rovnou otevře, co je potřeba.</para>
///
/// <para>Zdroj hlavního cíle: dokud běží průvodce prvními kroky, je to jeho
/// aktuální krok; potom první nesplněný úkol. Průvodce a úkoly se tak nepřebíjejí
/// — hráč vždycky vidí právě jednu „další věc".</para>
///
/// <para>Vrstva UI: jen čte simulaci a volá zpět akce obrazovky, nezapisuje do ní.</para>
/// </summary>
internal sealed class ObjectiveTracker
{
    private const int BarWidth = 190;
    private const int MaxSideGoals = 2;

    private static readonly Color HeadlineColor = new(255, 214, 120);
    private static readonly Color HintColor = new(186, 198, 214);
    private static readonly Color ProgressColor = new(150, 220, 150);

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Action<FocusHint> _onFocus;
    private readonly Action _onSkipGuide;

    private readonly VerticalStackPanel _panel = new() { Spacing = 5 };
    private readonly List<(GoalCondition Condition, Label Progress, ProgressBar Bar)> _slots = new();

    private bool _dirty = true;
    private int _builtForStep = -1;

    public ObjectiveTracker(ScreenManager screens, Simulation simulation, Action<FocusHint> onFocus, Action onSkipGuide)
    {
        _screens = screens;
        _simulation = simulation;
        _onFocus = onFocus;
        _onSkipGuide = onSkipGuide;

        var box = UiFactory.DarkPanel(_panel);
        box.HorizontalAlignment = HorizontalAlignment.Left;
        box.VerticalAlignment = VerticalAlignment.Center;
        box.Margin = new Thickness(10, 0, 0, 0);
        Root = box;
    }

    /// <summary>Widget k vložení do rozvržení HUD.</summary>
    public Widget Root { get; }

    /// <summary>Obsah se změnil (splněný úkol, nový krok průvodce) — přestav při dalším snímku.</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>
    /// Volá se každý snímek: přestaví obsah, jen když je potřeba, jinak jen
    /// dopíše čísla pokroku (přestavba widgetů 60× za vteřinu je zbytečná alokace).
    /// </summary>
    public void Update()
    {
        // Krok průvodce se posouvá v simulaci, ne kliknutím v UI — panel si proto
        // hlídá i jeho změnu sám, jinak by ukazoval starý krok až do dalšího úkolu.
        if (_dirty || _builtForStep != _simulation.TutorialStep)
        {
            Rebuild();
            _dirty = false;
            _builtForStep = _simulation.TutorialStep;
        }

        foreach (var (condition, progress, bar) in _slots)
        {
            long current = Math.Min(_simulation.EvaluateMetric(condition.Kind, condition.Param), condition.Target);
            progress.Text = $"{current} / {condition.Target}";
            bar.SetProgress(condition.Target > 0 ? current / (double)condition.Target : 1.0);
        }
    }

    private void Rebuild()
    {
        _panel.Widgets.Clear();
        _slots.Clear();

        if (_simulation.CurrentTutorialStep is { } step)
        {
            AddHeadline(_screens.Loc[step.NameKey], _screens.Loc[step.HintKey], step.Condition, step.Focus, guide: true);
        }

        AddQuests(headlineTaken: _simulation.CurrentTutorialStep is not null);
    }

    /// <summary>
    /// Úkoly pod hlavním cílem. Když průvodce doběhl, povýší se první nesplněný
    /// úkol na hlavní cíl — HUD tak nikdy nezůstane bez „další věci".
    /// </summary>
    private void AddQuests(bool headlineTaken)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        var pending = new List<QuestDef>();
        for (int i = 0; i < content.Quests.Count && pending.Count <= MaxSideGoals; i++)
        {
            if (!_simulation.IsQuestCompleted(i))
            {
                pending.Add(content.Quests[i]);
            }
        }

        int firstSide = 0;
        if (!headlineTaken && pending.Count > 0)
        {
            var main = pending[0];
            AddHeadline(loc[main.NameKey], loc[main.DescriptionKey], main.Condition, FocusHint.None, guide: false);
            firstSide = 1;
        }

        var side = new List<(string Name, GoalCondition Condition, string Tooltip)>();
        for (int i = firstSide; i < pending.Count && side.Count < MaxSideGoals; i++)
        {
            side.Add((loc[pending[i].NameKey], pending[i].Condition,
                loc[pending[i].DescriptionKey] + '\n' + loc.Format("panel.reward",
                    CostFormat.Line(content, loc, pending[i].Reward))));
        }

        // Dynamický úkol se přidá, jen když by panel jinak zůstal poloprázdný —
        // je nekonečný, takže sám o sobě nic „dalšího" neslibuje.
        if (side.Count < MaxSideGoals)
        {
            long target = _simulation.DynamicQuestTarget;
            var dyn = content.QuestsDynamic;
            side.Add((loc.Format("quest.dynamic", target),
                new GoalCondition(dyn.BaseCondition.Kind, dyn.BaseCondition.Param, target),
                loc.Format("quest.dynamic.desc", target)));
        }

        _panel.Widgets.Add(new Label
        {
            Text = loc["hud.otherGoals"],
            TextColor = UiFactory.Accent,
            Tooltip = loc["tip.quests"],
        });

        foreach (var (name, condition, tooltip) in side)
        {
            AddSideGoal(name, condition, tooltip);
        }
    }

    /// <summary>
    /// Hlavní cíl: nadpis, věta „jak na to", pruh pokroku a tlačítko, které
    /// otevře, co je potřeba. Text návodu je to podstatné — bez něj hráč ví,
    /// <em>co</em> se po něm chce, ale ne <em>kudy</em>.
    /// </summary>
    private void AddHeadline(string name, string hint, GoalCondition condition, FocusHint focus, bool guide)
    {
        var loc = _screens.Loc;
        var stack = new VerticalStackPanel { Spacing = 4 };

        stack.Widgets.Add(new Label
        {
            Text = loc["hud.objective"],
            TextColor = UiFactory.Accent,
            Tooltip = loc["tip.objective"],
        });
        stack.Widgets.Add(new Label { Text = name, TextColor = HeadlineColor });
        stack.Widgets.Add(new Label { Text = Wrap(hint), TextColor = HintColor });

        var bar = new ProgressBar(BarWidth, height: 8);
        stack.Widgets.Add(bar.Root);
        var progress = new Label { TextColor = ProgressColor };
        stack.Widgets.Add(progress);
        _slots.Add((condition, progress, bar));

        if (focus.Kind != FocusKind.None)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.showMe"], () => _onFocus(focus), loc["tip.showMe"]));
        }

        if (guide)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.skipGuide"], _onSkipGuide, loc["tip.skipGuide"]));
        }

        var card = new Panel
        {
            Background = new SolidBrush(UiPalette.PanelDeep),
            Border = new SolidBrush(UiFactory.Accent * 0.55f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
        };
        card.Widgets.Add(stack);
        _panel.Widgets.Add(card);
    }

    private void AddSideGoal(string name, GoalCondition condition, string tooltip)
    {
        var slot = new VerticalStackPanel { Spacing = 2, Tooltip = tooltip };
        slot.Widgets.Add(new Label { Text = name });
        var bar = new ProgressBar(BarWidth);
        slot.Widgets.Add(bar.Root);
        var progress = new Label { TextColor = ProgressColor };
        slot.Widgets.Add(progress);
        _panel.Widgets.Add(slot);
        _slots.Add((condition, progress, bar));
    }

    /// <summary>
    /// Zalomí návod na krátké řádky. Myra label neumí sám zalomit na šířku panelu
    /// a jednořádkový návod by HUD roztáhl přes půl obrazovky.
    /// </summary>
    private static string Wrap(string text, int width = 34)
    {
        var result = new System.Text.StringBuilder(text.Length + 16);
        int lineLength = 0;
        foreach (string word in text.Split(' '))
        {
            if (lineLength > 0 && lineLength + 1 + word.Length > width)
            {
                result.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                result.Append(' ');
                lineLength++;
            }

            result.Append(word);
            lineLength += word.Length;
        }

        return result.ToString();
    }
}
