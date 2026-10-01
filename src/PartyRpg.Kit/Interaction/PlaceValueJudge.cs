using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// The ruleset's answer about one value a save says a place keeps: why it is not one its rules could have
/// left, or null when it is.
/// </summary>
/// <remarks>
/// The kit keeps a place's values by name without knowing what any name means, so whether a name is one the
/// ruleset writes, and whether its figure is one those rules could leave, is the ruleset's to say. A load
/// asks before it composes anything, and every answer that is not null is a problem named with the rest.
/// </remarks>
/// <param name="place">The place the value belongs to.</param>
/// <param name="key">The value's name.</param>
/// <param name="value">The recorded figure.</param>
/// <returns>Why the value contradicts the ruleset, in words that finish "…, and …"; or null when it fits.</returns>
public delegate string? PlaceValueJudge(PlaceId place, string key, long value);
