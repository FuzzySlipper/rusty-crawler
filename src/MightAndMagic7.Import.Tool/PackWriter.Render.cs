using System.Globalization;
using System.Text.Json;
using MightAndMagic7.Import.Events;
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
    /// <summary>How many sprite groups the media pack carries as atlases.</summary>
    internal int Sprites { get; init; }

    /// <summary>How many looks it carries: monster rows, placed decorations and loose object kinds.</summary>
    internal int Looks { get; init; }

    /// <summary>The sprite entries a view names that the archive lacks.</summary>
    internal IReadOnlyList<string> MissingSprites { get; init; } = [];

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

    /// <summary>The definition kind a sprite group's atlas is written under.</summary>
    internal const string SpriteDefinitionKind = "sprite";

    /// <summary>The definition kind a creature's, decoration's or loose object's look is written under.</summary>
    internal const string LookDefinitionKind = "look";

    /// <summary>
    /// Whether the installation carries the frame table and the three look lists; one that does not (a test's synthetic
    /// archive) writes no looks rather than refusing the media pack.
    /// </summary>
    internal static bool HasLookTables(LodInstall install) =>
        install.ArchiveNames().Contains("Events.lod", StringComparer.OrdinalIgnoreCase)
        && new[] { "dsft.bin", "dmonlist.bin", "ddeclist.bin", "dobjlist.bin" }.All(name => install.Archive("Events.lod").Find(name) is not null);

    /// <summary>The identity a sprite group has in the media pack: its first frame's index in the frame table.</summary>
    internal static string SpriteId(int frame) => "frame-" + frame.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes every sprite group a look draws, as an atlas, and every look: each monster row's eight animations, each
    /// decoration the maps place or their events give, and each loose object kind.
    /// </summary>
    private static (int Sprites, int Looks, IReadOnlyList<string> MissingSprites) WriteLooks(
        string packDirectory, SpriteFrameTable table, LookLists lists, SpriteAtlasBuilder atlases, IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<EvtProgram> programs)
    {
        Directory.CreateDirectory(Path.Combine(packDirectory, "sprites"));
        SortedDictionary<int, SpriteAtlasImage?> groups = [];
        SpriteAtlasImage? Group(int frame)
        {
            if (frame <= 0 || frame >= table.Frames.Count) return null;
            if (!groups.TryGetValue(frame, out SpriteAtlasImage? atlas)) groups[frame] = atlas = atlases.Build(table.Group(frame));
            return atlas;
        }

        string Sprite(int? frame) => frame is { } first && Group(first) is not null ? SpriteId(first) : string.Empty;
        // A decoration is carried when a map places it or an event's sprite step gives it to a placed decoration's cog.
        HashSet<int> placed = [.. maps.Values.SelectMany(map => map.Decorations).Select(decoration => lists.DecorationRow(decoration.Name))];
        HashSet<string> given = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtInstruction instruction in programs.SelectMany(program => program.Instructions))
        {
            if (instruction.TryReadSetSprite(out _, out _, out string name) && name.Length > 0) given.Add(name);
        }

        placed.UnionWith(lists.Decorations.Where(decoration => given.Contains(decoration.Name)).Select(decoration => decoration.Index));
        List<(string, Action<Utf8JsonWriter>)> looks = [];
        foreach (MonsterLook monster in lists.Monsters)
        {
            string[] actions = [.. monster.Groups.Select(group => Sprite(table.Find(group)))];
            looks.Add(($"monster-{monster.Monster.ToString(CultureInfo.InvariantCulture)}", writer =>
            {
                writer.WriteNumber("height", monster.Height);
                writer.WriteNumber("radius", monster.Radius);
                writer.WriteStartArray("actions");
                foreach (string action in actions) writer.WriteStringValue(action);
                writer.WriteEndArray();
            }));
        }

        foreach (DecorationLook decoration in lists.Decorations.Where(decoration => placed.Contains(decoration.Index)))
        {
            bool hidden = (decoration.Flags & (DecorationLook.DontDraw | DecorationLook.Marker)) != 0;
            string sprite = hidden ? string.Empty : Sprite(decoration.Frame);
            looks.Add(($"decoration-{decoration.Index.ToString(CultureInfo.InvariantCulture)}", writer =>
            {
                writer.WriteString("name", decoration.Name);
                writer.WriteString("sprite", sprite);
                writer.WriteNumber("height", decoration.Height);
                writer.WriteNumber("radius", decoration.Radius);
                writer.WriteNumber("lightRadius", decoration.LightRadius);
                writer.WriteStartArray("lightColour");
                writer.WriteNumberValue(decoration.LightColour.R);
                writer.WriteNumberValue(decoration.LightColour.G);
                writer.WriteNumberValue(decoration.LightColour.B);
                writer.WriteEndArray();
                writer.WriteBoolean("hidden", hidden);
            }));
        }

        foreach (ObjectLook loose in lists.Objects)
        {
            bool hidden = (loose.Flags & 0x1) != 0;
            string sprite = hidden ? string.Empty : Sprite(loose.Frame);
            looks.Add(($"object-{loose.Index.ToString(CultureInfo.InvariantCulture)}", writer =>
            {
                writer.WriteString("sprite", sprite);
                writer.WriteBoolean("hidden", hidden);
            }));
        }

        int lookCount = WriteDocument(packDirectory, "looks.json", "looks", LookDefinitionKind, looks);
        List<(string, Action<Utf8JsonWriter>)> sprites = [];
        List<string> missing = [];
        foreach ((int frame, SpriteAtlasImage? atlas) in groups)
        {
            if (atlas is null) continue;
            missing.AddRange(atlas.Missing);
            string id = SpriteId(frame);
            string path = $"sprites/{id}.png";
            using (FileStream stream = File.Create(Path.Combine(packDirectory, path))) ImageWriter.WriteRgba(atlas.Rgba, atlas.Width, atlas.Height, stream);
            sprites.Add((id, writer =>
            {
                writer.WriteString("path", path);
                writer.WriteString("group", atlas.Frames[0].GroupName);
                writer.WriteNumber("width", atlas.Width);
                writer.WriteNumber("height", atlas.Height);
                writer.WriteNumber("columns", atlas.Columns);
                writer.WriteNumber("cellWidth", atlas.CellWidth);
                writer.WriteNumber("cellHeight", atlas.CellHeight);
                writer.WriteNumber("octants", atlas.Octants);
                writer.WriteNumber("scale", atlas.Frames[0].Scale);
                writer.WriteBoolean("centred", atlas.Frames.All(f => (f.Flags & SpriteFrame.Center) != 0));
                writer.WriteBoolean("lit", (atlas.Frames[0].Flags & SpriteFrame.Lit) != 0);
                writer.WriteStartArray("seconds");
                foreach (SpriteFrame f in atlas.Frames) writer.WriteNumberValue(f.Seconds);
                writer.WriteEndArray();
                writer.WriteStartArray("missing");
                foreach (string entry in atlas.Missing) writer.WriteStringValue(entry);
                writer.WriteEndArray();
                writer.WriteStartObject("source");
                writer.WriteString("archive", "SPRITES.LOD");
                writer.WriteString("table", "Events.lod:dsft.bin");
                writer.WriteNumber("firstFrame", frame);
                writer.WriteString("transform", "each frame's views by the donor's naming, coloured by the frame's palette, mirrored views flipped, packed one cell each");
                writer.WriteEndObject();
            }));
        }

        int spriteCount = WriteDocument(packDirectory, "sprites.json", "sprites", SpriteDefinitionKind, sprites);
        return (spriteCount, lookCount, [.. missing.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)]);
    }

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

    /// <summary>
    /// Builds every place's render geometry over the decoded maps, in place-id order, each with the face cogs its own
    /// event program switches; a program pairs with its map by the map file's stem, as the containers' do.
    /// </summary>
    private static IReadOnlyList<PlaceRender> EmitRenders(IReadOnlyDictionary<int, DecodedMap> maps, TerrainTileTable tiles, BitmapLibrary bitmaps,
        IReadOnlyList<EvtProgram> programs, Mm7Tables tables)
    {
        Dictionary<string, EvtProgram> byStem = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs) byStem.TryAdd(Path.GetFileNameWithoutExtension(program.Name), program);
        return [.. maps.OrderBy(pair => pair.Key).Select(pair =>
        {
            return PlaceRender.Emit(pair.Key, pair.Value, tiles, bitmaps.Size,
                PlaceSwitches.Of(byStem.GetValueOrDefault(Path.GetFileNameWithoutExtension(pair.Value.FileName))));
        })];
    }

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
    private static ((string PackId, int Documents, int Entries) Pack, int Textures, int Skies, int Icons, IReadOnlyList<string> MissingIcons,
        int Sprites, int Looks, IReadOnlyList<string> MissingSprites) WriteMedia(
        InstallProvenance provenance,
        string packDirectory,
        IReadOnlyList<PlaceRender> renders,
        BitmapLibrary? bitmaps,
        TerrainTileTable? tiles,
        Mm7Tables tables,
        BitmapLibrary? icons,
        LodInstall? install,
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<EvtProgram> programs)
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

        // The looks of what stands in a place, and the sprite groups they draw.
        (int sprites, int looks, IReadOnlyList<string> missingSprites) = install is null || !HasLookTables(install)
            ? (WriteDocument(packDirectory, "sprites.json", "sprites", SpriteDefinitionKind, []), WriteDocument(packDirectory, "looks.json", "looks", LookDefinitionKind, []), [])
            : WriteLooks(packDirectory, SpriteFrameTable.Read(install), LookLists.Read(install), new SpriteAtlasBuilder(install), maps, programs);
        WriteManifest(packDirectory, "mm7-media", "definitions", provenance,
            [
                ("textures.json", "textures", TextureDefinitionKind, []),
                ("icons.json", "icons", IconDefinitionKind, []),
                ("sprites.json", "sprites", SpriteDefinitionKind, []),
                ("looks.json", "looks", LookDefinitionKind, []),
            ]);
        return (("mm7-media", 4, written + iconCount + sprites + looks), textures.Count, skies.Count, iconCount, missingIcons, sprites, looks, missingSprites);

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
