namespace MightAndMagic7.Import.Maps;

/// <summary>One door slot of an indoor level's delta.</summary>
/// <remarks>
/// The delta always carries a fixed 200 slots, most of them unused; <see cref="InUse"/> reports whether
/// this one holds a door. A door's geometry is the faces named by <see cref="FaceIds"/>, moved along
/// the offsets as it opens, and those faces are what connect its two sides — MM7 doors never name
/// their sectors themselves, so <see cref="SectorIds"/> is empty in every shipped door.
///
/// <see cref="State"/> is kept as the stored number. The reading that matches the shipped data is the
/// one where 0 is the door's rest position and 2 its fully moved position, with 1 and 3 the two
/// transitions; the donor's own naming of these states disagrees with that and is not followed here.
/// </remarks>
/// <param name="Index">The door's slot index in the delta's fixed door array.</param>
/// <param name="InUse">Whether the slot holds a door, which the file signals by a non-zero vertex count.</param>
/// <param name="State">The door's stored state number.</param>
/// <param name="Attributes">The door's attribute bitfield; 1 means triggered and 4 means silent.</param>
/// <param name="DoorId">The door's identifier.</param>
/// <param name="TimeSinceTriggered">Time elapsed since the door was last triggered.</param>
/// <param name="Direction">The direction the door moves in.</param>
/// <param name="MoveLength">How far the door moves.</param>
/// <param name="OpenSpeed">The speed the door opens at.</param>
/// <param name="CloseSpeed">The speed the door closes at.</param>
/// <param name="VertexIds">The vertices the door moves, as indices into the level's vertex array.</param>
/// <param name="FaceIds">The faces the door moves, as indices into the level's face array.</param>
/// <param name="SectorIds">The sectors the door moves; empty in every shipped door.</param>
/// <param name="DeltaUs">Per-face horizontal texture offsets.</param>
/// <param name="DeltaVs">Per-face vertical texture offsets.</param>
/// <param name="XOffsets">Per-offset-step X movement.</param>
/// <param name="YOffsets">Per-offset-step Y movement.</param>
/// <param name="ZOffsets">Per-offset-step Z movement.</param>
public sealed record MapDoor(
    int Index,
    bool InUse,
    int State,
    uint Attributes,
    uint DoorId,
    uint TimeSinceTriggered,
    MapPoint Direction,
    uint MoveLength,
    uint OpenSpeed,
    uint CloseSpeed,
    IReadOnlyList<int> VertexIds,
    IReadOnlyList<int> FaceIds,
    IReadOnlyList<int> SectorIds,
    IReadOnlyList<int> DeltaUs,
    IReadOnlyList<int> DeltaVs,
    IReadOnlyList<int> XOffsets,
    IReadOnlyList<int> YOffsets,
    IReadOnlyList<int> ZOffsets);
