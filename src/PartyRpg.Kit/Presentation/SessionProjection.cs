using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
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
    CombatSnapshot Combat = default);

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
                ("spellPointsMax", builder.Number(snapshot.Party.SpellPointsMax)))),
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
            (CombatField, Combat(builder, snapshot.Combat)));
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
                ("price", builder.Number(offer.Price))));
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
    /// and the actors fighting them. Readiness is published as the recovery the fight holds — a length of
    /// game time — so the panel shows what the product says and never counts a cooldown down for itself. A
    /// snapshot built without fight facts carries the default value, whose lists are null rather than empty:
    /// they are published as empty so a reader never sees an actor that is not there, exactly as the rest and
    /// conversation blocks do.
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
            ("members", builder.Array([.. members])),
            ("enemies", builder.Array([.. enemies])),
            ("actor", builder.String(combat.Actor ?? string.Empty)),
            ("kind", builder.String(combat.Kind ?? string.Empty)),
            ("target", builder.String(combat.Target ?? string.Empty)),
            ("outcome", builder.String(combat.Outcome ?? string.Empty)),
            ("code", builder.String(combat.Code ?? string.Empty)),
            ("message", builder.String(combat.Message ?? string.Empty)),
            ("recoverySeconds", builder.Number(combat.RecoverySeconds)));
    }

    /// <summary>Builds one actor of a fight block: who it is, whether it may act, and how long it owes.</summary>
    private static uint Fighter(UiValueBuilder builder, CombatActorSnapshot actor) =>
        builder.Object(
            ("id", builder.String(actor.Id)),
            ("name", builder.String(actor.Name)),
            ("ready", builder.Boolean(actor.Ready)),
            ("recoverySeconds", builder.Number(actor.RecoverySeconds)),
            ("distance", builder.Number(actor.Distance)));

    /// <summary>Builds the rest block: what the last stop did, what it cost, and what sleep debt stands.</summary>
    /// <remarks>
    /// Every fact is the mechanism's own: the kind asked for, the refusal's code and sentence, where the
    /// clock went, what the larder was charged and covered, whether a night was broken, which conditions a
    /// completed sleep cleared, and when the debt of sleep next falls due. A snapshot built without rest
    /// facts carries the default value, whose strings are null rather than empty: they are published as
    /// empty so a reader never sees a name that is not there, exactly as the service and save blocks do.
    /// </remarks>
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
