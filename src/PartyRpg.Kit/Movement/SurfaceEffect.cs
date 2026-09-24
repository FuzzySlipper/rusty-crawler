using Rusty.Engine;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// What standing on one kind of ground does to the party's motion.
/// </summary>
/// <remarks>
/// A surface is named, not enumerated: whoever owns a place's data says which surface the ground the
/// engine reported is, and this says what that surface does. Nothing here knows a place, a map, or a
/// region, so a road is fast because the surface it is made of says so and not because of where it is.
/// Both multipliers are stated relative to ordinary ground: <c>1</c> leaves a speed as it is, a value
/// below <c>1</c> slows the party down, and a value above <c>1</c> speeds it up.
/// </remarks>
/// <param name="Id">The surface's own name, as content declares it.</param>
/// <param name="SpeedMultiplier">What the surface multiplies the party's walk, back, and strafe speeds by.</param>
/// <param name="JumpMultiplier">What the surface multiplies the party's jump speed by.</param>
public readonly record struct SurfaceEffect(string Id, double SpeedMultiplier, double JumpMultiplier)
{
    /// <summary>The effect of ground nothing classified: it changes nothing.</summary>
    public static SurfaceEffect Ordinary { get; } = new("ordinary", 1, 1);

    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// The caller's rule for what kind of ground the engine says the party is standing on.
/// </summary>
/// <remarks>
/// The engine reports the ground by its source — a voxel chunk, an admitted mesh instance, or an active
/// entity — and not by what a player would call it. Naming that source is content's work, so it arrives
/// here as a rule rather than as a table the kit keeps: the kit never learns which sources are water and
/// which are road, and a place that adds a new surface adds content, not code.
/// </remarks>
/// <param name="ground">The ground the engine resolved, with the source it came from.</param>
/// <param name="surfaceId">The surface's name when the rule recognises the ground.</param>
/// <returns>Whether the ground was recognised. Unrecognised ground is ordinary ground.</returns>
public delegate bool SurfaceClassifier(CharacterGround ground, out string surfaceId);
