using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Interaction;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// One complete session presentation: what the session is, what mode it is in, the admitted simulation
/// it has measured so far, and every block the projection publishes, each read from the owner that holds
/// its facts. Every value here is owned by one of those owners; none of it is a placeholder for a mechanism
/// that does not exist yet.
/// </summary>
/// <remarks>
/// Every block is required and every block is a reference: a snapshot cannot be built without a reading of
/// each one, so no block reaches <see cref="SessionProjection.Build"/> as a value nobody read. A session
/// without a mechanism passes that block's own no-mechanism value (each block's <c>None</c>), which says the
/// mechanism is not there rather than showing an empty one that looks like a quiet street.
/// </remarks>
/// <param name="Composition">The compiled ruleset this session runs.</param>
/// <param name="Mode">The session's mode.</param>
/// <param name="SimulationSeconds">Admitted simulation time accumulated while running.</param>
/// <param name="AdmittedSteps">Admitted fixed steps accumulated while running.</param>
/// <param name="World">Where the party is, or an empty world when the session has no places loaded.</param>
/// <param name="Movement">
/// What the party's last admitted step did, or no facts at all when the session has no movement to report —
/// a session without a world, or one whose party has not stepped yet — so the panel says so rather than
/// claiming the way is clear.
/// </param>
/// <param name="Clock">
/// Where the session's one clock stands, or the not-known value when its ruleset composed none, so the panel
/// says it does not know the date rather than showing a date nobody kept.
/// </param>
/// <param name="Party">
/// The party's accounts and standing, or the not-known value when the session holds no party — which is what
/// content that declares neither members nor starting values gets — rather than an empty purse it invented.
/// </param>
/// <param name="Creation">
/// What the session is creating or the party it plays, or the empty screen when it is doing neither, rather
/// than a screen that shows an unfinished party nobody is making.
/// </param>
/// <param name="Save">How the session stands with its save slot, the never-saved state until a save is asked for.</param>
/// <param name="Interaction">What the party faces and what using it did, or the no-mechanism value.</param>
/// <param name="Service">What the party is doing at a service, or the no-mechanism value.</param>
/// <param name="Rest">What the party's last stop did and what going without sleep is doing to it, or the no-mechanism value.</param>
/// <param name="Conversation">What the party is saying and to whom, or the no-mechanism value.</param>
/// <param name="Combat">Who is fighting, who may act, and what the party's last order did, or the no-mechanism value.</param>
/// <param name="Progression">What the party has earned and what a level costs, or the no-owner value.</param>
/// <param name="Promotion">Which ranks the party's classes lead to and what the last rank did, or the no-ladder value.</param>
/// <param name="Skills">Each member's skills, their ceilings, and what a raise would buy, or the no-policy value.</param>
/// <param name="Magic">What the party can cast and what the last casting did, or the no-magic value.</param>
/// <param name="Alchemy">What in the pack mixes and what the last mixture did, or the no-mixtures value.</param>
/// <param name="Quests">What the party's journal holds and what its last errand did, or the no-owner value.</param>
/// <param name="Journal">The five books and what each holds, or the no-journal value.</param>
/// <param name="Map">What the party has mapped of the place it stands in, or the no-map value.</param>
/// <param name="Keys">
/// The keys the host bound its controls to, <see cref="ControlKeys.None"/> when it bound none, so a screen names
/// those controls by their buttons alone rather than by keys nobody pressed.
/// </param>
/// <param name="Equipment">What each member wears and what the pack could be worn from, or the no-figure value.</param>
public sealed record SessionSnapshot(
    SessionComposition Composition,
    SessionMode Mode,
    double SimulationSeconds,
    ulong AdmittedSteps,
    WorldSnapshot World,
    MovementSnapshot Movement,
    ClockSnapshot Clock,
    PartySnapshot Party,
    CreationSnapshot Creation,
    SaveSnapshot Save,
    InteractionSnapshot Interaction,
    ServiceSnapshot Service,
    RestSnapshot Rest,
    ConversationSnapshot Conversation,
    CombatSnapshot Combat,
    ProgressionSnapshot Progression,
    PromotionSnapshot Promotion,
    SkillsSnapshot Skills,
    MagicSnapshot Magic,
    AlchemySnapshot Alchemy,
    QuestSnapshot Quests,
    JournalSnapshot Journal,
    MapSnapshot Map,
    ControlKeys Keys,
    EquipmentSnapshot Equipment);

