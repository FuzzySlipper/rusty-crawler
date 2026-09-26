using System.Numerics;
using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Interaction;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// A source of elapsed game time for the world's own bookkeeping.
/// </summary>
/// <remarks>
/// The one clock satisfies this seam, and it is the source a product runs on: a place's population is
/// restored against the day count the clock reports, so respawn is measured in the same game time travel,
/// rest, and the admitted update all move. The seam exists so the world asks for a day count rather than
/// for a clock, which is what lets a test drive respawn with a day source of its own. A world without a
/// source does not advance, and says so by doing nothing.
/// </remarks>
public interface IWorldTimeSource
{
    /// <summary>Whole game days elapsed since the session began, day zero being its first day.</summary>
    int ElapsedGameDays { get; }
}

/// <summary>
/// What the party's movement has done so far, as an observation.
/// </summary>
/// <remarks>
/// This is where a fall becomes visible: the movement owner reports what a landing cost and this records
/// it, because applying it would mean reaching into health the kit does not hold. The party's health
/// owner applies <see cref="LastFall"/> when it exists; until then a fall past the threshold is reported
/// here, published as an engine diagnostic, and charged to nobody.
/// </remarks>
/// <param name="Last">The last step's outcome, or null before the party has taken one.</param>
/// <param name="Falls">How many landings went past the tuning's fall threshold.</param>
public sealed record MovementDiagnostics(MovementOutcome? Last, int Falls)
{
    /// <summary>Nothing has moved yet: no step, and no fall.</summary>
    public static MovementDiagnostics None { get; } = new(null, 0);

    /// <summary>What the last landing cost, or none when the party has not landed past the threshold.</summary>
    public FallOutcome LastFall => Last?.Fall ?? FallOutcome.None;
}

/// <summary>
/// One place's collision geometry, in the engine's own canonical artifact document.
/// </summary>
/// <remarks>
/// The bytes are the engine's document, not a kit format: the engine parses them itself, and everything
/// between content and that parse copies them unchanged. A kit that re-wrote the document would own a
/// second copy of the engine's schema and would be the first thing to disagree with it.
/// </remarks>
public sealed record PlaceGeometry
{
    /// <summary>Creates a place's geometry.</summary>
    /// <param name="path">The artifact's own path, which identifies it to the engine's content owner.</param>
    /// <param name="artifact">The artifact document's bytes, exactly as content wrote them.</param>
    /// <exception cref="ArgumentException">The artifact has no path or no bytes.</exception>
    public PlaceGeometry(string path, ReadOnlyMemory<byte> artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (artifact.Length == 0)
        {
            throw new ArgumentException(
                $"The collision artifact '{path}' carries no bytes, so the place that declares it would be admitted as geometry nobody can walk on.",
                nameof(artifact));
        }

        Path = path;
        Artifact = artifact;
    }

    /// <summary>The artifact's own path, which identifies it to the engine's content owner.</summary>
    public string Path { get; }

    /// <summary>The artifact document's bytes, exactly as content wrote them.</summary>
    public ReadOnlyMemory<byte> Artifact { get; }
}

/// <summary>Where a place's collision geometry comes from, when the loaded content carries any.</summary>
public interface IPlaceGeometrySource
{
    /// <summary>The place's collision geometry, or null when content provides none for it.</summary>
    /// <param name="place">The place the party is entering.</param>
    PlaceGeometry? For(PlaceId place);
}

/// <summary>
/// Reads a place's collision geometry out of the content the product loaded.
/// </summary>
/// <remarks>
/// <para>
/// The definition kind and the artifact property are supplied by whoever owns the content vocabulary, so
/// this reader stays general while the ruleset keeps naming its own documents. The bytes handed on are
/// the artifact document exactly as content wrote it — read out of the envelope it arrived in and never
/// re-serialized, so the engine parses the document the author wrote rather than a copy of it.
/// </para>
/// <para>
/// An entry that exists for a place but carries no artifact document stops the read instead of yielding
/// nothing: a place whose geometry silently went missing is a party walking through the floor, and that
/// is worse than a named failure at the moment the party tries to enter.
/// </para>
/// </remarks>
public sealed class ContentPlaceGeometry : IPlaceGeometrySource
{
    private readonly ContentCatalog _catalog;
    private readonly string _definitionKind;
    private readonly string _artifactProperty;

