using System.Buffers.Binary;
using System.IO.Compression;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// Turns container entry bytes into decoded media units.
/// </summary>
/// <remarks>
/// <para>
/// The wrapper families are sniffed in the order the format spec fixes: the compressed-data wrapper,
/// then the 48-byte image wrapper (which includes palette-only and non-image entries), then the sprite
/// wrapper, then a payload that has to be sniffed again (a nested PCX or a font). Every predicate here
/// was checked against the operator's installation.
/// </para>
/// <para>
/// Decoding takes the entry's <b>raw</b> bytes rather than an already-unwrapped
/// <see cref="LodPayload"/>, because unwrapping an image entry consumes the 48-byte header and with it
/// the geometry: a <see cref="LodPayload"/> for an image carries the inflated pixel block and the
/// palette but no width, height, flags, or palette id. The payload overload therefore serves the
/// families whose bytes describe themselves and reports the image family as needing the raw entry.
/// </para>
/// </remarks>
public static class MediaDecoder
{
    /// <summary>Size of the image wrapper's header.</summary>
    public const int ImageHeaderSize = 48;

    /// <summary>Size of the sprite wrapper's header, before its line table.</summary>
    public const int SpriteHeaderSize = 32;

    /// <summary>Bytes of palette that follow an image or palette wrapper.</summary>
    public const int PaletteSize = IndexedPalette.ByteSize;

    /// <summary>The flag that says an image carries a mip chain.</summary>
    public const uint MipmapFlag = 0x0002;

    /// <summary>The flag that says a wrapper holds something other than an image.</summary>
    public const uint NotAnImageFlag = 0x0100;

    /// <summary>The flag that says palette index 0 is transparent.</summary>
    public const uint ZeroIsTransparentFlag = 0x0200;

    /// <summary>How deeply nested wrappers are unwrapped before the bytes are called undecodable.</summary>
    private const int MaxNesting = 4;

    /// <summary>Decodes one container entry from its raw bytes, failing when it is not media.</summary>
    /// <param name="entryBytes">The entry exactly as the container stores it, wrappers included.</param>
    /// <param name="namedPalettes">
    /// Resolves a sprite's palette id to the <c>pal%03d</c> entry it names. Omit when the caller has no
    /// installation at hand; sprites then decode to an identity grey ramp rather than to no image.
    /// </param>
    public static DecodedImage DecodeImage(byte[] entryBytes, Func<ushort, IndexedPalette?>? namedPalettes = null) =>
        TryDecode(entryBytes, namedPalettes, out DecodedImage? image, out string? reason)
            ? image!
            : throw new MediaFormatException(reason!);

    /// <summary>Decodes an already-unwrapped payload, failing when its family needs the raw entry.</summary>
    /// <param name="payload">The payload the container reader produced.</param>
    /// <param name="namedPalettes">Resolves a sprite's palette id, as in the raw-entry overload.</param>
    public static DecodedImage DecodeImage(LodPayload payload, Func<ushort, IndexedPalette?>? namedPalettes = null) =>
        TryDecode(payload, namedPalettes, out DecodedImage? image, out string? reason)
            ? image!
            : throw new MediaFormatException(reason!);

    /// <summary>Decodes one container entry from its raw bytes.</summary>
    /// <param name="entryBytes">The entry exactly as the container stores it, wrappers included.</param>
    /// <param name="namedPalettes">Resolves a sprite's palette id, or null when there is no installation.</param>
    /// <param name="image">The decoded unit, or null when the bytes are not decodable media.</param>
    /// <param name="reason">Why the bytes are not decodable media, when they are not.</param>
    public static bool TryDecode(
        byte[] entryBytes,
        Func<ushort, IndexedPalette?>? namedPalettes,
        out DecodedImage? image,
        out string? reason)
    {
        ArgumentNullException.ThrowIfNull(entryBytes);
        return TryDecodeNested(entryBytes, namedPalettes, depth: 0, out image, out reason);
    }

    /// <summary>Decodes an already-unwrapped payload.</summary>
    /// <param name="payload">The payload the container reader produced.</param>
    /// <param name="namedPalettes">Resolves a sprite's palette id, or null when there is no installation.</param>
    /// <param name="image">The decoded unit, or null when the payload is not decodable media.</param>
    /// <param name="reason">Why the payload is not decodable media, when it is not.</param>
    public static bool TryDecode(
        LodPayload payload,
        Func<ushort, IndexedPalette?>? namedPalettes,
        out DecodedImage? image,
        out string? reason)
    {
        switch (payload.Kind)
        {
            case LodPayloadKind.Palette:
                image = DecodedImage.PaletteStrip(IndexedPalette.FromBytes(payload.Palette!));
                reason = null;
                return true;
            case LodPayloadKind.Image:
                // The container reader returns the inflated pixel block for an image, so the header
                // that said how wide it is has already been consumed.
                image = null;
                reason = "an image payload has already been unwrapped, so its 48-byte header and with it the geometry are gone; decode the raw entry instead";
                return false;
            default:
                return TryDecodeNested(payload.Bytes, namedPalettes, depth: 0, out image, out reason);
        }
    }

