using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jak modlitba dopadla — UI z toho skládá hlášku i podívanou.</summary>
public enum PrayerOutcome
{
    /// <summary>Vyslyšeno: účinek proběhl.</summary>
    Answered,

    /// <summary>Nevyslyšeno: víra se utratila, nestalo se nic.</summary>
    Unanswered,

    /// <summary>
    /// Rána spadla, ale vedle. Platí jen pro <c>smite_*</c> modlitby: nebe se
    /// nedá odvolat, jen minout.
    /// </summary>
    Strayed,

    /// <summary>Nedost víry.</summary>
    NotEnoughFaith,

    /// <summary>Víra není odemčená / v datech vypnutá.</summary>
    Unavailable,
}

/// <summary>
/// Modlitby: hráč obětuje víru a <b>doufá</b>.
///
/// <para>Proč to ve hře je: všechno ostatní v CivDle je jistota — postavíš,
/// vyrobí se. Modlitba je jediné místo, kde hráč vědomě riskuje: volí sílu,
/// a čím větší, tím dražší a tím spíš to nevyjde. Z toho vzniká rozhodnutí
/// (malá jistota vs. drahý hazard), které se jinde v idle hře nedá koupit.</para>
///
/// <para>Výsledek je <b>deterministický</b> ze seedu, tiku a cíle: stejný svět
/// se stejnou historií se zachová stejně. Bez toho by nešlo testovat a hráč by
/// mohl výsledek „přetáčet" znovunačtením savu.</para>
///
/// <para>Účinky jsou behavior-ID hooky (CLAUDE.md: data = co, kód = jak).
/// Neznámé ID se tiše přeskočí, ale víra se stejně utratí — z pohledu hráče
/// to je nevyslyšená modlitba, ne chyba.</para>
/// </summary>
internal sealed class PrayerSystem
{
    private readonly GameContent _content;
    private readonly long _seed;

    /// <summary>Kolikrát se hráč modlil — vstupuje do losování, ať se výsledky neopakují.</summary>
    private long _prayerCount;

