namespace CivDle.Core.Content;

/// <summary>
/// Stroj, který létá nad mapou — balon, dvouplošník, dopravní letadlo, dron.
///
/// <para>Proč zvlášť od <see cref="VehicleDef"/>: vozidlo se drží silnic a je
/// vázané terénem, letadlo ne. Sdílet jeden typ by znamenalo v každém záznamu
/// nést pole, která pro tu druhou půlku nedávají smysl (nosnost silnice vs.
/// výška letu).</para>
///
/// <para>Stejně jako vozidla je to <b>kulisa</b> — nic nevozí, jen dává mapě
/// měřítko a pohyb nad zástavbou. Letadlo vzlétá od budovy (letiště, hangár);
/// balon může vzlétnout odkudkoli nad městem.</para>
/// </summary>
/// <param name="Id">Stabilní ID. Sprite se hledá pod <c>agent.&lt;Id&gt;</c>.</param>
/// <param name="Color">Barva trupu (fallback, když sprite chybí).</param>
/// <param name="Speed">Rychlost v pixelech za sekundu.</param>
/// <param name="Altitude">
/// Jak vysoko letí, v pixelech. Kreslí se o tolik výš než jeho stín — z toho
/// vzniká dojem výšky, aniž by hra potřebovala třetí rozměr.
/// </param>
/// <param name="MinEraOrder">Od jaké éry létá.</param>
/// <param name="MaxEraOrder">Do jaké éry ještě létá; −1 = navždy.</param>
/// <param name="HomeBuildingIndex">
/// Budova, od které vzlétá; −1 = vzlétá odkudkoli nad zástavbou.
///
/// <para>Bez domovské budovy by letadlo bylo jen tapeta. S ní je odměnou za
/// postavené letiště — hráč ho postaví a nad městem začne něco létat.</para>
/// </param>
public sealed record AircraftDef(
    string Id,
    RgbColor Color,
    float Speed,
    float Altitude,
    int MinEraOrder,
    int MaxEraOrder,
    int HomeBuildingIndex)
{
    /// <summary>Létá tenhle typ v dané éře?</summary>
    public bool FitsEra(int eraOrder) =>
        eraOrder >= MinEraOrder && (MaxEraOrder < 0 || eraOrder <= MaxEraOrder);

    /// <summary>Vzlétá od konkrétní budovy (jinak stačí zástavba pod ním)?</summary>
    public bool NeedsHomeBuilding => HomeBuildingIndex >= 0;

    /// <summary>ID spritu v knihovně.</summary>
    public string SpriteId => $"agent.{Id}";
}
