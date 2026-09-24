using Rusty.Engine.Entities;

namespace PartyRpg.Kit.World;

/// <summary>
/// The entities living in the place the party is standing in: created from content's placements, stepped
/// inside the session's one admitted update, and destroyed the moment the party leaves.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the only place these entities live.</b> They live in one <see cref="EntityStore"/> the
/// population owns — the engine's own entity mechanism, not a store invented beside it — and this class
/// is the only code that creates, steps, or destroys them. There is no component registry, no ambient
/// service lookup, and no loop of its own: <see cref="Step"/> is called by the session's admitted update
/// and is the only thing that moves a population forward. Behavior belongs to whoever attaches
/// components to an entity's <see cref="PlacePopulationEntity.Actor"/> and steps them inside that same
/// update; the population does not grow a scheduler for it.
/// </para>
/// <para>
/// <b>Content identity and runtime identity are different things.</b> A placement's
/// <see cref="PlacementContentId"/> is what content authored and the half a save could record; the
/// runtime <see cref="EntityId"/> is local to this population's store, never reused, and never durable.
/// Leaving destroys the entities, so nothing outside a visit can depend on one.
/// </para>
/// <para>
/// <b>A visit is rebuilt, never accumulated.</b> Entering populates the place from its placements after
/// destroying whatever was alive, so walking in and out leaves the same population every time and never
/// two of anything. A place the party has emptied stays empty until the world restores it, and a restore
/// of the place the party stands in rebuilds the population exactly once.
/// </para>
/// </remarks>
public sealed class PlacePopulation : IDisposable
{
    private readonly EntityStore _entities = new();
    private readonly PlacePopulationContent _content;
    private readonly PlaceStateLedger _places;
    private PlacePopulationEntity[] _live = [];
    private bool _disposed;

    /// <summary>Creates the population of a world, reading the placements its places declare.</summary>
    /// <remarks>
    /// Placements are read once, here, rather than lazily on the day the party walks in: a content defect
    /// then fails while the world is being built, with every defective placement reported at once, and
    /// what a place holds cannot change halfway through a session.
    /// </remarks>
    /// <param name="places">The world's places, which carry the placements.</param>
    /// <param name="states">The world's per-place state, which says whether a place has been emptied.</param>
    public PlacePopulation(PlaceGraph places, PlaceStateLedger states)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(states);
        _places = states;
        _content = PlacePopulationContent.Read(places);
    }

    /// <summary>
    /// The place the population was built for, or null before any place was entered. It is the place,
    /// not the entity count, that says what is populated: a place content leaves empty is still where
    /// the population stands.
    /// </summary>
    public PlaceId? Place { get; private set; }

    /// <summary>The live entities, in the order content declared their placements.</summary>
    public IReadOnlyList<PlacePopulationEntity> Entities => _live;

    /// <summary>What the population holds right now, for a projection or a diagnostic read.</summary>
    public PlacePopulationDiagnostics Diagnostics
    {
        get
        {
            SortedDictionary<string, int> byKind = new(StringComparer.Ordinal);
            foreach (PlacePopulationEntity entity in _live)
            {
                byKind[entity.Content.Kind] = byKind.GetValueOrDefault(entity.Content.Kind) + 1;
            }

            return new PlacePopulationDiagnostics(Place, _disposed ? 0 : _entities.Diagnostics(0).EntityCount, byKind);
        }
    }

    /// <summary>The placements a place declares, whether or not the party is standing in it.</summary>
    /// <param name="place">The place whose placements to read.</param>
    public IReadOnlyList<PlacementDefinition> PlacementsOf(PlaceId place) => _content.PlacementsOf(place);

    /// <summary>
    /// Steps the population inside the session's one admitted update: the party's place decides what is
    /// alive, and a place the world restored under the party is rebuilt.
    /// </summary>
    /// <remarks>
    /// A restore reported for a place the party is not in changes nothing here. Nothing is alive for a
    /// place the party has left, so there is no population to restore: the place is populated fresh,
    /// from its placements, the next time the party walks in.
    /// </remarks>
    /// <param name="place">The place the party is standing in.</param>
    /// <param name="restored">The places the world restored this update, as its state ledger reported them.</param>
    /// <returns>The live entities, in the order content declared their placements.</returns>
    public IReadOnlyList<PlacePopulationEntity> Step(PlaceId place, IReadOnlyList<PlaceState> restored)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(restored);

        if (Place != place)
        {
            Populate(place);
            return _live;
        }

        foreach (PlaceState state in restored)
        {
            if (state.Place != place) continue;

            // The world restored the place the party is standing in, which happens when a rest or a
            // journey carries the clock past its interval. Rebuilding is what makes the restore mean
            // anything to a party already inside; doing it here is what keeps it to one rebuild.
            Populate(place);
            break;
        }

        return _live;
    }

    /// <summary>Destroys every entity and releases the store they lived in.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Despawn();
        _entities.Dispose();
    }

    /// <summary>Destroys whatever is alive and builds the place's population from content.</summary>
    private void Populate(PlaceId place)
    {
        // The place's state is resolved before anything is destroyed: a place the world does not have
        // fails by name here, and a refused step must not empty the place the party is actually in.
        PlaceState state = _places.StateOf(place);
        Despawn();
        Place = place;

        // An emptied place populates nobody until the world restores it. Building its population on
        // re-entry would quietly undo the party's clearing, and the restore the ledger reports is the
        // one path that brings a population back.
        if (state.Cleared) return;

        List<PlacePopulationEntity> live = [];
        foreach (PlacementDefinition placement in _content.PlacementsOf(place))
        {
            EntityId id = _entities.Create(new EntityTypeId(placement.Content.Kind), EntityLifecycle.Active);
            Actor actor = new(_entities, id);

            // The placement is attached, not copied: what the entity is in content and what a ruleset
            // reads about it are one record, so the two can never drift apart.
            actor.Add(placement);
            live.Add(new PlacePopulationEntity(actor, placement));
        }

        _live = [.. live];
    }

    /// <summary>Destroys every live entity, so nothing outlives the visit that created it.</summary>
    private void Despawn()
    {
        foreach (PlacePopulationEntity entity in _live)
        {
            // References a caller already holds stay valid and report themselves dead; only the store's
            // rows go away, which is what makes a leak observable in the store's own count.
            _entities.Destroy(entity.Id, null);
        }

        _live = [];
        Place = null;
    }
}
