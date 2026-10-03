using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Media;

namespace MightAndMagic7.Import.Render;

/// <summary>
/// One sprite group packed into a single image: every frame's views, one cell each, standing on the cell's bottom edge.
/// </summary>
/// <param name="Rgba">The packed image, RGBA8.</param>
/// <param name="Width">Its width.</param>
/// <param name="Height">Its height.</param>
/// <param name="Columns">How many cells a row holds.</param>
/// <param name="CellWidth">One cell's width in texels.</param>
/// <param name="CellHeight">One cell's height in texels.</param>
/// <param name="Octants">How many views a frame has: eight, or one for a frame drawn the same from every side.</param>
/// <param name="Frames">The group's frames, in order.</param>
/// <param name="Missing">The sprite entries a view names that the archive lacks; their cells are empty.</param>
public sealed record SpriteAtlasImage(
    byte[] Rgba,
    int Width,
    int Height,
    int Columns,
    int CellWidth,
    int CellHeight,
    int Octants,
    IReadOnlyList<SpriteFrame> Frames,
    IReadOnlyList<string> Missing)
{
    /// <summary>The cell of a frame's view: <c>frame * octants + octant</c>.</summary>
    public int Cell(int frame, int octant) => (frame * Octants) + octant;
}

/// <summary>
/// Builds a sprite group's atlas from the sprite archive, by the donor's own rules for which entry each view draws.
/// </summary>
/// <remarks>
/// <para>
/// A frame's eight views are its texture name with an octant digit; a frame marked one-image draws the bare name from
/// every side, one marked three-image stores views 0, 2 and 4 only, and a fidget borrows its side views from the
/// creature's standing frames; a view whose mirror flag is set draws the opposite view's entry flipped (OpenEnroth
/// <c>src/Engine/Graphics/Sprites.cpp:33-160</c>, <c>InitializeSprite</c>, and <c>billboardFlagsForSprite</c>). The flip is
/// baked into the atlas, so a renderer draws a cell as it stands.
/// </para>
/// <para>
/// Every view is coloured by its frame's palette rather than the sprite header's, which is how one creature's art is
/// recoloured for its grades (<c>docs/research/mm7-media-formats.md</c> §3); palette entry zero is transparent.
/// </para>
/// </remarks>
public sealed class SpriteAtlasBuilder
{
    private readonly LodArchive? _sprites;
    private readonly Func<ushort, IndexedPalette?> _palettes;
    private readonly Dictionary<string, DecodedImage?> _decoded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Opens the installation's sprite archive and its palettes.</summary>
    public SpriteAtlasBuilder(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        _sprites = install.ArchiveNames().Contains("SPRITES.LOD", StringComparer.OrdinalIgnoreCase) ? install.Archive("SPRITES.LOD") : null;
        _palettes = MediaExtractor.BuildNamedPaletteLookup(install);
    }

    /// <summary>Packs the group whose first frame is the given one, or null when the group draws no view at all.</summary>
    public SpriteAtlasImage? Build(IReadOnlyList<SpriteFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0) return null;
        int octants = frames.All(frame => (frame.Flags & SpriteFrame.Image1) != 0) ? 1 : 8;
        List<(string Entry, bool Mirrored, int PaletteId)?> views = [];
        foreach (SpriteFrame frame in frames)
        {
            for (int octant = 0; octant < octants; octant++) views.Add(View(frame, octant));
        }

        List<string> missing = [];
        List<(byte[] Rgba, int Width, int Height)?> images = [];
        foreach (var view in views)
        {
            if (view is not { } named || Image(named.Entry) is not { } image)
            {
                if (view is { } absent) missing.Add(absent.Entry);
                images.Add(null);
                continue;
            }

            IndexedPalette palette = _palettes((ushort)Math.Max(0, named.PaletteId)) ?? image.Palette;
            byte[] rgba = palette.ToRgba(image.Indices, zeroIsTransparent: true);
            images.Add((named.Mirrored ? Mirror(rgba, image.Width, image.Height) : rgba, image.Width, image.Height));
        }

