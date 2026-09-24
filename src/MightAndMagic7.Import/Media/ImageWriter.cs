using System.Buffers.Binary;
using System.IO.Compression;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// Writes a decoded unit as a PNG, using nothing but the standard library.
/// </summary>
/// <remarks>
/// <para>
/// The output is RGBA8 (colour type 6) with one filter byte per scanline, always the "none" filter.
/// RGBA rather than an indexed PNG because the units disagree about where their colours come from —
/// an embedded palette, a named palette, or per-pixel colour in a nested PCX — and one encoding keeps
/// every emitted file readable by the same consumer.
/// </para>
/// <para>
/// Deflate comes from <see cref="ZLibStream"/>, which is what a PNG IDAT chunk is. The bytes are
/// reproducible for a fixed input and runtime, which is what the extractor's determinism check relies
/// on; no timestamp or generator string is written, so two runs of the same data agree byte for byte.
/// </para>
/// </remarks>
public static class ImageWriter
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly byte[] HeaderType = "IHDR"u8.ToArray();
    private static readonly byte[] DataType = "IDAT"u8.ToArray();
    private static readonly byte[] EndType = "IEND"u8.ToArray();

    /// <summary>Writes a unit's base level as a PNG.</summary>
    public static void WritePng(DecodedImage image, Stream output)
    {
        ArgumentNullException.ThrowIfNull(image);
        WriteRgba(image.ToRgba(), image.Width, image.Height, output);
    }

    /// <summary>Writes raw RGBA8 pixels as a PNG.</summary>
    /// <param name="rgba">The pixels, four bytes per pixel, row-major.</param>
    /// <param name="width">Image width in pixels.</param>
    /// <param name="height">Image height in pixels.</param>
    /// <param name="output">The stream to write to; it is left open.</param>
    public static void WriteRgba(byte[] rgba, int width, int height, Stream output)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        ArgumentNullException.ThrowIfNull(output);
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"An image must have a positive size, not {width}x{height}.");
        if (rgba.Length != (long)width * height * 4) throw new ArgumentException($"An image of {width}x{height} needs {width * height * 4} RGBA bytes, not {rgba.Length}.", nameof(rgba));

        output.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)height);
        header[8] = 8;  // bit depth
        header[9] = 6;  // colour type: truecolour with alpha
        header[10] = 0; // compression method: deflate
        header[11] = 0; // filter method: the five standard filters
        header[12] = 0; // interlace method: none
        WriteChunk(output, HeaderType, header);

        using MemoryStream compressed = new();
        using (ZLibStream deflate = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Filter(rgba, width, height));
        }

        WriteChunk(output, DataType, compressed.ToArray());
        WriteChunk(output, EndType, []);
    }

    /// <summary>
    /// Prefixes every scanline with the "none" filter byte. Filtering exists to help compression, and
    /// the adaptive choice would make the output depend on a heuristic rather than on the pixels.
    /// </summary>
    private static byte[] Filter(byte[] rgba, int width, int height)
    {
        int stride = width * 4;
        byte[] filtered = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int target = y * (stride + 1);
            filtered[target] = 0;
            rgba.AsSpan(y * stride, stride).CopyTo(filtered.AsSpan(target + 1));
        }

        return filtered;
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(word, (uint)data.Length);
        output.Write(word);
        output.Write(type);
        output.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc32.Compute(type, data));
        output.Write(word);
    }
}
