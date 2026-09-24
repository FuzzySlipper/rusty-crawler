using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// One complete session presentation: what the session is, what mode it is in, the admitted simulation
/// it has measured so far, and what its last movement step did. Every value here is owned by the
/// session; none of it is a placeholder for a mechanism that does not exist yet.
/// </summary>
/// <param name="Composition">The compiled ruleset this session runs.</param>
/// <param name="Mode">The session's mode.</param>
/// <param name="SimulationSeconds">Admitted simulation time accumulated while running.</param>
/// <param name="AdmittedSteps">Admitted fixed steps accumulated while running.</param>
/// <param name="Updates">Admitted updates this session has consumed.</param>
/// <param name="World">Where the party is, or an empty world when the session has no places loaded.</param>
/// <param name="Movement">
/// What the party's last admitted step did, or no facts at all when the session has no movement to
/// report — a session without a world, or one whose party has not stepped yet. The default is that
/// empty value, so a snapshot built without movement facts publishes a panel that says so rather than
/// one that claims the way is clear.
/// </param>
public readonly record struct SessionSnapshot(
    SessionComposition Composition,
    SessionMode Mode,
    double SimulationSeconds,
    ulong AdmittedSteps,
    ulong Updates,
    WorldSnapshot World,
    MovementSnapshot Movement = default);

/// <summary>Where the party is in the world, as the panel needs it: which place, where in it, and how much of the world is known.</summary>
/// <param name="Place">The place the party is in, empty when the session has no world.</param>
/// <param name="Name">The place's display name.</param>
/// <param name="Kind">The place's kind, as the wire spells it.</param>
/// <param name="Pose">The party's position and facing in that place.</param>
/// <param name="Visited">How many places the party has visited.</param>
/// <param name="Places">How many places the world holds.</param>
public readonly record struct WorldSnapshot(
    string Place,
    string Name,
    string Kind,
    PlacePose Pose,
    int Visited,
    int Places)
{
    /// <summary>The world of a session that has no places loaded.</summary>
    public static WorldSnapshot Empty => new(string.Empty, string.Empty, string.Empty, PlacePose.Origin, 0, 0);

    /// <summary>Whether the session has a world at all.</summary>
    public bool HasWorld => Places > 0;
}

/// <summary>Builds the session projection value. The wire vocabulary is stable and versioned by contract.</summary>
public static class SessionProjection
{
    /// <summary>The composition's ruleset identity field.</summary>
    public const string RulesetField = "ruleset";

    /// <summary>The composition's display title field.</summary>
    public const string TitleField = "title";

    /// <summary>The session object's wire name.</summary>
    public const string SessionField = "session";

    /// <summary>The composition's bundle identity field, empty when no bundle was selected.</summary>
    public const string BundleField = "bundle";

    /// <summary>The composition's resolved content pack count field.</summary>
    public const string ContentPacksField = "contentPacks";

    /// <summary>The world object's wire name.</summary>
    public const string WorldField = "world";

    /// <summary>The movement object's wire name.</summary>
    public const string MovementField = "movement";

    /// <summary>Builds the projection value for a snapshot.</summary>
    public static UiValue Build(SessionSnapshot snapshot)
    {
        UiValueBuilder builder = new();
        uint root = builder.Object(
            ("composition", builder.Object(
                (RulesetField, builder.String(snapshot.Composition.Ruleset.Value)),
                (TitleField, builder.String(snapshot.Composition.Title)),
                (BundleField, builder.String(snapshot.Composition.Bundle ?? string.Empty)),
                (ContentPacksField, builder.Number(snapshot.Composition.ContentPacks)))),
            (SessionField, builder.Object(
                ("mode", builder.String(WireName(snapshot.Mode))),
                ("simulationSeconds", builder.Number(snapshot.SimulationSeconds)),
                ("admittedSteps", builder.Number(snapshot.AdmittedSteps)),
                ("updates", builder.Number(snapshot.Updates)))),
            (WorldField, builder.Object(
                ("place", builder.String(snapshot.World.Place)),
                ("name", builder.String(snapshot.World.Name)),
                ("kind", builder.String(snapshot.World.Kind)),
                ("x", builder.Number(snapshot.World.Pose.X)),
                ("y", builder.Number(snapshot.World.Pose.Y)),
                ("z", builder.Number(snapshot.World.Pose.Z)),
                ("yaw", builder.Number(snapshot.World.Pose.Yaw)),
                ("visited", builder.Number(snapshot.World.Visited)),
                ("places", builder.Number(snapshot.World.Places)))),
            // Published even when nothing has moved: the motion word says which of "the world refused me"
            // and "the party has not stepped yet" the panel is looking at, and a block that only appeared
            // once something had moved would leave the two indistinguishable again.
            (MovementField, builder.Object(
                ("motion", builder.String(MotionWord(snapshot.Movement))),
                ("blocked", builder.String(WireName(snapshot.Movement.Blocked))),
                ("stepRise", builder.Number(snapshot.Movement.StepRise)),
                ("fallDistance", builder.Number(snapshot.Movement.FallDistance)),
                ("fallDamage", builder.Number(snapshot.Movement.FallDamage)))));
        return builder.Build(root);
    }

    /// <summary>
    /// The wire name for the state the party's last admitted step left it in.
    /// </summary>
    /// <remarks>
    /// Grounded, airborne, and "the party has not moved" are one word rather than a presence flag beside a
    /// grounded flag: a wire that could say grounded while also saying nothing has moved would let the
    /// panel report footing it does not know.
    /// </remarks>
    /// <param name="movement">The movement facts the snapshot carries.</param>
    /// <returns>The word the projection publishes for them.</returns>
    public static string MotionWord(MovementSnapshot movement) =>
        !movement.Moved ? "none" : movement.Grounded ? "grounded" : "airborne";

    /// <summary>
    /// The wire name for what refused the party's displacement.
    /// </summary>
    /// <remarks>
    /// The engine reports the obstacles it met as flags that can name several at once, and the panel needs
    /// one reason a person can act on. The order below is the order in which a flag explains the refusal:
    /// a party that began inside geometry is the headline whatever else was also met, then the surfaces it
    /// walked into, and last a solver that spent its budget without naming a particular obstacle. A flag
    /// this wire has no word for is still a refusal, so it is reported as blocked rather than as free.
    /// </remarks>
    /// <param name="blocked">The engine's block flags for the step.</param>
    /// <returns>The word the projection publishes for them.</returns>
    public static string WireName(CharacterBlockFlags blocked)
    {
        if ((blocked & CharacterBlockFlags.StartSolid) != 0) return "start-solid";
        if ((blocked & CharacterBlockFlags.Wall) != 0) return "wall";
        if ((blocked & CharacterBlockFlags.SteepSlope) != 0) return "steep-slope";
        if ((blocked & CharacterBlockFlags.Ceiling) != 0) return "ceiling";
        if ((blocked & CharacterBlockFlags.SolverBudget) != 0) return "solver-budget";
        return blocked == CharacterBlockFlags.None ? "none" : "blocked";
    }

    /// <summary>The wire name for a place kind.</summary>
    public static string WireName(PlaceKind kind) => kind switch
    {
        PlaceKind.Region => "region",
        PlaceKind.Interior => "interior",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown place kind."),
    };

    /// <summary>The wire name for a session mode.</summary>
    public static string WireName(SessionMode mode) => mode switch
    {
        SessionMode.Starting => "starting",
        SessionMode.Running => "running",
        SessionMode.Paused => "paused",
        SessionMode.Stopped => "stopped",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown session mode."),
    };
}
