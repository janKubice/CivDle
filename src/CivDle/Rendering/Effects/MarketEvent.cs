using Microsoft.Xna.Framework;

namespace CivDle.Rendering.Effects;

/// <summary>Ve které části svého života tržiště je.</summary>
public enum MarketPhase
{
    /// <summary>Žádné tržiště není.</summary>
    Closed,

    /// <summary>Staví se stánky.</summary>
    RaisingStalls,

    /// <summary>Obchoduje se — tohle je ta část, kvůli které to celé je.</summary>
    Trading,

    /// <summary>Balí se a odjíždí.</summary>
    PackingUp,
}

/// <summary>
/// Tržiště, které vyroste po příjezdu karavany a zase se rozejde.
///
/// <para><b>Proč to vzniklo:</b> karavana dojela do města, vyplatila surovinu
/// a v tu ránu zmizela. Byla to událost bez následku — číslo, které vyskočilo
/// a spadlo zpátky. Tržiště z příjezdu dělá něco, co má začátek, průběh
/// a konec: stánky se postaví, lidi se k nim seběhnou, po chvíli se sbalí
/// a je po trhu. Svět se tím na pár minut změní, a právě to je na sledování
/// zajímavé.</para>
///
/// <para><b>Vrstva:</b> render. Tržiště nic nevyrábí a neukládá se — je to
/// následek události, ne herní stav. Kdyby ho viděla simulace, musel by se
/// ukládat a nesměl by se rozejít se savem.</para>
/// </summary>
public sealed class MarketEvent
{
    /// <summary>Jak dlouho se stavějí stánky.</summary>
    private const float RaiseSeconds = 2.5f;

    /// <summary>Jak dlouho se obchoduje. Dost dlouho, aby se stihli sejít lidi.</summary>
    private const float TradeSeconds = 45f;

    /// <summary>Jak dlouho se balí.</summary>
    private const float PackSeconds = 3f;

    private float _elapsed;

    /// <summary>Ve které fázi tržiště je.</summary>
    public MarketPhase Phase { get; private set; } = MarketPhase.Closed;

    /// <summary>Dlaždice, na které trh stojí.</summary>
    public int TileX { get; private set; }

    /// <summary>Dlaždice, na které trh stojí.</summary>
    public int TileY { get; private set; }

    /// <summary>Střed trhu ve world pixelech.</summary>
    public Vector2 Position =>
        new((TileX + 0.5f) * TerrainRenderer.TileSize, (TileY + 0.5f) * TerrainRenderer.TileSize);

    /// <summary>Stojí tu zrovna trh (byť se teprve staví nebo už balí)?</summary>
    public bool IsOpen => Phase != MarketPhase.Closed;

    /// <summary>
    /// Má se teď k trhu scházet dav? Jen když se obchoduje — na prázdné stánky
    /// nemá kdo chodit a při balení už jsou lidi pryč.
    /// </summary>
    public bool DrawsCrowd => Phase == MarketPhase.Trading;

    /// <summary>
    /// Jak vysoko stojí stánky (0 = nic, 1 = postavené). Stavění i balení se
    /// tím dá ukázat pohybem, ne přepnutím — trh, který naskočí celý naráz,
    /// vypadá jako chyba vykreslení.
    /// </summary>
    public float StallScale => Phase switch
    {
        MarketPhase.RaisingStalls => Math.Clamp(_elapsed / RaiseSeconds, 0f, 1f),
        MarketPhase.Trading => 1f,
        MarketPhase.PackingUp => 1f - Math.Clamp(_elapsed / PackSeconds, 0f, 1f),
        _ => 0f,
    };

    /// <summary>Karavana dorazila — postav trh.</summary>
    public void Open(int tileX, int tileY)
    {
        TileX = tileX;
        TileY = tileY;
        Phase = MarketPhase.RaisingStalls;
        _elapsed = 0f;
    }

    /// <summary>Posune trh v čase.</summary>
    public void Update(float dt)
    {
        if (Phase == MarketPhase.Closed)
        {
            return;
        }

        _elapsed += dt;

        switch (Phase)
        {
            case MarketPhase.RaisingStalls when _elapsed >= RaiseSeconds:
                Phase = MarketPhase.Trading;
                _elapsed = 0f;
                break;

            case MarketPhase.Trading when _elapsed >= TradeSeconds:
                Phase = MarketPhase.PackingUp;
                _elapsed = 0f;
                break;

            case MarketPhase.PackingUp when _elapsed >= PackSeconds:
                Phase = MarketPhase.Closed;
                _elapsed = 0f;
                break;
        }
    }
}
