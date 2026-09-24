using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// Builds synthetic container files and installations, so the readers are exercised over constructed
/// bytes. No original game data is copied into the repository, and none is needed to run these tests.
/// </summary>
internal static class LodFixture
{
    internal const int HeaderSize = 256;
    internal const int RootEntrySize = 32;
    internal const int EntrySize = 32;
    internal const int CompressionHeaderSize = 16;
    internal const int ImageHeaderSize = 48;

    /// <summary>Builds one archive from named entries whose payloads are already wrapped as given.</summary>
    internal static byte[] Archive(string versionField, params (string Name, byte[] Payload)[] entries)
    {
        byte[] directory = new byte[entries.Length * EntrySize];
        int dataStart = HeaderSize + RootEntrySize + directory.Length;
        List<byte> file = [.. new byte[dataStart]];

        for (int index = 0; index < entries.Length; index++)
        {
            (string name, byte[] payload) = entries[index];
            int record = index * EntrySize;
            WriteFixed(directory, record, name, 16);
            // Entry offsets are relative to the root directory entry, not to the payload region.
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(record + 0x10), (uint)(file.Count - HeaderSize - RootEntrySize));
            BinaryPrimitives.WriteUInt32LittleEndian(directory.AsSpan(record + 0x14), (uint)payload.Length);
            file.AddRange(payload);
        }

        byte[] result = [.. file];
        Encoding.ASCII.GetBytes("LOD\0").CopyTo(result, 0);
        WriteFixed(result, 4, versionField, 80);
        WriteFixed(result, 84, "fixture", 80);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xA4), 100);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xAC), 1);
        WriteFixed(result, HeaderSize, "fixture-root", 16);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(HeaderSize + 0x10), (uint)(HeaderSize + RootEntrySize));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(HeaderSize + 0x14), (uint)directory.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(HeaderSize + 0x1C), (ushort)entries.Length);
        directory.CopyTo(result, HeaderSize + RootEntrySize);
        return result;
    }

    /// <summary>A payload stored verbatim.</summary>
    internal static byte[] Verbatim(byte[] bytes) => bytes;

    /// <summary>A payload in the compressed-data wrapper, deflated.</summary>
    internal static byte[] Compressed(byte[] bytes, bool setWriterBug = false)
    {
        byte[] deflated = Deflate(bytes);
        int declared = setWriterBug ? CompressionHeaderSize + deflated.Length : deflated.Length;
        byte[] result = new byte[CompressionHeaderSize + deflated.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 91969);
        Encoding.ASCII.GetBytes("mvii").CopyTo(result, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)declared);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), (uint)bytes.Length);
        deflated.CopyTo(result, CompressionHeaderSize);
        return result;
    }

    /// <summary>A payload in the compressed-data wrapper, stored without deflating.</summary>
    internal static byte[] CompressedStored(byte[] bytes)
    {
        byte[] result = new byte[CompressionHeaderSize + bytes.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result, 91969);
        Encoding.ASCII.GetBytes("mvii").CopyTo(result, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), 0);
        bytes.CopyTo(result, CompressionHeaderSize);
        return result;
    }

    /// <summary>A payload in the image wrapper, flagged as text and deflated.</summary>
    internal static byte[] DeflatedText(byte[] bytes)
    {
        byte[] deflated = Deflate(bytes);
        byte[] result = new byte[ImageHeaderSize + deflated.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x14), (uint)deflated.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x28), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x2C), 0x100);
        deflated.CopyTo(result, ImageHeaderSize);
        return result;
    }

    /// <summary>An image wrapper: a sized image whose pixel block is followed by its palette.</summary>
    internal static byte[] Image(byte[] pixels, ushort width = 4, ushort height = 4, bool deflated = false)
    {
        byte[] payload = deflated ? Deflate(pixels) : pixels;
        byte[] result = new byte[ImageHeaderSize + payload.Length + 0x300];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x10), (uint)(width * height));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x14), (uint)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x18), width);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x1A), height);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x28), deflated ? (uint)pixels.Length : 0);
        payload.CopyTo(result, ImageHeaderSize);
        for (int index = 0; index < 0x300; index++) result[ImageHeaderSize + payload.Length + index] = (byte)index;
        return result;
    }

    /// <summary>A palette-only wrapper.</summary>
    internal static byte[] Palette()
    {
        byte[] result = new byte[ImageHeaderSize + 0x300];
        for (int index = 0; index < 0x300; index++) result[ImageHeaderSize + index] = (byte)(255 - index);
        return result;
    }

    /// <summary>Writes an installation directory holding one rules archive.</summary>
    internal static string Installation(string versionField, params (string Name, byte[] Payload)[] entries)
    {
        string root = Path.Combine(Path.GetTempPath(), $"mm7-fixture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        File.WriteAllBytes(Path.Combine(root, "DATA", "Events.lod"), Archive(versionField, entries));
        return root;
    }

    /// <summary>Writes a text table into a fixture installation.</summary>
    internal static (string Name, byte[] Payload) TextTable(string name, string text) =>
        (name, DeflatedText(Encoding.Latin1.GetBytes(text)));

    private static byte[] Deflate(byte[] bytes)
    {
        using MemoryStream output = new();
        using (ZLibStream stream = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(bytes);
        }

        return output.ToArray();
    }

    private static void WriteFixed(byte[] target, int offset, string value, int width)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        int length = Math.Min(bytes.Length, width - 1);
        bytes.AsSpan(0, length).CopyTo(target.AsSpan(offset, length));
    }
}
