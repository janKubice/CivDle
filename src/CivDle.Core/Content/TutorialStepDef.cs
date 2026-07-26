using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Kam má UI hráče poslat, aby krok splnil. Data říkají „na tohle se podívej",
/// kód říká „jak to otevřít" — kroky tak nemusí znát obrazovky ani tlačítka.
/// </summary>
public enum FocusKind
{
    /// <summary>Bez nápovědy — hráč má vše na mapě před sebou.</summary>
    None,

    /// <summary>Ukázat mapu / vycentrovat město (klikací činnost přímo ve světě).</summary>
    Map,

    /// <summary>Otevřít stavební katalog na konkrétní budově.</summary>
    Build,

    /// <summary>Zapnout nástroj mapy (sázení, silnice, terraformace…).</summary>
    Tool,

    /// <summary>Otevřít obrazovku (výzkum, úkoly, Vzestup…).</summary>
    Screen,
}

/// <summary>
/// Ukazatel „kam se podívat" pro jeden krok průvodce. Neměnný.
/// </summary>
/// <param name="Kind">Druh cíle.</param>
/// <param name="BuildingIndex">Index budovy pro <see cref="FocusKind.Build"/>, jinak −1.</param>
/// <param name="Target">ID nástroje/obrazovky pro <see cref="FocusKind.Tool"/> a <see cref="FocusKind.Screen"/>.</param>
public readonly record struct FocusHint(FocusKind Kind, int BuildingIndex, string Target)
{
    /// <summary>Nápověda, která nikam neukazuje.</summary>
    public static readonly FocusHint None = new(FocusKind.None, -1, string.Empty);
}

/// <summary>
/// Jeden krok průvodce prvními kroky z <c>data/tutorial.json</c>. Kroky se plní
/// po pořádku: aktivní je vždy právě jeden a HUD ho ukazuje jako hlavní cíl.
///
/// <para>Existuje kvůli tomu, že hra sama o sobě neřekne, co po hráči chce —
/// klikací sběr, stavění a výzkum jsou tři různé smyčky a bez vedení není jasné,
/// kterou začít. Krok nese jak podmínku (kdy je hotový), tak nápovědu (jak na to)
/// a ukazatel, kam kliknout.</para>
///
/// <para>Text v jazycích pod <c>tutorial.&lt;Id&gt;</c> (název) a
/// <c>tutorial.&lt;Id&gt;.hint</c> (jak na to).</para>
/// </summary>
/// <param name="Id">Stabilní ID (do lokalizace; pořadí v savu je index).</param>
/// <param name="Condition">Kdy je krok hotový (metrika ≥ práh).</param>
/// <param name="Focus">Kam UI hráče pošle tlačítkem „Ukaž mi".</param>
public sealed record TutorialStepDef(
    string Id,
    GoalCondition Condition,
    FocusHint Focus)
{
    /// <summary>Lokalizační klíč názvu kroku (co se má stát).</summary>
    public string NameKey => $"tutorial.{Id}";

    /// <summary>Lokalizační klíč nápovědy (jak na to).</summary>
    public string HintKey => $"tutorial.{Id}.hint";
}
