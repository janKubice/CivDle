using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Jedna zakázka z <c>data/contracts.json</c>: někdo si objedná surovinu, dá na
/// to termín a zaplatí jinými surovinami.
///
/// <para>Proč to ve hře je: mezi událostmi (jednou za ~10 minut) a úkoly (na
/// desítky minut) nebylo nic, co by hráče drželo u obrazovky — jen sledování
/// čísel. Zakázka je krátká smyčka: konkrétní cíl, viditelný termín a odměna,
/// kterou si hráč sám vyzvedne. Kvůli ní má smysl <b>řídit</b> výrobu, ne jen
/// čekat.</para>
///
/// <para>Definice je šablona, ne konkrétní nabídka. Množství i odměna se při
/// vypsání škálují podle toho, jak daleko je hráč (viz
/// <see cref="ContractBoardConfig.ScaleGrowth"/>), takže stejná zakázka roste
/// s městem a nikdy nedojdou — stejný princip jako u dynamických úkolů.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="DemandResourceIndex">Kterou surovinu si zákazník objednal.</param>
/// <param name="DemandAmount">Základní objednané množství (před škálováním).</param>
/// <param name="Reward">Základní odměna (před škálováním).</param>
/// <param name="DurationSeconds">Kolik času hráč na splnění má.</param>
/// <param name="RequirementOrNull">
/// Od jaké fáze se zakázka smí nabízet; <c>null</c> = odjakživa. Bez toho by si
/// osada o pěti lidech objednala ocel — a nabídka, kterou hráč nemůže splnit,
/// je horší než žádná.
/// </param>
/// <param name="LegacyPoints">
/// Body Odkazu za odevzdání — <b>neškálují se</b>. Pozdní hra má surovin
/// nadbytek a odměna v nich nic neznamená; bod Odkazu je vzácný a znamená
/// vždycky totéž, proto pevně.
/// </param>
public sealed record ContractDef(
    string Id,
    int DemandResourceIndex,
    int DemandAmount,
    IReadOnlyList<ResourceAmount> Reward,
    double DurationSeconds,
    GoalCondition? RequirementOrNull = null,
    int LegacyPoints = 0)
{
    /// <summary>Lokalizační klíč jména zakázky (kdo a co chce).</summary>
    public string NameKey => $"contract.{Id}";

    /// <summary>Kolik tiků zakázka vydrží, než vyprší.</summary>
    public int DurationTicks => (int)Math.Round(DurationSeconds * Simulation.TicksPerSecond);

    /// <summary>Podmínka, od které se smí nabízet (nebo <c>null</c>).</summary>
    public GoalCondition? Requirement => RequirementOrNull;
}

/// <summary>
/// Nastavení nástěnky zakázek z <c>data/contracts.json</c> — kolik jich běží,
/// jak rychle se doplňují a jak rostou s hráčem.
/// </summary>
/// <param name="Slots">Kolik zakázek visí najednou.</param>
/// <param name="RestockSeconds">Za jak dlouho se prázdné místo zaplní novou.</param>
/// <param name="ScaleGrowth">
/// O kolik se objednávka i odměna zvětší za každou splněnou zakázku. Drží
/// nabídku úměrnou městu — bez toho by pozdní hra dostávala objednávky na
/// dvacet prken.
/// </param>
/// <param name="MaxScale">
/// Od kdy nabídka roste jen pomalu (<paramref name="SoftGrowth"/>). Dřív tvrdý
/// strop: po ~60 zakázkách byly objednávky napořád stejné a pozdní hra neměla
/// na nástěnce co dělat.
/// </param>
/// <param name="SoftGrowth">
/// Růst za každou zakázku nad <paramref name="MaxScale"/>; 1 = tvrdý strop
/// (starší data).
/// </param>
public sealed record ContractBoardConfig(
    int Slots,
    double RestockSeconds,
    double ScaleGrowth,
    double MaxScale,
    double SoftGrowth = 1.0)
{
    /// <summary>
    /// Pojistka proti přetečení čísel, ne herní strop: odměna se počítá do
    /// <c>int</c> a základ zakázky je nejvýš stovky, milion je tak bezpečný
    /// a v praxi nedosažitelný (tisíce odevzdaných zakázek).
    /// </summary>
    public const double NumericCeiling = 1_000_000;

    /// <summary>Vypnuté zakázky — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static ContractBoardConfig Disabled { get; } = new(0, 0, 1.0, 1.0);

    /// <summary>Má smysl nástěnku vůbec vést?</summary>
    public bool IsEnabled => Slots > 0;

    /// <summary>Za jak dlouho (v ticích) se prázdné místo zaplní.</summary>
    public int RestockTicks => (int)Math.Round(RestockSeconds * Simulation.TicksPerSecond);

    /// <summary>
    /// Násobič velikosti nabídky po daném počtu splněných zakázek: do
    /// <see cref="MaxScale"/> roste o <see cref="ScaleGrowth"/>, pak jen
    /// o <see cref="SoftGrowth"/>.
    /// </summary>
    public double ScaleAfter(long completed)
    {
        double raw = Math.Pow(ScaleGrowth, Math.Max(0, completed));
        if (raw <= MaxScale || SoftGrowth <= 1.0 || ScaleGrowth <= 1.0)
        {
            return Math.Min(MaxScale, raw);
        }

        // Kolik zakázek trvalo, než nabídka došla ke stropu — odtud se počítá
        // pomalý růst, aby se na hraně nic neskokovalo.
        double completedAtCap = Math.Log(MaxScale) / Math.Log(ScaleGrowth);
        double beyond = completed - completedAtCap;
        return Math.Min(NumericCeiling, MaxScale * Math.Pow(SoftGrowth, beyond));
    }
}

/// <summary>
/// Nástěnka zakázek tak, jak leží v datech: šablony plus jejich nastavení.
/// Prázdný katalog je legitimní stav (hra bez zakázek).
/// </summary>
public sealed record ContractCatalog(ContractBoardConfig Board, DefRegistry<ContractDef> Contracts)
{
    /// <summary>Prázdná nástěnka — pro starší data i pro testy, které zakázky neřeší.</summary>
    public static ContractCatalog Empty { get; } = new(
        ContractBoardConfig.Disabled,
        new DefRegistry<ContractDef>(Array.Empty<ContractDef>(), c => c.Id, "zakázka", allowEmpty: true));

    /// <summary>Jsou zakázky zapnuté a je vůbec co nabízet?</summary>
    public bool IsEnabled => Board.IsEnabled && Contracts.Count > 0;
}
