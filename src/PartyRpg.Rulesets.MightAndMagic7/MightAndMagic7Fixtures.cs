using System.Globalization;
using System.Text;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What using a fixture does here: the steps of the map event it raises, interpreted for the instructions
/// this game's fixtures use, as effects on the party's own owners.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a script machine.</b> A fixture's event is a short program of the donor's own instructions — compare
/// a variable and jump, add to one, print a line, choose who the next steps act on — and the fixtures of this
/// game use a small part of that language: wells, fountains, obelisks, signs, trees and shrines compare and
/// write a handful of variables. Those are interpreted here, each as a change through the owner that already
/// keeps the thing it names (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:317-357</c> for comparison and
/// assignment; <c>src/Engine/Objects/Character.cpp:3594-3989</c>, <c>3990-4589</c>, <c>4590-5167</c> and
/// <c>5168-5689</c> for what comparing, setting, adding to and subtracting from each variable means): a note is the knowledge owner's, a quest bit and a party bit are records, coin
/// is the purse's, an item the shared pack's, hit and spell points a member's resources, an attribute a
/// member's attributes, a condition a member's conditions, and a temporary resistance the running effects
/// a ward leaves. Every other instruction and every other variable is refused by name the moment a run
/// reaches it, before anything is changed; which ones, and how many of the shipped steps each covers, is in
/// the ruleset README.
/// </para>
/// <para>
/// <b>Judged, then settled.</b> A run walks the steps against what the party and the fixture hold, writing into
/// an overlay of its own so a later comparison reads an earlier step's write, and collects what it would do.
/// Only when it reaches its end without a refusal is anything applied, so a well that would refill the party
/// and then reach an instruction this game does not interpret changes nothing and says which instruction.
/// </para>
/// <para>
/// <b>What a place keeps.</b> The donor's map variables — a well's charges, a shrine's once-a-week bit, the
/// count of levers a room's puzzle has pulled — belong to the map, not to the thing that wrote them (OpenEnroth
/// <c>src/Engine/Engine.h:62-65</c>, one array per loaded map, saved with the map's delta in
/// <c>src/Engine/Snapshots/CompositeSnapshots.cpp:338</c>). They are the values the interaction ledger keeps
/// for the place, under <c>map-variable:</c> and the slot, so every fixture of the place reads and writes the
/// same ones; when each timer of the place last ran is kept beside them under <c>timer:</c>, the event and
/// the step. A use reads them from its context and states what it changed in its outcome; the ledger carries
/// them in the save, and forgets them when the clock restores the place — the donor re-reads a respawned
/// map's whole delta, its variables with it (<c>src/Engine/Graphics/Indoor.cpp:313-319</c>). The fixture's own
/// state word is only what its last use left: <c>used</c>, or <c>read</c> for a sign.
/// </para>
/// <para>
/// <b>A move leads the party away.</b> A step that moves the party to another place names the travel link the
/// importer read it as, and the run ends there with that journey in its outcome: the kit records the use where it was
/// made and the world takes the journey through its one transition path, so a cave mouth, a shrine and a plate in the
/// floor travel exactly as a walk-in does. The move reached is whichever the run's own branches reach — a barrow's
/// door compares a map variable, a shrine a quest bit — so a move behind a condition is taken only when it holds. The
/// donor shows an entry picture and waits for a confirmation before a move that names a house or a picture, and runs
/// on after a move that does not (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:207-252</c>); this build asks for
/// no confirmation and runs nothing after the move, because the party has left: approximate. A move within the place
/// sets the party down where it names and the run goes on, as the donor's does; a move that names no position moves
/// nobody, as the donor's (<c>:124-134</c>).
/// </para>
/// <para>
/// <b>Who a step acts on.</b> The donor starts a use's run on the active character
/// (<c>src/Engine/Evt/EvtInterpreter.cpp:623</c>) and lets a step choose one by position, the whole party, or
/// one at random (<c>src/Engine/Evt/EvtInterpreter.cpp:101-115</c>). This build selects no active character
/// (#8659), so the active one is read as the first member able to act. A write to something the party holds
/// once — a coin, a note, a bit — is made once whoever is chosen; the donor makes it once per chosen
/// character, which pays a whole-party gold step four times, and that is not kept.
/// </para>
/// <para>
/// <b>A person's topic runs the global program through the same run.</b> A topic of a person's slot raises the
/// global program's event of its number (OpenEnroth <c>src/GUI/UI/NPCTopics.cpp:662-666</c>); it is run here as
/// one use of the placement the person stands at (<see cref="Speak"/>), with the same steps, the same refusals and
/// the same settling a fixture's run has, and a message it shows is what the person answers
/// (<c>src/Engine/Evt/EvtInterpreter.cpp:417-423</c>). Whether the topic is offered at all is the event's own offer
/// check, run by the same run in the donor's offer mode (<see cref="Offers"/>,
/// <c>src/Engine/Evt/EvtInterpreter.cpp:152-181</c>, <c>:632-655</c>): every member is asked each comparison, a
/// <c>set-can-show-dialog-item</c> step states the answer, and an event that states none is offered. A run that
/// reaches a step this game does not interpret changes nothing and the person still says what the run had said,
/// with the refusal as the residue. The donor offers at most four scripted topics (<c>NPCTopics.cpp:603</c>); this
/// build offers every one whose check allows it: ours.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Fixtures
{
    /// <summary>The placement kind the importer writes a fixture as.</summary>
    internal const string FixturePlacementKind = "fixture";

    /// <summary>The placement field that names the event a fixture or a decoration raises.</summary>
    internal const string EventField = "eventId";

    /// <summary>The placement field that names the event a house's door raises, which a house's own use runs.</summary>
    internal const string SourceEventField = "sourceEvent";

    /// <summary>The placement field a fixture's region model name is written under.</summary>
    internal const string ModelNameField = "sourceModelName";

    /// <summary>The target kind a fixture is.</summary>
    internal const string TargetKind = "fixture";

    /// <summary>The target kind a person's word is: the global event a topic raises, run as a use of their placement.</summary>
    internal const string SpokenTargetKind = "spoken";

    /// <summary>The state word a use a person's word raised reads as, which nothing records.</summary>
    internal const string SpokenState = "spoken";

    /// <summary>The placement kind the importer writes a floor trigger as: the event a place's pressure plates raise.</summary>
    internal const string FloorTriggerPlacementKind = "floor-trigger";

    /// <summary>Which owner a note a fixture taught is attributed to.</summary>
    internal const string Source = "fixture";

    /// <summary>
    /// How many steps one use may run before it is refused as a run that does not end.
    /// </summary>
    /// <remarks>
    /// Every shipped fixture's program ends well inside this: the longest is a few dozen steps, and a loop
    /// through a random jump picks a way out on the first draw. A run that walks further than this is a program
    /// that loops on a state nothing changes, which is a refusal rather than a hung update.
    /// </remarks>
    internal const int StepLimit = 256;

    /// <summary>The seed a random jump draws under, so a draw is the engine's keyed one and repeatable.</summary>
    internal const ulong RollSeed = 0x5C1E_7A11_5EE9_0009;

    /// <summary>The scope a random jump draws under.</summary>
    internal const string RollScope = "mm7.fixture";

    /// <summary>The state word a used fixture holds when it keeps nothing else.</summary>
    internal const string UsedState = "used";

    /// <summary>The state word a read sign holds.</summary>
    internal const string ReadState = "read";

    /// <summary>How many map variables a place has: the donor's array is 75 bytes (OpenEnroth <c>src/Engine/Engine.h:63</c>).</summary>
    internal const int MapVariableSlots = 75;

    /// <summary>The largest figure a map variable holds, which is a byte's (OpenEnroth <c>src/Engine/Engine.h:63</c>).</summary>
    internal const int MapVariableLimit = 255;

    /// <summary>The prefix of the name a place keeps one of its map variables under.</summary>
    internal const string VariablePrefix = "map-variable:";

    /// <summary>The prefix of the name a place keeps when one of its timers last ran under.</summary>
    internal const string TimerPrefix = "timer:";

    /// <summary>The prefix of the name a place keeps when one of the event counters was last set under.</summary>
    internal const string CounterPrefix = "counter:";

    /// <summary>How many event counters there are (OpenEnroth <c>src/Engine/Objects/Character.cpp:3934-3951</c>, ten).</summary>
    internal const int Counters = 10;

    /// <summary>The source field a placement the map's own actor records stand carries, whose index is the actor's.</summary>
    internal const string ActorSourceField = "actors";

    /// <summary>The placement field a door's own id is written under, which a door step names it by.</summary>
    internal const string DoorIdField = "doorId";

    /// <summary>The most a stored base resistance reaches, which is a byte's (OpenEnroth <c>src/Engine/Objects/Character.cpp:4788-4817</c>).</summary>
    internal const int ResistanceLimit = 255;

    /// <summary>The prefix of the name a place keeps whether its events turned one of its groups of creatures hostile under.</summary>
    internal const string HostileGroupPrefix = "hostile-group:";

    /// <summary>
    /// The creature attribute a group flag step sets to turn a group against the party: the donor's aggressor bit
    /// (OpenEnroth <c>src/Engine/Objects/ActorEnums.h:109</c>), the only one this game's events toggle.
    /// </summary>
    internal const long AggressorFlag = 0x0008_0000;

    /// <summary>
    /// The creature attribute a group flag step sets to hide a group, and clears to show it again: the donor's
    /// <c>ACTOR_UNKNOW11</c> (OpenEnroth <c>src/Engine/Objects/ActorEnums.h:106</c>), the bit a map's own record holds a
    /// creature hidden by (<c>src/Engine/Objects/Actor.cpp:3823-3851</c>, MMExtension <c>Scripts/Core/ConstAndBits.lua:125</c>
    /// names it <c>Invisible</c>).
    /// </summary>
    internal const long HiddenFlag = 0x0001_0000;

    /// <summary>The prefix of the name a place keeps whether its events hid one of its groups of creatures under.</summary>
    internal const string HiddenGroupPrefix = "hidden-group:";

    /// <summary>The face bit that hides a face group, which is all a player would see change (<c>src/Engine/Graphics/FaceEnums.h:21</c>).</summary>
    private const long InvisibleFaceBit = 0x0000_2000;

    /// <summary>The face bit that lets a party pass through a face group (<c>src/Engine/Graphics/FaceEnums.h:39</c>).</summary>
    private const long PassableFaceBit = 0x2000_0000;

    /// <summary>
    /// The face bit that makes a face group fluid (<c>src/Engine/Graphics/FaceEnums.h:12</c>), which a player cannot act
    /// on here.
    /// </summary>
    /// <remarks>
    /// A fluid face is not the water a party drowns in: the donor drowns a party only on a region's water squares
    /// (<c>src/Engine/Graphics/Outdoor.cpp:1321-1335</c>, <c>:1389-1395</c>) and reads a fluid face for its footsteps, a
    /// splash, a corpse sinking and a night's food outdoors (<c>src/Engine/Graphics/Indoor.cpp:1499</c>,
    /// <c>Outdoor.cpp:355-362</c>, <c>:854</c>, <c>:1417</c>, <c>:1617</c>). This build plays no sound and draws no
    /// splash, prices a camp by its place, and reads its imported fluid faces for nothing, so a face group an event
    /// turns fluid is presentation and is passed over like one made invisible.
    /// </remarks>
    private const long FluidFaceBit = 0x0000_0010;

    /// <summary>
    /// The steps that change only what a player sees or hears: a texture, a sprite, a sound, a character's
    /// portrait reacting, an interior light, a movie. The product draws no world and plays no sound, so a run passes
    /// over them and the event's other steps still run.
    /// </summary>
    /// <remarks>
    /// The donor's player hangs three effects on a movie's name rather than on a step of the program — the alignment
    /// the party takes when the arbiter's movie plays, and a week's rest and healing after the movie of the crossing
    /// to Harmondale (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:288-300</c>). Those are the executable's, not
    /// the data's: this build's light or dark path is the class a promotion chooses, and the crossing's week is not
    /// kept. Ours.
    /// </remarks>
    internal static readonly IReadOnlySet<string> PresentationSteps = new HashSet<string>(StringComparer.Ordinal)
    {
        "set-texture", "set-sprite", "play-sound", "character-animation", "toggle-indoor-light", "show-movie",
    };

    /// <summary>The residue a step that moves geometry leaves, which collision does not follow in this build.</summary>
    internal const string CollisionResidue =
        "Collision does not move in this build: a door's polygons and a face group's solidity stay where the place was admitted, so the way it opens cannot be walked yet (#8594).";

    private readonly MightAndMagic7MapEvents _events;
    private readonly Func<PartyKnowledge?> _knowledge;
    private readonly MightAndMagic7SpellEffects? _effects;
    private readonly IRandomService? _random;
    private readonly GameDuration _bonusLasts;
    private readonly MightAndMagic7Loot? _loot;
    private readonly MightAndMagic7Spells? _spells;
    private readonly Func<PartyProgression?> _progression;
    private readonly Func<string, ConversationPerson?> _people;
    private readonly Func<PlaceId, IReadOnlyList<PlaceActor>?> _actors;
    private readonly Func<PartyJournal?> _journal;
    private readonly Func<string, SpokenTopic?> _topics;
    private readonly Func<int, bool> _greetings;
    private readonly Func<PlaceId, PlacePopulation?> _population;
    private readonly Func<string, IReadOnlyList<int>> _starting;
    private long _summoned;

    /// <summary>Creates this game's fixtures over the map events content carries.</summary>
    /// <param name="events">The map events and the discovery table.</param>
    /// <param name="knowledge">
    /// The party's knowledge, asked when a step compares a note: it is read through a call because the owner is
    /// composed after the world on the path that creates a party.
    /// </param>
    /// <param name="effects">The running effects a temporary resistance is left in, or null when this session keeps none.</param>
    /// <param name="random">The engine's random service a random jump draws from, or null when the product has none.</param>
    /// <param name="tuning">This game's tuning, which states how long a fixture's temporary bonus lasts.</param>
    /// <param name="loot">This game's loot, which an item gift drawn at a treasure level is drawn from.</param>
    /// <param name="spells">This game's magic, which a spell a fixture casts is rolled by.</param>
    /// <param name="progression">The one writer of experience and skill points, read through a call for the same reason the knowledge owner is.</param>
    /// <param name="people">Who one of the people table's ids is, which a step calling somebody over reads.</param>
    /// <param name="actors">
    /// What a place's population holds and whether each is down, which a step counting the dead reads
    /// (<see cref="ActorsOf"/>); null for a place it cannot answer for, and absent in a session that keeps none.
    /// </param>
    /// <param name="journal">The party's journal, which a step writing a history line writes into, read through a call.</param>
    /// <param name="topics">
    /// What a topic a person's word raised is — its label, its table words and the global event it raises — which the
    /// conversation answers (<see cref="MightAndMagic7Conversation.SpokenTopic"/>); absent in a session that speaks with nobody.
    /// </param>
    /// <param name="greetings">Whether the greeting table carries a row, which a step changing a person's greeting names.</param>
    /// <param name="population">
    /// The live population of a place, which a summoning puts its creatures into; null for a place the party does not
    /// stand in now, and absent in a session that keeps none.
    /// </param>
    /// <param name="starting">
    /// The items a person's own map records start them with (<see cref="MightAndMagic7Conversation.StartingOf"/>), which an
    /// item step can take from them; absent in a session that speaks with nobody.
    /// </param>
    internal MightAndMagic7Fixtures(
        MightAndMagic7MapEvents events,
        Func<PartyKnowledge?>? knowledge = null,
        MightAndMagic7SpellEffects? effects = null,
        IRandomService? random = null,
        TuningProfile? tuning = null,
        MightAndMagic7Loot? loot = null,
        MightAndMagic7Spells? spells = null,
        Func<PartyProgression?>? progression = null,
        Func<string, ConversationPerson?>? people = null,
        Func<PlaceId, IReadOnlyList<PlaceActor>?>? actors = null,
        Func<PartyJournal?>? journal = null,
        Func<string, SpokenTopic?>? topics = null,
        Func<int, bool>? greetings = null,
        Func<PlaceId, PlacePopulation?>? population = null,
        Func<string, IReadOnlyList<int>>? starting = null)
    {
        _starting = starting ?? (_ => []);
        _topics = topics ?? (_ => null);
        _greetings = greetings ?? (_ => false);
        _population = population ?? (_ => null);
        _actors = actors ?? (_ => null);
        _journal = journal ?? (() => null);
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _knowledge = knowledge ?? (() => null);
        _effects = effects;
        _random = random;
        _loot = loot;
        _spells = spells;
        _progression = progression ?? (() => null);
        _people = people ?? (_ => null);
        TuningProfile read = tuning ?? MightAndMagic7Tuning.Read(null);
        _bonusLasts = GameDuration.FromHours(read.Whole(MightAndMagic7Tuning.FixtureBonusHours));
    }

    /// <summary>The map events this game's fixtures run.</summary>
    internal MightAndMagic7MapEvents Events => _events;

    /// <summary>What a placement offers when its use raises a map event, or null when it raises none.</summary>
    /// <remarks>
    /// A fixture placement is the importer's, standing where the faces raising its event are; a decoration
    /// raising an event is the level's own. Both are the same target: something whose use runs the event. A
    /// sign — a fixture whose region model the level names as one — is read rather than worked, and its name is
    /// what its event's hint says.
    /// </remarks>
    /// <param name="request">The placement and its recorded state.</param>
    /// <param name="reach">How far from it the party may stand.</param>
    internal InteractionTargetDefinition? Describe(InteractionTargetRequest request, double reach)
    {
        PlacementDefinition placement = request.Placement;
        bool fixture = string.Equals(placement.Content.Kind, FixturePlacementKind, StringComparison.Ordinal);
        bool decoration = string.Equals(placement.Content.Kind, MightAndMagic7Interaction.DecorationPlacementKind, StringComparison.Ordinal);
        bool trigger = string.Equals(placement.Content.Kind, FloorTriggerPlacementKind, StringComparison.Ordinal);
        if (!fixture && !decoration && !trigger) return null;
        if (placement.Source.GetInt32(EventField) is not { } eventId || eventId == 0) return null;

        string label = _events.Find(request.Place, eventId)?.Label ?? string.Empty;
        string name = label.Length > 0
            ? label
            : placement.Source.GetString(MightAndMagic7Interaction.DecorationNameField) is { Length: > 0 } decorationName
                ? $"A fixture ({decorationName})"
                : "A fixture";
        // A floor trigger is trodden on rather than aimed at: the plates' reaches raise it, and the reticle never
        // offers it.
        return new InteractionTargetDefinition(
            new InteractionTargetKind(TargetKind),
            name,
            trigger ? InteractionVerb.Tread : IsSign(placement) ? InteractionVerb.Read : InteractionVerb.Pull,
            reach,
            request.State);
    }

    /// <summary>Runs the event a fixture raises and answers with what it did, or why it did nothing.</summary>
    /// <param name="target">The fixture, as described.</param>
    /// <param name="context">The place, the placement, the party and the clock.</param>
    internal InteractionOutcome Use(InteractionTargetDefinition target, InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        int eventId = context.Placement.Source.GetInt32(EventField) ?? 0;
        if (_events.Find(context.Place, eventId) is not { } mapEvent)
        {
            return InteractionOutcome.Refused(new Refusal(
                MightAndMagic7Codes.FixtureEventMissing,
                $"{target.Name} raises map event {eventId} of place '{context.Place}', and the loaded content carries no such event, so there is nothing to run."));
        }

        Run run = new(this, mapEvent, target, context);
        if (Execute(run, mapEvent, target, context) is { } refusal) return InteractionOutcome.Refused(refusal);
        return run.Settle();
    }

    /// <summary>
    /// Runs a house's own event when the party uses the house, or answers null when the house's event only opens it and
    /// the use is the ordinary one of speaking with whoever keeps it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor runs a clicked face's event whatever it holds (OpenEnroth <c>src/Engine/Graphics/Viewport.cpp:300-320</c>),
    /// and a house's door is such a face: its event opens the house with a <c>speak-in-house</c> step and runs on
    /// (<c>src/Engine/Evt/EvtInterpreter.cpp:189-198</c>). So the house's use runs it — its comparisons decide whether the
    /// party is let in at all, a branch that reaches a move takes the party there instead (the Small House behind the
    /// doors of Celeste and The Pit), and its writes are settled like any run's. The step opens the house it names:
    /// the people of the placement of that house in this place, which is the house used unless the event names another
    /// (the arbiter's door opens one of three houses by what the party has done).
    /// </para>
    /// <para>
    /// A run that reaches no house step and no move keeps the party outside (<see cref="InteractionOutcome.KeptOut"/>)
    /// with what the run said. A run that meets a step this game does not interpret settles nothing and the house opens
    /// as it did before the event was run, with the refusal as the residue: the door is not barred by a step this build
    /// cannot read.
    /// </para>
    /// </remarks>
    /// <param name="target">The house's keeper, as described.</param>
    /// <param name="context">The house's placement, the party and the clock.</param>
    /// <param name="houses">Who stands at the placement of a house of this place, by the house's number, or null.</param>
    internal InteractionOutcome? House(InteractionTargetDefinition target, InteractionContext context, Func<int, ConversationSubject?> houses)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(houses);
        if (context.Placement.Source.GetInt32(SourceEventField) is not { } eventId || _events.Find(context.Place, eventId) is not { Housed: true } mapEvent)
        {
            return null;
        }

        Run run = new(this, mapEvent, target, context) { Houses = houses };
        if (Execute(run, mapEvent, target, context) is { } refusal)
        {
            return InteractionOutcome.Applied(SpokenState, $"The party speaks with {target.Name}.", refusal.Message);
        }

        return run.SettleHouse();
    }

    /// <summary>Runs an event from its first step, after the timers that keep what it reads; answers with a refusal or null.</summary>
    private Refusal? Execute(Run run, MapEvent mapEvent, InteractionTargetDefinition target, InteractionContext context)
    {
        // The timers that keep what this event reads run first, each once when its period has passed since it
        // last ran — and every one of them on the fixture's first use, which is the donor's own reading of a
        // timer on a map the party has not visited (OpenEnroth src/Engine/Evt/Processor.cpp:120-140).
        long now = context.Clock?.Elapsed.Milliseconds ?? 0;
        foreach ((MapEvent owner, MapEventStep timer) in _events.TimersFor(mapEvent))
        {
            string key = TimerKey(owner, timer);
            if (Period(timer) is not { } period)
            {
                return NotInterpreted(target, owner, timer, $"a timer whose period '{timer.Period}' this game does not read");
            }

            if (run.Kept(key) is { } last && now - last < period.Milliseconds) continue;
            if (run.Execute(owner, timer.Step + 1) is { } refused) return refused;
            run.Keep(key, now);
        }

        return run.Execute(mapEvent, 0);
    }

    /// <summary>
    /// What a person's word raised, as the target the interaction mechanism runs (<see cref="Speak"/>), or null when it
    /// names no topic this world's people carry an event for.
    /// </summary>
    /// <param name="request">The placement the person stands at, and the word.</param>
    /// <param name="reach">How far from it the party may stand, which an answer does not consult.</param>
    internal InteractionTargetDefinition? Spoken(InteractionTargetRequest request, double reach)
    {
        if (request.Raised.Length == 0 || _topics(request.Raised) is not { } topic || _events.Global(topic.Event) is null) return null;
        return new InteractionTargetDefinition(
            new InteractionTargetKind(SpokenTargetKind),
            topic.Label,
            InteractionVerb.Talk,
            reach,
            SpokenState);
    }

    /// <summary>
    /// Runs the global event a person's topic raises and answers with what the person said and what the run did, or
    /// with the line the run reached and why nothing changed.
    /// </summary>
    /// <param name="target">The word, as described.</param>
    /// <param name="context">The placement the person stands at, the party, the clock, and the word.</param>
    internal InteractionOutcome Speak(InteractionTargetDefinition target, InteractionContext context)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        if (_topics(context.Raised) is not { } topic || _events.Global(topic.Event) is not { } spoken)
        {
            return InteractionOutcome.Refused(new Refusal(
                MightAndMagic7Codes.FixtureEventMissing,
                $"What was said raised '{context.Raised}', and the loaded content carries no global event for it, so there is nothing to run."));
        }

        Run run = new(this, spoken, target, context);
        if (run.Execute(spoken, 0) is { } refusal)
        {
            // What the person had said before the run met a step this game does not interpret is still what they said;
            // nothing the run collected is applied, and the refusal says why.
            return InteractionOutcome.Applied(SpokenState, run.Said(topic.Words), refusal.Message);
        }

        return run.Settle(topic.Words);
    }

    /// <summary>
    /// Whether a topic raising a global event is offered, by the event's own offer check, and why not when it is not.
    /// </summary>
    /// <remarks>
    /// The donor runs the event in its offer mode with every member asked (OpenEnroth
    /// <c>src/Engine/Evt/EvtInterpreter.cpp:152-181</c>, <c>:632-655</c>): a comparison jumps when any member holds it,
    /// a <c>set-can-show-dialog-item</c> step states the answer, an end or an exit stops, and every other step is passed
    /// over. A check that never states an answer offers the topic, as the donor's does. A comparison of a variable this
    /// game does not interpret withholds the topic and names the variable, rather than offering what might not be.
    /// </remarks>
    /// <param name="eventId">The global event the topic raises.</param>
    /// <param name="place">The place the conversation is in.</param>
    /// <param name="placement">The placement the person stands at.</param>
    /// <param name="party">The party, or null when the world holds none.</param>
    /// <param name="clock">The one clock, or null when the session keeps none.</param>
    internal Verdict Offers(int eventId, PlaceId place, PlacementDefinition placement, PartyEntity? party, GameClock? clock)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (_events.Global(eventId) is not { ChecksOffer: true } spoken) return Verdict.Met;
        InteractionTargetDefinition target = new(new InteractionTargetKind(SpokenTargetKind), spoken.Name, InteractionVerb.Talk, double.MaxValue, SpokenState);
        Run run = new(this, spoken, target, new InteractionContext(place, placement, target, party, clock));
        return run.Offer(spoken);
    }

    /// <summary>Whether a fixture is a sign: a region model the level itself names as one.</summary>
    /// <remarks>
    /// A sign's event does nothing but show its hint — "Welcome to Emerald Isle", "Shops" — which is what the
    /// party reads; the level marks the model it stands on with the word in its own name (<c>TownSign_S</c>,
    /// <c>3 way signE</c>, <c>sign fireW</c>), and that is the only thing in the data that tells a sign from a
    /// crate whose hint is also all its event does.
    /// </remarks>
    private static bool IsSign(PlacementDefinition placement) =>
        placement.Source.GetString(ModelNameField).Contains("sign", StringComparison.OrdinalIgnoreCase);

    /// <summary>The name a place keeps a timer's last run under.</summary>
    internal static string TimerKey(MapEvent owner, MapEventStep timer) =>
        string.Create(CultureInfo.InvariantCulture, $"{TimerPrefix}{owner.Id}.{timer.Step}");

    /// <summary>The name a place keeps one of its map variables under.</summary>
    internal static string VariableKey(int slot) =>
        string.Create(CultureInfo.InvariantCulture, $"{VariablePrefix}{slot}");

    /// <summary>
    /// Why a value a save says a place keeps is not one this game's fixtures could have left, or null when it is.
    /// </summary>
    /// <remarks>
    /// A map variable is one of the place's 75 byte-sized slots; a timer is one the place's own events hold,
    /// and it cannot have last run after the game time the save had reached. Every other name is not one a
    /// fixture writes.
    /// </remarks>
    /// <param name="place">The place.</param>
    /// <param name="key">The value's name.</param>
    /// <param name="value">The recorded figure.</param>
    /// <param name="elapsed">The game time the save had reached, in milliseconds.</param>
    internal string? Judge(PlaceId place, string key, long value, long elapsed)
    {
        if (key.StartsWith(VariablePrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(key.AsSpan(VariablePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int slot) || slot >= MapVariableSlots)
            {
                return string.Create(CultureInfo.InvariantCulture, $"a place has map variables 0 to {MapVariableSlots - 1} only");
            }

            return value is < 0 or > MapVariableLimit
                ? string.Create(CultureInfo.InvariantCulture, $"a map variable holds 0 to {MapVariableLimit}")
                : null;
        }

        if (key.StartsWith(TimerPrefix, StringComparison.Ordinal))
        {
            string[] parts = key[TimerPrefix.Length..].Split('.');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int eventId) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int step) ||
                _events.Find(place, eventId) is not { } owner ||
                owner.At(step) is not { } timer ||
                !MightAndMagic7MapEvents.IsTimer(timer.Op))
            {
                return "the place's map events hold no such timer";
            }

            return value < 0 || value > elapsed
                ? string.Create(CultureInfo.InvariantCulture, $"a timer cannot have last run outside the {elapsed} ms of game time the save had reached")
                : null;
        }

        if (key.StartsWith(HostileGroupPrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(key.AsSpan(HostileGroupPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int group) || group == 0)
            {
                return "a group a place's events turn hostile is named by its number, which is not zero";
            }

            return value is 0 or 1 ? null : "a group is hostile (1) or not (0)";
        }

        if (key.StartsWith(HiddenGroupPrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(key.AsSpan(HiddenGroupPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int group) || group == 0)
            {
                return "a group a place's events hide is named by its number, which is not zero";
            }

            return value is 0 or 1 ? null : "a group is hidden (1) or shown (0)";
        }

        if (key.StartsWith(CounterPrefix, StringComparison.Ordinal))
        {
            if (!int.TryParse(key.AsSpan(CounterPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int counter) || counter >= Counters)
            {
                return string.Create(CultureInfo.InvariantCulture, $"there are event counters 0 to {Counters - 1} only");
            }

            return value < 0 || value > elapsed
                ? string.Create(CultureInfo.InvariantCulture, $"a counter cannot have been set outside the {elapsed} ms of game time the save had reached")
                : null;
        }

        return "no fixture of this game keeps a value of that name";
    }

    /// <summary>How long a timer waits between runs, or null when its period is one this game does not read.</summary>
    /// <remarks>
    /// The donor's own periods (OpenEnroth <c>src/Engine/Evt/Processor.cpp:101-118</c>): a year, a 28-day month,
    /// a week, a day, or the record's own interval in half minutes. A daily timer's hour is not kept: it runs a
    /// day after it last ran rather than at its stated hour, which is an approximation this game makes.
    /// </remarks>
    private static GameDuration? Period(MapEventStep timer) => timer.Period switch
    {
        "yearly" => GameDuration.FromHours(24 * 336),
        "monthly" => GameDuration.FromHours(24 * 28),
        "weekly" => GameDuration.FromHours(24 * 7),
        "daily" => GameDuration.FromHours(24),
        "interval" when timer.HalfMinutes > 0 => GameDuration.FromSeconds(timer.HalfMinutes * 30L),
        _ => null,
    };

    /// <summary>The refusal for a step this game does not interpret.</summary>
    private static Refusal NotInterpreted(InteractionTargetDefinition target, MapEvent mapEvent, MapEventStep step, string what) =>
        new(
            MightAndMagic7Codes.FixtureStepNotInterpreted,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{target.Name} runs {mapEvent.Name}, and its step {step.Step} is {what}, which this game does not interpret: nothing was changed."));

    /// <summary>The refusal for a variable this game does not interpret.</summary>
    /// <param name="target">The fixture.</param>
    /// <param name="mapEvent">The event it runs.</param>
    /// <param name="step">The step.</param>
    /// <param name="waits">What the variable waits on, naming its receiver, or empty when nothing is routed for it.</param>
    private static Refusal VariableNotInterpreted(InteractionTargetDefinition target, MapEvent mapEvent, MapEventStep step, string waits = "") =>
        new(
            MightAndMagic7Codes.FixtureVariableNotInterpreted,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{target.Name} runs {mapEvent.Name}, and its step {step.Step} {step.Op}s the variable '{Describe(step)}', which this game does not interpret for that instruction{(waits.Length > 0 ? $" ({waits})" : string.Empty)}: nothing was changed."));

    private static string Describe(MapEventStep step) =>
        step.Which.Length > 0 ? $"{step.Variable} {step.Which}" : step.Variable;

    /// <summary>The attribute a step's member word names.</summary>
    private static AttributeId? Attribute(string which) => which switch
    {
        "might" => new AttributeId("Might"),
        "intellect" => new AttributeId("Intellect"),
        "personality" => new AttributeId("Personality"),
        "endurance" => new AttributeId("Endurance"),
        "speed" => new AttributeId("Speed"),
        "accuracy" => new AttributeId("Accuracy"),
        "luck" => new AttributeId("Luck"),
        _ => null,
    };

    /// <summary>The kind of harm a step's resistance or damage word names.</summary>
    private static DamageKindId? DamageKind(string which) => which switch
    {
        "fire" => MightAndMagic7Damage.Fire,
        "air" => MightAndMagic7Damage.Air,
        "water" => MightAndMagic7Damage.Water,
        "earth" => MightAndMagic7Damage.Earth,
        "spirit" => MightAndMagic7Damage.Spirit,
        "mind" => MightAndMagic7Damage.Mind,
        "body" => MightAndMagic7Damage.Body,
        "light" => MightAndMagic7Damage.Light,
        "dark" => MightAndMagic7Damage.Dark,
        "physical" => MightAndMagic7Damage.Physical,
        "magic" => MightAndMagic7Damage.Magic,
        _ => null,
    };

    /// <summary>The condition a step's member word names.</summary>
    private static ConditionId? Condition(string which) => which switch
    {
        "cursed" => MightAndMagic7Conditions.Cursed,
        "weak" => MightAndMagic7Conditions.Weak,
        "asleep" => MightAndMagic7Conditions.Sleep,
        "afraid" => MightAndMagic7Conditions.Fear,
        "drunk" => MightAndMagic7Conditions.Drunk,
        "insane" => MightAndMagic7Conditions.Insane,
        "poison-weak" => MightAndMagic7Conditions.PoisonWeak,
        "disease-weak" => MightAndMagic7Conditions.DiseaseWeak,
        "poison-medium" => MightAndMagic7Conditions.PoisonMedium,
        "disease-medium" => MightAndMagic7Conditions.DiseaseMedium,
        "poison-severe" => MightAndMagic7Conditions.PoisonSevere,
        "disease-severe" => MightAndMagic7Conditions.DiseaseSevere,
        "paralyzed" => MightAndMagic7Conditions.Paralyzed,
        "unconscious" => MightAndMagic7Conditions.Unconscious,
        "dead" => MightAndMagic7Conditions.Dead,
        "stoned" => MightAndMagic7Conditions.Petrified,
        "eradicated" => MightAndMagic7Conditions.Eradicated,
        _ => null,
    };

    /// <summary>Whether a calendar date falls in a season, or null when the season is not one of the four.</summary>
    /// <remarks>
    /// The donor's own boundaries, by month and day: spring from the twenty-first of the third month, summer of
    /// the sixth, autumn of the ninth, winter of the twelfth (OpenEnroth
    /// <c>src/Engine/Evt/EvtInterpreter.cpp:43-72</c>), read against this game's own calendar.
    /// </remarks>
    private static bool? InSeason(string season, GameDate date) => season switch
    {
        "spring" => Between(date, 3, 6),
        "summer" => Between(date, 6, 9),
        "autumn" => Between(date, 9, 12),
        "winter" => Between(date, 12, 3),
        _ => null,
    };

    /// <summary>Whether a date stands from the twenty-first of one month to the twentieth of another.</summary>
    private static bool Between(GameDate date, int fromMonth, int toMonth)
    {
        if (date.Month == fromMonth) return date.Day >= 21;
        if (date.Month == toMonth) return date.Day <= 20;
        return fromMonth < toMonth
            ? date.Month > fromMonth && date.Month < toMonth
            : date.Month > fromMonth || date.Month < toMonth;
    }

    /// <summary>
    /// The kind of note a discovery row's category is in this game's knowledge, and whether it is one at all.
    /// </summary>
    /// <remarks>
    /// This game's reading of the table's words (<see cref="MightAndMagic7Knowledge"/>): a <c>stat</c> row is
    /// what a landmark gives and a <c>misc</c> row what something did, both an effect; an <c>obelisk</c> row, and
    /// a <c>teacher</c> or <c>seer</c> row, are something the party was shown or told, a clue; a <c>potion</c>
    /// row is a recipe.
    /// </remarks>
    private static KnowledgeKind KindOf(string category) => category switch
    {
        "obelisk" or "teacher" or "seer" => KnowledgeKind.Clue,
        "potion" => KnowledgeKind.Recipe,
        _ => KnowledgeKind.Effect,
    };

    /// <summary>The name a place keeps when one of the event counters was last set under.</summary>
    internal static string CounterKey(int counter) =>
        string.Create(CultureInfo.InvariantCulture, $"{CounterPrefix}{counter}");

    /// <summary>The rung a mastery word names, one novice to four grand master, or zero for none.</summary>
    private static int Rung(string mastery) => mastery switch
    {
        "novice" => 1,
        "expert" => 2,
        "master" => 3,
        "grandmaster" => 4,
        _ => 0,
    };

    /// <summary>The skill a step's skill word names, as this game's content calls it.</summary>
    private static SkillId? SkillOf(string word) => word switch
    {
        "staff" => new SkillId("Staff"),
        "sword" => new SkillId("Sword"),
        "dagger" => new SkillId("Dagger"),
        "axe" => new SkillId("Axe"),
        "spear" => new SkillId("Spear"),
        "bow" => new SkillId("Bow"),
        "mace" => new SkillId("Mace"),
        "blaster" => new SkillId("Blaster"),
        "shield" => new SkillId("Shield"),
        "leather" => new SkillId("Leather"),
        "chain" => new SkillId("Chain"),
        "plate" => new SkillId("Plate"),
        "fire" => new SkillId("Fire"),
        "air" => new SkillId("Air"),
        "water" => new SkillId("Water"),
        "earth" => new SkillId("Earth"),
        "spirit" => new SkillId("Spirit"),
        "mind" => new SkillId("Mind"),
        "body" => new SkillId("Body"),
        "light" => new SkillId("Light"),
        "dark" => new SkillId("Dark"),
        "identify-item" => new SkillId("Identify Item"),
        "merchant" => new SkillId("Merchant"),
        "repair" => new SkillId("Repair"),
        "bodybuilding" => new SkillId("Bodybuilding"),
        "meditation" => new SkillId("Meditation"),
        "perception" => new SkillId("Perception"),
        "diplomacy" => new SkillId("Diplomacy"),
        "thievery" => new SkillId("Thievery"),
        "disarm-trap" => new SkillId("Disarm Traps"),
        "dodge" => new SkillId("Dodging"),
        "unarmed" => new SkillId("Unarmed"),
        "identify-monster" => new SkillId("Identify Monster"),
        "armsmaster" => new SkillId("Armsmaster"),
        "stealing" => new SkillId("Stealing"),
        "alchemy" => new SkillId("Alchemy"),
        "learning" => new SkillId("Learning"),
        _ => null,
    };

    /// <summary>The subject a discovery row's note is kept under, which is the row's own number.</summary>
    internal static string DiscoverySubject(int number) =>
        string.Create(CultureInfo.InvariantCulture, $"discovery:{number}");

    /// <summary>The record a party bit is kept as.</summary>
    internal static string MemberBitRecord(int bit) =>
        string.Create(CultureInfo.InvariantCulture, $"member-bit:{bit}");

    /// <summary>
    /// The record an award bit an event sets is kept as: the donor's award of that number
    /// (<c>src/Engine/Data/AwardEnums.h</c>), held by the party.
    /// </summary>
    internal static string AwardRecord(int award) =>
        string.Create(CultureInfo.InvariantCulture, $"award-bit:{award}");

    /// <summary>The name a place keeps whether its events turned one of its groups hostile under.</summary>
    internal static string HostileGroupKey(int group) =>
        string.Create(CultureInfo.InvariantCulture, $"{HostileGroupPrefix}{group}");

    /// <summary>The name a place keeps whether its events hid one of its groups under.</summary>
    internal static string HiddenGroupKey(int group) =>
        string.Create(CultureInfo.InvariantCulture, $"{HiddenGroupPrefix}{group}");

    /// <summary>
    /// Whether a place's own events left one of its groups hidden (true) or shown again (false), or null when they never
    /// touched it and the records say.
    /// </summary>
    /// <param name="values">The values the place keeps.</param>
    /// <param name="group">The group, which zero never is.</param>
    internal static bool? IsGroupHidden(IReadOnlyDictionary<string, long> values, int group) =>
        group != 0 && values.TryGetValue(HiddenGroupKey(group), out long hidden) ? hidden > 0 : null;

    /// <summary>Whether a place's own events left one of its groups of creatures hostile, read from the values it keeps.</summary>
    /// <param name="values">The values the place keeps.</param>
    /// <param name="group">The group, which zero never is.</param>
    internal static bool IsGroupHostile(IReadOnlyDictionary<string, long> values, int group) =>
        group != 0 && values.TryGetValue(HostileGroupKey(group), out long hostile) && hostile > 0;

    /// <summary>
    /// Why a record a save says the party carries is not one this game's fixtures could have written, or null when it
    /// is or when it is not one of theirs.
    /// </summary>
    /// <remarks>A changed topic slot names a person the content carries and one of the person's six slots.</remarks>
    /// <param name="name">The record's name.</param>
    /// <param name="count">How many times it is on record.</param>
    internal string? JudgeRecord(string name, int count) =>
        MightAndMagic7TopicSlots.Judge(name, count, person => _people(person) is not null) ??
        MightAndMagic7PersonState.Judge(name, count, person => _people(person) is not null, _greetings, _starting);

    /// <summary>
    /// Every creature and person a place's population holds and whether each is down, as the population and the
    /// world's per-place state answer it; null when the world has no such place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The place's placements are what it holds — its creatures as the encounters resolved them, and the creatures
    /// and people its own actor records stand — and the live population says which of them are down this visit. A place the party
    /// cleared populates nobody until the clock restores it, so everything it held reads as down there, which is
    /// what clearing it was.
    /// </para>
    /// <para>
    /// <b>What is ours.</b> The donor's actors keep their own death in the map's saved delta, one by one
    /// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:2811-2894</c> counts them). This game's world ledger keeps
    /// defeated placement identities until the clock restores the place. A later visit, a save and this count
    /// read that same memory; nothing here changes the population.
    /// </para>
    /// </remarks>
    /// <param name="world">The session's world.</param>
    /// <param name="place">The place counted.</param>
    internal static IReadOnlyList<PlaceActor>? ActorsOf(SessionWorld world, PlaceId place)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (world.Graph.Find(place) is null) return null;
        bool cleared = world.Places.StateOf(place).Cleared;
        Dictionary<PlacementContentId, bool> down = [];
        if (!cleared && world.Population.Place == place)
        {
            foreach (PlacePopulationEntity entity in world.Population.Entities)
            {
                down[entity.Content] = !entity.IsAlive || CreatureHealth.Find(entity.Actor) is { IsDown: true };
            }
        }

        List<PlaceActor> actors = [];
        foreach (PlacementDefinition placement in world.Population.PlacementsOf(place))
        {
            if (!IsActor(placement)) continue;
            actors.Add(new PlaceActor(placement, cleared || world.Interactions.IsDefeated(place, placement.Content) || down.GetValueOrDefault(placement.Content)));
        }

        return actors;
    }

    /// <summary>Whether a placement is one of the actors a count of the dead reads: a creature or a person standing in the place.</summary>
    internal static bool IsActor(PlacementDefinition placement) =>
        string.Equals(placement.Content.Kind, MightAndMagic7Combat.CreaturePlacementKind, StringComparison.Ordinal) ||
        string.Equals(placement.Content.Kind, MightAndMagic7Conversation.PersonPlacementKind, StringComparison.Ordinal);

    /// <summary>One use's run: the steps walked, what they would change, and the overlay later steps read.</summary>
    private sealed class Run
    {
        private readonly MightAndMagic7Fixtures _rules;
        private readonly MapEvent _event;
        private readonly InteractionTargetDefinition _target;
        private readonly InteractionContext _context;
        private readonly List<Action> _effects = [];
        private readonly List<string> _said = [];
        private readonly List<string> _done = [];
        private readonly List<InteractionItemYield> _items = [];
        private readonly List<KnowledgeReport> _learned = [];
        private readonly Dictionary<string, bool> _records = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _members = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _carried = new(StringComparer.Ordinal);
        private readonly SortedDictionary<string, long> _kept = new(StringComparer.Ordinal);
        private readonly Dictionary<PlacementContentId, string> _changes = [];
        private readonly List<string> _residue = [];
        private readonly Dictionary<int, string> _classes = [];
        private Refusal? _refused;
        private int? _bank;
        private int? _reputation;
        private int? _food;
        private ConversationSubject? _speaks;
        private bool _opens;
        private InteractionTravel? _travels;
        private InteractionRelocation? _relocates;
        private int _coins;
        private int _draws;

        internal Run(MightAndMagic7Fixtures rules, MapEvent mapEvent, InteractionTargetDefinition target, InteractionContext context)
        {
            _rules = rules;
            _event = mapEvent;
            _target = target;
            _context = context;
        }

        /// <summary>Who stands at the placement of a house of the place, by its number, when the run is a house's own event.</summary>
        internal Func<int, ConversationSubject?>? Houses { get; init; }

        /// <summary>A value the place keeps as the run has left it so far, or null when nothing has written it.</summary>
        internal long? Kept(string key) =>
            _kept.TryGetValue(key, out long written) ? written
            : _context.PlaceValues.TryGetValue(key, out long held) ? held
            : null;

        /// <summary>Writes a value the place keeps, which the outcome states and the ledger takes when the use is applied.</summary>
        internal void Keep(string key, long value) => _kept[key] = value;

        /// <summary>A map variable as the run has left it; one nothing has written is zero, as the donor's are.</summary>
        private int Variable(int slot) => (int)(Kept(VariableKey(slot)) ?? 0);

        private PartyEntity? Party => _context.Party;

        /// <summary>Walks one event from a step, collecting what it would do; answers with a refusal or null.</summary>
        internal Refusal? Execute(MapEvent mapEvent, int start)
        {
            int step = start;
            int walked = 0;
            List<int> who = Active();
            while (mapEvent.At(step) is { } current)
            {
                if (++walked > StepLimit)
                {
                    return new Refusal(
                        MightAndMagic7Codes.FixtureRunaway,
                        string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name} past {StepLimit} steps without ending, so nothing was changed."));
                }

                int next = step + 1;
                switch (current.Op)
                {
                    case "exit":
                        return null;
                    case "move-to-map" when current.WithinPlace:
                        // A move within the place sets the party down elsewhere in it and the run goes on, as the
                        // donor's does (OpenEnroth src/Engine/Evt/EvtInterpreter.cpp:231-235); a move naming no position
                        // moves nobody.
                        if (current.Position != (0, 0, 0))
                        {
                            _relocates = new InteractionRelocation(current.Position.X, current.Position.Y, current.Position.Z, current.Yaw == -1 ? null : current.Yaw);
                        }

                        break;
                    case "move-to-map":
                        // The party leaves: what follows a move is not run, and the journey is the outcome's.
                        return Move(mapEvent, current);
                    case "jump":
                        next = current.Target ?? next;
                        break;
                    case "compare":
                    {
                        (bool holds, Refusal? refused) = Compare(mapEvent, current, who);
                        if (refused is not null) return refused;
                        if (holds) next = current.Target ?? next;
                        break;
                    }

                    case "add" or "subtract" or "set":
                        if (Write(mapEvent, current, who) is { } refusedWrite) return refusedWrite;
                        break;
                    case "status-text" or "show-message":
                        // A message a topic's event shows is the person's answer; one a place's event shows is the line
                        // the party reads (OpenEnroth src/Engine/Evt/EvtInterpreter.cpp:403-423).
                        if (current.Text.Length > 0) _said.Add(current.Text);
                        break;
                    case MightAndMagic7MapEvents.OfferCompare or MightAndMagic7MapEvents.OfferSet or MightAndMagic7MapEvents.OfferEnd:
                        // A topic's offer check is read by Offer; a regular run passes over it, as the donor's does
                        // (src/Engine/Evt/EvtInterpreter.cpp:184-185, the regular switch has no case for them).
                        break;
                    case "for-party-member":
                        (who, Refusal? chose) = Choose(mapEvent, current);
                        if (chose is not null) return chose;
                        break;
                    case "random-go-to":
                        if (current.Targets.Count == 0) return NotInterpreted(_target, mapEvent, current, "a random jump with nowhere to go");
                        if (Rolls(mapEvent) is not { } rolls)
                        {
                            return new Refusal(
                                MightAndMagic7Codes.FixtureNothingToRoll,
                                string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {current.Step} chooses at random, and this product has no random service to choose with: nothing was changed."));
                        }

                        next = current.Targets[rolls.Pick(current.Targets.Count)];
                        break;
                    case "receive-damage":
                        if (Harm(mapEvent, current) is { } refusedHarm) return refusedHarm;
                        break;
                    case "change-door-state":
                        if (Door(mapEvent, current) is { } refusedDoor) return refusedDoor;
                        break;
                    case var presentation when PresentationSteps.Contains(presentation):
                        // What a player would see or hear is not drawn here; the event's gameplay runs on.
                        break;
                    case "set-faces-bit":
                        // A face group made invisible or fluid is presentation; one made passable changes the ground a
                        // party walks on, which this build's collision does not follow.
                        if ((current.Flag & PassableFaceBit) != 0) Residue(CollisionResidue);
                        if ((current.Flag & ~(InvisibleFaceBit | PassableFaceBit | FluidFaceBit)) != 0)
                        {
                            return NotInterpreted(_target, mapEvent, current, string.Create(CultureInfo.InvariantCulture, $"a face bit 0x{current.Flag:X} this game does not read"));
                        }

                        break;
                    case "give-item":
                        if (Give(mapEvent, current) is { } refusedGift) return refusedGift;
                        break;
                    case "cast-spell":
                        if (Cast(mapEvent, current, who) is { } refusedCast) return refusedCast;
                        break;
                    case "speak-npc":
                        if (Speak(mapEvent, current) is { } refusedSpeech) return refusedSpeech;
                        break;
                    case "check-skill":
                    {
                        (bool holds, Refusal? refused) = CheckSkill(mapEvent, current, who);
                        if (refused is not null) return refused;
                        if (holds) next = current.Target ?? next;
                        break;
                    }

                    case "move-npc":
                        if (MoveNpc(mapEvent, current) is { } refusedMove) return refusedMove;
                        break;
                    case "set-npc-greeting":
                        if (Greet(mapEvent, current) is { } refusedGreeting) return refusedGreeting;
                        break;
                    case "npc-set-item":
                        if (Hand(mapEvent, current) is { } refusedItem) return refusedItem;
                        break;
                    case "speak-in-house" when Houses is { } houses:
                        // The house opens and the run goes on, as the donor's does (src/Engine/Evt/EvtInterpreter.cpp:189-198).
                        _opens = true;
                        if (houses(current.House) is { } inside) _speaks = inside;
                        else Residue(string.Create(CultureInfo.InvariantCulture, $"House {current.House} is not one this place holds, so nobody answers there."));
                        break;
                    case "set-npc-topic":
                        if (TopicSlot(mapEvent, current) is { } refusedTopic) return refusedTopic;
                        break;
                    case "is-actor-killed":
                    {
                        (bool holds, Refusal? refused) = Killed(mapEvent, current);
                        if (refused is not null) return refused;
                        if (holds) next = current.Target ?? next;
                        break;
                    }

                    case "toggle-actor-group-flag":
                        if (GroupFlag(mapEvent, current) is { } refusedFlag) return refusedFlag;
                        break;
                    case "summon-monsters":
                        if (Summon(mapEvent, current) is { } refusedSummon) return refusedSummon;
                        break;
                    case "check-season":
                    {
                        if (_context.Clock is not { } clock || InSeason(current.Which, clock.Now) is not { } holds)
                        {
                            return NotInterpreted(_target, mapEvent, current, $"a jump on the season '{current.Which}' in a session that keeps no calendar to read it from");
                        }

                        if (holds) next = current.Target ?? next;
                        break;
                    }
                    default:
                        // A trigger ends a use's run: what follows it is the map's to run, not the party's.
                        if (MightAndMagic7MapEvents.IsTrigger(current.Op)) return null;
                        return NotInterpreted(_target, mapEvent, current, $"a '{current.Op}' instruction");
                }

                step = next;
            }

            return null;
        }

        /// <summary>What the run has had said so far, or the given words when it said nothing, or the event's name.</summary>
        /// <param name="words">What the topic's table says, or empty.</param>
        internal string Said(string words) =>
            _said.Count > 0 ? string.Join(" ", _said) : words.Length > 0 ? words : $"{_target.Name}: there is nothing more to it.";

        /// <summary>
        /// Walks a topic's offer check — the donor's offer mode — and answers whether the topic is offered.
        /// </summary>
        internal Verdict Offer(MapEvent mapEvent)
        {
            List<int> who = Party is { } party ? [.. Enumerable.Range(0, party.Members.Count)] : [];
            bool stated = false;
            bool shows = false;
            int step = 0;
            int walked = 0;
            while (mapEvent.At(step) is { } current && ++walked <= StepLimit)
            {
                int next = step + 1;
                if (current.Op == "exit" || current.Op == MightAndMagic7MapEvents.OfferEnd) break;
                if (current.Op == MightAndMagic7MapEvents.OfferSet)
                {
                    stated = true;
                    shows = current.On;
                }
                else if (current.Op == MightAndMagic7MapEvents.OfferCompare)
                {
                    stated = true;
                    (bool holds, Refusal? refused) = Compare(mapEvent, current, who);
                    if (refused is not null)
                    {
                        return Verdict.Unmet(string.Create(
                            CultureInfo.InvariantCulture,
                            $"whether this is brought up waits on '{Describe(current)}', which this game does not read ({mapEvent.Name}, step {current.Step})"));
                    }

                    if (holds) next = current.Target ?? next;
                }

                step = next;
            }

            return !stated || shows ? Verdict.Met : Verdict.Unmet("it is not something they bring up yet");
        }

        /// <summary>Applies everything the run collected, and answers with what the use did.</summary>
        /// <param name="words">What a topic's table says, which a run that said nothing answers with; empty for a fixture.</param>
        internal InteractionOutcome Settle(string words = "")
        {
            foreach (Action effect in _effects) effect();

            // What the run did to the purse is one net figure: a gain goes through the outcome, which the kit
            // credits through the party's accounts, and a net payment is taken from the purse here — never both,
            // so a step that pays and a step that finds within one run cannot be counted twice.
            if (_coins < 0 && Party is { } payer) payer.Purse.TryDebit(-_coins);
            bool sign = _target.Verb == InteractionVerb.Read;
            if (sign && _event.Label.Length > 0)
            {
                // A sign's words are something written the party read: this game keeps it as a clue, which the
                // original does not — its sign shows the line and keeps nothing — so the note is ours.
                _learned.Add(new KnowledgeReport(
                    KnowledgeKind.Clue,
                    Source,
                    string.Create(CultureInfo.InvariantCulture, $"sign:{_event.Place}.{_event.Id}"),
                    $"\"{_event.Label}\"",
                    _event.Place.Value));
            }

            string message = _said.Count > 0
                ? string.Join(" ", _said)
                : words.Length > 0
                    ? words
                    : _done.Count > 0
                        ? $"{_target.Name}: {string.Join(" ", _done)}"
                        : sign
                            ? $"The sign reads: \"{_event.Label}\"."
                            : _travels is not null || _relocates is not null
                                ? $"{_target.Name} leads the party on."
                                : $"{_target.Name}: nothing comes of it.";
            return InteractionOutcome.Applied(
                sign ? ReadState : _target.Kind.Value == SpokenTargetKind ? SpokenState : UsedState,
                message,
                string.Join(" ", _residue),
                items: _items,
                gain: _coins > 0 ? PartyCost.OfGold(_coins) : null,
                learned: _learned,
                kept: _kept,
                changes: [.. _changes.Select(change => new InteractionTargetChange(change.Key, change.Value))],
                speaks: _speaks,
                travels: _travels,
                relocates: _travels is null ? _relocates : null);
        }

        /// <summary>
        /// Applies a house's own run: the journey a branch reached, the house its step opened, or the party kept outside
        /// with what the run said.
        /// </summary>
        internal InteractionOutcome SettleHouse()
        {
            InteractionOutcome settled = Settle($"The party speaks with {_target.Name}.");
            if (_travels is not null || _opens) return settled;
            return settled with { KeptOut = true };
        }

        /// <summary>Collects a person's move to another house, which the party's records keep and the conversation reads.</summary>
        /// <remarks>The donor writes the house on the person's record (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:470-471</c>).</remarks>
        private Refusal? MoveNpc(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            string id = string.Create(CultureInfo.InvariantCulture, $"npc-{step.Person}");
            if (_rules._people(id) is null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} moves person {step.Person}, and the loaded people table holds nobody of that id: nothing was changed."));
            }

            int house = Math.Max(0, step.House);
            _effects.Add(() => MightAndMagic7PersonState.Move(party.Records, id, house));
            return null;
        }

        /// <summary>Collects a person's change of greeting, which the party's records keep and the conversation reads.</summary>
        /// <remarks>
        /// The donor writes the row and forgets that the person greeted the party already (OpenEnroth
        /// <c>src/Engine/Evt/EvtInterpreter.cpp:541-545</c>), so the row's first line is said next.
        /// </remarks>
        private Refusal? Greet(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            string id = string.Create(CultureInfo.InvariantCulture, $"npc-{step.Person}");
            if (_rules._people(id) is null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} changes the greeting of person {step.Person}, and the loaded people table holds nobody of that id: nothing was changed."));
            }

            int row = Math.Max(0, step.Greeting);
            if (row != 0 && !_rules._greetings(row))
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} gives person {step.Person} greeting {row}, and the loaded greeting table has no such row: nothing was changed."));
            }

            _effects.Add(() => MightAndMagic7PersonState.Greet(party.Records, id, row));
            return null;
        }

        /// <summary>Collects an item given to a person to carry, or taken from them, which the party's records keep.</summary>
        /// <remarks>
        /// The donor gives it to, or takes it from, every creature standing for the person (OpenEnroth
        /// <c>src/Engine/Objects/Actor.cpp:139-165</c>); here it is the person's, read by a theft and by their body
        /// (<see cref="MightAndMagic7PersonState"/>).
        /// </remarks>
        private Refusal? Hand(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            string id = string.Create(CultureInfo.InvariantCulture, $"npc-{step.Person}");
            if (_rules._people(id) is null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} hands an item to or from person {step.Person}, and the loaded people table holds nobody of that id: nothing was changed."));
            }

            if (step.Item <= 0) return NotInterpreted(_target, mapEvent, step, "an item step that names no item");
            int item = step.Item;
            bool give = step.On;
            _effects.Add(() =>
            {
                if (give) MightAndMagic7PersonState.Give(party.Records, id, item, _rules._starting(id));
                else MightAndMagic7PersonState.Take(party.Records, id, item, _rules._starting(id));
            });
            return null;
        }

        /// <summary>Judges a move: the journey it names, which ends the run, or why it cannot be made.</summary>
        /// <remarks>
        /// The link is the importer's reading of the instruction — the place graph's own entry for it — and the place
        /// must issue it, which is judged here, before anything the run collected is settled.
        /// </remarks>
        private Refusal? Move(MapEvent mapEvent, MapEventStep step)
        {
            PlaceTransition? transition = null;
            foreach (PlaceTransition candidate in _context.PlaceTransitions)
            {
                if (!string.Equals(candidate.Source, step.Link, StringComparison.Ordinal)) continue;
                transition = candidate;
                break;
            }

            TransitionKind? kind = step.Travel switch
            {
                "walking" => TransitionKind.Walking,
                "entrance" => TransitionKind.Entrance,

                // The global program's moves are the world's own, issued from no place, and are set going by a person's
                // word (PartyInteraction.Answer hands the world's transitions to such a run).
                "scripted" when transition is { IsWorldIssued: true } => TransitionKind.Scripted,
                _ => null,
            };
            if (step.Link.Length == 0 || transition is null || kind is null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureTravelUnknown,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} moves the party along travel link '{step.Link}' as '{step.Travel}', and place '{_context.Place}' issues no such transition of a kind a party walks: nothing was changed."));
            }

            _travels = new InteractionTravel(transition, kind.Value);
            return null;
        }

        /// <summary>The members a run starts on: the active one, which this build reads as the first able to act.</summary>
        private List<int> Active()
        {
            if (Party is not { } party) return [];
            for (int index = 0; index < party.Members.Count; index++)
            {
                if (MightAndMagic7Conditions.CanAct(party.Members[index])) return [index];
            }

            return party.Members.Count > 0 ? [0] : [];
        }

        /// <summary>The members a choice names.</summary>
        private (List<int> Who, Refusal? Refused) Choose(MapEvent mapEvent, MapEventStep step)
        {
            int count = Party?.Members.Count ?? 0;
            switch (step.Who)
            {
                case "party":
                    return ([.. Enumerable.Range(0, count)], null);
                case "active":
                    return (Active(), null);
                case "member":
                    return (step.Member < count ? [step.Member] : [], null);
                case "random":
                    if (count == 0) return ([], null);
                    if (Rolls(mapEvent) is not { } rolls) return ([], NotInterpreted(_target, mapEvent, step, "a choice of a character at random in a product with no random service"));
                    return ([rolls.Pick(count)], null);
                default:
                    return ([], NotInterpreted(_target, mapEvent, step, $"a choice of '{step.Who}'"));
            }
        }

        /// <summary>The engine's keyed draw for this run, or null when the product has no random service.</summary>
        private KeyedRolls? Rolls(MapEvent mapEvent)
        {
            if (_rules._random is not { } random) return null;
            long now = _context.Clock?.Elapsed.Milliseconds ?? 0;
            _draws++;
            return new KeyedRolls(
                random,
                RollSeed,
                RollScope,
                string.Create(CultureInfo.InvariantCulture, $"{mapEvent.Place}/{mapEvent.Id}/{now}/{_draws}"));
        }

        private Refusal NoParty(MapEvent mapEvent, MapEventStep step) =>
            new(
                MightAndMagic7Codes.FixtureNoParty,
                string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} reads or changes the party, and this world holds no party."));

        /// <summary>Whether a comparison holds, for any of the members chosen when it is theirs.</summary>
        private (bool Holds, Refusal? Refused) Compare(MapEvent mapEvent, MapEventStep step, List<int> who)
        {
            if (step.Variable == MightAndMagic7MapEvents.MapVariable) return (Variable(step.Index) >= step.Value, null);
            if (Party is not { } party) return (false, NoParty(mapEvent, step));
            switch (step.Variable)
            {
                case "quest-bit":
                    return (HasRecord(MightAndMagic7Quests.ErrandRecord(step.Value.ToString(CultureInfo.InvariantCulture))), null);
                case "member-bit":
                    return (HasRecord(MemberBitRecord(step.Value)), null);
                case "award":
                    return (HasRecord(AwardRecord(step.Value)), null);
                case "autonote":
                    return (Knows(step.Value), null);
                case "gold":
                    return (party.Purse.Coins + _coins >= step.Value, null);
                case "item":
                    return (Carried(party, step.Value) > 0, null);
                case "bank-gold":
                    // The party's one balance, which every bank keeps (MightAndMagic7Services.BankHolding).
                    return (Bank(party) >= step.Value, null);

                // The place's reputation in the donor's sign, which this game's one reputation is read through.
                case "reputation":
                    return (MightAndMagic7Standing.HoldsEventComparison(step.Value, Reputation(party)), null);

                // The larder, at least the figure (OpenEnroth src/Engine/Objects/Character.cpp:3663-3664).
                case "food":
                    return (Food(party) >= step.Value, null);
                case "counter":
                {
                    // A counter holds when it was set and the stated hours have passed since (OpenEnroth
                    // src/Engine/Objects/Character.cpp:3934-3951).
                    if (step.Index is < 0 or >= Counters) return (false, VariableNotInterpreted(_target, mapEvent, step));
                    long now = _context.Clock?.Elapsed.Milliseconds ?? 0;
                    return (Kept(CounterKey(step.Index)) is { } set && now - set >= GameDuration.FromHours(step.Value).Milliseconds, null);
                }

                case "hireling":
                    return (false, VariableNotInterpreted(_target, mapEvent, step, "this build keeps no hirelings, #8514"));

                // Whether the party is invisible, whatever the value (OpenEnroth src/Engine/Objects/Character.cpp:3979-3980):
                // the spell's party-wide effect, the one the fight reads for whether a creature notices the party.
                case "invisible":
                    return (party.Effects.MagnitudeOf(SpellEffectIds.Invisibility) > 0, null);

                // The place's alert status, compared for equality rather than at least (OpenEnroth
                // src/Engine/Objects/Character.cpp:3956-3958). The donor reads it from the map's saved state, every shipped
                // map states zero, and no instruction or code of the donor's writes it, so it is zero in play.
                case "alert":
                    return (step.Value == 0, null);
            }

            Func<PartyMember, int, int?>? read = step.Variable switch
            {
                "hit-points" => (member, index) => Member(index, "hp", member.Resources.HitPoints.Current),

                // Whether a character is whole: the donor compares current with maximum and ignores the value
                // (OpenEnroth src/Engine/Objects/Character.cpp:3624-3627).
                "full-hit-points" => (member, index) =>
                    Member(index, "hp", member.Resources.HitPoints.Current) >= member.Resources.HitPoints.Maximum ? step.Value : step.Value - 1,
                "full-spell-points" => (member, index) =>
                    Member(index, "sp", member.Resources.SpellPoints.Current) >= member.Resources.SpellPoints.Maximum ? step.Value : step.Value - 1,
                "spell-points" => (member, index) => Member(index, "sp", member.Resources.SpellPoints.Current),
                "attribute" when Attribute(step.Which) is { } attribute =>
                    (member, index) => member.Attributes.TryGet(attribute, out int value) ? Member(index, $"attribute:{step.Which}", value) : null,
                "resistance-bonus" when DamageKind(step.Which) is { } kind && _rules._effects is { } effects =>
                    (member, index) => Member(index, $"resistance:{step.Which}", effects.MagnitudeOn(member, SpellEffectIds.Resistance(kind))),
                "resistance" when DamageKind(step.Which) is { } kind =>
                    (member, index) => Member(index, $"stored:{step.Which}", member.Resistances.Of(kind)),
                "attribute-bonus" when Attribute(step.Which) is { } attribute && _rules._effects is { } effects =>
                    (member, index) => Member(index, $"bonus:{step.Which}", effects.MagnitudeOn(member, SpellEffectIds.Attribute(attribute))),
                "skill-points" => (member, index) => Member(index, "skill-points", member.Progression.SkillPoints),
                "experience" => (member, index) => (int)Math.Min(int.MaxValue, member.Progression.Experience + Member(index, "experience", 0)),
                "condition" when Condition(step.Which) is { } condition =>
                    (member, index) => Member(index, $"condition:{step.Which}", member.Conditions.Has(condition) ? 1 : 0),

                // Whether the character wears the item, in any slot (OpenEnroth src/Engine/Objects/Character.cpp:3981-3982).
                "item-equipped" => (member, _) => Wears(member, step.Value) ? 1 : 0,

                // Whether the character is of the class the step names — equality, not at least (OpenEnroth
                // src/Engine/Objects/Character.cpp:3618-3619) — as the run has left it.
                "class" when step.Which.Length > 0 => (member, index) => string.Equals(ClassOf(member, index), step.Which, StringComparison.Ordinal) ? 1 : 0,
                _ => null,
            };
            if (read is null) return (false, VariableNotInterpreted(_target, mapEvent, step));

            foreach (int index in who)
            {
                if (read(party.Members[index], index) is not { } value) continue;

                // A condition compares whether it is held rather than how much of it: the donor's comparison
                // of one is a test of the bit (OpenEnroth src/Engine/Objects/Character.cpp:3826-3859).
                bool holds = step.Variable is "condition" or "item-equipped" or "class" ? value > 0 : value >= step.Value;
                if (holds) return (true, null);
            }

            return (false, null);
        }

        /// <summary>Collects a write, judged now and applied when the run ends.</summary>
        private Refusal? Write(MapEvent mapEvent, MapEventStep step, List<int> who)
        {
            string op = step.Op;
            if (step.Variable == MightAndMagic7MapEvents.MapVariable)
            {
                // The donor's map variables are bytes: an addition stops at 255 and a subtraction at nothing
                // (OpenEnroth src/Engine/Objects/Character.cpp:4606-4613).
                if (step.Index is < 0 or >= MapVariableSlots) return VariableNotInterpreted(_target, mapEvent, step);
                int now = Variable(step.Index);
                Keep(VariableKey(step.Index), op switch
                {
                    "add" => Math.Min(MapVariableLimit, now + step.Value),
                    "subtract" => Math.Max(0, now - step.Value),
                    _ => Math.Clamp(step.Value, 0, MapVariableLimit),
                });
                return null;
            }

            if (Party is not { } party) return NoParty(mapEvent, step);
            switch (step.Variable, op)
            {
                case ("quest-bit", _):
                    Record(MightAndMagic7Quests.ErrandRecord(step.Value.ToString(CultureInfo.InvariantCulture)), op != "subtract");
                    return null;
                case ("member-bit", _):
                    Record(MemberBitRecord(step.Value), op != "subtract");
                    return null;
                case ("award", _):
                    // The donor sets or clears one award bit of each chosen character (OpenEnroth
                    // src/Engine/Objects/Character.cpp:3640-3641, :4689-4694, :5206-5208); this build keeps the
                    // bit on the party, as it keeps a party bit, and shows it nowhere: approximate.
                    Record(AwardRecord(step.Value), op != "subtract");
                    return null;
                case ("autonote", "add" or "set"):
                    return Learn(mapEvent, step);
                case ("gold", "add"):
                    _coins += step.Value;
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"the party finds {step.Value} gold."));
                    return null;
                case ("gold", "subtract"):
                {
                    int taken = Math.Min(step.Value, party.Purse.Coins + _coins);
                    _coins -= taken;
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"the party pays {taken} gold."));
                    return null;
                }

                case ("item", "add"):
                {
                    ItemDefinitionId item = new(step.Value.ToString(CultureInfo.InvariantCulture));
                    _carried[item.Value] = _carried.GetValueOrDefault(item.Value) + 1;
                    _items.Add(new InteractionItemYield(item));
                    return null;
                }

                case ("gold", "set"):
                {
                    // Setting the purse is a payment or a find of the difference, through the same net figure.
                    int held = party.Purse.Coins + _coins;
                    _coins += Math.Max(0, step.Value) - held;
                    return null;
                }

                case ("bank-gold", _):
                {
                    // The party's one balance: an addition adds, a subtraction the balance cannot cover takes nothing,
                    // and a set states it (OpenEnroth src/Engine/Objects/Character.cpp:4968-4970, :5660-5664,
                    // :4384-4386). Made once, as every write to what the party holds once is.
                    int held = Bank(party);
                    int after = op switch
                    {
                        "add" => (int)Math.Min(int.MaxValue, (long)held + Math.Max(0, step.Value)),
                        "subtract" => step.Value > held ? held : held - Math.Max(0, step.Value),
                        _ => Math.Max(0, step.Value),
                    };
                    if (after == held) return null;
                    _bank = after;
                    _effects.Add(() => party.Holdings.Hold(MightAndMagic7Services.BankHolding, after));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"the bank now keeps {after} gold for the party."));
                    return null;
                }

                case ("reputation", _):
                {
                    // The world's opinion, moved through this game's one reading of an event's figure and told to the
                    // one owner that moves it, as a deed whose figure is the move.
                    if (_rules._progression() is not { } progression)
                    {
                        return VariableNotInterpreted(_target, mapEvent, step, "a change of the world's opinion in a session that keeps no progression owner to move it through");
                    }

                    int before = Reputation(party);
                    int after = MightAndMagic7Standing.AfterEventStep(op, step.Value, before);
                    if (after == before) return null;
                    _reputation = after;
                    _effects.Add(() => progression.Deed(MightAndMagic7Standing.EventStepSource, after - before));
                    return null;
                }

                case ("food", _):
                {
                    // The larder: an addition finds food, a subtraction eats it and never below none, a set states it
                    // (OpenEnroth src/Engine/Objects/Character.cpp:4748-4752, :5243-5246, :4121-4124).
                    int held = Food(party);
                    int after = op switch
                    {
                        "add" => (int)Math.Min(int.MaxValue, (long)held + Math.Max(0, step.Value)),
                        "subtract" => Math.Max(0, held - step.Value),
                        _ => Math.Max(0, step.Value),
                    };
                    if (after == held) return null;
                    _food = after;
                    _effects.Add(() =>
                    {
                        int now = party.Food.Portions;
                        if (after > now) party.Food.Credit(after - now);
                        else party.Food.TryDebit(now - after);
                    });
                    _done.Add(after > held
                        ? string.Create(CultureInfo.InvariantCulture, $"the party finds {after - held} food.")
                        : string.Create(CultureInfo.InvariantCulture, $"the party's food falls to {after}."));
                    return null;
                }
                case ("counter", _):
                    // Any write to a counter sets it to now (OpenEnroth src/Engine/Objects/Character.cpp:4365-4376).
                    if (step.Index is < 0 or >= Counters) return VariableNotInterpreted(_target, mapEvent, step);
                    Keep(CounterKey(step.Index), _context.Clock?.Elapsed.Milliseconds ?? 0);
                    return null;
                case ("hireling", _):
                    return VariableNotInterpreted(_target, mapEvent, step, "this build keeps no hirelings, #8514");
                case ("history", "add" or "set"):
                    return History(mapEvent, step, party);
                case ("item", "subtract"):
                {
                    ItemDefinitionId item = new(step.Value.ToString(CultureInfo.InvariantCulture));
                    if (Carried(party, step.Value) <= 0) return null;
                    _carried[item.Value] = _carried.GetValueOrDefault(item.Value) - 1;
                    _effects.Add(() =>
                    {
                        if (party.Inventory.Find(item) is { } held) party.ConsumeItem(held.Id);
                    });
                    return null;
                }
            }

            return WriteMembers(mapEvent, step, who, party);
        }

        /// <summary>Collects a write to the members a step chooses.</summary>
        private Refusal? WriteMembers(MapEvent mapEvent, MapEventStep step, List<int> who, PartyEntity party)
        {
            string op = step.Op;
            Action<PartyMember, int>? write = (step.Variable, op) switch
            {
                ("hit-points", "add") => (member, index) =>
                {
                    Member(index, "hp", member.Resources.HitPoints.Current, step.Value);
                    _effects.Add(() => member.Resources.RestoreHitPoints(step.Value));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} regains {step.Value} hit points."));
                },
                ("hit-points", "subtract") => (member, index) =>
                {
                    Member(index, "hp", member.Resources.HitPoints.Current, -step.Value);
                    _effects.Add(() => member.TakeDamage(step.Value));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} loses {step.Value} hit points."));
                },
                ("full-hit-points", "add" or "set") => (member, index) =>
                {
                    // Filling a character's hit points is the donor's whole reading of adding to the maximum
                    // (OpenEnroth src/Engine/Objects/Character.cpp:4660-4664).
                    int missing = member.Resources.HitPoints.Maximum - Member(index, "hp", member.Resources.HitPoints.Current);
                    if (missing <= 0) return;
                    Member(index, "hp", member.Resources.HitPoints.Current, missing);
                    _effects.Add(() => member.Resources.RestoreHitPoints(missing));
                    _done.Add($"{member.Profile.Name} is whole again.");
                },
                ("full-spell-points", "add" or "set") => (member, index) =>
                {
                    int missing = member.Resources.SpellPoints.Maximum - Member(index, "sp", member.Resources.SpellPoints.Current);
                    if (missing <= 0) return;
                    Member(index, "sp", member.Resources.SpellPoints.Current, missing);
                    _effects.Add(() => member.Resources.RestoreSpellPoints(missing));
                    _done.Add($"{member.Profile.Name}'s spell points are full again.");
                },
                ("spell-points", "add") => (member, index) =>
                {
                    Member(index, "sp", member.Resources.SpellPoints.Current, step.Value);
                    _effects.Add(() => member.Resources.RestoreSpellPoints(step.Value));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} regains {step.Value} spell points."));
                },
                ("spell-points", "subtract") => (member, index) =>
                {
                    int spent = Math.Min(step.Value, member.Resources.SpellPoints.Current);
                    Member(index, "sp", member.Resources.SpellPoints.Current, -spent);
                    _effects.Add(() => member.Resources.TrySpendSpellPoints(Math.Min(spent, member.Resources.SpellPoints.Current)));
                },
                ("attribute", _) when Attribute(step.Which) is { } attribute => (member, index) =>
                {
                    // A character the content gives no such attribute has nothing for the step to change.
                    if (!member.Attributes.TryGet(attribute, out int held)) return;
                    int now = Member(index, $"attribute:{step.Which}", held);
                    int after = op switch
                    {
                        "add" => now + step.Value,
                        "subtract" => now - step.Value,
                        _ => step.Value,
                    };
                    _members[$"{index}:attribute:{step.Which}"] = after;
                    _effects.Add(() => member.Attributes.Set(attribute, after));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name}'s {attribute} is now {after}."));
                },
                ("resistance-bonus", "add" or "set") when DamageKind(step.Which) is { } kind && _rules._effects is { } effects => (member, index) =>
                {
                    int now = Member(index, $"resistance:{step.Which}", effects.MagnitudeOn(member, SpellEffectIds.Resistance(kind)));
                    int after = op == "add" ? now + step.Value : step.Value;
                    _members[$"{index}:resistance:{step.Which}"] = after;
                    GameDuration lasts = _rules._bonusLasts;
                    _effects.Add(() => effects.Resist(party, member, kind, after, lasts));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} resists {step.Which} by {after}."));
                },
                ("resistance", "add" or "set") when DamageKind(step.Which) is { } kind => (member, index) =>
                {
                    // A permanent gift to what the character resists by nature, kept on the character and
                    // capped at a byte (OpenEnroth src/Engine/Objects/Character.cpp:4788-4817).
                    int now = Member(index, $"stored:{step.Which}", member.Resistances.Of(kind));
                    int after = Math.Min(ResistanceLimit, op == "add" ? now + step.Value : step.Value);
                    _members[$"{index}:stored:{step.Which}"] = after;
                    _effects.Add(() => member.Resistances.Set(kind, after));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} resists {step.Which} by {after} for good."));
                },
                ("attribute-bonus", _) when Attribute(step.Which) is { } attribute && _rules._effects is { } effects => (member, index) =>
                {
                    // A temporary bonus is the running effect a spell raising the attribute leaves, which the fight
                    // reads in the attribute's own sum; it lasts fixture.bonus-hours.
                    int now = Member(index, $"bonus:{step.Which}", effects.MagnitudeOn(member, SpellEffectIds.Attribute(attribute)));
                    int after = op switch { "add" => now + step.Value, "subtract" => now - step.Value, _ => step.Value };
                    _members[$"{index}:bonus:{step.Which}"] = after;
                    GameDuration lasts = _rules._bonusLasts;
                    _effects.Add(() => effects.Leave(party, member, SpellEffectIds.Attribute(attribute), after, lasts));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name}'s {attribute} is raised by {after} for a while."));
                },
                ("armour-class-bonus", _) when _rules._effects is { } effects => (member, index) =>
                {
                    // The armour a stone skin leaves on the character, which the armour class's own sum reads.
                    int now = Member(index, "armour-bonus", effects.MagnitudeOn(member, SpellEffectIds.Armour));
                    int after = op switch { "add" => now + step.Value, "subtract" => now - step.Value, _ => step.Value };
                    _members[$"{index}:armour-bonus"] = after;
                    GameDuration lasts = _rules._bonusLasts;
                    _effects.Add(() => effects.Leave(party, member, SpellEffectIds.Armour, after, lasts));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name}'s armour class is changed by {after} for a while."));
                },
                ("skill-points", "add") when _rules._progression() is { } progression => (member, _) =>
                {
                    _effects.Add(() => progression.Gift(member.Id, 0, step.Value));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} gains {step.Value} skill points."));
                },
                ("experience", "add") when _rules._progression() is { } progression => (member, index) =>
                {
                    Member(index, "experience", 0, step.Value);
                    _effects.Add(() => progression.Gift(member.Id, step.Value, 0));
                    _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} gains {step.Value} experience."));
                },
                ("age", "set" or "add") => (member, _) =>
                {
                    // The donor's age modifier: a set restores it, an addition ages the character (OpenEnroth
                    // src/Engine/Objects/Character.cpp:4084-4086 and 4686-4688). This build's offset counts
                    // years only upward, so a set to anything restores youth and then ages by the figure.
                    int years = Math.Max(0, step.Value);
                    _effects.Add(() =>
                    {
                        if (op == "set") member.Progression.Rejuvenate();
                        if (years > 0) member.Progression.Age(years);
                    });
                    _done.Add(op == "set" && years == 0 ? $"{member.Profile.Name} is young again." : $"{member.Profile.Name} ages.");
                },
                ("class", "set") when step.Which.Length > 0 => (member, index) => Promote(mapEvent, step, member, index),
                ("major-condition", "set") => (member, _) =>
                {
                    // Setting the worst condition clears every condition (OpenEnroth src/Engine/Objects/Character.cpp:4346-4349).
                    _effects.Add(() => member.Conditions.ClearAll());
                    _done.Add($"{member.Profile.Name} is cured of everything.");
                },
                ("condition", "add" or "set") when Condition(step.Which) is { } condition => (member, index) =>
                {
                    _members[$"{index}:condition:{step.Which}"] = 1;
                    _effects.Add(() => member.Conditions.Apply(new ActiveCondition(condition)));
                    _done.Add($"{member.Profile.Name} is {condition}.");
                },
                ("condition", "subtract") when Condition(step.Which) is { } condition => (member, index) =>
                {
                    _members[$"{index}:condition:{step.Which}"] = 0;
                    _effects.Add(() => member.Conditions.Clear(condition));
                    _done.Add($"{member.Profile.Name} is no longer {condition}.");
                },
                _ => null,
            };
            if (write is null) return VariableNotInterpreted(_target, mapEvent, step);
            foreach (int index in who)
            {
                write(party.Members[index], index);
                if (_refused is { } refused) return refused;
            }

            return null;
        }

        /// <summary>The class a member stands in as the run has left it.</summary>
        private string ClassOf(PartyMember member, int index) =>
            _classes.TryGetValue(index, out string? changed) ? changed : member.Profile.Class.Value;

        /// <summary>
        /// Collects a step making a member the class it names: the rank of this game's ladder that leads there, given
        /// through the one writer of ranks on the terms the program has judged.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The donor writes the class number and nothing else (OpenEnroth <c>src/Engine/Objects/Character.cpp:4028-4029</c>);
        /// the shipped programs write it only in a promoter's own events, each raising every member of the class it
        /// promotes from after the event's own checks of what the party brought. Here the change is the ladder's rank
        /// from the member's class to the one named (<see cref="MightAndMagic7Promotions"/>), given through
        /// <see cref="PartyProgression.Grant"/>, so the class, the rank and the record a rank leaves move together as
        /// every promotion's do, the light or dark alternative is the rank the class names, and whether the member may
        /// rise at all is judged in the one place every rank is. A change the ladder states no rank for, or one the
        /// member cannot take, refuses the run before anything is settled; a member already of the class is left as
        /// they are.
        /// </para>
        /// <para>
        /// <b>Ours.</b> The donor's own side effect of making somebody a Lich — filling the empty lich jar they carry
        /// (<c>Character.cpp:4030-4036</c>) — is not kept: a jar here is what the rank's errand brings back, not an item
        /// the class fills.
        /// </para>
        /// </remarks>
        private void Promote(MapEvent mapEvent, MapEventStep step, PartyMember member, int index)
        {
            string from = ClassOf(member, index);
            if (string.Equals(from, step.Which, StringComparison.Ordinal)) return;
            if (_rules._progression() is not { Promotions: { } ladder } progression)
            {
                _refused = VariableNotInterpreted(_target, mapEvent, step, "a change of class in a session that keeps no ladder of ranks to make it through");
                return;
            }

            PromotionRank? rank = ladder.Ladder.From(new ClassId(from)).FirstOrDefault(candidate => string.Equals(candidate.To.Value, step.Which, StringComparison.Ordinal));
            if (rank is null)
            {
                _refused = NotInterpreted(_target, mapEvent, step, $"a change of {member.Profile.Name}'s class from {from} to {step.Which}, which this game's ladder states no rank for");
                return;
            }

            if (progression.JudgeGrant(rank.Id, member.Id) is { } refusal)
            {
                _refused = new Refusal(
                    refusal.Code,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} makes {member.Profile.Name} a {step.Which}: {refusal.Message} Nothing was changed."));
                return;
            }

            _classes[index] = step.Which;
            _effects.Add(() => progression.Grant(rank.Id, member.Id));
            _done.Add($"{member.Profile.Name} is now a {step.Which}.");
        }


        /// <summary>Collects the note a step writes.</summary>
        private Refusal? Learn(MapEvent mapEvent, MapEventStep step)
        {
            if (_rules._events.DiscoveryOf(step.Value) is not { } discovery)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureDiscoveryUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} writes note {step.Value}, and the loaded discovery table has no such row: nothing was changed."));
            }

            // The row is the fact, wherever it was learned: the donor keeps one bit per row
            // (OpenEnroth src/Engine/Party.h:329), and a row's own words say where it was found.
            _records[DiscoverySubject(discovery.Number)] = true;
            _learned.Add(new KnowledgeReport(KindOf(discovery.Category), Source, DiscoverySubject(discovery.Number), discovery.Text));
            return null;
        }

        /// <summary>Collects a harm to the members a step names.</summary>
        private Refusal? Harm(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            (List<int> who, Refusal? refused) = Choose(mapEvent, step);
            if (refused is not null) return refused;

            // The harm lands through the member's own damage entry, as a trap's does; the donor first reduces
            // it by the member's resistance to its kind (src/Engine/Objects/Character.cpp, receiveDamage), which
            // this game does not, so a fixture's harm is the record's own figure.
            foreach (int index in who)
            {
                PartyMember member = party.Members[index];
                _effects.Add(() => member.TakeDamage(step.Amount));
                _done.Add(string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} takes {step.Amount} {step.Kind} damage."));
            }

            return null;
        }

        /// <summary>Collects a door step: the door the place holds under the step's id, and the state it leaves it in.</summary>
        /// <remarks>
        /// The door is the door owner's target (<see cref="MightAndMagic7Interaction"/>), read in the word its
        /// own use reads and recorded under its own identity, so a door a lever opened reads as open when the
        /// party walks up to it. Open and close move a door to that end; toggle moves a door at rest to the
        /// other (OpenEnroth <c>src/Engine/Graphics/Indoor.cpp:721-770</c>). A door id the place does not hold
        /// moves nothing, as the donor's own lookup does. At most one door matches: a place holding two under one
        /// number is refused while the world is built (<c>interaction-door-number-reused</c>), so the lookup below
        /// never chooses.
        /// </remarks>
        private Refusal? Door(MapEvent mapEvent, MapEventStep step)
        {
            if (step.Action is not ("open" or "close" or "toggle"))
            {
                return NotInterpreted(_target, mapEvent, step, $"a door step whose action '{step.Action}' this game does not read");
            }

            PlacementDefinition? door = _context.PlaceTargets.FirstOrDefault(placement =>
                string.Equals(placement.Content.Kind, MightAndMagic7Interaction.DoorPlacementKind, StringComparison.Ordinal) &&
                placement.Source.GetInt32(DoorIdField) == step.Door);
            if (door is null)
            {
                Residue(string.Create(CultureInfo.InvariantCulture, $"Door {step.Door} is not one this place holds, so nothing moved there."));
                return null;
            }

            string now = _changes.TryGetValue(door.Content, out string? changed)
                ? changed
                : MightAndMagic7Interaction.DoorState(door, _context.TargetState(door.Content));
            bool open = string.Equals(now, MightAndMagic7Interaction.OpenState, StringComparison.Ordinal);
            bool opens = step.Action switch { "open" => true, "close" => false, _ => !open };
            if (opens == open) return null;
            _changes[door.Content] = opens ? MightAndMagic7Interaction.OpenState : MightAndMagic7Interaction.ClosedState;
            _done.Add(opens ? "A door opens." : "A door closes.");
            Residue(CollisionResidue);
            return null;
        }

        /// <summary>Collects an item gift: the item the step names, or one drawn at its treasure level.</summary>
        private Refusal? Give(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is null) return NoParty(mapEvent, step);
            if (step.Item != 0)
            {
                ItemDefinitionId named = new(step.Item.ToString(CultureInfo.InvariantCulture));
                _carried[named.Value] = _carried.GetValueOrDefault(named.Value) + 1;
                _items.Add(new InteractionItemYield(named));
                return null;
            }

            if (_rules._loot is not { } loot || Rolls(mapEvent) is not { } rolls)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureNothingToRoll,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} gives an item drawn at treasure level {step.Level}, and this product has no loot table or random service to draw it with: nothing was changed."));
            }

            if (loot.Given(step.Level, new LootFilter(step.ItemKind, step.ItemSkill), rolls) is not { } item) return null;
            _carried[item.Definition.Value] = _carried.GetValueOrDefault(item.Definition.Value) + item.Count;
            _items.Add(new InteractionItemYield(item.Definition, item.Count));
            return null;
        }

        /// <summary>Collects a spell a fixture casts at the party: the spell's own roll at the step's rank and mastery.</summary>
        /// <remarks>
        /// The donor launches the spell from a point of the map at the party and lets it fly (OpenEnroth
        /// <c>src/Engine/Spells/Spells.cpp:548-600</c>); this build flies nothing, so the spell lands on the
        /// characters the run has chosen — the active one unless a step chose others — at the roll its own row
        /// states, unreduced like a fixture's other harm. An approximation of where a bolt or a blast lands.
        /// </remarks>
        private Refusal? Cast(MapEvent mapEvent, MapEventStep step, List<int> who)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            string id = step.Spell.ToString(CultureInfo.InvariantCulture);
            if (_rules._spells is not { } spells || spells.Spell(id) is not { } spell)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureSpellUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} casts spell {id}, and the loaded spell table holds no such spell: nothing was changed."));
            }

            if (spells.Harm(spell) is not { } kind)
            {
                return NotInterpreted(_target, mapEvent, step, $"a cast of '{spell.Name}', which does no harm this game reads");
            }

            if (Rolls(mapEvent) is not { } rolls)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureNothingToRoll,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} casts '{spell.Name}', and this product has no random service to roll it with: nothing was changed."));
            }

            DamageRoll roll = spells.Damage(spell, step.Rank, Rung(step.Mastery));
            foreach (int index in who)
            {
                PartyMember member = party.Members[index];
                int harm = roll.Roll(rolls.Under(string.Create(CultureInfo.InvariantCulture, $"cast/{step.Step}/{index}")), "harm");
                _effects.Add(() => member.TakeDamage(harm));
                _done.Add(string.Create(CultureInfo.InvariantCulture, $"{spell.Name} strikes {member.Profile.Name} for {harm} {kind} damage."));
            }

            return null;
        }

        /// <summary>Collects a call to one of the people: the conversation the use opens with them.</summary>
        private Refusal? Speak(MapEvent mapEvent, MapEventStep step)
        {
            string id = string.Create(CultureInfo.InvariantCulture, $"npc-{step.Person}");
            if (_rules._people(id) is not { } person)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} calls over person {step.Person}, and the loaded people table holds nobody of that id: nothing was changed."));
            }

            _speaks = new ConversationSubject(id, [person]);
            return null;
        }

        /// <summary>Whether any chosen character holds a skill at the step's rank and exactly its mastery.</summary>
        /// <remarks>OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:517-524</c>: the rank at least, the mastery exactly.</remarks>
        private (bool Holds, Refusal? Refused) CheckSkill(MapEvent mapEvent, MapEventStep step, List<int> who)
        {
            if (Party is not { } party) return (false, NoParty(mapEvent, step));
            if (SkillOf(step.Which) is not { } skill || Rung(step.Mastery) == 0)
            {
                return (false, NotInterpreted(_target, mapEvent, step, $"a jump on the skill '{step.Which}' at '{step.Mastery}'"));
            }

            foreach (int index in who)
            {
                PartyMember member = party.Members[index];
                if (member.Skills.TryGet(skill, out SkillEntry entry) && entry.Level >= step.Rank && entry.Tier.Value == Rung(step.Mastery)) return (true, null);
            }

            return (false, null);
        }

        /// <summary>Collects a change of which topic one of a person's slots raises, which the party's records keep.</summary>
        /// <remarks>
        /// The donor writes the slot on the person's own record (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:449-468</c>);
        /// here it is a party record the conversation reads (<see cref="MightAndMagic7TopicSlots"/>). The donor's own
        /// side effect of one particular change — a guild door opening while a particular person speaks
        /// (<c>EvtInterpreter.cpp:458-466</c>) — is a house screen this build does not have, and is not kept.
        /// </remarks>
        private Refusal? TopicSlot(MapEvent mapEvent, MapEventStep step)
        {
            if (Party is not { } party) return NoParty(mapEvent, step);
            string id = string.Create(CultureInfo.InvariantCulture, $"npc-{step.Person}");
            if (_rules._people(id) is null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixturePersonUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} changes a topic of person {step.Person}, and the loaded people table holds nobody of that id: nothing was changed."));
            }

            if (step.Index is < 0 or >= MightAndMagic7TopicSlots.Slots)
            {
                return NotInterpreted(_target, mapEvent, step, string.Create(CultureInfo.InvariantCulture, $"a change of topic slot {step.Index}, which a person does not have"));
            }

            int slot = step.Index;
            int raises = Math.Max(0, step.Raises);
            _effects.Add(() => MightAndMagic7TopicSlots.Change(party.Records, id, slot, raises));
            return null;
        }

        /// <summary>Whether the dead a step counts are dead: by group, kind, one creature, or all of the place's.</summary>
        /// <remarks>
        /// The donor's count (OpenEnroth <c>src/Engine/Objects/Actor.cpp:2811-2834</c>): with a number, at least that
        /// many of the matching actors are dead; without one, every matching actor is, which a place holding none of
        /// them answers yes to. A group is the placement's own group, a kind is its monster row, and one creature is
        /// the map's own actor record by its index — which the donor also numbers the creatures its spawn points add
        /// after them, at load; those are not numbered here, so a count of one creature reads only the map's own
        /// actors: its people and the creatures the level is built holding, each under the record's own index.
        /// </remarks>
        private (bool Holds, Refusal? Refused) Killed(MapEvent mapEvent, MapEventStep step)
        {
            Func<PlacementDefinition, bool>? matches = step.Which switch
            {
                "any" => _ => true,
                "group" => placement => placement.Source.GetInt32(MightAndMagic7MonsterAi.GroupField) == step.Value,
                "kind" => placement => string.Equals(
                    placement.Source.GetId(MightAndMagic7Combat.MonsterField),
                    step.Value.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal),
                "creature" => placement => string.Equals(placement.SourceField, ActorSourceField, StringComparison.Ordinal) && placement.SourceIndex == step.Value,
                _ => null,
            };
            if (matches is null) return (false, NotInterpreted(_target, mapEvent, step, $"a count of the dead by '{step.Which}'"));
            if (_rules._actors(mapEvent.Global ? _context.Place : mapEvent.Place) is not { } actors)
            {
                return (false, NotInterpreted(_target, mapEvent, step, "a count of the dead in a session that keeps no population to count"));
            }

            int total = 0;
            int dead = 0;
            foreach (PlaceActor actor in actors)
            {
                if (!matches(actor.Placement)) continue;
                total++;
                if (actor.Down) dead++;
            }

            return (step.Amount > 0 ? dead >= step.Amount : dead == total, null);
        }

        /// <summary>Collects a summoning: the creatures of one of the place's encounter slots put on the field at the step's point.</summary>
        /// <remarks>
        /// The slot is resolved as a spawn record's is (<see cref="MightAndMagic7Spawns.Summoned"/>), now, so a run that
        /// cannot draw what it summons is refused before anything is settled; the creatures enter the place's live
        /// population when the run is applied, through the population's own creation, and the fight reads them as it
        /// reads every creature — a goblin summoned in ambush is the party's enemy at the band the matrix gives its kind
        /// toward the party, and one joining a group the event turns hostile is an enemy by that. They stand for the visit: the donor's own are
        /// saved with the map, while this build's schema carries no population, so a save taken while one stands is
        /// refused by name as one taken beside a summoned elemental is (#8658).
        /// </remarks>
        private Refusal? Summon(MapEvent mapEvent, MapEventStep step)
        {
            if (step.Summons is not { } slot)
            {
                return NotInterpreted(_target, mapEvent, step, "a summoning whose encounter the place's map table resolves to no creature");
            }

            if (_rules._population(mapEvent.Place) is not { } population)
            {
                return NotInterpreted(_target, mapEvent, step, "a summoning in a session that keeps no live population of this place to put creatures in");
            }

            string id = string.Create(CultureInfo.InvariantCulture, $"event-{mapEvent.Place}-{mapEvent.Id}-{step.Step}-{++_rules._summoned}");
            IReadOnlyList<PlacementDefinition> creatures = MightAndMagic7Spawns.Summoned(id, slot, step.Amount, step.Position, step.Group, Rolls(mapEvent), out string? unresolved);
            if (unresolved is not null)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureNothingToRoll,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', whose step {step.Step} summons creatures, and {unresolved}: nothing was changed."));
            }

            _effects.Add(() =>
            {
                foreach (PlacementDefinition creature in creatures) population.Summon(creature);
            });
            foreach (IGrouping<string, PlacementDefinition> kind in creatures.GroupBy(creature => creature.Source.GetString("monsterName"), StringComparer.Ordinal))
            {
                _done.Add(string.Create(CultureInfo.InvariantCulture, $"{kind.Count()} {kind.Key} appear."));
            }

            return null;
        }

        /// <summary>
        /// Collects a group of the place's creatures turned hostile or made peaceful again, or hidden or shown again, which
        /// the place keeps.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The donor sets the aggressor bit on every actor of the group (OpenEnroth <c>src/Engine/Objects/Actor.cpp:3823-3851</c>),
        /// which makes it the party's enemy at the longest band (<c>Actor.cpp:2155-2156</c>) and is saved with the map.
        /// Here the place keeps it under <c>hostile-group:</c> and the group, the fight reads it in the creature's own
        /// nature (<see cref="MightAndMagic7Combat"/>), and the clock's restoring the place forgets it as the donor's
        /// re-read delta does; a group of zero is the donor's "no group" and changes nothing.
        /// </para>
        /// <para>
        /// The donor's other bit an event sets on a group is the one a map's own record holds a creature hidden by
        /// (<see cref="HiddenFlag"/>): setting it takes the group off the field and clearing it stands the group again
        /// (<c>Actor.cpp:3823-3851</c>). The place keeps it under <c>hidden-group:</c> and the group, which the
        /// population reads when the place is populated (<see cref="MightAndMagic7Spawns.Stands"/>) — so a group shown
        /// again stands even where its records say the level holds it hidden — and a group hidden while the party stands
        /// in the place leaves the field at once. <b>Ours</b>: a group shown again while the party is there stands the
        /// next time the place is populated rather than at once, because a creature put on the field during a visit is a
        /// summoning, which a save refuses (#8658).
        /// </para>
        /// </remarks>
        private Refusal? GroupFlag(MapEvent mapEvent, MapEventStep step)
        {
            if (step.Flag == HiddenFlag) return HideGroup(mapEvent, step);
            if (step.Flag != AggressorFlag)
            {
                return NotInterpreted(_target, mapEvent, step, string.Create(CultureInfo.InvariantCulture, $"a creature flag 0x{step.Flag:X} this game does not read"));
            }

            if (step.Group == 0) return null;
            string key = HostileGroupKey(step.Group);
            bool was = Kept(key) is > 0;
            Keep(key, step.On ? 1 : 0);
            if (was != step.On) _done.Add(step.On ? "The creatures here turn on the party." : "The creatures here are calm again.");
            return null;
        }

        /// <summary>Collects a group of the place's creatures hidden, or shown again, which the place keeps.</summary>
        private Refusal? HideGroup(MapEvent mapEvent, MapEventStep step)
        {
            if (step.Group == 0) return null;
            string key = HiddenGroupKey(step.Group);
            bool was = Kept(key) is > 0;
            Keep(key, step.On ? 1 : 0);
            if (!step.On || was) return null;
            PlaceId place = mapEvent.Global ? _context.Place : mapEvent.Place;
            if (_rules._population(place) is not { } population) return null;
            int group = step.Group;
            _effects.Add(() =>
            {
                foreach (PlacePopulationEntity entity in population.Entities.ToArray())
                {
                    if (MightAndMagic7Spawns.GroupOf(entity.Placement) == group) population.Dismiss(entity);
                }
            });
            _done.Add("The creatures here are gone from sight.");
            return null;
        }

        /// <summary>Collects a history line written into the party's journal, dated now and worded as the table words it.</summary>
        /// <remarks>
        /// The donor writes a history slot once, at the time it is first set (OpenEnroth
        /// <c>src/Engine/Objects/Character.cpp:3995-4002</c>); the journal keeps one line per slot for the same reason.
        /// The line's marks are filled as the donor's book fills them (<c>src/GUI/GUIWindow.cpp:953-965</c>): the day,
        /// written as this build writes every day, and the party's characters by position.
        /// </remarks>
        private Refusal? History(MapEvent mapEvent, MapEventStep step, PartyEntity party)
        {
            if (_rules._events.HistoryOf(step.Index) is not { } line)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureHistoryUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs {mapEvent.Name}, whose step {step.Step} writes history line {step.Index}, and the loaded history table has no such line: nothing was changed."));
            }

            if (_rules._journal() is not { } journal || _context.Clock is not { } clock)
            {
                return VariableNotInterpreted(_target, mapEvent, step, "a history line in a session that keeps no journal to write it in");
            }

            StringBuilder text = new(line.Text.Replace("{date}", clock.Now.DayText, StringComparison.Ordinal));
            for (int position = 1; position <= 4; position++)
            {
                string name = position <= party.Members.Count ? party.Members[position - 1].Profile.Name : string.Empty;
                text.Replace(string.Create(CultureInfo.InvariantCulture, $"{{member:{position}}}"), name);
            }

            string written = text.ToString();
            string subject = string.Create(CultureInfo.InvariantCulture, $"history:{line.Slot}");
            if (journal.Entries.Any(entry => entry.Kind == JournalEntryKind.Chronicle && string.Equals(entry.Subject, subject, StringComparison.Ordinal))) return null;
            _effects.Add(() => journal.Record(new JournalEvent(JournalEntryKind.Chronicle, Source, subject, written)));
            _done.Add(line.Title.Length > 0 ? $"A page is written in the party's history: \"{line.Title}\"." : "A page is written in the party's history.");
            return null;
        }

        private void Residue(string line)
        {
            if (!_residue.Contains(line, StringComparer.Ordinal)) _residue.Add(line);
        }

        private bool HasRecord(string name) =>
            _records.TryGetValue(name, out bool pending) ? pending : Party?.Records.Has(name) == true;

        private void Record(string name, bool mark)
        {
            _records[name] = mark;
            PartyEntity party = Party!;
            _effects.Add(() =>
            {
                if (mark)
                {
                    if (!party.Records.Has(name)) party.Records.Mark(name);
                }
                else
                {
                    party.Records.Remove(name);
                }
            });
        }

        private bool Knows(int number)
        {
            if (_records.TryGetValue(DiscoverySubject(number), out bool pending)) return pending;
            if (_rules._events.DiscoveryOf(number) is not { } discovery || _rules._knowledge() is not { } knowledge) return false;
            return knowledge.Knows(new KnowledgeReport(KindOf(discovery.Category), Source, DiscoverySubject(number), discovery.Text));
        }

        private static bool Wears(PartyMember member, int item)
        {
            string id = item.ToString(CultureInfo.InvariantCulture);
            return member.Equipment.Items.Any(equipped => string.Equals(equipped.Item.Definition.Value, id, StringComparison.Ordinal));
        }

        private int Carried(PartyEntity party, int item)
        {
            string id = item.ToString(CultureInfo.InvariantCulture);
            return party.Inventory.TotalOf(new ItemDefinitionId(id)) + _carried.GetValueOrDefault(id);
        }

        /// <summary>The bank's balance as the run has left it.</summary>
        private int Bank(PartyEntity party) => _bank ?? party.Holdings.BalanceOf(MightAndMagic7Services.BankHolding);

        /// <summary>The party's reputation as the run has left it, in this game's sign.</summary>
        private int Reputation(PartyEntity party) => _reputation ?? party.Reputation.Reputation;

        /// <summary>The larder as the run has left it.</summary>
        private int Food(PartyEntity party) => _food ?? party.Food.Portions;

        /// <summary>A member's value as the run has left it, or as the member holds it when the run has not written it.</summary>
        private int Member(int index, string key, int held, int change = 0)
        {
            string slot = string.Create(CultureInfo.InvariantCulture, $"{index}:{key}");
            int now = _members.TryGetValue(slot, out int written) ? written : held;
            if (change != 0) _members[slot] = now + change;
            return now;
        }
    }
}

/// <summary>One creature or person a place holds, and whether it is down, which a step counting the dead reads.</summary>
/// <param name="Placement">The placement it stands on.</param>
/// <param name="Down">Whether it is dead or gone.</param>
internal readonly record struct PlaceActor(PlacementDefinition Placement, bool Down);

/// <summary>A topic a person's word raised, as the conversation states it: what it is called, what the table says, and its event.</summary>
/// <param name="Id">The topic's identity, which the word names.</param>
/// <param name="Label">What the topic is called.</param>
/// <param name="Words">What the topic table says for it, or empty when the event alone speaks.</param>
/// <param name="Event">The global event it raises, which is the topic's own number.</param>
internal sealed record SpokenTopic(string Id, string Label, string Words, int Event);
