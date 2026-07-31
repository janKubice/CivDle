using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Nástěnka zakázek: drží pár běžících objednávek, odpočítává jim termín
/// a prázdná místa po chvíli zaplní novou nabídkou.
///
/// <para>Proč to ve hře je: mezi náhodnou událostí (jednou za ~10 minut) a úkolem
/// (na desítky minut) nebylo nic, co by hráči dalo důvod něco udělat <b>teď</b>.
/// Zakázka je ta chybějící krátká smyčka — konkrétní surovina, viditelný termín,
/// odměna, kterou si hráč vyzvedne sám.</para>
///
/// <para>Odevzdání je záměrně <b>akce hráče</b>, ne automatika: kdyby se zakázka
/// splnila sama, jakmile má město dost, byl by z ní jen pasivní příjem. Takhle
/// je to rozhodnutí — surovinu, kterou odevzdáš, zrovna nemáš na stavbu.</para>
///
/// <para>Systém řídí jen „jak"; samotný stav (co visí, kolik zbývá) drží
/// simulace, protože se ukládá do savu — stejné dělení jako u úkolů.</para>
/// </summary>
internal sealed class ContractSystem
{
    private readonly GameContent _content;

    /// <summary>
    /// Vlastní generátor se seedem světa. Nabídka tím zůstane deterministická
    /// (stejný svět = stejné zakázky) a nemíchá se do jiných náhod v simulaci.
    /// </summary>
    private readonly Random _rng;

    public ContractSystem(GameContent content, long seed)
    {
        _content = content;
        _rng = new Random((int)(seed ^ 0x5C0FFEE));
    }

    public void Tick(Simulation sim)
    {
        var catalog = _content.Contracts;
        if (!catalog.IsEnabled)
        {
            return;
        }

        var slots = sim.ContractSlotsMutable;
        for (int i = 0; i < slots.Length; i++)
        {
            ref var slot = ref slots[i];
            if (slot.TicksLeft > 0)
            {
                slot.TicksLeft--;
                continue;
            }

            if (slot.IsActive)
            {
                // Vypršelo. Žádný trest — jen nabídka odešla; hráč o nic nepřišel,
                // což je konzistentní se zbytkem hry (soft pressure, ne pasti).
                slot = ContractSlot.Empty(catalog.Board.RestockTicks);
                sim.EnqueueNotification(new GameNotification(
                    NotificationKind.ContractExpired, "toast.contractExpired", "contract.board"));
                continue;
            }

            Offer(sim, catalog, ref slot);
        }
    }

    /// <summary>
    /// Vypíše na prázdné místo novou zakázku — nebo ho nechá prázdné, když
    /// město ještě na žádnou nedorostlo.
    /// </summary>
    private void Offer(Simulation sim, ContractCatalog catalog, ref ContractSlot slot)
    {
        int defIndex = PickEligible(sim, catalog, sim.ContractSlots);
        if (defIndex < 0)
        {
            // Nic vhodného. Zkusíme to zas za chvíli, ať se to nezkouší každý tik.
            slot.TicksLeft = catalog.Board.RestockTicks;
            return;
        }

        var def = catalog.Contracts[defIndex];
        double scale = catalog.Board.ScaleAfter(sim.ContractsCompleted);

        slot = new ContractSlot
        {
            DefIndex = defIndex,
            DemandAmount = Math.Max(1, (long)Math.Round(def.DemandAmount * scale)),
            TicksLeft = def.DurationTicks,
            RewardScale = scale,
        };

        sim.EnqueueNotification(new GameNotification(
            NotificationKind.ContractOffered, "toast.contractOffered", def.NameKey));
    }

    /// <summary>
    /// Vybere zakázku, na kterou už město dorostlo a která zrovna nevisí na jiném
    /// místě — dvě stejné nabídky vedle sebe vypadají jako chyba, ne jako nabídka.
    ///
    /// <para>Reservoir sampling: rovnoměrný výběr jedním průchodem bez pomocného
    /// seznamu. Zakázek jsou desítky a tohle běží jednou za pár sekund, ale je to
    /// stejně krátké jako alokovat.</para>
    /// </summary>
    private int PickEligible(Simulation sim, ContractCatalog catalog, ReadOnlySpan<ContractSlot> slots)
    {
        var contracts = catalog.Contracts;
        int chosen = -1;
        int seen = 0;

        for (int i = 0; i < contracts.Count; i++)
        {
            if (contracts[i].Requirement is { } requirement
                && sim.EvaluateMetric(requirement.Kind, requirement.Param) < requirement.Target)
            {
                continue;
            }

            if (IsAlreadyOffered(slots, i))
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

    private static bool IsAlreadyOffered(ReadOnlySpan<ContractSlot> slots, int defIndex)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].DefIndex == defIndex)
            {
                return true;
            }
        }

        return false;
    }
}
