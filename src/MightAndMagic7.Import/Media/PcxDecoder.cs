using System.Buffers.Binary;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// A PCX image nested inside a container entry, decoded far enough to become a PNG.
/// </summary>
/// <remarks>
/// The shipped nested files are 8 bits per plane with <b>three</b> planes, so they carry 24-bit colour
/// rather than palette indices; a few are single-plane with the 768-byte palette tail. Both shapes are
/// handled, and the three-plane case becomes a direct RGBA unit because quantizing it would lose the
/// image.
/// </remarks>
public static class PcxDecoder
{
    private const byte Manufacturer = 0x0A;
    private const int HeaderSize = 128;
    private const int PaletteTailSize = 1 + IndexedPalette.ByteSize;

    /// <summary>Whether the bytes begin with a PCX header.</summary>
    public static bool LooksLikePcx(ReadOnlySpan<byte> bytes) => bytes.Length >= HeaderSize && bytes[0] == Manufacturer;

    /// <summary>Decodes a PCX payload, or reports why the bytes are not one this decoder accepts.</summary>
    /// <param name="payload">The PCX file's bytes, already unwrapped.</param>
    /// <param name="fallbackPalette">Palette to use for a single-plane file that carries no tail palette.</param>
    /// <param name="image">The decoded image.</param>
    /// <param name="reason">Why the payload could not be decoded.</param>
    public static bool TryDecode(byte[] payload, IndexedPalette? fallbackPalette, out DecodedImage? image, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(payload);
        image = null;
        reason = null;

        if (!LooksLikePcx(payload))
        {
            reason = "the payload does not start with a PCX manufacturer byte";
            return false;
        }

        int encoding = payload[2];
        int bitsPerPixel = payload[3];
        int xMin = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(4));
        int yMin = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(6));
        int width = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(8)) - xMin + 1;
        int height = BinaryPrimitives.ReadInt16LittleEndian(payload.AsSpan(10)) - yMin + 1;
        int planes = payload[65];
        int bytesPerLine = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(66));

        if (encoding != 1)
        {
            reason = $"only run-length encoded PCX files are supported, and this one declares encoding {encoding}";
            return false;
        }

        if (bitsPerPixel != 8)
        {
            reason = $"only 8-bit PCX files are supported, and this one declares {bitsPerPixel} bits per plane";
            return false;
        }

        if (planes is not (1 or 3))
        {
            reason = $"only one- and three-plane PCX files are supported, and this one declares {planes} planes";
            return false;
        }

        if (width <= 0 || height <= 0 || bytesPerLine <= 0)
        {
            reason = $"the PCX header declares {width}x{height} pixels in {bytesPerLine} bytes per line";
            return false;
        }

        // One stored byte expands to at most 63 decoded bytes, so a header that asks for far more than
        // that is describing an image this file cannot hold; refusing here keeps a corrupt header from
        // asking for a huge allocation.
        long scanlineBytes = (long)height * planes * bytesPerLine;
        if (scanlineBytes > (payload.Length - HeaderSize) * 63L)
        {
            reason = $"the PCX header asks for {scanlineBytes} decoded bytes, which {payload.Length - HeaderSize} stored bytes cannot hold";
            return false;
        }

        byte[] scanlines = new byte[scanlineBytes];
        if (!TryDecodeScanlines(payload, scanlines, out string? defect))
        {
            reason = defect;
            return false;
        }

        if (planes == 3) image = DecodeThreePlane(scanlines, width, height, bytesPerLine);
        else image = DecodeSinglePlane(scanlines, width, height, bytesPerLine, payload, fallbackPalette);
        return true;
    }

    /// <summary>
    /// Expands the run-length encoded scanlines. Successive bytes may repeat a value, and a byte with
    /// its top two bits set is a run whose length is its low six bits; that is the whole of PCX RLE.
    /// </summary>
    private static bool TryDecodeScanlines(byte[] payload, byte[] scanlines, out string? defect)
    {
        defect = null;
        int at = HeaderSize;
        int written = 0;
        while (written < scanlines.Length)
        {
            if (at >= payload.Length)
            {
                defect = $"the run-length data ends after {written} of {scanlines.Length} decoded bytes";
                return false;
            }

            byte value = payload[at++];
            if ((value & 0xC0) == 0xC0)
            {
                int count = value & 0x3F;
                if (at >= payload.Length)
                {
                    defect = "the run-length data ends in the middle of a repeat count";
                    return false;
                }

                byte repeated = payload[at++];
                if (written + count > scanlines.Length)
                {
                    defect = $"a repeat run of {count} bytes overruns the {scanlines.Length} decoded bytes";
                    return false;
                }

                scanlines.AsSpan(written, count).Fill(repeated);
                written += count;
            }
            else
            {
                scanlines[written++] = value;
            }
        }

        return true;
    }

    private static DecodedImage DecodeThreePlane(byte[] scanlines, int width, int height, int bytesPerLine)
    {
        byte[] rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            int row = y * 3 * bytesPerLine;
            for (int x = 0; x < width; x++)
            {
                int at = ((y * width) + x) * 4;
                rgba[at] = scanlines[row + x];
                rgba[at + 1] = scanlines[row + bytesPerLine + x];
                rgba[at + 2] = scanlines[row + (2 * bytesPerLine) + x];
                rgba[at + 3] = 255;
            }
        }

        return DecodedImage.DirectRgba(MediaKind.Pcx, width, height, rgba);
    }

    private static DecodedImage DecodeSinglePlane(
        byte[] scanlines,
        int width,
        int height,
        int bytesPerLine,
        byte[] payload,
        IndexedPalette? fallbackPalette)
    {
        byte[] indices = new byte[width * height];
        for (int y = 0; y < height; y++) scanlines.AsSpan(y * bytesPerLine, width).CopyTo(indices.AsSpan(y * width, width));

        (IndexedPalette? palette, MediaPaletteSource source) = ReadTailPalette(payload);
        if (palette is null)
        {
            palette = fallbackPalette ?? IndexedPalette.Grey;
            source = fallbackPalette is null ? MediaPaletteSource.Unresolved : MediaPaletteSource.Embedded;
        }

        // A single-plane PCX carries indices like a flat image does, but the family it came from is
        // still PCX, so the caller names the kind rather than inheriting the bitmap one.
        return DecodedImage.Bitmap(width, height, indices, [new MipLevel(width, height, 0)], palette, source, false, 0, 0, MediaKind.Pcx);
    }

    private static (IndexedPalette? Palette, MediaPaletteSource Source) ReadTailPalette(byte[] payload)
    {
        if (payload.Length < PaletteTailSize) return (null, MediaPaletteSource.None);
        ReadOnlySpan<byte> tail = payload.AsSpan(payload.Length - PaletteTailSize);
        // The marker is what distinguishes a palette tail from the last bytes of the image data; a
        // three-plane file happens to start its tail with 0x0C too, which is why this is only
        // consulted for single-plane files.
        return tail[0] == 0x0C
            ? (IndexedPalette.FromBytes(tail[1..]), MediaPaletteSource.PcxTail)
            : (null, MediaPaletteSource.None);
    }
}
