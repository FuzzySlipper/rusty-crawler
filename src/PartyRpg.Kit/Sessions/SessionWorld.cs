using System.Numerics;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The live world inside a session: where the party is, what state each place is in, and the one path
/// that moves the party between places.
/// </summary>
/// <remarks>
/// Every mechanism here is the general one: a place is a place, and travel is travel, whether the party
/// walked to a region's edge, stepped through a door, or arrived by a scripted move at the start of the
/// game. Arriving and travelling both mark the place visited, so knowledge accrues the same way
/// wherever the party goes.
/// </remarks>
public sealed class SessionWorld : IDisposable, IGameTimeObserver, IInteractionWorld, IRestSite, ICombatWorld
{
    private readonly TransitionExecutive _transitions;
    private readonly IDisposable? _clockSubscription;
    private readonly IFallRule? _falls;
    private readonly ICorpseSource? _corpses;
    private readonly IWorldTimeSource? _time;
    private readonly GameClock? _clock;
    private readonly PartyResourceLedger? _resources;
    private readonly PartyEntity? _entity;
    private readonly PlacePopulation _population;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly Dictionary<PlaceId, PlaceEntrance[]> _entrances;
    private readonly InteractionLedger _interactions = new();
    private MovementDiagnostics _movement = MovementDiagnostics.None;
    private bool _disposed;

