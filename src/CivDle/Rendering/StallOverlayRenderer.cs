using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Inspektor úzkých hrdel: každá budova dostane barvu podle toho, jestli
/// pracuje — a když ne, proč.
///
/// <para>Proč to hra potřebuje: ve městě o tisících budov je „něco stojí"
/// zpráva bez ceny. Lišta umí říct, že nečinných budov je čtyřicet, ale ne
/// <b>které</b> a <b>kde</b>. Hráč pak obchází město a kliká po jedné.
/// Barevná mapa odpoví na jeden pohled.</para>
///
/// <para>Barvy nesou příčinu, ne závažnost, a jsou vybrané tak, aby šly od
/// sebe i při oddálení: červená = chybí vstup, modrá = chybí lidi, oranžová =
/// došlo, co těžit, žlutá = plný sklad (vyrábí naprázdno), zelená = jede.
/// Rozestavěné budovy jsou šedé — ty nestojí, ty se staví.</para>
///
/// <para><b>Plný sklad není stav budovy</b>, ale výsledek: výroba se kvůli
/// němu nezastaví, přebytek propadá. Dopočítává se proto tady z výstupů
/// receptu, ne ze simulace — je to otázka pohledu, ne stav světa.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění, kreslí jen to, co je
/// ve výřezu kamery.</para>
/// </summary>
public sealed class StallOverlayRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Nad tímhle zaplněním skladu se výstup považuje za ucpaný.</summary>
    private const double FullStorageThreshold = 0.999;

    /// <summary>Síla závoje. Pod ním musí být pořád poznat, co za budovu to je.</summary>
    private const float Alpha = 0.55f;

    private static readonly Color Working = new(120, 210, 130);
    private static readonly Color MissingInput = new(214, 82, 74);
    private static readonly Color NoWorkers = new(88, 150, 226);
    private static readonly Color NoTerrain = new(226, 152, 62);
    private static readonly Color StorageFull = new(230, 208, 88);
    private static readonly Color Building = new(130, 134, 146);
    private static readonly Color Damaged = new(198, 66, 120);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    /// <summary>Indexy budov ve výřezu. Jeden seznam na celý život — žádná alokace za snímek.</summary>
    private readonly List<int> _visible = new();

    public StallOverlayRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Vykreslí závoj nad budovami ve výřezu.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        var buildings = simulation.Buildings;

        // Přes index, ne celé město: inspektor se zapíná právě tehdy, když má
        // hráč velké město a chce vědět, co se v něm zaseklo.
        simulation.BuildingsIn(
            (int)Math.Floor(min.X / TileSize) - 1,
            (int)Math.Floor(min.Y / TileSize) - 1,
            (int)Math.Ceiling(max.X / TileSize) + 1,
            (int)Math.Ceiling(max.Y / TileSize) + 1,
            _visible);

        spriteBatch.Begin(transformMatrix: camera.Transform);

        for (int slot = 0; slot < _visible.Count; slot++)
        {
            int i = _visible[slot];
            if (i >= buildings.Length)
            {
                continue;
            }

            ref readonly var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];

            var bounds = new Rectangle(
                building.X * TileSize,
                building.Y * TileSize,
                def.FootprintWidth * TileSize,
                def.FootprintHeight * TileSize);

            if (bounds.Right < min.X || bounds.Left > max.X
                || bounds.Bottom < min.Y || bounds.Top > max.Y)
            {
                continue;
            }

            spriteBatch.Draw(_pixel, bounds, ColorFor(simulation, building, def) * Alpha);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Jakou barvou budovu obarvit. Veřejné kvůli testům — pravidlo „co je
    /// vlastně úzké hrdlo" je rozhodnutí o hře, ne kreslení, a má se dát
    /// ověřit bez okna.
    /// </summary>
    public static Color ColorFor(Simulation simulation, in BuildingInstance building, BuildingDef def)
    {
        if (building.Stall == BuildingStall.UnderConstruction)
        {
            return Building;
        }

        // Příčiny hlášené simulací mají přednost: budova, která nemá z čeho
        // vyrábět, není „ucpaná skladem", i kdyby byl sklad plný.
        switch (building.Stall)
        {
            case BuildingStall.MissingInput:
                return MissingInput;
            case BuildingStall.NoWorkers:
                return NoWorkers;
            case BuildingStall.NoTerrain:
                return NoTerrain;
            case BuildingStall.Damaged:
                return Damaged;
        }

        return HasNowhereToPut(simulation, def) ? StorageFull : Working;
    }

    /// <summary>
    /// Má budova plný sklad na všechno, co vyrábí? Pak sice pracuje, ale
    /// výsledek propadá — a to je úzké hrdlo, i když se nic „nezastavilo".
    ///
    /// <para>Stačí <b>jeden</b> volný výstup: recept doběhne celý a zbytek se
    /// uloží, takže dokud je kam dát aspoň jednu surovinu, práce má smysl.</para>
    /// </summary>
    private static bool HasNowhereToPut(Simulation simulation, BuildingDef def)
    {
        if (def.Recipe is not { } recipe || recipe.Outputs.Count == 0)
        {
            return false; // budova bez výstupu (bydlení, park) se ucpat nedá
        }

        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            int index = recipe.Outputs[i].ResourceIndex;
            double cap = simulation.GetStorageCap(index);
            if (cap <= 0 || simulation.GetResource(index) < cap * FullStorageThreshold)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Do které položky legendy budova patří. Legenda i mapa musí odpovídat
    /// témuž pravidlu — kdyby se rozešly, ukazoval by počet něco jiného než
    /// barvy na mapě.
    /// </summary>
    public static int LegendSlot(Simulation simulation, in BuildingInstance building, BuildingDef def)
    {
        var color = ColorFor(simulation, building, def);
        for (int i = 0; i < Legend.Count; i++)
        {
            if (Legend[i].Color == color)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Barvy pro legendu — UI je nesmí opisovat, jinak se rozejdou.</summary>
    public static IReadOnlyList<(string LocKey, Color Color)> Legend { get; } = new[]
    {
        ("inspector.working", Working),
        ("inspector.missingInput", MissingInput),
        ("inspector.noWorkers", NoWorkers),
        ("inspector.noTerrain", NoTerrain),
        ("inspector.storageFull", StorageFull),
        ("inspector.building", Building),
        ("inspector.damaged", Damaged),
    };
}
