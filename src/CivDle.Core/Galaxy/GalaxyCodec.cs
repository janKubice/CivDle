namespace CivDle.Core.Galaxy;

/// <summary>
/// Zápis a čtení stavu galaxie do sekce savu (svety-design.md 7.7). Snímky
/// neaktivních světů se ukládají jako celé savy ve stávajícím formátu — každý
/// svět se tak čte stejnou, léty prověřenou cestou jako dřív jediné město.
///
/// <para>Vše jménem (ID světů a surovin), nic indexem: pořadí v datech se mezi
/// verzemi mění. Vlastní číslo verze, aby sekce mohla růst bez nového formátu
/// celého savu.</para>
/// </summary>
public static class GalaxyCodec
{
    private const int Version = 1;

    /// <summary>Strop počtu záznamů — ochrana před poškozeným souborem.</summary>
    private const int MaxRecords = 64;

    private const int MaxEntries = 100_000;

    public static void Write(BinaryWriter writer, GalaxyState state)
    {
        writer.Write(Version);
        writer.Write(state.ActiveWorldId);
        writer.Write(state.ActiveEnteredAtTick);
        writer.Write(state.ActiveEnteredAtSeconds);
        writer.Write(state.LastSeenTick);
        writer.Write(state.GateOpened);

        writer.Write(state.Ship is not null);
        if (state.Ship is { } ship)
        {
            writer.Write(ship.TargetWorldId);
            writer.Write(ship.StageIndex);
            WriteAmounts(writer, ship.Invested);
        }

        writer.Write(state.Records.Count);
        foreach (var record in state.Records.Values)
        {
            writer.Write(record.WorldId);
            writer.Write(record.Seed);
            writer.Write(record.FoundedAtSeconds);
            writer.Write(record.LeftAtSeconds);
            writer.Write(record.LandingX);
            writer.Write(record.LandingY);

            writer.Write(record.Stars.Count);
            foreach (string star in record.Stars)
            {
                writer.Write(star);
            }

            WriteAmounts(writer, record.PendingDelta);

            writer.Write(record.Summary is not null);
            if (record.Summary is { } summary)
            {
                writer.Write(summary.ResourceIds.Count);
                for (int r = 0; r < summary.ResourceIds.Count; r++)
                {
                    writer.Write(summary.ResourceIds[r]);
                    writer.Write(summary.Stocks[r]);
                    writer.Write(summary.Flows[r]);
                    writer.Write(summary.Caps[r]);
                }

                writer.Write(summary.Population);
                writer.Write(summary.Housing);
                writer.Write(summary.GrowthPerSecond);
            }

            writer.Write(record.Snapshot?.Length ?? -1);
            if (record.Snapshot is { } snapshot)
            {
                writer.Write(snapshot);
            }
        }
    }

    public static GalaxyState Read(BinaryReader reader)
    {
        int version = reader.ReadInt32();
        if (version is < 1 or > Version)
        {
            throw new InvalidDataException($"Neznámá verze galaxie v savu ({version}).");
        }

        var state = new GalaxyState
        {
            ActiveWorldId = reader.ReadString(),
            ActiveEnteredAtTick = reader.ReadInt64(),
            ActiveEnteredAtSeconds = reader.ReadDouble(),
            LastSeenTick = reader.ReadInt64(),
            GateOpened = reader.ReadBoolean(),
        };

        if (reader.ReadBoolean())
        {
            var ship = new ColonyShipState(reader.ReadString()) { StageIndex = reader.ReadInt32() };
            ReadAmounts(reader, ship.Invested);
            state.Ship = ship;
        }

        int records = Count(reader, MaxRecords);
        for (int i = 0; i < records; i++)
        {
            var record = new WorldRecord(reader.ReadString(), reader.ReadInt64())
            {
                FoundedAtSeconds = reader.ReadDouble(),
                LeftAtSeconds = reader.ReadDouble(),
                LandingX = reader.ReadInt32(),
                LandingY = reader.ReadInt32(),
            };

            int stars = Count(reader, MaxEntries);
            for (int s = 0; s < stars; s++)
            {
                record.Stars.Add(reader.ReadString());
            }

            ReadAmounts(reader, record.PendingDelta);

            if (reader.ReadBoolean())
            {
                int count = Count(reader, MaxEntries);
                var ids = new string[count];
                var stocks = new double[count];
                var flows = new double[count];
                var caps = new double[count];
                for (int r = 0; r < count; r++)
                {
                    ids[r] = reader.ReadString();
                    stocks[r] = reader.ReadDouble();
                    flows[r] = reader.ReadDouble();
                    caps[r] = reader.ReadDouble();
                }

                record.Summary = new WorldSummary(
                    ids, stocks, flows, caps, reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
            }

            int snapshot = reader.ReadInt32();
            if (snapshot >= 0)
            {
                record.Snapshot = reader.ReadBytes(snapshot);
                if (record.Snapshot.Length != snapshot)
                {
                    throw new EndOfStreamException("Snímek světa v savu je useknutý.");
                }
            }

            state.Add(record);
        }

        if (!state.Records.ContainsKey(state.ActiveWorldId))
        {
            throw new InvalidDataException($"Aktivní svět '{state.ActiveWorldId}' v galaxii chybí.");
        }

        return state;
    }

    private static void WriteAmounts(BinaryWriter writer, IReadOnlyDictionary<string, double> amounts)
    {
        writer.Write(amounts.Count);
        foreach (var (id, amount) in amounts)
        {
            writer.Write(id);
            writer.Write(amount);
        }
    }

    private static void ReadAmounts(BinaryReader reader, Dictionary<string, double> into)
    {
        int count = Count(reader, MaxEntries);
        for (int i = 0; i < count; i++)
        {
            into[reader.ReadString()] = reader.ReadDouble();
        }
    }

    private static int Count(BinaryReader reader, int max)
    {
        int count = reader.ReadInt32();
        if (count < 0 || count > max)
        {
            throw new InvalidDataException($"Nesmyslný počet v galaxii ({count}).");
        }

        return count;
    }
}
