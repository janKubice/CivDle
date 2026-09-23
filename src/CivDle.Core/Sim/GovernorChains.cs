using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Odpověď guvernérovi na otázku „dá se tahle surovina vůbec sehnat?" — tedy
/// jestli teče, nebo jestli ji umí vyrobit budova, kterou smí postavit
/// a jejíž vstupy se dají sehnat taky (rekurzivně po řetězu).
///
/// <para><b>Proč to existuje:</b> guvernér stavěl první článek řetězu, aniž by
/// věděl, jestli dojde na konec. Změřeno: chléb je v ceně jedné budovy, tak
/// postavil pekárnu → pekárna chce mouku → mlýn → mlýn chce obilí, a obilné
/// pole je za výzkumem. Výsledek: dvě mrtvé budovy a trvalé hlášení
/// „nemá čím vyrobit obilí", se kterým hráč nemohl nic udělat.</para>
///
/// <para>Výsledky se drží jen po dobu jednoho kola auto-stavby
/// (<see cref="BeginRound"/>): během kola se nic neodemyká a přítoky se
/// mění pomalu, takže jedna odpověď na surovinu stačí — a řetěz se neprochází
/// znovu pro každého kandidáta.</para>
///
/// <para>Vrstva: simulace, deterministická, bez alokací za běhu (pole se
/// alokuje jednou v konstruktoru).</para>
/// </summary>
internal sealed class GovernorChains
{
    private const byte Unknown = 0;
    private const byte Obtainable = 1;
    private const byte Unobtainable = 2;
    private const byte Checking = 3;

    private readonly GameContent _content;
    private readonly BuildingCapability[] _capabilities;
    private readonly byte[] _state;

    public GovernorChains(GameContent content, BuildingCapability[] capabilities)
    {
        _content = content;
        _capabilities = capabilities;
        _state = new byte[content.Resources.Count];
    }

    /// <summary>Zapomene odpovědi z minulého kola (mezitím se mohlo něco odemknout).</summary>
    public void BeginRound() => Array.Clear(_state);

    /// <summary>
    /// Smí guvernér tuhle budovu postavit, aby nakrmil řetězec? Kromě budov
    /// s <c>autoBuild</c> i běžné výrobny (doly, huti, dílny) — ale ne divy,
    /// podmořské stavby, bydlení ani služby: ty zůstávají hráčovou volbou.
    /// </summary>
    public bool IsAllowedForSupply(Simulation sim, int defIndex)
    {
        var def = _content.Buildings[defIndex];
        if (!def.Buildable || !sim.IsBuildingUnlocked(defIndex) || def.Recipe is null)
        {
            return false;
        }

        if (def.AutoBuild)
        {
            return true;
        }

        return (def.Category == "production" || def.Category == "industry")
            && !def.IsSubsea
            && def.BuildTicks == 0;
    }

    /// <summary>Dají se sehnat všechny vstupy budovy? (Budova bez vstupů vždycky.)</summary>
    public bool InputsObtainable(Simulation sim, int defIndex)
    {
        foreach (int input in _capabilities[defIndex].NeedsInputs)
        {
            if (!IsObtainable(sim, input))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Teče surovina, nebo ji umí vyrobit budova, kterou guvernér smí postavit
    /// a kterou má čím krmit?
    /// </summary>
    public bool IsObtainable(Simulation sim, int resource)
    {
        switch (_state[resource])
        {
            case Obtainable:
                return true;
            case Unobtainable:
            case Checking: // kruh v řetězu (A z B, B z A) — tudy cesta nevede
                return false;
        }

        if (AutoBuildSystem.IsFlowing(sim, resource))
        {
            _state[resource] = Obtainable;
            return true;
        }

        _state[resource] = Checking;
        bool obtainable = false;
        for (int defIndex = 0; defIndex < _capabilities.Length && !obtainable; defIndex++)
        {
            var capability = _capabilities[defIndex];
            if (capability.Outputs.Contains(resource)
                && !capability.NeedsInputs.Contains(resource)
                && IsAllowedForSupply(sim, defIndex))
            {
                obtainable = InputsObtainable(sim, defIndex);
            }
        }

        _state[resource] = obtainable ? Obtainable : Unobtainable;
        return obtainable;
    }

    /// <summary>
    /// Kde řetěz končí: první surovina směrem ke kořeni, kterou nikdo vyrobit
    /// nemůže. Pro hlášení — „nemá čím vyrobit obilí" řekne hráči, co
    /// vyzkoumat; „nemá čím vyrobit mouku" by ho poslalo stavět další mlýn.
    /// </summary>
    public int MissingRoot(Simulation sim, int resource)
    {
        int current = resource;
        for (int depth = 0; depth < _capabilities.Length; depth++)
        {
            int next = -1;
            for (int defIndex = 0; defIndex < _capabilities.Length && next < 0; defIndex++)
            {
                var capability = _capabilities[defIndex];
                if (!capability.Outputs.Contains(current) || !IsAllowedForSupply(sim, defIndex))
                {
                    continue;
                }

                foreach (int input in capability.NeedsInputs)
                {
                    if (input != current && !IsObtainable(sim, input))
                    {
                        next = input;
                        break;
                    }
                }
            }

            if (next < 0)
            {
                return current;
            }

            current = next;
        }

        return current;
    }
}
