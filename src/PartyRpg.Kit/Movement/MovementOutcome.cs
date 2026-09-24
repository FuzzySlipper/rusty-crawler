using System.Numerics;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// Where the party ended up after one step, and what the engine and the tuning said about it.
/// </summary>
/// <remarks>
/// This is the whole answer to a movement request: the pose the party now holds, how far the engine
/// actually moved it and what stopped it, what the party is standing on, and what a landing cost. It
/// carries the engine's own facts rather than a summary of them, so a caller that needs to know a wall
/// from a steep slope can tell, and nothing has to be asked twice.
/// </remarks>
/// <param name="Pose">The party's pose after the step, in its place's own coordinates.</param>
/// <param name="Displacement">How far the engine moved the party, in the engine's world axes.</param>
/// <param name="Grounded">Whether the party is supported by ground after the step.</param>
/// <param name="Step">What the engine's step-up solver did, including the height it raised the party by.</param>
/// <param name="Blocked">What stopped the party, if anything: a wall, a ceiling, a slope too steep to climb, or a spent solver budget.</param>
/// <param name="Stance">What the engine made of the stance the intent asked for.</param>
/// <param name="Surface">The surface the party is standing on after the step.</param>
/// <param name="Fall">What a landing in this step cost, or none when the party did not land.</param>
public readonly record struct MovementOutcome(
    PlacePose Pose,
    Vector3 Displacement,
    bool Grounded,
    CharacterStep Step,
    CharacterBlockFlags Blocked,
    CharacterStanceFact Stance,
    SurfaceEffect Surface,
    FallOutcome Fall)
{
    /// <summary>Whether something in the world stopped the party from going where it asked.</summary>
    public bool WasBlocked => Blocked != CharacterBlockFlags.None;

    /// <summary>
    /// Whether the engine raised the party over a ledge instead of stopping it.
    /// </summary>
    /// <remarks>
    /// A step counts only when the engine attempted and accepted one with a rise: an attempted step that
    /// was refused is the party walking into a wall, not the party mounting it.
    /// </remarks>
    public bool SteppedUp => Step is { Present: true, Attempted: true, Accepted: true, Rise: > 0 };
}