    /// <summary>Creates the world a session steps.</summary>
    /// <param name="graph">The places and the transitions between them.</param>
    /// <param name="party">The party's one position and facing.</param>
    /// <param name="places">Per-place runtime state.</param>
    /// <param name="costRule">The rule every transition is quoted through.</param>
    /// <param name="time">
    /// Where elapsed game days come from, which a product hands the one clock. Without a source the world
    /// does not advance.
    /// </param>
    /// <param name="mover">
    /// The party's movement, when the engine gave the world something to walk in. Without one the party
    /// has no motion at all, and every mechanism that would move it says so by doing nothing.
    /// </param>
    /// <param name="diagnostics">
    /// Where a fall the tuning priced, a cost that had no account to land in, and a use that was refused are
    /// reported. The report is the only thing that happens to a fall here: the party's health is not this
    /// owner's.
    /// </param>
    /// <param name="entrances">
    /// The transitions a walking party can take, each with the reach in its place that takes it. Without
    /// any, walking moves the party and never the place, which is what content that declares no entrances
    /// gets.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, which a journey charges its time to. Without one the quotes travel states
    /// still hold, and the time part of a transition cannot be applied.
    /// </param>
    /// <param name="resources">
    /// The party's own accounts, which a journey charges its provisions to. Without one the party's larder
    /// is not this world's to reach, and the food part of a transition cannot be applied.
    /// </param>
    /// <param name="partyEntity">
    /// The party itself, which an interaction reaches for what it requires and gives. It is borrowed, never
    /// owned: the session holds the party and disposes it, and this world only reads it — the same way it
    /// borrows the party's accounts to charge a road.
    /// </param>
    /// <param name="interaction">
    /// How a use is resolved in this game, when its ruleset answers for one. Without it the world has no
    /// interaction at all: walking still moves the party, and nothing can be used, which is the honest state
    /// of a ruleset that has not answered.
    /// </param>
    /// <param name="schedule">
    /// Which places are clocked, as this game's content says. It is what a door's hours are read from and
    /// what the panel shows about the town the party stands in. Without one every place is open at every
    /// hour, because nothing has said otherwise.
    /// </param>
    /// <param name="creatures">
    /// How the place's creatures move, which is the same engine service and the same collision scene the
    /// party walks in. Without one creatures stand where content placed them: a product with no engine has
    /// nothing to move them with, and says so rather than sliding them through walls.
    /// </param>
    /// <param name="falls">
    /// What a landing past the tuning's threshold does to each member. Without one a fall is measured and
    /// reported and harms nobody, which is what a game that states no fall damage asks for.
    /// </param>
    /// <param name="vitals">
    /// The game's answer about what each placed creature can take, which gives a creature its health the
    /// moment the population places it. Without one no creature carries health, and harm aimed at one lands
    /// nowhere.
    /// </param>
    /// <param name="expansion">
    /// The game's answer about placements that state a request rather than an answer — an encounter that
    /// asks for some creatures of a kind — which the population resolves while it reads the places. Without
    /// one every placement stands as content states it.
    /// </param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public SessionWorld(
        PlaceGraph graph,
        PartyPoseOwner party,
        PlaceStateLedger places,
        ITravelCostRule costRule,
        IWorldTimeSource? time = null,
        IPartyMover? mover = null,
        IDiagnosticsService? diagnostics = null,
        IReadOnlyList<PlaceEntrance>? entrances = null,
        GameClock? clock = null,
        PartyResourceLedger? resources = null,
        PartyEntity? partyEntity = null,
        InteractionPolicy? interaction = null,
        PlaceSchedule? schedule = null,
        ICreatureMover? creatures = null,
        IFallRule? falls = null,
        ICreatureVitals? vitals = null,
        IPlacementExpansion? expansion = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(places);
        _transitions = new TransitionExecutive(costRule);
        // A world handed a clock but no separate day source reads its days from that same clock: in a
        // product they are one object, and a second source here would be a second answer to what day it is.
        _time = time ?? clock;
        _clock = clock;
        // The world lives through the days its clock crosses, whoever moved it, and stops hearing that clock
        // when it is released — a world replaced by another must not be told about a day it no longer holds.
        _clockSubscription = clock?.Observe(this);
        _resources = resources;
        _entity = partyEntity;
        _falls = falls;
        _diagnostics = diagnostics;
        Graph = graph;
        Party = party;
        Places = places;
        Places.MarkVisited(party.Place);
        _population = new PlacePopulation(graph, places, vitals is null ? null : new CreatureComposer(vitals), expansion);
        _entrances = Index(graph, entrances);
        Mover = mover;
        Creatures = creatures;
        Schedule = schedule ?? PlaceSchedule.Empty;
        Interaction = interaction is null ? null : new PartyInteraction(this, interaction.Rule, interaction.Space, interaction.Tuning, interaction.Corpses, interaction.Selection);
        _corpses = interaction?.Corpses;
        // The place the party starts in is entered exactly as any other is, so the scene it walks in is
        // filled from that place's content before the first step rather than one arrival late.
        mover?.Enter(party.Place);
    }

    /// <summary>The places and the transitions between them.</summary>
    public PlaceGraph Graph { get; }

    /// <summary>The party's one position and facing.</summary>
    public PartyPoseOwner Party { get; }

    /// <summary>Per-place runtime state.</summary>
    public PlaceStateLedger Places { get; }

    /// <summary>The party's movement, or null when the world has no engine to move in.</summary>
    public IPartyMover? Mover { get; }

    /// <summary>
    /// How the place's creatures move, or null when this world has no engine to move them in.
    /// </summary>
    /// <remarks>
    /// The mover is the world's own because the collision scene is: a creature walks in the ground the place
    /// admitted for the party, so the two cannot end up in different worlds. Whoever drives the opposition
    /// asks for it here rather than composing a second scene of its own.
    /// </remarks>
    public ICreatureMover? Creatures { get; }

    /// <summary>
    /// Which places are clocked and when their doors stand open, as this game's content says.
    /// </summary>
    /// <remarks>
    /// The schedule is read here rather than kept beside the clock: the clock owns the time, and a schedule
    /// is content's answer about which hours a place keeps, so every question is answered against the
    /// clock's own position at the moment it is asked. The interaction mechanism's ruleset holds the same
    /// schedule, so a door and the panel cannot disagree about whether a shop is shut.
    /// </remarks>
    public PlaceSchedule Schedule { get; }

    /// <summary>What the party's movement has done so far, as an observation rather than as state anything steps.</summary>
    public MovementDiagnostics Movement => _movement;

    /// <summary>The place the party is in.</summary>
    public PlaceId Place => Party.Place;

    /// <summary>The entities the current place is populated with, and the one owner that steps them.</summary>
    public PlacePopulation Population => _population;

    /// <summary>
    /// What the party can use, or null when this world's ruleset answered no interaction policy. The
    /// mechanism is the world's, and the session steps it inside the one admitted update.
    /// </summary>
    public PartyInteraction? Interaction { get; }

    /// <summary>What the party's last use did, or null before it has used anything.</summary>
    public InteractionResult? LastInteraction => Interaction?.LastResult;

    /// <summary>
    /// The party's own accounts, which a journey charges its provisions to and a use's price settles
    /// against. It is null when the world was composed without them, and the service mechanism the session
    /// composes reads the same ledger from here when the ruleset handed it none directly.
    /// </summary>
    public PartyResourceLedger? Accounts => _resources;

    /// <summary>
    /// Lives through the days an advance of the one clock crossed, whoever moved it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A journey, a rest, a wait, and a night at an inn all move the clock inside an update, and a place's
    /// population is restored against the day the clock now stands on in that same advance rather than at
    /// whatever update happens to come next. The world registers itself with the clock it is composed over and
    /// releases that registration when it is disposed.
    /// </para>
    /// <para>
    /// What was summoned into the party's place for a length is counted down by the same advance, so a creature a
    /// spell called up for an hour is gone when an hour of game time has passed, however it passed.
    /// </para>
    /// </remarks>
    /// <param name="advance">Where the clock was, where it went, and what it crossed.</param>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (advance.Moved) _population.Elapse(advance.Elapsed);
        if (advance.Crossings.Days > 0) AdvanceTime();
    }

    /// <summary>
    /// Steps the interaction mechanism inside the admitted update: the reticle is refreshed from where the
    /// party now stands and what its place holds, and a use the player asked for is applied to whatever it
    /// holds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A use is an instant rather than an interval, so it is stepped here whether or not the session is
    /// running: a held session still shows the world the player is looking at, and a lever pulled while it
    /// is held is an act rather than a passage of time. That is deliberately not how movement works — a
    /// paused session admits no interval for motion — and it is why a use needs no elapsed seconds.
    /// </para>
    /// <para>
    /// Every use is reported, whether it applied or was refused, because the panel shows the last answer and
    /// a use that left no trace would be indistinguishable from a key that never arrived.
    /// </para>
    /// </remarks>
    /// <param name="use">Whether the player asked to use what the party faces.</param>
    /// <returns>The use's result, or null when the world has no interaction or the player asked for none.</returns>
    public InteractionResult? Interact(bool use)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Interaction is not { } interaction) return null;
        interaction.Update();
        if (!use) return null;

        InteractionResult result = interaction.Use();
        Report(result);
        return result;
    }

    /// <summary>
    /// Moves the party by one step of the world's admitted time, and takes the transition that step
    /// walked into.
    /// </summary>
    /// <remarks>
    /// The world is the only owner of the party's pose, so movement is asked from here rather than from
    /// whoever read the input: the session reads what the player wants, and this is where that becomes
    /// motion. A world without a mover has nothing to move the party with and answers null, which is the
    /// honest answer for a product running without the engine's spatial service.
    /// </remarks>
    /// <param name="intent">What the player asked for this step.</param>
    /// <param name="elapsedSeconds">The admitted world time this step covers.</param>
    /// <returns>The step's outcome, or null when the party has no movement.</returns>
    public MovementOutcome? Step(MovementIntent intent, double elapsedSeconds)
    {
        if (Mover is not { } mover) return null;

        // The pose the step begins at is what tells an entrance that was walked into from one the party
        // was already standing in, and it must be read before the mover replaces it.
        PlacePose before = Party.PlacePose;
        MovementOutcome outcome = mover.Step(intent, elapsedSeconds);
        _movement = new MovementDiagnostics(
            outcome,
            _movement.Falls + (outcome.Fall.PastThreshold ? 1 : 0));
        Report(outcome);
        Enter(before);
        return outcome;
    }

    /// <summary>
    /// Takes a transition, or refuses it. Arriving moves the party, marks the destination visited, and
    /// charges the journey; a refusal leaves the party exactly where it was and charges nothing.
    /// </summary>
    /// <remarks>
    /// This is the only place a transition's cost is applied. A walk into an entrance, a scripted move, and
    /// any other caller all arrive here, so a journey is charged exactly once — on arrival, after the
    /// destination has admitted the party — and a transition that never happened costs nothing.
    /// </remarks>
    public TransitionResult Travel(PlaceTransition transition, TransitionKind kind)
    {
        ArgumentNullException.ThrowIfNull(transition);
        TransitionResult result = _transitions.Take(new TransitionRequest(Graph, transition, kind, Party.Place, Party.PlacePose));
        if (!result.Arrived) return result;

        // An arrival the place will not admit is a refusal, not a half-done move: the party stays put and
        // nothing is charged, because a journey nobody took is a journey nobody pays for.
        PlaceId fromPlace = Party.Place;
        PlacePose fromPose = Party.PlacePose;
        try
        {
            Party.Enter(result.Place, result.Pose);
        }
        catch (ArgumentException error)
        {
            return TransitionResult.Refused(kind, Party.Place, Party.PlacePose, new Refusal(
                TravelCodes.PlaceRefusedArrival,
                $"The destination {result.Place} refused the arrival: {error.Message}"));
        }

        // The destination's ground is admitted before anything is charged, for the same reason: an engine that
        // will not take the place's collision leaves the party back where it stood, on the ground it stood on,
        // with its time and its provisions as they were.
        try
        {
            EnterPlace(result.Place);
        }
        catch (EngineCallException error)
        {
            Party.Enter(fromPlace, fromPose);
            EnterPlace(fromPlace);
            return TransitionResult.Refused(kind, fromPlace, fromPose, new Refusal(
                TravelCodes.PlaceGroundRefused,
                $"The engine would not admit the ground of {result.Place}, so the party stayed where it stood: {error.Message}"));
        }

        Charge(result.ChargedCost);
        Places.MarkVisited(result.Place);
        return result;
    }

    /// <summary>
    /// Puts the party where a scenario starts it.
    /// </summary>
    /// <remarks>
    /// Starting is a placement, not travel: there is no place to leave and no journey to charge for, so
    /// this does not go through the transition path and no cost is quoted. Every move *between* places
    /// does, including the world-issued transitions content declares — those are taken with
    /// <see cref="Travel"/> like any other.
    /// </remarks>
    public void ArriveAt(PlaceId place, PlacePose pose)
    {
        Party.Enter(place, pose);
        EnterPlace(place);
        Places.MarkVisited(place);
    }

    /// <summary>
    /// Boards the passage the party holds to a place, which is how a journey bought at a counter is taken.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A fare is taken through the one transition path and never by a reach.</b> The counter that sold the
    /// passage hands the party here — the ticket is the party's own, and no place's ground has an entrance to
    /// board at — so the journey goes through <see cref="Travel"/> with its own kind, is priced by the cost
    /// rule from the ticket, and arrives at the destination's own arrival point. Nothing here charges
    /// anything: the crossing is charged once, on arrival, by the path every other crossing takes, and a
    /// refusal leaves the party standing at the counter with its coin and its ticket as they were.
    /// </para>
    /// <para>
    /// <b>Which journey is taken is the ticket's own statement.</b> A passage names the place it reaches and
    /// the route it was sold on, and the graph's fares from the place the party stands in are matched on both:
    /// a region may keep a coach and a boat that both reach the same town, and the route on the ticket is what
    /// tells one journey from the other. The ticket carries no days, so a retune since it was bought changes
    /// how long the journey takes and never which journey it names. A world that sells two journeys alike in
    /// both is refused by name rather than guessed between, and a party that holds no passage is handed to the
    /// cost rule, which owns that refusal and names the counter that sells one.
    /// </para>
    /// </remarks>
    /// <param name="destination">The place the passage reaches, which is what the counter sold.</param>
    /// <returns>The arrival, or the refusal, in the shape every other crossing answers with.</returns>
    public TransitionResult Board(PlaceId destination)
    {
        List<PlaceTransition> journeys =
        [
            .. Graph.TransitionsFrom(Party.Place).Where(journey => journey.IsFare && journey.To == destination),
        ];

        if (journeys.Count == 0)
        {
            return TransitionResult.Refused(TransitionKind.PaidService, Party.Place, Party.PlacePose, new Refusal(
                TravelCodes.TravelFareUnrouted,
                $"No counter in {Graph.Require(Party.Place).Name} sells a passage to {Graph.Require(destination).Name}, so the journey the ticket names cannot be taken from where the party stands."));
        }

        // The ticket is the party's own state, and a world with no party entity holds none: a journey is
        // then handed to the cost rule, which refuses it by name rather than travelling on a ticket nobody
        // could have bought.
        string? route = _entity is { } party ? party.Passages.RouteTo(destination) : null;
        if (route is not null)
        {
            List<PlaceTransition> matching = [.. journeys.Where(journey => string.Equals(journey.FareRoute, route, StringComparison.Ordinal))];
            if (matching.Count == 0)
            {
                return TransitionResult.Refused(TransitionKind.PaidService, Party.Place, Party.PlacePose, new Refusal(
                    TravelCodes.TravelFareUnstated,
                    $"The party's passage to {Graph.Require(destination).Name} runs on route '{route}' and none of the {journeys.Count} journeys sold from {Graph.Require(Party.Place).Name} to it runs on that route, so the journey the ticket names is not one this world sells from here."));
            }

            if (matching.Count > 1)
            {
                return TransitionResult.Refused(TransitionKind.PaidService, Party.Place, Party.PlacePose, new Refusal(
                    TravelCodes.TravelFareAmbiguous,
                    $"{matching.Count} journeys sold from {Graph.Require(Party.Place).Name} to {Graph.Require(destination).Name} run on the route '{route}' the party's passage names, so which of them was bought cannot be told from the ticket."));
            }

            journeys = matching;
        }

        // A party holding no passage is still handed to the cost rule rather than refused here: the rule is
        // the one owner of what boarding costs and of why a party with no ticket cannot board.
        TransitionResult boarded = Travel(journeys[0], TransitionKind.PaidService);
        Report(boarded, destination);
        return boarded;
    }

    /// <summary>Reports a boarding, because a journey bought at a counter leaves no other trace of itself.</summary>
    /// <remarks>
    /// A counter's journey changes the place the party stands in without a step being taken, so without this
    /// the panel's account of where the party is would move with nothing saying why. The report names the
    /// place the passage reached and, on a refusal, the rule's own reason.
    /// </remarks>
    private void Report(TransitionResult boarded, PlaceId destination)
    {
        if (boarded.Arrived)
        {
            _diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "travel",
                Code: "fare-boarded",
                Message: $"The party boarded its passage to {Graph.Require(destination).Name} and arrived in place '{boarded.Place}' at {boarded.Pose}, the journey taking {boarded.ChargedCost.Time.Amount} {boarded.ChargedCost.Time.Unit}.",
                Correlation: string.Empty));
            return;
        }

        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "travel",
            Code: "fare-refused",
            Message: $"The party's passage to {Graph.Require(destination).Name} was not boarded and cost nothing: {boarded.Refusal}",
            Correlation: string.Empty));
    }

    /// <summary>
    /// Advances the world to the day its time source reports, returning the places whose population was
    /// restored. Without a time source the world does not advance, and says so by doing nothing.
    /// </summary>
    public IReadOnlyList<PlaceState> AdvanceTime()
    {
        IReadOnlyList<PlaceState> restored = _time is null ? [] : Places.AdvanceTo(_time.ElapsedGameDays);

        // The population follows the same advance: a place whose reset came due is repopulated here, in
        // the same update that moved the clock, rather than by a second timer of its own.
        Step(restored);

        // A restored place comes back as it was: what the party did to its doors and containers belongs to
        // the visit that did it, so a place whose population is restored forgets it in the same update. A
        // reset that left an opened door open and an emptied chest empty would be a population brought back
        // into a ruin.
        foreach (PlaceState state in restored) _interactions.Forget(state.Place);
        return restored;
    }

    /// <summary>Populates the party's place, which also happens on the first update after arriving.</summary>
    public IReadOnlyList<PlacePopulationEntity> Populate() => Step([]);

    /// <summary>
    /// Steps the population, and tells whoever keeps bodies when it was built afresh: a body belongs to the
    /// entities of one build, and the next build holds none of them.
    /// </summary>
    private IReadOnlyList<PlacePopulationEntity> Step(IReadOnlyList<PlaceState> restored)
    {
        long before = _population.Generation;
        IReadOnlyList<PlacePopulationEntity> live = _population.Step(Party.Place, restored);
        if (_population.Generation != before) _corpses?.Repopulated(Party.Place);
        return live;
    }

    /// <summary>
    /// Captures the world's durable state: where the party stands, and what each place remembers.
    /// </summary>
    /// <remarks>
    /// The graph, the entrances, the mover's collision scene, the movement observations, the population's
    /// entities, and what the party has done to each place's targets are absent on purpose. The graph and the
    /// entrances are loaded content, the scene is refilled from the place the party resumes in, movement
    /// observations belong to the steps that produced them, the entities are rebuilt from placements, and
    /// interaction state is live state the persistence owner does not carry yet (#8593) — each of them a runtime
    /// shape that a load composes again rather than one a save carries.
    /// </remarks>
    public WorldSave Capture() => new(Party.Capture(), Places.Capture());

    /// <summary>Releases the entities the population owns and the engine's collision scene with the movement.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _clockSubscription?.Dispose();
        _population.Dispose();

        // The product's selection outlives this world, so a world being released gives it up: an inspection
        // made after this reads an empty scene rather than a place nobody stands in.
        Interaction?.Release();

        // The creature mover walks in the party mover's own spatial session, so it is released first and
        // releases nothing of its own beyond the walkers it kept: the scene belongs to the movement.
        Creatures?.Dispose();
        Mover?.Dispose();
    }

    /// <summary>What the panel shows about the world.</summary>
    public WorldSnapshot Snapshot
    {
        get
        {
            PlaceDefinition place = Graph.Require(Party.Place);
            GameDate? now = _clock?.Now;
            return new WorldSnapshot(
                place.Id.Value,
                place.Name,
                SessionProjection.WireName(place.Kind),
                Party.PlacePose,
                Places.States.Count(state => state.Visited),
                Graph.Places.Count,
                // What the place's own hours read as right now, recomputed from the clock rather than
                // remembered: a shop that closed while the party stood in it reads closed in the same
                // projection that shows the clock it closed by.
                Open: now is not { } at || Schedule.IsOpenAt(place.Id, at),
                Hours: Schedule.HoursOf(place.Id)?.ToString() ?? string.Empty,
                NextChange: NextChange(place.Id));
        }
    }

    /// <summary>When the party's place next opens or closes, as a point on the calendar, or empty when it keeps no hours.</summary>
    private string NextChange(PlaceId place)
    {
        if (_clock is not { } clock) return string.Empty;
        return Schedule.NextChangeAfter(place, clock.Now, clock.Calendar) is { } change
            ? change.MinuteText
            : string.Empty;
    }

    /// <summary>
    /// Fills the movement's collision scene from the place the party has just entered.
    /// </summary>
    /// <remarks>
    /// Entering admits or empties, in that order, whatever the place provides. A place the engine refuses
    /// throws here rather than being papered over: a party walking on nothing is a worse failure than a
    /// named one, and the arrival is the moment the defect is attributable to a place.
    /// </remarks>
    private void EnterPlace(PlaceId place) => Mover?.Enter(place);

    /// <summary>The place the party is in, which is where an interaction reads its targets from.</summary>
    PlaceId IInteractionWorld.Place => Party.Place;

    /// <summary>Where the party stands and faces, which is what the reticle is aimed by.</summary>
    PlacePose IInteractionWorld.Pose => Party.PlacePose;

    /// <summary>
    /// What the party's place holds, read from content rather than from the entities standing in it: the
    /// population is emptied when the party clears a place, and a cleared place's doors are still doors.
    /// </summary>
    IReadOnlyList<PlacementDefinition> IInteractionWorld.Placements => _population.PlacementsOf(Party.Place);

    /// <summary>The party itself, which an interaction requires things of and gives things to.</summary>
    PartyEntity? IInteractionWorld.Party => _entity;

    /// <summary>The party's own accounts, which a use's price settles against.</summary>
    PartyResourceLedger? IInteractionWorld.Accounts => _resources;

    /// <summary>The session's one clock, which a time-of-day requirement is judged against.</summary>
    GameClock? IInteractionWorld.Clock => _clock;

    /// <summary>What the party has already done to the targets of every place it has been in.</summary>
    InteractionLedger IInteractionWorld.States => _interactions;

    /// <summary>
    /// Whether nothing solid stands between two points of the place the party is in.
    /// </summary>
    /// <remarks>
    /// A world with no mover holds no collision scene at all, so nothing occludes anything in it: that is a
    /// fact about a world without an engine rather than a guess, and it is the same honesty with which such a
    /// world reports that the party stands on no geometry.
    /// </remarks>
    bool IInteractionWorld.InSight(Vector3 from, Vector3 to) => Mover?.InSight(from, to) ?? true;

    /// <summary>
    /// The place a stop happens in, which is where the ground and the night are read from.
    /// </summary>
    /// <remarks>
    /// The place is handed over as content defined it — its identity, whether it is a region or an interior,
    /// and the entry behind it — so a ruleset reads what a night costs and how dangerous it is from the same
    /// fields the world was built from, rather than from a vocabulary this layer invented for them.
    /// </remarks>
    PlaceDefinition IRestSite.Place => Graph.Require(Party.Place);

    /// <summary>Where in the place the party stands, which is what a hostile's distance is measured from.</summary>
    PlacePose IRestSite.Pose => Party.PlacePose;

    /// <summary>
    /// What lives in the place right now, which is what a camp's refusal reads for anything hostile near.
    /// </summary>
    /// <remarks>
    /// The population is the live entities of the party's own visit, destroyed when it walks out, so a place
    /// the party left has nothing near and a place it stands in has exactly what content put there. The
    /// world holds no opinion about which of them is hostile: a spawn point, a wandering monster, and a
    /// shopkeeper are content's words, and the ruleset answers for them.
    /// </remarks>
    IReadOnlyList<PlacePopulationEntity> IRestSite.Population => _population.Entities;

    /// <summary>
    /// The place a fight happens in, which is the place the party already stands in: a fight is over the
    /// live world, so it is handed the same place the interaction and rest mechanisms read rather than a
    /// battle scene of its own.
    /// </summary>
    PlaceId ICombatWorld.Place => Party.Place;

    /// <summary>Where the party stands, which is what every actor's distance from the party is measured from.</summary>
    PlacePose ICombatWorld.Pose => Party.PlacePose;

    /// <summary>
    /// What is alive in the place right now, which is what a fight decides who is hostile from.
    /// </summary>
    /// <remarks>
    /// The population is the live entities of the party's own visit, created from the place's placements and
    /// destroyed when it walks out, so a fight sees what is actually standing there and a place the party has
    /// left holds nobody to fight. The world holds no opinion about which of them is hostile: that is the
    /// ruleset's answer about what each thing is, plus what the party has done to it.
    /// </remarks>
    IReadOnlyList<PlacePopulationEntity> ICombatWorld.Population => _population.Entities;

    /// <summary>Reports what one use did, whether it applied or was refused.</summary>
    /// <remarks>
    /// The panel shows the last answer and nothing else, so a use that left no report would be
    /// indistinguishable from a key that never arrived. The report names the target, the verb, and the
    /// refusal's own code, which is what makes an opened door, a door that was already open, and a fixture
    /// whose event the ruleset refused to run readable from the product's own account of itself.
    /// </remarks>
    private void Report(InteractionResult result)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "interaction",
            Code: result.IsApplied ? "interaction-used" : "interaction-refused",
            Message: result.IsApplied
                ? $"The party used {result.TargetName} ({result.Verb}) in place '{Party.Place}' at {Party.PlacePose}: {result.Message}"
                : $"The party's use of {result.TargetName} in place '{Party.Place}' at {Party.PlacePose} was refused ({result.Code}): {result.Message}",
            Correlation: string.Empty));
    }

    /// <summary>
    /// Applies what a transition quoted: its game time to the clock, and its provisions to the party.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the receiver the transition path's cost contract was waiting for. The time is converted
    /// through <see cref="TravelTimeConversion"/> rather than guessed, because how long a day is belongs to
    /// the calendar the clock keeps, and it is applied with the same advance every other owner of time
    /// uses. The provisions go to <see cref="PartyResourceLedger.SpendDay(Provisions)"/>, which is the one
    /// path into the party's larder and which also asks the ruleset what a larder left at that level does
    /// to the party — so arriving short of food weakens the party on the road rather than at the next camp.
    /// </para>
    /// <para>
    /// Charging is reported and not invented where an owner is missing: a world composed without a clock,
    /// or without a party to charge, must not let a quoted cost disappear quietly, because a journey that
    /// looked free is exactly what the cost contract exists to prevent.
    /// </para>
    /// </remarks>
    private void Charge(TravelCost cost)
    {
        if (cost.IsFree) return;

        if (!cost.Time.IsNone)
        {
            if (_clock is { } clock)
            {
                // A journey's days are the clock's own: the clock tells every owner of game time about the
                // advance, this world among them, so a day crossed on the road is a day the world's places and
                // a shop's shelves live through in this same arrival.
                clock.Advance(TravelTimeConversion.Elapsed(clock.Calendar, cost.Time));
            }
            else
            {
                Report(
                    "travel-time-uncharged",
                    $"The transition quoted {cost.Time.Amount} {cost.Time.Unit} of travel, but the session has no clock, so no time was charged.");
            }
        }

        if (!cost.Food.IsNone)
        {
            if (_resources is { } resources)
            {
                resources.SpendDay(cost.Food);
            }
            else
            {
                Report(
                    "travel-provisions-uncharged",
                    $"The transition quoted {cost.Food.Amount} {cost.Food.Unit} of provisions, but the session holds no party, so nothing was charged to a larder.");
            }
        }
    }

    /// <summary>Reports something the world could not hand to an owner, naming what is missing.</summary>
    private void Report(string code, string message) =>
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "travel",
            Code: code,
            Message: message,
            Correlation: string.Empty));

    /// <summary>
    /// Takes the transition whose entrance the step just carried the party into.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole of walking between places: a place's own entrances are consulted after the step
    /// that moved the party, and the one it entered is taken through <see cref="Travel"/> — the same path,
    /// with the same cost contract and the same arrival, that any other transition takes. Nothing here
    /// decides where the party goes or what it costs, and there is no second way between places.
    /// </para>
    /// <para>
    /// Only the step's <em>entry</em> counts: the party must have been outside the reach when the step
    /// began and be inside it when the step ended. A party standing in an entrance it did not walk into —
    /// because a transition placed it there, or because it has not left yet — does not travel, which is
    /// what keeps a door from bouncing a party straight back through it. A refusal is reported and the
    /// party keeps walking: the rule's answer is a fact about the journey, and a party that is refused
    /// must not be silently moved or silently stopped.
    /// </para>
    /// </remarks>
    private void Enter(PlacePose before)
    {
        if (!_entrances.TryGetValue(Party.Place, out PlaceEntrance[]? entrances)) return;

        PlacePose now = Party.PlacePose;
        foreach (PlaceEntrance entrance in entrances)
        {
            if (!entrance.Contains(now) || entrance.Contains(before)) continue;
            TransitionResult result = Travel(entrance.Transition, entrance.Kind);
            if (result.Arrived)
            {
                // The party is somewhere else now, so the entrances of the place it left cannot apply to
                // the rest of this step, and the arrival pose is what the next step's entry test starts
                // from.
                Report(entrance, result);
                return;
            }

            Report(entrance, result.Refusal!);
        }
    }

    /// <summary>Reports the crossing a walk-in took, which nothing else states as a fact.</summary>
    /// <remarks>
    /// The panel shows the place the party is in and not how it got there, so a crossing that happened
    /// and one that never fired look the same on screen. This is the world's own account of it: which
    /// entrance was entered, where the party left, and where it arrived.
    /// </remarks>
    private void Report(PlaceEntrance entrance, TransitionResult result)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "travel",
            Code: "entrance-entered",
            Message: $"The party walked into the entrance '{entrance.Source}' in place '{entrance.Place}' and arrived in place '{result.Place}' at {result.Pose}.",
            Correlation: string.Empty));
    }

    /// <summary>Reports a walk-in the cost rule refused, which nothing else on screen would show.</summary>
    private void Report(PlaceEntrance entrance, Refusal refusal)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "travel",
            Code: "entrance-refused",
            Message: $"The party walked into the entrance '{entrance.Source}' in place '{Party.Place}' and the transition to '{entrance.Transition.To}' was refused: {refusal}",
            Correlation: string.Empty));
    }

    /// <summary>
    /// Indexes the entrances by the place that issues them, refusing one that stands nowhere.
    /// </summary>
    /// <remarks>
    /// An entrance whose transition the graph does not hold would be a door a load should have refused:
    /// walking into it would fail inside an admitted update, where a failure is far harder to attribute
    /// than at the moment the world was built.
    /// </remarks>
    private static Dictionary<PlaceId, PlaceEntrance[]> Index(PlaceGraph graph, IReadOnlyList<PlaceEntrance>? entrances)
    {
        Dictionary<PlaceId, List<PlaceEntrance>> byPlace = [];
        foreach (PlaceEntrance entrance in entrances ?? [])
        {
            if (!graph.Transitions.Contains(entrance.Transition))
            {
                throw new ArgumentException(
                    $"The entrance '{entrance.Source}' takes transition '{entrance.Transition.Source}', which place '{entrance.Place}' does not issue, so walking into it could never be taken.",
                    nameof(entrances));
            }

            if (!byPlace.TryGetValue(entrance.Place, out List<PlaceEntrance>? list))
            {
                list = [];
                byPlace[entrance.Place] = list;
            }

            list.Add(entrance);
        }

        return byPlace.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    /// <summary>
    /// Lands a fall the tuning measured past its threshold on each member, and reports it.
    /// </summary>
    /// <remarks>
    /// The harm is the game's answer for each member and arrives through that member's own damage entry, so a
    /// fall leaves the same conditions a trap or a blow would when it empties a member. A world composed
    /// without a fall rule, or without a party, reports the landing and harms nobody.
    /// </remarks>
    private void Report(MovementOutcome outcome)
    {
        if (!outcome.Fall.PastThreshold) return;
        FallOutcome fall = outcome.Fall;
        int harmed = 0;
        int total = 0;
        if (_falls is { } rule && _entity is { } party)
        {
            foreach (PartyMember member in party.Members)
            {
                int damage = Math.Max(0, rule.DamageTo(member, fall));
                if (damage == 0) continue;
                member.TakeDamage(damage);
                harmed++;
                total += damage;
            }
        }

        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "movement",
            Code: "fall-past-threshold",
            Message: $"The party landed in place '{Party.Place}' after falling {fall.Distance:0.###}, past the tuning's threshold by {fall.Excess:0.###}; {harmed} member(s) took {total} harm.",
            Correlation: string.Empty));
    }
}
