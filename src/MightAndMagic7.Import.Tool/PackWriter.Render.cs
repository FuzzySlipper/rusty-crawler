using System.Globalization;
using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Media;
using MightAndMagic7.Import.Render;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Tool;

/// <summary>What one write's render output held: per place, and the bitmaps the media pack carries.</summary>
/// <param name="Places">Every place written, in id order.</param>
/// <param name="Textures">How many world bitmaps the media pack carries.</param>
/// <param name="Skies">How many sky panoramas it carries.</param>
internal sealed record RenderSummary(IReadOnlyList<PlaceRender> Places, int Textures, int Skies)
{
    internal static RenderSummary Empty { get; } = new([], 0, 0);

    /// <summary>Every bitmap some place names that the installation does not hold.</summary>
    internal IReadOnlyList<string> MissingTextures => [.. Places.SelectMany(place => place.MissingTextures).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];
}

internal static partial class PackWriter
{
    /// <summary>The definition kind a place's render geometry is written under.</summary>
    internal const string RenderDefinitionKind = "place-render";

    /// <summary>The definition kind a world bitmap is written under in the media pack.</summary>
    internal const string TextureDefinitionKind = "texture";

    /// <summary>The directory a place's binary mesh is written to inside the world pack.</summary>
    private const string RenderDirectory = "render";

    /// <summary>Builds every place's render geometry over the decoded maps, in place-id order.</summary>
    private static IReadOnlyList<PlaceRender> EmitRenders(IReadOnlyDictionary<int, DecodedMap> maps, TerrainTileTable tiles, BitmapLibrary bitmaps) =>
        [.. maps.OrderBy(pair => pair.Key).Select(pair => PlaceRender.Emit(pair.Key, pair.Value, tiles, bitmaps.Size))];

    /// <summary>The identity a bitmap has in the media pack: its archive entry name, lower-cased.</summary>
    internal static string TextureId(string name) => name.ToLowerInvariant();

    /// <summary>The identity a sky panorama has in the media pack.</summary>
    internal static string SkyId(string name) => "sky-" + name.ToLowerInvariant();

