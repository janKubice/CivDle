using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Zpevněná zem pod hustou zástavbou — z velkoměsta má být shora vidět město,
/// ne louka s domečky.
///
/// <para>Proč to vzniklo: u aglomerace o statisících lidí prosvítala mezi
/// budovami pořád původní tráva a písek. Výsledek nevypadal jako metropole, ale
/// jako vesnice roztažená přes celou obrazovku — a hlavně, <b>park uprostřed
/// města nešlo poznat od kusu neupravené krajiny</b>, protože obojí bylo
/// zelené. Když se zem pod zástavbou zpevní, zeleň zbude jen tam, kde ji hráč
/// nechal, a ta pak nese význam.</para>
///
/// <para>Zastavěnost se počítá na <b>hrubé mřížce</b> a přepočítává jednou za
/// pár sekund, ne každý snímek — přesně jak to má dělat systém, který se týká
/// desítek tisíc budov. Je to čistě odvozená hodnota z budov, takže se nikam
/// neukládá a po načtení savu se prostě spočítá znovu.</para>
///
/// <para>Kreslí se <b>měkkými skvrnami</b>, ne dlaždicemi. Ostré čtverce by
/// z města udělaly šachovnici a hlavně by prozradily mřížku, která je jenom
/// pomocná — beton má mít okraj tam, kde končí domy, ne tam, kde končí buňka.</para>
///
/// <para>Vrstva: čistý render nad simulací. Kreslí se po terénu a před cestami,
/// takže silnice a budovy zůstanou nahoře.</para>
/// </summary>
public sealed class UrbanGroundRenderer
{
    /// <summary>
    /// Hrana buňky v dlaždicích. Menší buňka = přesnější obrys, ale víc kreslení;
    /// čtyři dlaždice drží obrys města i u řídkého předměstí.
    /// </summary>
    private const int CellTiles = 4;

    /// <summary>Jak často se zastavěnost přepočítá. Město se za dvě sekundy nezmění k nepoznání.</summary>
    private const double RebuildSeconds = 2.0;

    /// <summary>
    /// Kolik budov v buňce už znamená „plně zastavěno". Míň než počet dlaždic:
    /// mezi domy jsou ulice a dvorky, takže plně městský blok nikdy nemá budovu
    /// na každé dlaždici.
    /// </summary>
    private const float FullCoverage = CellTiles * CellTiles * 0.3f;

    /// <summary>Nejvyšší krytí betonu. Ne 1 — pod městem má být pořád znát, na čem stojí.</summary>
    private const float MaxAlpha = 0.9f;

    /// <summary>Pod tímhle krytím se buňka nekreslí; jinak by po mapě zůstávaly duchy po zbořeništích.</summary>
    private const float MinAlpha = 0.04f;

    /// <summary>
    /// Beton drží tón silnic, aby město působilo jako jeden materiál. Světlejší
    /// než stín schválně: dlažba na denním světle je světlá, a tmavá varianta
    /// vypadala přes trávu jako špinavý opar místo jako zem.
    /// </summary>
    private static readonly Color PavementColor = new(148, 144, 134);

    private readonly SoftShadow _blob;
    private readonly GameContent _content;
    private readonly Dictionary<long, float> _density = new();

    private double _sinceRebuild = RebuildSeconds; // ať první snímek hned počítá
    private int _lastBuildingCount = -1;

    public UrbanGroundRenderer(SoftShadow blob, GameContent content)
    {
        _blob = blob;
        _content = content;
    }

    /// <summary>
    /// Přepočítá zastavěnost, když je čas. Volá se herním časem, takže se
    /// v pauze nic nemění.
    /// </summary>
    public void Update(float dt, Simulation simulation)
    {
        _sinceRebuild += dt;
        if (_sinceRebuild < RebuildSeconds && simulation.Buildings.Length == _lastBuildingCount)
        {
            return;
        }

        _sinceRebuild = 0;
        _lastBuildingCount = simulation.Buildings.Length;
        Rebuild(simulation);
    }

    /// <summary>
    /// Postaví mřížku znovu z budov.
    ///
    /// <para>Celý přepočet místo průběžné údržby schválně: zastavěnost je
    /// odvozená hodnota a průběžné dopočítávání by znamenalo hlídat každou
    /// stavbu, demolici, přesun a sloučení. Jeden průchod polem budov jednou za
    /// dvě sekundy je levnější než čtyři místa, kde se dá zapomenout na
    /// aktualizaci.</para>
    /// </summary>
    private void Rebuild(Simulation simulation)
    {
        _density.Clear();

        var buildings = simulation.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];

            // Kolik která budova zpevní, říkají data: pole a rybárny mají
            // zůstat zelené, jinak by z farmy bylo parkoviště.
            float paving = (float)_content.Buildings[building.DefIndex].Paving;
            if (paving <= 0f)
            {
                continue;
            }

            long key = CellKey(CellOf(building.X), CellOf(building.Y));
            _density.TryGetValue(key, out float weight);
            _density[key] = weight + paving;
        }
    }

    /// <summary>Vykreslí zpevněnou zem přes viditelnou část mapy.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_density.Count == 0)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        const int cellPixels = CellTiles * tileSize;

        var (min, max) = camera.VisibleWorldBounds();

        // O buňku navíc na každou stranu: skvrna přesahuje svou buňku, takže by
        // se u kraje obrazovky jinak objevovala a mizela viditelná hrana.
        int fromX = (int)Math.Floor(min.X / cellPixels) - 1;
        int toX = (int)Math.Ceiling(max.X / cellPixels) + 1;
        int fromY = (int)Math.Floor(min.Y / cellPixels) - 1;
        int toY = (int)Math.Ceiling(max.Y / cellPixels) + 1;

        spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        for (int cellY = fromY; cellY <= toY; cellY++)
        {
            for (int cellX = fromX; cellX <= toX; cellX++)
            {
                if (!_density.TryGetValue(CellKey(cellX, cellY), out float weight))
                {
                    continue;
                }

                float alpha = Math.Min(1f, weight / FullCoverage) * MaxAlpha;
                if (alpha < MinAlpha)
                {
                    continue;
                }

                // Skvrna je větší než buňka, aby se sousedé slili do jedné plochy
                // místo mřížky teček.
                int spread = cellPixels / 2;
                var destination = new Rectangle(
                    cellX * cellPixels - spread / 2,
                    cellY * cellPixels - spread / 2,
                    cellPixels + spread,
                    cellPixels + spread);

                _blob.Draw(spriteBatch, destination, PavementColor * alpha);
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Buňka, do které dlaždice patří.
    ///
    /// <para>Dělení se musí zaokrouhlovat <b>dolů</b>, ne k nule: mapa jde i do
    /// záporných souřadnic a celočíselné dělení v C# by dlaždice −3 až 3
    /// naskládalo do jedné buňky, takže by beton u západního okraje města
    /// přetékal o kus dál, než kam sahá zástavba.</para>
    /// </summary>
    private static int CellOf(int tile) =>
        tile >= 0 ? tile / CellTiles : (tile - CellTiles + 1) / CellTiles;

    /// <summary>Klíč buňky. Mapa je nekonečná i do záporných souřadnic, proto obě půlky zvlášť.</summary>
    private static long CellKey(int cellX, int cellY) =>
        ((long)cellX << 32) | (uint)cellY;
}
