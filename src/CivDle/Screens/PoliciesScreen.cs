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
/// Politiky růstu jako overlay (automatizace, stupeň 4): seznam pravidel
/// s přepínačem zap/vyp. Zapnutá politika hned mění chování auto-stavby
/// a plnění zón. Simulace mezitím stojí.
/// </summary>
public sealed class PoliciesScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Kam byl seznam odrolovaný — přestavba po kliknutí ho jinak vrátí nahoru.</summary>
    private readonly ScrollMemory _scroll = new();

    public PoliciesScreen(ScreenManager screens, Simulation simulation)
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

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var policies = _screens.Content.Policies;

        var list = new VerticalStackPanel { Spacing = 8 };
        for (int i = 0; i < policies.Count; i++)
        {
            list.Widgets.Add(Row(i));
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["policy.title"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(GovernorSection());
        layout.Widgets.Add(_scroll.Track(new ScrollViewer { Content = list, Height = 360, Width = 460 }));
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
        _scroll.Restore(_desktop);
    }

    /// <summary>
    /// Guvernérova správa vylepšení: dokud ji hráč neodemkne technologií, vidí jen
    /// zámek (automatizace se odemyká, není výchozí). Po odemčení si nastaví míru —
    /// od „vypnuto" po „vše, svižně".
    /// </summary>
    /// <summary>Zaměření guvernéra jako táhlo: od „jen růst" po „jen kvalita".</summary>
    private static readonly GovernorFocus[] FocusSteps =
    {
        GovernorFocus.Growth,
        GovernorFocus.MostlyGrowth,
        GovernorFocus.Balanced,
        GovernorFocus.MostlyQuality,
        GovernorFocus.Quality,
    };

    /// <summary>
    /// Táhlo velikost ↔ kvalita.
    ///
    /// <para>Bez něj měl hráč na výběr jen „vylepšuj" a „nevylepšuj". Kdo chtěl
    /// kompaktní vypiplané město, neměl jak guvernérovi říct, ať přestane
    /// zabírat krajinu; kdo chtěl expandovat, ho nemohl přimět nechat
    /// vylepšování být.</para>
    /// </summary>
    private Widget FocusRow()
    {
        var loc = _screens.Loc;
        var plan = _simulation.Plan;

        var box = new VerticalStackPanel { Spacing = 5 };
        box.Widgets.Add(new Label
        {
            Text = loc.Format("governor.focus", loc[FocusKey(plan.Focus)]),
            TextColor = UiPalette.Accent,
        });

        var row = new HorizontalStackPanel { Spacing = 6 };
        foreach (var focus in FocusSteps)
        {
            var captured = focus;
            bool active = plan.Focus == focus;
            var button = new Button
            {
                Content = new Label
                {
                    Text = loc[FocusShortKey(focus)],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White : UiPalette.Text,
                },
                Width = 80,
                Height = 32,
                Background = new PanelBrush(active ? UiPalette.PanelGood : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _simulation.Plan.SetFocus(captured);
                BuildUi();
            };
            row.Widgets.Add(button);
        }

        box.Widgets.Add(row);

        // Krajní volby znamenají, že jedna půlka práce úplně stojí. To musí být
        // napsané — jinak vypadá guvernér, který nestaví, jako rozbitý.
        if (!plan.BuildsAtAll || !plan.UpgradesAtAll)
        {
            box.Widgets.Add(new Label
            {
                Text = loc[plan.BuildsAtAll ? "governor.focus.noBuild" : "governor.focus.noUpgrade"],
                TextColor = UiPalette.Warn,
                Wrap = true,
            });
        }

        return box;
    }

    /// <summary>
    /// Přepínače kategorií: co všechno smí guvernér stavět.
    ///
    /// <para>Seznam se bere z obsahu, ne z pevného výčtu v kódu — jinak by
    /// budovy z modů neměly jak se do nabídky dostat.</para>
    /// </summary>
    private Widget CategoryRows()
    {
        var loc = _screens.Loc;
        var plan = _simulation.Plan;

        var box = new VerticalStackPanel { Spacing = 5 };
        box.Widgets.Add(new Label { Text = loc["governor.categories"], TextColor = UiPalette.Accent });
        box.Widgets.Add(new Label
        {
            Text = loc["governor.categories.hint"],
            TextColor = Color.LightGray,
            Wrap = true,
        });

        var row = new HorizontalStackPanel { Spacing = 6 };
        int inRow = 0;
        foreach (string category in AutoBuildCategories())
        {
            string captured = category;
            bool allowed = plan.AllowsCategory(category);
            var button = new Button
            {
                Content = new Label
                {
                    Text = loc[$"category.{category}"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = allowed ? Color.White : UiPalette.TextDim,
                },
                Width = 132,
                Height = 30,
                Background = new PanelBrush(allowed ? UiPalette.PanelGood : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _simulation.Plan.SetCategoryAllowed(captured, !_simulation.Plan.AllowsCategory(captured));
                BuildUi();
            };

            row.Widgets.Add(button);
            if (++inRow % 3 == 0)
            {
                box.Widgets.Add(row);
                row = new HorizontalStackPanel { Spacing = 6 };
            }
        }

        if (inRow % 3 != 0)
        {
            box.Widgets.Add(row);
        }

        return box;
    }

    /// <summary>
    /// Kategorie, které vůbec připadají v úvahu — ty, ve kterých guvernér
    /// umí něco postavit. Vypínač kategorie, kde nic nestaví, by nedělal nic.
    /// </summary>
    private IEnumerable<string> AutoBuildCategories() =>
        Enumerable.Range(0, _screens.Content.Buildings.Count)
            .Where(_simulation.GovernorConsiders)
            .Select(i => _screens.Content.Buildings[i].Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal);

    /// <summary>Kolik bodů plánu se ukáže — víc by z přehledu udělalo výpis.</summary>
    private const int PlanLines = 3;

    /// <summary>
    /// Plán guvernéra: co město teď nejvíc potřebuje a čím to řeší.
    ///
    /// <para>Proč: stav („staví trh") říká, <em>co</em> guvernér dělá, ne
    /// <em>proč</em>. Hráč, který viděl sklad vedle skladu, neměl jak poznat,
    /// jestli je to úmysl, nebo chyba. Plán to říká jednou větou na cíl.</para>
    /// </summary>
    private void AddPlan(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        var agenda = _simulation.GovernorAgenda;
        box.Widgets.Add(new Label { Text = loc["governor.plan"], TextColor = UiPalette.Accent });
        if (agenda.Count == 0)
        {
            box.Widgets.Add(new Label { Text = loc["governor.plan.none"], TextColor = UiPalette.TextDim, Wrap = true });
            return;
        }

        for (int i = 0; i < agenda.Count && i < PlanLines; i++)
        {
            box.Widgets.Add(new Label
            {
                Text = "• " + GovernorPlanText.Line(_screens.Content, loc, agenda[i]),
                TextColor = i == 0 ? UiPalette.TextBright : UiPalette.Text,
                Wrap = true,
            });
        }
    }

    /// <summary>Přepínač „guvernér vybírá výzkum" — výchozí vypnutý, strom je hráčova volba.</summary>
    private void AddResearchToggle(VerticalStackPanel box)
    {
        var loc = _screens.Loc;
        bool on = _simulation.Plan.ChoosesResearch;
        box.Widgets.Add(UiFactory.SmallButton(
            loc.Format("governor.research", loc[on ? "common.on" : "common.off"]),
            () =>
            {
                _simulation.Plan.SetChoosesResearch(!_simulation.Plan.ChoosesResearch);
                BuildUi();
            },
            loc["governor.researchDesc"]));
    }

    private static string FocusKey(GovernorFocus focus) =>
        $"governor.focus.{focus.ToString().ToLowerInvariant()}";

    private static string FocusShortKey(GovernorFocus focus) => FocusKey(focus) + ".short";

    private Widget GovernorSection()
    {
        var loc = _screens.Loc;
        var box = new VerticalStackPanel
        {
            Spacing = 5,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new PanelBrush(UiPalette.Panel),
        };
        box.Widgets.Add(new Label { Text = loc["hud.governor"], TextColor = UiFactory.Accent });

        // Co guvernér právě dělá — auto-stavba běží i před odemčením správy
        // vylepšení, takže stav patří nad zámek.
        var status = _simulation.GovernorStatus;
        string line = GovernorStatusText.Line(_screens.Content, loc, status);
        if (line.Length > 0)
        {
            box.Widgets.Add(new Label
            {
                Text = line,
                Wrap = true,
                TextColor = GovernorStatusText.NeedsPlayer(status) ? UiPalette.Warn : UiPalette.TextBright,
            });
            box.Widgets.Add(new Label { Text = GovernorStatusText.Hint(loc, status), TextColor = UiPalette.TextDim, Wrap = true });
        }

        AddPlan(box);
        AddResearchToggle(box);

        if (!_simulation.IsGovernorUnlocked)
        {
            box.Widgets.Add(new Label { Text = loc["governor.locked"], TextColor = UiPalette.TextDim, Wrap = true });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.desc"], TextColor = Color.LightGray, Wrap = true });

        // Tempo výstavby černé na bílém: zrychlení z upgradů je jinak vidět jen
        // „nějak rychleji" a hráč nepozná, jestli se koupený bonus vůbec projevil.
        box.Widgets.Add(new Label
        {
            Text = loc.Format(
                "governor.pace",
                (_simulation.AutoBuildInterval / (double)Simulation.TicksPerSecond).ToString("0.#"),
                _simulation.AutoBuildBudget),
            TextColor = UiPalette.Accent,
        });

        box.Widgets.Add(new Label
        {
            Text = loc.Format("governor.level", loc[$"governor.level{_simulation.AutoUpgradeLevel}"]),
            TextColor = _simulation.AutoUpgradeLevel > 0 ? UiPalette.Good : Color.LightGray,
        });

        // Stupně jako řada tlačítek — aktuální je zvýrazněný.
        var levels = new HorizontalStackPanel { Spacing = 6 };
        for (int level = 0; level <= Simulation.MaxAutoUpgradeLevel; level++)
        {
            int captured = level;
            bool active = _simulation.AutoUpgradeLevel == level;
            bool unlocked = level <= _simulation.MaxUnlockedAutoUpgradeLevel;
            var button = new Button
            {
                Content = new Label
                {
                    Text = unlocked ? level.ToString() : "×",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White
                        : unlocked ? UiPalette.Text : UiPalette.TextDim,
                },
                Width = 44,
                Height = 32,
                // Zamčený stupeň musí být poznat POZADÍM, ne jen zašedlým textem:
                // obě větve tu dřív vracely tutéž barvu, takže dostupné
                // a nedostupné tlačítko vypadalo úplně stejně.
                Background = new PanelBrush(active ? UiPalette.PanelGood
                    : unlocked ? UiPalette.Panel : UiPalette.PanelDeep),
            };
            button.Click += (_, _) =>
            {
                _simulation.SetAutoUpgradeLevel(captured);
                BuildUi();
            };
            levels.Widgets.Add(button);
        }

        box.Widgets.Add(levels);

        box.Widgets.Add(FocusRow());
        box.Widgets.Add(CategoryRows());

        // U zamčeného stupně musí být vidět, ČÍM se odemkne — jinak vypadá
        // přeškrtnuté tlačítko jako rozbitá hra.
        if (_simulation.MaxUnlockedAutoUpgradeLevel < Simulation.MaxAutoUpgradeLevel)
        {
            box.Widgets.Add(new Label
            {
                Text = loc[_simulation.MaxUnlockedAutoUpgradeLevel < 2 ? "governor.locked2" : "governor.locked3"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
        }

        box.Widgets.Add(AddReserve(loc));

        // Automatické slučování je vlastní přepínač, ne další stupeň: mění půdorys
        // města a je nevratné, takže se nemá zapnout jen posunutím míry vylepšování.
        box.Widgets.Add(new Label { Text = " " });
        if (!_simulation.IsAutoMergeUnlocked)
        {
            box.Widgets.Add(new Label
            {
                Text = loc["governor.mergeLocked"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.mergeDesc"], TextColor = Color.LightGray, Wrap = true });
        var mergeToggle = UiFactory.SmallButton(
            loc.Format("governor.merge", loc[_simulation.AutoMerge ? "common.on" : "common.off"]),
            () =>
            {
                _simulation.SetAutoMerge(!_simulation.AutoMerge);
                BuildUi();
            },
            loc["tip.governorMerge"]);
        box.Widgets.Add(mergeToggle);
        return box;
    }

    /// <summary>
    /// Rezerva surovin: kolik guvernér nesmí utratit. Je to jediné nastavení,
    /// které hráči vrací kontrolu nad tím, co si schoval — bez něj si automatiku
    /// dřív nebo později vypne.
    /// </summary>
    private Widget AddReserve(CivDle.Core.Content.Localization loc)
    {
        var box = new VerticalStackPanel { Spacing = 4 };
        box.Widgets.Add(new Label { Text = " " });

        if (!_simulation.IsGovernorReserveUnlocked)
        {
            box.Widgets.Add(new Label
            {
                Text = loc["governor.reserveLocked"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.reserveDesc"], TextColor = Color.LightGray, Wrap = true });
        box.Widgets.Add(new Label
        {
            Text = loc.Format("governor.reserve", (int)Math.Round(_simulation.GovernorReserve * 100)),
            TextColor = UiPalette.Good,
        });

        var steps = new HorizontalStackPanel { Spacing = 6 };
        foreach (int percent in new[] { 0, 10, 25, 50, 75 })
        {
            int captured = percent;
            bool active = (int)Math.Round(_simulation.GovernorReserve * 100) == percent;
            var button = new Button
            {
                Content = new Label
                {
                    Text = percent + " %",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White : UiPalette.Text,
                },
                Width = 58,
                Height = 32,
                Background = new PanelBrush(active ? UiPalette.PanelGood : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _simulation.SetGovernorReserve(captured / 100.0);
                BuildUi();
            };
            steps.Widgets.Add(button);
        }

        box.Widgets.Add(steps);
        return box;
    }

    private Widget Row(int index)
    {
        var loc = _screens.Loc;
        var policy = _screens.Content.Policies[index];
        bool active = _simulation.IsPolicyActive(index);

        var row = new VerticalStackPanel
        {
            Spacing = 4,
            Width = 436,
            Padding = new Thickness(12, 8),
            // Zapnutá politika má být vidět na první pohled. Obě větve tu dřív
            // vracely tutéž barvu, takže se stav dal poznat jen z textu tlačítka.
            Background = new PanelBrush(active ? UiPalette.PanelGood : UiPalette.Panel),
        };
        row.Widgets.Add(new Label
        {
            Text = loc[policy.NameKey],
            TextColor = active ? UiPalette.Good : UiPalette.Text,
        });
        row.Widgets.Add(new Label { Text = loc[policy.DescriptionKey], TextColor = Color.LightGray, Wrap = true });

        // Odměna za výzvu, kterou hráč ještě nemá: ukázat, ne schovat — ať ví,
        // co ho čeká a kde si to vysloužit. Zapnout ale nejde.
        if (!active && !_simulation.IsPolicyAvailable(index))
        {
            row.Widgets.Add(new Label
            {
                Text = loc.Format("policy.locked", LockedBy(policy.UnlockedBy)),
                TextColor = UiPalette.Warn,
                Wrap = true,
            });
            return row;
        }

        row.Widgets.Add(UiFactory.SmallButton(active ? loc["policy.on"] : loc["policy.off"], () =>
        {
            _simulation.TogglePolicy(index);
            BuildUi(); // překresli, ať se přepínač i barva hned obnoví
        }));
        return row;
    }

    /// <summary>Jméno výzvy (či cíle), která politiku odemkne.</summary>
    private string LockedBy(string? unlockKey)
    {
        var loc = _screens.Loc;
        if (unlockKey is null)
        {
            return string.Empty;
        }

        if (unlockKey.StartsWith(ChallengeRewards.Prefix, StringComparison.Ordinal))
        {
            int scenario = _screens.Content.Scenarios.IndexOf(unlockKey[ChallengeRewards.Prefix.Length..]);
            return scenario >= 0 ? loc[_screens.Content.Scenarios[scenario].NameKey] : loc["scenarios.title"];
        }

        return loc[$"quest.{unlockKey["quest:".Length..]}"];
    }
}
