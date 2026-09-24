using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MightAndMagic7.Import.Lod;

/// <summary>
/// A read-only view over one original container file.
/// </summary>
/// <remarks>
/// The layout is the container OpenEnroth documents in <c>src/Library/Lod/LodSnapshots.h</c> and
/// <c>src/Library/LodFormats/LodFormats.cpp</c>: a 256-byte header, one 32-byte root directory entry,
/// then a fixed-width entry table, and two payload wrappers that are sniffed rather than declared.
///
/// The reader is deliberately strict about two things the data gets wrong: the header's version
/// field does not identify the game, and the same table name can exist in more than one archive, so
/// an entry is only ever resolved through <see cref="LodSource"/> rather than "whichever archive was
/// opened first".
/// </remarks>
public sealed class LodArchive
{
    /// <summary>Size of the container header.</summary>
    public const int HeaderSize = 256;

    /// <summary>Size of the root directory entry that follows the header.</summary>
    public const int RootEntrySize = 32;

    /// <summary>Entry record size for every version except MM8.</summary>
    public const int DefaultFileEntrySize = 32;

    /// <summary>Entry record size for MM8 archives.</summary>
    public const int Mm8FileEntrySize = 76;

    /// <summary>Size of the compressed-data wrapper.</summary>
    public const int CompressionHeaderSize = 16;

    /// <summary>Size of the image/text wrapper.</summary>
    public const int ImageHeaderSize = 48;

    /// <summary>The version value the compressed-data wrapper carries.</summary>
    public const uint CompressionVersion = 91969;

    /// <summary>The flag that marks an image wrapper as carrying text rather than an image.</summary>
    public const uint TextEntryFlag = 0x100;

    /// <summary>Size of the palette every image entry ships with.</summary>
    public const int PaletteSize = 0x300;

    private readonly byte[] _data;

    private LodArchive(string name, byte[] data)
    {
        Name = name;
        _data = data;
        (Version, VersionString, Description, FileEntrySize, Entries, DuplicateEntryNames) = Parse(name, data);
    }

    /// <summary>The archive's file name, used in every failure message.</summary>
    public string Name { get; }

    /// <summary>The version the header declares. It identifies an entry record width, not a game.</summary>
    public LodVersion Version { get; }

    /// <summary>The raw version string.</summary>
    public string VersionString { get; }

    /// <summary>The archive's description field.</summary>
    public string Description { get; }

    /// <summary>The entry record width this archive uses.</summary>
    public int FileEntrySize { get; }

    /// <summary>Every entry, ordered by name.</summary>
    public IReadOnlyList<LodEntry> Entries { get; }

    /// <summary>
    /// Names that appeared more than once. The first entry wins, matching the donor reader; the rest
    /// are recorded so a caller can report them rather than silently losing data.
    /// </summary>
    public IReadOnlyList<string> DuplicateEntryNames { get; }

