using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Materiál, který si guvernér drží na stavbu, na kterou zrovna šetří.
///
/// <para><b>Proč to existuje:</b> výrobny si berou vstupy ve chvíli, kdy dorazí,
/// kdežto guvernér se rozhoduje jednou za pár sekund. Pila si vezme tři dřeva,
/// jakmile tam jsou — a dřevorubec stojí pět. Když dřevo teče pomalu, na pět se
/// nikdy nedostane a město zamrzne natrvalo (změřeno: 100 minut na 94/94 lidech
/// s plným jídlem, kamenem i vědou). Rezerva z toho dělá frontu: výrobny smí
/// brát jen to, co je <b>nad</b> ní.</para>
///
/// <para><b>Podlaha, ne zákaz.</b> Pila dál řeže, jen z přebytku nad rezervou. Díky
/// tomu rezerva nezablokuje řetěz, který ji má naplnit (dům chce dřevo i prkna,
/// prkna vznikají ze dřeva — pila bere dřevo nad rezervovanou pětkou).</para>
///
/// <para>Rezervu respektuje všechno, co může chvíli počkat: <b>výrobní recepty,
/// topení, údržba služeb, opotřebení nástrojů a automatika</b>. Změřeno, proč
/// i ty: v zimě topení spálilo každé dřevo a údržba sýpek snědla celý přítok
/// (0,74 z 0,77 dřeva za sekundu) — guvernér pak půl hodiny šetřil na farmu
/// a město mezitím hladovělo. Lidé radši chvíli mrznou a trh chvíli neslouží.</para>
///
/// <para>Mimo rezervu zůstává jen jídlo pro lidi a hráčovy příkazy (stavba,
/// výzkum): rezerva je guvernérova fronta, ne daň z hráčových rozhodnutí.</para>
///
/// <para>Vrstva: stav simulace (ukládá se, aby načtená hra pokračovala stejně).
/// Rozhoduje o ní guvernér, čte ji výroba.</para>
/// </summary>
public sealed class ConstructionClaim
{
    private readonly double[] _amounts;

    /// <param name="resourceCount">Kolik surovin hra má.</param>
    public ConstructionClaim(int resourceCount)
    {
        _amounts = new double[resourceCount];
    }

    /// <summary>Na jakou budovu se šetří; −1 = na nic.</summary>
    public int DefIndex { get; private set; } = -1;

    /// <summary>Od kterého tiku se na ni šetří (podle toho se pozná, že guvernér uvízl).</summary>
    public long SinceTick { get; private set; }

    /// <summary>Šetří guvernér na něco?</summary>
    public bool IsActive => DefIndex >= 0;

    /// <summary>Kolik suroviny je zadržené (0 = nic).</summary>
    public double AmountOf(int resourceIndex) => _amounts[resourceIndex];

    /// <summary>Zadržené množství po surovinách — pro výrobní tik (bez kopírování).</summary>
    internal double[] Amounts => _amounts;

    /// <summary>
    /// Začne (nebo pokračuje) šetřit na budovu. Když jde o tutéž budovu, čas
    /// začátku se nemění — jinak by se nedalo poznat, že šetření trvá dlouho.
    /// Rezerva nikdy nepřesáhne kapacitu skladu: víc se stejně nevejde.
    /// </summary>
    internal void Set(int defIndex, IReadOnlyList<ResourceAmount> cost, double[] storageCaps, long tick)
    {
        if (defIndex != DefIndex)
        {
            SinceTick = tick;
        }

        DefIndex = defIndex;
        Array.Clear(_amounts);
        for (int i = 0; i < cost.Count; i++)
        {
            int index = cost[i].ResourceIndex;
            _amounts[index] = Math.Min(cost[i].Amount, storageCaps[index]);
        }
    }

    /// <summary>Přestane šetřit (postaveno, nebo už není na co).</summary>
    internal void Clear()
    {
        DefIndex = -1;
        SinceTick = 0;
        Array.Clear(_amounts);
    }

    /// <summary>Obnoví rezervu ze savu.</summary>
    internal void Restore(int defIndex, long sinceTick, IReadOnlyList<(int ResourceIndex, double Amount)> amounts)
    {
        Clear();
        if (defIndex < 0)
        {
            return;
        }

        DefIndex = defIndex;
        SinceTick = sinceTick;
        foreach (var (index, amount) in amounts)
        {
            if (index >= 0 && index < _amounts.Length)
            {
                _amounts[index] = Math.Max(0, amount);
            }
        }
    }
}
