namespace MightAndMagic7.Import.Media;

/// <summary>
/// One decoded media unit: the pixels, the colours they are indexed against, and the facts about the
/// wrapper they came from.
/// </summary>
/// <remarks>
/// Every unit keeps its 8-bit indices even when a palette exists, because the product may recolour a
/// sprite from the animation table's own palette id rather than the one in the sprite header. Colours
/// are therefore a view over the indices, never a replacement for them.
/// </remarks>
public sealed class DecodedImage
{
    private DecodedImage(
        MediaKind kind,
        int width,
        int height,
        byte[] pixels,
        MipLevel[] levels,
        IndexedPalette palette,
        MediaPaletteSource paletteSource,
        byte[]? rgba,
        bool zeroIsTransparent,
        uint flags,
        ushort paletteId,
        ushort emptyBottomLines)
    {
        Kind = kind;
        Width = width;
        Height = height;
        Pixels = pixels;
        Levels = levels;
        Palette = palette;
        PaletteSource = paletteSource;
        Rgba = rgba;
        ZeroIsTransparent = zeroIsTransparent;
        Flags = flags;
        PaletteId = paletteId;
        EmptyBottomLines = emptyBottomLines;
    }

    /// <summary>The media family this unit belongs to.</summary>
    public MediaKind Kind { get; }

    /// <summary>Width of the base level in pixels.</summary>
    public int Width { get; }

    /// <summary>Height of the base level in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// The whole pixel block as stored: the base level first, then any mip levels largest first. For a
    /// sprite this is the canvas the line table was drawn into, and for a PCX it is the raw 24-bit
    /// payload, which is not indexed at all.
    /// </summary>
    public byte[] Pixels { get; }

    /// <summary>The mip chain, base level first. A unit without mipmaps has exactly one level.</summary>
    public IReadOnlyList<MipLevel> Levels { get; }

    /// <summary>The colours the base level's indices refer to.</summary>
    public IndexedPalette Palette { get; }

    /// <summary>Where those colours came from.</summary>
    public MediaPaletteSource PaletteSource { get; }

    /// <summary>
    /// Set only when the unit is not palette-indexed: a nested PCX stores three colour planes per
    /// pixel, so quantizing it to 256 colours would lose the image rather than describe it.
    /// </summary>
    public byte[]? Rgba { get; }

    /// <summary>Whether palette index 0 is transparent for this unit.</summary>
    public bool ZeroIsTransparent { get; }

    /// <summary>The image or sprite header's flag word; zero when the family has none.</summary>
    public uint Flags { get; }

    /// <summary>The palette id a sprite header names; zero when the family has none.</summary>
    public ushort PaletteId { get; }

    /// <summary>Blank lines the sprite header reports at the bottom of the canvas.</summary>
    public ushort EmptyBottomLines { get; }

    /// <summary>The base level's indices, row-major with a stride of <see cref="Width"/>.</summary>
    public ReadOnlySpan<byte> Indices => Pixels.AsSpan(0, Width * Height);

    /// <summary>One mip level's indices.</summary>
    public ReadOnlySpan<byte> Level(int level)
    {
        MipLevel mip = Levels[level];
        return Pixels.AsSpan(mip.Offset, mip.Width * mip.Height);
    }

    /// <summary>The base level as RGBA8.</summary>
    public byte[] ToRgba() => ToRgba(0);

    /// <summary>One mip level as RGBA8.</summary>
    public byte[] ToRgba(int level)
    {
        if (Rgba is not null) return level == 0 ? [.. Rgba] : throw new ArgumentOutOfRangeException(nameof(level));

        MipLevel mip = Levels[level];
        return Palette.ToRgba(Pixels.AsSpan(mip.Offset, mip.Width * mip.Height), ZeroIsTransparent);
    }

    /// <summary>Builds a flat image from the image wrapper's inflated pixel block.</summary>
    /// <param name="kind">
    /// The family the indices belong to. A nested PCX holds palette indices too when it is a
    /// single-plane file, so the family is the caller's to state rather than the factory's to assume.
    /// </param>
    public static DecodedImage Bitmap(
        int width,
        int height,
        byte[] pixels,
        MipLevel[] levels,
        IndexedPalette palette,
        MediaPaletteSource paletteSource,
        bool zeroIsTransparent,
        uint flags,
        ushort paletteId,
        MediaKind kind = MediaKind.Bitmap)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(palette);
        if (width <= 0 || height <= 0) throw new MediaFormatException($"An image must have a positive size, not {width}x{height}.");
        if (pixels.Length < width * height) throw new MediaFormatException($"An image of {width}x{height} needs {width * height} pixels, not {pixels.Length}.");

        return new DecodedImage(
            kind, width, height, pixels, levels, palette, paletteSource, null, zeroIsTransparent, flags, paletteId, 0);
    }

    /// <summary>Builds the 256x1 strip a palette-only entry decodes to.</summary>
    public static DecodedImage PaletteStrip(IndexedPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        byte[] indices = new byte[IndexedPalette.Size];
        for (int index = 0; index < indices.Length; index++) indices[index] = (byte)index;

        return new DecodedImage(
            MediaKind.Palette,
            IndexedPalette.Size,
            1,
            indices,
            [new MipLevel(IndexedPalette.Size, 1, 0)],
            palette,
            MediaPaletteSource.Embedded,
            null,
            false,
            0,
            0,
            0);
    }

    /// <summary>Builds one sprite frame from the canvas its line table was drawn into.</summary>
    public static DecodedImage Sprite(
        int width,
        int height,
        byte[] canvas,
        IndexedPalette palette,
        MediaPaletteSource paletteSource,
        ushort paletteId,
        ushort emptyBottomLines,
        uint flags) =>
        new(
            MediaKind.Sprite,
            width,
            height,
            canvas,
            [new MipLevel(width, height, 0)],
            palette,
            paletteSource,
            null,
            // Sprite index 0 is the transparent colour by definition; the donor's renderer relies on
            // it, and no sprite header carries a flag that could say otherwise.
            true,
            flags,
            paletteId,
            emptyBottomLines);

    /// <summary>Builds a unit whose colours are stored per pixel rather than indexed.</summary>
    public static DecodedImage DirectRgba(MediaKind kind, int width, int height, byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        if (width <= 0 || height <= 0) throw new MediaFormatException($"An image must have a positive size, not {width}x{height}.");
        if (rgba.Length != width * height * 4) throw new MediaFormatException($"An RGBA image of {width}x{height} needs {width * height * 4} bytes, not {rgba.Length}.");

        return new DecodedImage(
            kind,
            width,
            height,
            rgba,
            [new MipLevel(width, height, 0)],
            IndexedPalette.Empty,
            MediaPaletteSource.None,
            rgba,
            false,
            0,
            0,
            0);
    }
}