    public PrayerSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
    }

    /// <summary>Počet dosud pronesených modliteb (do savu i do statistik).</summary>
    public long PrayerCount => _prayerCount;

    /// <summary>Obnoví počítadlo ze savu — jinak by se po načtení losovalo znovu stejně.</summary>
    public void RestoreCount(long count) => _prayerCount = Math.Max(0, count);

    /// <summary>
    /// Pronese modlitbu. Víra se strhne vždycky — obětuje se předem, ne až za
    /// výsledek; to je celý ten hazard.
    /// </summary>
    public PrayerOutcome Pray(Simulation sim, int prayerIndex, int strength, int targetX, int targetY)
    {
        var faith = _content.Faith;
        if (!faith.IsEnabled || prayerIndex < 0 || prayerIndex >= faith.Prayers.Count)
        {
            return PrayerOutcome.Unavailable;
        }

        var prayer = faith.Prayers[prayerIndex];
        int level = Math.Clamp(strength, 1, MaxStrength);
        int cost = prayer.CostAt(level);
        if (sim.GetResource(faith.FaithResourceIndex) < cost)
        {
            return PrayerOutcome.NotEnoughFaith;
        }

        sim.AddResource(faith.FaithResourceIndex, -cost);
        _prayerCount++;

        bool answered = Roll(sim, targetX, targetY) < prayer.ChanceAt(level);
        if (!answered)
        {
            // Žehnání se dá odepřít; rána ne. Když si hráč přivolá z nebe kámen
            // a nebe ho nevyslyší, kámen stejně spadne — jen vedle. Bez toho
            // byla drahá modlitba v půlce případů „utratil jsi víru a nestalo
            // se vůbec nic", což je nejhorší možná zpětná vazba.
            if (!IsSmite(prayer.Effect))
            {
                return PrayerOutcome.Unanswered;
            }

            var (strayX, strayY) = StrayTarget(sim, prayer, targetX, targetY);
            sim.ReportStrike(strayX, strayY);
            Apply(sim, prayer, level, strayX, strayY);
            return PrayerOutcome.Strayed;
        }

        sim.ReportStrike(targetX, targetY);
        Apply(sim, prayer, level, targetX, targetY);
        return PrayerOutcome.Answered;
    }

    /// <summary>Je to rána (ničí), nebo žehnání (pomáhá)?</summary>
    private static bool IsSmite(string effect) => effect.StartsWith("smite_", StringComparison.Ordinal);

    /// <summary>
    /// Kam rána dopadne, když mine. Odchylka je řádově dosah rány — dost na to,
    /// aby zasáhla něco jiného, ne tak, aby zmizela za obzor.
    /// </summary>
    private static (int X, int Y) StrayTarget(Simulation sim, PrayerDef prayer, int targetX, int targetY)
    {
        int spread = Math.Max(2, prayer.RadiusTiles * 2);
        unchecked
        {
            // Vlastní míchání, ať odchylka nekoreluje s losem o vyslyšení —
            // jinak by minutí padalo pořád stejným směrem.
            ulong h = (ulong)sim.TickCount * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)targetX * 0xC2B2AE3D27D4EB4FUL;
            h ^= (ulong)(uint)targetY * 0x165667B19E3779F9UL;
            h ^= h >> 29;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 32;

            int dx = (int)(h % (ulong)(spread * 2 + 1)) - spread;
            int dy = (int)((h >> 20) % (ulong)(spread * 2 + 1)) - spread;
            return (targetX + dx, targetY + dy);
        }
    }

    /// <summary>Nejvyšší síla modlitby, kterou lze zvolit.</summary>
    public const int MaxStrength = 5;

    /// <summary>
    /// Los 0–1, deterministicky ze seedu, počtu modliteb, tiku a cíle. Míchání
    /// je obyčejný hash — nepotřebuje kvalitu kryptografie, jen aby dvě sousední
    /// dlaždice nedaly stejné číslo.
    /// </summary>
    private double Roll(Simulation sim, int targetX, int targetY)
    {
        unchecked
        {
            ulong h = (ulong)_seed * 6364136223846793005UL + 1442695040888963407UL;
            h ^= (ulong)_prayerCount * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)sim.TickCount * 0xBF58476D1CE4E5B9UL;
            h ^= (ulong)(uint)targetX * 0x94D049BB133111EBUL;
            h ^= (ulong)(uint)targetY * 0xD6E8FEB86659FD93UL;
            h ^= h >> 31;
            h *= 0x7FB5D329728EA185UL;
            h ^= h >> 27;
            return (h >> 11) / (double)(1UL << 53);
        }
    }

    /// <summary>Provede účinek modlitby. Neznámé behavior-ID se tiše přeskočí.</summary>
    private void Apply(Simulation sim, PrayerDef prayer, int strength, int targetX, int targetY)
    {
        double magnitude = prayer.MagnitudeAt(strength);
        switch (prayer.Effect)
        {
            case "bless_harvest":
                // Přímý dar do skladu — nejjednodušší a v nouzi nejcennější.
                sim.AddResource(_content.Gameplay.FoodResourceIndex, magnitude);
                break;

            case "bless_rain":
                sim.StartBlessing(BlessingKind.Rain, magnitude, prayer.RadiusTiles, targetX, targetY);
                break;

            case "bless_growth":
                sim.StartBlessing(BlessingKind.Growth, magnitude, prayer.RadiusTiles, targetX, targetY);
                break;

            case "bless_cleanse":
                sim.CleanseArea(targetX, targetY, prayer.RadiusTiles, magnitude);
                break;

            case "smite_meteor":
                sim.StrikeMeteor(targetX, targetY, prayer.RadiusTiles);
                break;

            case "smite_flood":
                sim.StrikeFlood(targetX, targetY, prayer.RadiusTiles);
                break;

            case "bless_regrow":
                // Rychlý růst: to, co pily vykácely, se vrátí naráz. Lesní školka
                // dělá totéž pomalu a bez rizika — tohle je ta netrpělivá cesta.
                sim.RegrowArea(targetX, targetY, prayer.RadiusTiles);
                break;

            case "bless_reveal":
                sim.Fog.Reveal(targetX, targetY, prayer.RadiusTiles);
                break;

            case "bless_windfall":
                // Dar do skladu, ale jen ze surovin, které hráč zná — jinak by
                // modlitba obcházela celou progresi.
                sim.GrantKnownResource(magnitude, targetX, targetY);
                break;

            case "bless_festival":
                // Obchází ochlazení slavnosti schválně: hráč za to zaplatil vírou
                // a riskoval nevyslyšení. Kdyby modlitba mlčky nic neudělala jen
                // proto, že tlačítko zrovna nejde, byla by to past.
                sim.ForceBoost();
                break;

            case "smite_blight":
                sim.BlightArea(targetX, targetY, prayer.RadiusTiles);
                break;
        }
    }
}

/// <summary>Druh probíhajícího požehnání (dočasný bonus z vyslyšené modlitby).</summary>
public enum BlessingKind
{
    /// <summary>Déšť: rychlejší výroba jídla.</summary>
    Rain,

    /// <summary>Plodnost: rychlejší růst obyvatel.</summary>
    Growth,
}
