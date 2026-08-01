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
/// Úkoly jako overlay: aktivní (s pokrokem a odměnou) a splněné. Pevné úkoly
/// z <c>quests.json</c> + jeden dynamický, který se pořád posouvá výš. Vede hráče
/// hrou a dává mu pořád co dělat. Simulace mezitím stojí (jen vrchní obrazovka tiká).
/// </summary>
public sealed class QuestsScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public QuestsScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
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

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    /// <summary>
    /// Prosba obyvatele úplně nahoře — nad zakázkami i výzvami.
    ///
    /// <para>Je to jediná položka, za kterou stojí konkrétní člověk, a zároveň
    /// jediná, která se dá minout nadobro. Zakázka se vrátí, výzva bude i zítra;
    /// Marek Kovář, kterému hráč nepomohl, ne.</para>
    /// </summary>
    private void AddCitizenRequest(VerticalStackPanel list)
    {
        var loc = _screens.Loc;
        if (!_simulation.CitizensEnabled)
        {
            return;
        }

        list.Widgets.Add(Header(loc["citizen.request.title"]));

        if (_simulation.PendingCitizenDef is not { } def)
        {
            list.Widgets.Add(new Label
            {
                Text = loc["citizen.waiting"],
                TextColor = new Color(150, 160, 175),
            });
            return;
        }

        var content = _screens.Content;
        var stack = new VerticalStackPanel { Spacing = 3 };

        // Jméno a prosba jako jedna věta: „Marek Kovář by rád mlel mouku."
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("citizen.line", _simulation.PendingCitizenName, loc[def.TextKey]),
            TextColor = new Color(255, 224, 168),
        });
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("citizen.needs", CostFormat.Line(content, loc, def.Cost)),
            TextColor = new Color(214, 222, 232),
        });
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("citizen.timeLeft",
                DurationFormat.FromTicks(_simulation.PendingCitizenRequest.TicksLeft)),
            TextColor = new Color(186, 198, 214),
        });

        if (_simulation.CanHelpCitizen())
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["citizen.help"], () =>
            {
                _simulation.TryHelpCitizen();
                BuildUi();
            }));
        }

        var panel = new Panel
        {
            Background = new SolidBrush(new Color(42, 38, 30, 210)),
            Border = new SolidBrush(new Color(255, 224, 168) * 0.5f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Tooltip = loc["tip.citizens"],
        };
        panel.Widgets.Add(stack);
        list.Widgets.Add(panel);
    }

    /// <summary>
    /// Nástěnka zakázek úplně nahoře. Je to nejkratší smyčka ve hře — objednávka
    /// běží minuty, výzva den, úkol klidně hodinu — takže patří první.
    /// </summary>
    private void AddContracts(VerticalStackPanel list)
    {
        var loc = _screens.Loc;
        if (!_simulation.ContractsEnabled)
        {
            return;
        }

        list.Widgets.Add(Header(loc["contract.board"]));

        bool anyOffer = false;
        for (int slot = 0; slot < _simulation.ContractSlots.Length; slot++)
        {
            if (_simulation.ContractAt(slot) is { } def)
            {
                anyOffer = true;
                list.Widgets.Add(ContractRow(slot, def));
            }
        }

        // Prázdná nástěnka musí říct proč, jinak vypadá jako rozbitá funkce.
        if (!anyOffer)
        {
            list.Widgets.Add(new Label
            {
                Text = loc["contract.waiting"],
                TextColor = new Color(150, 160, 175),
            });
        }
    }

    /// <summary>Řádek zakázky: co chce, co za to dá, kolik zbývá času a tlačítko odevzdat.</summary>
    private Widget ContractRow(int slot, ContractDef def)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var state = _simulation.ContractSlots[slot];
        bool canDeliver = _simulation.CanFulfilContract(slot);

        var stack = new VerticalStackPanel { Spacing = 3 };
        stack.Widgets.Add(new Label { Text = loc[def.NameKey], TextColor = new Color(255, 214, 120) });
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("contract.demand",
                loc[content.Resources[def.DemandResourceIndex].NameKey], state.DemandAmount),
            TextColor = new Color(214, 222, 232),
        });
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("contract.rewardLabel",
                CostFormat.Line(content, loc, _simulation.ContractReward(slot))),
            TextColor = new Color(150, 220, 150),
        });

        // Termín barvou: poslední čtvrtina červená, ať se to dá číst koutkem oka.
        double left = def.DurationTicks > 0 ? state.TicksLeft / (double)def.DurationTicks : 0;
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("contract.timeLeft", DurationFormat.FromTicks(state.TicksLeft)),
            TextColor = left < 0.25 ? new Color(235, 130, 110) : new Color(186, 198, 214),
        });

        if (canDeliver)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["contract.deliver"], () =>
            {
                _simulation.TryFulfilContract(slot);
                BuildUi(); // nabídka zmizela, nástěnka se musí překreslit
            }));
        }
        else
        {
            long missing = state.DemandAmount - (long)_simulation.GetResource(def.DemandResourceIndex);
            stack.Widgets.Add(new Label
            {
                Text = loc.Format("contract.notEnough", Math.Max(1, missing)),
                TextColor = new Color(235, 170, 110),
            });
        }

        var panel = new Panel
        {
            Background = new SolidBrush(new Color(32, 42, 58, 200)),
            Border = new SolidBrush((canDeliver ? new Color(150, 220, 150) : UiFactory.Accent) * 0.55f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Tooltip = loc["tip.contracts"],
        };
        panel.Widgets.Add(stack);
        return panel;
    }

    /// <summary>
    /// Dnešní výzvy nad úkoly. Jsou nahoře schválně: úkoly čekají, výzvy platí
    /// jen dnes, takže je hráč má vidět první.
    /// </summary>
    private void AddDailyChallenges(VerticalStackPanel list)
    {
        var loc = _screens.Loc;
        var catalog = _screens.Content.Challenges;
        var active = _simulation.ActiveChallenges;
        if (!catalog.IsEnabled || active.Count == 0)
        {
            return;
        }

        list.Widgets.Add(Header(loc["hud.challenges"]));
        for (int slot = 0; slot < active.Count; slot++)
        {
            var challenge = catalog.Challenges[active[slot]];
            if (_simulation.IsChallengeDone(slot))
            {
                list.Widgets.Add(CompletedRow(loc[challenge.NameKey]));
                continue;
            }

            // Pokrok se u výzev počítá od vydání sady, takže hotová podmínka
            // nestačí — musí se ukázat dnešní číslo, ne celoherní metrika.
            list.Widgets.Add(ChallengeRow(challenge, _simulation.ChallengeProgress(slot)));
        }

        list.Widgets.Add(new Label
        {
            Text = loc["challenge.resetsAt"],
            TextColor = new Color(150, 160, 175),
        });
    }

    /// <summary>Řádek výzvy: jméno, popis, dnešní pokrok a odměna.</summary>
    private Widget ChallengeRow(ChallengeDef challenge, long progress)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        long target = challenge.Condition.Target;

        var stack = new VerticalStackPanel { Spacing = 3 };
        stack.Widgets.Add(new Label { Text = loc[challenge.NameKey], TextColor = new Color(255, 214, 120) });
        stack.Widgets.Add(new Label { Text = loc[challenge.DescriptionKey], TextColor = new Color(186, 198, 214) });

        var bar = new ProgressBar(320);
        bar.SetProgress(target > 0 ? Math.Min(progress, target) / (double)target : 1.0);
        stack.Widgets.Add(bar.Root);
        stack.Widgets.Add(new Label
        {
            Text = $"{Math.Min(progress, target)} / {target}   " + loc.Format("panel.reward",
                CostFormat.Line(content, loc, challenge.Reward)),
            TextColor = new Color(150, 220, 150),
        });

        var panel = new Panel
        {
            Background = new SolidBrush(new Color(32, 42, 58, 200)),
            Border = new SolidBrush(UiFactory.Accent * 0.55f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Tooltip = loc["tip.challenges"],
        };
        panel.Widgets.Add(stack);
        return panel;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        var list = new VerticalStackPanel { Spacing = 8 };

        AddCitizenRequest(list);
        AddContracts(list);
        AddDailyChallenges(list);

        list.Widgets.Add(Header(loc["panel.quests.active"]));
        for (int i = 0; i < content.Quests.Count; i++)
        {
            if (!_simulation.IsQuestCompleted(i))
            {
                var quest = content.Quests[i];
                list.Widgets.Add(ActiveRow(loc[quest.NameKey], loc[quest.DescriptionKey], quest.Condition, quest.Reward));
            }
        }

        // Dynamický úkol (vždy aktivní, roste s hrou).
        long dynTarget = _simulation.DynamicQuestTarget;
        var dyn = content.QuestsDynamic;
        list.Widgets.Add(ActiveRow(
            loc.Format("quest.dynamic", dynTarget), loc.Format("quest.dynamic.desc", dynTarget),
            new GoalCondition(dyn.BaseCondition.Kind, dyn.BaseCondition.Param, dynTarget), dyn.BaseReward));

        // Splněné.
        bool anyCompleted = false;
        for (int i = 0; i < content.Quests.Count; i++)
        {
            if (_simulation.IsQuestCompleted(i))
            {
                if (!anyCompleted)
                {
                    list.Widgets.Add(Header(loc["panel.quests.completed"]));
                    anyCompleted = true;
                }

                list.Widgets.Add(CompletedRow(loc[content.Quests[i].NameKey]));
            }
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["hud.quests"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 380, Width = 460 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private static Label Header(string text) => new()
    {
        Text = text,
        TextColor = UiFactory.Accent,
    };

    private Widget ActiveRow(string name, string desc, GoalCondition condition, IReadOnlyList<Core.Content.ResourceAmount> reward)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        long current = Math.Min(_simulation.EvaluateMetric(condition.Kind, condition.Param), condition.Target);

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(28, 36, 50, 235)),
        };
        row.Widgets.Add(new Label { Text = name, TextColor = UiFactory.Accent });
        row.Widgets.Add(new Label { Text = desc, TextColor = Color.LightGray, Wrap = true });

        var bar = new ProgressBar(412, 8);
        bar.SetProgress(condition.Target > 0 ? current / (double)condition.Target : 1.0);
        row.Widgets.Add(bar.Root);
        row.Widgets.Add(new Label { Text = $"{current} / {condition.Target}", TextColor = new Color(150, 220, 150) });
        if (reward.Count > 0)
        {
            row.Widgets.Add(new Label
            {
                Text = loc.Format("panel.reward", CostFormat.Line(content, loc, reward)),
                TextColor = Color.Gray,
            });
        }

        return row;
    }

    private Widget CompletedRow(string name)
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel
        {
            Spacing = 8,
            Width = 436,
            Padding = new Thickness(12, 6),
            Background = new SolidBrush(new Color(24, 34, 28, 235)),
        };
        row.Widgets.Add(new Label { Text = name, TextColor = new Color(150, 170, 150) });
        row.Widgets.Add(new Label { Text = loc["quest.done"], TextColor = Color.LightGreen });
        return row;
    }
}
