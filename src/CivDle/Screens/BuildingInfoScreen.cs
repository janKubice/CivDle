using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Detail budovy jako overlay: klik na budovu ji rozklikne, ukáže kategorii,
/// polohu a — je-li dostupná další úroveň — tlačítko na vylepšení (např. dům →
/// chalupa). Vylepšení proběhne na místě (stejný půdorys), panel se přestaví,
/// aby ukázal novou úroveň. Simulace mezitím stojí (jen vrchní obrazovka tiká).
/// </summary>
public sealed class BuildingInfoScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly int _buildingIndex;
    private readonly Action<int> _onStartMove;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <param name="onStartMove">Zavolá se s indexem budovy, když hráč zvolí „Přesunout" (režim řídí herní obrazovka).</param>
    public BuildingInfoScreen(ScreenManager screens, Simulation simulation, int buildingIndex, Action<int> onStartMove)
    {
        _screens = screens;
        _simulation = simulation;
        _buildingIndex = buildingIndex;
        _onStartMove = onStartMove;
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
        var content = _screens.Content;
        ref readonly var instance = ref _simulation.Buildings[_buildingIndex];
        var def = content.Buildings[instance.DefIndex];

        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var header = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var icon = _screens.Sprites.Get($"building.{def.Id}");
        if (icon is not null)
        {
            header.Widgets.Add(UiFactory.Icon(icon, 34));
        }

        header.Widgets.Add(new Label
        {
            Text = loc[def.NameKey],
            VerticalAlignment = VerticalAlignment.Center,
        });
        layout.Widgets.Add(header);

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("panel.building.info", loc[$"category.{def.Category}"], instance.X, instance.Y),
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Proč budova nevyrábí, hned pod jménem. Tohle je první věc, na kterou se
        // hráč ptá, když ji rozklikne — a dřív mu na ni nikdo neodpověděl.
        if (GameplayScreen.StallText(instance.Stall) is { } stallKey)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc[stallKey],
                TextColor = Rendering.BuildingRenderer.StallColor(instance.Stall),
                HorizontalAlignment = HorizontalAlignment.Center,
                Wrap = true,
                Width = 380,
            });
        }

        // Napojení na síť: bez cesty vyrábí budova pomaleji, a hráč musí mít
        // šanci to zjistit jinak než z tabulky v hlavě.
        if (def.Recipe is not null && content.Gameplay.Roads.DisconnectedProductionMult < 1.0)
        {
            bool connected = _simulation.IsBuildingConnected(_buildingIndex);
            layout.Widgets.Add(new Label
            {
                Text = connected
                    ? loc["building.roadConnected"]
                    : loc.Format("building.roadMissing",
                        (int)Math.Round(content.Gameplay.Roads.DisconnectedProductionMult * 100)),
                TextColor = connected ? UiPalette.Good : UiPalette.Warn,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Rozestavěný div: postup a zbývající čas. Dokud se staví, nemá smysl
        // nabízet vylepšení ani mluvit o výrobě — ještě nic nedělá.
        if (!instance.IsComplete)
        {
            double progress = _simulation.ConstructionProgress01(_buildingIndex);
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("building.underConstruction", (int)Math.Round(progress * 100)),
                TextColor = UiPalette.TextBright,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("building.buildTimeLeft", DurationFormat.FromTicks(instance.BuildTicksRemaining)),
                TextColor = Color.LightGray,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Bonus za okolí konkrétní budovy — hráč po letech nepamatuje, proč tahle
        // pila táhne a ta o kus dál ne. Ukazuje se, i když je nulový: „stojí špatně"
        // je stejně užitečná informace jako „stojí dobře".
        if (def.Adjacency is not null)
        {
            double bonus = instance.AdjacencyMult - 1f;
            layout.Widgets.Add(new Label
            {
                Text = bonus > 0
                    ? loc.Format("building.adjacencyGood", BuildingSummary.Percent(bonus))
                    : loc["building.adjacencyNone"],
                TextColor = bonus > 0 ? UiPalette.Good : UiPalette.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Svoz: proč tenhle důl v horách táhne na půl plynu. Ukazuje se jen tam,
        // kde něco ubírá — u budov ve městě by to byl jen řádek navíc.
        if (def.Recipe is not null && instance.HaulMult < 0.995f)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("building.haulPenalty", BuildingSummary.Percent(instance.HaulMult)),
                TextColor = UiPalette.Warn,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Milník za počet budov: kolik jich stojí, co to dává a kolik chybí do
        // dalšího stupně. Bez toho posledního řádku je milník neviditelný — a tím
        // pádem k ničemu, protože právě ten je ta mrkev.
        if (def.Milestones is { } milestones)
        {
            int tier = _simulation.MilestoneTier(instance.DefIndex);
            long toNext = _simulation.MilestoneToNextTier(instance.DefIndex);
            long built = _simulation.MilestoneCount(instance.DefIndex);

            if (tier > 0)
            {
                layout.Widgets.Add(new Label
                {
                    Text = loc.Format("building.milestoneTier", tier,
                        BuildingSummary.Percent(_simulation.MilestoneMultiplier(instance.DefIndex) - 1.0)),
                    TextColor = UiPalette.Good,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            layout.Widgets.Add(new Label
            {
                Text = toNext > 0
                    ? loc.Format("building.milestoneNext", toNext, built)
                    : loc.Format("building.milestoneMax", built),
                TextColor = toNext > 0 ? UiPalette.TextBright : UiPalette.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tooltip = loc.Format("tip.milestones", milestones.Every),
            });
        }

        // Kdo budovu založil. Tenhle jeden řádek je celý smysl pojmenovaných
        // obyvatel — bez něj je to jen další budova.
        if (_simulation.FounderOf(instance.X, instance.Y) is { Length: > 0 } founder)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("citizen.founder", founder),
                TextColor = UiPalette.TextBright,
                HorizontalAlignment = HorizontalAlignment.Center,
                Tooltip = loc["tip.citizens"],
            });
        }

        // Čtvrť: kam budova patří a co jí to přináší. Bez tohohle by se synergie
        // projevila jen v číslech a hráč by netušil, že za ni může sousedství.
        if (_simulation.DistrictOf(_buildingIndex) is { } district)
        {
            var type = content.Districts.Types[district.TypeIndex];
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("building.inDistrict", loc[type.NameKey], district.BuildingCount),
                TextColor = type.MapColor.ToXna(),
                HorizontalAlignment = HorizontalAlignment.Center,
            });

            if (instance.DistrictMult > 1.001f)
            {
                layout.Widgets.Add(new Label
                {
                    Text = loc.Format("building.districtBonus", BuildingSummary.Percent(instance.DistrictMult - 1f)),
                    TextColor = UiPalette.Good,
                    HorizontalAlignment = HorizontalAlignment.Center,
                });
            }

            // Stinná stránka se musí říct nahlas, jinak vypadá synergie jako
            // čistý zisk a hráč nechápe, proč mu zrovna tady houstne smog.
            if (type.PollutionMult > 1.001 && def.AffectsPollution && !def.Pollution.IsCleaner)
            {
                layout.Widgets.Add(new Label
                {
                    Text = loc["building.districtSmog"],
                    TextColor = UiPalette.TextBright,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Tooltip = loc["tip.district"],
                });
            }
        }

        // Zamoření pod budovou: proč zrovna tahle farma sotva rodí. Bez tohohle
        // řádku by hráč viděl klesající výnos a nevěděl, že za to může huť vedle.
        if (def.Recipe is not null && instance.PollutionMult < 0.995f)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("building.pollutionPenalty", BuildingSummary.Percent(instance.PollutionMult)),
                TextColor = UiPalette.Bad,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        // Co budova sama dělá s okolím — ať se dá poznat viník, ne jen následek.
        if (def.AffectsPollution)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc[def.Pollution.IsCleaner ? "building.cleansArea" : "building.pollutesArea"],
                TextColor = def.Pollution.IsCleaner ? UiPalette.Warn : UiPalette.TextBright,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        if (instance.IsComplete)
        {
            layout.Widgets.Add(UpgradeSection(instance.DefIndex));
            layout.Widgets.Add(MergeSection(instance.DefIndex, instance.X, instance.Y));
        }

        // Agency: přesunout jinam nebo zbourat (vrátí půl ceny). Obojí se ODEMYKÁ —
        // na začátku hráč jen staví, zásahy do hotového města přijdou později.
        var actions = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (_simulation.IsFeatureUnlocked("move"))
        {
            actions.Widgets.Add(UiFactory.SmallButton(loc["building.move"], () =>
            {
                _onStartMove(_buildingIndex);
                _screens.Pop();
            }));
        }

        if (_simulation.IsFeatureUnlocked("demolish"))
        {
            actions.Widgets.Add(UiFactory.SmallButton(loc["building.demolish"], () =>
            {
                _simulation.TryDemolish(_buildingIndex);
                _screens.Pop();
            }));
        }

        if (actions.Widgets.Count > 0)
        {
            layout.Widgets.Add(actions);
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Sloučení bloku 2×2 přímo z panelu budovy.
    ///
    /// <para>Dřív se slučovalo jen zvláštním nástrojem ve spodní liště, takže
    /// hráč, který si budovu rozklikl, o té možnosti vůbec nevěděl. Vylepšení
    /// i sloučení jsou přitom totéž rozhodnutí („co s touhle budovou dál") a
    /// patří na jedno místo.</para>
    ///
    /// <para>Když to nejde, řekne se PROČ — zašedlé tlačítko bez vysvětlení je
    /// horší než žádné.</para>
    /// </summary>
    private Widget MergeSection(int defIndex, int tileX, int tileY)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var def = content.Buildings[defIndex];
        var stack = new VerticalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

        if (!def.CanMergeIntoBigger || !_simulation.IsFeatureUnlocked("merge"))
        {
            return stack; // tenhle typ se neslučuje (nebo to hráč ještě neumí)
        }

        var target = content.Buildings[def.MergesToIndex];
        var result = _simulation.CanMerge(tileX, tileY);

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("panel.cost", CostFormat.Line(content, loc, def.MergeCost)),
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (result == PlacementResult.Ok)
        {
            stack.Widgets.Add(UiFactory.IconButton(
                _screens.Sprites.Get("icon.merge"),
                loc.Format("building.merge", loc[target.NameKey]),
                () =>
                {
                    _simulation.TryMerge(tileX, tileY);
                    _screens.Pop();
                }));
            return stack;
        }

        stack.Widgets.Add(new Label
        {
            Text = loc[MergeProblemKey(result)],
            TextColor = UiPalette.Warn,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return stack;
    }

    /// <summary>Proč vylepšení nejde — každý důvod má vlastní větu.</summary>
    private static string UpgradeProblemKey(PlacementResult result) => result switch
    {
        PlacementResult.NotEnoughResources => "building.upgradeNoResources",
        PlacementResult.NotUnlocked => "building.upgradeLocked",
        _ => "building.upgradeUnavailable",
    };

    /// <summary>Proč sloučení nejde — každý důvod má vlastní větu, ne jedno „nelze".</summary>
    private static string MergeProblemKey(PlacementResult result) => result switch
    {
        PlacementResult.NotEnoughResources => "building.mergeNoResources",
        PlacementResult.NotUnlocked => "building.mergeLocked",
        _ => "building.mergeNoBlock",
    };

    private Widget UpgradeSection(int defIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var def = content.Buildings[defIndex];

        var section = new VerticalStackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        if (!def.HasUpgrade)
        {
            section.Widgets.Add(new Label
            {
                Text = loc["building.maxLevel"],
                TextColor = UiPalette.Text,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return section;
        }

        var next = content.Buildings[def.UpgradesToIndex];
        section.Widgets.Add(new Label
        {
            Text = loc.Format("panel.cost", CostFormat.Line(content, loc, def.UpgradeCost)),
            TextColor = Color.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Když to nejde, řekne se PROČ. Zašedlé tlačítko bez vysvětlení nechá
        // hráče hádat, jestli mu chybí suroviny, nebo výzkum.
        var upgradeResult = _simulation.CanUpgrade(_buildingIndex);
        if (upgradeResult != PlacementResult.Ok)
        {
            section.Widgets.Add(new Label
            {
                Text = loc[UpgradeProblemKey(upgradeResult)],
                TextColor = UiPalette.Warn,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return section;
        }

        var button = UiFactory.IconButton(
            _screens.Sprites.Get("icon.upgrade"),
            loc.Format("building.upgrade", loc[next.NameKey]),
            () =>
            {
                if (_simulation.TryUpgradeBuilding(_buildingIndex) == PlacementResult.Ok)
                {
                    BuildUi(); // budova je teď o úroveň výš — ukázat nový stav
                }
            });
        section.Widgets.Add(button);
        return section;
    }
}