/// <summary>Where the party is in the world, as the panel needs it: which place, where in it, and how much of the world is known.</summary>
/// <param name="Place">The place the party is in, empty when the session has no world.</param>
/// <param name="Name">The place's display name.</param>
/// <param name="Kind">The place's kind, as the wire spells it.</param>
/// <param name="Pose">The party's position and facing in that place.</param>
/// <param name="Visited">How many places the party has visited.</param>
/// <param name="Places">How many places the world holds.</param>
/// <param name="Open">
/// Whether the place's own buildings are open at the hour the projection was built, which is a clock read
/// rather than a state anybody set. A place that keeps no hours is open, because nothing has shut it.
/// </param>
/// <param name="Hours">The hours the place keeps, empty when content clocks nothing in it.</param>
/// <param name="NextChange">When those hours next change, as a point on the game calendar, empty when nothing does.</param>
public sealed record WorldSnapshot(
    string Place,
    string Name,
    string Kind,
    PlacePose Pose,
    int Visited,
    int Places,
    bool Open = false,
    string Hours = "",
    string NextChange = "")
{
    /// <summary>The world of a session that has no places loaded.</summary>
    public static WorldSnapshot Empty => new(string.Empty, string.Empty, string.Empty, PlacePose.Origin, 0, 0);

    /// <summary>Whether the session has a world at all.</summary>
    public bool HasWorld => Places > 0;

    /// <summary>Writes the world block: which place, where in it, and how much of the world is known.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("place", builder.String(Place)),
            ("name", builder.String(Name)),
            ("kind", builder.String(Kind)),
            ("x", builder.Number(Pose.X)),
            ("y", builder.Number(Pose.Y)),
            ("z", builder.Number(Pose.Z)),
            ("yaw", builder.Number(Pose.Yaw)),
            ("visited", builder.Number(Visited)),
            ("places", builder.Number(Places)),
            // Whether the town's doors stand open is a clock read published beside the place: a shop
            // that shut at its closing hour reads shut here in the same projection that shows the hour.
            ("open", builder.Boolean(Open)),
            ("hours", builder.String(Hours)),
            ("nextChange", builder.String(NextChange)));
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

    /// <summary>The composition's field naming which start the party took: creation, scenario, or resumed.</summary>
    public const string PartyStartField = "partyStart";

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

    /// <summary>The interaction object's wire name.</summary>
    public const string InteractionField = "interaction";

    /// <summary>The service object's wire name.</summary>
    public const string ServiceField = "service";

    /// <summary>The rest object's wire name.</summary>
    public const string RestField = "rest";

    /// <summary>The conversation object's wire name.</summary>
    public const string ConversationField = "conversation";

    /// <summary>The fight object's wire name.</summary>
    public const string CombatField = "combat";

    /// <summary>The progression object's wire name.</summary>
    public const string ProgressionField = "progression";

    /// <summary>The name of the projection field the promotion block is published under.</summary>
    public const string PromotionField = "promotion";

    /// <summary>The name of the projection field the skills block is published under.</summary>
    public const string SkillsField = "skills";

    /// <summary>The name of the projection field the magic block is published under.</summary>
    public const string MagicField = "magic";

    /// <summary>The name of the projection field the alchemy block is published under.</summary>
    public const string AlchemyField = "alchemy";

    /// <summary>The name of the projection field the quests block is published under.</summary>
    public const string QuestsField = "quests";

    /// <summary>The name of the projection field the journal's books are published under.</summary>
    public const string JournalField = "journal";

    /// <summary>The name of the projection field the automap is published under.</summary>
    public const string MapField = "map";

    /// <summary>The name of the projection field the members' figures are published under.</summary>
    public const string EquipmentField = "equipment";

    /// <summary>The name of the projection field the stand-alone controls are published under.</summary>
    public const string ControlsField = "controls";

    /// <summary>Builds the projection value for a snapshot.</summary>
    /// <remarks>
    /// Each block is written by its own snapshot, which is the one place its keys are spelled; this method only
    /// says which blocks the projection carries and in what order. Every block is a value the session read from
    /// its owner, so there is no block here a snapshot was not given.
    /// </remarks>
    public static UiValue Build(SessionSnapshot snapshot)
    {
        UiValueBuilder builder = new();
        uint root = builder.Object(
            ("composition", Composition(builder, snapshot.Composition)),
            (SessionField, builder.Object(
                ("mode", builder.String(WireName(snapshot.Mode))),
                ("simulationSeconds", builder.Number(snapshot.SimulationSeconds)),
                ("admittedSteps", builder.Number(snapshot.AdmittedSteps)))),
            (WorldField, snapshot.World.Write(builder)),
            // The clock and the party are published even when the session has neither: "no clock" and "no
            // party" are facts about the session the panel shows, and a block that only appeared once the
            // ruleset supplied one would leave them indistinguishable from a projection that never asked.
            (ClockField, snapshot.Clock.Write(builder)),
            (PartyField, snapshot.Party.Write(builder)),
            // Published even when nothing has moved: the motion word says which of "the world refused me"
            // and "the party has not stepped yet" the panel is looking at, and a block that only appeared
            // once something had moved would leave the two indistinguishable again.
            (MovementField, snapshot.Movement.Write(builder)),
            // The creation screen is published in every mode for the same reason: "not creating" and
            // "creating a party nobody has finished" are different facts, and a block that only appeared
            // while the flow was live would leave a screen unable to tell them apart.
            (CreationField, snapshot.Creation.Write(builder)),
            // The save block is published in every mode for the same reason again: a session that cannot
            // save, one that has saved nothing yet, and one whose last save failed are three different
            // facts, and a block that only appeared after a save would leave a player unable to tell them
            // apart — which is exactly how a save that silently did nothing would look.
            (SaveField, snapshot.Save.Write(builder)),
            // The interaction block is published in every mode for the same reason the save block is: "this
            // session holds no interaction", "nothing is in front of the party", and "something is in front
            // of the party and out of reach" are three different facts, and a block that only appeared when
            // something was usable would leave a player unable to tell an empty room from a refused aim.
            (InteractionField, snapshot.Interaction.Write(builder)),
            // The service block is published in every mode for the same reason the interaction block is:
            // "this session holds no service mechanism", "the party stands at no counter", and "the counter
            // is shut for the night" are three different facts, and a block that only appeared at a counter
            // would leave a player unable to tell an empty street from a refused door.
            (ServiceField, snapshot.Service.Write(builder)),
            // The rest block is published in every mode for the same reason the service block is: "this
            // session holds no rest mechanism", "the party has not stopped yet", and "the party was refused a
            // night's sleep" are three different facts, and a block that only appeared after a stop would
            // leave a player unable to tell a quiet street from a refused camp.
            (RestField, snapshot.Rest.Write(builder)),
            // The conversation block is published in every mode for the same reason the rest block is:
            // "this session holds no conversation mechanism", "nobody is being spoken with", and "the
            // person has nothing to say about that" are three different facts, and a block that only
            // appeared while somebody was talking would leave a player unable to tell an empty road from a
            // topic the state withholds.
            (ConversationField, snapshot.Conversation.Write(builder)),
            // The fight block is published in every mode for the same reason the conversation block is:
            // "this session holds no fight", "nothing is hostile", and "the party is fighting and two of its
            // members are recovering" are three different facts, and a block that only appeared once
            // something was hostile would leave a player unable to tell a quiet street from a fight.
            (CombatField, snapshot.Combat.Write(builder)),
            // The progression block is published in every mode for the same reason the fight block is: "this
            // session holds no progression owner", "the party has earned nothing", and "a member has banked
            // what a level takes" are three different facts, and a block that only appeared once somebody
            // had levelled would leave a player unable to tell an unearned level from a mechanism that is
            // not there.
            (ProgressionField, snapshot.Progression.Write(builder)),
            // The promotion block is published in every mode for the same reason the progression block is:
            // "this session's ruleset stated no ladder", "no class of the party's leads anywhere", and "a
            // member rose a rank" are three different facts, and a block that only appeared once somebody had
            // been promoted would leave a screen unable to tell a party at the top of its ladder from a game
            // that has no ranks at all.
            (PromotionField, snapshot.Promotion.Write(builder)),
            // The skills block is published in every mode for the same reason the progression block is: "this
            // session's ruleset stated no skill policy", "the party holds no skills yet", and "a member's
            // blade is at the ceiling their class allows" are three different facts, and a block that only
            // appeared once somebody had spent a point would leave a screen unable to tell them apart.
            (SkillsField, snapshot.Skills.Write(builder)),
            // The magic block is published in every mode for the same reason the skills block is: "this
            // session's ruleset stated no magic policy", "nobody has learned a spell", and "a member holds a
            // spell their mastery or their pool will not pay for" are three different facts, and a block
            // that only appeared once somebody had cast would leave a screen unable to tell them apart.
            (MagicField, snapshot.Magic.Write(builder)),
            // The alchemy block is published in every mode for the same reason the magic block is: "this
            // session's ruleset stated no mixtures", "the pack holds nothing that mixes", and "a mixture was
            // refused for a mastery or for want of room" are three different facts, and a block that only
            // appeared once something had been mixed would leave a pack screen unable to tell them apart.
            (AlchemyField, snapshot.Alchemy.Write(builder)),
            // The quests block is published in every mode for the same reason the alchemy block is: "this
            // session's ruleset stated no quests", "the party has taken nothing", and "an errand was refused
            // because an objective is unmet" are three different facts, and a block that only appeared once
            // somebody had taken an errand would leave a screen unable to tell them apart.
            (QuestsField, snapshot.Quests.Write(builder)),
            // The journal block is published in every mode for the same reason the quests block is: "this
            // session's ruleset stated no journal at all", "the party has been nowhere and written nothing
            // down", and "a book no owner fills yet" are three different facts, and a block that only
            // appeared once something had been written would leave a screen unable to tell them apart.
            (JournalField, snapshot.Journal.Write(builder)),
            // The automap is published in every mode for the same reason the journal is: "this session's
            // ruleset stated no automap", "the place content carries no map for", and "the party has seen none
            // of a mapped place yet" are three different facts, and a block that only appeared once the party
            // had walked somewhere would leave a screen unable to tell them apart.
            (MapField, snapshot.Map.Write(builder)),
            // The figures are published in every mode for the same reason: "this ruleset states no figure", "the
            // member wears nothing", and "a change was refused for a skill" are three different facts.
            (EquipmentField, snapshot.Equipment.Write(builder)),
            // The controls are published in every mode for the same reason every block is: "this control would
            // be taken now", "it would be refused", and "it is bound to this key" are facts about the session, and
            // a screen that worked any of them out would be a second copy of the rule that decides them.
            (ControlsField, ControlsSnapshot.Read(snapshot).Write(builder)));
        return builder.Build(root);
    }

    /// <summary>Writes the composition block: which ruleset, which bundle, and how much content it resolved.</summary>
    /// <remarks>
    /// A composition that selected no bundle states that with no bundle identity, which the wire spells as an
    /// empty name: it is the one optional value a block here carries, and it is the composition's own fact rather
    /// than a block the snapshot was not given.
    /// </remarks>
    private static uint Composition(UiValueBuilder builder, SessionComposition composition) =>
        builder.Object(
            (RulesetField, builder.String(composition.Ruleset.Value)),
            (TitleField, builder.String(composition.Title)),
            (BundleField, builder.String(composition.Bundle is { } bundle ? bundle : string.Empty)),
            (ContentPacksField, builder.Number(composition.ContentPacks)),
            (PartyStartField, builder.String(WireName(composition.PartyStart))));

    /// <summary>The wire name for the start a session's party took.</summary>
    /// <param name="start">The start the session states.</param>
    public static string WireName(SessionPartyStart start) => start switch
    {
        SessionPartyStart.Creation => "creation",
        SessionPartyStart.Scenario => "scenario",
        SessionPartyStart.Resumed => "resumed",
        _ => throw new ArgumentOutOfRangeException(nameof(start), start, "A session's party took a start the wire has no word for."),
    };

    /// <summary>
    /// The wire name for the state the party's last admitted step left it in.
    /// </summary>
    /// <remarks>
    /// Grounded, airborne, flying, and "the party has not moved" are one word rather than a presence flag beside a
    /// grounded flag: a wire that could say grounded while also saying nothing has moved would let the
    /// panel report footing it does not know. A flying party is in the air by its own choice, which is a different
    /// fact from a party that jumped or fell, so it has its own word.
    /// </remarks>
    /// <param name="movement">The movement facts the snapshot carries.</param>
    /// <returns>The word the projection publishes for them.</returns>
    public static string MotionWord(MovementSnapshot movement) =>
        !movement.Moved ? "none" : movement.Flying ? "flying" : movement.Grounded ? "grounded" : "airborne";

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
        SessionMode.TurnBased => "turnbased",
        SessionMode.Stopped => "stopped",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown session mode."),
    };

    /// <summary>The wire name for which pacing a fight is being played in.</summary>
    public static string WireName(CombatPacing pacing) => pacing switch
    {
        CombatPacing.RealTime => "realtime",
        CombatPacing.TurnBased => "turnbased",
        _ => throw new ArgumentOutOfRangeException(nameof(pacing), pacing, "Unknown combat pacing."),
    };

    /// <summary>The wire name for which part of a paced round a fight is in.</summary>
    /// <remarks>
    /// A phase with no word is refused rather than published as an empty string: "no round is under way" has
    /// its own name, and a panel that could not tell it from a phase this wire cannot describe would show a
    /// real-time fight as a paused round.
    /// </remarks>
    public static string WireName(TurnPhase phase) => phase switch
    {
        TurnPhase.None => "none",
        TurnPhase.Action => "action",
        TurnPhase.Movement => "movement",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown turn phase."),
    };

    /// <summary>The wire name for which side of a fight an actor is on.</summary>
    public static string WireName(CombatSide side) => side switch
    {
        CombatSide.Party => "party",
        CombatSide.Opposition => "opposition",
        CombatSide.Neutral => "neutral",
        CombatSide.Ally => "ally",
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown combat side."),
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

    /// <summary>The wire name for a use.</summary>
    /// <remarks>
    /// A verb with no word is refused rather than published as an empty string for the same reason a save
    /// state is: a panel that could not tell "there is nothing to use here" from a use this wire has no name
    /// for would show a target it cannot describe as no target at all.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The verb has no wire name.</exception>
    public static string WireName(InteractionVerb verb) => verb switch
    {
        InteractionVerb.Search => "search",
        InteractionVerb.Open => "open",
        InteractionVerb.Unlock => "unlock",
        InteractionVerb.Disarm => "disarm",
        InteractionVerb.Pull => "pull",
        InteractionVerb.Talk => "talk",
        InteractionVerb.Read => "read",
        InteractionVerb.Tread => "tread",
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "Unknown interaction verb."),
    };

    /// <summary>The wire name for a stop the party asked for.</summary>
    /// <remarks>
    /// A kind with no word is refused rather than published as an empty string for the same reason a verb is:
    /// a panel that could not tell "the party has not stopped" from a stop this wire has no name for would
    /// show a command it cannot describe as no command at all.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The kind has no wire name.</exception>
    public static string WireName(RestKind kind) => kind switch
    {
        RestKind.Rest => "rest",
        RestKind.Camp => "camp",
        RestKind.WaitUntilDawn => "wait-dawn",
        RestKind.WaitAnHour => "wait-hour",
        RestKind.WaitFiveMinutes => "wait-five-minutes",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown rest kind."),
    };

    /// <summary>The wire name for why the reticle holds or refuses what the party faces.</summary>
    /// <remarks>
    /// These words are the interaction selection's own reasons, spelled for a person: "nothing is in front
    /// of me" and "the thing I am looking at is out of reach" are answers a player acts on differently, and
    /// a panel that showed both as "not usable" would be hiding the game's own answer.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The reason has no wire name.</exception>
    public static string WireName(InteractionReason reason) => reason switch
    {
        InteractionReason.Ready => "ready",
        InteractionReason.NoCandidate => "no-candidate",
        InteractionReason.OutsideQuery => "outside-query",
        InteractionReason.OutOfReach => "out-of-reach",
        InteractionReason.VisibilityUnknown => "visibility-unknown",
        InteractionReason.Occluded => "occluded",
        InteractionReason.Unavailable => "unavailable",
        InteractionReason.Locked => "locked",
        InteractionReason.InvalidTarget => "invalid-target",
        InteractionReason.StaleTarget => "stale-target",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown interaction reason."),
    };
}
