using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>A seek-exact reader over one decoded map payload.</summary>
/// <remarks>
/// A map payload is one ordered walk: every later offset derives from an earlier count, so a field this
/// decoder does not need still has to be consumed, and consuming the wrong number of bytes silently
/// reinterprets everything after it. Every read therefore names the field it belongs to, and a failure
/// names the field and the offset it sat at rather than reporting that some stream ended.
///
/// The reader never copies a whole array to slice it: spans point into the payload, so a payload with a
/// corrupt count fails on the count instead of allocating the array the count claims.
/// </remarks>
internal sealed class MapPayloadReader
{
    private readonly byte[] _bytes;

    internal MapPayloadReader(LodPayload payload)
    {
        _bytes = payload.Bytes;
        Source = payload.Entry.Name;
    }

    /// <summary>The entry name failures are reported against.</summary>
    internal string Source { get; }

    /// <summary>How far the walk has got.</summary>
    internal int Position { get; private set; }

    /// <summary>The payload's length in bytes.</summary>
    internal int Length => _bytes.Length;

    /// <summary>How many bytes the walk has not consumed.</summary>
    internal int Remaining => _bytes.Length - Position;

    /// <summary>The offset the most recent read started at, for messages about a value already read.</summary>
    internal int LastOffset { get; private set; }

    internal byte Byte(string field) => Take(1, field)[0];

    internal ushort UInt16(string field) => BinaryPrimitives.ReadUInt16LittleEndian(Take(2, field));

    internal short Int16(string field) => BinaryPrimitives.ReadInt16LittleEndian(Take(2, field));

    internal uint UInt32(string field) => BinaryPrimitives.ReadUInt32LittleEndian(Take(4, field));

    internal int Int32(string field) => BinaryPrimitives.ReadInt32LittleEndian(Take(4, field));

    internal long Int64(string field) => BinaryPrimitives.ReadInt64LittleEndian(Take(8, field));

    internal float Single(string field) => BinaryPrimitives.ReadSingleLittleEndian(Take(4, field));

    /// <summary>Reads a fixed-width character field, which the formats pad with NUL bytes.</summary>
    internal string Text(int width, string field)
    {
        ReadOnlySpan<byte> raw = Take(width, field);
        int terminator = raw.IndexOf((byte)0);
        return Encoding.Latin1.GetString(terminator >= 0 ? raw[..terminator] : raw);
    }

    /// <summary>Reads a fixed-size byte block.</summary>
    internal byte[] Bytes(int count, string field) => Take(count, field).ToArray();

    /// <summary>Reads a fixed-size block without copying it, for records parsed in place.</summary>
    internal ReadOnlySpan<byte> Span(int count, string field) => Take(count, field);

    /// <summary>Consumes a field whose bytes this decoder does not keep.</summary>
    internal void Skip(int count, string field) => Take(count, field);

    /// <summary>Reads a count prefix and fails when that many elements could not fit in what remains.</summary>
    internal int ArrayCount(int elementSize, string field)
    {
        int offset = Position;
        uint count = UInt32(field);
        if (count > (uint)(Remaining / elementSize))
        {
            throw Failure(
                $"the count of '{field}' at 0x{offset:X} is {count}, which cannot fit in the {Remaining} bytes that remain.");
        }

        return (int)count;
    }

    /// <summary>Reads a signed byte size and converts it to an element count.</summary>
    internal int Elements(int byteSize, int elementSize, string field)
    {
        if (byteSize < 0 || byteSize % elementSize != 0)
        {
            throw Failure(
                $"'{field}' at 0x{LastOffset:X} declares {byteSize} bytes, which is not a whole number of {elementSize}-byte values.");
        }

        if (byteSize > Remaining)
        {
            throw Failure($"'{field}' at 0x{LastOffset:X} declares {byteSize} bytes but only {Remaining} remain.");
        }

        return byteSize / elementSize;
    }

    /// <summary>Validates an element count that came from a fixed-size record rather than a count prefix.</summary>
    internal int BytesOf(int count, int elementSize, string field)
    {
        if (count < 0)
        {
            throw Failure($"'{field}' at 0x{Position:X} has a negative element count ({count}).");
        }

        long bytes = (long)count * elementSize;
        if (bytes > Remaining)
        {
            throw Failure(
                $"'{field}' at 0x{Position:X} declares {count} elements of {elementSize} bytes, but only {Remaining} remain.");
        }

        return (int)bytes;
    }

    /// <summary>Reads a value and fails when it is not the one the documented layout requires.</summary>
    internal int ExpectInt32(int expected, string field)
    {
        int offset = Position;
        int actual = Int32(field);
        if (actual != expected)
        {
            throw Failure($"'{field}' at 0x{offset:X} is {actual} where the layout requires {expected}.");
        }

        return actual;
    }

    /// <summary>Fails when the walk did not consume the payload exactly.</summary>
    internal void ExpectEnd()
    {
        if (Remaining != 0)
        {
            throw Failure(
                $"the walk ended at 0x{Position:X} with {Remaining} of {Length} bytes unconsumed, so the payload does not match the documented layout.");
        }
    }

    /// <summary>Reads a count of 16-bit signed values, which the face and door pools use.</summary>
    internal int[] Int16Values(int count, string field)
    {
        ReadOnlySpan<byte> raw = Take(BytesOf(count, 2, field), field);
        int[] values = new int[count];
        for (int index = 0; index < count; index++) values[index] = BinaryPrimitives.ReadInt16LittleEndian(raw[(index * 2)..]);
        return values;
    }

    /// <summary>Reads a count of 16-bit unsigned values, which the sector pools use for indices.</summary>
    internal int[] UInt16Values(int count, string field)
    {
        ReadOnlySpan<byte> raw = Take(BytesOf(count, 2, field), field);
        int[] values = new int[count];
        for (int index = 0; index < count; index++) values[index] = BinaryPrimitives.ReadUInt16LittleEndian(raw[(index * 2)..]);
        return values;
    }

    /// <summary>Builds a failure naming the entry, so a report can say which map and which field.</summary>
    internal LodFormatException Failure(string message) => new($"{Source}: {message}");

    private ReadOnlySpan<byte> Take(int count, string field)
    {
        LastOffset = Position;
        if (count < 0)
        {
            throw Failure($"'{field}' at 0x{Position:X} has a negative size ({count}).");
        }

        if (count > Remaining)
        {
            throw Failure($"'{field}' at 0x{Position:X} needs {count} bytes but only {Remaining} remain.");
        }

        ReadOnlySpan<byte> result = _bytes.AsSpan(Position, count);
        Position += count;
        return result;
    }
}
