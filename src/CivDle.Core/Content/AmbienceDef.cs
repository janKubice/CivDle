namespace CivDle.Core.Content;

/// <summary>
/// Jedna ambientní zvuková kulisa z <c>data/ambience.json</c>. Zvuk se negeneruje
/// z nahrávek, ale ze čtyř čísel — projekt drží „no balast" a nechce vozit
/// audio assety (viz <c>AmbientMusic</c>, který takhle dělá i hudbu).
///
/// <para>Výběr: kulisa se použije, když sedí na biom pod městem a (volitelně) na
/// aktuální počasí. Kulisa vázaná na počasí má PŘEDNOST — déšť je slyšet víc než
/// les, ve kterém prší.</para>
/// </summary>
/// <param name="Id">Stabilní ID (jen pro čitelnost dat a chybové hlášky).</param>
/// <param name="BiomeIndices">Biomy, kde kulisa hraje; prázdné = jakýkoli.</param>
/// <param name="WeatherIndices">Jevy počasí, u kterých hraje; prázdné = jakékoli.</param>
/// <param name="NoiseLevel">Množství šumu (vítr, déšť, příboj) 0–1.</param>
/// <param name="ToneHz">Frekvence táhlého podkresu v Hz (0 = bez tónu).</param>
/// <param name="ToneLevel">Hlasitost podkresu 0–1.</param>
/// <param name="PulseHz">Rychlost pomalého vlnobití hlasitosti (0 = neklesá).</param>
/// <param name="Volume">Celková hlasitost kulisy 0–1.</param>
public sealed record AmbienceDef(
    string Id,
    IReadOnlyList<int> BiomeIndices,
    IReadOnlyList<int> WeatherIndices,
    double NoiseLevel,
    double ToneHz,
    double ToneLevel,
    double PulseHz,
    double Volume)
{
    /// <summary>Je kulisa vázaná na konkrétní počasí? Takové mají přednost před biomovými.</summary>
    public bool IsWeatherBound => WeatherIndices.Count > 0;

    /// <summary>Hraje tahle kulisa pro daný biom a jev počasí?</summary>
    public bool Matches(int biomeIndex, int weatherIndex)
    {
        if (BiomeIndices.Count > 0 && !BiomeIndices.Contains(biomeIndex))
        {
            return false;
        }

        return WeatherIndices.Count == 0 || (weatherIndex >= 0 && WeatherIndices.Contains(weatherIndex));
    }
}
