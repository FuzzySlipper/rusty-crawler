using System.Globalization;
using System.Text;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
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
/// <b>Who a step acts on.</b> The donor starts a use's run on the active character
/// (<c>src/Engine/Evt/EvtInterpreter.cpp:623</c>) and lets a step choose one by position, the whole party, or
/// one at random (<c>src/Engine/Evt/EvtInterpreter.cpp:101-115</c>). This build selects no active character
/// (#8659), so the active one is read as the first member able to act. A write to something the party holds
/// once — a coin, a note, a bit — is made once whoever is chosen; the donor makes it once per chosen
/// character, which pays a whole-party gold step four times, and that is not kept.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Fixtures
{
    /// <summary>The placement kind the importer writes a fixture as.</summary>
    internal const string FixturePlacementKind = "fixture";

    /// <summary>The placement field that names the event a fixture or a decoration raises.</summary>
    internal const string EventField = "eventId";

    /// <summary>The placement field a fixture's region model name is written under.</summary>
    internal const string ModelNameField = "sourceModelName";

    /// <summary>The target kind a fixture is.</summary>
    internal const string TargetKind = "fixture";

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

    private readonly MightAndMagic7MapEvents _events;
    private readonly Func<PartyKnowledge?> _knowledge;
    private readonly MightAndMagic7SpellEffects? _effects;
    private readonly IRandomService? _random;
    private readonly GameDuration _bonusLasts;

    /// <summary>Creates this game's fixtures over the map events content carries.</summary>
    /// <param name="events">The map events and the discovery table.</param>
    /// <param name="knowledge">
    /// The party's knowledge, asked when a step compares a note: it is read through a call because the owner is
    /// composed after the world on the path that creates a party.
    /// </param>
    /// <param name="effects">The running effects a temporary resistance is left in, or null when this session keeps none.</param>
    /// <param name="random">The engine's random service a random jump draws from, or null when the product has none.</param>
    /// <param name="tuning">This game's tuning, which states how long a fixture's temporary bonus lasts.</param>
    internal MightAndMagic7Fixtures(
        MightAndMagic7MapEvents events,
        Func<PartyKnowledge?>? knowledge = null,
        MightAndMagic7SpellEffects? effects = null,
        IRandomService? random = null,
        TuningProfile? tuning = null)
    {
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _knowledge = knowledge ?? (() => null);
        _effects = effects;
        _random = random;
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
        if (!fixture && !decoration) return null;
        if (placement.Source.GetInt32(EventField) is not { } eventId || eventId == 0) return null;

        string label = _events.Find(request.Place, eventId)?.Label ?? string.Empty;
        string name = label.Length > 0
            ? label
            : placement.Source.GetString(MightAndMagic7Interaction.DecorationNameField) is { Length: > 0 } decorationName
                ? $"A fixture ({decorationName})"
                : "A fixture";
        return new InteractionTargetDefinition(
            new InteractionTargetKind(TargetKind),
            name,
            IsSign(placement) ? InteractionVerb.Read : InteractionVerb.Pull,
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

        // The timers that keep what this event reads run first, each once when its period has passed since it
        // last ran — and every one of them on the fixture's first use, which is the donor's own reading of a
        // timer on a map the party has not visited (OpenEnroth src/Engine/Evt/Processor.cpp:120-140).
        long now = context.Clock?.Elapsed.Milliseconds ?? 0;
        foreach ((MapEvent owner, MapEventStep timer) in _events.TimersFor(mapEvent))
        {
            string key = TimerKey(owner, timer);
            if (Period(timer) is not { } period)
            {
                return InteractionOutcome.Refused(NotInterpreted(target, owner, timer, $"a timer whose period '{timer.Period}' this game does not read"));
            }

            if (run.Kept(key) is { } last && now - last < period.Milliseconds) continue;
            if (run.Execute(owner, timer.Step + 1) is { } refused) return InteractionOutcome.Refused(refused);
            run.Keep(key, now);
        }

        if (run.Execute(mapEvent, 0) is { } refusal) return InteractionOutcome.Refused(refusal);
        return run.Settle();
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
                $"{target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', and its step {step.Step} is {what}, which this game does not interpret: nothing was changed."));

    /// <summary>The refusal for a variable this game does not interpret.</summary>
    private static Refusal VariableNotInterpreted(InteractionTargetDefinition target, MapEvent mapEvent, MapEventStep step) =>
        new(
            MightAndMagic7Codes.FixtureVariableNotInterpreted,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', and its step {step.Step} {step.Op}s the variable '{Describe(step)}', which this game does not interpret for that instruction: nothing was changed."));

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

    /// <summary>The subject a discovery row's note is kept under, which is the row's own number.</summary>
    internal static string DiscoverySubject(int number) =>
        string.Create(CultureInfo.InvariantCulture, $"discovery:{number}");

    /// <summary>The record a party bit is kept as.</summary>
    internal static string MemberBitRecord(int bit) =>
        string.Create(CultureInfo.InvariantCulture, $"member-bit:{bit}");

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
        private int _coins;
        private int _draws;

        internal Run(MightAndMagic7Fixtures rules, MapEvent mapEvent, InteractionTargetDefinition target, InteractionContext context)
        {
            _rules = rules;
            _event = mapEvent;
            _target = target;
            _context = context;
        }

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
                        string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}' past {StepLimit} steps without ending, so nothing was changed."));
                }

                int next = step + 1;
                switch (current.Op)
                {
                    case "exit":
                        return null;
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
                    case "status-text":
                        if (current.Text.Length > 0) _said.Add(current.Text);
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
                                string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', whose step {current.Step} chooses at random, and this product has no random service to choose with: nothing was changed."));
                        }

                        next = current.Targets[rolls.Pick(current.Targets.Count)];
                        break;
                    case "receive-damage":
                        if (Harm(mapEvent, current) is { } refusedHarm) return refusedHarm;
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

        /// <summary>Applies everything the run collected, and answers with what the use did.</summary>
        internal InteractionOutcome Settle()
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
                : _done.Count > 0
                    ? $"{_target.Name}: {string.Join(" ", _done)}"
                    : sign
                        ? $"The sign reads: \"{_event.Label}\"."
                        : $"{_target.Name}: nothing comes of it.";
            return InteractionOutcome.Applied(
                sign ? ReadState : UsedState,
                message,
                items: _items,
                gain: _coins > 0 ? PartyCost.OfGold(_coins) : null,
                learned: _learned,
                kept: _kept);
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
                string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', whose step {step.Step} reads or changes the party, and this world holds no party."));

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
                case "autonote":
                    return (Knows(step.Value), null);
                case "gold":
                    return (party.Purse.Coins + _coins >= step.Value, null);
                case "item":
                    return (Carried(party, step.Value) > 0, null);
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
                "condition" when Condition(step.Which) is { } condition =>
                    (member, index) => Member(index, $"condition:{step.Which}", member.Conditions.Has(condition) ? 1 : 0),
                _ => null,
            };
            if (read is null) return (false, VariableNotInterpreted(_target, mapEvent, step));

            foreach (int index in who)
            {
                if (read(party.Members[index], index) is not { } value) continue;

                // A condition compares whether it is held rather than how much of it: the donor's comparison
                // of one is a test of the bit (OpenEnroth src/Engine/Objects/Character.cpp:3826-3859).
                bool holds = step.Variable == "condition" ? value > 0 : value >= step.Value;
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
            foreach (int index in who) write(party.Members[index], index);
            return null;
        }

        /// <summary>Collects the note a step writes.</summary>
        private Refusal? Learn(MapEvent mapEvent, MapEventStep step)
        {
            if (_rules._events.DiscoveryOf(step.Value) is not { } discovery)
            {
                return new Refusal(
                    MightAndMagic7Codes.FixtureDiscoveryUnknown,
                    string.Create(CultureInfo.InvariantCulture, $"{_target.Name} runs map event {mapEvent.Id} of place '{mapEvent.Place}', whose step {step.Step} writes note {step.Value}, and the loaded discovery table has no such row: nothing was changed."));
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

        private int Carried(PartyEntity party, int item)
        {
            string id = item.ToString(CultureInfo.InvariantCulture);
            return party.Inventory.TotalOf(new ItemDefinitionId(id)) + _carried.GetValueOrDefault(id);
        }

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
