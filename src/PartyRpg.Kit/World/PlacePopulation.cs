using PartyRpg.Kit.Time;
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
/// <para>
/// <b>Something may be created while the party stands there.</b> A creature a spell calls up or a body a spell
/// stands back up is created here, by <see cref="Summon"/>, in the same store and composed by the same composer
/// as everything content placed — the one way an entity comes to exist. It belongs to the visit like every
/// other entity: leaving or a rebuild ends it, and a length it was given ends it sooner, counted down by the
/// advances of the session's one clock (<see cref="Elapse"/>) rather than by a timer of its own.
/// </para>
/// </remarks>
public sealed class PlacePopulation : IDisposable
{
    private readonly EntityStore _entities = new();
    private readonly PlacePopulationContent _content;
    private readonly PlaceStateLedger _places;
    private readonly IPlacementComposer? _composer;
    private PlacePopulationEntity[] _live = [];
    private readonly Dictionary<EntityId, long> _lasting = [];
    private bool _disposed;

    /// <summary>Creates the population of a world, reading the placements its places declare.</summary>
    /// <remarks>
    /// Placements are read once, here, rather than lazily on the day the party walks in: a content defect
    /// then fails while the world is being built, with every defective placement reported at once, and
    /// what a place holds cannot change halfway through a session.
    /// </remarks>
    /// <param name="places">The world's places, which carry the placements.</param>
    /// <param name="states">The world's per-place state, which says whether a place has been emptied.</param>
    /// <param name="composer">What each placed entity is composed with beyond its placement, when a game states more.</param>
    /// <param name="expansion">
    /// What a placement that states a request — "some creatures of this encounter" — resolves to, as the game
    /// decides. It is asked while the placements are read, and it answers the same on every read, so every
    /// visit, every restore, and every load populates a place with the same entities.
    /// </param>
    public PlacePopulation(
        PlaceGraph places,
        PlaceStateLedger states,
        IPlacementComposer? composer = null,
        IPlacementExpansion? expansion = null)
    {
        _composer = composer;
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(states);
        _places = states;
        _content = PlacePopulationContent.Read(places, expansion);
    }

    /// <summary>
    /// The place the population was built for, or null before any place was entered. It is the place,
    /// not the entity count, that says what is populated: a place content leaves empty is still where
    /// the population stands.
    /// </summary>
    public PlaceId? Place { get; private set; }

    /// <summary>How many times a population has been built, which moves each time the place's entities are made afresh.</summary>
    /// <remarks>
    /// Whatever belonged to the entities of one build — a body lying where one of them fell — belongs to no
    /// entity of the next, so a caller that keeps such things compares this before and after a step.
    /// </remarks>
    public long Generation { get; private set; }

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

    /// <summary>
    /// Creates an entity in the place the party stands in now, which no placement content states.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The placement is the game's own statement of what is created — the row it fights as, where it stands —
    /// and it is attached and composed exactly as a placement content states is, so every reader of an entity
    /// reads a summoned one the same way. Its identity must not be one a live entity already answers for:
    /// two entities under one content identity would make a body, a target, and a report ambiguous.
    /// </para>
    /// <para>
    /// A length is how long the entity stays: the clock's advances count it down and the entity is destroyed
    /// when it has run (<see cref="Elapse"/>). Without one it stays for the visit.
    /// </para>
    /// </remarks>
    /// <param name="placement">What is created and where it stands.</param>
    /// <param name="lasts">How long it stays, or null for the rest of the visit.</param>
    /// <returns>The entity, live from now.</returns>
    /// <exception cref="ArgumentNullException">No placement was supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The length is no time at all.</exception>
    /// <exception cref="InvalidOperationException">
    /// No place has been populated, so there is nowhere to create it; or a live entity already answers for the
    /// placement's identity.
    /// </exception>
    public PlacePopulationEntity Summon(PlacementDefinition placement, GameDuration? lasts = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(placement);
        if (lasts is { IsNone: true })
        {
            throw new ArgumentOutOfRangeException(nameof(lasts), lasts, "An entity that stays no time at all would be gone in the advance that made it; a length is some game time.");
        }

        if (Place is not { } place)
        {
            throw new InvalidOperationException($"Nothing can be created for '{placement.Content}' before a place is populated: there is nowhere for it to stand.");
        }

        if (_live.Any(entity => entity.Content == placement.Content))
        {
            throw new InvalidOperationException($"An entity already answers for '{placement.Content}' in place '{place}', so a second one under that identity cannot be created.");
        }

        EntityId id = _entities.Create(new EntityTypeId(placement.Content.Kind), EntityLifecycle.Active);
        Actor actor = new(_entities, id);
        actor.Add(placement);
        actor.Add(new StandingPose(placement.Pose));
        PlacePopulationEntity entity = new(actor, placement, summoned: true);
        _composer?.Compose(entity, place);
        _live = [.. _live, entity];
        if (lasts is { } length) _lasting[id] = length.Milliseconds;
        return entity;
    }

    /// <summary>Destroys one live entity of this visit, which is how something leaves a place the party is still in.</summary>
    /// <param name="entity">The entity.</param>
    /// <returns>Whether it was live here and is now gone.</returns>
    /// <exception cref="ArgumentNullException">No entity was supplied.</exception>
    public bool Dismiss(PlacePopulationEntity entity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entity);
        if (!_live.Contains(entity)) return false;
        _entities.Destroy(entity.Id);
        _lasting.Remove(entity.Id);
        _live = [.. _live.Where(live => live != entity)];
        return true;
    }

    /// <summary>How much game time a summoned entity has left, or null when it stays for the visit or is not one.</summary>
    /// <param name="entity">The entity.</param>
    public GameDuration? RemainingOf(PlacePopulationEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _lasting.TryGetValue(entity.Id, out long left) ? GameDuration.FromMilliseconds(left) : null;
    }

    /// <summary>
    /// Counts down what was summoned for a length by game time that passed, destroying what has run out.
    /// </summary>
    /// <param name="elapsed">The game time the session's one clock moved by.</param>
    /// <returns>The entities that ran out, now gone, in the order they were live.</returns>
    public IReadOnlyList<PlacePopulationEntity> Elapse(GameDuration elapsed)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (elapsed.IsNone || _lasting.Count == 0) return [];
        List<PlacePopulationEntity> ended = [];
        foreach (PlacePopulationEntity entity in _live)
        {
            if (!_lasting.TryGetValue(entity.Id, out long left)) continue;
            long remaining = left - elapsed.Milliseconds;
            if (remaining > 0) _lasting[entity.Id] = remaining;
            else ended.Add(entity);
        }

        foreach (PlacePopulationEntity entity in ended) Dismiss(entity);
        return ended;
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
        Generation++;

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
            actor.Add(new StandingPose(placement.Pose));
            PlacePopulationEntity entity = new(actor, placement);
            _composer?.Compose(entity, place);
            live.Add(entity);
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
            _entities.Destroy(entity.Id);
        }

        _live = [];
        _lasting.Clear();
        Place = null;
    }
}