    /// <summary>Creates the reader.</summary>
    /// <param name="catalog">The validated content the world was built from.</param>
    /// <param name="definitionKind">The definition kind whose entries carry places' collision artifacts.</param>
    /// <param name="artifactProperty">The property of such an entry that holds the engine's artifact document.</param>
    /// <exception cref="ArgumentNullException">No content catalog was supplied.</exception>
    /// <exception cref="ArgumentException">A name is missing, so no entry could ever be found.</exception>
    public ContentPlaceGeometry(ContentCatalog catalog, string definitionKind, string artifactProperty)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactProperty);
        _catalog = catalog;
        _definitionKind = definitionKind;
        _artifactProperty = artifactProperty;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The place's entry declares no artifact document, so the geometry content promised cannot be handed on.
    /// </exception>
    public PlaceGeometry? For(PlaceId place)
    {
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in _catalog.Entries(_definitionKind))
        {
            if (!string.Equals(entry.Id, place.Value, StringComparison.Ordinal)) continue;
            if (!entry.Payload.TryGetProperty(_artifactProperty, out JsonElement artifact) ||
                artifact.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Place '{place}' declares collision geometry in '{pack.PackId}/{document.DocumentId}', but its '{_artifactProperty}' is not the engine's artifact document, so the place's collision cannot be admitted.");
            }

            // The entry's own identity is the artifact's path, so the engine's content owner reports
            // which pack, document, and entry a retained artifact came from rather than an opaque handle.
            return new PlaceGeometry(
                $"{pack.PackId}/{document.DocumentId}/{entry.Id}.json",
                Encoding.UTF8.GetBytes(artifact.GetRawText()));
        }

        return null;
    }
}

/// <summary>What entering a place did to the movement's collision scene.</summary>
/// <remarks>
/// A place that provides no geometry is not an error and not a fallback: it is a place the party can
/// stand nowhere in, and saying so is what keeps the emptiness visible instead of implied.
/// </remarks>
/// <param name="Place">The place whose geometry was asked for.</param>
/// <param name="Admitted">Whether the engine admitted geometry for it.</param>
/// <param name="CollisionVertices">How many collision vertices the admitted artifact carried.</param>
/// <param name="CollisionTriangles">How many collision triangles the admitted artifact carried.</param>
/// <param name="NavigationCells">How many walkable navigation cells the admitted artifact carried.</param>
public sealed record PlaceGeometryAdmission(
    PlaceId Place,
    bool Admitted,
    ulong CollisionVertices,
    ulong CollisionTriangles,
    ulong NavigationCells)
{
    /// <summary>The scene holds nothing for this place: the party stands on nothing in it.</summary>
    /// <param name="place">The place whose geometry was asked for.</param>
    public static PlaceGeometryAdmission Empty(PlaceId place) => new(place, false, 0, 0, 0);
}

/// <summary>
/// The navigation policy a place's artifact cells are admitted under.
/// </summary>
/// <remarks>
/// The artifact states its own cell size and its cells; the grid identity, the chunking, and how far a
/// navigation step may climb are the product's policy, and they are stated here in the engine's own
/// terms rather than being worked out from the artifact.
/// </remarks>
/// <param name="GridId">The grid identity the artifact's cells are projected into.</param>
/// <param name="ChunkSize">How many cells a navigation chunk spans; the engine requires a cubic chunk.</param>
/// <param name="MaxStepCells">How many cells a navigation step may climb.</param>
public sealed record PlaceNavigationPolicy(ulong GridId = 0, uint ChunkSize = 16, uint MaxStepCells = 4);

/// <summary>
/// The party's movement as the world drives it: the collision scene it walks in, the geometry that
/// belongs to the place it is in, and the one step it takes per admitted interval.
/// </summary>
/// <remarks>
/// Entering and stepping are one collaborator because they are one scene: whoever admits a place's
/// geometry is whoever resolves the party's steps in it, so the ground the party stands on and the party
/// standing on it can never be two different scenes.
/// </remarks>
public interface IPartyMover : IDisposable
{
    /// <summary>
    /// Releases whatever geometry the scene holds and admits the place's own, which is what makes a
    /// place's collision belong to that place.
    /// </summary>
    /// <param name="place">The place the party is entering.</param>
    /// <returns>What the scene holds for the place now.</returns>
    PlaceGeometryAdmission Enter(PlaceId place);

