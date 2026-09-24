namespace MightAndMagic7.Import.Maps;

/// <summary>One placed model of an outdoor map.</summary>
/// <remarks>
/// An outdoor level is a height field plus a set of models placed on it. The model's vertices are
/// absolute world coordinates, not positions relative to <see cref="Position"/> — the file stores the
/// geometry already placed, so a consumer must not add the two. The bounds come from the model header
/// rather than from the vertices, so a model whose header disagrees with its geometry says so instead
/// of the disagreement being hidden.
///
/// The header also carries a second bounding box, a bounding sphere, a "was drawn" flag and two centre
/// words. Every one of those is unread by the donor and the centre words are zero throughout the
/// shipped maps; the second box and the sphere are surfaced because the walk reads them, and the rest
/// are not invented into the model's meaning.
/// </remarks>
/// <param name="Index">The model's index in the map's model array.</param>
/// <param name="Name">The model's internal name, for example <c>"Tavern_E"</c>.</param>
/// <param name="Name2">The model's second name, which differs from the first for boats and chests.</param>
/// <param name="WasSeenFlag">The renderer's "already drawn" flag, zero throughout the shipped maps.</param>
/// <param name="ConvexFaceCount">The stored convex-face count, zero for every model of every shipped map.</param>
/// <param name="NodeCount">The stored BSP node count, zero for every model of every shipped map.</param>
/// <param name="DecorationCount">The stored decoration count.</param>
/// <param name="Position">The model's stored position; see the remarks before using it against vertices.</param>
/// <param name="Bounds">The model's bounding box.</param>
/// <param name="OtherBounds">The model's second bounding box, unread by the donor.</param>
/// <param name="BoundingCenter">The model's stored bounding sphere centre.</param>
/// <param name="BoundingRadius">The model's stored bounding sphere radius.</param>
/// <param name="Vertices">The model's vertices, in absolute world coordinates.</param>
/// <param name="Faces">The model's faces, with their vertices resolved.</param>
public sealed record OutdoorModel(
    int Index,
    string Name,
    string Name2,
    int WasSeenFlag,
    int ConvexFaceCount,
    int NodeCount,
    int DecorationCount,
    MapPoint Position,
    MapBounds Bounds,
    MapBounds OtherBounds,
    MapPoint BoundingCenter,
    int BoundingRadius,
    IReadOnlyList<MapPoint> Vertices,
    IReadOnlyList<MapFace> Faces);
