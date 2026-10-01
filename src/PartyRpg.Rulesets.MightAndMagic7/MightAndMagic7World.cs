using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Builds this game's world from the content the product loaded.
/// </summary>
/// <remarks>
/// The world exists when content declares both places and where the party starts. The starting place is
/// scenario data rather than something the ruleset knows by name, so a bundle without a scenario starts
/// with no world and the panel says so, instead of the product pretending to be somewhere.
/// </remarks>
internal static class MightAndMagic7World
{
    /// <summary>The definition kind a scenario's starting entry uses.</summary>
    internal const string StartDefinitionKind = "scenario-start";

    /// <summary>
    /// The definition kind a place's collision artifact is declared under, one entry per place, keyed by
    /// the place's own id.
    /// </summary>
    /// <remarks>
    /// The importer emits this document into the imported world pack, and emits one only for a place whose
    /// geometry it could close: a place it refused has no entry here, which the mover admits as an empty
    /// scene and reports rather than inventing ground. The document's own shape is the engine's, and the
    /// ruleset reads it without rewriting a field of it.
    /// </remarks>
    internal const string GeometryDefinitionKind = "place-geometry";

    /// <summary>The property of a geometry entry that holds the engine's canonical collision artifact.</summary>
    internal const string GeometryArtifactProperty = "artifact";

    /// <summary>The property of a place's geometry entry that names its ground: its water and its fluid faces.</summary>
    internal const string GeometrySurfacesProperty = "surfaces";

    /// <summary>
    /// The definition kind that carries the reaches a walking party takes this game's transitions
    /// through, one entry per trigger face, keyed by the link it takes and the face it came from.
    /// </summary>
    /// <remarks>
    /// The importer emits this document into the imported world pack from the event faces the maps carry.
    /// A place with no entry here is a place whose transitions cannot be walked into: the world then moves
    /// the party and never the place, and the write report names every such link rather than the product
    /// inventing a trigger position for it.
    /// </remarks>
    internal const string EntranceDefinitionKind = "place-entrance";