    /// <summary>Moves the party by one step of admitted world time.</summary>
    /// <param name="intent">What the player asked for this step.</param>
    /// <param name="elapsedSeconds">The admitted world time the step covers.</param>
    /// <returns>Where the party ended up and what the engine and the tuning said about it.</returns>
    MovementOutcome Step(MovementIntent intent, double elapsedSeconds);

    /// <summary>
    /// Whether nothing solid stands between two points of the place the party is in, in the engine's world
    /// axes.
    /// </summary>
    /// <remarks>
    /// This is the scene's own line of sight, asked of whoever holds the collision: a use reaches only what
    /// the party can see, and the answer must come from the geometry the party is actually walking in rather
    /// than from a second opinion about what is between two points. A mover whose place holds no geometry
    /// answers that nothing occludes anything, because it holds nothing that could.
    /// </remarks>
    /// <param name="from">Where the sight line starts.</param>
    /// <param name="to">What the party is looking at.</param>
    /// <returns>Whether the target is in sight.</returns>
    bool InSight(Vector3 from, Vector3 to);
}

/// <summary>
/// The engine-backed party mover: one movement owner, and the collision scene it walks in filled from
/// the place the party is in.
/// </summary>
/// <remarks>
/// <para>
/// The geometry path is the engine's own content artifact: bytes content already carries are admitted to
/// the engine's content owner, and the spatial service resolves and copies them. Nothing here builds
/// vertices, infers collision from a visual mesh, or synthesizes a document for a place that has none —
/// a place with no artifact gets an empty scene, and empty is reported as empty.
/// </para>
/// <para>
/// The release on entering is a real engine call with empty collider sets rather than a flag: the scene
/// the party walks in is the engine's, so leaving the previous place's geometry behind is the engine's
/// state to be emptied, not the mover's to remember.
/// </para>
/// </remarks>
public sealed class EnginePartyMover : IPartyMover
{
    private readonly ISpatialService _spatial;
    private readonly IContentService _content;
    private readonly PartyMovement _movement;
    private readonly IPlaceGeometrySource? _geometry;
    private readonly PlaceNavigationPolicy _navigation;
    private bool _filled;
    private bool _disposed;

