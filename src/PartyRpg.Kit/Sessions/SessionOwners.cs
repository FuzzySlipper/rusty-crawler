using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The mechanisms a session composes over its party, and the one sequence that composes them.
/// </summary>
/// <remarks>
/// <para>
/// <b>It exists before the session does, and is empty until the session composes it.</b> A game's answers
/// sometimes need an owner the session composes — a death counts toward the errands the quest owner holds, a
/// find is written in the journal — and those answers are composed before the session is. They are handed this
/// object and read its owners when an act actually arrives, which is always after the composition that made
/// them, so no answer ever reaches into a session that is still being built.
/// </para>
/// <para>
/// <b>One sequence, run once per party.</b> The owners are composed in dependency order — progression before
/// the counter that trains through it, rest before the inn that sells a night, the fight before the casting
/// that is paced by it — when the session is built and again when a created party is accepted, and each is
/// composed once: a mechanism that needs the party waits for it, and one that does not is composed at once.
/// A session that creates its party therefore ends up with exactly the owners a session handed its party does.
/// </para>
/// </remarks>
public sealed class SessionOwners
{
    private readonly List<IDeadlineOwner> _deadlineOwners = [];
    private SessionRules? _rules;
    private SessionRecords? _records;
    private bool _conversationsSeeTheParty;
    private bool _magicObserved;

    /// <summary>Creates the owners a session will compose, over its one clock.</summary>
    /// <param name="clock">The session's one game clock, or null when its game composed none.</param>
    /// <param name="diagnostics">Where the session reports what it did and refused, when reachable.</param>
    public SessionOwners(GameClock? clock = null, IDiagnosticsService? diagnostics = null)
    {
        Clock = clock;
        Diagnostics = new SessionDiagnostics(diagnostics);
    }

    /// <summary>The session's one game clock, or null when its game composed none.</summary>
    public GameClock? Clock { get; }

    /// <summary>Where the session reports, in the one shape every report takes.</summary>
    internal SessionDiagnostics Diagnostics { get; }

    /// <summary>The answers the owners were composed over, or none before the session composed them.</summary>
    public SessionRules Rules => _rules ?? SessionRules.None;

    /// <summary>The party the session plays, or null while it creates one or when content supplied none.</summary>
    public PartyEntity? Party { get; private set; }

    /// <summary>The live world the party walks in, or null while it creates one or when content supplied none.</summary>
    public SessionWorld? World { get; private set; }

    /// <summary>The party's one settlement path, which a shop, a road, and a turn-in all settle through.</summary>
    public PartyResourceLedger? Accounts { get; private set; }

    /// <summary>The one owner experience, a level, a skill point, and a rank move through.</summary>
    public PartyProgression? Progression { get; private set; }

    /// <summary>The one mechanism every stop is applied by.</summary>
    public PartyRest? Rest { get; private set; }

    /// <summary>The one mechanism every counter is served by.</summary>
    public PartyServices? Services { get; private set; }

    /// <summary>The one mechanism every person is spoken with by, composed with or without a party.</summary>
    public PartyConversations? Conversations { get; private set; }

    /// <summary>The one owner of what a party has been offered, taken, and finished.</summary>
    public PartyQuests? Quests { get; private set; }

    /// <summary>The one owner of what a party has written down.</summary>
    public PartyJournal? Journal { get; private set; }

    /// <summary>The one owner of what the party has learned.</summary>
    public PartyKnowledge? Knowledge { get; private set; }

    /// <summary>What the party has mapped.</summary>
    public PartyMaps? Maps { get; private set; }

    /// <summary>The one state every attack is paced by.</summary>
    public CombatState? Combat { get; private set; }

    /// <summary>What drives the other side of the fight, or null when the game gives no policy for it.</summary>
    public CombatDirector? Director { get; private set; }

    /// <summary>The one workflow every casting goes through.</summary>
    public Spellcasting? Casting { get; private set; }

    /// <summary>The one workflow every mixture goes through.</summary>
    public PotionMixing? Mixing { get; private set; }

    /// <summary>The owners that set deadlines on the clock, which a save and the deadline report ask about.</summary>
    internal IReadOnlyList<IDeadlineOwner> DeadlineOwners => _deadlineOwners;

    /// <summary>
    /// Binds the owners to the answers they are composed over, and composes what needs no party.
    /// </summary>
    /// <remarks>
    /// The deadline report is registered with the clock before any owner, so it asks who held a deadline
    /// before that owner acts on it: a one-shot deadline its owner re-arms is still reported as that owner's.
    /// </remarks>
    /// <exception cref="InvalidOperationException">These owners already belong to a session.</exception>
    internal void Bind(SessionRules rules, SessionRecords? records)
    {
        if (_rules is not null)
        {
            throw new InvalidOperationException(
                "These owners already belong to a session: a second session composed over them would share one party's quests, journal, and fight with it.");
        }

        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _records = records;
        Clock?.Observe(new DeadlineReport(this));
    }

