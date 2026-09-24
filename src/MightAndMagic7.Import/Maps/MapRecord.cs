using System.Buffers.Binary;
using System.Text;

namespace MightAndMagic7.Import.Maps;

/// <summary>Field accessors for the payloads' fixed-size records.</summary>
/// <remarks>
/// A record's fields sit at fixed offsets that the formats document, so a record is sliced out of the
/// payload and read at those offsets rather than field by field in walk order. A record of the wrong
/// width cannot shift the walk as a result: the walk has already advanced over the declared record
/// size before any field is read, so the two disagree loudly instead of drifting.
/// </remarks>
internal static class MapRecord
{
    /// <summary>Size of a level decoration record, in both families.</summary>
    internal const int DecorationSize = 32;

    /// <summary>Width of a decoration's name field.</summary>
    internal const int DecorationNameWidth = 32;

    /// <summary>Size of a spawn point record, in both families.</summary>
    internal const int SpawnPointSize = 24;

    private const int PlaneScale = 65536;

    internal static byte Byte(ReadOnlySpan<byte> record, int offset) => record[offset];

    internal static short Int16(ReadOnlySpan<byte> record, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(record[offset..]);

    internal static ushort UInt16(ReadOnlySpan<byte> record, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(record[offset..]);

    internal static int Int32(ReadOnlySpan<byte> record, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(record[offset..]);

    internal static uint UInt32(ReadOnlySpan<byte> record, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(record[offset..]);

    internal static string Text(ReadOnlySpan<byte> record, int offset, int width)
    {
        ReadOnlySpan<byte> raw = record.Slice(offset, width);
        int terminator = raw.IndexOf((byte)0);
        return Encoding.Latin1.GetString(terminator >= 0 ? raw[..terminator] : raw);
    }

    /// <summary>Reads a 32-bit position.</summary>
    internal static MapPoint Point(ReadOnlySpan<byte> record, int offset) =>
        new(Int32(record, offset), Int32(record, offset + 4), Int32(record, offset + 8));

    /// <summary>Reads a 16-bit position, which indoor vertices, lights and portals use.</summary>
    internal static MapPoint ShortPoint(ReadOnlySpan<byte> record, int offset) =>
        new(Int16(record, offset), Int16(record, offset + 2), Int16(record, offset + 4));

    /// <summary>Reads a box stored as three minima followed by three maxima.</summary>
    internal static MapBounds Bounds(ReadOnlySpan<byte> record, int offset) =>
        new(
            Int32(record, offset),
            Int32(record, offset + 4),
            Int32(record, offset + 8),
            Int32(record, offset + 12),
            Int32(record, offset + 16),
            Int32(record, offset + 20));

    /// <summary>Reads a box stored as three 16-bit pairs, each minimum before its maximum.</summary>
    internal static MapBounds ShortBounds(ReadOnlySpan<byte> record, int offset) =>
        new(
            Int16(record, offset),
            Int16(record, offset + 4),
            Int16(record, offset + 8),
            Int16(record, offset + 2),
            Int16(record, offset + 6),
            Int16(record, offset + 10));

    /// <summary>Reads a 16.16 fixed-point plane as unit-scale floats.</summary>
    internal static MapPlane FixedPlane(ReadOnlySpan<byte> record, int offset) =>
        new(
            Int32(record, offset) / (float)PlaneScale,
            Int32(record, offset + 4) / (float)PlaneScale,
            Int32(record, offset + 8) / (float)PlaneScale,
            Int32(record, offset + 12) / (float)PlaneScale);

    /// <summary>Reads the decoration records and pairs them with their names.</summary>
    internal static MapDecoration[] Decorations(ReadOnlySpan<byte> data, string[] names)
    {
        MapDecoration[] decorations = new MapDecoration[names.Length];
        for (int index = 0; index < names.Length; index++)
        {
            ReadOnlySpan<byte> record = data.Slice(index * DecorationSize, DecorationSize);
            decorations[index] = new MapDecoration(
                index,
                names[index],
                UInt16(record, 0x00),
                UInt16(record, 0x02),
                Point(record, 0x04),
                Int32(record, 0x10),
                UInt16(record, 0x14),
                UInt16(record, 0x16),
                UInt16(record, 0x18),
                Int16(record, 0x1C));
        }

        return decorations;
    }

    /// <summary>Derives the party's arrival points from the decorations that name one.</summary>
    internal static MapEntryPoint[] EntryPoints(IReadOnlyList<MapDecoration> decorations)
    {
        List<MapEntryPoint> entryPoints = [];
        foreach (MapDecoration decoration in decorations)
        {
            if (EntryPointNames.TryCanonical(decoration.Name, out string name))
            {
                entryPoints.Add(new MapEntryPoint(name, decoration.Position, decoration.YawAngle, decoration.Index));
            }
        }

        return [.. entryPoints];
    }

    /// <summary>Reads the spawn point records.</summary>
    internal static MapSpawnPoint[] SpawnPoints(ReadOnlySpan<byte> data, int count)
    {
        MapSpawnPoint[] spawns = new MapSpawnPoint[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> record = data.Slice(index * SpawnPointSize, SpawnPointSize);
            spawns[index] = new MapSpawnPoint(
                index,
                Point(record, 0x00),
                UInt16(record, 0x0C),
                UInt16(record, 0x0E),
                UInt16(record, 0x10),
                UInt16(record, 0x12),
                UInt32(record, 0x14));
        }

        return spawns;
    }
}