    /// <summary>Reads a nested font's header and atlas, or reports why the bytes are not a font.</summary>
    /// <param name="entryBytes">The entry exactly as the container stores it, wrappers included.</param>
    /// <param name="facts">The font's measurements, when the bytes are a font.</param>
    /// <param name="reason">Why the bytes are not a font, when they are not.</param>
    public static bool TryReadFont(byte[] entryBytes, out FontFacts facts, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(entryBytes);
        byte[] payload = TryUnwrap(entryBytes, out byte[] unwrapped, out _) ? unwrapped : entryBytes;
        return FontFacts.TryRead(payload, out facts, out reason);
    }

    /// <summary>Which wrapper an entry was found in. This is a container fact, not a payload fact.</summary>
    public static MediaWrapper WrapperOf(LodPayload payload) => payload.Kind switch
    {
        LodPayloadKind.Compressed or LodPayloadKind.CompressedStored => MediaWrapper.Compressed,
        LodPayloadKind.Image => MediaWrapper.Image,
        LodPayloadKind.Palette => MediaWrapper.Palette,
        LodPayloadKind.Text or LodPayloadKind.DeflatedText => MediaWrapper.NonImage,
        _ => LooksLikeSprite(payload.Bytes) ? MediaWrapper.Sprite : MediaWrapper.Raw,
    };

