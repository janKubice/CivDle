using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Hlídá <b>tvar</b> boční lišty — tedy které ikony v ní vůbec mají být.
///
/// <para>Proč to existuje: lišta se přestavovala jedině tehdy, když přibyla
/// odemčená funkce z <c>features.json</c>. Jenže půlka tlačítek na ten seznam
/// vůbec nekouká — orbita se objeví, teprve až stojí kosmodrom, zvonohra, až
/// nějaká zazvoní, doktríny s prvním bodem Vzestupu. Všechny tyhle podmínky
/// nastanou <b>pozdě</b>, ve chvíli, kdy je ze seznamu funkcí odemčené už
/// všechno a počítadlo se nemá jak hnout. Tlačítko se pak nikdy neobjevilo:
/// mechanika ve hře byla, hráč se k ní neměl jak dostat.</para>
///
/// <para>Proč se nepřestavuje prostě po každé stavbě: dvě z podmínek se ptají
/// celé zástavby („stojí kosmodrom?", „stojí zvonohra?"), což je průchod polem
/// budov. Ptají se proto jen tehdy, když se zástavba opravdu změnila —
/// <see cref="Simulation.BuildingRevision"/>. Zbytek jsou laciné vlastnosti,
/// na které se dá koukat každý snímek.</para>
///
/// <para>Vrstva: UI. Simulaci jen čte, nic v ní nemění.</para>
/// </summary>
public sealed class HudLayout
{
    private readonly bool _doctrinesInContent;

    private Shape _shape;
    private long _buildingRevision = -1;
    private bool _hasLaunchSite;
    private bool _hasCarillon;

    /// <summary>
    /// Zapamatuje si stav, ve kterém se lišta právě staví. První dotaz po
    /// vytvoření tedy odpoví „nic se nezměnilo" — lišta je čerstvá.
    /// </summary>
    public HudLayout(GameContent content, Simulation simulation)
    {
        _doctrinesInContent = content.Doctrines.IsEnabled;
        _shape = Read(simulation);
    }

    /// <summary>
    /// Změnil se od minulého dotazu tvar lišty? Když ano, nový stav si rovnou
    /// zapamatuje — volající tedy dostane <c>true</c> právě jednou za změnu.
    /// </summary>
    public bool HasChanged(Simulation simulation)
    {
        var next = Read(simulation);
        if (next == _shape)
        {
            return false;
        }

        _shape = next;
        return true;
    }

    private Shape Read(Simulation simulation)
    {
        // Kosmodrom a zvonohra se hledají průchodem přes všechny budovy. Dokud
        // se zástavba nezměnila, změnit se nemohla ani odpověď — a tenhle dotaz
        // padá každý snímek.
        if (simulation.BuildingRevision != _buildingRevision)
        {
            _buildingRevision = simulation.BuildingRevision;
            _hasLaunchSite = simulation.HasLaunchSite;
            _hasCarillon = simulation.HasCarillon;
        }

        return new Shape(
            simulation.UnlockedFeatureCount,
            simulation.ContractsEnabled,
            simulation.GrandWorkAvailable,
            simulation.FrontierDefense,
            _hasLaunchSite,
            simulation.Figures.Remembered.Count > 0,
            _doctrinesInContent && (simulation.PrestigePoints > 0 || simulation.Doctrine is not null),
            simulation.LegacyAvailable,
            simulation.HistoryEnabled,
            _hasCarillon);
    }

    /// <summary>
    /// Podmínky, na kterých lišta stojí, v jednom porovnatelném balíčku.
    /// Record struct kvůli tomu porovnání — přibude podmínka, přibude položka,
    /// a nedá se zapomenout ji porovnat.
    /// </summary>
    private readonly record struct Shape(
        int UnlockedFeatures,
        bool Contracts,
        bool GrandWork,
        bool Frontier,
        bool LaunchSite,
        bool Figures,
        bool Doctrines,
        bool Legacy,
        bool History,
        bool Carillon);
}
