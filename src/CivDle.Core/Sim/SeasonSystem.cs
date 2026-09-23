using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Roční období: čtyřtaktní rytmus, kvůli kterému není každá minuta idle hry
/// stejná jako ta předchozí. Na jaře se roste, v létě sklízí, na podzim sbírá
/// do zásoby, v zimě přežívá z toho, co se nastřádalo.
///
/// <para>Které období zrovna je, je čistá funkce čísla dne (viz
/// <see cref="SeasonCalendar.IndexForDay"/>) — nic se neukládá a nic se nemůže
/// rozejít se savem, stejně jako u počasí. Tenhle systém tak řeší jedinou věc,
/// která stav <b>má</b>: topení v zimě.</para>
///
/// <para>Zima ubírá měkce: pole nesou míň a lidé topí. Když dřevo dojde, růst
/// se zpomalí (<see cref="SeasonDef.ColdGrowthMult"/>), ale nikdo neumírá —
/// stejná dohoda jako u jídla.</para>
/// </summary>
internal sealed class SeasonSystem
{
    private readonly GameContent _content;

    public SeasonSystem(GameContent content) => _content = content;

    public void Tick(Simulation sim)
    {
        var calendar = _content.Seasons;
        if (!calendar.IsEnabled)
        {
            sim.HasFuelForHeating = true;
            return;
        }

        var season = calendar.Seasons[calendar.IndexForDay(sim.DayNumber)];
        if (!season.NeedsHeating || calendar.FuelResourceIndex < 0)
        {
            sim.HasFuelForHeating = true;
            return;
        }

        double demand = sim.Population * season.FuelPerPersonPerSecond / Simulation.TicksPerSecond;
        var resources = sim.Resources;
        int fuel = calendar.FuelResourceIndex;

        // Topí se jen z toho, co je nad rezervou guvernéra. Jinak zima spálila
        // každé dřevo, jakmile přišlo, guvernér nikdy nenašetřil pět dřev na
        // dřevorubce — a město stálo celou zimu i s tím, co by ho z ní dostalo.
        // Lidé radši chvíli mrznou (růst zpomalí), než aby město zamrzlo celé.
        double available = Math.Max(0, resources[fuel] - sim.Claim.AmountOf(fuel));
        double burned = Math.Min(available, demand);
        resources[fuel] -= burned;
        sim.Ledger.RecordConsumed(calendar.FuelResourceIndex, burned, ConsumptionKind.Heating);

        // Netopí se „skoro dost" — buď je čím, nebo město mrzne a přestane růst.
        sim.HasFuelForHeating = burned >= demand - 1e-9;
    }
}
