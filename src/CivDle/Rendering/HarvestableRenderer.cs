using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Kreslí těžitelné objekty přímo na terén (strom na lese, kámen v horách —
/// bod „na podlaze chci vidět ty věci"). Každý klik strom viditelně zmenší;
/// po pár klicích spadne s velkým efektem (anticipace + payoff, game-feel-wow.md)
/// a chvíli je z něj pařez, než doroste. Chop stav je čistě vizuální (řídká mapa
/// naklikaných dlaždic) — simulace zůstává nekonečná a bezstavová.
/// </summary>
public sealed class HarvestableRenderer
{
    private const int ClicksToFell = 4;
    private const float FallSeconds = 0.5f;
    private const float RegrowSeconds = 6f;

    private sealed class ChopState
    {
        public int Clicks;
        public float FallTimer;   // > 0 = právě padá
        public float RegrowTimer; // > 0 = pařez dorůstá
    }

    private readonly SpriteLibrary _sprites;
    private readonly GameContent _content;
    private readonly Texture2D? _shadow;
    private readonly Dictionary<long, ChopState> _chops = new();
    private readonly List<long> _finished = new();

    /// <summary>
    /// Pro každý biom sada spritů těžitelného uzlu, nebo prázdné pole.
    ///
    /// <para><b>Proč sada a ne jeden:</b> těžitelný uzel stojí na <b>každé</b>
    /// dlaždici svého biomu. Jeden sprite pro všechny tak znamenal, že les byl
    /// mřížka jednoho a téhož stromu — tapeta, ne porost. A protože týž sprite
    /// dostala tajga i džungle, vypadaly všechny lesy světa stejně.</para>
    ///
    /// <para>Z několika tvarů na biom vznikne porost už jen tím, že se střídají;
    /// spolu se zrcadlením a odstínem (viz <c>DrawNode</c>) se opakování
    /// ztratí.</para>
    /// </summary>
    private readonly string[][] _nodeSpritesByBiome;

    public HarvestableRenderer(SpriteLibrary sprites, GameContent content)
    {
        _sprites = sprites;
        _content = content;
        _shadow = sprites.Get("fx.shadow");

        _nodeSpritesByBiome = new string[content.Biomes.Count][];
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            _nodeSpritesByBiome[i] = Array.Empty<string>();
            var yield = content.Biomes[i].ClickYield;
            if (yield is null)
            {
                continue;
            }

            _nodeSpritesByBiome[i] = SpritesFor(content.Biomes[i].Id, content.Resources[yield.ResourceIndex].Id);
        }
    }

    /// <summary>
    /// Které tvary rostou v daném biomu.
    ///
    /// <para>Suroviny rozhodují o <i>druhu</i> uzlu (dřevo/kámen), biom o jeho
    /// <i>podobě</i>: v tajze jehličnany, v džungli tropické stromy, v mangrove
    /// chůdové kořeny. Dřív dostaly všechny dřevo dávající biomy tentýž strom,
    /// takže tajga vypadala jako listnatý les.</para>
    /// </summary>
    private static string[] SpritesFor(string biomeId, string resourceId)
    {
        if (resourceId == "stone")
        {
            return new[] { "node.rock" };
        }

        if (resourceId != "wood")
        {
            return new[] { "node.tree" };
        }

        return biomeId switch
        {
            "taiga" => new[] { "deco.conifer", "node.tree", "deco.conifer" },
            "highlands" => new[] { "deco.conifer", "node.tree" },
            "jungle" => new[] { "deco.jungle_tree", "node.tree", "deco.jungle_tree" },
            "mangrove" => new[] { "deco.mangrove", "deco.jungle_tree" },
            "swamp" => new[] { "deco.mangrove", "node.tree" },
            _ => new[] { "node.tree", "deco.broadleaf", "node.tree" },
        };
    }

    /// <summary>Zaznamená klik na těžitelnou dlaždici. Vrací true, když strom právě spadl (payoff).</summary>
    public bool RegisterChop(int tileX, int tileY)
    {
        long key = CivDle.Core.World.TileKey.Pack(tileX, tileY);
        if (!_chops.TryGetValue(key, out var state))
        {
            state = new ChopState();
            _chops[key] = state;
        }

        if (state.RegrowTimer > 0f)
        {
            return false; // pařez ještě dorůstá — klik se nepočítá do kácení
        }

        state.Clicks++;
        if (state.Clicks >= ClicksToFell)
        {
            state.FallTimer = FallSeconds;
            return true;
        }

        return false;
    }

    /// <summary>Čas pro kolébání stromů ve větru.</summary>
    private float _time;

    public void Update(float dt)
    {
        _time += dt;
        _finished.Clear();
        foreach (var (key, state) in _chops)
        {
            if (state.FallTimer > 0f)
            {
                state.FallTimer -= dt;
                if (state.FallTimer <= 0f)
                {
                    state.RegrowTimer = RegrowSeconds; // spadl → pařez
                }
            }
            else if (state.RegrowTimer > 0f)
            {
                state.RegrowTimer -= dt;
                if (state.RegrowTimer <= 0f)
                {
                    _finished.Add(key); // dorostl → zpět do plného stavu (zapomenout)
                }
            }
        }

        foreach (long key in _finished)
        {
            _chops.Remove(key);
        }
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < DetailLevel.Harvestables)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        int startX = (int)MathF.Floor(min.X / tileSize);
        int startY = (int)MathF.Floor(min.Y / tileSize);
        int endX = (int)MathF.Ceiling(max.X / tileSize);
        int endY = (int)MathF.Ceiling(max.Y / tileSize);
        if (!DetailLevel.FitsBudget(startX, startY, endX, endY))
        {
            return;
        }

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                var shapes = _nodeSpritesByBiome[simulation.BiomeAt(x, y)];
                string? spriteKey = shapes.Length > 0
                    ? shapes[(int)(Hash(x, y) % (ulong)shapes.Length)]
                    : null;
                if (spriteKey is null && simulation.TryGetPlantedNode(x, y, out int plantedResource))
                {
                    spriteKey = ResourceSprite(plantedResource); // zasazený háj se kreslí i těží jako přírodní
                }

                // Na zastavěné dlaždici nic neroste — ani na cizí, ani pod silnicí.
                // Dřív se strom kreslil dál a dům pak stál „ve stromě"; hráč to
                // hlásil jako první věc, která na cizích městech bije do očí.
                if (spriteKey is null
                    || simulation.IsOccupied(x, y) || simulation.IsNpcOccupied(x, y)
                    || simulation.HasRoadAt(x, y))
                {
                    continue;
                }

                // Vytěžená dlaždice je prázdná, dokud nedoroste — a čím míň
                // v uzlu zbývá, tím je menší. Hráč tak vidí, kde už bral,
                // a nemusí to zkoušet naslepo.
                int charges = simulation.NodeChargesLeft(x, y);
                if (charges == 0)
                {
                    continue;
                }

                int capacity = simulation.NodeMaxCharges(x, y);
                float fullness = capacity > 0 ? Math.Clamp(charges / (float)capacity, 0.45f, 1f) : 1f;
                DrawNode(spriteBatch, x, y, spriteKey, fullness);
            }
        }

        spriteBatch.End();
    }

    private void DrawNode(SpriteBatch spriteBatch, int tileX, int tileY, string spriteKey, float fullness)
    {
        const int tileSize = TerrainRenderer.TileSize;
        // Jemný jitter velikosti/pozice per dlaždice, ať to není mřížka.
        ulong h = Hash(tileX, tileY);
        float sizeJitter = (0.82f + (h & 0xFF) / 255f * 0.36f) * fullness;
        int drawSize = (int)(tileSize * sizeJitter) + 2;
        int offsetX = (int)((h >> 8) % 5) - 2;

        float baseX = tileX * tileSize + tileSize * 0.5f + offsetX;
        float baseY = tileY * tileSize + tileSize - 1;

        _chops.TryGetValue(CivDle.Core.World.TileKey.Pack(tileX, tileY), out var state);

        var texture = _sprites.Get(spriteKey);
        if (texture is null)
        {
            return;
        }

        bool isTree = spriteKey == "node.tree";
        var origin = new Vector2(texture.Width * 0.5f, texture.Height); // kotva dole uprostřed

        // Měkký kontaktní stín u paty, ať objekt „sedí" na terénu, ne se vznáší.
        if (_shadow is not null)
        {
            var shadowScale = new Vector2(drawSize * 0.85f / _shadow.Width, drawSize * 0.32f / _shadow.Height);
            spriteBatch.Draw(_shadow, new Vector2(baseX, baseY - 1f), null, Color.White * 0.7f, 0f,
                new Vector2(_shadow.Width * 0.5f, _shadow.Height * 0.5f), shadowScale, SpriteEffects.None, 0f);
        }

        if (state is { RegrowTimer: > 0f })
        {
            // Zbytek po sběru (pařez u stromu, suť u kamene), který postupně dorůstá.
            float grow = 1f - state.RegrowTimer / RegrowSeconds;
            var remnant = _sprites.Get(isTree ? "node.stump" : "node.rubble") ?? texture;
            spriteBatch.Draw(remnant, new Vector2(baseX, baseY), null, Color.White, 0f,
                new Vector2(remnant.Width * 0.5f, remnant.Height), (float)drawSize / remnant.Width * 0.8f, SpriteEffects.None, 0f);
            if (grow > 0.4f)
            {
                float scale = (float)drawSize / texture.Width * MathF.Max(0.3f, grow);
                spriteBatch.Draw(texture, new Vector2(baseX, baseY), null, Color.White * (grow - 0.4f), 0f, origin, scale, SpriteEffects.None, 0f);
            }

            return;
        }

        float shrink = state is null ? 1f : 1f - state.Clicks / (float)ClicksToFell * 0.45f;

        // Stromy se ve větru sotva znatelně kolébají. Je to pár setin radiánu,
        // ale je to jediný pohyb, který v klidné scéně zbývá — bez něj přestane
        // mozek číst obraz jako místo a začne ho číst jako obrázek. Kámen se
        // nekolébá, ten by ve větru vypadal jako guma.
        float rotation = isTree ? AmbientWind.Sway(tileX, tileY, _time) : 0f;
        if (state is { FallTimer: > 0f })
        {
            float t = 1f - state.FallTimer / FallSeconds;
            shrink = 1f - 0.45f;
            // Strom se kácí (rotace k zemi); kámen se jen drolí (bez rotace).
            rotation = isTree ? -t * 1.4f : 0f;
        }

        float finalScale = (float)drawSize / texture.Width * shrink;

        // Zrcadlení a odstín. Uzel stojí na KAŽDÉ dlaždici svého biomu, takže
        // se tvary opakují sebevíc jich je — zrcadlením se počet siluet zdarma
        // zdvojnásobí a drobná odchylka jasu rozbije i ten zbytek. Bez toho je
        // les mřížka, ne porost.
        var flip = (h & 0x10000) == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
        var tint = NodeTint(h);

        spriteBatch.Draw(texture, new Vector2(baseX, baseY), null, tint, rotation, origin, finalScale, flip, 0f);
    }

    /// <summary>
    /// Drobná odchylka jasu a teploty jednoho uzlu.
    ///
    /// <para>Rozsah je schválně malý: víc a z lesa je flekatý koberec. Jde
    /// o to, aby dva sousední stromy nebyly <b>doslova</b> týž pixel — oko
    /// shodu pozná okamžitě, i když rozdíl by nepojmenovalo.</para>
    /// </summary>
    private static Color NodeTint(ulong hash)
    {
        float shade = 0.86f + ((hash >> 17) & 0xFF) / 255f * 0.22f;   // 0,86–1,08
        float warm = 0.96f + ((hash >> 25) & 0xFF) / 255f * 0.08f;    // trocha do žluta
        return new Color(
            Math.Clamp(shade * warm, 0f, 1f),
            Math.Clamp(shade, 0f, 1f),
            Math.Clamp(shade * (1.96f - warm), 0f, 1f));
    }

    /// <summary>Sprite pro zasazený uzel podle suroviny (dřevo → strom, kámen → skála).</summary>
    private string ResourceSprite(int resourceIndex) => _content.Resources[resourceIndex].Id switch
    {
        "wood" => "node.tree",
        "stone" => "node.rock",
        _ => "node.tree",
    };

    private static ulong Hash(int x, int y)
    {
        ulong h = (uint)x * 0x9E3779B97F4A7C15UL ^ (uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        return h ^ (h >> 31);
    }
}