    /// <summary>Creates the mover over a party's movement owner.</summary>
    /// <param name="spatial">The engine service that owns the collision scene and resolves character steps.</param>
    /// <param name="movement">The party's movement owner, whose scene this fills with places' geometry.</param>
    /// <param name="content">The engine's content owner, which retains the artifact document a place provides.</param>
    /// <param name="geometry">Where places' artifacts come from. Without one every place has no geometry.</param>
    /// <param name="navigation">The navigation policy artifacts are admitted under.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public EnginePartyMover(
        ISpatialService spatial,
        PartyMovement movement,
        IContentService content,
        IPlaceGeometrySource? geometry = null,
        PlaceNavigationPolicy? navigation = null)
    {
        _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _geometry = geometry;
        _navigation = navigation ?? new PlaceNavigationPolicy();
    }

    /// <summary>What the scene holds for the place the party is in, or null before it entered one.</summary>
    public PlaceGeometryAdmission? Current { get; private set; }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public PlaceGeometryAdmission Enter(PlaceId place)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_geometry?.For(place) is not { } geometry)
        {
            // A place with no geometry leaves the scene empty, and emptying it is a real engine call only
            // when this mover ever filled it: a mover with no geometry source has never handed the scene
            // anything, and telling the engine to clear a scene it holds nothing in would be work that
            // changes nothing.
            if (_filled) Release();
            Current = PlaceGeometryAdmission.Empty(place);
            return Current;
        }

        if (_filled) Release();

        // The reference is released as soon as the engine has resolved and copied the document: the
        // scene retains its own collision, so nothing downstream depends on the artifact staying
        // admitted to the content owner.
        using ContentReference reference = _content.AdmitReference(
            new ContentAdmissionRequest(geometry.Path, geometry.Artifact, ReadOnlyMemory<ContentSourceFile>.Empty));
        SpatialContentArtifactReplaceReceipt receipt = _spatial.ReplaceContentArtifact(
            new SpatialContentArtifactReplaceRequest(
                _movement.Session,
                reference,
                _navigation.GridId,
                _navigation.ChunkSize,
                _navigation.MaxStepCells));

        _filled = true;
        Current = new PlaceGeometryAdmission(
            place,
            Admitted: true,
            receipt.CollisionVertexCount,
            receipt.CollisionTriangleCount,
            receipt.NavigationCellCount);
        return Current;
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _movement.Step(intent, elapsedSeconds);
    }

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The mover has been disposed.</exception>
    public bool InSight(Vector3 from, Vector3 to)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The engine's own line-of-sight composition over this mover's spatial session: the collision the
        // party walks in is the collision it sees through, and the ray carries no entity colliders because
        // the scene holds none — the population's colliders are not submitted to the spatial session yet.
        return InteractionVisibilityQuery.Cast(
            _spatial,
            _movement.Session,
            from,
            to,
            new SpatialQueryFilter(0, 0),
            ReadOnlyMemory<SpatialEntityCollider>.Empty,
            ReadOnlyMemory<ulong>.Empty) == InteractionVisibility.Visible;
    }

    /// <summary>Releases the engine's spatial session, which destroys its collision scene with it.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _movement.Dispose();
    }

    /// <summary>Empties the scene, so nothing of the place being left can be stood on.</summary>
    private void Release()
    {
        _spatial.ReplaceCollision(new CollisionReplaceRequest(
            _movement.Session,
            ReadOnlyMemory<StaticMeshAsset>.Empty,
            ReadOnlyMemory<Vector3>.Empty,
            ReadOnlyMemory<Triangle>.Empty,
            ReadOnlyMemory<StaticMeshInstance>.Empty));
        _filled = false;
        Current = null;
    }
}

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
public sealed class SessionWorld : IDisposable, IInteractionWorld, IRestSite, ICombatWorld, ICombatPositions
{
    private readonly TransitionExecutive _transitions;
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
        ICreatureMover? creatures = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(places);
        _transitions = new TransitionExecutive(costRule);
        // A world handed a clock but no separate day source reads its days from that same clock: in a
        // product they are one object, and a second source here would be a second answer to what day it is.
        _time = time ?? clock;
        _clock = clock;
        _resources = resources;
        _entity = partyEntity;
        _diagnostics = diagnostics;
        Graph = graph;
        Party = party;
        Places = places;
        Places.MarkVisited(party.Place);
        _population = new PlacePopulation(graph, places);
        _entrances = Index(graph, entrances);
        Mover = mover;
        Creatures = creatures;
        Schedule = schedule ?? PlaceSchedule.Empty;
        Interaction = interaction is null ? null : new PartyInteraction(this, interaction.Rule, interaction.Space, interaction.Tuning);
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
    /// Who is told every time this world moves the session's one clock.
    /// </summary>
    /// <remarks>
    /// A journey's charge is an advance of the one clock that no admitted update sees, so an owner that
    /// keeps a game-time schedule — a service's shelves, and later rest and effects — has to be told about
    /// it or its deadline would re-arm past a stretch of time nobody spent. It is assigned rather than
    /// composed because the mechanism that observes the clock is composed over the party, and for a session
    /// that creates its party that is the moment creation is accepted, after this world exists. A world
    /// nobody tells still moves its clock; only the telling is absent.
    /// </remarks>
    public IGameTimeObserver? TimeObserver { get; private set; }

    /// <summary>Tells this world who to hand its clock advances to.</summary>
    /// <param name="observer">The owner that keeps a schedule against the session's one clock.</param>
    /// <exception cref="ArgumentNullException">The observer is null.</exception>
    public void ObserveTimeWith(IGameTimeObserver observer) =>
        TimeObserver = observer ?? throw new ArgumentNullException(nameof(observer));

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
        try
        {
            Party.Enter(result.Place, result.Pose);
        }
        catch (ArgumentException error)
        {
            return TransitionResult.Refused(kind, Party.Place, Party.PlacePose, new TravelRefusal(
                "place-refused-arrival",
                $"The destination {result.Place} refused the arrival: {error.Message}"));
        }

