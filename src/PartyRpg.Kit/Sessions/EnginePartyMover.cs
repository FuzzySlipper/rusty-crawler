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
