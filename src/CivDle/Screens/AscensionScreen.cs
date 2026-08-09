using CivDle.Core;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Vzestup (prestige) jako overlay — háček dema. Ukazuje body Vzestupu, podmínku
/// a strom trvalých upgradů (za body). Vzestup zresetuje éru, ale upgrady a body
/// zůstávají; teaser slibuje plnou verzi. Simulace mezitím stojí.
/// </summary>
public sealed class AscensionScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly WorldInfo _info;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Čeká tlačítko Vzestupu na potvrzení? (Nevratný krok na dvě kliknutí.)</summary>
    private bool _confirming;

    /// <summary>
    /// Kam byl seznam odscrollovaný. Po koupi se obrazovka staví znovu a bez
    /// tohohle skočila na začátek — hráč pak po každém nákupu hledal, kde byl.
    /// </summary>
    private Point _scroll;

    /// <summary>Scroller seznamu; drží se kvůli obnovení pozice po přestavbě.</summary>
    private ScrollViewer? _list;

    /// <summary>Kolik úrovní se kupuje jedním kliknutím.</summary>
    private int _batch = 1;

    /// <summary>Nabídka násobičů nákupu. „Max" bere, na co body stačí.</summary>
    private static readonly int[] Batches = { 1, 5, 25, int.MaxValue };

    /// <summary>Šířka panelu. Vzestup je hlavní progrese hry, ne poznámka pod čarou.</summary>
    private const int PanelWidth = 760;

    /// <summary>Šířka řádku upgradu; scroller si nechá místo na posuvník.</summary>
    private const int RowWidth = PanelWidth - 56;

    public AscensionScreen(ScreenManager screens, Simulation simulation, WorldInfo info)
    {
        _screens = screens;
        _simulation = simulation;
        _info = info;
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.62f);
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

        // Pozici si vezmi ze starého scrolleru dřív, než ho přestavíš.
        if (_list is not null)
        {
            _scroll = _list.ScrollPosition;
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["prestige.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(180, 140, 230),
        });
        // Nevyužité body svítí. Je to jediná věc, kterou má hráč po Vzestupu
        // udělat, a v šedivém řádku mezi ostatními zanikala.
        bool hasPoints = _simulation.PrestigePoints > 0;
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.points", _simulation.PrestigePoints),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = hasPoints ? new Color(255, 215, 120) : Color.LightGray,
        });

        if (hasPoints)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["prestige.spendNow"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 220, 150),
                Wrap = true,
                Width = PanelWidth - 40,
            });
        }

        layout.Widgets.Add(AscendAction());

        layout.Widgets.Add(BatchPicker());

        // Upgrady (strom) ve scrollu.
        var list = new VerticalStackPanel { Spacing = 8 };
        var upgrades = _screens.Content.PrestigeUpgrades;
        for (int i = 0; i < upgrades.Count; i++)
        {
            list.Widgets.Add(UpgradeRow(i));
        }

        _list = new ScrollViewer { Content = list, Height = 440, Width = PanelWidth - 20 };
        layout.Widgets.Add(_list);

        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);

        // Vrátit scroll až nad hotovým rozvržením — dřív scroller ještě nezná
        // svou výšku a pozici by uřízl na nulu.
        if (_list is not null)
        {
            _list.ScrollPosition = _scroll;
        }
    }

    /// <summary>
    /// Přepínač „kolik úrovní naráz". Kupovat po jedné je u opakovatelných
    /// upgradů, kde hráč utrácí stovky bodů, jen klikání.
    /// </summary>
    private Widget BatchPicker()
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        row.Widgets.Add(new Label
        {
            Text = loc["prestige.batch"],
            TextColor = new Color(180, 185, 200),
            VerticalAlignment = VerticalAlignment.Center,
        });

        foreach (int size in Batches)
        {
            int captured = size;
            var button = UiFactory.SmallButton(
                size == int.MaxValue ? loc["prestige.batchMax"] : "×" + size,
                () =>
                {
                    _batch = captured;
                    BuildUi();
                });

            if (size == _batch)
            {
                button.Background = new SolidBrush(new Color(110, 86, 160, 240));
            }

            row.Widgets.Add(button);
        }

        return row;
    }

    private Widget AscendAction()
    {
        var loc = _screens.Loc;
        if (_simulation.CanAscend())
        {
            var ready = new VerticalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
            ready.Widgets.Add(AscendPreviewPanel(_simulation.PreviewAscension()));

            // Poslední pohled na to, co Vzestup smaže. Nabízí se tady, protože
            // tohle je jediné místo, kde hráč o zmizení světa opravdu ví.
            if (_simulation.History.Count > 1)
            {
                ready.Widgets.Add(UiFactory.SmallButton(
                    loc["timelapse.beforeAscend"],
                    () => _screens.Push(new TimelapseScreen(
                        _screens, _simulation.History, _simulation.Terrain, _simulation.Seed, SaveTimelapse))));
            }

            var button = new Button
            {
                Content = new Label
                {
                    Text = _confirming
                        ? loc["prestige.confirm"]
                        : loc.Format("prestige.ascend", _simulation.PendingAscensionPoints()),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Padding = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidBrush(_confirming
                    ? new Color(170, 70, 90, 245)
                    : new Color(120, 80, 170, 240)),
            };

            // Dvě kliknutí, ne jedno: Vzestup je jediná nevratná akce ve hře
            // a omylem smazané město se nedá vrátit.
            button.Click += (_, _) =>
            {
                if (!_confirming)
                {
                    _confirming = true;
                    BuildUi();
                    return;
                }

                _confirming = false;

                // Časosběr se uloží automaticky — Vzestup ho za okamžik smaže
                // a „měl sis ho uložit" je přesně ta věta, kterou hráč nemá
                // nikdy slyšet.
                SaveTimelapse();
                if (_simulation.TryAscend() == PlacementResult.Ok)
                {
                    // Panel se přestaví HNED: hráč po Vzestupu právě dostal body
                    // a chce je utratit. Dřív pod bilancí zůstala stará obrazovka
                    // s nulou a vypadalo to, že se nic nepřipsalo.
                    BuildUi();

                    // Bilance běhu jako tečka za kapitolou — bez ní je Vzestup
                    // jen tlačítko „smazat město".
                    _screens.Push(new RunSummaryScreen(_screens, _simulation.LastRun));
                }
            };

            ready.Widgets.Add(button);
            return ready;
        }

        _confirming = false;

        // Práh roste s každým Vzestupem — ukazuj ten AKTUÁLNÍ, ne základní z dat.
        long current = _simulation.AscensionProgress();
        long target = _simulation.AscensionRequirement();

        var pending = new VerticalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        pending.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.requirement", current, target),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(210, 170, 120),
        });

        var bar = new ProgressBar(412, 8);
        bar.SetProgress(target > 0 ? current / (double)target : 1.0);
        pending.Widgets.Add(bar.Root);
        return pending;
    }

    /// <summary>
    /// Rozvaha před nevratným krokem: co zůstane a co zmizí.
    ///
    /// <para>Do téhle chvíle tlačítko říkalo jen „+N bodů" a hráč se o tom, že
    /// přišel o město, silnice i výzkum, dozvěděl až potom. Nejde o balanc, ale
    /// o informovaný souhlas.</para>
    /// </summary>
    /// <summary>Uloží časosběr běhu do sbírky (se snímkem aktuální podoby města).</summary>
    private void SaveTimelapse()
    {
        _simulation.CaptureHistoryNow();
        _screens.Saves.Timelapses.TrySave(
            _simulation.History, _simulation.Seed, _info.SizeId, _info.PresetId);
    }

    private Widget AscendPreviewPanel(AscensionPreview preview)
    {
        var loc = _screens.Loc;
        var stack = new VerticalStackPanel
        {
            Spacing = 4,
            Width = RowWidth,
            Padding = new Thickness(16, 12),
            Background = new SolidBrush(new Color(30, 26, 44, 235)),
            Border = new SolidBrush(new Color(120, 96, 180, 160)),
            BorderThickness = new Thickness(1),
        };

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.preview.gain", preview.PointsGained, preview.PointsAfter),
            TextColor = new Color(190, 160, 235),
            Wrap = true,
        });

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.preview.keeps", preview.UpgradesOwned, preview.LevelAfter),
            TextColor = new Color(150, 220, 150),
            Wrap = true,
        });

        // „Přijdeš o" má smysl psát jen tehdy, když je o co přijít — po prvním
        // Vzestupu z holé mapy by to byl planý strašák.
        if (preview.LosesAnything)
        {
            stack.Widgets.Add(new Label
            {
                Text = loc.Format("prestige.preview.loses",
                    preview.Buildings, preview.Population, preview.RoadTiles, preview.Techs),
                TextColor = new Color(235, 150, 120),
                Wrap = true,
            });
        }

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.preview.next", preview.NextRequirement),
            TextColor = new Color(150, 160, 175),
            Wrap = true,
        });

        return stack;
    }

    private Widget UpgradeRow(int upgradeIndex)
    {
        var loc = _screens.Loc;
        var upgrade = _screens.Content.PrestigeUpgrades[upgradeIndex];

        // Barva podkladu nese stav uzlu. Dřív vypadaly všechny řádky stejně a
        // hráč musel číst tlačítko, aby poznal, co si vlastně může koupit.
        bool maxed = _simulation.IsUpgradeMaxed(upgradeIndex);
        var state = _simulation.CanBuyUpgrade(upgradeIndex);
        var (fill, border) = maxed
            ? (new Color(26, 40, 30, 235), new Color(90, 150, 110, 170))
            : state switch
            {
                PlacementResult.Ok => (new Color(42, 34, 66, 240), new Color(170, 140, 235, 200)),
                PlacementResult.NotUnlocked => (new Color(22, 20, 30, 220), new Color(70, 70, 84, 120)),
                _ => (new Color(30, 26, 44, 235), new Color(90, 84, 120, 130)),
            };

        var row = new VerticalStackPanel
        {
            Spacing = 4,
            Width = RowWidth,
            Padding = new Thickness(16, 12),
            Background = new SolidBrush(fill),
            Border = new SolidBrush(border),
            BorderThickness = new Thickness(1),
        };

        // Nadpis a úroveň na jednom řádku: jméno vlevo, stav vpravo. Svislý
        // sloupec tří štítků dělal ze seznamu dlouhou šedou stěnu.
        var header = new HorizontalStackPanel { Spacing = 10, Width = RowWidth - 34 };
        header.Widgets.Add(new Label
        {
            Text = loc[upgrade.NameKey],
            TextColor = maxed ? new Color(150, 220, 160) : new Color(205, 175, 245),
        });
        if (upgrade.IsRepeatable)
        {
            header.Widgets.Add(new Label
            {
                Text = loc.Format("prestige.level", _simulation.UpgradeLevel(upgradeIndex), upgrade.MaxLevel),
                TextColor = new Color(160, 170, 190),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
        }

        row.Widgets.Add(header);
        row.Widgets.Add(new Label
        {
            Text = loc[upgrade.DescriptionKey],
            TextColor = state == PlacementResult.NotUnlocked ? new Color(140, 140, 152) : Color.LightGray,
            Wrap = true,
            Width = RowWidth - 34,
        });

        row.Widgets.Add(UpgradeAction(upgradeIndex));
        return row;
    }

    private Widget UpgradeAction(int upgradeIndex)
    {
        var loc = _screens.Loc;
        var upgrade = _screens.Content.PrestigeUpgrades[upgradeIndex];

        // Pozor: „vlastněno" smí platit jen pro jednorázové uzly. Opakovatelný
        // upgrade se po první koupi musí dát koupit dál — jinak je z celé
        // opakovatelnosti mrtvá mechanika, kterou UI nepustí ke slovu.
        if (_simulation.IsUpgradeMaxed(upgradeIndex))
        {
            return new Label
            {
                Text = loc[upgrade.IsRepeatable ? "prestige.maxed" : "prestige.owned"],
                TextColor = Color.LightGreen,
            };
        }

        var status = _simulation.CanBuyUpgrade(upgradeIndex);
        if (status == PlacementResult.NotUnlocked)
        {
            return new Label { Text = loc["prestige.locked"], TextColor = new Color(150, 150, 160) };
        }

        // Cena další úrovně, ne základní z dat — u opakovatelných roste. Při
        // dávce ukaž, kolik úrovní na body opravdu vyjde, a jejich součet:
        // „Koupit ×5" a pak koupit tři je horší než nic neslibovat.
        int levels = _simulation.AffordableUpgradeLevels(upgradeIndex, _batch);
        string label = levels > 1
            ? loc.Format("prestige.buyMany", levels, Numbers.Format(_simulation.UpgradeBatchCost(upgradeIndex, levels)))
            : loc.Format("prestige.buy", Numbers.Format(_simulation.UpgradeCost(upgradeIndex)));

        var button = new Button
        {
            Content = new Label
            {
                Text = label,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidBrush(new Color(72, 56, 110, 235)),
            Enabled = status == PlacementResult.Ok,
        };
        button.Click += (_, _) => BuyBatch(upgradeIndex);
        return button;
    }

    /// <summary>
    /// Koupí až <see cref="_batch"/> úrovní jedním kliknutím. Každá úroveň se
    /// kupuje zvlášť a za svou (rostoucí) cenu — dávka šetří klikání, ne body.
    /// </summary>
    private void BuyBatch(int upgradeIndex)
    {
        int bought = 0;
        while (bought < _batch && _simulation.TryBuyUpgrade(upgradeIndex) == PlacementResult.Ok)
        {
            bought++;
        }

        if (bought > 0)
        {
            BuildUi();
        }
    }

    /// <summary>
    /// Smoke test: projede všechny násobiče a za každý zkusí nakoupit.
    ///
    /// <para>Obrazovka se po každé koupi staví celá znovu (kvůli cenám a
    /// obnovení scrollu) — a přesně přestavba je místo, kde UI padá. Ať se to
    /// pozná tady, ne až hráči s plnou kapsou bodů.</para>
    /// </summary>
    internal void BuyEverythingForSmoke()
    {
        foreach (int size in Batches)
        {
            _batch = size;
            BuildUi();
            for (int i = 0; i < _screens.Content.PrestigeUpgrades.Count; i++)
            {
                BuyBatch(i);
            }
        }
    }
}
