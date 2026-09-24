namespace MightAndMagic7.Import.Maps;

/// <summary>One level decoration: scenery, but also the level's arrival points.</summary>
/// <remarks>
/// Both families store 32 bytes per decoration and 32 bytes per name, kept in two parallel arrays.
/// The name is the decoration's internal identity, and it is what the game matches the party's start
/// points and the decoration table against; <see cref="DescriptionId"/> is the index the runtime
/// resolves it to.
///
/// <see cref="Position"/> is the stored position. It is deliberately not adjusted: when the game
/// places the party it replaces Z with a floor level computed from the level's geometry, which is a
/// placement decision rather than part of the payload.
/// </remarks>
/// <param name="Index">The decoration's index in the level's decoration array.</param>
/// <param name="Name">The decoration's name as stored, for example <c>"Party Start"</c> or <c>"tree04"</c>.</param>
/// <param name="DescriptionId">The decoration table id the runtime resolves the name to.</param>
/// <param name="Flags">The decoration's flag word, kept raw; the delta carries the runtime flags.</param>
/// <param name="Position">The stored position.</param>
/// <param name="YawAngle">Facing, in 2048 units per turn: 0 west, 512 south, 1024 east, 1536 north.</param>
/// <param name="Cog">The clickable-object number, or 0 when the decoration is not clickable.</param>
/// <param name="EventId">The event the decoration raises, or 0 when it raises none.</param>
/// <param name="TriggerRange">The range at which the decoration's event triggers.</param>
/// <param name="EventVarId">The map variable the decoration's event reads, or 0 when it reads none.</param>
public sealed record MapDecoration(
    int Index,
    string Name,
    int DescriptionId,
    int Flags,
    MapPoint Position,
    int YawAngle,
    int Cog,
    int EventId,
    int TriggerRange,
    int EventVarId);