    /// <summary>
    /// Whether the pixel or glyph block inside a wrapper was stored deflated. The manifest records this
    /// separately from the container's payload compression, because the two are independent: an entry
    /// can carry an uncompressed image header whose pixel block is deflated, and the shipped archives do.
    /// </summary>
    public static bool IsPixelBlockDeflated(byte[] entryBytes)
    {
        ArgumentNullException.ThrowIfNull(entryBytes);
        if (LooksLikeSprite(entryBytes)) return BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.AsSpan(0x1C)) != 0;
        return TryReadImageWrapper(entryBytes, out ImageWrapper wrapper) &&
            wrapper.Shape != WrapperShape.Palette &&
            BinaryPrimitives.ReadUInt32LittleEndian(entryBytes.AsSpan(0x28)) != 0;
    }

    /// <summary>Whether these bytes are a sprite wrapper whose line table and pixel block fit inside it.</summary>
    public static bool LooksLikeSprite(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < SpriteHeaderSize) return false;
        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x0C..]);
        ushort width = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x10..]);
        ushort height = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x12..]);
        ushort paletteId = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x14..]);
        ushort unused = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x16..]);
        ushort emptyBottomLines = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x18..]);

        return dataSize > 0
            && width > 0
            && height > 0
            && paletteId > 0
            && unused == 0
            && emptyBottomLines <= height
            && bytes.Length == SpriteHeaderSize + (height * 8) + dataSize;
    }

    private static bool TryDecodeNested(
        byte[] bytes,
        Func<ushort, IndexedPalette?>? namedPalettes,
        int depth,
        out DecodedImage? image,
        out string? reason)
    {
        image = null;
        reason = null;
        if (depth > MaxNesting)
        {
            reason = $"the entry nests more than {MaxNesting} wrappers deep";
            return false;
        }

        // The image wrapper is tried before the sprite wrapper, as the donor's sniffer does: a sprite
        // never satisfies the image predicate (its 0x10 field is width|height<<16, which cannot equal
        // width*height), while an image of 65536 pixels or more can satisfy the sprite predicate's own
        // size test.
        if (TryReadImageWrapper(bytes, out ImageWrapper wrapper))
        {
            switch (wrapper.Shape)
            {
                case WrapperShape.Palette:
                    image = DecodedImage.PaletteStrip(IndexedPalette.FromBytes(bytes.AsSpan(bytes.Length - PaletteSize)));
                    return true;
                case WrapperShape.NonImage:
                    // The wrapper says the payload is not an image, so sniff it again: this is where the
                    // nested PCX files and the fonts live.
                    return TryDecodeNested(wrapper.Pixels, namedPalettes, depth + 1, out image, out reason);
                default:
                    if (wrapper.Pixels.Length < wrapper.Width * wrapper.Height)
                    {
                        reason = $"the image wrapper declares {wrapper.Width}x{wrapper.Height} but carries {wrapper.Pixels.Length} pixel bytes";
                        return false;
                    }

                    image = DecodedImage.Bitmap(
                        wrapper.Width,
                        wrapper.Height,
                        wrapper.Pixels,
                        BuildLevels(wrapper.Width, wrapper.Height, wrapper.Pixels.Length),
                        IndexedPalette.FromBytes(wrapper.Palette),
                        MediaPaletteSource.Embedded,
                        (wrapper.Flags & ZeroIsTransparentFlag) != 0,
                        wrapper.Flags,
                        wrapper.PaletteId);
                    return true;
            }
        }

        if (LooksLikeSprite(bytes)) return TryDecodeSprite(bytes, namedPalettes, out image, out reason);

        if (TryUnwrap(bytes, out byte[] payload, out _))
        {
            return TryDecodeNested(payload, namedPalettes, depth + 1, out image, out reason);
        }

        if (PcxDecoder.TryDecode(bytes, null, out image, out string? pcxReason)) return true;
        reason = PcxDecoder.LooksLikePcx(bytes) ? $"the nested PCX image is not decodable: {pcxReason}" : Describe(bytes);
        return false;
    }

    /// <summary>
    /// Undoes the two wrappers that can carry any payload: the compressed-data wrapper and the image
    /// wrapper, in either order. The donor treats this as a sniffer rather than a declared format, and
    /// the second level exists in the data — the fonts and the PCX files are both payloads of a wrapper
    /// that is itself deflated.
    /// </summary>
    private static bool TryUnwrap(byte[] bytes, out byte[] payload, out uint flags)
    {
        payload = bytes;
        flags = 0;
        if (bytes.Length < LodArchive.ImageHeaderSize) return false;

        if (bytes.Length >= LodArchive.CompressionHeaderSize &&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) == LodArchive.CompressionVersion &&
            bytes.AsSpan(4, 4).SequenceEqual("mvii"u8))
        {
            uint declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
            uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
            ReadOnlySpan<byte> body = bytes.AsSpan(LodArchive.CompressionHeaderSize);
            // One writer stored the whole record size in the payload-size field; the donor works around
            // it and so does this decoder, rather than rejecting real data.
            int length = declaredSize == (uint)bytes.Length ? body.Length : (int)Math.Min(declaredSize, (uint)body.Length);
            payload = decompressedSize == 0 ? body[..length].ToArray() : Inflate(body[..length], decompressedSize);
            return true;
        }

        if (TryReadImageWrapper(bytes, out ImageWrapper wrapper) && wrapper.Shape != WrapperShape.Palette)
        {
            payload = wrapper.Pixels;
            flags = wrapper.Flags;
            return true;
        }

        return false;
    }

    private static bool TryDecodeSprite(
        byte[] bytes,
        Func<ushort, IndexedPalette?>? namedPalettes,
        out DecodedImage? image,
        out string? reason)
    {
        image = null;
        reason = null;
        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x0C));
        int width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x10));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x12));
        ushort paletteId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x14));
        ushort emptyBottomLines = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x18));
        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x1A));
        uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x1C));

        int pixelOffset = SpriteHeaderSize + (height * 8);
        if (pixelOffset + dataSize > bytes.Length)
        {
            reason = "the sprite's line table and pixel block run past the end of the entry";
            return false;
        }

        byte[] pixels;
        try
        {
            ReadOnlySpan<byte> stored = bytes.AsSpan(pixelOffset, (int)dataSize);
            pixels = decompressedSize == 0 ? stored.ToArray() : Inflate(stored, decompressedSize);
        }
        catch (MediaFormatException error)
        {
            reason = $"the sprite's pixel block is not readable: {error.Message}";
            return false;
        }

        byte[] canvas = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            int line = SpriteHeaderSize + (y * 8);
            int begin = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(line));
            int end = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(line + 2));
            int offset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(line + 4));

            // An empty row is written as begin == end, most often as -1/-1, so it has to be recognised
            // before the range test rather than after it.
            if (begin == end) continue;
            if (begin < 0 || end < 0 || begin > width || end > width || begin > end ||
                offset > pixels.Length || offset + end - begin > pixels.Length)
            {
                reason = $"the sprite's line table is invalid at row {y} (columns {begin}..{end}, offset {offset} into {pixels.Length} pixel bytes)";
                return false;
            }

            pixels.AsSpan(offset, end - begin).CopyTo(canvas.AsSpan((y * width) + begin, end - begin));
        }

        IndexedPalette? named = namedPalettes?.Invoke(paletteId);
        image = DecodedImage.Sprite(
            width,
            height,
            canvas,
            named ?? IndexedPalette.Grey,
            named is null ? MediaPaletteSource.Unresolved : MediaPaletteSource.Named,
            paletteId,
            emptyBottomLines,
            flags);
        return true;
    }

    /// <summary>
    /// Builds the level list by taking the base image and then halving while the next level still fits
    /// in the pixel block. Every mipmapped image in the shipped data carries four levels, but the count
    /// is measured from the block rather than assumed from the size, because the header's size field is
    /// not always the truth.
    /// </summary>
    private static MipLevel[] BuildLevels(int width, int height, int pixelBytes)
    {
        List<MipLevel> levels = [];
        int levelWidth = width;
        int levelHeight = height;
        int offset = 0;
        while (levelWidth > 0 && levelHeight > 0 && offset + (levelWidth * levelHeight) <= pixelBytes)
        {
            levels.Add(new MipLevel(levelWidth, levelHeight, offset));
            offset += levelWidth * levelHeight;
            levelWidth /= 2;
            levelHeight /= 2;
        }

        return [.. levels];
    }

    private static bool TryReadImageWrapper(byte[] bytes, out ImageWrapper wrapper)
    {
        wrapper = default;
        if (bytes.Length < ImageHeaderSize) return false;

        uint size = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x10));
        uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x14));
        int width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x18));
        int height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x1A));
        ushort paletteId = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x24));
        uint decompressedSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x28));
        uint flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0x2C));

        if (bytes.Length == ImageHeaderSize + PaletteSize)
        {
            bool zeroHeader = true;
            for (int offset = 0x10; offset < ImageHeaderSize && zeroHeader; offset++) zeroHeader = bytes[offset] == 0;
            if (zeroHeader)
            {
                wrapper = new ImageWrapper(WrapperShape.Palette, 0, 0, [], [], 0, 0);
                return true;
            }
        }

        // A non-image payload is the header plus its block and nothing else: no palette travels with
        // it, and its geometry fields are zero. widthMinus1 and heightMinus1 are deliberately not
        // tested, because the shipped fonts carry garbage there.
        if (size == 0 && dataSize > 0 && width == 0 && height == 0 &&
            (flags & NotAnImageFlag) != 0 && bytes.Length == ImageHeaderSize + dataSize)
        {
            wrapper = new ImageWrapper(WrapperShape.NonImage, 0, 0, ReadBlock(bytes, ImageHeaderSize, dataSize, decompressedSize), [], flags, paletteId);
            return true;
        }

        bool pixelsFit = decompressedSize == 0 ? dataSize >= size : decompressedSize >= size;
        if (size == 0 || dataSize == 0 || width <= 0 || height <= 0 || size != (uint)width * height || !pixelsFit ||
            bytes.Length != ImageHeaderSize + dataSize + PaletteSize)
        {
            return false;
        }

        wrapper = new ImageWrapper(
            WrapperShape.Image,
            width,
            height,
            ReadBlock(bytes, ImageHeaderSize, dataSize, decompressedSize),
            bytes.AsSpan(bytes.Length - PaletteSize, PaletteSize).ToArray(),
            flags,
            paletteId);
        return true;
    }

    private static byte[] ReadBlock(byte[] bytes, int offset, uint dataSize, uint decompressedSize)
    {
        ReadOnlySpan<byte> stored = bytes.AsSpan(offset, (int)dataSize);
        return decompressedSize == 0 ? stored.ToArray() : Inflate(stored, decompressedSize);
    }

    /// <summary>
    /// Inflates a zlib block. A block that does not inflate is a defect in the data rather than a reason
    /// to stop the archive, so the caller records it against the entry and carries on.
    /// </summary>
    private static byte[] Inflate(ReadOnlySpan<byte> stored, uint decompressedSize)
    {
        try
        {
            using MemoryStream input = new(stored.ToArray());
            using ZLibStream stream = new(input, CompressionMode.Decompress);
            using MemoryStream output = new(checked((int)decompressedSize));
            stream.CopyTo(output);
            byte[] result = output.ToArray();
            if (result.Length != decompressedSize)
            {
                throw new MediaFormatException($"the block declares {decompressedSize} bytes but inflated to {result.Length}");
            }

            return result;
        }
        catch (InvalidDataException error)
        {
            throw new MediaFormatException("the block is not valid deflate data", error);
        }
    }

    private static string Describe(byte[] bytes) =>
        bytes.Length < ImageHeaderSize
            ? $"only {bytes.Length} bytes, too few for any wrapper"
            : $"no wrapper predicate matched {bytes.Length} bytes";

    private enum WrapperShape
    {
        Image,
        Palette,
        NonImage,
    }

    private readonly record struct ImageWrapper(
        WrapperShape Shape,
        int Width,
        int Height,
        byte[] Pixels,
        byte[] Palette,
        uint Flags,
        ushort PaletteId);
}
