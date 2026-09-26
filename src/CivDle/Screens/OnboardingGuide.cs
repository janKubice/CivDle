using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>Na co průvodce zrovna ukazuje.</summary>
internal enum GuidePointer
{
    /// <summary>Na nic — hráč ví, co dělat, nebo průvodce skončil.</summary>
    None,

    /// <summary>Na uzel k ručnímu sběru (první strom).</summary>
    Harvest,

    /// <summary>Na doporučené místo pro vybranou budovu.</summary>
    Build,
}

/// <summary>Cíl ukazatele: dlaždice (u stavby levý horní roh půdorysu) a budova.</summary>
internal readonly record struct GuideTarget(GuidePointer Kind, int X, int Y, int DefIndex)
{
    /// <summary>Nikam.</summary>
    public static GuideTarget None { get; } = new(GuidePointer.None, 0, 0, -1);
}

/// <summary>Velký okamžik prvních minut — ukazuje se jednou za celý profil hráče.</summary>
internal enum OnboardingMoment
{
    /// <summary>První výroba bez kliknutí: „město teď pracuje samo". Háček celé hry.</summary>
    WorksAlone,

    /// <summary>První noc — okna se rozsvítí, vesnice žije i ve tmě.</summary>
    FirstNight,

    /// <summary>„Klidně hru zavři — vesnice poroste dál." Slib žánru nahlas.</summary>
    SafeToClose,
}

/// <summary>
/// Průvodce prvními pěti minutami: kde hra začíná, na co ukázat a kdy přijde
/// který velký okamžik.
///
/// <para><b>Proč existuje:</b> lidé z dema odcházeli kolem druhé minuty. Start
/// na savaně, kde klik nic neudělá; „Ukaž mi", které jen vycentrovalo kameru;
/// a slib žánru („město pracuje samo, klidně odejdi") hra nikde neřekla.
/// Tahle třída drží logiku; kreslí <c>GuideRenderer</c> a oslavy obrazovka.</para>
///
/// <para>Vrstva UI: simulaci jen čte (zeptá se na terén a místa pro stavbu),
/// nic do ní nezapisuje. Zapisuje jen do profilu — které okamžiky hráč viděl.</para>
/// </summary>
internal sealed class OnboardingGuide
{
    /// <summary>Jak často se přepočítá, na co ukázat (s). Hledání místa není zadarmo.</summary>
    private const float RetargetSeconds = 0.5f;

    /// <summary>Jak daleko od startu se hledá strom nebo místo pro stavbu.</summary>
    private const int SearchRadius = 16;

    /// <summary>Kdy nejdřív smí zaznít „klidně zavři" (s hraní) — konec prvních pěti minut.</summary>
    private const float SafeToCloseAfterSeconds = 300f;

    /// <summary>Kolik budov musí stát, aby první noc bylo co rozsvítit.</summary>
    private const int FirstNightMinBuildings = 3;

    private readonly GameContent _content;
    private readonly Simulation _simulation;
    private readonly PlayerProfile _profile;
    private readonly int _safeToCloseStep;
    private readonly Queue<OnboardingMoment> _moments = new();

    private float _retargetTimer;
    private float _playSeconds;
    private int _lastStep = -1;

    /// <param name="content">Obsah hry.</param>
    /// <param name="simulation">Běžící hra (jen se čte).</param>
    /// <param name="profile">Profil hráče — které okamžiky už viděl.</param>
    public OnboardingGuide(GameContent content, Simulation simulation, PlayerProfile profile)
    {
        _content = content;
        _simulation = simulation;
        _profile = profile;
        StartTile = StartSiteFinder.Find(simulation);

        // „Klidně zavři" patří za krok, kdy už město roste samo (grow) — dřív
        // by to byl slib, který hráč ještě neviděl splněný.
        _safeToCloseStep = int.MaxValue;
        for (int i = 0; i < content.Tutorial.Count; i++)
        {
            if (content.Tutorial[i].Id == "grow")
            {
                _safeToCloseStep = i + 1;
                break;
            }
        }
    }