        Charge(result.ChargedCost);
        EnterPlace(result.Place);
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
    /// Advances the world to the day its time source reports, returning the places whose population was
    /// restored. Without a time source the world does not advance, and says so by doing nothing.
    /// </summary>
    public IReadOnlyList<PlaceState> AdvanceTime()
    {
        IReadOnlyList<PlaceState> restored = _time is null ? [] : Places.AdvanceTo(_time.ElapsedGameDays);

        // The population follows the same advance: a place whose reset came due is repopulated here, in
        // the same update that moved the clock, rather than by a second timer of its own.
        _population.Step(Party.Place, restored);

        // A restored place comes back as it was: what the party did to its doors and containers belongs to
        // the visit that did it, so a place whose population is restored forgets it in the same update. A
        // reset that left an opened door open and an emptied chest empty would be a population brought back
        // into a ruin.
        foreach (PlaceState state in restored) _interactions.Forget(state.Place);
        return restored;
    }

    /// <summary>Populates the party's place, which also happens on the first update after arriving.</summary>
    public IReadOnlyList<PlacePopulationEntity> Populate() => _population.Step(Party.Place, []);

    /// <summary>
    /// Captures the world's durable state: where the party stands, and what each place remembers.
    /// </summary>
    /// <remarks>
    /// The graph, the entrances, the mover's collision scene, the movement observations, the population's
    /// entities, and what the party has done to each place's targets are absent on purpose. The graph and the
    /// entrances are loaded content, the scene is refilled from the place the party resumes in, movement
    /// observations belong to the steps that produced them, the entities are rebuilt from placements, and
    /// interaction state is live state the persistence owner does not carry yet — each of them a runtime
    /// shape that a load composes again rather than one a save carries.
    /// </remarks>
    public WorldSave Capture() => new(Party.Capture(), Places.Capture());

    /// <summary>Releases the entities the population owns and the engine's collision scene with the movement.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _population.Dispose();

        // The creature mover walks in the party mover's own spatial session, so it is released first and
        // releases nothing of its own beyond the walkers it kept: the scene belongs to the movement.
        (Creatures as IDisposable)?.Dispose();
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
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{change.Year:0000}-{change.Month:00}-{change.Day:00} {change.Hour:00}:{change.Minute:00}")
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
    /// Where an actor stands now, which for a creature that has moved is not where content placed it.
    /// </summary>
    /// <remarks>
    /// Only the creatures this world has actually moved have a live position; everything else stands where
    /// its placement put it, and the fight reads the placement itself. Answering null rather than the
    /// placement keeps the two apart: a world that has no position of its own for an actor must not claim
    /// content's, because a fight that read one would measure a creature that moved against where it began.
    /// </remarks>
    PlacePose? ICombatPositions.PoseOf(CombatantId actor) => Creatures?.PoseOf(actor);

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
    /// whose event nothing executes readable from the product's own account of itself.
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
                ClockAdvance advance = clock.Advance(TravelTimeConversion.Elapsed(clock.Calendar, cost.Time));

                // A journey's days are the clock's own, so a day boundary crossed on the road is a day the
                // world's places live through: the crossing the advance reported is what brings their
                // schedules up to the day the clock now stands on, in this same arrival rather than at
                // whatever update happens to come next.
                if (advance.Crossings.Days > 0) AdvanceTime();

                // The same advance reaches whoever keeps a schedule against game time, so a shop's shelves
                // refresh on the road exactly as they do in town. The journey is where an advance happens
                // without an update watching it, and a schedule that only saw the updates would wait a whole
                // further interval for a restock whose time the road already spent.
                TimeObserver?.Observe(advance);
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
    private void Report(PlaceEntrance entrance, TravelRefusal refusal)
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
    /// Reports a fall the tuning priced to the engine's diagnostics, without applying it.
    /// </summary>
    /// <remarks>
    /// A fall is the party's health owner's business, and that owner does not exist yet; the report is
    /// what makes the cost observable in the meantime, and it names the place so the drop can be traced
    /// to the geometry that produced it.
    /// </remarks>
    private void Report(MovementOutcome outcome)
    {
        if (!outcome.Fall.PastThreshold || _diagnostics is null) return;
        FallOutcome fall = outcome.Fall;
        _diagnostics.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "movement",
            Code: "fall-past-threshold",
            Message: $"The party landed in place '{Party.Place}' after falling {fall.Distance:0.###}, past the tuning's threshold by {fall.Excess:0.###}; the fall is reported and not applied, because the party's health owner does not exist yet.",
            Correlation: string.Empty));
    }
}
