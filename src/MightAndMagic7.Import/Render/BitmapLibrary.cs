using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Media;

namespace MightAndMagic7.Import.Render;

/// <summary>
/// The installation's world bitmaps (<c>BITMAPS.LOD</c>), looked up by the names levels and the tile table spell, and
/// decoded once each through the media decoder.
/// </summary>
/// <remarks>
/// Names are matched ignoring case and the first duplicate wins, as the container reader does (OpenEnroth
/// <c>src/Library/Lod/LodReader.cpp:108-116</c>); a level may spell a texture in another case than the archive.
/// A bitmap's colours come from its own embedded palette and palette entry zero is transparent only where the
/// image's flag says so (<c>docs/research/mm7-media-formats.md</c> §2).
/// </remarks>
public sealed class BitmapLibrary
{
    /// <summary>The archive the world's bitmaps live in.</summary>
    public const string ArchiveName = "BITMAPS.LOD";

    /// <summary>The archive the interface's images live in: item pictures and portraits.</summary>
    public const string IconArchiveName = "ICONS.LOD";

    private readonly LodArchive? _archive;
    private readonly Dictionary<string, DecodedImage?> _decoded = new(StringComparer.OrdinalIgnoreCase);

    private BitmapLibrary(LodArchive? archive) => _archive = archive;

    /// <summary>
    /// Opens the installation's bitmap archive. An installation without one holds no bitmaps: every name is then
    /// unresolved, and the render output reports each one missing rather than the import refusing the world.
    /// </summary>
    public static BitmapLibrary Open(LodInstall install) => Open(install, ArchiveName);

    /// <summary>Opens one of the installation's image archives, or an empty library when the installation lacks it.</summary>
    public static BitmapLibrary Open(LodInstall install, string archiveName)
    {
        ArgumentNullException.ThrowIfNull(install);
        bool present = install.ArchiveNames().Contains(archiveName, StringComparer.OrdinalIgnoreCase);
        return new BitmapLibrary(present ? install.Archive(archiveName) : null) { Archive = archiveName };
    }

    /// <summary>The archive this library reads.</summary>
    public string Archive { get; private init; } = ArchiveName;

    /// <summary>The bitmap a name spells, decoded, or null when the archive holds no image under it.</summary>
    public DecodedImage? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (_decoded.TryGetValue(name, out DecodedImage? cached)) return cached;
        DecodedImage? image = null;
        if (_archive?.Find(name) is { } entry && MediaDecoder.TryDecode(_archive.Raw(entry), null, out DecodedImage? decoded, out _)
            && decoded!.Kind == MediaKind.Bitmap)
        {
            image = decoded;
        }

        _decoded[name] = image;
        return image;
    }

    /// <summary>A bitmap's size, or null when the archive holds no image under the name.</summary>
    public (int Width, int Height)? Size(string name) => Find(name) is { } image ? (image.Width, image.Height) : null;

    /// <summary>The entry name the archive stores a bitmap under, which is its identity in a pack.</summary>
    public string? EntryName(string name) => string.IsNullOrWhiteSpace(name) ? null : _archive?.Find(name)?.Name;
}

/// <summary>A shoreline tile with the water drawn under its keyed texels.</summary>
/// <remarks>
/// A terrain tile's texels in the key colour show the water tile at the same coordinate (OpenEnroth
/// <c>resources/shaders/glterrain.frag:31-41</c>, which replaces them with the water texture array's frame). The key is
/// the colour (252, 0, 252) the shoreline tiles are drawn in [data: 10,643 of <c>wtrdrXSE</c>'s 16,384 texels]. The
/// original animates its water; the composite takes its first frame, which is ours.
/// </remarks>
public static class ShoreComposite
{
    /// <summary>Whether a tile has any keyed texel.</summary>
    public static bool HasKey(byte[] rgba)
    {
        ArgumentNullException.ThrowIfNull(rgba);
        for (int at = 0; at < rgba.Length; at += 4) if (IsKey(rgba, at)) return true;
        return false;
    }

    /// <summary>The tile with each keyed texel replaced by the water tile's texel at the same coordinate.</summary>
    public static byte[] Over(DecodedImage tile, DecodedImage water)
    {
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(water);
        byte[] rgba = tile.ToRgba();
        byte[] under = water.ToRgba();
        for (int y = 0; y < tile.Height; y++)
        {
            for (int x = 0; x < tile.Width; x++)
            {
                int at = ((y * tile.Width) + x) * 4;
                if (!IsKey(rgba, at)) continue;
                int from = (((y % water.Height) * water.Width) + (x % water.Width)) * 4;
                rgba[at] = under[from]; rgba[at + 1] = under[from + 1]; rgba[at + 2] = under[from + 2]; rgba[at + 3] = 255;
            }
        }

        return rgba;
    }

    private static bool IsKey(byte[] rgba, int at) => rgba[at] == 252 && rgba[at + 1] == 0 && rgba[at + 2] == 252;
}

/// <summary>
/// A region's sky as the Engine's sky background takes it: a 2:1 panorama.
/// </summary>
/// <remarks>
/// The original draws its sky bitmap tiled over a dome above the horizon (OpenEnroth
/// <c>src/Engine/Graphics/Renderer/OpenGLRenderer.cpp</c>, the sky pass). The panorama is ours: the bitmap tiled four
/// times around the upper half, fading in the last few rows above the horizon into the bitmap's own mean colour, which
/// fills the lower half where the ground stands.
/// </remarks>
public static class SkyPanorama
{
    /// <summary>The panorama's width; its height is half of it.</summary>
    public const int Width = 1024;

    /// <summary>Builds the panorama's RGBA pixels from a sky bitmap.</summary>
    public static byte[] From(DecodedImage sky)
    {
        ArgumentNullException.ThrowIfNull(sky);
        byte[] source = sky.ToRgba();
        int height = Width / 2;
        long r = 0, g = 0, b = 0;
        for (int index = 0; index < source.Length; index += 4) { r += source[index]; g += source[index + 1]; b += source[index + 2]; }
        int pixels = Math.Max(1, source.Length / 4);
        (byte R, byte G, byte B) mean = ((byte)(r / pixels), (byte)(g / pixels), (byte)(b / pixels));
        byte[] rgba = new byte[Width * height * 4];
        int horizon = height / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                int at = ((y * Width) + x) * 4;
                byte red = mean.R, green = mean.G, blue = mean.B;
                if (y < horizon)
                {
                    int sx = (x * 4 * sky.Width / Width) % sky.Width;
                    int sy = (y * sky.Height / horizon) % sky.Height;
                    int from = ((sy * sky.Width) + sx) * 4;
                    double fade = Math.Clamp((y - (horizon * 0.92)) / (horizon * 0.08), 0, 1);
                    red = (byte)((source[from] * (1 - fade)) + (mean.R * fade));
                    green = (byte)((source[from + 1] * (1 - fade)) + (mean.G * fade));
                    blue = (byte)((source[from + 2] * (1 - fade)) + (mean.B * fade));
                }

                rgba[at] = red; rgba[at + 1] = green; rgba[at + 2] = blue; rgba[at + 3] = 255;
            }
        }

        return rgba;
    }
}
