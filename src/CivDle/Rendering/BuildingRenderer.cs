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

    /// <summary>Zapamatovaná řadicí funkce — viz <see cref="BySouthEdgeComparison"/>.</summary>
    private Comparison<int>? _bySouthEdgeCache;

    private Comparison<int> _bySouthEdge => _bySouthEdgeCache ??= BySouthEdgeComparison;

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
        SortBySouthEdge(buildings);

        // Odrazy jdou úplně první: leží na vodě, tedy pod vším ostatním.
        if (detailed)
        {
            for (int slot = 0; slot < _visible.Count; slot++)
            {
                int index = _visible[slot];
                if (index < buildings.Length
                    && buildings[index].IsComplete
                    && IsVisible(buildings[index], min, max, out var mirrorDef, out var mirrorBounds))
                {
                    DrawReflection(spriteBatch, simulation, buildings[index], mirrorDef, mirrorBounds);
                }
            }

            var foreignMirrors = simulation.NpcBuildings;
            for (int i = 0; i < foreignMirrors.Length; i++)
            {
                if (IsVisible(foreignMirrors[i], min, max, out var mirrorDef, out var mirrorBounds))
                {
                    DrawReflection(spriteBatch, simulation, foreignMirrors[i], mirrorDef, mirrorBounds);
                }
            }
        }

        // Stíny mají VLASTNÍ průchod, dřív než se nakreslí jediná budova.
        //
        // Dokud si stín kreslila každá budova sama těsně před sebou, padal
        // i na sousedy nakreslené dřív — v husté zástavbě si tak domy dělaly
        // tmavé fleky přes střechy a blok vypadal špinavě. Odděleným průchodem
        // leží stín vždycky na zemi. A jako vedlejší efekt vznikne v hustých
        // blocích přirozené zahuštění tmy, protože se skvrny překrývají: to
        // je zadarmo získané kontaktní ztmavení, po kterém město přestane
        // plavat nad terénem.
        if (detailed && SceneLight.Enabled)
        {
            for (int slot = 0; slot < _visible.Count; slot++)
            {
                int index = _visible[slot];
                if (index < buildings.Length
                    && buildings[index].IsComplete
                    && IsVisible(buildings[index], min, max, out var shadowDef, out var shadowBounds))
                {
                    DrawGrounding(spriteBatch, shadowBounds, shadowDef.FootprintWidth * shadowDef.FootprintHeight);
                }
            }

            var foreignShadows = simulation.NpcBuildings;
            for (int i = 0; i < foreignShadows.Length; i++)
            {
                if (IsVisible(foreignShadows[i], min, max, out var shadowDef, out var shadowBounds))
                {
                    DrawGrounding(spriteBatch, shadowBounds, shadowDef.FootprintWidth * shadowDef.FootprintHeight);
                }
            }
        }

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

    /// <summary>
    /// Odraz budovy stojící u vody.
    ///
    /// <para><b>Proč zrovna tohle:</b> břeh byl dosud čára, na které město
    /// končilo. Voda vedle přístavu nevěděla, že tam přístav je — vypadala
    /// úplně stejně jako voda uprostřed oceánu. Přitom odraz je to jediné, co
    /// hladinu spojí s tím, co nad ní stojí, a zároveň nejlevnější způsob, jak
    /// z modré plochy udělat vodu.</para>
    ///
    /// <para>Kreslí se převrácený sprite pod budovu: nižší než originál (odraz
    /// se na hladině zkracuje), průsvitný a s nádechem do modra, protože se
    /// dívá skrz vodu. A mírně se vlní — bez pohybu vypadá odraz jako druhá,
    /// vzhůru nohama postavená budova.</para>
    ///
    /// <para>Jde jen o budovy, které se vody opravdu dotýkají spodní hranou.
    /// Zkoumat celé okolí by znamenalo čtyři dotazy na dlaždici u každé
    /// budovy ve výřezu — a odraz stranou stejně není vidět, protože se
    /// hladina odráží směrem k divákovi.</para>
    /// </summary>
    private void DrawReflection(
        SpriteBatch spriteBatch, Simulation simulation, in BuildingInstance building,
        BuildingDef def, Rectangle bounds)
    {
        if (!ReflectsOnWater(simulation, building.X, building.Y, def.FootprintWidth, def.FootprintHeight)
            || _sprites.Get($"building.{def.Id}") is not { } sprite)
        {
            return;
        }

        // Vlnka. Amplituda pod dva pixely: víc a odraz se od budovy utrhne.
        int sway = (int)MathF.Round(MathF.Sin(_time * 1.7f + building.X * 0.6f) * 1.4f);
        int height = Math.Max(1, (int)(bounds.Height * ReflectionSquash));

        spriteBatch.Draw(
            sprite,
            new Rectangle(bounds.X + sway, bounds.Bottom, bounds.Width, height),
            null,
            ReflectionTint,
            0f,
            Vector2.Zero,
            SpriteEffects.FlipVertically,
            0f);
    }

    /// <summary>
    /// Odráží se tahle budova ve vodě? Tedy dotýká se <b>spodní</b> hranou
    /// vodní dlaždice?
    ///
    /// <para>Jen spodní hranou schválně. Zkoumat celé okolí by znamenalo čtyři
    /// dotazy na dlaždici u každé budovy ve výřezu, a odraz stranou stejně
    /// není vidět: hladina se odráží směrem k divákovi, tedy dolů po
    /// obrazovce.</para>
    /// </summary>
    public static bool ReflectsOnWater(Simulation simulation, int x, int y, int width, int height)
    {
        int below = y + height;
        for (int tx = x; tx < x + width; tx++)
        {
            if (simulation.IsWaterAt(tx, below))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Jak vysoký je odraz oproti budově. Na hladině se zkracuje.</summary>
    private const float ReflectionSquash = 0.62f;

    /// <summary>
    /// Nádech odrazu: průsvitný a do modra. Tint se násobí, takže tím zároveň
    /// ztmavne — a to je správně, pod hladinu je vidět hůř než nad ni.
    /// </summary>
    private static readonly Color ReflectionTint = new Color(120, 170, 210) * 0.45f;

    /// <summary>
    /// Obdélník, ve kterém budova opravdu <b>vypadá</b> — tedy i s tím, oč
    /// přerůstá svůj půdorys.
    ///
    /// <para>Vysoká budova se kreslí nahoru po obrazovce a patou zůstává na
    /// půdorysu: v pohledu shora se stavba tyčí směrem od diváka, takže je to
    /// horní hrana, která se posune. Kdyby rostla dolů, stála by v půdorysu
    /// sousedů pod sebou.</para>
    ///
    /// <para>Veřejné, protože totéž musí vědět výběr myší: hráč klikne na to,
    /// co vidí, ne na dlaždice, na kterých to stojí.</para>
    /// </summary>
    public static Rectangle VisualRect(Rectangle footprint, int visualHeight)
    {
        if (visualHeight <= 0)
        {
            return footprint;
        }

        int extra = visualHeight * TerrainRenderer.TileSize;
        return new Rectangle(footprint.X, footprint.Y - extra, footprint.Width, footprint.Height + extra);
    }

    /// <summary>
    /// Budova, jejíž <b>viditelná</b> část leží pod dlaždicí — i když ta
    /// dlaždice patří někomu jinému.
    ///
    /// <para><b>Proč to musí existovat:</b> jakmile mrakodrap přeroste svůj
    /// půdorys, zakryje dlaždice nad sebou. Klik na jeho fasádu by bez tohohle
    /// vybral to, co je za ní — tedy hráč by klikl na věž a otevřel se mu dům,
    /// který vůbec nevidí. To je ten druh chyby, kterou nikdo nenahlásí, jen
    /// mu ovládání bude připadat rozbité.</para>
    ///
    /// <para>Prohledávají se řádky <b>pod</b> dlaždicí, od nejvzdálenějšího
    /// k nejbližšímu: budova stojící jižněji se kreslí později, takže fasádou
    /// vyhrává. Smyčka je krátká a shora omezená nejvyšší výškou v datech,
    /// takže nezáleží na velikosti města.</para>
    ///
    /// <para>Nízké budovy tudy neprocházejí vůbec — ty se kryjí se svým
    /// půdorysem a najde je obyčejný dotaz na dlaždici.</para>
    /// </summary>
    public static bool TryPickTall(
        Simulation simulation, GameContent content, int tileX, int tileY, int maxVisualHeight,
        out int buildingIndex)
    {
        for (int row = tileY + maxVisualHeight; row > tileY; row--)
        {
            if (!simulation.TryGetBuildingAt(tileX, row, out int candidate))
            {
                continue;
            }

            ref readonly var building = ref simulation.Buildings[candidate];
            var def = content.Buildings[building.DefIndex];

            if (CoversTile(building.Y, def.VisualHeight, tileY))
            {
                buildingIndex = candidate;
                return true;
            }
        }

        buildingIndex = -1;
        return false;
    }

    /// <summary>
    /// Sahá budova s patou na řádku <paramref name="buildingY"/> obrazem až na
    /// řádek <paramref name="tileY"/>?
    ///
    /// <para>Budova se kreslí od <c>Y − výška</c> dolů, takže zakrývá řádky
    /// nad svým půdorysem. Řádky <b>uvnitř</b> půdorysu sem nepatří: ty najde
    /// obyčejný dotaz na dlaždici a započítat je podruhé by znamenalo, že
    /// vysoká budova přebije kohokoli, kdo na ní stojí.</para>
    ///
    /// <para>Tohle je celé pravidlo výběru vysokých budov — proto je oddělené
    /// a dá se ověřit bez světa i bez grafiky.</para>
    /// </summary>
    public static bool CoversTile(int buildingY, int visualHeight, int tileY) =>
        visualHeight > 0 && tileY < buildingY && buildingY - visualHeight <= tileY;

    /// <summary>
    /// Nejvyšší přerůstání v obsahu. Spočítá se jednou a slouží jako strop
    /// prohledávání ve <see cref="TryPickTall"/> — bez něj by se muselo hádat,
    /// jak daleko se dívat.
    /// </summary>
    public static int MaxVisualHeight(GameContent content)
    {
        int max = 0;
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            max = Math.Max(max, content.Buildings[i].VisualHeight);
        }

        return max;
    }

    /// <summary>
    /// Pořadí kreslení: od severu k jihu.
    ///
    /// <para><b>Proč teprve teď:</b> dokud každá budova zabírala právě svůj
    /// půdorys, nemohly se překrývat a na pořadí nezáleželo. Jakmile mrakodrap
    /// přeroste nahoru, musí ho zakrýt to, co stojí <i>před</i> ním, tedy
    /// jižněji. Bez toho by věž překreslila dům v popředí a dojem výšky by se
    /// převrátil.</para>
    ///
    /// <para>Řadí se podle spodní hrany, ne podle indexu: index je pořadí
    /// stavby, což se scénou nemá nic společného. Při shodě rozhodne index,
    /// aby bylo řazení stabilní a scéna se mezi snímky nepřeskupovala.</para>
    /// </summary>
    private void SortBySouthEdge(ReadOnlySpan<BuildingInstance> buildings)
    {
        // Pole se musí vejít na NEJVYŠŠÍ index z výřezu, ne jen na délku pole
        // budov. Index z novějšího stavu simulace může být větší — renderer to
        // ostatně o pár řádků níž sám ošetřuje — a klíč by se pak zapisoval za
        // konec pole.
        int needed = buildings.Length;
        for (int slot = 0; slot < _visible.Count; slot++)
        {
            needed = Math.Max(needed, _visible[slot] + 1);
        }

        if (_sortKeys.Length < needed)
        {
            // Roste po dvojnásobku, ne přesně: město přibývá po jedné budově
            // a realokace při každé stavbě by byla alokace za snímek.
            Array.Resize(ref _sortKeys, Math.Max(64, needed * 2));
        }

        // Klíče se dopočítají dopředu do pole. Řadicí funkce pak nesahá na
        // buildings — span se do lambdy zachytit nedá.
        for (int slot = 0; slot < _visible.Count; slot++)
        {
            int index = _visible[slot];
            _sortKeys[index] = index < buildings.Length
                ? buildings[index].Y + _content.Buildings[buildings[index].DefIndex].FootprintHeight
                : int.MaxValue;
        }

        _visible.Sort(_bySouthEdge);
    }

    /// <summary>Spodní hrana budovy v dlaždicích, indexovaná indexem budovy.</summary>
    private int[] _sortKeys = Array.Empty<int>();

    /// <summary>
    /// Řadicí funkce jako pole, ne lambda na místě: <see cref="List{T}.Sort(Comparison{T})"/>
    /// by si z každého volání udělal nový delegát, tedy alokaci za snímek.
    /// </summary>
    private Comparison<int> BySouthEdgeComparison => (a, b) =>
        _sortKeys[a] != _sortKeys[b] ? _sortKeys[a].CompareTo(_sortKeys[b]) : a.CompareTo(b);

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

        // Posun o pixel rozbije dokonalé řady. Až tady, aby stín zůstal podle
        // půdorysu — kdyby se posouval s budovou, přestal by ležet na zemi.
        var body = new Rectangle(bounds.X + look.OffsetX, bounds.Y + look.OffsetY, bounds.Width, bounds.Height);

        // Vysoká budova se kreslí do obdélníku, který přerůstá půdorys nahoru.
        // Stín a odznaky zůstávají u paty — ty patří na zem.
        body = VisualRect(body, def.VisualHeight);

        var sprite = _sprites.Get($"building.{def.Id}");
        if (sprite is not null)
        {
            var flip = look.Mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(sprite, body, null, tint, 0f, Vector2.Zero, flip, 0f);

            // Na pole sníh nepadá. Sněhová čepice obkresluje horní třetinu
            // siluety — u domu to obkreslí střechu, u lánu, který vyplňuje celé
            // plátno, z toho byla bílá deska přes horní třetinu pole. Pole
            // nemá střechu a zasněžená zem se stejně kreslí už v terénu.
            if (_snow > 0.001f && !_sprites.IsFlat($"building.{def.Id}"))
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

        // Proč se tu odznak „budova stojí" NEKRESLÍ: ve zralém městě stojí
        // skoro každá těžební budova (vytěžila okolí), takže odznaků byly na
        // obrazovce stovky — z upozornění se stala vyrážka. A místo pro ně
        // není: nad půdorysem kreslí střechu dům nad ním, uvnitř leží odznak
        // na kresbě. Od toho je vyhrazený překryv úzkých hrdel s legendou
        // (StallOverlayRenderer) a inspektor po kliknutí.
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
    /// <summary>
    /// Nejmenší budova, která přístavek unese.
    ///
    /// <para>Na dlaždici o šestnácti pixelech nebylo kam ho dát: markýza přes
    /// celou šířku byla červený pruh přes zeď, prádelní šňůra dvě barevné
    /// tečky uprostřed domu. Sprite tam přitom už má dveře i okna — přístavek
    /// jen přebil kresbu. Od dvou dlaždic je místa dost a přístavek dělá to,
    /// k čemu byl: odliší jeden dům od druhého.</para>
    /// </summary>
    private const int ExtraMinTiles = 2 * TerrainRenderer.TileSize;

    private void DrawExtra(SpriteBatch spriteBatch, BuildingExtra extra, Rectangle bounds)
    {
        // Komín je svislý a stojí NAD střechou, takže nepřekáží ani malému
        // domku — a je na něm zavěšený kouř. Ostatní přístavky leží na kresbě.
        if (extra == BuildingExtra.Chimney)
        {
            int stack = Math.Max(2, bounds.Width / 8);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(bounds.X + bounds.Width / 4, bounds.Y - stack * 2, stack, stack * 2),
                new Color(92, 78, 68));
            return;
        }

        if (bounds.Width < ExtraMinTiles || bounds.Height < ExtraMinTiles)
        {
            return;
        }

        switch (extra)
        {
            case BuildingExtra.Awning:
                // Pruh nad vchodem, přisazený ke spodní hraně. Dřív visel šest
                // pixelů nad ní, což je u malého domu půlka zdi.
                int awning = Math.Max(1, bounds.Height / 12);
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(
                        bounds.X + bounds.Width / 4, bounds.Bottom - awning * 2,
                        bounds.Width / 2, awning),
                    new Color(196, 82, 74));
                break;

            case BuildingExtra.Laundry:
                // Šňůra podél zdi a na ní dva hadříky, vše ve čtvrtinách šířky.
                int cloth = Math.Max(1, bounds.Width / 12);
                int lineY = bounds.Y + bounds.Height * 2 / 3;
                spriteBatch.Draw(
                    _pixel, new Rectangle(bounds.X + cloth, lineY, bounds.Width - cloth * 2, 1),
                    new Color(210, 205, 190) * 0.7f);
                spriteBatch.Draw(
                    _pixel, new Rectangle(bounds.X + bounds.Width / 4, lineY, cloth, cloth * 2),
                    new Color(226, 226, 236));
                spriteBatch.Draw(
                    _pixel, new Rectangle(bounds.X + bounds.Width * 2 / 3, lineY, cloth, cloth * 2),
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

    /// <summary>
    /// Barva přiřazená důvodu, proč budova stojí. Čte ji inspektor a popisek
    /// pod kurzorem, aby měl důvod stejnou barvu všude, kde se pojmenuje.
    /// </summary>
    public static Color StallColor(BuildingStall stall) => stall switch
    {
        BuildingStall.NoWorkers => new Color(255, 190, 70),   // oranžová = chybí lidi
        BuildingStall.MissingInput => new Color(240, 90, 80), // červená = chybí surovina
        BuildingStall.NoTerrain => new Color(150, 110, 220),  // fialová = došlo okolí
        _ => Color.Transparent,
    };

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
            // Truhlík u paty domu. Dřív visel pět pixelů nad spodní hranou
            // a byl dva pixely vysoký — na malém domě z toho byl barevný pruh
            // přes zeď. Teď je to proužek na zemi pod oknem, v poměru k budově.
            int boxWidth = Math.Max(2, bounds.Width / 3);
            int boxHeight = Math.Max(1, bounds.Height / 16);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    bounds.X + bounds.Width / 2 - boxWidth / 2, bounds.Bottom - boxHeight,
                    boxWidth, boxHeight),
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

        DrawScaffolding(spriteBatch, bounds);
        DrawCrane(spriteBatch, def, bounds, progress);
        DrawBuildDust(spriteBatch, bounds, progress);
        DrawProgressBar(spriteBatch, bounds, progress);
    }

    /// <summary>
    /// Lešení: vodorovná patra a svislé stojky.
    ///
    /// <para>Samotné dvě vodorovné čáry vypadaly jako přeškrtnutá budova.
    /// Teprve stojky z toho udělají konstrukci — mřížka je to, podle čeho oko
    /// lešení pozná.</para>
    /// </summary>
    private void DrawScaffolding(SpriteBatch spriteBatch, Rectangle bounds)
    {
        var scaffold = new Color(220, 190, 120) * 0.8f;

        spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + bounds.Height / 3, bounds.Width, 1), scaffold);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y + 2 * bounds.Height / 3, bounds.Width, 1), scaffold);

        // Stojky po krajích a uprostřed. Víc už je na šestnácti pixelech kaše.
        var post = scaffold * 0.75f;
        spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), post);
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), post);
        if (bounds.Width >= 24)
        {
            spriteBatch.Draw(_pixel, new Rectangle(bounds.X + bounds.Width / 2, bounds.Y, 1, bounds.Height), post);
        }
    }

    /// <summary>
    /// Jeřáb nad velkou stavbou. Otáčí se a houpe mu hák.
    ///
    /// <para><b>Proč to stavbě chybělo:</b> staveniště bylo statický obrázek
    /// s pruhem postupu. Pruh říká „pracuje se" číslem, ale nic se nehýbe —
    /// a hráč se dívá na obraz, ne na číslo. Otáčející se jeřáb je to jediné,
    /// co ze staveniště udělá <b>událost</b>: místo, kde se zrovna něco děje
    /// a stojí za to se na ně podívat.</para>
    ///
    /// <para>Jen u velkých staveb: nad domkem o jedné dlaždici by byl jeřáb
    /// větší než dům. A jen v rozběhnuté stavbě — na začátku se kope, na konci
    /// už jeřáb odjel.</para>
    /// </summary>
    private void DrawCrane(SpriteBatch spriteBatch, BuildingDef def, Rectangle bounds, double progress)
    {
        if (def.FootprintWidth < 2 || def.FootprintHeight < 2 || progress is < 0.15 or > 0.9)
        {
            return;
        }

        var steel = new Color(228, 176, 70);
        int mastX = bounds.X + 2;
        int mastTop = bounds.Y - bounds.Height / 2;

        // Stožár od paty stavby nad ni: jeřáb musí přerůst to, co staví.
        spriteBatch.Draw(_pixel, new Rectangle(mastX, mastTop, 2, bounds.Bottom - mastTop), steel);

        // Rameno se otáčí. Sinus se převádí na délku ramene, takže výložník
        // vypadá, jako by se točil kolem stožáru — bez jediné rotace textury.
        float phase = _time * 0.6f + bounds.X * 0.03f;
        int reach = (int)(MathF.Sin(phase) * (bounds.Width * 0.9f));
        int jibY = mastTop + 2;
        int from = Math.Min(mastX, mastX + reach);
        spriteBatch.Draw(_pixel, new Rectangle(from, jibY, Math.Abs(reach) + 2, 2), steel);

        // Lano a na něm břemeno. Houpe se pomaleji než rameno, takže zaostává —
        // to je ten detail, po kterém to vypadá, že něco opravdu visí.
        int hookX = mastX + reach;
        int hookDrop = (int)(bounds.Height * (0.35f + 0.25f * MathF.Sin(phase * 1.7f)));
        spriteBatch.Draw(_pixel, new Rectangle(hookX, jibY, 1, hookDrop), steel * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(hookX - 1, jibY + hookDrop, 3, 3), new Color(150, 118, 74));
    }

    /// <summary>
    /// Prach od paty stavby.
    ///
    /// <para>Kouř nad komínem říká „tady se pracuje" u hotových provozů; tohle
    /// je jeho protějšek pro stavbu. Obláčky jsou rozprostřené po dráze, takže
    /// v každém okamžiku jeden stoupá a jiný se rozplývá.</para>
    /// </summary>
    private void DrawBuildDust(SpriteBatch spriteBatch, Rectangle bounds, double progress)
    {
        if (progress >= 0.97)
        {
            return; // hotovo, uklizeno
        }

        var dust = new Color(206, 194, 172);
        float phase = bounds.X * 0.21f + bounds.Y * 0.13f;

        for (int p = 0; p < 3; p++)
        {
            float rise = (_time * 0.55f + p / 3f + phase * 0.05f) % 1f;
            float alpha = 0.30f * (1f - rise) * MathF.Min(1f, rise * 5f);
            if (alpha <= 0.01f)
            {
                continue;
            }

            int size = 2 + (int)(rise * 3f);
            int px = bounds.X + (int)(bounds.Width * (0.2f + 0.6f * ((phase + p) % 1f)));
            int py = bounds.Bottom - 2 - (int)(rise * bounds.Height * 0.6f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, size, size), dust * alpha);
        }
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
