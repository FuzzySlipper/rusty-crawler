using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Interaction;

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
/// <param name="Interaction">
/// What the party faces and what using it did, or the no-mechanism value when the session holds no
/// interaction at all. Defaulted for the same reason the others are: a session whose ruleset composed no
/// interaction publishes that rather than an empty reticle that looks like an empty room.
/// </param>
/// <param name="Service">
/// What the party is doing at a service, or the no-mechanism value when the session holds none. Defaulted
/// for the same reason the others are: a session whose ruleset answered no service policy publishes that
/// rather than a counter with nothing on it.
/// </param>
/// <param name="Rest">
/// What the party's last stop did and what going without sleep is doing to it, or the no-mechanism value
/// when the session holds no rest mechanism. Defaulted for the same reason the others are: a session whose
/// ruleset answered no rest policy publishes that rather than a rest that never happened.
/// </param>
/// <param name="Conversation">
/// What the party is saying and to whom, or the no-mechanism value when the session holds no conversation
/// mechanism. Defaulted for the same reason the others are: a session whose ruleset answered no dialogue
/// policy publishes that rather than an empty conversation that looks like somebody with nothing to say.
/// </param>
/// <param name="Combat">
/// Who is fighting, who may act, and what the party's last order did, or the no-mechanism value when the
/// session holds no fight. Defaulted for the same reason the others are: a session whose ruleset answered no
/// combat policy publishes that rather than a quiet street that looks like a fight nobody can see.
/// </param>
/// <param name="Progression">
/// What the party has earned and what a level costs, or the no-owner value when the session holds none.
/// Defaulted for the same reason the others are: a session whose ruleset answered no progression policy
/// publishes that rather than a party whose levels nothing could rise.
/// </param>
/// <param name="Magic">
/// What the party can cast and what the last casting did, or the no-magic value when the session holds no
/// spell policy. Defaulted for the same reason the others are: a session whose ruleset answered no magic
/// publishes that rather than a spellbook nothing could cast from.
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
    SaveSnapshot Save = default,
    InteractionSnapshot Interaction = default,
    ServiceSnapshot Service = default,
    RestSnapshot Rest = default,
    ConversationSnapshot Conversation = default,
    CombatSnapshot Combat = default,
    ProgressionSnapshot Progression = default,
    SkillsSnapshot Skills = default,
    MagicSnapshot Magic = default);

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
public readonly record struct WorldSnapshot(
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

    /// <summary>The name of the projection field the skills block is published under.</summary>
    public const string SkillsField = "skills";

    /// <summary>The name of the projection field the magic block is published under.</summary>
    public const string MagicField = "magic";

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
                ("places", builder.Number(snapshot.World.Places)),
                // Whether the town's doors stand open is a clock read published beside the place: a shop
                // that shut at its closing hour reads shut here in the same projection that shows the hour.
                ("open", builder.Boolean(snapshot.World.Open)),
                ("hours", builder.String(snapshot.World.Hours ?? string.Empty)),
                ("nextChange", builder.String(snapshot.World.NextChange ?? string.Empty)))),
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
                ("conditions", builder.String(snapshot.Party.Conditions ?? string.Empty)),
                // What the party has left to lose and to cast with: a night's sleep restores the pools, and
                // a panel that showed only food and conditions would leave the recovery invisible.
                ("hitPoints", builder.Number(snapshot.Party.HitPoints)),
                ("hitPointsMax", builder.Number(snapshot.Party.HitPointsMax)),
                ("spellPoints", builder.Number(snapshot.Party.SpellPoints)),
                ("spellPointsMax", builder.Number(snapshot.Party.SpellPointsMax)),
                // What the party carries: everything a search, a purchase, or a kill put in the one shared
                // pack, so what a corpse held is visible as a number that moved rather than only as a
                // sentence about it.
                ("pack", builder.Number(snapshot.Party.Pack)))),
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
                ("message", builder.String(snapshot.Save.Message ?? string.Empty)))),
            // The interaction block is published in every mode for the same reason the save block is: "this
            // session holds no interaction", "nothing is in front of the party", and "something is in front
            // of the party and out of reach" are three different facts, and a block that only appeared when
            // something was usable would leave a player unable to tell an empty room from a refused aim.
            (InteractionField, Interaction(builder, snapshot.Interaction)),
            // The service block is published in every mode for the same reason the interaction block is:
            // "this session holds no service mechanism", "the party stands at no counter", and "the counter
            // is shut for the night" are three different facts, and a block that only appeared at a counter
            // would leave a player unable to tell an empty street from a refused door.
            (ServiceField, Service(builder, snapshot.Service)),
            // The rest block is published in every mode for the same reason the service block is: "this
            // session holds no rest mechanism", "the party has not stopped yet", and "the party was refused a
            // night's sleep" are three different facts, and a block that only appeared after a stop would
            // leave a player unable to tell a quiet street from a refused camp.
            (RestField, Rest(builder, snapshot.Rest)),
            // The conversation block is published in every mode for the same reason the rest block is:
            // "this session holds no conversation mechanism", "nobody is being spoken with", and "the
            // person has nothing to say about that" are three different facts, and a block that only
            // appeared while somebody was talking would leave a player unable to tell an empty road from a
            // topic the state withholds.
            (ConversationField, Conversation(builder, snapshot.Conversation)),
            // The fight block is published in every mode for the same reason the conversation block is:
            // "this session holds no fight", "nothing is hostile", and "the party is fighting and two of its
            // members are recovering" are three different facts, and a block that only appeared once
            // something was hostile would leave a player unable to tell a quiet street from a fight.
            (CombatField, Combat(builder, snapshot.Combat)),
            // The progression block is published in every mode for the same reason the fight block is: "this
            // session holds no progression owner", "the party has earned nothing", and "a member has banked
            // what a level takes" are three different facts, and a block that only appeared once somebody
            // had levelled would leave a player unable to tell an unearned level from a mechanism that is
            // not there.
            (ProgressionField, Progression(builder, snapshot.Progression)),
            // The skills block is published in every mode for the same reason the progression block is: "this
            // session's ruleset stated no skill policy", "the party holds no skills yet", and "a member's
            // blade is at the ceiling their class allows" are three different facts, and a block that only
            // appeared once somebody had spent a point would leave a screen unable to tell them apart.
            (SkillsField, Skills(builder, snapshot.Skills)),
            // The magic block is published in every mode for the same reason the skills block is: "this
            // session's ruleset stated no magic policy", "nobody has learned a spell", and "a member holds a
            // spell their mastery or their pool will not pay for" are three different facts, and a block
            // that only appeared once somebody had cast would leave a screen unable to tell them apart.
            (MagicField, Magic(builder, snapshot.Magic)));
        return builder.Build(root);
    }

    /// <summary>Builds the interaction block: what is faced, what it requires, and what the last use did.</summary>
    /// <remarks>
    /// The requirements are sent as the sentences the ruleset gave them, so a locked door announces what it
    /// needs before anybody tries it, and the panel spells none of them itself. A snapshot built without
    /// interaction facts carries the default value, whose strings and list are null rather than empty: they
    /// are published as empty so a reader never sees a name that is not there, exactly as the save block does.
    /// </remarks>
    private static uint Interaction(UiValueBuilder builder, InteractionSnapshot interaction)
    {
        List<uint> requires = [];
        foreach (string requirement in interaction.Requires ?? []) requires.Add(builder.String(requirement));

        return builder.Object(
            ("available", builder.Boolean(interaction.Available)),
            ("target", builder.String(interaction.Target ?? string.Empty)),
            ("label", builder.String(interaction.Label ?? string.Empty)),
            ("verb", builder.String(interaction.Verb ?? string.Empty)),
            ("state", builder.String(interaction.State ?? string.Empty)),
            ("distance", builder.Number(interaction.Distance)),
            ("reason", builder.String(interaction.Reason ?? string.Empty)),
            ("requires", builder.Array([.. requires])),
            ("bodies", builder.Number(interaction.Bodies)),
            ("outcome", builder.String(interaction.Outcome ?? string.Empty)),
            ("code", builder.String(interaction.Code ?? string.Empty)),
            ("message", builder.String(interaction.Message ?? string.Empty)),
            ("residue", builder.String(interaction.Residue ?? string.Empty)));
    }

    /// <summary>Builds the service block: which counter the party stands at, what it offers, and what happened.</summary>
    /// <remarks>
    /// Every list is sent whole so the screen decides nothing: the shelves with their prices, the lessons
    /// with their fees, what the counter would buy from the party, and which members a lesson could go to.
    /// A snapshot built without service facts carries the default value, whose strings and lists are null
    /// rather than empty: they are published as empty so a reader never sees a name that is not there,
    /// exactly as the save and interaction blocks do.
    /// </remarks>
    private static uint Service(UiValueBuilder builder, ServiceSnapshot service)
    {
        List<uint> operations = [];
        foreach (string operation in service.Operations ?? []) operations.Add(builder.String(operation));

        List<uint> memberships = [];
        foreach (string membership in service.Memberships ?? []) memberships.Add(builder.String(membership));

        List<uint> stock = [];
        foreach (ServiceStockSnapshot offer in service.Stock ?? [])
        {
            stock.Add(builder.Object(
                ("lot", builder.String(offer.Lot)),
                ("item", builder.String(offer.Item)),
                ("name", builder.String(offer.Name)),
                ("count", builder.Number(offer.Count)),
                ("price", builder.Number(offer.Price)),
                ("sale", builder.Boolean(offer.IsSale))));
        }

        List<uint> lessons = [];
        foreach (ServiceLessonSnapshot offer in service.Lessons ?? [])
        {
            lessons.Add(builder.Object(
                ("kind", builder.String(offer.Kind)),
                ("subject", builder.String(offer.Subject)),
                ("name", builder.String(offer.Name)),
                ("amount", builder.Number(offer.Amount)),
                ("price", builder.Number(offer.Price)),
                // The rung is published because two lessons of one skill are two rows on the screen: a
                // teach command names the subject and the rung together, and the row a player pressed is
                // the row it sends back.
                ("tier", builder.Number(offer.Tier))));
        }

        List<uint> sales = [];
        foreach (ServiceSaleSnapshot offer in service.Sales ?? [])
        {
            sales.Add(builder.Object(
                ("item", builder.String(offer.Item)),
                ("definition", builder.String(offer.Definition)),
                ("name", builder.String(offer.Name)),
                ("price", builder.Number(offer.Price)),
                ("damage", builder.Number(offer.Damage)),
                ("identified", builder.Boolean(offer.Identified))));
        }

        List<uint> members = [];
        foreach (ServiceMemberSnapshot member in service.Members ?? [])
        {
            members.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("name", builder.String(member.Name))));
        }

        return builder.Object(
            ("available", builder.Boolean(service.Available)),
            ("open", builder.Boolean(service.Open)),
            ("id", builder.String(service.Id ?? string.Empty)),
            ("kind", builder.String(service.Kind ?? string.Empty)),
            ("name", builder.String(service.Name ?? string.Empty)),
            ("proprietor", builder.String(service.Proprietor ?? string.Empty)),
            ("state", builder.String(service.State ?? string.Empty)),
            ("hours", builder.String(service.Hours ?? string.Empty)),
            ("operations", builder.Array([.. operations])),
            ("memberships", builder.Array([.. memberships])),
            ("stock", builder.Array([.. stock])),
            ("lessons", builder.Array([.. lessons])),
            ("sales", builder.Array([.. sales])),
            ("members", builder.Array([.. members])),
            ("action", builder.String(service.Action ?? string.Empty)),
            ("outcome", builder.String(service.Outcome ?? string.Empty)),
            ("code", builder.String(service.Code ?? string.Empty)),
            ("message", builder.String(service.Message ?? string.Empty)),
            ("paid", builder.Number(service.Paid)),
            ("earned", builder.Number(service.Earned)),
            ("coins", builder.Number(service.Coins)));
    }

    /// <summary>Builds the conversation block: who is here, what was said, and what may be asked about.</summary>
    /// <remarks>
    /// Every list is sent whole so the screen decides nothing: the people present, the topics on offer, the
    /// topics the state withholds with the reason each is withheld, and what has been said so far. A
    /// snapshot built without conversation facts carries the default value, whose strings and lists are null
    /// rather than empty: they are published as empty so a reader never sees a name that is not there,
    /// exactly as the rest and service blocks do.
    /// </remarks>
    private static uint Conversation(UiValueBuilder builder, ConversationSnapshot conversation)
    {
        List<uint> people = [];
        foreach (ConversationPersonSnapshot person in conversation.People ?? [])
        {
            people.Add(builder.Object(
                ("id", builder.String(person.Id)),
                ("name", builder.String(person.Name)),
                ("portrait", builder.String(person.Portrait)),
                ("speaking", builder.Boolean(person.Speaking))));
        }

        List<uint> topics = [];
        foreach (ConversationTopicSnapshot topic in conversation.Topics ?? [])
        {
            topics.Add(Topic(builder, topic));
        }

        List<uint> withheld = [];
        foreach (ConversationTopicSnapshot topic in conversation.Withheld ?? [])
        {
            withheld.Add(Topic(builder, topic));
        }

        List<uint> said = [];
        foreach (ConversationLineSnapshot line in conversation.Said ?? [])
        {
            said.Add(builder.Object(
                ("speaker", builder.String(line.Speaker)),
                ("text", builder.String(line.Text)),
                ("residue", builder.String(line.Residue))));
        }

        return builder.Object(
            ("available", builder.Boolean(conversation.Available)),
            ("open", builder.Boolean(conversation.Open)),
            ("subject", builder.String(conversation.Subject ?? string.Empty)),
            ("speaker", builder.String(conversation.Speaker ?? string.Empty)),
            ("greeting", builder.String(conversation.Greeting ?? string.Empty)),
            ("people", builder.Array([.. people])),
            ("topics", builder.Array([.. topics])),
            ("withheld", builder.Array([.. withheld])),
            ("said", builder.Array([.. said])),
            ("action", builder.String(conversation.Action ?? string.Empty)),
            ("outcome", builder.String(conversation.Outcome ?? string.Empty)),
            ("code", builder.String(conversation.Code ?? string.Empty)),
            ("message", builder.String(conversation.Message ?? string.Empty)),
            ("residue", builder.String(conversation.Residue ?? string.Empty)),
            ("handoff", builder.String(conversation.Handoff ?? string.Empty)),
            ("topic", builder.String(conversation.Topic ?? string.Empty)));
    }

    /// <summary>Builds one topic of a conversation block, on offer or withheld.</summary>
    private static uint Topic(UiValueBuilder builder, ConversationTopicSnapshot topic) =>
        builder.Object(
            ("id", builder.String(topic.Id)),
            ("label", builder.String(topic.Label)),
            ("available", builder.Boolean(topic.Available)),
            ("reason", builder.String(topic.Reason)));

    /// <summary>Builds the fight block: who is in it, who may act, and what the last order did.</summary>
    /// <remarks>
    /// Both sides are sent whole so the screen decides nothing: the party's members with their readiness,
    /// and the actors fighting them. Readiness is the ready light itself — an actor may act when its recovery
    /// has elapsed and nothing has laid it out — and the recovery is sent beside it as the length of game
    /// time the fight holds, so the panel shows what the product says and never counts a cooldown down for
    /// itself. A snapshot built without fight facts carries the default value, whose lists are null rather
    /// than empty: they are published as empty so a reader never sees an actor that is not there, exactly as
    /// the rest and conversation blocks do.
    /// </remarks>
    private static uint Combat(UiValueBuilder builder, CombatSnapshot combat)
    {
        List<uint> members = [];
        foreach (CombatActorSnapshot actor in combat.Members ?? []) members.Add(Fighter(builder, actor));

        List<uint> enemies = [];
        foreach (CombatActorSnapshot actor in combat.Enemies ?? []) enemies.Add(Fighter(builder, actor));

        return builder.Object(
            ("available", builder.Boolean(combat.Available)),
            ("engaged", builder.Boolean(combat.Engaged)),
            ("opposition", builder.Number(combat.Opposition)),
            ("ready", builder.Number(combat.Ready)),
            // Which pacing this one fight is being played in, and the round it is in: a panel that could not
            // tell a real-time fight from a paced one could not say why the world is waiting for it.
            ("pacing", builder.String(WireName(combat.Pacing))),
            ("turn", Turn(builder, combat.Turn)),
            ("members", builder.Array([.. members])),
            ("enemies", builder.Array([.. enemies])),
            ("actor", builder.String(combat.Actor ?? string.Empty)),
            ("kind", builder.String(combat.Kind ?? string.Empty)),
            ("target", builder.String(combat.Target ?? string.Empty)),
            ("outcome", builder.String(combat.Outcome ?? string.Empty)),
            ("code", builder.String(combat.Code ?? string.Empty)),
            ("message", builder.String(combat.Message ?? string.Empty)),
            ("recoverySeconds", builder.Number(combat.RecoverySeconds)),
            ("resolved", builder.Boolean(combat.Resolved)),
            ("hit", builder.Boolean(combat.Hit)),
            ("chance", builder.Number(combat.Chance)),
            ("damageRolled", builder.Number(combat.DamageRolled)),
            ("damage", builder.Number(combat.Damage)),
            ("damageKind", builder.String(combat.DamageKind ?? string.Empty)),
            ("resistance", builder.String(combat.Resistance ?? string.Empty)),
            ("condition", builder.String(combat.Condition ?? string.Empty)),
            ("targetDown", builder.Boolean(combat.TargetDown)),
            ("byParty", builder.Boolean(combat.ByParty)));
    }

    /// <summary>
    /// Builds the round a paced fight is in: the phase, whose turn it is, and the order actors act in.
    /// </summary>
    /// <remarks>
    /// Every number here is the pacing's own — a length of game time, never a countdown the screen runs — and
    /// the order is the fight's own reading of each actor's recovery, so a panel that shows a member due in
    /// two seconds is showing what the fight holds rather than what the panel worked out. A snapshot built
    /// without a round publishes zeros and no phase, which is what the real-time pacing is.
    /// </remarks>
    private static uint Turn(UiValueBuilder builder, CombatTurnSnapshot? turn)
    {
        List<uint> order = [];
        foreach (TurnOrderActorSnapshot actor in turn?.Order ?? []) order.Add(Ordered(builder, actor));

        return builder.Object(
            ("phase", builder.String(turn is { } paced ? WireName(paced.Phase) : string.Empty)),
            ("round", builder.Number(turn?.Round ?? 0)),
            ("actor", builder.String(turn?.Actor ?? string.Empty)),
            ("actorName", builder.String(turn?.ActorName ?? string.Empty)),
            ("playerTurn", builder.Boolean(turn?.PlayerTurn ?? false)),
            ("dueSeconds", builder.Number(turn?.DueSeconds ?? 0)),
            ("roundSeconds", builder.Number(turn?.RoundSeconds ?? 0)),
            ("elapsedSeconds", builder.Number(turn?.ElapsedSeconds ?? 0)),
            ("movementSeconds", builder.Number(turn?.MovementSeconds ?? 0)),
            ("last", builder.String(turn?.Last ?? string.Empty)),
            ("order", builder.Array([.. order])));
    }

    /// <summary>Builds one actor of the order a paced round acts in.</summary>
    private static uint Ordered(UiValueBuilder builder, TurnOrderActorSnapshot actor) =>
        builder.Object(
            ("id", builder.String(actor.Id)),
            ("name", builder.String(actor.Name)),
            ("side", builder.String(WireName(actor.Side))),
            ("remainingSeconds", builder.Number(actor.RemainingSeconds)),
            ("ready", builder.Boolean(actor.Ready)),
            ("canAct", builder.Boolean(actor.CanAct)),
            ("waiting", builder.Boolean(actor.Waiting)),
            ("current", builder.Boolean(actor.Current)));

    /// <summary>Builds one actor of a fight block: who it is, whether it may act, and how long it owes.</summary>
    private static uint Fighter(UiValueBuilder builder, CombatActorSnapshot actor) =>
        builder.Object(
            ("id", builder.String(actor.Id)),
            ("name", builder.String(actor.Name)),
            ("ready", builder.Boolean(actor.Ready)),
            ("recoverySeconds", builder.Number(actor.RecoverySeconds)),
            ("distance", builder.Number(actor.Distance)),
            ("hitPoints", builder.Number(actor.HitPoints)),
            ("hitPointsMax", builder.Number(actor.HitPointsMax)),
            ("conditions", builder.String(actor.Conditions ?? string.Empty)),
            ("down", builder.Boolean(actor.Down)),
            ("activity", builder.String(actor.Activity ?? string.Empty)));

    /// <summary>Builds the progression block: what each member has earned and what a level would cost.</summary>
    /// <remarks>
    /// Every member is sent whole so the screen decides nothing: the level, the experience banked, the
    /// points held, the experience the curve takes for the next level, and the fee the counter the party
    /// stands at would charge for it. A snapshot built without progression facts carries the default value,
    /// whose list is null rather than empty: it is published as empty so a reader never sees a member that
    /// is not there, exactly as the fight and rest blocks do.
    /// </remarks>
    private static uint Progression(UiValueBuilder builder, ProgressionSnapshot progression)
    {
        List<uint> members = [];
        foreach (ProgressionMemberSnapshot member in progression.Members ?? [])
        {
            members.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("member", builder.String(member.Member)),
                ("name", builder.String(member.Name)),
                ("level", builder.Number(member.Level)),
                ("experience", builder.Number(member.Experience)),
                ("skillPoints", builder.Number(member.SkillPoints)),
                ("nextLevel", builder.Number(member.NextLevel)),
                ("fee", builder.Number(member.Fee)),
                ("cap", builder.Number(member.Cap))));
        }

        return builder.Object(
            ("available", builder.Boolean(progression.Available)),
            ("members", builder.Array([.. members])),
            ("outcome", builder.String(progression.Outcome ?? string.Empty)),
            ("source", builder.String(progression.Source ?? string.Empty)),
            ("earned", builder.Number(progression.Earned)),
            ("code", builder.String(progression.Code ?? string.Empty)),
            ("message", builder.String(progression.Message ?? string.Empty)));
    }

    /// <summary>Builds the skills block: each member's skills, their ceilings, and what a raise would buy.</summary>
    /// <remarks>
    /// Every row is sent whole — the skill, its block, the level, the rung's own word, the ceiling level and
    /// the ceiling rung's word, what the next level would cost and the sentence that refuses it — so the
    /// screen reads a plan rather than computing one. A snapshot built without skill facts carries the
    /// default value, whose list is null rather than empty: it is published as empty so a reader never sees
    /// a member that is not there, exactly as the progression and fight blocks do.
    /// </remarks>
    private static uint Skills(UiValueBuilder builder, SkillsSnapshot skills)
    {
        List<uint> members = [];
        foreach (SkillMemberSnapshot member in skills.Members ?? [])
        {
            List<uint> rows = [];
            foreach (SkillRowSnapshot row in member.Skills)
            {
                rows.Add(builder.Object(
                    ("skill", builder.String(row.Skill)),
                    ("block", builder.String(row.Block)),
                    ("level", builder.Number(row.Level)),
                    ("tier", builder.String(row.Tier)),
                    ("ceilingLevel", builder.Number(row.CeilingLevel)),
                    ("ceilingTier", builder.String(row.CeilingTier)),
                    ("pointsSpent", builder.Number(row.PointsSpent)),
                    // What the next point would reach, published rather than added up by the screen: a panel
                    // that showed "raise to level 5" would otherwise be doing the ruleset's arithmetic.
                    ("reached", builder.Number(row.Reached)),
                    ("cost", builder.Number(row.Cost)),
                    ("refusal", builder.String(row.Refusal))));
            }

            members.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("member", builder.String(member.Member)),
                ("name", builder.String(member.Name)),
                ("class", builder.String(member.Class)),
                ("rank", builder.Number(member.Rank)),
                ("skills", builder.Array([.. rows]))));
        }

        return builder.Object(
            ("available", builder.Boolean(skills.Available)),
            ("members", builder.Array([.. members])),
            ("outcome", builder.String(skills.Outcome ?? string.Empty)),
            ("member", builder.String(skills.Member ?? string.Empty)),
            ("skill", builder.String(skills.Skill ?? string.Empty)),
            ("level", builder.Number(skills.Level)),
            ("cost", builder.Number(skills.Cost)),
            ("code", builder.String(skills.Code ?? string.Empty)),
            ("message", builder.String(skills.Message ?? string.Empty)));
    }

    /// <summary>Builds the rest block: what the last stop did, what it cost, and what sleep debt stands.</summary>
    /// <remarks>
    /// Every fact is the mechanism's own: the kind asked for, the refusal's code and sentence, where the
    /// clock went, what the larder was charged and covered, whether a night was broken, which conditions a
    /// completed sleep cleared, and when the debt of sleep next falls due. A snapshot built without rest
    /// facts carries the default value, whose strings are null rather than empty: they are published as
    /// empty so a reader never sees a name that is not there, exactly as the service and save blocks do.
    /// </remarks>
    /// <summary>Builds the magic block: each member's spellbook, what a casting costs, and what the last one did.</summary>
    /// <remarks>
    /// Every row and every target is sent whole so the screen decides nothing: which spells a member knows,
    /// what each costs that caster, what each is aimed at, and which actors a casting could name with the
    /// side each is on. A snapshot built without magic facts carries the default value, whose lists are null
    /// rather than empty: they are published as empty so a reader never sees a name that is not there,
    /// exactly as the service and skills blocks do.
    /// </remarks>
    private static uint Magic(UiValueBuilder builder, MagicSnapshot magic)
    {
        List<uint> members = [];
        foreach (SpellMemberSnapshot member in magic.Members ?? [])
        {
            List<uint> spells = [];
            foreach (SpellRowSnapshot spell in member.Spells ?? [])
            {
                spells.Add(builder.Object(
                    ("spell", builder.String(spell.Spell)),
                    ("name", builder.String(spell.Name)),
                    ("school", builder.String(spell.School)),
                    ("tier", builder.String(spell.Tier)),
                    ("tierRung", builder.Number(spell.TierRung)),
                    ("cost", builder.Number(spell.Cost)),
                    ("targeting", builder.String(spell.Targeting)),
                    ("effect", builder.String(spell.Effect))));
            }

            members.Add(builder.Object(
                ("index", builder.Number(member.Index)),
                ("member", builder.String(member.Member)),
                ("name", builder.String(member.Name)),
                ("class", builder.String(member.Class)),
                ("spellPoints", builder.Number(member.SpellPoints)),
                ("spellPointsMax", builder.Number(member.SpellPointsMax)),
                ("quickSpell", builder.String(member.QuickSpell)),
                ("quickSpellName", builder.String(member.QuickSpellName)),
                ("spells", builder.Array([.. spells]))));
        }

        List<uint> targets = [];
        foreach (SpellTargetSnapshot target in magic.Targets ?? [])
        {
            targets.Add(builder.Object(
                ("target", builder.String(target.Target)),
                ("name", builder.String(target.Name)),
                ("side", builder.String(target.Side))));
        }

        return builder.Object(
            ("available", builder.Boolean(magic.Available)),
            ("members", builder.Array([.. members])),
            ("targets", builder.Array([.. targets])),
            ("outcome", builder.String(magic.Outcome ?? string.Empty)),
            ("member", builder.Number(magic.Member)),
            ("caster", builder.String(magic.Caster ?? string.Empty)),
            ("spell", builder.String(magic.Spell ?? string.Empty)),
            ("cost", builder.Number(magic.Cost)),
            ("target", builder.String(magic.Target ?? string.Empty)),
            ("effect", builder.String(magic.Effect ?? string.Empty)),
            ("code", builder.String(magic.Code ?? string.Empty)),
            ("message", builder.String(magic.Message ?? string.Empty)));
    }

    private static uint Rest(UiValueBuilder builder, RestSnapshot rest) =>
        builder.Object(
            ("available", builder.Boolean(rest.Available)),
            ("kind", builder.String(rest.Kind ?? string.Empty)),
            ("outcome", builder.String(rest.Outcome ?? string.Empty)),
            ("code", builder.String(rest.Code ?? string.Empty)),
            ("message", builder.String(rest.Message ?? string.Empty)),
            ("from", builder.String(rest.From ?? string.Empty)),
            ("to", builder.String(rest.To ?? string.Empty)),
            ("elapsedSeconds", builder.Number(rest.ElapsedSeconds)),
            ("charged", builder.Number(rest.Charged)),
            ("covered", builder.Number(rest.Covered)),
            ("unit", builder.String(rest.Unit ?? string.Empty)),
            ("interrupted", builder.Boolean(rest.Interrupted)),
            ("recovered", builder.Boolean(rest.Recovered)),
            ("restored", builder.Number(rest.Restored)),
            ("cleared", builder.String(rest.Cleared ?? string.Empty)),
            ("shortage", builder.String(rest.Shortage ?? string.Empty)),
            ("tired", builder.Boolean(rest.Tired)),
            ("fatigueDue", builder.String(rest.FatigueDue ?? string.Empty)),
            ("fatigueLanded", builder.Number(rest.FatigueLanded)));

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
        InteractionVerb.Pull => "pull",
        InteractionVerb.Talk => "talk",
        InteractionVerb.Read => "read",
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
