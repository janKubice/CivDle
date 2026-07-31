using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Právě běžící prosba obyvatele. Struktura, ne třída — je jedna a je maličká.
/// </summary>
/// <param name="DefIndex">Šablona prosby; −1 = zrovna nikdo nic nechce.</param>
/// <param name="FirstNameIndex">Index křestního jména v katalogu.</param>
/// <param name="SurnameIndex">Index příjmení v katalogu.</param>
/// <param name="TicksLeft">Kolik tiků zbývá, než to obyvatel vzdá.</param>
public readonly record struct CitizenRequest(
    int DefIndex, int FirstNameIndex, int SurnameIndex, int TicksLeft)
{
    /// <summary>Čeká zrovna někdo na odpověď?</summary>
    public bool IsActive => DefIndex >= 0;

    /// <summary>Nikdo nic nechce.</summary>
    public static CitizenRequest None { get; } = new(-1, 0, 0, 0);
}

/// <summary>
/// Obyvatelé se jménem: občas se ozve někdo konkrétní, že si chce otevřít
/// živnost, a potřebuje k tomu materiál.
///
/// <para>Proč to ve hře je: populace byla jedno velké číslo a lidé na mapě
/// anonymní tečky. Tohle z čísla dělá lidi — a když hráč pomůže, zůstane po tom
/// na mapě budova, kterou <b>někdo založil</b> a která jeho jméno nese dál.</para>
///
/// <para>Prosba je vždycky jen jedna. Není to nástěnka zakázek: zakázka je
/// obchod, tohle je moment. Tři naráz by z toho udělaly další seznam úkolů.</para>
///
/// <para>Systém řídí jen „jak"; stav (kdo, co, kolik zbývá) drží simulace kvůli
/// savu — stejné dělení jako u úkolů a zakázek.</para>
/// </summary>
internal sealed class CitizenSystem
{
    private readonly GameContent _content;
    private readonly Random _rng;

    public CitizenSystem(GameContent content, long seed)
    {
        _content = content;
        _rng = new Random((int)(seed ^ 0x0C1721E5));
    }

    public void Tick(Simulation sim)
    {
        var catalog = _content.Citizens;
        if (!catalog.IsEnabled)
        {
            return;
        }

        if (sim.PendingCitizenRequest.IsActive)
        {
            var active = sim.PendingCitizenRequest;
            if (active.TicksLeft > 1)
            {
                sim.PendingCitizenRequest = active with { TicksLeft = active.TicksLeft - 1 };
                return;
            }

            // Vzdal to. Žádný trest — jen odešel, jako všechno ostatní ve hře.
            sim.PendingCitizenRequest = CitizenRequest.None;
            sim.CitizenCooldownTicks = catalog.GapTicks;
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.CitizenLeft, "toast.citizenLeft", "citizen.someone"));
            return;
        }

        if (sim.CitizenCooldownTicks > 0)
        {
            sim.CitizenCooldownTicks--;
            return;
        }

        Offer(sim, catalog);
    }

    /// <summary>Pošle za hráčem někoho nového — nebo počká, když ještě není s čím.</summary>
    private void Offer(Simulation sim, CitizenCatalog catalog)
    {
        int defIndex = PickEligible(sim, catalog);
        if (defIndex < 0)
        {
            sim.CitizenCooldownTicks = catalog.GapTicks;
            return;
        }

        var request = new CitizenRequest(
            defIndex,
            _rng.Next(catalog.FirstNames.Count),
            _rng.Next(catalog.Surnames.Count),
            catalog.Requests[defIndex].DurationTicks);

        sim.PendingCitizenRequest = request;
        sim.EnqueueNotification(new GameNotification(
            NotificationKind.CitizenAsks, "toast.citizenAsks", "citizen.someone"));
    }

    /// <summary>
    /// Vybere prosbu, na kterou už město dorostlo a jejíž budovu má odemčenou.
    /// Prosba o pekárnu ve světě, kde se pekárna ještě nedá postavit, by byla
    /// slib, který nejde splnit.
    /// </summary>
    private int PickEligible(Simulation sim, CitizenCatalog catalog)
    {
        var requests = catalog.Requests;
        int chosen = -1;
        int seen = 0;

        for (int i = 0; i < requests.Count; i++)
        {
            var candidate = requests[i];
            if (!sim.IsBuildingUnlocked(candidate.BuildingIndex))
            {
                continue;
            }

            if (candidate.Requirement is { } requirement
                && sim.EvaluateMetric(requirement.Kind, requirement.Param) < requirement.Target)
            {
                continue;
            }

            seen++;
            if (_rng.Next(seen) == 0)
            {
                chosen = i;
            }
        }

        return chosen;
    }
}
