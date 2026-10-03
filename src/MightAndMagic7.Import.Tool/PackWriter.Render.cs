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
/// <param name="Icons">How many interface images it carries.</param>
/// <param name="MissingIcons">The interface images the tables name that the installation does not hold.</param>
internal sealed record RenderSummary(IReadOnlyList<PlaceRender> Places, int Textures, int Skies, int Icons = 0, IReadOnlyList<string>? MissingIcons = null)
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

    /// <summary>The definition kind an interface image — an item's picture, a portrait — is written under.</summary>
    internal const string IconDefinitionKind = "icon";

    /// <summary>
    /// The face sets a party member can wear, in the donor's order (OpenEnroth <c>src/Engine/mm7_data.cpp:50-55</c>,
    /// <c>pPlayerPortraitsNames</c>); a face's frame is the set's name and a two-digit frame number
    /// (<c>src/GUI/UI/UIGame.cpp:219</c>), the neutral face being frame 01.
    /// </summary>
    private static readonly string[] MemberFaceSets =
    [
        "pc01-", "pc02", "pc03", "pc04", "pc05-", "pc06", "pc07", "pc08", "pc09-", "pc10", "pc11-", "pc12", "pc13",
        "pc14", "pc15", "pc16", "pc17-", "pc18", "pc19", "pc20", "pc21-", "pc22-", "pc23", "pc24-", "pc25-",
    ];

    /// <summary>
    /// The interface images the media pack carries and why: each item's picture, each person's portrait
    /// (<c>npc{:03}</c>, OpenEnroth <c>src/GUI/UI/UIDialogue.cpp:67</c>), and each member face set's neutral frame.
    /// </summary>
    private static IReadOnlyList<(string Entry, string Use)> IconsNamed(Mm7Tables tables) =>
    [
        .. tables.Items.Items.Select(item => item.Picture).Where(picture => picture.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(picture => (picture, "item")),
        .. tables.People.Npcs.Select(person => person.Portrait).Where(portrait => portrait > 0).Distinct()
            .Select(portrait => ($"npc{portrait:000}", "person-portrait")),
        .. MemberFaceSets.Select(set => (set + "01", "member-portrait")),
    ];

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
    private static ((string PackId, int Documents, int Entries) Pack, int Textures, int Skies, int Icons, IReadOnlyList<string> MissingIcons) WriteMedia(
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyList<PlaceRender> renders,
        BitmapLibrary? bitmaps,
        TerrainTileTable? tiles,
        Mm7Tables tables,
        BitmapLibrary? icons)
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

        // Interface images: written where the archive holds them, and each absent one named in the summary rather than
        // replaced by a stand-in.
        Directory.CreateDirectory(Path.Combine(packDirectory, "icons"));
        List<(string, Action<Utf8JsonWriter>)> iconEntries = [];
        HashSet<string> taken = new(StringComparer.Ordinal);
        List<string> missingIcons = [];
        foreach ((string name, string use) in IconsNamed(tables))
        {
            if (icons?.Find(name) is not { } image || icons.EntryName(name) is not { } entry || !taken.Add(TextureId(entry)))
            {
                if (icons?.Find(name) is null) missingIcons.Add(name);
                continue;
            }

            string id = TextureId(entry);
            string path = $"icons/{id}.png";
            using (FileStream stream = File.Create(Path.Combine(packDirectory, path))) ImageWriter.WritePng(image, stream);
            iconEntries.Add((id, writer =>
            {
                writer.WriteString("path", path);
                writer.WriteString("use", use);
                writer.WriteNumber("width", image.Width);
                writer.WriteNumber("height", image.Height);
                writer.WriteBoolean("transparent", image.ZeroIsTransparent);
                writer.WriteStartObject("source");
                writer.WriteString("archive", icons.Archive);
                writer.WriteString("entry", entry);
                writer.WriteString("transform", "base level, embedded palette");
                writer.WriteEndObject();
            }));
        }

        int iconCount = WriteDocument(packDirectory, "icons.json", "icons", IconDefinitionKind, iconEntries);
        WriteManifest(packDirectory, "mm7-media", "definitions", provenance,
            [
                ("textures.json", "textures", TextureDefinitionKind, []),
                ("icons.json", "icons", IconDefinitionKind, []),
            ]);
        return (("mm7-media", 2, written + iconCount), textures.Count, skies.Count, iconCount, missingIcons);

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