    /// <summary>
    /// Běží ještě tichý start? Dokud průvodce vede první kroky (do chvíle, kdy
    /// vesnice roste sama), nemá hráče rušit nic, co s nimi nesouvisí.
    /// Změřeno na snímcích: v prvních třech vteřinách vyskočily „výsledek voleb"
    /// a dvě zakázky — přesně když měl hráč sledovat šipku na strom.
    /// </summary>
    public bool IsQuietStart => !_simulation.IsTutorialFinished && _simulation.TutorialStep < _safeToCloseStep;

    /// <summary>
    /// Smí se toto hlášení ukázat během tichého startu? Jen to, co patří
    /// k prvním krokům: krok průvodce (milník), splněný úkol, achievement.
    /// Zakázky, volby a prosby obyvatel počkají — nic se neztratí, jen se
    /// neozývají.
    /// </summary>
    public static bool BelongsToStart(NotificationKind kind) => kind is NotificationKind.Milestone
        or NotificationKind.QuestCompleted
        or NotificationKind.AchievementUnlocked
        or NotificationKind.BuildingMilestone
        or NotificationKind.Ascended;

    /// <summary>Kde hra začíná (táborák, kamera, první dům).</summary>
    public (int X, int Y) StartTile { get; }

    /// <summary>Hoří na startu táborák? Jen dokud nestojí nic — pak je středem města první dům.</summary>
    public bool ShowCampfire => _simulation.Buildings.Length == 0;

    /// <summary>Na co se právě ukazuje.</summary>
    public GuideTarget Target { get; private set; } = GuideTarget.None;

    /// <summary>
    /// Změnil se krok průvodce od posledního dotazu? (Obrazovka pak rovnou
    /// vybere budovu, kterou krok chce — hráč nemusí hledat v katalogu.)
    /// </summary>
    public bool TryTakeStepChange(out TutorialStepDef step)
    {
        step = null!;
        int current = _simulation.TutorialStep;
        if (current == _lastStep || _simulation.CurrentTutorialStep is not { } now)
        {
            _lastStep = current;
            return false;
        }

        _lastStep = current;
        step = now;
        return true;
    }

    /// <summary>Volá se každý snímek.</summary>
    public void Update(float dt, double nightFactor)
    {
        _playSeconds += dt;
        _retargetTimer -= dt;
        if (_retargetTimer <= 0f || !IsStillValid(Target))
        {
            _retargetTimer = RetargetSeconds;
            Target = ComputeTarget();
        }

        if (nightFactor > 0.5 && _simulation.Buildings.Length >= FirstNightMinBuildings)
        {
            Offer(OnboardingMoment.FirstNight);
        }

        // Obojí zároveň: až vesnice roste sama (jinak by hráč slibu nevěřil)
        // a až po pěti minutách. Změřeno: vesnice doroste za necelou minutu —
        // „klidně zavři" ve 40. vteřině by hráče z hry poslal dřív, než začala.
        if (_simulation.TutorialStep >= _safeToCloseStep && _playSeconds >= SafeToCloseAfterSeconds)
        {
            Offer(OnboardingMoment.SafeToClose);
        }
    }

    /// <summary>Co vyrobila první budova, která pracovala sama (pro větu oslavy); −1 = zatím nic.</summary>
    public int WorksAloneResource { get; private set; } = -1;

    /// <summary>
    /// Budova něco vyrobila (z fronty vizuálních událostí). První výroba, do
    /// které hráč neklikl, je háček hry — „tohle se děje beze mě".
    /// </summary>
    public void OnProduced(int resourceIndex)
    {
        if (WorksAloneResource < 0)
        {
            WorksAloneResource = resourceIndex;
        }

        Offer(OnboardingMoment.WorksAlone);
    }

    /// <summary>Vyzvedne další okamžik k oslavě (jeden po druhém, ať se nepřekřikují).</summary>
    public bool TryTakeMoment(out OnboardingMoment moment) => _moments.TryDequeue(out moment);