    /// <summary>
    /// Writes each place's binary mesh under <c>render/</c> and the <c>place-render</c> document that indexes them.
    /// </summary>
    private static int WritePlaceRenders(string packDirectory, IReadOnlyList<PlaceRender> renders, IReadOnlyDictionary<int, DecodedMap> maps, Mm7Tables tables, BitmapLibrary? bitmaps)
    {
        Directory.CreateDirectory(Path.Combine(packDirectory, RenderDirectory));
        List<(string, Action<Utf8JsonWriter>)> entries = [];
        foreach (PlaceRender render in renders)
        {
            string id = render.PlaceId.ToString(CultureInfo.InvariantCulture);
            string mesh = $"{RenderDirectory}/{id}.mesh";
            File.WriteAllBytes(Path.Combine(packDirectory, mesh), render.ToBytes());
            DecodedMap map = maps[render.PlaceId];
            entries.Add((id, writer =>
            {
                writer.WriteString("mapFile", tables.Maps.Maps.First(row => row.Id == render.PlaceId).FileName);
                writer.WriteString("kind", map is OutdoorMap ? "region" : "interior");
                writer.WriteString("mesh", mesh);
                writer.WriteNumber("vertices", render.Vertices);
                writer.WriteNumber("triangles", render.Triangles);
                writer.WriteNumber("undrawnFaces", render.UndrawnFaces);
                writer.WriteNumber("untexturedFaces", render.UntexturedFaces);
                writer.WriteString("sky", render.Sky.Length > 0 && bitmaps?.Find(render.Sky) is not null ? SkyId(bitmaps.EntryName(render.Sky)!) : string.Empty);
                writer.WriteStartArray("materials");
                foreach (RenderMaterial material in render.Materials)
                {
                    writer.WriteStartObject();
                    writer.WriteString("texture", material.Resolved ? TextureId(bitmaps!.EntryName(material.Texture)!) : string.Empty);
                    writer.WriteString("named", material.Texture);
                    writer.WriteString("surface", material.Surface switch
                    {
                        RenderSurface.Terrain => "terrain",
                        RenderSurface.Water => "water",
                        RenderSurface.Sky => "sky",
                        _ => "face",
                    });
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteStartArray("doors");
                foreach (RenderPart part in render.Parts.Where(part => part.Door is not null))
                    writer.WriteStringValue($"door-{part.Door!.Value.ToString(CultureInfo.InvariantCulture)}");
                writer.WriteEndArray();
                writer.WriteStartArray("missingTextures");
                foreach (string missing in render.MissingTextures) writer.WriteStringValue(missing);
                writer.WriteEndArray();
            }));
        }

        return WriteDocument(packDirectory, "place-render.json", "place-render", RenderDefinitionKind, entries);
    }

    /// <summary>The references a place's render document makes: its place and every texture it binds.</summary>
    private static IReadOnlyList<string> RenderReferences(IReadOnlyList<PlaceRender> renders, BitmapLibrary? bitmaps) =>
    [
        .. renders.Select(render => $"place:{render.PlaceId.ToString(CultureInfo.InvariantCulture)}"),
        .. renders.SelectMany(render => render.Materials).Where(material => material.Resolved)
            .Select(material => $"{TextureDefinitionKind}:{TextureId(bitmaps!.EntryName(material.Texture)!)}")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
        .. renders.Where(render => render.Sky.Length > 0 && bitmaps?.Find(render.Sky) is not null)
            .Select(render => $"{TextureDefinitionKind}:{SkyId(bitmaps!.EntryName(render.Sky)!)}")
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// Writes the media pack: every world bitmap a place binds, as a PNG with its source entry, and every region's sky
    /// as a panorama built from its sky bitmap.
    /// </summary>
    private static ((string PackId, int Documents, int Entries) Pack, int Textures, int Skies) WriteMedia(
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyList<PlaceRender> renders,
        BitmapLibrary? bitmaps,
        TerrainTileTable? tiles)
    {
        SortedDictionary<string, string> textures = new(StringComparer.Ordinal);
        HashSet<string> ground = new(StringComparer.Ordinal);
        SortedDictionary<string, string> skies = new(StringComparer.Ordinal);
        if (bitmaps is not null)
        {
            foreach (RenderMaterial material in renders.SelectMany(render => render.Materials).Where(material => material.Resolved))
            {
                textures.TryAdd(TextureId(bitmaps.EntryName(material.Texture)!), bitmaps.EntryName(material.Texture)!);
                if (material.Surface is RenderSurface.Terrain or RenderSurface.Water) ground.Add(TextureId(bitmaps.EntryName(material.Texture)!));
            }
            foreach (PlaceRender render in renders.Where(render => render.Sky.Length > 0 && bitmaps.Find(render.Sky) is not null))
                skies.TryAdd(SkyId(bitmaps.EntryName(render.Sky)!), bitmaps.EntryName(render.Sky)!);
        }

        Directory.CreateDirectory(Path.Combine(packDirectory, "textures"));
        Directory.CreateDirectory(Path.Combine(packDirectory, "skies"));
        List<(string, Action<Utf8JsonWriter>)> entries = [];
        DecodedImage? water = tiles is null ? null : bitmaps?.Find(tiles.WaterTileName);
        foreach ((string id, string entry) in textures)
        {
            DecodedImage image = bitmaps!.Find(entry)!;
            string path = $"textures/{id}.png";
            byte[] rgba = image.ToRgba();

            // A ground tile shows the water under its keyed texels; a face's texture is written as decoded.
            bool shore = ground.Contains(id) && water is not null && ShoreComposite.HasKey(rgba);
            if (shore) rgba = ShoreComposite.Over(image, water!);
            using (FileStream stream = File.Create(Path.Combine(packDirectory, path))) ImageWriter.WriteRgba(rgba, image.Width, image.Height, stream);
            entries.Add((id, writer => WriteImage(writer, path, image.Width, image.Height, shore ? "ground-shore" : "world", entry, image.ZeroIsTransparent)));
        }

        foreach ((string id, string entry) in skies)
        {
            byte[] rgba = SkyPanorama.From(bitmaps!.Find(entry)!);
            string path = $"skies/{id}.png";
            using (FileStream stream = File.Create(Path.Combine(packDirectory, path))) ImageWriter.WriteRgba(rgba, SkyPanorama.Width, SkyPanorama.Width / 2, stream);
            entries.Add((id, writer => WriteImage(writer, path, SkyPanorama.Width, SkyPanorama.Width / 2, "sky", entry, false)));
        }

        int written = WriteDocument(packDirectory, "textures.json", "textures", TextureDefinitionKind, entries);
        WriteManifest(packDirectory, "mm7-media", "definitions", provenance,
            [("textures.json", "textures", TextureDefinitionKind, [])]);
        return (("mm7-media", 1, written), textures.Count, skies.Count);

        static void WriteImage(Utf8JsonWriter writer, string path, int width, int height, string use, string entry, bool transparent)
        {
            writer.WriteString("path", path);
            writer.WriteString("use", use);
            writer.WriteNumber("width", width);
            writer.WriteNumber("height", height);
            writer.WriteBoolean("transparent", transparent);
            writer.WriteStartObject("source");
            writer.WriteString("archive", BitmapLibrary.ArchiveName);
            writer.WriteString("entry", entry);
            writer.WriteString("transform", use switch
            {
                "sky" => "panorama: tiled above the horizon, the bitmap's mean colour below (ours)",
                "ground-shore" => "base level, embedded palette, keyed texels replaced by the water tile's first frame",
                _ => "base level, embedded palette",
            });
            writer.WriteEndObject();
        }
    }
}
