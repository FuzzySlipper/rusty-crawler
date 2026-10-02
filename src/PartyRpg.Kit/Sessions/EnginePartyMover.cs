using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Interaction;

namespace PartyRpg.Kit.Sessions;

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

    /// <summary>Updates the current place from its canonical authored state when it changed.</summary>
    void Refresh(PlaceId place) { }

    /// <summary>Moves the party by one step of admitted world time.</summary>
    /// <param name="intent">What the player asked for this step.</param>
    /// <param name="elapsedSeconds">The admitted world time the step covers.</param>
    /// <returns>Where the party ended up and what the engine and the tuning said about it.</returns>
    MovementOutcome Step(MovementIntent intent, double elapsedSeconds);

    /// <summary>Whether the party stands on something it could leap from now; a mover that cannot leap answers false.</summary>
    bool CanLeap => false;

    /// <summary>
    /// Asks the next step to leap at a multiple of the party's own jump; a mover that cannot leap answers false.
    /// </summary>
    /// <param name="multiple">How many times the party's own jump the leap is.</param>
    /// <returns>Whether the leap was taken.</returns>
    bool Leap(double multiple) => false;

    /// <summary>Whether the party may fly now; a mover that cannot fly answers false.</summary>
    bool MayFly => false;

    /// <summary>Whether the party is flying now; a mover that cannot fly answers false.</summary>
    bool Flying => false;

    /// <summary>
    /// The ground the party stands on now, or null when it stands on nothing — in the air, flying, or before its first
    /// step. A mover that tells no ground apart answers null.
    /// </summary>
    SurfaceEffect? Footing => null;

    /// <summary>
    /// The named ground a pose in a place stands on — the water a body fell in — or null when it stands on ordinary
    /// ground, or the place is not the one the mover holds geometry for. A mover that tells no ground apart answers null.
    /// </summary>
    /// <param name="place">The place the pose is in.</param>
    /// <param name="pose">The pose, whose height is where it stands.</param>
    string? GroundUnder(PlaceId place, PlacePose pose) => null;

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
/// infers collision from a visual mesh or synthesizes a document for a place that has none —
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
    private PlaceGeometry? _currentGeometry;
    private bool _disposed;

    /// <summary>Creates the mover over a party's movement owner.</summary>
    /// <param name="spatial">The engine service that owns the collision scene and resolves character steps.</param>
    /// <param name="movement">The party's movement owner, whose scene this fills with places' geometry.</param>
    /// <param name="content">The engine's content owner, which retains the artifact document a place provides.</param>
    /// <param name="navigation">The game's navigation policy, which artifacts are admitted under and creatures steer by.</param>
    /// <param name="geometry">Where places' artifacts come from. Without one every place has no geometry.</param>
    /// <exception cref="ArgumentNullException">A required collaborator is missing.</exception>
    public EnginePartyMover(
        ISpatialService spatial,
        PartyMovement movement,
        IContentService content,
        PlaceNavigationPolicy navigation,
        IPlaceGeometrySource? geometry = null)
    {
        _spatial = spatial ?? throw new ArgumentNullException(nameof(spatial));
        _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _geometry = geometry;
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    /// <summary>The game's navigation policy, which a creature walking in the same scene steers by.</summary>
    public PlaceNavigationPolicy Navigation => _navigation;

    /// <summary>What the scene holds for the place the party is in, or null before it entered one.</summary>
    public PlaceGeometryAdmission? Current { get; private set; }

    /// <summary>
    /// The collision scene the party walks in and every place's geometry is admitted to, which is the scene
    /// anything else that walks in the place must be stepped in too.
    /// </summary>
    public SpatialSession Session => _movement.Session;

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
            _movement.Motion.Ground = PlaceSurfaces.None;
            Current = PlaceGeometryAdmission.Empty(place);
            return Current;
        }

        return Admit(place, geometry);
    }

    /// <inheritdoc />
    public void Refresh(PlaceId place)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Current?.Place != place || _geometry?.For(place) is not { Collision: not null } geometry ||
            ReferenceEquals(geometry, _currentGeometry)) return;
        Admit(place, geometry);
    }

    private PlaceGeometryAdmission Admit(PlaceId place, PlaceGeometry geometry)
    {
        if (_filled) Release();

        // The place's named ground is the place's, like its collision: what the party stood on in the last place names
        // nothing here.
        _movement.Motion.Ground = geometry.Surfaces;

        ulong vertices, triangles, cells;
        if (geometry.Collision is { } collision)
        {
            // A whole replacement owns all identities. Never mix guessed IDs with an artifact's private IDs.
            StaticMeshAsset[] assets = collision.Triangles.Length == 0 ? [] :
                [new(1, 0, checked((uint)collision.Positions.Length), 0, checked((uint)collision.Triangles.Length))];
            StaticMeshInstance[] instances = assets.Length == 0 ? [] :
                [new(1, 1, new Transform(Vector3.Zero, Quaternion.Identity, Vector3.One))];
            _spatial.ReplaceCollision(new CollisionReplaceRequest(_movement.Session, assets,
                collision.Positions, collision.Triangles, instances));
            // The successful call admitted this complete authored mesh; it carries no immutable artifact identity.
            vertices = (ulong)collision.Positions.Length;
            triangles = (ulong)collision.Triangles.Length;
            cells = 0;
        }
        else
        {
            using ContentReference reference = _content.AdmitReference(
                new ContentAdmissionRequest(geometry.Path, geometry.Artifact, ReadOnlyMemory<ContentSourceFile>.Empty));
            SpatialContentArtifactReplaceReceipt receipt = _spatial.ReplaceContentArtifact(
                new SpatialContentArtifactReplaceRequest(_movement.Session, reference,
                    _navigation.GridId, _navigation.ChunkSize, _navigation.MaxStepCells));
            vertices = receipt.CollisionVertexCount;
            triangles = receipt.CollisionTriangleCount;
            cells = receipt.NavigationCellCount;
        }
        _filled = true;
        _currentGeometry = geometry;
        string? reason = null;
        if (geometry.Navigation is { } region)
        {
            CollisionNavigationConfig defaults = _spatial.DefaultCollisionNavigationConfig();
            CharacterControllerConfig body = _movement.Controller;
            double scale = body.Shape.StandingHeight / defaults.Character.Shape.StandingHeight;
            CollisionNavigationConfig config = defaults with
            {
                GridId = _navigation.GridId == 0 ? defaults.GridId : _navigation.GridId,
                CellSize = region.CellSize,
                ChunkSize = _navigation.ChunkSize,
                MaximumCells = _navigation.MaximumCells,
                Character = body,
                MaximumDrop = body.Surface.MaximumStepHeight,
                VerticalSearchCells = Math.Max(1u, (uint)Math.Ceiling(body.Surface.MaximumStepHeight / region.CellSize)),
                SnapAbove = defaults.SnapAbove * scale,
                SnapBelow = defaults.SnapBelow * scale,
            };
            // Collision bounds may be flat; the derivation region must include supports and headroom.
            Vector3 minimum = region.Minimum with { Y = region.Minimum.Y - (float)region.CellSize };
            Vector3 maximum = region.Maximum with { Y = region.Maximum.Y + body.Shape.StandingHeight };
            try
            {
                CollisionNavigationReplaceReceipt navigation = _spatial.ReplaceCollisionNavigation(
                    new CollisionNavigationReplaceRequest(_movement.Session, minimum, maximum, config));
                cells = navigation.WalkableCellCount;
                if (cells == 0) reason = "Engine derived no walkable supports for this place's body and collision.";
            }
            catch (EngineCallException error) when (error.Diagnostics.Span.ToArray().Any(diagnostic => diagnostic.Code == "CSHARP_COLLISION_NAVIGATION_BUDGET"))
            {
                // A derivation budget is recoverable: retain collision and name why pursuit must hold.
                cells = 0;
                reason = $"Navigation exceeds the {_navigation.MaximumCells}-column derivation budget; collision remains admitted.";
            }
        }
        else if (cells == 0) reason = "The place supplies no navigation region or walkable artifact cells.";

        Current = new PlaceGeometryAdmission(
            place,
            Admitted: true,
            vertices,
            triangles,
            cells) { NavigationReason = reason };
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
    public bool CanLeap => !_disposed && _movement.Motion.Grounded;

    /// <inheritdoc />
    public bool Leap(double multiple)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _movement.Motion.Leap(multiple);
    }

    /// <inheritdoc />
    public bool MayFly => !_disposed && _movement.Motion.MayFly;

    /// <inheritdoc />
    public bool Flying => !_disposed && _movement.Motion.Flying;

    /// <inheritdoc />
    public SurfaceEffect? Footing => !_disposed && _movement.Motion.Grounded && !_movement.Motion.Flying ? _movement.Motion.Surface : null;

    /// <inheritdoc />
    public string? GroundUnder(PlaceId place, PlacePose pose) =>
        !_disposed && Current?.Place == place ? _movement.Motion.GroundUnder(pose) : null;

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
        _currentGeometry = null;
    }
}
