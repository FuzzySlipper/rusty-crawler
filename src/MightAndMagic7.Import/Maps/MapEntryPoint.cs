namespace MightAndMagic7.Import.Maps;

/// <summary>A place the party can arrive on the map, derived from its decorations.</summary>
/// <remarks>
/// Arrival is not a header field: the game looks for a decoration named after the start point it wants
/// and places the party there. Names are matched case-insensitively, because the shipped maps spell
/// them inconsistently — <c>"Party Start"</c> and <c>"north start"</c> both occur — and
/// <see cref="Name"/> is the canonical spelling of whichever name matched.
/// </remarks>
/// <param name="Name">The canonical start-point name, for example <c>"Party Start"</c> or <c>"North Start"</c>.</param>
/// <param name="Position">The decoration's stored position, before the game computes the floor level.</param>
/// <param name="YawAngle">The decoration's facing, in 2048 units per turn.</param>
/// <param name="DecorationIndex">The index of the decoration the point was derived from.</param>
public sealed record MapEntryPoint(string Name, MapPoint Position, int YawAngle, int DecorationIndex);
