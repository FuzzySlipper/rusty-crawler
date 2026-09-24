using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// A read-only view over a sound bank: a count, a fixed-width entry table, then the payloads.
/// </summary>
/// <remarks>
/// The bank is not a LOD archive, so the container reader does not apply. Each payload is preceded by a
/// word holding its stored size, and the entry's own offset points <i>past</i> that word; the reader
/// follows the offset the way the shipped data and the donor both do rather than deriving it.
/// </remarks>
public sealed class SndArchive
{
    /// <summary>Bytes one entry record occupies.</summary>
    public const int EntrySize = 52;

    /// <summary>Bytes of the leading entry count.</summary>
    public const int CountSize = 4;

    private readonly byte[] _data;

    private SndArchive(string name, byte[] data, SndEntry[] entries)
    {
        Name = name;
        _data = data;
        Entries = entries;
    }

    /// <summary>The bank's file name.</summary>
    public string Name { get; }

    /// <summary>The bank's file size in bytes.</summary>
    public long ByteLength => _data.LongLength;

    /// <summary>The bank's samples, in storage order.</summary>
    public IReadOnlyList<SndEntry> Entries { get; }

    /// <summary>Opens a sound bank from disk.</summary>
    public static SndArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(Path.GetFileName(path), File.ReadAllBytes(path));
    }

    /// <summary>Opens a sound bank from bytes already in memory.</summary>
    public static SndArchive FromBytes(string name, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(data);
        return Parse(name, data);
    }

    /// <summary>The stored payload bytes of one sample, before any inflating.</summary>
    public byte[] StoredBytes(SndEntry entry)
    {
        if (entry.Offset < 0 || entry.StoredSize < 0 || entry.Offset + entry.StoredSize > _data.LongLength)
        {
            throw new MediaFormatException($"{Name}: '{entry.Name}' runs past the end of the file");
        }

        return _data.AsSpan((int)entry.Offset, entry.StoredSize).ToArray();
    }

    /// <summary>
    /// Reads one sample's payload, inflating it when the bank says it was deflated.
    /// </summary>
    /// <param name="entry">The sample to read.</param>
    /// <param name="payload">The decoded payload, which for the shipped bank is a whole RIFF file.</param>
    /// <param name="rawDeflateFallback">True when the fallback decoder, not zlib, produced the payload.</param>
    /// <param name="reason">Why the sample could not be read.</param>
    public bool TryRead(SndEntry entry, out byte[] payload, out bool rawDeflateFallback, out string? reason)
    {
        payload = [];
        rawDeflateFallback = false;
        reason = null;

        if (entry.Offset < 0 || entry.StoredSize < 0 || entry.Offset + entry.StoredSize > _data.LongLength)
        {
            reason = $"{Name}: '{entry.Name}' runs past the end of the file";
            return false;
        }

        ReadOnlySpan<byte> stored = _data.AsSpan((int)entry.Offset, entry.StoredSize);
        // The bank writes the stored size into the inflated-size field for samples it stored as-is, so
        // "different from the stored size" is the test, not "non-zero".
        if (entry.DecompressedSize == 0 || entry.DecompressedSize == entry.StoredSize)
        {
            payload = stored.ToArray();
            return true;
        }

        if (TryInflate(stored, entry.DecompressedSize, zlib: true, out payload)) return true;

        // One sample in the shipped bank fails its checksum. The donor retries as a bare deflate stream,
        // which skips the header and the Adler check, and warns when that fails too; so does this
        // reader, because the alternative is discarding a sample that may still decode.
        if (stored.Length > 6 && TryInflate(stored[2..^4], entry.DecompressedSize, zlib: false, out payload))
        {
            rawDeflateFallback = true;
            return true;
        }

        payload = [];
        reason = $"{Name}: '{entry.Name}' is not readable deflate data";
        return false;
    }

    private static SndArchive Parse(string name, byte[] data)
    {
        if (data.Length < CountSize)
        {
            throw new MediaFormatException($"{name}: too small to be a sound bank ({data.Length} bytes).");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
        long tableEnd = CountSize + ((long)count * EntrySize);
        if (tableEnd > data.Length)
        {
            throw new MediaFormatException(
                $"{name}: the header declares {count} samples, which needs {tableEnd} bytes but the file holds {data.Length}.");
        }

        List<SndEntry> entries = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < count; index++)
        {
            int record = CountSize + (index * EntrySize);
            string entryName = CString(data.AsSpan(record, 40));
            long offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(record + 0x28));
            int storedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(record + 0x2C));
            int decompressedSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(record + 0x30));

            // A bank with two samples of the same name, or a sample that runs past the end of the file,
            // is corrupt in a way that would make a later lookup silently pick the wrong bytes, so such
            // entries are left out rather than resolved to whatever came first.
            if (entryName.Length == 0 || !seen.Add(entryName) || offset < tableEnd || offset + storedSize > data.Length)
            {
                continue;
            }

            entries.Add(new SndEntry(index, entryName, offset, storedSize, decompressedSize));
        }

        return new SndArchive(name, data, [.. entries]);
    }

    private static bool TryInflate(ReadOnlySpan<byte> stored, int decompressedSize, bool zlib, out byte[] payload)
    {
        payload = [];
        try
        {
            using MemoryStream input = new(stored.ToArray());
            using Stream stream = zlib
                ? new ZLibStream(input, CompressionMode.Decompress)
                : new DeflateStream(input, CompressionMode.Decompress);
            using MemoryStream output = new(decompressedSize);
            stream.CopyTo(output);
            byte[] result = output.ToArray();
            // A short read means the stream ended early, which is a corrupt sample even though the
            // inflater did not complain.
            if (result.Length != decompressedSize) return false;

            payload = result;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static string CString(ReadOnlySpan<byte> raw)
    {
        int terminator = raw.IndexOf((byte)0);
        ReadOnlySpan<byte> text = terminator >= 0 ? raw[..terminator] : raw;
        return Encoding.Latin1.GetString(text);
    }
}