    /// <summary>
    /// Nejbližší uzel suroviny k místu (strom na první klik; po kliku do
    /// prázdna „tamhle"). Vytěžené uzly se nepočítají.
    /// </summary>
    public bool TryFindNode(int resource, int fromX, int fromY, out int x, out int y)
    {
        for (int ring = 0; ring <= SearchRadius; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue;
                    }

                    int nodeResource = _simulation.NodeResourceAt(fromX + dx, fromY + dy);
                    if (nodeResource >= 0 && (resource < 0 || nodeResource == resource))
                    {
                        x = fromX + dx;
                        y = fromY + dy;
                        return true;
                    }
                }
            }
        }

        x = y = 0;
        return false;
    }

    /// <summary>
    /// Doporučené místo pro budovu u startu. Těžba k nejvíc uzlům v dosahu
    /// (dřevorubec do lesa), ostatní co nejblíž startu — ale ne přímo na
    /// táborák, ať první dům stojí „u ohně", ne na něm.
    /// </summary>
    public bool TrySuggestPlacement(int defIndex, out int x, out int y)
    {
        var def = _content.Buildings[defIndex];
        var (startX, startY) = StartTile;
        int bestScore = int.MinValue;
        x = y = 0;
        for (int ring = 1; ring <= SearchRadius; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring || !CanStand(defIndex, startX + dx, startY + dy))
                    {
                        continue;
                    }

                    if (!def.HarvestsTerrain)
                    {
                        x = startX + dx;
                        y = startY + dy;
                        return true; // nejbližší volné místo
                    }

                    int score = NodesAround(startX + dx, startY + dy, def.TerrainHarvestRadius) * 4 - ring;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        x = startX + dx;
                        y = startY + dy;
                    }
                }
            }
        }

        return bestScore != int.MinValue;
    }

    private GuideTarget ComputeTarget()
    {
        if (_simulation.CurrentTutorialStep is not { } step)
        {
            return GuideTarget.None;
        }

        var (startX, startY) = StartTile;
        switch (step.Focus.Kind)
        {
            case FocusKind.Harvest when _content.Resources.TryIndexOf(step.Focus.Target, out int resource):
                return TryFindNode(resource, startX, startY, out int nodeX, out int nodeY)
                    ? new GuideTarget(GuidePointer.Harvest, nodeX, nodeY, -1)
                    : GuideTarget.None;

            case FocusKind.Build when step.Focus.BuildingIndex >= 0:
                int defIndex = step.Focus.BuildingIndex;
                return TrySuggestPlacement(defIndex, out int siteX, out int siteY)
                    ? new GuideTarget(GuidePointer.Build, siteX, siteY, defIndex)
                    : GuideTarget.None;

            default:
                return GuideTarget.None;
        }
    }

    /// <summary>Platí cíl pořád? (Strom mohl spadnout, místo zastavět.)</summary>
    private bool IsStillValid(GuideTarget target) => target.Kind switch
    {
        GuidePointer.Harvest => _simulation.NodeResourceAt(target.X, target.Y) >= 0,
        GuidePointer.Build => CanStand(target.DefIndex, target.X, target.Y),
        _ => true,
    };

    /// <summary>Stojí tu budova? Na cenu se neptá — ukazuje se místo, ne to, jestli na ni hráč má.</summary>
    private bool CanStand(int defIndex, int x, int y)
    {
        var result = _simulation.CanPlace(defIndex, x, y);
        return result is PlacementResult.Ok or PlacementResult.NotEnoughResources;
    }

    private int NodesAround(int cx, int cy, int radius)
    {
        int count = 0;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (_simulation.NodeResourceAt(x, y) >= 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Zařadí okamžik, pokud ho hráč ještě neviděl (a zapíše ho, ať se neopakuje).</summary>
    private void Offer(OnboardingMoment moment)
    {
        if (_profile.MarkMomentSeen(MomentId(moment)))
        {
            _moments.Enqueue(moment);
        }
    }

    /// <summary>Stabilní ID okamžiku v profilu (enum se může přejmenovat, profil ne).</summary>
    internal static string MomentId(OnboardingMoment moment) => moment switch
    {
        OnboardingMoment.WorksAlone => "works_alone",
        OnboardingMoment.FirstNight => "first_night",
        _ => "safe_to_close",
    };
}
