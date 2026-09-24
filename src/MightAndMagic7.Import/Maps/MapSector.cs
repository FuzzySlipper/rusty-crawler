namespace MightAndMagic7.Import.Maps;

/// <summary>One sector of an indoor level: a convex room the level's faces belong to.</summary>
/// <remarks>
/// A sector's face, portal, decoration and light lists are not stored with the sector. They are
/// concatenated into the level's shared pools in sector order, and only the counts in the sector
/// record say where one list ends and the next begins — which is why those pools have to be walked
/// exactly, and why this record carries the resolved lists rather than offsets into them.
///
/// Portals are also counted among the faces, and repeat at the head of the face list when the level
/// was stored that way; the far side of a portal is the owning face's back sector.
/// </remarks>
/// <param name="Index">The sector's index in the level's sector array.</param>
/// <param name="Flags">The sector's flag word, kept raw; 0x08 marks non-vertical portals.</param>
/// <param name="WaterLevel">The sector's water level, or -30000 when it has none.</param>
/// <param name="MistLevel">The sector's mist level.</param>
/// <param name="LightDistanceMultiplier">The sector's light distance multiplier.</param>
/// <param name="MinAmbientLightLevel">The sector's minimum ambient light level.</param>
/// <param name="FirstBspNode">The sector's first BSP node.</param>
/// <param name="ExitTag">
/// The sector's exit tag, stored as one 16-bit field. A donor reads this slot as two 8-bit fields
/// instead, which would only differ for a non-zero value, and every shipped sector stores zero.
/// </param>
/// <param name="Bounds">The sector's bounding box.</param>
/// <param name="FloorIds">The sector's floor faces.</param>
/// <param name="WallIds">The sector's wall faces.</param>
/// <param name="CeilingIds">The sector's ceiling faces.</param>
/// <param name="FluidIds">The sector's fluid faces; empty in every shipped sector.</param>
/// <param name="PortalIds">The sector's portal faces.</param>
/// <param name="FaceIds">Every face the sector owns, portals included.</param>
/// <param name="NonBspFaceIds">The leading faces of <paramref name="FaceIds"/> that the BSP tree does not hold.</param>
/// <param name="CogIds">The sector's clickable-object numbers; empty in every shipped sector.</param>
/// <param name="DecorationIds">The sector's decorations.</param>
/// <param name="MarkerIds">The sector's markers; empty in every shipped sector.</param>
/// <param name="LightIds">The sector's lights, as indices into the level's light array.</param>
public sealed record MapSector(
    int Index,
    int Flags,
    int WaterLevel,
    int MistLevel,
    int LightDistanceMultiplier,
    int MinAmbientLightLevel,
    int FirstBspNode,
    int ExitTag,
    MapBounds Bounds,
    IReadOnlyList<int> FloorIds,
    IReadOnlyList<int> WallIds,
    IReadOnlyList<int> CeilingIds,
    IReadOnlyList<int> FluidIds,
    IReadOnlyList<int> PortalIds,
    IReadOnlyList<int> FaceIds,
    IReadOnlyList<int> NonBspFaceIds,
    IReadOnlyList<int> CogIds,
    IReadOnlyList<int> DecorationIds,
    IReadOnlyList<int> MarkerIds,
    IReadOnlyList<int> LightIds);
