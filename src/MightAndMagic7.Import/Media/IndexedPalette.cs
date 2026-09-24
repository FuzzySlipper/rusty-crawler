namespace MightAndMagic7.Import.Media;

/// <summary>A 256-entry palette, with the expansion an 8-bit image needs.</summary>
/// <remarks>
/// Entries are stored in the order the container writes them: three bytes per colour, red first.
/// A palette is immutable; the shared <see cref="Empty"/> and <see cref="Grey"/> instances exist so a
/// unit that has no palette of its own does not allocate one per entry.
/// </remarks>
public sealed class IndexedPalette
{
    /// <summary>Number of entries in every palette.</summary>
    public const int Size = 256;

    /// <summary>Bytes one palette occupies in a container: 256 RGB triplets.</summary>
    public const int ByteSize = Size * 3;

    private static readonly Rgb24[] EmptyColors = new Rgb24[Size];
    private static readonly Rgb24[] GreyColors = CreateGreyColors();

    private readonly Rgb24[] _colors;

    private IndexedPalette(Rgb24[] colors) => _colors = colors;

    /// <summary>A palette of 256 black entries, for a unit whose colours are stored per pixel.</summary>
    public static IndexedPalette Empty { get; } = new(EmptyColors);

    /// <summary>An identity grey ramp, so a unit with an unresolvable palette still shows its shape.</summary>
    public static IndexedPalette Grey { get; } = new(GreyColors);

    /// <summary>The colour at an index.</summary>
    public Rgb24 this[int index] => _colors[index];

    /// <summary>Reads a container's 768-byte palette.</summary>
    public static IndexedPalette FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < ByteSize)
        {
            throw new MediaFormatException($"A palette is {ByteSize} bytes, but {bytes.Length} were given.");
        }

        Rgb24[] colors = new Rgb24[Size];
        for (int index = 0; index < Size; index++)
        {
            colors[index] = new Rgb24(bytes[index * 3], bytes[(index * 3) + 1], bytes[(index * 3) + 2]);
        }

        return new IndexedPalette(colors);
    }

    /// <summary>
    /// Expands indices to RGBA8. Alpha is fully opaque except for index 0 on a unit whose family says
    /// index 0 is transparent; the caller carries that fact because it is a per-unit flag, not a
    /// palette property, and the flag is unreliable in the shipped data.
    /// </summary>
    public byte[] ToRgba(ReadOnlySpan<byte> indices, bool zeroIsTransparent)
    {
        byte[] rgba = new byte[indices.Length * 4];
        for (int index = 0; index < indices.Length; index++)
        {
            byte value = indices[index];
            Rgb24 color = _colors[value];
            int at = index * 4;
            rgba[at] = color.R;
            rgba[at + 1] = color.G;
            rgba[at + 2] = color.B;
            rgba[at + 3] = zeroIsTransparent && value == 0 ? (byte)0 : (byte)255;
        }

        return rgba;
    }

    private static Rgb24[] CreateGreyColors()
    {
        Rgb24[] colors = new Rgb24[Size];
        for (int index = 0; index < Size; index++) colors[index] = new Rgb24((byte)index, (byte)index, (byte)index);
        return colors;
    }
}
