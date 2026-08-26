using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení budov a ghost náhledu umisťování. Budova se kreslí spritem
/// (klíč <c>building.&lt;id&gt;</c> z <see cref="SpriteLibrary"/>); bez spritu se
/// vrátí k barevnému obdélníku z definice. Culling podle viditelného výřezu.
/// Čte jen ze simulace, nikdy do ní nezapisuje.
/// </summary>
public sealed class BuildingRenderer
{
    private const int Inset = 2;

    private readonly Texture2D _pixel;
    private readonly SoftShadow _shadow;
    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;

    public BuildingRenderer(Texture2D whitePixel, GameContent content, SpriteLibrary sprites, SoftShadow shadow)
    {
        _pixel = whitePixel;
        _shadow = shadow;
        _content = content;
        _sprites = sprites;
    }

    /// <summary>Posun animací (létající balon). Jediný stav rendereru.</summary>
    private float _time;

    /// <summary>Posune animace budov (houpající se balon nad kotvištěm).</summary>
    public void Update(float dt) => _time += dt;

    /// <summary>Vykreslí všechny viditelné budovy.</summary>
    /// <summary>
    /// Kolik sněhu leží na střechách (0 = nic). Bere se z právě běžícího
    /// období; render si nic nepamatuje, jen si to na začátku snímku přečte.
    /// </summary>
    /// <summary>Barva sněhu — nádech do modra, ne čistě bílá; čistá bílá vypadá jako díra.</summary>
    private static readonly Color SnowColor = new(232, 240, 250);

    private float _snow;

    /// <summary>
    /// Indexy budov ve výřezu. Jeden seznam na celý život rendereru: dotaz se
    /// volá jednou za snímek a nová kolekce pokaždé by byla přesně ta alokace
    /// za snímek, kterou CLAUDE.md zakazuje.
    /// </summary>
    private readonly List<int> _visible = new();

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        _snow = (float)(simulation.CurrentSeason?.SnowCover ?? 0.0);
        _snowCaps.Clear();

        // Zjednodušený režim: při oddálení jsou budovy pár pixelů velké, takže
        // sprite, stín ani odznaky nejsou k rozeznání — a přitom stojí tři kresby
        // na budovu místo jedné. Tvar města zůstane čitelný z barev.
        bool detailed = camera.Zoom >= DetailLevel.BuildingSprites;

        // Prosperita se propisuje do obrazu jen zblízka: z výšky by z nádechu
        // zbyl jeden pixel a stálo by to dva dotazy na mřížku u každé budovy.
        bool showsProsperity = detailed;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        // Ptáme se indexu, ne celého města. Při deseti tisících budovách bylo
        // projití pole od nuly do konce jediná věc, kterou bylo při oddálení
        // znát — deset tisíc dotazů na to, aby se nakreslilo dvě stě.
        const int tileSize = TerrainRenderer.TileSize;
        simulation.BuildingsIn(
            (int)Math.Floor(min.X / tileSize) - 1,
            (int)Math.Floor(min.Y / tileSize) - 1,
            (int)Math.Ceiling(max.X / tileSize) + 1,
            (int)Math.Ceiling(max.Y / tileSize) + 1,
            _visible);

        var buildings = simulation.Buildings;
        for (int slot = 0; slot < _visible.Count; slot++)
        {
            int i = _visible[slot];
            if (i >= buildings.Length)
            {
                continue; // index z novějšího stavu, než jaký renderer drží
            }

            ref readonly var building = ref buildings[i];
            if (!IsVisible(building, min, max, out var def, out var bounds))
            {
                continue;
            }

            if (detailed && !building.IsComplete)
            {
                // Staveniště (divy světa): budova je vidět jen zpola vztyčená a nad ní
                // roste pruh postupu. Bez toho by rozestavěný div vypadal jako hotový,
                // který se z neznámého důvodu fláká.
                DrawConstructionSite(spriteBatch, def, bounds, simulation.ConstructionProgress01(i));
                continue;
            }

            // Jak se tomuhle místu daří — z toho se odvodí nádech i ozdoby.
            // Prosperita je vlastnost světa; render ji jen zobrazuje.
            double prosperity = showsProsperity ? simulation.ProsperityAt(building.X, building.Y) : 1.0;
            DrawBuilding(spriteBatch, building, def, bounds, detailed, showsProsperity, prosperity, i);
        }

