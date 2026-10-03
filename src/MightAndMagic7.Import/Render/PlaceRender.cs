using System.Buffers.Binary;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;

namespace MightAndMagic7.Import.Render;

/// <summary>What a material draws: the ground's square tiles, a level or model face, or a face the level marks as sky.</summary>
public enum RenderSurface
{
    /// <summary>A region's terrain square, its tile's bitmap stretched over the square once.</summary>
    Terrain,

    /// <summary>A region's terrain square whose tile is water.</summary>
    Water,

    /// <summary>A model face outdoors or a level face indoors, its bitmap laid by the face's own texture coordinates.</summary>
    Face,

    /// <summary>A level face the level marks as sky (<c>FACE_INDOOR_SKY</c>), drawn with its own bitmap.</summary>
    Sky,
}

/// <summary>One material a place's render mesh binds: the bitmap it draws and what kind of surface it is.</summary>
/// <param name="Texture">The bitmap's name as the level or tile table spells it, or empty when the face names none.</param>
/// <param name="Surface">What the material draws.</param>
/// <param name="Resolved">Whether the bitmap exists in the installation; an unresolved one is drawn untextured.</param>
public sealed record RenderMaterial(string Texture, RenderSurface Surface, bool Resolved);

/// <summary>One contiguous range of a render mesh: its vertices, its triangles, and the door that moves it, if any.</summary>
/// <param name="Door">The door's index in the level, which the collision layout writes as <c>door-&lt;index&gt;</c>; null for static geometry.</param>
/// <param name="Switch">The face cog whose faces a map event hides, shows or retextures, when the part is that cog's; null otherwise.</param>
/// <param name="StartsHidden">Whether the part's faces start invisible (<c>FACE_IsInvisible</c>), to be shown by an event.</param>
/// <param name="VertexStart">The part's first vertex.</param>
/// <param name="VertexCount">How many vertices it has.</param>
/// <param name="IndexStart">The part's first index; its indices count from its own first vertex.</param>
/// <param name="IndexCount">How many indices it has.</param>
/// <param name="Groups">Its material ranges, each an index range within the part.</param>
public sealed record RenderPart(int? Door, int? Switch, bool StartsHidden, int VertexStart, int VertexCount, int IndexStart, int IndexCount, IReadOnlyList<RenderGroup> Groups);

/// <summary>A range of a part's indices drawn with one material.</summary>
/// <param name="Material">The material's index in the place's material list.</param>
/// <param name="Start">The first index, relative to the part's own first index.</param>
/// <param name="Count">How many indices.</param>
public sealed record RenderGroup(int Material, int Start, int Count);

/// <summary>
/// One place's render geometry: the surfaces a party sees, in the same Engine axes and units the place's collision uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Render is not collision.</b> The two read the same decoded faces and lay them on the same axes
/// (<see cref="Collision.CollisionMesh"/>: Engine x is the map's x, Engine y its height, Engine z its negated y), so a
/// wall is drawn exactly where the party is stopped. They differ in which faces they keep: a portal is neither, an
/// invisible face (<c>FACE_IsInvisible</c>) collides but is not drawn, an ethereal face (<c>FACE_ETHEREAL</c>) is drawn
/// and walked through, and a face whose corners collapse is kept for drawing because a door may open it
/// (OpenEnroth <c>src/Engine/Graphics/FaceEnums.h</c>).
/// </para>
/// <para>
/// <b>Texture coordinates are the donor's.</b> A face's corner coordinates are texels, offset by the face's own delta
/// and divided by its bitmap's size (OpenEnroth <c>src/Engine/Graphics/Renderer/OpenGLRenderer.cpp</c>, the model and
/// BSP passes, <c>textureUs + textureDeltaU</c>); a terrain square stretches its tile's bitmap over itself once, u along
/// the grid's columns and v along its rows (the terrain pass of the same file). A door's faces keep their coordinates
/// as the door moves rather than following the donor's per-door texture sliding, which is ours.
/// </para>
/// <para>
/// <b>Doors.</b> A door's faces are written as their own part, each corner at the rest position and with the travel
/// <see cref="PlaceCollisionLayout.DoorCorners"/> reads for the collision too: a closed door's corners stand at rest plus
/// travel in both.
/// </para>
/// </remarks>
public sealed class PlaceRender
{
    /// <summary>The binary mesh document's first eight bytes.</summary>
    public static ReadOnlySpan<byte> Magic => "PRMESH02"u8;

