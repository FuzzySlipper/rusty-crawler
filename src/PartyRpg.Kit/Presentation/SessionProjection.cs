using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// One complete session presentation: what the session is, what mode it is in, the admitted simulation
/// it has measured so far, where the party is and what its last movement step did, where the game clock
/// stands, and what the party's own accounts hold. Every value here is owned by one of those owners; none
/// of it is a placeholder for a mechanism that does not exist yet.
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
/// <param name="Clock">
/// Where the session's one clock stands, or the not-known value when its ruleset composed none. It is
/// defaulted for the same reason movement is: a snapshot built without a clock publishes a panel that says
/// it does not know the date rather than one that shows a date nobody kept.
/// </param>
/// <param name="Party">
/// The party's accounts and standing, or the not-known value when the session holds no party — which is
/// what content that declares neither members nor starting values gets. Defaulted, so a session without a
/// party publishes that rather than an empty purse it invented.
/// </param>
/// <param name="Creation">
/// What the session is creating, or null when it is doing neither that nor playing a party it accepted.
/// Defaulted for the same reason the party is: a session that creates nothing publishes that rather than a
/// screen that shows an unfinished party nobody is making, and a snapshot built without creation facts
/// publishes the empty screen rather than a draft with no lists in it.
/// </param>
/// <param name="Save">
/// How the session stands with its save slot, or the never-saved state when a snapshot carries no save
/// facts. Defaulted for the same reason the clock is: a session that has saved nothing publishes that
/// rather than an outcome nobody produced.
/// </param>
public readonly record struct SessionSnapshot(
    SessionComposition Composition,
    SessionMode Mode,
    double SimulationSeconds,
    ulong AdmittedSteps,
    ulong Updates,
    WorldSnapshot World,
    MovementSnapshot Movement = default,
    ClockSnapshot Clock = default,
    PartySnapshot Party = default,
    CreationSnapshot? Creation = null,
    SaveSnapshot Save = default);

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

    /// <summary>The clock object's wire name.</summary>
    public const string ClockField = "clock";

    /// <summary>The party object's wire name.</summary>
    public const string PartyField = "party";

    /// <summary>The creation object's wire name.</summary>
    public const string CreationField = "creation";

    /// <summary>The save object's wire name.</summary>
    public const string SaveField = "save";

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
            // The clock and the party are published even when the session has neither: "no clock" and "no
            // party" are facts about the session the panel shows, and a block that only appeared once the
            // ruleset supplied one would leave them indistinguishable from a projection that never asked.
            (ClockField, builder.Object(
                ("present", builder.Boolean(snapshot.Clock.Present)),
                ("date", builder.String(snapshot.Clock.Date ?? string.Empty)),
                ("time", builder.String(snapshot.Clock.Time ?? string.Empty)),
                ("daylight", builder.String(snapshot.Clock.Daylight ?? string.Empty)),
                ("elapsedDays", builder.Number(snapshot.Clock.ElapsedDays)))),
            (PartyField, builder.Object(
                ("present", builder.Boolean(snapshot.Party.Present)),
                ("members", builder.Number(snapshot.Party.Members)),
                ("coins", builder.Number(snapshot.Party.Coins)),
                ("provisions", builder.Number(snapshot.Party.Provisions)),
                ("unit", builder.String(snapshot.Party.Unit ?? string.Empty)),
                ("reputation", builder.Number(snapshot.Party.Reputation)),
                ("fame", builder.Number(snapshot.Party.Fame)),
                ("conditions", builder.String(snapshot.Party.Conditions ?? string.Empty)))),
            // Published even when nothing has moved: the motion word says which of "the world refused me"
            // and "the party has not stepped yet" the panel is looking at, and a block that only appeared
            // once something had moved would leave the two indistinguishable again.
            (MovementField, builder.Object(
                ("motion", builder.String(MotionWord(snapshot.Movement))),
                ("blocked", builder.String(WireName(snapshot.Movement.Blocked))),
                ("stepRise", builder.Number(snapshot.Movement.StepRise)),
                ("fallDistance", builder.Number(snapshot.Movement.FallDistance)),
                ("fallDamage", builder.Number(snapshot.Movement.FallDamage)))),
            // The creation screen is published in every mode for the same reason: "not creating" and
            // "creating a party nobody has finished" are different facts, and a block that only appeared
            // while the flow was live would leave a screen unable to tell them apart.
            (CreationField, Creation(builder, snapshot.Creation ?? CreationSnapshot.None)),
            // The save block is published in every mode for the same reason again: a session that cannot
            // save, one that has saved nothing yet, and one whose last save failed are three different
            // facts, and a block that only appeared after a save would leave a player unable to tell them
            // apart — which is exactly how a save that silently did nothing would look.
            (SaveField, builder.Object(
                ("available", builder.Boolean(snapshot.Save.Available)),
                ("resumed", builder.Boolean(snapshot.Save.Resumed)),
                // A snapshot built without save facts carries the default value, whose strings are null
                // rather than empty: the block publishes them as empty so a reader never sees a name that
                // is not there, exactly as the clock and party blocks do.
                ("slot", builder.String(snapshot.Save.Slot ?? string.Empty)),
                ("state", builder.String(WireName(snapshot.Save.State))),
                ("at", builder.String(snapshot.Save.At ?? string.Empty)),
                ("code", builder.String(snapshot.Save.Code ?? string.Empty)),
                ("message", builder.String(snapshot.Save.Message ?? string.Empty)))));
        return builder.Build(root);
    }

    /// <summary>Builds the creation block: where the flow stands, what it offers, and what it refused.</summary>
    /// <remarks>
    /// The lists are the flow's own options and the party's own members, sent whole so the screen decides
    /// nothing: a screen that had to work out which skills a class offers, or which attribute a score
    /// belongs to, would be evaluating the game's rules.
    /// </remarks>
    private static uint Creation(UiValueBuilder builder, CreationSnapshot creation)
    {
        List<uint> roster = [];
        foreach (CreationMemberSnapshot member in creation.Roster)
        {
            roster.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("step", builder.String(member.Step)),
                ("name", builder.String(member.Name)),
                ("race", builder.String(member.Race)),
                ("class", builder.String(member.Class)),
                ("portrait", builder.String(member.Portrait)),
                ("pool", builder.Number(member.PoolRemaining))));
        }

        List<uint> portraits = [];
        foreach (CreationPortraitSnapshot portrait in creation.Portraits)
        {
            portraits.Add(builder.Object(
                ("id", builder.String(portrait.Id)),
                ("name", builder.String(portrait.Name)),
                ("race", builder.String(portrait.Race)),
                ("selected", builder.Boolean(portrait.Selected))));
        }

        List<uint> classes = [];
        foreach (CreationClassSnapshot option in creation.Classes)
        {
            classes.Add(builder.Object(
                ("id", builder.String(option.Id)),
                ("name", builder.String(option.Name)),
                ("selected", builder.Boolean(option.Selected))));
        }

        List<uint> skills = [];
        foreach (CreationSkillSnapshot skill in creation.Skills)
        {
            skills.Add(builder.Object(
                ("id", builder.String(skill.Id)),
                ("name", builder.String(skill.Name)),
                ("state", builder.String(skill.State))));
        }

        List<uint> attributes = [];
        foreach (CreationAttributeSnapshot attribute in creation.Attributes)
        {
            attributes.Add(builder.Object(
                ("id", builder.String(attribute.Id)),
                ("name", builder.String(attribute.Name)),
                ("value", builder.Number(attribute.Value)),
                ("minimum", builder.Number(attribute.Minimum)),
                ("maximum", builder.Number(attribute.Maximum)),
                ("canRaise", builder.Boolean(attribute.CanRaise)),
                ("canLower", builder.Boolean(attribute.CanLower))));
        }

        List<uint> party = [];
        foreach (CreationPartyMemberSnapshot member in creation.Party)
        {
            party.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("name", builder.String(member.Name)),
                ("race", builder.String(member.Race)),
                ("class", builder.String(member.Class)),
                ("portrait", builder.String(member.Portrait))));
        }

        return builder.Object(
            ("active", builder.Boolean(creation.Active)),
            ("accepted", builder.Boolean(creation.Accepted)),
            ("hasDefault", builder.Boolean(creation.HasDefault)),
            ("member", builder.Number(creation.MemberIndex)),
            ("members", builder.Number(creation.MemberCount)),
            ("step", builder.String(creation.Step)),
            ("pool", builder.Number(creation.PoolRemaining)),
            ("refusalCode", builder.String(creation.RefusalCode)),
            ("refusalMessage", builder.String(creation.RefusalMessage)),
            ("roster", builder.Array([.. roster])),
            ("portraits", builder.Array([.. portraits])),
            ("classes", builder.Array([.. classes])),
            ("skills", builder.Array([.. skills])),
            ("attributes", builder.Array([.. attributes])),
            ("party", builder.Array([.. party])));
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
        SessionMode.Creating => "creating",
        SessionMode.Running => "running",
        SessionMode.Paused => "paused",
        SessionMode.Stopped => "stopped",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown session mode."),
    };

    /// <summary>The wire name for how a session stands with its save slot.</summary>
    /// <remarks>
    /// A state with no word is refused rather than published as an empty string: a panel that could not
    /// tell "saved" from a state this wire has no name for would show a save that never landed as one
    /// that did.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The state has no wire name.</exception>
    public static string WireName(SaveState state) => state switch
    {
        SaveState.Never => "none",
        SaveState.Saved => "saved",
        SaveState.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown save state."),
    };

    /// <summary>The wire name for a creation step.</summary>
    public static string WireName(CreationStep step) => step switch
    {
        CreationStep.Portrait => "portrait",
        CreationStep.Class => "class",
        CreationStep.Name => "name",
        CreationStep.Attributes => "attributes",
        CreationStep.Skills => "skills",
        CreationStep.Complete => "complete",
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown creation step."),
    };
}