    /// <summary>
    /// Takes the party the session plays and the world it walks in, and composes every owner that waited for it.
    /// </summary>
    /// <remarks>
    /// The ledger a journey charges is the one a shop charges, so a session handed no ledger settles through
    /// the world's own, and the place the party starts in is populated here so the first update plays in a
    /// furnished place rather than one update late.
    /// </remarks>
    internal void Take(PartyEntity? party, SessionWorld? world, PartyResourceLedger? accounts)
    {
        Party = party;
        World = world;
        Accounts = accounts ?? world?.Accounts;
        world?.Populate();
        Compose();
    }

    /// <summary>Composes, in dependency order, every owner whose answers and party are there.</summary>
    internal void Compose()
    {
        SessionRules rules = Rules;
        GameClock? clock = Clock;

        if (Progression is null && rules.Progression is { } growth && Party is { } grown)
        {
            Progression = new PartyProgression(growth.Rule, grown, rules.Skills, growth.Promotions);
        }

        // A room at an inn is a night's sleep, so the rest mechanism is composed before the counter that rents
        // one, and both over the one clock that tells every owner registered with it about every advance.
        if (Rest is null && rules.Rest is { } rest && Party is { } sleeper)
        {
            Rest = new PartyRest(rest, sleeper, clock, World, Accounts);
            Observe(Rest);
        }

        if (Services is null && rules.Service is { } service && Party is { } customer)
        {
            Services = new PartyServices(service, customer, Accounts, clock, Progression, Rest);
            Observe(Services);
        }

        // Composed over nobody when there is nobody, so who stands in a place is still published, and composed
        // again when a party arrives, because a conversation holds the party it was built with. Nothing is lost:
        // no conversation can be open while a party is being made.
        if (rules.Conversation is { } dialogue && (Conversations is null || (!_conversationsSeeTheParty && Party is not null)))
        {
            Conversations = new PartyConversations(dialogue, Party, clock);
            _conversationsSeeTheParty = Party is not null;
        }

        if (Quests is null && rules.Quests is { } quests && Party is { } errant)
        {
            Quests = new PartyQuests(quests, errant, Accounts, Progression, clock, _records?.Quests);
        }

        // The journal and the notes need the clock and nothing else: every line is dated in game time, and what
        // their books show is read from the owners that hold those facts when the projection is built.
        if (Journal is null && rules.Journal is { } journal && clock is not null)
        {
            Journal = new PartyJournal(journal, clock, _records?.Journal);
        }

        if (Knowledge is null && rules.Knowledge is { } knowledge && clock is not null)
        {
            Knowledge = new PartyKnowledge(knowledge, clock, _records?.Knowledge);
        }

        // The automap is composed over the places' own maps and never over the world's per-place state, so a
        // place the clock restores leaves what the party has seen of it exactly as it was.
        if (Maps is null && rules.Map is { } map)
        {
            Maps = new PartyMaps(map.Rule, map.Source, _records?.Maps);
        }

        if (Combat is null && rules.Combat is { } combat && Party is { } fighters)
        {
            Combat = new CombatState(combat, fighters, World, clock, Diagnostics.Service);
            Observe(Combat);
            if (combat.Ai is { } policy)
            {
                Director = new CombatDirector(Combat, policy, World?.Creatures, World?.Places, Diagnostics.Service);
            }
        }

        // The casting workflow is composed over the fight, so a cast and a swing are paced by one state.
        if (Casting is null && rules.Magic is { } magic && Party is { } casters)
        {
            Casting = new Spellcasting(casters, magic, Combat, rules.Names);
            if (!_magicObserved && magic.Time is { } lasting)
            {
                _magicObserved = true;
                Observe(lasting);
            }
        }

        if (Mixing is null && rules.Alchemy is { } alchemy && Party is { } mixers)
        {
            Mixing = new PotionMixing(mixers, alchemy.Mixtures, alchemy.Rule, Knowledge, rules.Names);
        }
    }

    /// <summary>Registers one more owner of game time with the one clock.</summary>
    private void Observe(IGameTimeObserver owner)
    {
        if (Clock is not { } clock) return;
        clock.Observe(owner);
        if (owner is IDeadlineOwner deadlines) _deadlineOwners.Add(deadlines);
    }

    /// <summary>Reports every deadline an advance brought due, and whose it was.</summary>
    private sealed class DeadlineReport(SessionOwners owners) : IGameTimeObserver
    {
        public void Observe(ClockAdvance advance)
        {
            foreach (DeadlineDue due in advance.Due)
            {
                bool owned = owners._deadlineOwners.Any(owner => owner.Holds(due.Deadline));
                owners.Diagnostics.Applied(
                    "clock",
                    "deadline-due",
                    owned
                        ? $"Game time reached {due.Fired}, which a deadline of {due.Deadline} was set for; the owner that set it heard it."
                        : $"Game time reached {due.Fired}, which a deadline of {due.Deadline} was set for; no owner the session composed holds it, so nothing acted on it.");
            }
        }
    }
}
