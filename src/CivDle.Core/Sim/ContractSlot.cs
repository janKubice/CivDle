namespace CivDle.Core.Sim;

/// <summary>
/// Jedno místo na nástěnce zakázek: buď na něm visí nabídka, nebo se čeká, až
/// dojde nový zákazník.
///
/// <para>Struktura, ne třída: míst je pár, mění se každý tik (odpočet) a nemá
/// vlastní chování — jen data, se kterými pracuje <c>ContractSystem</c>.</para>
/// </summary>
public struct ContractSlot
{
    /// <summary>Index šablony v registru zakázek; −1 = místo je prázdné.</summary>
    public int DefIndex;

    /// <summary>Kolik suroviny zákazník chce (už po škálování).</summary>
    public long DemandAmount;

    /// <summary>Kolik tiků zbývá do vypršení nabídky.</summary>
    public int TicksLeft;

    /// <summary>Násobič odměny, kterým se přepočte základ ze šablony.</summary>
    public double RewardScale;

    /// <summary>Visí tu zrovna nabídka?</summary>
    public readonly bool IsActive => DefIndex >= 0;

    /// <summary>Prázdné místo čekající na nového zákazníka.</summary>
    public static ContractSlot Empty(int restockTicks) =>
        new() { DefIndex = -1, TicksLeft = restockTicks };
}