    /// <summary>The sky a region shows on the party's first visit.</summary>
    public const string FirstVisitSky = "plansky3";

    /// <summary>The donor's portal attribute.</summary>
    private const uint PortalAttribute = 0x00000001;

    /// <summary>The donor's invisible attribute, <c>FACE_IsInvisible</c>.</summary>
    private const uint InvisibleAttribute = InvisibleFaceBit;

    /// <summary><c>FACE_IsInvisible</c>, the bit a face-bit event sets to hide a cog's faces (OpenEnroth <c>src/Engine/Graphics/FaceEnums.h:21</c>).</summary>
    internal const uint InvisibleFaceBit = 0x00002000;

    /// <summary>The donor's indoor sky attribute, <c>FACE_INDOOR_SKY</c>.</summary>
    private const uint SkyAttribute = 0x00400000;

    private readonly List<float> _positions = [];
    private readonly List<float> _normals = [];
    private readonly List<float> _uvs = [];
    private readonly List<float> _travel = [];
    private readonly List<uint> _indices = [];
    private readonly List<RenderPart> _parts = [];
    private readonly List<RenderMaterial> _materials = [];
    private readonly Dictionary<(string, RenderSurface), int> _materialIndex = [];
    private readonly Func<string, (int Width, int Height)?> _bitmaps;

    private PlaceRender(int placeId, Func<string, (int Width, int Height)?> bitmaps)
    {
        PlaceId = placeId;
        _bitmaps = bitmaps;
    }

    /// <summary>The place's id.</summary>
    public int PlaceId { get; }

    /// <summary>Every material the mesh binds, in the order its groups name them.</summary>
    public IReadOnlyList<RenderMaterial> Materials => _materials;

    /// <summary>The mesh's parts: static geometry first, then one per door that moves faces.</summary>
    public IReadOnlyList<RenderPart> Parts => _parts;

    /// <summary>How many vertices the mesh holds.</summary>
    public int Vertices => _positions.Count / 3;

    /// <summary>How many triangles the mesh holds.</summary>
    public int Triangles => _indices.Count / 3;

    /// <summary>Faces left out because the level never draws them: portals and invisible faces.</summary>
    public int UndrawnFaces { get; private set; }

    /// <summary>Faces drawn untextured because the bitmap they name is not in the installation, or they name none.</summary>
    public int UntexturedFaces { get; private set; }

    /// <summary>The region's sky bitmap, empty indoors or where the map names none.</summary>
    public string Sky { get; private set; } = string.Empty;

