using System.Buffers.Binary;
using System.Text;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// A read-only view over a video container's entry table.
/// </summary>
/// <remarks>
/// Only the listing is implemented. The payloads are a proprietary codec with no decoder in this
/// repository and no donor decoder either — the donor shells out to libav — and the product does not
/// reproduce the original's movies, so slicing them into files would produce artifacts nothing can
/// play. The container is still read because its entries are provenance: a manifest that silently
/// omitted two hundred movies would read as if the installation had none.
/// </remarks>
public sealed class VidArchive
{
    /// <summary>Bytes one entry record occupies.</summary>
    public const int EntrySize = 44;

    /// <summary>Bytes of the leading entry count.</summary>
    public const int CountSize = 4;

    private VidArchive(string name, long byteLength, VidEntry[] entries)
    {
        Name = name;
        ByteLength = byteLength;
        Entries = entries;
    }

    /// <summary>The container's file name.</summary>
    public string Name { get; }

    /// <summary>The container's file size in bytes.</summary>
    public long ByteLength { get; }

    /// <summary>The movies, in storage order.</summary>
    public IReadOnlyList<VidEntry> Entries { get; }

    /// <summary>Reads a video container's entry table from disk.</summary>
    public static VidArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(Path.GetFileName(path), File.ReadAllBytes(path));
    }

    /// <summary>Reads a video container's entry table from bytes already in memory.</summary>
    public static VidArchive FromBytes(string name, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(data);
        return Parse(name, data);
    }

    private static VidArchive Parse(string name, byte[] data)
    {
        if (data.Length < CountSize)
        {
            throw new MediaFormatException($"{name}: too small to be a video container ({data.Length} bytes).");
        }

        uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
        long headerSize = CountSize + ((long)count * EntrySize);
        if (headerSize > data.Length)
        {
            throw new MediaFormatException(
                $"{name}: the header declares {count} movies, which needs {headerSize} bytes but the file holds {data.Length}.");
        }

        List<VidEntry> records = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < count; index++)
        {
            int record = CountSize + (index * EntrySize);
            string entryName = CString(data.AsSpan(record, 40));
            long offset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(record + 0x28));
            if (entryName.Length == 0 || !seen.Add(entryName) || offset < headerSize || offset > data.Length) continue;
            records.Add(new VidEntry(index, entryName, offset, 0));
        }

        // There is no size field, so each entry runs to the next one's offset and the last runs to the
        // end of the file. Sorted offsets are what makes that a length rather than a guess.
        VidEntry[] ordered = [.. records.OrderBy(entry => entry.Offset)];
        VidEntry[] entries = new VidEntry[ordered.Length];
        for (int index = 0; index < ordered.Length; index++)
        {
            long end = index + 1 < ordered.Length ? ordered[index + 1].Offset : data.Length;
            entries[index] = ordered[index] with { Size = end - ordered[index].Offset };
        }

        return new VidArchive(name, data.LongLength, entries);
    }

    private static string CString(ReadOnlySpan<byte> raw)
    {
        int terminator = raw.IndexOf((byte)0);
        ReadOnlySpan<byte> text = terminator >= 0 ? raw[..terminator] : raw;
        return Encoding.Latin1.GetString(text);
    }
}
