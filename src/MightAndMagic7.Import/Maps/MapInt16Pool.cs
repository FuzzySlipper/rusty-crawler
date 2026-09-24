using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Maps;

/// <summary>One of a payload's declared 16-bit pools, consumed in order.</summary>
/// <remarks>
/// A pool holds no internal structure: only the records that come before it say how many values each
/// owner has, so walking it in that order is the only thing that keeps it aligned. Signedness is
/// applied when the values are read — the face and door pools are signed, the sector pools are
/// indices and unsigned — because a pool that guessed one for all of them would turn a negative offset
/// into a huge index.
/// </remarks>
internal sealed class MapInt16Pool
{
    private readonly int[] _values;
    private readonly string _source;
    private readonly string _name;
    private readonly int _offset;

    private int _cursor;

    internal MapInt16Pool(int[] values, string source, string name, int offset)
    {
        _values = values;
        _source = source;
        _name = name;
        _offset = offset;
    }

    /// <summary>How many values the payload declared.</summary>
    internal int Count => _values.Length;

    /// <summary>How many values the walk has consumed.</summary>
    internal int Consumed => _cursor;

    /// <summary>Takes the next values for one owner, failing when the pool cannot supply them.</summary>
    internal int[] Take(int count, string field)
    {
        if (count < 0)
        {
            throw Failure($"'{field}' asks for {count} values from the {_name} pool at 0x{_offset:X}.");
        }

        if (_cursor + count > _values.Length)
        {
            throw Failure(
                $"the {_name} pool at 0x{_offset:X} holds {_values.Length} values, but '{field}' needs {count} more after {_cursor}.");
        }

        int[] result = _values[_cursor..(_cursor + count)];
        _cursor += count;
        return result;
    }

    /// <summary>Consumes values a list owns but the decoder does not keep, for example vertex offsets.</summary>
    internal void Skip(int count, string field) => Take(count, field);

    /// <summary>Fails when the pool was not consumed exactly, which means a later list is misaligned.</summary>
    internal void ExpectConsumed(string field)
    {
        if (_cursor != _values.Length)
        {
            throw Failure(
                $"the {_name} pool at 0x{_offset:X} holds {_values.Length} values but '{field}' consumed {_cursor}, so the payload does not match the documented layout.");
        }
    }

    private LodFormatException Failure(string message) => new($"{_source}: {message}");
}
