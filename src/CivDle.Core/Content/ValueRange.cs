namespace CivDle.Core.Content;

/// <summary>Uzavřený interval [Min, Max] na normalizovaných hodnotách 0–1.</summary>
public readonly record struct ValueRange(float Min, float Max)
{
    /// <summary>Celý rozsah 0–1 — výchozí, když definice interval neomezuje.</summary>
    public static readonly ValueRange Full = new(0f, 1f);

    /// <summary>Vrací true, když hodnota leží v intervalu (včetně krajů).</summary>
    public bool Contains(float value) => value >= Min && value <= Max;

    /// <summary>Neomezuje interval vůbec nic? (Definice, která rozsah neuvedla.)</summary>
    public bool IsFull => Min <= 0f && Max >= 1f;
}
