using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Kde má hra začít: nejbližší místo k počátku světa, odkud je na první
/// obrazovce les (první klik), kámen a louka (dům, farma).
///
/// <para><b>Proč:</b> kamera dřív začínala na první souši od počátku — často
/// uprostřed savany nebo pláže, kde klik neudělá nic a první budova nemá kam.
/// Hráč pak v první minutě klikal do prázdna a hra na to nijak nereagovala.
/// Co musí být na dohled, říkají data (<see cref="OnboardingConfig"/>).</para>
///
/// <para>Deterministické ze seedu (terén je funkce seedu, uzly na startu ještě
/// nikdo nevytěžil), takže se nic neukládá — načtená hra najde totéž místo.
/// Počítá se jednou při startu hry; biomy se čtou líně a pamatují, ať se
/// terén nepočítá pro každého kandidáta znovu.</para>
/// </summary>
public static class StartSiteFinder
{
    /// <summary>Krok, po kterém se zkoušejí kandidáti — na „kde začít" stačí každé druhé pole.</summary>
    private const int CandidateStride = 2;

    /// <summary>Kolik vhodných dlaždic musí budova mít na dohled, v násobcích své plochy.</summary>
    private const int BuildingRoomFactor = 2;

    /// <summary>
    /// Najde místo startu. Když žádné nesplní všechno, vezme to, které splní
    /// nejvíc; když není ani to (úvod vypnutý), první souš od počátku.
    /// </summary>
    public static (int X, int Y) Find(Simulation sim)
    {
        var content = sim.ContentRef;
        var config = content.Gameplay.Onboarding;
        if (!config.HasStartSite)
        {
            return FirstLand(sim, content);
        }

        var biomes = new BiomeCache(sim, config.StartSearchRadius + config.StartRadius);
        int bestScore = -1;
        (int X, int Y) best = FirstLand(sim, content);
        int requirements = config.StartNodes.Count + config.StartBuildings.Count;

        for (int ring = 0; ring <= config.StartSearchRadius; ring += CandidateStride)
        {
            int length = ring == 0 ? 1 : ring * 8;
            for (int i = 0; i < length; i += CandidateStride)
            {
                RingTile(ring, i, out int x, out int y);
                if (!IsCampfireSpot(content, config, biomes.At(x, y)))
                {
                    continue;
                }

                int score = Score(content, config, biomes, x, y);
                if (score == requirements)
                {
                    return (x, y); // nejbližší, které splní všechno
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = (x, y);
                }
            }
        }

        return best;
    }

    /// <summary>Stojí na tom místě první budova (táborák, první dům)?</summary>
    private static bool IsCampfireSpot(GameContent content, OnboardingConfig config, int biome)
    {
        if (content.Biomes[biome].IsWater)
        {
            return false;
        }

        return config.StartBuildings.Count == 0 || content.Buildings[config.StartBuildings[0]].AllowedBiomes[biome];
    }

    /// <summary>Kolik požadavků místo splní (uzly surovin + půda pro budovy).</summary>
    private static int Score(GameContent content, OnboardingConfig config, BiomeCache biomes, int cx, int cy)
    {
        int radius = config.StartRadius;
        Span<int> nodes = stackalloc int[config.StartNodes.Count];
        Span<int> room = stackalloc int[config.StartBuildings.Count];
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                int biome = biomes.At(x, y);
                int yieldResource = content.Biomes[biome].ClickYield?.ResourceIndex ?? -1;
                for (int n = 0; n < nodes.Length; n++)
                {
                    if (config.StartNodes[n].ResourceIndex == yieldResource)
                    {
                        nodes[n]++;
                    }
                }

                for (int b = 0; b < room.Length; b++)
                {
                    if (content.Buildings[config.StartBuildings[b]].AllowedBiomes[biome])
                    {
                        room[b]++;
                    }
                }
            }
        }

        int score = 0;
        for (int n = 0; n < nodes.Length; n++)
        {
            score += nodes[n] >= config.StartNodes[n].Amount ? 1 : 0;
        }

        for (int b = 0; b < room.Length; b++)
        {
            var def = content.Buildings[config.StartBuildings[b]];
            score += room[b] >= def.FootprintWidth * def.FootprintHeight * BuildingRoomFactor ? 1 : 0;
        }

        return score;
    }

    /// <summary>První souš ve spirále od počátku — chování z doby před úvodem.</summary>
    private static (int X, int Y) FirstLand(Simulation sim, GameContent content)
    {
        for (int ring = 0; ring < 300; ring++)
        {
            int length = ring == 0 ? 1 : ring * 8;
            for (int i = 0; i < length; i++)
            {
                RingTile(ring, i, out int x, out int y);
                if (!content.Biomes[sim.BiomeAt(x, y)].IsWater)
                {
                    return (x, y);
                }
            }
        }

        return (0, 0);
    }

    /// <summary><paramref name="index"/>-tá dlaždice čtvercového kruhu kolem počátku, po obvodu.</summary>
    private static void RingTile(int ring, int index, out int x, out int y)
    {
        if (ring == 0)
        {
            x = y = 0;
            return;
        }

        int side = ring * 2;
        int offset = index % side;
        switch (index / side)
        {
            case 0: x = -ring + offset; y = -ring; break;
            case 1: x = ring; y = -ring + offset; break;
            case 2: x = ring - offset; y = ring; break;
            default: x = -ring; y = ring - offset; break;
        }
    }

    /// <summary>Biomy čtverce kolem počátku, počítané až při prvním dotazu.</summary>
    private sealed class BiomeCache
    {
        private const int Unknown = -1;

        private readonly Simulation _sim;
        private readonly int _half;
        private readonly int _side;
        private readonly int[] _biomes;

        public BiomeCache(Simulation sim, int half)
        {
            _sim = sim;
            _half = half;
            _side = half * 2 + 1;
            _biomes = new int[_side * _side];
            Array.Fill(_biomes, Unknown);
        }

        public int At(int x, int y)
        {
            int ix = x + _half, iy = y + _half;
            if ((uint)ix >= (uint)_side || (uint)iy >= (uint)_side)
            {
                return _sim.BiomeAt(x, y);
            }

            ref int slot = ref _biomes[iy * _side + ix];
            if (slot == Unknown)
            {
                slot = _sim.BiomeAt(x, y);
            }

            return slot;
        }
    }
}