    /// <summary>Every bitmap the place names that the installation does not hold.</summary>
    public IReadOnlyList<string> MissingTextures => [.. _materials.Where(material => !material.Resolved && material.Texture.Length > 0)
        .Select(material => material.Texture).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal)];

    /// <summary>Builds one place's render geometry.</summary>
    /// <param name="placeId">The place's id.</param>
    /// <param name="map">The decoded map.</param>
    /// <param name="tiles">The terrain tile table, which names a region's square bitmaps.</param>
    /// <param name="bitmaps">The size of a bitmap by name, or null when the installation does not hold it.</param>
    /// <param name="switches">The face cogs the place's events hide, show or retexture, and the bitmaps they retexture with.</param>
    public static PlaceRender Emit(int placeId, DecodedMap map, TerrainTileTable tiles, Func<string, (int Width, int Height)?> bitmaps,
        PlaceSwitches? switches = null)
    {
        switches ??= PlaceSwitches.None;
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tiles);
        ArgumentNullException.ThrowIfNull(bitmaps);
        PlaceRender render = new(placeId, bitmaps);
        // A door's corners are the collision layout's own reading: the same rest positions and travel.
        Dictionary<int, CollisionCorner> moved = PlaceCollisionLayout.DoorCorners(map);

        // Static geometry is the first part; every door that moves a drawn face is a part of its own after it.
        // A face of a cog an event switches is kept even when it starts invisible, in a part of its own, so the event
        // can show, hide or retexture it in place; a door's faces stay with their door.
        List<(MapFace Face, int? Door)> faces = [];
        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            bool switched = face.CogNumber != 0 && switches.Cogs.Contains(face.CogNumber);
            if (face.BackSectorId > 0 || (face.Attributes & PortalAttribute) != 0 || face.Vertices.Count < 3
                || ((face.Attributes & InvisibleAttribute) != 0 && !switched))
            {
                render.UndrawnFaces++;
                continue;
            }

            int? door = face.VertexIds.Select(id => moved.TryGetValue(id, out CollisionCorner? bound) ? bound.Door : null).FirstOrDefault(found => found is not null);

            // A door's part carries no switch, so an invisible face a door moves stays undrawn whatever its cog.
            if (door is not null && (face.Attributes & InvisibleAttribute) != 0)
            {
                render.UndrawnFaces++;
                continue;
            }

            faces.Add((face, door));
        }

        bool Switched(MapFace face) => face.CogNumber != 0 && switches.Cogs.Contains(face.CogNumber);
        render.BeginPart(null);
        if (map is OutdoorMap outdoor)
        {
            // The sky a region shows is its saved weather's, and a region never visited shows plansky3 (OpenEnroth
            // src/Engine/Graphics/Outdoor.cpp:518-534); the map header's own sky name is not what the donor draws.
            string weather = outdoor.Delta?.Weather.SkyTexture ?? string.Empty;
            render.Sky = weather.Length > 0 && bitmaps(weather) is not null ? weather : FirstVisitSky;
            render.AddTerrain(outdoor, tiles);
        }

        foreach ((MapFace face, _) in faces.Where(entry => entry.Door is null && !Switched(entry.Face))) render.AddFace(face, null, moved);
        render.EndPart();
        foreach (IGrouping<int, (MapFace Face, int? Door)> door in faces.Where(entry => entry.Door is not null).GroupBy(entry => entry.Door!.Value).OrderBy(group => group.Key))
        {
            render.BeginPart(door.Key);
            foreach ((MapFace face, _) in door) render.AddFace(face, door.Key, moved);
            render.EndPart();
        }

        foreach (var cog in faces.Where(entry => entry.Door is null && Switched(entry.Face))
            .GroupBy(entry => (entry.Face.CogNumber, Hidden: (entry.Face.Attributes & InvisibleAttribute) != 0))
            .OrderBy(group => group.Key.CogNumber).ThenBy(group => group.Key.Hidden))
        {
            render.BeginPart(null, cog.Key.CogNumber, cog.Key.Hidden);
            foreach ((MapFace face, _) in cog) render.AddFace(face, null, moved);
            render.EndPart();
        }

        // The bitmaps an event retextures with are materials of the place too, so a retextured cog draws from the same list.
        foreach (string texture in switches.Textures) render.Material(texture, RenderSurface.Face);
        return render;
    }

    // ---- building ----

    private int _partVertexStart;
    private int _partIndexStart;
    private int? _partDoor;
    private int? _partSwitch;
    private bool _partHidden;
    private readonly Dictionary<int, List<uint>> _partGroups = [];

    private void BeginPart(int? door, int? cog = null, bool hidden = false)
    {
        _partDoor = door;
        _partSwitch = cog;
        _partHidden = hidden;
        _partVertexStart = Vertices;
        _partIndexStart = _indices.Count;
        _partGroups.Clear();
    }

    private void EndPart()
    {
        // Indices are collected per material while the part is built, then laid out material by material so each group
        // is one contiguous range.
        List<RenderGroup> groups = [];
        foreach ((int material, List<uint> indices) in _partGroups.OrderBy(pair => pair.Key))
        {
            groups.Add(new RenderGroup(material, _indices.Count - _partIndexStart, indices.Count));
            _indices.AddRange(indices);
        }

        if (Vertices > _partVertexStart)
        {
            _parts.Add(new RenderPart(_partDoor, _partSwitch, _partHidden, _partVertexStart, Vertices - _partVertexStart, _partIndexStart, _indices.Count - _partIndexStart, groups));
        }
    }

    internal int Material(string texture, RenderSurface surface)
    {
        string key = texture.ToLowerInvariant();
        if (_materialIndex.TryGetValue((key, surface), out int index)) return index;
        index = _materials.Count;
        _materials.Add(new RenderMaterial(texture, surface, texture.Length > 0 && _bitmaps(texture) is not null));
        _materialIndex[(key, surface)] = index;
        return index;
    }

    private List<uint> Group(int material)
    {
        if (!_partGroups.TryGetValue(material, out List<uint>? indices)) _partGroups[material] = indices = [];
        return indices;
    }

    private uint Vertex(double x, double y, double z, (double X, double Y, double Z) normal, double u, double v, double[]? travel)
    {
        uint local = checked((uint)(Vertices - _partVertexStart));
        _positions.Add((float)x); _positions.Add((float)y); _positions.Add((float)z);
        _normals.Add((float)normal.X); _normals.Add((float)normal.Y); _normals.Add((float)normal.Z);
        _uvs.Add((float)u); _uvs.Add((float)v);
        _travel.Add((float)(travel?[0] ?? 0)); _travel.Add((float)(travel?[1] ?? 0)); _travel.Add((float)(travel?[2] ?? 0));
        return local;
    }

    /// <summary>A region's terrain: one quad per square, its tile's bitmap stretched over it, with smooth normals.</summary>
    private void AddTerrain(OutdoorMap map, TerrainTileTable tiles)
    {
        int squares = OutdoorMap.TerrainCells - 1;
        string[] names = tiles.TextureSquares(map);
        bool[] water = tiles.WaterSquares(map);
        for (int row = 0; row < squares; row++)
        {
            for (int column = 0; column < squares; column++)
            {
                string name = names[(row * squares) + column];
                int material = Material(name, water[(row * squares) + column] ? RenderSurface.Water : RenderSurface.Terrain);
                if (!_materials[material].Resolved) UntexturedFaces++;
                List<uint> group = Group(material);
                uint northWest = TerrainCorner(map, column, row, 0, 0);
                uint northEast = TerrainCorner(map, column + 1, row, 1, 0);
                uint southWest = TerrainCorner(map, column, row + 1, 0, 1);
                uint southEast = TerrainCorner(map, column + 1, row + 1, 1, 1);

                // The same split and winding the collision uses, so the drawn ground is the walked ground.
                group.AddRange([northWest, southWest, southEast, northWest, southEast, northEast]);
            }
        }
    }

    private uint TerrainCorner(OutdoorMap map, int column, int row, double u, double v)
    {
        MapPoint point = map.CellToWorld(column, row);

        // A corner's normal is the height field's gradient there, from its neighbours, in Engine axes.
        int west = map.TerrainHeightAt(Math.Max(column - 1, 0), row);
        int east = map.TerrainHeightAt(Math.Min(column + 1, OutdoorMap.TerrainCells - 1), row);
        int north = map.TerrainHeightAt(column, Math.Max(row - 1, 0));
        int south = map.TerrainHeightAt(column, Math.Min(row + 1, OutdoorMap.TerrainCells - 1));
        double dx = (east - west) / (2d * OutdoorMap.TerrainCellSize);
        double dz = (south - north) / (2d * OutdoorMap.TerrainCellSize);
        (double X, double Y, double Z) normal = Normalize((-dx, 1, -dz));
        return Vertex(point.X, point.Z, -point.Y, normal, u, v, null);
    }

    /// <summary>One face, fanned from its first corner, laid by its own texture coordinates.</summary>
    private void AddFace(MapFace face, int? door, Dictionary<int, CollisionCorner> moved)
    {
        RenderSurface surface = (face.Attributes & SkyAttribute) != 0 ? RenderSurface.Sky : RenderSurface.Face;
        int material = Material(face.TextureName, surface);
        if (!_materials[material].Resolved) UntexturedFaces++;
        (int Width, int Height) size = face.TextureName.Length > 0 && _bitmaps(face.TextureName) is { } found ? found : (1, 1);
        (double X, double Y, double Z) normal = Normalize((face.Plane.NormalX, face.Plane.NormalZ, -face.Plane.NormalY));
        if (normal == (0, 0, 0)) normal = FanNormal(face);
        List<uint> corners = [];
        for (int index = 0; index < face.Vertices.Count; index++)
        {
            MapPoint point = face.Vertices[index];
            MapTextureCoordinate texel = index < face.TextureCoordinates.Count ? face.TextureCoordinates[index] : default;
            CollisionCorner? bound = door is not null && moved.TryGetValue(face.VertexIds[index], out CollisionCorner? corner) && corner.Door == door ? corner : null;
            double[] at = bound?.Rest ?? [point.X, point.Z, -point.Y];
            corners.Add(Vertex(at[0], at[1], at[2], normal,
                (texel.U + face.TextureDeltaU) / (double)size.Width,
                (texel.V + face.TextureDeltaV) / (double)size.Height,
                bound?.Travel));
        }

        List<uint> group = Group(material);
        for (int index = 2; index < corners.Count; index++) group.AddRange([corners[0], corners[index - 1], corners[index]]);
    }

    private static (double X, double Y, double Z) FanNormal(MapFace face)
    {
        MapPoint a = face.Vertices[0], b = face.Vertices[1], c = face.Vertices[2];
        (double X, double Y, double Z) u = (b.X - a.X, b.Z - a.Z, -(b.Y - a.Y));
        (double X, double Y, double Z) v = (c.X - a.X, c.Z - a.Z, -(c.Y - a.Y));
        return Normalize(((u.Y * v.Z) - (u.Z * v.Y), (u.Z * v.X) - (u.X * v.Z), (u.X * v.Y) - (u.Y * v.X)));
    }

    private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) value)
    {
        double length = Math.Sqrt((value.X * value.X) + (value.Y * value.Y) + (value.Z * value.Z));
        return length < 1e-9 ? (0, 0, 0) : (value.X / length, value.Y / length, value.Z / length);
    }

    // ---- writing ----

    /// <summary>
    /// Writes the binary mesh: the magic, four counts (vertices, indices, parts, groups), then little-endian f32
    /// positions (3 per vertex), normals (3), texture coordinates (2) and door travel (3), u32 indices, each part as
    /// (i32 door or -1, i32 switch cog or -1, u32 flags — bit 0 starts hidden — u32 vertex start, u32 vertex count,
    /// u32 index start, u32 index count, u32 first group, u32 group count), and each group as (u32 material, u32 start,
    /// u32 count).
    /// </summary>
    public byte[] ToBytes()
    {
        RenderGroup[] groups = [.. _parts.SelectMany(part => part.Groups)];
        int size = 8 + 16 + (_positions.Count * 4) + (_normals.Count * 4) + (_uvs.Count * 4) + (_travel.Count * 4)
            + (_indices.Count * 4) + (_parts.Count * 36) + (groups.Length * 12);
        byte[] bytes = new byte[size];
        Magic.CopyTo(bytes);
        int at = 8;
        void U32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value); at += 4; }
        void I32(int value) { BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at), value); at += 4; }
        void Floats(List<float> values) { foreach (float value in values) { BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at), value); at += 4; } }
        U32((uint)Vertices); U32((uint)_indices.Count); U32((uint)_parts.Count); U32((uint)groups.Length);
        Floats(_positions); Floats(_normals); Floats(_uvs); Floats(_travel);
        foreach (uint index in _indices) U32(index);
        int firstGroup = 0;
        foreach (RenderPart part in _parts)
        {
            I32(part.Door ?? -1); I32(part.Switch ?? -1); U32(part.StartsHidden ? 1u : 0u); U32((uint)part.VertexStart); U32((uint)part.VertexCount);
            U32((uint)part.IndexStart); U32((uint)part.IndexCount); U32((uint)firstGroup); U32((uint)part.Groups.Count);
            firstGroup += part.Groups.Count;
        }

        foreach (RenderGroup group in groups) { U32((uint)group.Material); U32((uint)group.Start); U32((uint)group.Count); }
        return bytes;
    }
}

