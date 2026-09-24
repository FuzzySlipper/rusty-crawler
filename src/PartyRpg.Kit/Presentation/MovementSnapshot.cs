using PartyRpg.Kit.Movement;
using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// What the party's last admitted movement step did, as the panel needs it.
/// </summary>
/// <remarks>
/// <para>
/// These are the movement owner's own facts copied into one presentation value, never a second opinion
/// about them: the engine's block flags say whether the displacement was refused and by what, the motion
/// the engine resolved says grounded or airborne, and the tuning priced any landing before the step was
/// reported. Nothing here re-derives a movement fact, which is what lets a person tell "the world stopped
/// me" from "my key never arrived" — the two are indistinguishable while the panel reports only the pose
/// and the stepped counts.
/// </para>
/// <para>
/// A session with no world, or one whose party has not taken a step yet, has no movement facts at all;
/// <see cref="None"/> is that state, so the panel says it knows nothing instead of showing a clear path.
/// </para>
/// </remarks>
/// <param name="Moved">
/// Whether a movement step has been admitted at all. The remaining facts describe that step, so they mean
/// nothing until this is true.
/// </param>
/// <param name="Grounded">Whether the engine reported the party supported by ground after the step.</param>
/// <param name="Blocked">What the engine said refused the party, <see cref="CharacterBlockFlags.None"/> when nothing did.</param>
/// <param name="StepRise">
/// How high an accepted step-up raised the party, in the engine's length unit. Zero when the engine
/// accepted no step-up, so a positive rise is the step-up itself and not a wish the engine refused.
/// </param>
/// <param name="FallDistance">
/// How far the last landing fell, in the engine's length unit. Zero when the step did not land.
/// </param>
/// <param name="FallDamage">
/// What that landing cost, in the unit the party's health is measured in. Zero when the fall cost nothing.
/// </param>
public readonly record struct MovementSnapshot(
    bool Moved,
    bool Grounded,
    CharacterBlockFlags Blocked,
    double StepRise,
    double FallDistance,
    double FallDamage)
{
    /// <summary>No movement to report: no step has been admitted, so every fact is at its quiet value.</summary>
    /// <remarks>
    /// This is what a session with no world publishes, and a paused session keeps reporting the last step
    /// it did take rather than falling back to this: the party is held where it stands, and the step that
    /// put it there is still the truth about its movement.
    /// </remarks>
    public static MovementSnapshot None => new(
        Moved: false,
        Grounded: false,
        Blocked: CharacterBlockFlags.None,
        StepRise: 0,
        FallDistance: 0,
        FallDamage: 0);

    /// <summary>Reads the movement facts out of the movement owner's last step.</summary>
    /// <param name="outcome">The last step the movement owner resolved, or null before the first one.</param>
    /// <returns>The facts of that step, or <see cref="None"/> when the party has not moved.</returns>
    public static MovementSnapshot From(MovementOutcome? outcome)
    {
        if (outcome is not { } step) return None;

        return new MovementSnapshot(
            Moved: true,
            Grounded: step.Grounded,
            Blocked: step.Blocked,
            // An attempted step the engine refused carries the rise the ledge would have taken, and the
            // party stayed below it: publishing that height would report a step-up nobody took. A rise is
            // therefore the owner's own accepted step-up and nothing else.
            StepRise: step.SteppedUp ? step.Step.Rise : 0,
            FallDistance: step.Fall.Distance,
            FallDamage: step.Fall.Damage);
    }
}
