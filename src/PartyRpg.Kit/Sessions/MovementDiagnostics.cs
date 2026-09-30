using PartyRpg.Kit.Movement;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// What the party's movement has done so far, as an observation.
/// </summary>
/// <remarks>
/// This is where a fall becomes visible: the movement owner reports what a landing cost and this records
/// it, because applying it would mean reaching into health the kit does not hold. The party's health
/// owner applies <see cref="LastFall"/> when it exists; until then a fall past the threshold is reported
/// here, published as an engine diagnostic, and charged to nobody.
/// </remarks>
/// <param name="Last">The last step's outcome, or null before the party has taken one.</param>
/// <param name="Falls">How many landings went past the tuning's fall threshold.</param>
public sealed record MovementDiagnostics(MovementOutcome? Last, int Falls)
{
    /// <summary>Nothing has moved yet: no step, and no fall.</summary>
    public static MovementDiagnostics None { get; } = new(null, 0);

    /// <summary>What the last landing cost, or none when the party has not landed past the threshold.</summary>
    public FallOutcome LastFall => Last?.Fall ?? FallOutcome.None;
}