/// <summary>What a place's events change about its faces: the cogs they hide, show or retexture, and the bitmaps they use.</summary>
/// <param name="Cogs">The face cogs a <c>set-texture</c> step or a <c>set-faces-bit</c> step on the invisible bit names.</param>
/// <param name="Textures">The bitmaps the place's <c>set-texture</c> steps give.</param>
public sealed record PlaceSwitches(IReadOnlySet<int> Cogs, IReadOnlyList<string> Textures)
{
    /// <summary>A place whose events switch nothing.</summary>
    public static PlaceSwitches None { get; } = new(new HashSet<int>(), []);

    /// <summary>
    /// Reads what one map's event program switches: the cog of every texture step, and of every face-bit step whose bit
    /// is the invisible one (OpenEnroth <c>src/Engine/Engine.cpp:948-990</c>), with the bitmaps the texture steps name.
    /// </summary>
    public static PlaceSwitches Of(Events.EvtProgram? program)
    {
        if (program is null) return None;
        HashSet<int> cogs = [];
        SortedSet<string> textures = new(StringComparer.OrdinalIgnoreCase);
        foreach (Events.EvtInstruction instruction in program.Instructions)
        {
            if (instruction.TryReadSetTexture(out int cog, out string texture) && cog != 0)
            {
                cogs.Add(cog);
                if (texture.Length > 0) textures.Add(texture);
            }
            else if (instruction.TryReadFlagToggle(out Events.FlagToggleInstruction toggle) && instruction.Opcode == Events.EvtOpcodes.SetFacesBit
                && (toggle.Flag & PlaceRender.InvisibleFaceBit) != 0 && toggle.Group != 0)
            {
                cogs.Add(toggle.Group);
            }
        }

        return new PlaceSwitches(cogs, [.. textures]);
    }
}