        // Cizí města. Jsou to tytéž instance budov, takže se kreslí týmž kódem —
        // kdyby měla vlastní, začala by se od hráčovy zástavby lišit hned po
        // první změně tady a vypadala by jako nalepená z jiné hry.
        var foreign = simulation.NpcBuildings;
        for (int i = 0; i < foreign.Length; i++)
        {
            ref readonly var building = ref foreign[i];
            if (IsVisible(building, min, max, out var def, out var bounds))
            {
                // Prosperita cizího města je věc jeho vlastníků, ne hráčova mřížka:
                // dosadí se neutrální 1.0, takže dům vypadá udržovaně, ale bez ozdob.
                DrawBuilding(spriteBatch, building, def, bounds, detailed,
                    showsProsperity: false, prosperity: 1.0, ornamentSeed: i);
            }
        }

        spriteBatch.End();
        DrawSnowPass(spriteBatch, camera);
    }

    /// <summary>Je budova ve výřezu? Vrací i její definici a obdélník ve světě.</summary>
    private bool IsVisible(
        in BuildingInstance building, Vector2 min, Vector2 max,
        out BuildingDef def, out Rectangle bounds)
    {
        const int tileSize = TerrainRenderer.TileSize;
        def = _content.Buildings[building.DefIndex];
        bounds = new Rectangle(
            building.X * tileSize, building.Y * tileSize,
            def.FootprintWidth * tileSize, def.FootprintHeight * tileSize);

        return bounds.Right >= min.X && bounds.X <= max.X
            && bounds.Bottom >= min.Y && bounds.Y <= max.Y;
    }

    /// <summary>
    /// Jedna hotová budova — sprite, stín, ozdoby, balon, odznak.
    ///
    /// <para>Jediné místo, kde se budova kreslí. Hráčova i cizí sem chodí stejnou
    /// cestou; liší se jen tím, co se do ní dosadí.</para>
    /// </summary>
    private void DrawBuilding(
        SpriteBatch spriteBatch, in BuildingInstance building, BuildingDef def, Rectangle bounds,
        bool detailed, bool showsProsperity, double prosperity, int ornamentSeed)
    {
        if (!detailed)
        {
            spriteBatch.Draw(_pixel, bounds, def.MapColor.ToXna());
            return;
        }

        // Čím se tenhle konkrétní dům liší od sousedního stejného druhu.
        // Odvozeno z polohy, takže se to mezi snímky ani po zbourání souseda nemění.
        var look = BuildingVariation.For(building.X, building.Y, building.DefIndex);
        var tint = BuildingVariation.Combine(ProsperityLook.Tint(prosperity), look.PaletteIndex);

        // Stín a ztmavení u paty. Kreslí se PŘED budovou a pro všechny stejným
        // směrem — to je celý trik, díky kterému scéna přestane být plochá.
        DrawGrounding(spriteBatch, bounds, def.FootprintWidth * def.FootprintHeight);

        // Posun o pixel rozbije dokonalé řady. Až tady, aby stín zůstal podle
        // půdorysu — kdyby se posouval s budovou, přestal by ležet na zemi.
        var body = new Rectangle(bounds.X + look.OffsetX, bounds.Y + look.OffsetY, bounds.Width, bounds.Height);

        var sprite = _sprites.Get($"building.{def.Id}");
        if (sprite is not null)
        {
            var flip = look.Mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(sprite, body, null, tint, 0f, Vector2.Zero, flip, 0f);
            if (_snow > 0.001f)
            {
                _snowCaps.Add(($"building.{def.Id}", body, flip));
            }
        }
        else
        {
            spriteBatch.Draw(_pixel, body, Color.Black * 0.6f);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(body.X + Inset, body.Y + Inset, body.Width - 2 * Inset, body.Height - 2 * Inset),
                ProsperityLook.Modulate(def.MapColor.ToXna(), tint));
        }

        DrawExtra(spriteBatch, look.Extra, body);

        if (showsProsperity)
        {
            DrawProsperityDetail(spriteBatch, ornamentSeed, prosperity, bounds);
        }

        // Balon nad kotvištěm opravdu létá: houpe se a stoupá. Statická
        // ikona balonu je jen obrázek balonu — tenhle pohyb je celý důvod,
        // proč si hráč všimne, že ta budova něco dělá.
        if (def.Scouts && def.FootprintWidth == 1)
        {
            DrawBalloon(spriteBatch, building, bounds);
        }

        // Stojící budova má být VIDĚT — a hlavně má být poznat PROČ. Jeden
        // červený roh pro všechno znamenal, že hráč viděl „něco je špatně"
        // a musel hádat; barva teď důvod rozliší a bublina ho pojmenuje.
        DrawStallBadge(spriteBatch, building.Stall, bounds);
    }

    /// <summary>Střechy k zasněžení, posbírané při hlavním průchodu.</summary>
    private readonly List<(string SpriteId, Rectangle Body, SpriteEffects Flip)> _snowCaps = new();

    /// <summary>
    /// Sníh na střechách — druhý průchod bílou siluetou.
    ///
    /// <para>Dvě slepé uličky, než tohle sedlo. Prostý bílý pruh přes horní
    /// okraj obdélníku visel ve vzduchu nad domem jako police: sprity
    /// nevyplňují celý obdélník. A obarvit sprite bíle v běžném míchání
    /// nefunguje vůbec — tint <b>násobí</b>, takže tmavě hnědá střecha krát
    /// bílá je pořád tmavě hnědá střecha.</para>
    ///
    /// <para>Ani aditivní míchání nestačilo: přičítá tutéž tmavou barvu, takže
    /// hnědá střecha jen mírně zesvětlá. Řešením je <b>bílá silueta</b> spritu
    /// (<see cref="Sprites.SpriteLibrary.Mask"/>) — má tvar střechy, ale bílé
    /// RGB, takže se dá kreslit jako sníh. Cenou je jeden batch navíc za
    /// snímek, a jen v zimě.</para>
    ///
    /// <para>Proč ne druhá, zimní sada spritů: devadesát čtyři budov krát dvě
    /// roční verze je sto osmdesát obrázků k překreslení při každé změně —
    /// a modům by zimní varianty stejně chyběly.</para>
    /// </summary>
    private void DrawSnowPass(SpriteBatch spriteBatch, Camera2D camera)
    {
        if (_snowCaps.Count == 0)
        {
            return;
        }

        // Necelá třetina výšky: sníh drží na střeše, ne na stěnách.
        const float capFraction = 0.34f;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        foreach (var (spriteId, body, flip) in _snowCaps)
        {
            var sprite = _sprites.Mask(spriteId);
            if (sprite is null)
            {
                continue;
            }

            int sourceHeight = Math.Max(1, (int)MathF.Round(sprite.Height * capFraction));
            int destHeight = Math.Max(1, (int)MathF.Round(body.Height * capFraction));

            spriteBatch.Draw(
                sprite,
                new Rectangle(body.X, body.Y, body.Width, destHeight),
                new Rectangle(0, 0, sprite.Width, sourceHeight),
                SnowColor * _snow,
                0f,
                Vector2.Zero,
                flip,
                0f);
        }

        spriteBatch.End();
        _snowCaps.Clear();
    }

    /// <summary>
    /// Posadí budovu do terénu měkkou skvrnou u paty, jedním společným směrem
    /// světla (<see cref="SceneLight"/>).
    ///
    /// <para>Dřív to byly dva plné obdélníky — kopie budovy posunutá stranou
    /// a rámeček kolem paty. Vypadalo to jako tmavé krabice čouhající z domů
    /// a u shluků se slévaly do špinavých ploch. Teď je to jedna měkká skvrna:
    /// jedno kreslení místo dvou a hlavně žádná tvrdá hrana.</para>
    /// </summary>
    private void DrawGrounding(SpriteBatch spriteBatch, Rectangle bounds, int footprintTiles)
    {
        if (!SceneLight.Enabled)
        {
            return;
        }

        _shadow.Draw(
            spriteBatch,
            SceneLight.ShadowRect(bounds, footprintTiles),
            SceneLight.ShadowColor * SceneLight.ShadowAlpha);
    }

    /// <summary>
    /// Přístavek, kterým se jeden dům liší od druhého — komín, markýza, prádlo.
    ///
    /// <para>Schválně pár pixelů a bez vazby na mechaniku. Kdyby komín něco
    /// znamenal, musel by hráč ulici číst; takhle si jen všimne, že není
    /// tapeta.</para>
    /// </summary>
    private void DrawExtra(SpriteBatch spriteBatch, BuildingExtra extra, Rectangle bounds)
    {
        switch (extra)
        {
            case BuildingExtra.Chimney:
                // Na střeše, u návětrné strany — odtud pak stoupá kouř.
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X + bounds.Width / 4, bounds.Y - 3, 2, 4),
                    new Color(92, 78, 68));
                break;

            case BuildingExtra.Awning:
                // Pruh nad vchodem u spodní hrany.
                spriteBatch.Draw(_pixel,
                    new Rectangle(bounds.X + 2, bounds.Bottom - 6, Math.Max(3, bounds.Width - 4), 2),
                    new Color(196, 82, 74));
                break;

            case BuildingExtra.Laundry:
                // Šňůra podél zdi a na ní dva hadříky.
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 1, bounds.Y + bounds.Height / 3, bounds.Width - 2, 1),
                    new Color(210, 205, 190) * 0.7f);
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 3, bounds.Y + bounds.Height / 3, 2, 3),
                    new Color(226, 226, 236));
                spriteBatch.Draw(_pixel, new Rectangle(bounds.X + bounds.Width - 6, bounds.Y + bounds.Height / 3, 2, 3),
                    new Color(150, 190, 220));
                break;
        }
    }

    /// <summary>Houpající se balon nad kotvištěm.</summary>
    private void DrawBalloon(SpriteBatch spriteBatch, in BuildingInstance building, Rectangle bounds)
    {
        float bob = MathF.Sin(_time * 1.3f + building.X * 0.7f + building.Y * 0.4f);
        int centerX = bounds.X + bounds.Width / 2;
        var balloon = new Rectangle(centerX - 5, bounds.Y - 14 + (int)(bob * 4f), 10, 12);

        spriteBatch.Draw(_pixel, new Rectangle(centerX, bounds.Y - 2 + (int)(bob * 4f), 1, 12),
            new Color(180, 170, 150)); // lano
        spriteBatch.Draw(_pixel, balloon, new Color(200, 106, 106));
        spriteBatch.Draw(_pixel, new Rectangle(balloon.X + 2, balloon.Y + 2, 6, 5), new Color(224, 140, 132));
    }

    /// <summary>Barva odznaku podle důvodu, proč budova stojí.</summary>
    public static Color StallColor(BuildingStall stall) => stall switch
    {
        BuildingStall.NoWorkers => new Color(255, 190, 70),   // oranžová = chybí lidi
        BuildingStall.MissingInput => new Color(240, 90, 80), // červená = chybí surovina
        BuildingStall.NoTerrain => new Color(150, 110, 220),  // fialová = došlo okolí
        _ => Color.Transparent,
    };

    /// <summary>
    /// Odznak v rohu budovy. Rozestavěná budova ho nedostane — u té je vidět
    /// lešení i pruh postupu, druhá cedule by jen šuměla.
    /// </summary>
    private void DrawStallBadge(SpriteBatch spriteBatch, BuildingStall stall, Rectangle bounds)
    {
        var color = StallColor(stall);
        if (color == Color.Transparent)
        {
            return;
        }

        spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 8, bounds.Y + 2, 6, 6), Color.Black * 0.5f);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 7, bounds.Y + 3, 4, 4), color);
    }

    /// <summary>
    /// Drobnost, ze které je poznat, jak se domu vede: kvetoucí dům dostane
    /// truhlík pod okny, zašlý šmouhu od kouře.
    ///
    /// <para>Schválně jen pár pixelů — je to periferní signál, ne odznak.
    /// Hráč si má všimnout, že ulice zezelenala, ne číst ikonky.</para>
    /// </summary>
    private void DrawProsperityDetail(
        SpriteBatch spriteBatch, int buildingIndex, double prosperity, Rectangle bounds)
    {
        if (ProsperityLook.HasOrnament(prosperity))
        {
            // Truhlík na parapetu: vodorovný proužek u spodní hrany.
            int boxWidth = Math.Max(2, bounds.Width / 3);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(bounds.X + bounds.Width / 2 - boxWidth / 2, bounds.Bottom - 5, boxWidth, 2),
                ProsperityLook.OrnamentColor(buildingIndex));
            return;
        }

        if (ProsperityLook.HasGrime(prosperity))
        {
            // Šmouha po zdi: svislý tmavý pruh od střechy dolů.
            spriteBatch.Draw(
                _pixel,
                new Rectangle(bounds.X + bounds.Width / 4, bounds.Y + 2, 1, Math.Max(2, bounds.Height / 2)),
                new Color(40, 38, 34) * 0.35f);
        }
    }

    /// <summary>
    /// Staveniště: budova vyrůstá zdola nahoru podle postupu, nahoře pruh
    /// s procenty. Roste zdola schválně — je to čitelné i na malém zoomu,
    /// kdy pruh splývá.
    /// </summary>
    private void DrawConstructionSite(
        SpriteBatch spriteBatch, BuildingDef def, Rectangle bounds, double progress)
    {
        int x = bounds.X;
        int y = bounds.Y;
        int width = bounds.Width;
        int height = bounds.Height;

        // Staveniště sedí v terénu stejně jako hotová budova — jinak by v jinak
        // nasvícené ulici plavalo.
        DrawGrounding(spriteBatch, bounds, def.FootprintWidth * def.FootprintHeight);

        // Základy: obrys rozestavěné budovy, ať je vidět, kolik místa zabere.
        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), new Color(60, 55, 45) * 0.45f);

        // Budova s fázemi má na každou fázi vlastní sprite — kreslí se celý,
        // ne vyříznutý zespodu. Div se tím staví jako div, ne jako dům, který
        // se vysouvá ze země.
        if (def.HasStages && _sprites.Get(def.StageSpriteAt(progress) ?? string.Empty) is { } staged)
        {
            spriteBatch.Draw(staged, bounds, Color.White);
            DrawProgressBar(spriteBatch, bounds, progress);
            return;
        }

        int risen = Math.Max(1, (int)(height * progress));
        var sprite = _sprites.Get($"building.{def.Id}");
        var partial = new Rectangle(x, y + height - risen, width, risen);
        if (sprite is not null)
        {
            // Výřez spodní části spritu — budova doslova roste ze země.
            var source = new Rectangle(
                0, sprite.Height - Math.Max(1, (int)(sprite.Height * progress)),
                sprite.Width, Math.Max(1, (int)(sprite.Height * progress)));
            spriteBatch.Draw(sprite, partial, source, Color.White * 0.85f);
        }
        else
        {
            spriteBatch.Draw(_pixel, partial, def.MapColor.ToXna() * 0.85f);
        }

        // Lešení: dvě vodorovné linky přes celý půdorys.
        var scaffold = new Color(220, 190, 120) * 0.8f;
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height / 3, width, 1), scaffold);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + 2 * height / 3, width, 1), scaffold);

        DrawProgressBar(spriteBatch, bounds, progress);
    }

    /// <summary>Pruh postupu nad staveništěm. Společný pro fázovanou i obecnou stavbu.</summary>
    private void DrawProgressBar(SpriteBatch spriteBatch, Rectangle bounds, double progress)
    {
        const int barHeight = 3;
        int barY = bounds.Y - barHeight - 2;
        spriteBatch.Draw(_pixel, new Rectangle(bounds.X, barY, bounds.Width, barHeight), Color.Black * 0.55f);
        spriteBatch.Draw(_pixel,
            new Rectangle(bounds.X, barY, Math.Max(1, (int)(bounds.Width * progress)), barHeight),
            new Color(240, 200, 90));
    }

    /// <summary>Poloprůhledný náhled budovy pod kurzorem — zelenkavý lze / červený nelze.</summary>
    public void DrawGhost(SpriteBatch spriteBatch, Camera2D camera, BuildingDef def, int tileX, int tileY, bool canPlace)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int x = tileX * tileSize;
        int y = tileY * tileSize;
        int width = def.FootprintWidth * tileSize;
        int height = def.FootprintHeight * tileSize;

        var tint = canPlace ? Color.White * 0.65f : Color.Red * 0.55f;
        var frame = canPlace ? new Color(120, 240, 140) : new Color(240, 110, 100);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        var sprite = _sprites.Get($"building.{def.Id}");
        if (sprite is not null)
        {
            spriteBatch.Draw(sprite, new Rectangle(x, y, width, height), tint);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), tint);
        }

        spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + height - 2, width, 2), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, height), frame);
        spriteBatch.Draw(_pixel, new Rectangle(x + width - 2, y, 2, height), frame);
        spriteBatch.End();
    }
}
