namespace MightAndMagic7.Import.Maps;

/// <summary>One polygon of a level, with its vertices already resolved to positions.</summary>
/// <remarks>
/// Outdoor faces store their vertex ids inline in the face record and their texture name once per face
/// in the owning model; indoor faces store the ids and texture coordinates in the level's shared face
/// data pool and index the level's texture array. Both collapse to this shape. The trailing duplicate
/// of the first vertex that the file formats carry is dropped, so <see cref="Vertices"/> and
/// <see cref="TextureCoordinates"/> have exactly the face's vertex count.
/// </remarks>
/// <param name="Index">
/// The face's index in the array it was read from: the level's face array indoors, the owning model's
/// face array outdoors.
/// </param>
/// <param name="TextureName">The texture's name, or empty when the face is untextured.</param>
/// <param name="Vertices">The face's corners, in winding order.</param>
/// <param name="VertexIds">The same corners as indices into the owning vertex array.</param>
/// <param name="TextureCoordinates">One coordinate per corner, in the same order.</param>
/// <param name="Plane">The face's plane.</param>
/// <param name="Attributes">The face's attribute bitfield, kept raw; the bits differ per family.</param>
/// <param name="PolygonType">
/// The raw polygon type. The two families number these differently — indoors, 1 wall, 3 floor,
/// 4 slope floor, 5 ceiling, 6 slope ceiling — so no shared enum is asserted over them.
/// </param>
/// <param name="TextureDeltaU">U offset applied when drawing the texture.</param>
/// <param name="TextureDeltaV">V offset applied when drawing the texture.</param>
/// <param name="SectorId">The owning sector, or -1 for an outdoor face, which belongs to no sector.</param>
/// <param name="BackSectorId">The far side of a portal, or -1 when there is none or outdoors.</param>
/// <param name="FaceExtraId">The face's entry in the level's extras array, or -1 outdoors.</param>
public sealed record MapFace(
    int Index,
    string TextureName,
    IReadOnlyList<MapPoint> Vertices,
    IReadOnlyList<int> VertexIds,
    IReadOnlyList<MapTextureCoordinate> TextureCoordinates,
    MapPlane Plane,
    uint Attributes,
    int PolygonType,
    int TextureDeltaU,
    int TextureDeltaV,
    int SectorId,
    int BackSectorId,
    int FaceExtraId);