    /// <summary>Opens an archive from disk.</summary>
    public static LodArchive Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new LodArchive(Path.GetFileName(path), File.ReadAllBytes(path));
    }

    /// <summary>Opens an archive from bytes already in memory.</summary>
    public static LodArchive FromBytes(string name, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new LodArchive(name, data);
    }

    /// <summary>Finds an entry by name, ignoring case, or null when the archive has none.</summary>
    public LodEntry? Find(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        foreach (LodEntry entry in Entries)
        {
            if (string.Equals(entry.Name, entryName, StringComparison.OrdinalIgnoreCase)) return entry;
        }

        return null;
    }

    /// <summary>Finds an entry by name and fails with a message naming the archive when it is absent.</summary>
    public LodEntry Require(string entryName) =>
        Find(entryName) ?? throw new LodFormatException($"{Name}: no entry named '{entryName}'.");

    /// <summary>Reads and decodes an entry's payload.</summary>
    public LodPayload Read(string entryName) => Read(Require(entryName));

    /// <summary>Reads and decodes an entry's payload.</summary>
    public LodPayload Read(LodEntry entry)
    {
        byte[] blob = Raw(entry);
        if (blob.Length >= CompressionHeaderSize &&
            BinaryPrimitives.ReadUInt32LittleEndian(blob) == CompressionVersion &&
            blob.AsSpan(4, 4).SequenceEqual("mvii"u8))
        {
            uint declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(8));
            uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(12));
            ReadOnlySpan<byte> body = blob.AsSpan(CompressionHeaderSize);
            // The original writer stored the whole record size in the payload-size field; the donor
            // works around it and so does this reader, rather than rejecting real data.
            int payloadLength = declaredSize == (uint)blob.Length
                ? body.Length
                : (int)Math.Min(declaredSize, (uint)body.Length);
            byte[] payload = body[..payloadLength].ToArray();
            if (decompressedSize == 0) return new LodPayload(entry, payload, LodPayloadKind.CompressedStored);
            return new LodPayload(entry, Inflate(entry, payload, decompressedSize), LodPayloadKind.Compressed);
        }

        if (TryReadTextEntry(blob, out uint textDataSize, out uint textDecompressedSize))
        {
            byte[] payload = blob.AsSpan(ImageHeaderSize, (int)textDataSize).ToArray();
            return textDecompressedSize == 0
                ? new LodPayload(entry, payload, LodPayloadKind.Text)
                : new LodPayload(entry, Inflate(entry, payload, textDecompressedSize), LodPayloadKind.DeflatedText);
        }

        if (TryReadImageEntry(blob, out uint imageDataSize, out uint imageDecompressedSize))
        {
            byte[] payload = blob.AsSpan(ImageHeaderSize, (int)imageDataSize).ToArray();
            byte[] palette = blob.AsSpan(blob.Length - PaletteSize, PaletteSize).ToArray();
            return new LodPayload(
                entry,
                imageDecompressedSize == 0 ? payload : Inflate(entry, payload, imageDecompressedSize),
                LodPayloadKind.Image,
                palette);
        }

        if (TryReadPaletteEntry(blob))
        {
            return new LodPayload(entry, [], LodPayloadKind.Palette, blob.AsSpan(ImageHeaderSize, PaletteSize).ToArray());
        }

        return new LodPayload(entry, blob, LodPayloadKind.Verbatim);
    }

    /// <summary>Reads an entry's raw, still-wrapped bytes.</summary>
    public byte[] Raw(LodEntry entry)
    {
        if (entry.Offset < 0 || entry.Size < 0 || entry.Offset + entry.Size > _data.LongLength)
        {
            throw new LodFormatException(
                $"{Name}: entry '{entry.Name}' runs past the end of the file (offset {entry.Offset}, size {entry.Size}, file {_data.LongLength}).");
        }

        return _data.AsSpan((int)entry.Offset, entry.Size).ToArray();
    }

    /// <summary>Whether an entry's payload is wrapped in a compressed form.</summary>
    public bool IsCompressed(LodEntry entry)
    {
        byte[] blob = Raw(entry);
        if (blob.Length >= CompressionHeaderSize &&
            BinaryPrimitives.ReadUInt32LittleEndian(blob) == CompressionVersion &&
            blob.AsSpan(4, 4).SequenceEqual("mvii"u8))
        {
            return true;
        }

        if (TryReadTextEntry(blob, out _, out uint textDecompressedSize)) return textDecompressedSize != 0;
        return TryReadImageEntry(blob, out _, out uint imageDecompressedSize) && imageDecompressedSize != 0;
    }

    private byte[] Inflate(LodEntry entry, byte[] payload, uint decompressedSize)
    {
        try
        {
            using MemoryStream input = new(payload);
            using ZLibStream stream = new(input, CompressionMode.Decompress);
            using MemoryStream output = new(checked((int)decompressedSize));
            stream.CopyTo(output);
            byte[] result = output.ToArray();
            if (result.Length != decompressedSize)
            {
                throw new LodFormatException(
                    $"{Name}: entry '{entry.Name}' declared {decompressedSize} decompressed bytes but produced {result.Length}.");
            }

            return result;
        }
        catch (InvalidDataException error)
        {
            throw new LodFormatException($"{Name}: entry '{entry.Name}' is not valid deflate data.", error);
        }
    }

    /// <summary>
    /// Recognises a text entry, exactly as the donor's <c>lod::detectCompressedPseudoImage</c> does:
    /// zero geometry, zero palette ids, the text flag, and a record that is the header plus the payload.
    /// </summary>
    /// <remarks>
    /// The palette ids sit at 0x24 and 0x26. The two fields before them are <c>widthMinus1</c> and
    /// <c>heightMinus1</c>, which the donor documents as sometimes carrying garbage, so a predicate
    /// that reads them as palette ids rejects entries that are perfectly good text.
    /// </remarks>
    private static bool TryReadTextEntry(byte[] blob, out uint dataSize, out uint decompressedSize)
    {
        dataSize = 0;
        decompressedSize = 0;
        if (blob.Length < ImageHeaderSize) return false;

        uint size = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x10));
        uint rawDataSize = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x14));
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(0x18));
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(0x1A));
        short widthLog2 = BinaryPrimitives.ReadInt16LittleEndian(blob.AsSpan(0x1C));
        short heightLog2 = BinaryPrimitives.ReadInt16LittleEndian(blob.AsSpan(0x1E));
        short paletteId = BinaryPrimitives.ReadInt16LittleEndian(blob.AsSpan(0x24));
        short alternatePaletteId = BinaryPrimitives.ReadInt16LittleEndian(blob.AsSpan(0x26));
        uint rawDecompressed = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x28));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x2C));

        bool matches = size == 0
            && rawDataSize > 0
            && width == 0
            && height == 0
            && widthLog2 == 0
            && heightLog2 == 0
            && paletteId == 0
            && alternatePaletteId == 0
            && (flags & TextEntryFlag) != 0
            && blob.Length == ImageHeaderSize + rawDataSize;
        if (!matches) return false;

        dataSize = rawDataSize;
        decompressedSize = rawDecompressed;
        return true;
    }

    /// <summary>
    /// Recognises an image entry as the donor's <c>lod::detectImage</c> does: a sized image whose pixel
    /// block follows the header and whose palette is the last 768 bytes.
    /// </summary>
    private static bool TryReadImageEntry(byte[] blob, out uint dataSize, out uint decompressedSize)
    {
        dataSize = 0;
        decompressedSize = 0;
        if (blob.Length < ImageHeaderSize + PaletteSize) return false;

        uint size = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x10));
        uint rawDataSize = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x14));
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(0x18));
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(blob.AsSpan(0x1A));
        uint rawDecompressed = BinaryPrimitives.ReadUInt32LittleEndian(blob.AsSpan(0x28));

        bool pixelsFit = rawDecompressed == 0
            ? rawDataSize >= size
            : rawDecompressed >= size;
        bool matches = size > 0
            && rawDataSize > 0
            && width > 0
            && height > 0
            && size == (uint)width * height
            && pixelsFit
            && blob.Length == ImageHeaderSize + rawDataSize + PaletteSize;
        if (!matches) return false;

        dataSize = rawDataSize;
        decompressedSize = rawDecompressed;
        return true;
    }

    /// <summary>Recognises a palette-only entry: a zeroed header followed by exactly one palette.</summary>
    private static bool TryReadPaletteEntry(byte[] blob)
    {
        if (blob.Length != ImageHeaderSize + PaletteSize) return false;
        for (int offset = 0x10; offset < ImageHeaderSize; offset++)
        {
            if (blob[offset] != 0) return false;
        }

        return true;
    }

    private static (LodVersion Version, string VersionString, string Description, int FileEntrySize, LodEntry[] Entries, string[] Duplicates) Parse(
        string name,
        byte[] data)
    {
        if (data.Length < HeaderSize + RootEntrySize)
        {
            throw new LodFormatException($"{name}: too small to be a container ({data.Length} bytes).");
        }

        if (!data.AsSpan(0, 3).SequenceEqual("LOD"u8))
        {
            throw new LodFormatException($"{name}: signature is not 'LOD'.");
        }

        string versionString = CString(data.AsSpan(4, 80));
        string description = CString(data.AsSpan(84, 80));
        LodVersion version = versionString switch
        {
            "MMVI" => LodVersion.Mm6,
            "GameMMVI" => LodVersion.Mm6Game,
            "MMVII" => LodVersion.Mm7,
            "MMVIII" => LodVersion.Mm8,
            _ => LodVersion.Unknown,
        };
        int fileEntrySize = version == LodVersion.Mm8 ? Mm8FileEntrySize : DefaultFileEntrySize;

        long rootOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize + 0x10));
        long rootSize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(HeaderSize + 0x14));
        ushort rootItemCount = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(HeaderSize + 0x1C));

        // Some re-releases ship a truncated directory size, so the region is clamped to the file.
        long directorySize = Math.Min(rootSize, data.Length - rootOffset);
        if (directorySize < (long)rootItemCount * fileEntrySize)
        {
            throw new LodFormatException(
                $"{name}: root directory holds {directorySize} bytes, too few for {rootItemCount} entries of {fileEntrySize} bytes.");
        }

        List<LodEntry> entries = [];
        List<string> duplicates = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < rootItemCount; index++)
        {
            long record = rootOffset + (index * fileEntrySize);
            string entryName = CString(data.AsSpan((int)record, 16));
            uint entryOffset;
            uint entrySize;
            if (version == LodVersion.Mm8)
            {
                entryOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)record + Mm8FileEntrySize - 12));
                entrySize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)record + Mm8FileEntrySize - 8));
            }
            else
            {
                entryOffset = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)record + 0x10));
                entrySize = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan((int)record + 0x14));
            }

            if (!seen.Add(entryName))
            {
                duplicates.Add(entryName);
                continue;
            }

            entries.Add(new LodEntry(entryName, rootOffset + entryOffset, checked((int)entrySize)));
        }

        entries.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        return (version, versionString, description, fileEntrySize, [.. entries], [.. duplicates]);
    }

    private static string CString(ReadOnlySpan<byte> raw)
    {
        int terminator = raw.IndexOf((byte)0);
        ReadOnlySpan<byte> text = terminator >= 0 ? raw[..terminator] : raw;
        return Encoding.Latin1.GetString(text);
    }
}