    /// <summary>The world graph this game reads from content: its places, its links, and the passages its counters sell.</summary>
    /// <remarks>
    /// The sold crossings are this game's fare network over the counters content places, timed by this game's
    /// fare rule over the catalog's tuning, so every reader of the world — the session, a save being judged, a
    /// test — sees the same journeys a counter offers.
    /// </remarks>
    /// <param name="catalog">The validated content.</param>
    internal static PlaceGraph Graph(ContentCatalog catalog) =>
        PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog), MightAndMagic7FareNetwork.Read(catalog));

    /// <summary>Composes the world, or null when the content does not place the party anywhere.</summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="context">What the host handed the ruleset, which carries the engine the world moves in.</param>
    /// <param name="clock">
    /// This game's one clock, which the world reads its days from and charges a journey's time to. A session
    /// holds no second source of game time, so travel time and respawn are measured in one clock.
    /// </param>
    /// <param name="resources">
    /// The party's own accounts, which a journey charges its provisions to. It is null when content declared
    /// no party, and a quoted food cost is then reported rather than quietly dropped.
    /// </param>
    /// <param name="entity">
    /// The party itself, which an interaction reaches for what it requires and gives what it finds. It is
    /// null when content declared no party, and a requirement that needs one is then unmet rather than
    /// satisfied by an invented carrier.
    /// </param>
    /// <param name="resume">
    /// The save a session is resuming from, when this world is being composed for a load. A world composed
    /// from a save takes the party's place and pose and every place's remembered state from that save, and
    /// needs no scenario start: where the party stands is what the save says, and a scenario that has since
    /// changed cannot quietly move a resumed party elsewhere.
    /// </param>
    /// <param name="services">
    /// This game's answers about services, when its content declares any. A counter the party talks to is a
    /// service placement, and the interaction mechanism needs the same answers the service mechanism itself
    /// is composed over — the one policy instance, so what a use offers and what a transaction does can
    /// never be two readings of one placement. Without it a service placement is not a target at all and
    /// walking into a shop is not possible, which is what a session with no services gets.
    /// </param>
    /// <param name="conversation">
    /// This game's answers about people, when its content declares any. Whether a placement holds somebody
    /// and what they are called is read from here, so the reticle's target and the person who answers are one
    /// reading of one placement rather than two. Without it nobody is reachable: every placement is answered
    /// as it was before people existed.
    /// </param>
    /// <param name="corpses">
    /// What this game keeps of the creatures the party brought down, when a session has one: the world's
    /// interaction answers read the bodies lying in a place from it, so the same owner the fight reports to
    /// is the one a search finds. Without it no placement is a body.
    /// </param>
    /// <param name="loot">
    /// This game's loot, which a container's random references and a body's own contents are answered by.
    /// Without one a container holding a random reference is refused by name rather than emptied of invented
    /// contents.
    /// </param>
    /// <param name="journal">
    /// The party's journal, read through a call the moment a search yields something, or null when this
    /// session keeps no record. It is a provider rather than an owner here for the same reason the party is:
    /// this world is composed before the session that keeps the journal exists, and a session that creates
    /// its party has none until the player accepts one.
    /// </param>
    /// <param name="vitals">
    /// This game's fight, whose answer about a monster row's hit points gives each creature its health the
    /// moment the place's population places it. Without it no creature carries health.
    /// </param>
    /// <param name="spawns">
    /// This game's answer about the encounters a level's spawn records ask for, which the population resolves
    /// into creatures while it reads the places. Without it an encounter stands as content states it, and no
    /// creature comes from it.
    /// </param>
    /// <param name="fixtures">
    /// This game's fixtures, which run the map events a fixture or a decoration raises. Without them a fixture
    /// is still a target and its use is refused by name.
    /// </param>
    internal static SessionWorld? Compose(
        ContentCatalog? catalog,
        RulesetSessionContext context,
        GameClock clock,
        PartyResourceLedger? resources,
        PartyEntity? entity = null,
        SessionSave? resume = null,
        MightAndMagic7Services? services = null,
        MightAndMagic7Conversation? conversation = null,
        MightAndMagic7Corpses? corpses = null,
        MightAndMagic7Loot? loot = null,
        Func<PartyJournal?>? journal = null,
        MightAndMagic7Combat? vitals = null,
        MightAndMagic7Spawns? spawns = null,
        MightAndMagic7Fixtures? fixtures = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);
        if (catalog is null) return null;
        PlaceGraph graph = Graph(catalog);
        if (graph.Places.Count == 0) return null;

        // What content says about interaction is read once, here, with the world: a requirement that names a
        // kind this game does not know is a defect the load names, rather than a lock that quietly is not
        // there when somebody tries the door.
        MightAndMagic7Interaction.Validate(catalog);

        // Which places are clocked is read once here, from the counters the places keep and the hours a place
        // states for itself, and the one reading is handed to the interaction rule and to the world: the door
        // a use is refused at and the hours the panel shows are the same schedule, so they cannot disagree.
        MightAndMagic7Schedules schedules = MightAndMagic7Schedules.Read(catalog, graph, services);
        foreach (string note in schedules.Notes)
        {
            context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "schedule",
                Code: "place-unclocked",
                Message: note,
                Correlation: string.Empty));
        }

        PlaceRespawnRule respawn = PlaceRespawnRule.FromContent();
        PartyPoseOwner party;
        PlaceStateLedger places;
        if (resume is { } save)
        {
            // The session judged everything the save names against this world before it moved the clock or
            // restored the party (MightAndMagic7Persistence.RequireLoadable), so what is rebuilt here fits.
            // The pose owner is created at the pose the party resumes at, which goes through the same
            // admission an entered pose goes through: a save can never put the party where its place would
            // refuse it, and the refusal would have been named above rather than thrown here.
            party = new PartyPoseOwner(save.World.Pose, MightAndMagic7Movement.Facing);
            places = PlaceStateLedger.Restore(graph, respawn, save.World.Places);
        }
        else
        {
            (PlaceId Place, string? EntryPoint)? start = ReadStart(catalog);
            if (start is not { } begin) return null;

            PlaceDefinition place = graph.Require(begin.Place);
            PlacePose pose = begin.EntryPoint is { Length: > 0 } entryPointId
                ? graph.ResolveArrival(new PlaceTransition(null, begin.Place, PlaceArrival.AtEntryPoint(entryPointId), "scenario-start"))
                : PlacePose.Origin;

            party = new PartyPoseOwner(new PartyPose(place.Id, pose), MightAndMagic7Movement.Facing);
            places = new PlaceStateLedger(graph, respawn);
        }

        // One pair of movers over one spatial session: the party and the place's creatures walk in the same
        // scene, and whatever the world is not handed is released here rather than left holding a session.
        (IPartyMover? mover, ICreatureMover? creatures) = Movers(party, context, MightAndMagic7Movement.FlightRule(entity, graph, party));
        try
        {
            SessionWorld world = new SessionWorld(
                graph,
                party,
                places,

                // The cost rule is composed over the party itself, because a fare is the party's own passage:
                // the counter that sells one writes it on the party and the road that honours it reads and
                // tears the same state, so a seat bought in one town cannot be spent in another's name.
                new MightAndMagic7TravelCostRule(entity),
                clock,
                mover,
                context.Engine?.Diagnostics,
                PlaceEntranceLoader.Load(catalog, graph),
                clock,
                resources,
                entity,
                new InteractionPolicy(Interaction(conversation, schedules.Schedule, corpses, loot, journal, fixtures), MightAndMagic7Movement.Space, MightAndMagic7Interaction.Aim, corpses, context.Interaction),
                schedules.Schedule,
                creatures,
                MightAndMagic7Movement.Falls(entity),
                vitals,
                spawns,

                // What each place kept of the party's uses — a well's charges, a puzzle's count, when a timer
                // last ran — is rebuilt from the save, judged with the rest of it before anything was composed.
                resume?.World.Interaction,
                MightAndMagic7Movement.Hazards(entity, mover));

            // What the population could not resolve — an encounter that needs a draw in a product with no
            // random service, a drawn grade the content carries no variant for — is reported where the other
            // composition notes are, once per encounter, rather than filled from an invented source.
            foreach (string note in spawns?.Unresolved ?? [])
            {
                context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                    DiagnosticsSeverity.Info,
                    DiagnosticsDisposition.Accepted,
                    Source: "population",
                    Code: "encounter-unresolved",
                    Message: note,
                    Correlation: string.Empty));
            }

            return world;
        }
        catch
        {
            (creatures as IDisposable)?.Dispose();
            mover?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// This game's answers about using what a place holds, with its people added when it has any.
    /// </summary>
    /// <remarks>
    /// The people answers wrap the doors-and-fixtures answers rather than replacing them, which is what keeps
    /// one interaction mechanism: a placement somebody stands at is somebody to talk to — a counter, a
    /// household, or a person a map places in the open — and everything else is answered exactly as it was.
    /// The doors-and-fixtures answers carry this game's schedule, which is what locks a door outside the
    /// hours its place keeps, and this game's bodies and loot, which is what makes a kill searchable and a
    /// chest that holds a random reference answerable; what a search yields is reported from the same object
    /// to the party's journal, so a notable find is written down by the rule that produced it.
    /// </remarks>
    private static IInteractionRule Interaction(
        MightAndMagic7Conversation? conversation,
        PlaceSchedule schedule,
        MightAndMagic7Corpses? corpses,
        MightAndMagic7Loot? loot,
        Func<PartyJournal?>? journal,
        MightAndMagic7Fixtures? fixtures)
    {
        MightAndMagic7Interaction answers = new(schedule, corpses, loot, journal, fixtures);
        return conversation is null ? answers : new MightAndMagic7PeopleInteraction(conversation, answers, fixtures);
    }

    /// <summary>
    /// How the party and the place's creatures move, when the host handed this ruleset an engine to move in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Movement needs both halves: places to walk between, which the world supplies, and an engine whose
    /// spatial service owns collision and resolves the step. Without the engine there is no movement at
    /// all, and the session says so by stepping nothing rather than by standing in for a spatial service
    /// the product does not have.
    /// </para>
    /// <para>
    /// <b>Both walk in one scene.</b> The creature mover is built over the party's own spatial session and
    /// the party's own controller profile, so a creature is swept against the ground the place admitted and
    /// moves by the same physics the party does, at the pace its own row states. Two scenes would let a
    /// creature stand on a floor the party cannot see.
    /// </para>
    /// </remarks>
    /// <param name="party">The party's own pose, which its movement asks to move.</param>
    /// <param name="context">What the host handed the ruleset, which carries the engine the world moves in.</param>
    /// <param name="flight">This game's answer to whether the party may fly now.</param>
    private static (IPartyMover? Mover, ICreatureMover? Creatures) Movers(PartyPoseOwner party, RulesetSessionContext context, IFlightRule flight)
    {
        if (context.Engine is not { } engine) return (null, null);

        // A context that carries no engine, or an engine that answers with no spatial service, means the
        // product has no collision to move through: there is no movement then, rather than movement that
        // walks through walls.
        if (engine.Spatial is not { } spatial) return (null, null);

        MovementTuning tuning = MightAndMagic7Movement.Tuning(spatial);
        PartyMovement movement = new(
            spatial,
            party,
            MightAndMagic7Movement.Space,
            MightAndMagic7Movement.Session,
            tuning,
            flight: flight);

        // A place's geometry comes from the catalog when content carries any. Without a catalog there is no
        // world either, but the mover is composed here where both are still in hand.
        IPlaceGeometrySource? geometry = context.Content is { } catalog
            ? new ContentPlaceGeometry(catalog, GeometryDefinitionKind, GeometryArtifactProperty, GeometrySurfacesProperty)
            : null;

        EnginePartyMover mover = new(spatial, movement, engine.Content, MightAndMagic7Movement.Navigation, geometry);
        return (mover, new EngineCreatureMotion(spatial, mover, MightAndMagic7Movement.Space, tuning.Controller, MightAndMagic7Movement.CreatureSettleReach));
    }

    /// <summary>
    /// The one place the selected packs start the party in, or null when they state no start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>One start, or none.</b> Which place a game begins in is the scenario's decision, so two starts
    /// inside the selected set would leave that decision to the order the packs happened to load in — a
    /// place nobody chose winning because its pack came first. That is refused with every candidate named,
    /// which is the same posture a selected pack that is present and wrong already meets.
    /// </para>
    /// <para>
    /// A start that names no place is refused the same way rather than skipped: a scenario that says where
    /// the party begins and then says nothing is a defect, and quietly falling through to the next entry
    /// would decide the start by the shape of the mistake.
    /// </para>
    /// <para>
    /// The catalog is already the selection — the packs the bundle named and no others — so a start in a
    /// pack nobody selected is not here to be found.
    /// </para>
    /// </remarks>
    /// <param name="catalog">The content the product selected.</param>
    /// <returns>The starting place and the arrival point it names, or null when the selection states no start.</returns>
    /// <exception cref="ContentValidationException">The selection states more than one start, or one that names no place.</exception>
    private static (PlaceId Place, string? EntryPoint)? ReadStart(ContentCatalog catalog)
    {
        if (OnlyStart(catalog) is not { } only) return null;
        string place = only.Entry.GetId("place");
        if (place.Length == 0)
        {
            throw new ContentValidationException(
                $"The scenario start '{only.Entry.Id}' names no place, so there is nowhere for the party to begin: {Candidate(only)}.",
                [new ContentValidationIssue(
                    "scenario-start-incomplete",
                    $"start '{only.Entry.Id}' names no place, so the party has nowhere to begin.",
                    only.Pack.PackId,
                    only.Document.DocumentId)]);
        }

        string entryPoint = only.Entry.GetId("entryPoint");
        return (new PlaceId(place), entryPoint.Length == 0 ? null : entryPoint);
    }

    /// <summary>
    /// Which start a new session's party takes, as the selected scenario states it, or null when it states none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The statement lives on the scenario start, not on the bundle.</b> How a game begins — where the party
    /// stands and who the party is — is one decision of the scenario's, and the start entry is already where the
    /// ruleset reads it: a scenario pack that ships a fixed party is the one that knows it means the party to be
    /// played, and the same pack selected by any bundle begins the same way. A bundle is the kit's pack selection
    /// and carries no ruleset meaning, so putting the switch there would make two bundles over one scenario two
    /// different games, and would leave a bundle stating a party for a scenario that has none.
    /// </para>
    /// <para>
    /// The property is <c>party</c>, with <c>creation</c> (the player-facing screen) or <c>scenario</c> (the
    /// party this scenario's <c>scenario-party</c> document fixes). A start that leaves it out takes creation
    /// wherever the host offers a creation screen; any other word is refused by name, because a misspelled
    /// switch silently taking the default would play a game nobody chose.
    /// </para>
    /// </remarks>
    /// <param name="catalog">The content the product selected, when it selected any.</param>
    /// <returns>The stated start, or null when the selection states no start or a start that does not say.</returns>
    /// <exception cref="ContentValidationException">The selection states more than one start, or one whose party word is not a start.</exception>
    internal static (SessionPartyStart Start, string Entry, string Pack)? StatedPartyStart(ContentCatalog? catalog)
    {
        if (catalog is null || OnlyStart(catalog) is not { } only) return null;
        if (!only.Entry.Has(PartyStartProperty)) return null;

        string word = only.Entry.GetString(PartyStartProperty);
        return word switch
        {
            CreationPartyStart => (SessionPartyStart.Creation, only.Entry.Id, only.Pack.PackId),
            ScenarioPartyStart => (SessionPartyStart.Scenario, only.Entry.Id, only.Pack.PackId),
            _ => throw new ContentValidationException(
                $"The scenario start '{only.Entry.Id}' in {only.Pack.PackId}/{only.Document.DocumentId} says its party is '{word}', which is not a start: a new session's party is '{CreationPartyStart}' or '{ScenarioPartyStart}'.",
                [new ContentValidationIssue(
                    "scenario-start-party-unknown",
                    $"start '{only.Entry.Id}' says its party is '{word}', and the party starts are '{CreationPartyStart}' and '{ScenarioPartyStart}'.",
                    only.Pack.PackId,
                    only.Document.DocumentId)]),
        };
    }

    /// <summary>The property of a scenario start that says which start a new session's party takes.</summary>
    internal const string PartyStartProperty = "party";

    /// <summary>The word for a start in which the player creates the party.</summary>
    internal const string CreationPartyStart = "creation";

    /// <summary>The word for a start that plays the party the scenario fixes.</summary>
    internal const string ScenarioPartyStart = "scenario";

    /// <summary>The one start entry the selection states, or null when it states none.</summary>
    /// <exception cref="ContentValidationException">The selection states more than one start.</exception>
    private static (LoadedPack Pack, ContentDocument Document, ContentEntry Entry)? OnlyStart(ContentCatalog catalog)
    {
        List<(LoadedPack Pack, ContentDocument Document, ContentEntry Entry)> starts = [.. catalog.Entries(StartDefinitionKind)];
        if (starts.Count == 0) return null;

        if (starts.Count > 1)
        {
            throw new ContentValidationException(
                $"The content this product selected states {starts.Count} scenario starts, and which place the party begins in would be the order the packs happened to load in. The candidates are {Joined(starts)}.",
                [.. starts.Select(candidate => new ContentValidationIssue(
                    "scenario-start-ambiguous",
                    $"{Candidate(candidate)}, and it is one of {starts.Count} starts this selection states.",
                    candidate.Pack.PackId,
                    candidate.Document.DocumentId))]);
        }

        return starts[0];

        static string Joined(IReadOnlyList<(LoadedPack Pack, ContentDocument Document, ContentEntry Entry)> candidates) =>
            string.Join("; ", candidates.Select(Candidate));
    }

    /// <summary>
    /// One start stated the way a refusal reads it: which pack and document declared it, which entry it is, and
    /// the place it would put the party in.
    /// </summary>
    private static string Candidate((LoadedPack Pack, ContentDocument Document, ContentEntry Entry) start)
    {
        string place = start.Entry.GetId("place");
        string begins = place.Length == 0 ? "no place" : $"place '{place}'";
        return $"'{start.Entry.Id}' in {start.Pack.PackId}/{start.Document.DocumentId}, which begins the party in {begins}";
    }
}