        if (images.All(image => image is null)) return null;
        int cellWidth = images.Max(image => image?.Width ?? 0);
        int cellHeight = images.Max(image => image?.Height ?? 0);
        int cells = images.Count;
        int columns = Math.Max(1, Math.Min(cells, (int)Math.Ceiling(Math.Sqrt(cells * (double)cellHeight / cellWidth))));
        int rows = (cells + columns - 1) / columns;
        int width = columns * cellWidth, height = rows * cellHeight;
        byte[] atlas = new byte[width * height * 4];
        bool centred = frames.All(frame => (frame.Flags & SpriteFrame.Center) != 0);
        for (int cell = 0; cell < cells; cell++)
        {
            if (images[cell] is not { } image) continue;
            int left = ((cell % columns) * cellWidth) + ((cellWidth - image.Width) / 2);

            // A view stands on its cell's bottom edge, which is where the donor puts the object's point; a centred group
            // is centred in its cell instead.
            int top = ((cell / columns) * cellHeight) + (centred ? (cellHeight - image.Height) / 2 : cellHeight - image.Height);
            for (int y = 0; y < image.Height; y++)
                Buffer.BlockCopy(image.Rgba, y * image.Width * 4, atlas, (((top + y) * width) + left) * 4, image.Width * 4);
        }

        Bleed(atlas, width, height);
        return new SpriteAtlasImage(atlas, width, height, columns, cellWidth, cellHeight, octants, frames, [.. missing.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    /// <summary>The entry one view of a frame draws, whether it is flipped, and the palette it is coloured by.</summary>
    private static (string Entry, bool Mirrored, int PaletteId)? View(SpriteFrame frame, int octant)
    {
        string name = frame.TextureName;
        if (name.Length == 0) return null;
        bool mirrored = (frame.Flags & (SpriteFrame.Mirror0 << octant)) != 0;
        string entry;
        if ((frame.Flags & SpriteFrame.Image1) != 0)
        {
            entry = name;
        }
        else if ((frame.Flags & SpriteFrame.Images3) != 0)
        {
            entry = name + (octant switch { 3 or 4 or 5 => "4", 2 or 6 => "2", _ => "0" });
        }
        else if ((frame.Flags & SpriteFrame.Fidget) != 0 && name.Length > 3)
        {
            entry = octant switch
            {
                0 => name + "0",
                4 => name[..^3] + "stA4",
                3 or 5 => name[..^3] + "stA3",
                2 or 6 => name + "2",
                _ => name + "1",
            };
        }
        else if (mirrored)
        {
            entry = name + (octant == 0 ? 0 : 8 - octant).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            entry = name.Length < 7 ? name + octant.ToString(System.Globalization.CultureInfo.InvariantCulture) : name;
        }

        return (entry, mirrored, frame.PaletteId);
    }

    private DecodedImage? Image(string entry)
    {
        if (_decoded.TryGetValue(entry, out DecodedImage? cached)) return cached;
        DecodedImage? image = null;
        if (_sprites?.Find(entry) is { } found && MediaDecoder.TryDecode(_sprites.Raw(found), _palettes, out DecodedImage? decoded, out _)
            && decoded!.Kind == MediaKind.Sprite)
        {
            image = decoded;
        }

        _decoded[entry] = image;
        return image;
    }

    /// <summary>
    /// Gives each transparent texel next to an opaque one that neighbour's colour, still transparent, twice over, so a
    /// renderer that filters or mips the atlas blends an edge with the sprite's own colour rather than the palette's
    /// transparent entry. Alpha is unchanged; this is ours, not the donor's.
    /// </summary>
    private static void Bleed(byte[] rgba, int width, int height)
    {
        // A texel is "set" once it has a colour of its own: opaque, or given one by an earlier pass.
        bool[] set = new bool[width * height];
        for (int at = 0; at < set.Length; at++)
        {
            set[at] = rgba[(at * 4) + 3] != 0;
            if (!set[at]) rgba[at * 4] = rgba[(at * 4) + 1] = rgba[(at * 4) + 2] = 0;
        }

        for (int pass = 0; pass < 2; pass++)
        {
            bool[] before = (bool[])set.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int at = (y * width) + x;
                    if (before[at]) continue;
                    int r = 0, g = 0, b = 0, n = 0;
                    for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
                    {
                        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                        {
                            int near = (ny * width) + nx;
                            if (!before[near]) continue;
                            r += rgba[near * 4]; g += rgba[(near * 4) + 1]; b += rgba[(near * 4) + 2]; n++;
                        }
                    }

                    if (n == 0) continue;
                    rgba[at * 4] = (byte)(r / n); rgba[(at * 4) + 1] = (byte)(g / n); rgba[(at * 4) + 2] = (byte)(b / n);
                    set[at] = true;
                }
            }
        }
    }

    private static byte[] Mirror(byte[] rgba, int width, int height)
    {
        byte[] flipped = new byte[rgba.Length];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Buffer.BlockCopy(rgba, ((y * width) + x) * 4, flipped, ((y * width) + (width - 1 - x)) * 4, 4);
        return flipped;
    }
}
