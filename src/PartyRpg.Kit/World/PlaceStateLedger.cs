using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// The one owner of per-place runtime state: what the party has learned about each place, whether its
/// population is still there, and when the world last restored that population.
/// </summary>
/// <remarks>
/// <para>
/// The ledger is keyed by place and holds a place's state whether or not the party is standing in it,
/// which is what makes leaving and returning mean anything: the state is the place's, not the visit's,
/// and nothing here is scoped to the session's current mode.
/// </para>
/// <para>
/// <b>Time is supplied, never measured.</b> The session owns the game clock; this ledger only receives
/// the elapsed game day through <see cref="AdvanceTo"/> and <see cref="AdvanceBy"/>. It counts no
/// frames, starts no timer, and reads no wall clock, so a pause, a mode change, or a headless test all
/// move a place's population on exactly the same terms.
/// </para>
/// <para>
/// <b>A reset restores the population and nothing else.</b> Everything the party has learned — visited,
/// discovered — outlives every reset the place will ever have. That separation is the reason the two
/// families of state live in one record with different lifetimes, instead of in one flag that a reset
/// clears wholesale.
/// </para>
/// <para>
/// The ledger does not hold the population itself. It holds the fact the population's owner reads —
/// <see cref="PlaceState.Cleared"/> and <see cref="PlaceState.RespawnCount"/> — and <see cref="AdvanceTo"/>
/// reports which places were restored so that owner can rebuild them.
/// </para>
/// </remarks>
public sealed class PlaceStateLedger
{
    private readonly PlaceGraph _places;
    private readonly Dictionary<PlaceId, int?> _respawnDays = [];
    private readonly Dictionary<PlaceId, PlaceState> _states = [];

    /// <summary>Creates a ledger for a world, resolving every place's reset interval from content.</summary>
    /// <remarks>
    /// Intervals are resolved once, here, for every place in the world rather than lazily on the day the
    /// party wanders in: a content defect then fails while the world is being built, and the schedule a
    /// place resets on cannot change halfway through a session.
    /// </remarks>
    /// <param name="places">The world's places, which are what state is keyed by.</param>
    /// <param name="respawn">Where each place's reset interval comes from.</param>
    public PlaceStateLedger(PlaceGraph places, PlaceRespawnRule respawn)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(respawn);
        _places = places;

        List<ContentValidationIssue> issues = [];
        foreach (PlaceDefinition place in places.Places)
        {
            try
            {
                _respawnDays[place.Id] = respawn.DaysFor(place);
            }
            catch (ContentValidationException error)
            {
                // A bad interval is content's problem, not this place's alone: keep resolving the rest of
                // the world so one failure reports every defective interval instead of only the first.
                issues.AddRange(error.Issues);
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world's respawn schedule cannot be built: {issues[0].Message}",
                issues);
        }
    }

    /// <summary>
    /// The game day the world has reached, counted in whole days elapsed since the session began, the
    /// session's first day being day zero.
    /// </summary>
    /// <remarks>
    /// This is the last time the session reported, kept so that <see cref="AdvanceBy"/> and
    /// <see cref="Capture"/> have a position to work from. It is a record of what the session said, not
    /// a clock: the ledger never moves it on its own.
    /// </remarks>
    public int ElapsedGameDays { get; private set; }

    /// <summary>Every place the ledger holds state for, in the order content declares its places.</summary>
    public IReadOnlyList<PlaceState> States
    {
        get
        {
            List<PlaceState> states = [];
            foreach (PlaceDefinition place in _places.Places)
            {
                if (_states.TryGetValue(place.Id, out PlaceState? state)) states.Add(state);
            }

            return states;
        }
    }

    /// <summary>The state of a place, or null when the ledger holds none for it.</summary>
    /// <param name="place">The place to read.</param>
    public PlaceState? Find(PlaceId place)
    {
        RequireKnown(place);
        return _states.GetValueOrDefault(place);
    }

    /// <summary>The state of a place, read as an untouched place when the ledger holds none for it.</summary>
    /// <param name="place">The place to read.</param>
    public PlaceState StateOf(PlaceId place)
    {
        RequireKnown(place);
        return _states.GetValueOrDefault(place) ?? PlaceState.Untouched(place);
    }

    /// <summary>Records that the party has been in a place, and returns its state afterwards.</summary>
    /// <remarks>
    /// Being in a place is the strongest way to know it exists, so a visit also marks it discovered. The
    /// first visit is also where a place's population schedule starts: before that the party has never
    /// seen the place, so there is no population the ledger has to keep fresh, and starting the interval
    /// at the session's first day instead would have the world restore places nobody has been to.
    /// A later visit changes nothing else — a place the party has cleared stays cleared until its
    /// interval elapses.
    /// </remarks>
    /// <param name="place">The place the party is in.</param>
    public PlaceState MarkVisited(PlaceId place)
    {
        RequireKnown(place);
        PlaceState state = Existing(place);
        return Store(state with
        {
            Visited = true,
            Discovered = true,
            LastResetDay = state.LastResetDay ?? ElapsedGameDays,
        });
    }

    /// <summary>Records that the party knows a place exists without having been there.</summary>
    /// <remarks>
    /// A rumor, a map, or a road sign teaches a place's name, and that knowledge has to survive the
    /// party never going there. Discovery alone starts no population schedule: nothing about the place
    /// has been touched, so there is nothing to restore.
    /// </remarks>
    /// <param name="place">The place the party has learned of.</param>
    public PlaceState MarkDiscovered(PlaceId place)
    {
        RequireKnown(place);
        return Store(Existing(place) with { Discovered = true });
    }

    /// <summary>Records that the party has emptied a place of its population.</summary>
    /// <param name="place">The place the party has cleared.</param>
    public PlaceState MarkCleared(PlaceId place)
    {
        RequireKnown(place);
        if (!_states.TryGetValue(place, out PlaceState? state))
        {
            throw new InvalidOperationException(
                $"Place '{place}' has no state to clear: the party has never been there, and a cleared place nobody has entered would claim knowledge the party does not have.");
        }

        return Store(state with { Cleared = true });
    }

    /// <summary>
    /// Moves the world to an elapsed game day, restoring the population of every place whose interval
    /// has elapsed, and returns those places with their state afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Respawn is a deadline, not an event counted by visits: a place restores when game time has passed,
    /// whether the party is standing in it, somewhere else, or has not been there since the day it was
    /// cleared. A place the party never entered is on no schedule and is left alone.
    /// </para>
    /// <para>
    /// A place whose interval has elapsed many times over resets once. The party finds one fresh
    /// population, and the mark moves to the day the reset actually happened, so the next interval is
    /// measured from it rather than from a deadline that was already missed.
    /// </para>
    /// <para>
    /// An advance that does not move the world forward in time does nothing at all, which also keeps a
    /// place whose content declares a zero-day interval from resetting once per update: such a place's
    /// population never lasts into the next day, and a day is the smallest step its schedule knows.
    /// </para>
    /// </remarks>
    /// <param name="elapsedGameDays">Whole game days elapsed since the session began.</param>
    public IReadOnlyList<PlaceState> AdvanceTo(int elapsedGameDays)
    {
        if (elapsedGameDays < ElapsedGameDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(elapsedGameDays),
                elapsedGameDays,
                $"Game time runs forward, and the world has already reached day {ElapsedGameDays}.");
        }

        List<PlaceState> restored = [];
        if (elapsedGameDays == ElapsedGameDays) return restored;

        foreach (PlaceDefinition place in _places.Places)
        {
            if (!_states.TryGetValue(place.Id, out PlaceState? state)) continue;
            if (state.LastResetDay is not { } lastResetDay) continue;
            if (_respawnDays[place.Id] is not { } intervalDays) continue;
            if (elapsedGameDays - lastResetDay < intervalDays) continue;

            restored.Add(Store(state with
            {
                Cleared = false,
                RespawnCount = state.RespawnCount + 1,
                LastResetDay = elapsedGameDays,
            }));
        }

        ElapsedGameDays = elapsedGameDays;
        return restored;
    }

    /// <summary>
    /// Moves the world forward by a number of game days, restoring populations as
    /// <see cref="AdvanceTo"/> does.
    /// </summary>
    /// <remarks>
    /// Discrete advancement is what rest, camping, and travel do to the clock, and each of them reaches
    /// the ledger through this call rather than through a loop of its own.
    /// </remarks>
    /// <param name="days">Whole game days to advance by, which may be zero.</param>
    public IReadOnlyList<PlaceState> AdvanceBy(int days)
    {
        if (days < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "Game time runs forward, so an advance is a count of days forward.");
        }

        if (days > int.MaxValue - ElapsedGameDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "That advance would carry the world past the last day its clock can name.");
        }

        return AdvanceTo(ElapsedGameDays + days);
    }

    /// <summary>Captures the whole ledger as data, for a save to carry and a load to rebuild from.</summary>
    public PlaceStateLedgerSnapshot Capture() => new(ElapsedGameDays, States);

    /// <summary>Rebuilds a ledger from a captured snapshot over the world it belongs to.</summary>
    /// <remarks>
    /// A snapshot that names a place this world does not have, that holds two states for one place, or
    /// that was captured after a day it claims to have reached cannot describe this world and is refused
    /// rather than rounded into something loadable. There is one current save schema, so a snapshot that
    /// does not fit is a defect to fix, not a version to migrate.
    /// </remarks>
    /// <param name="places">The world's places, which the snapshot's states must belong to.</param>
    /// <param name="respawn">Where each place's reset interval comes from.</param>
    /// <param name="snapshot">The captured ledger to rebuild.</param>
    public static PlaceStateLedger Restore(PlaceGraph places, PlaceRespawnRule respawn, PlaceStateLedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        PlaceStateLedger ledger = new(places, respawn);
        if (snapshot.ElapsedGameDays < 0)
        {
            throw new InvalidOperationException(
                $"The captured ledger reached day {snapshot.ElapsedGameDays}, which is before the session it belongs to began.");
        }

        foreach (PlaceState state in snapshot.States)
        {
            places.Require(state.Place);
            if (state.LastResetDay is { } lastResetDay && lastResetDay > snapshot.ElapsedGameDays)
            {
                throw new InvalidOperationException(
                    $"Place '{state.Place}' was last restored on day {lastResetDay}, after the day {snapshot.ElapsedGameDays} the captured ledger had reached.");
            }

            if (!ledger._states.TryAdd(state.Place, state))
            {
                throw new InvalidOperationException($"The captured ledger holds two states for place '{state.Place}'.");
            }
        }

        ledger.ElapsedGameDays = snapshot.ElapsedGameDays;
        return ledger;
    }

    /// <summary>The ledger's state for a place, or an untouched place when it holds none.</summary>
    private PlaceState Existing(PlaceId place) => _states.GetValueOrDefault(place) ?? PlaceState.Untouched(place);

    /// <summary>Writes a state back and hands the same value to the caller.</summary>
    private PlaceState Store(PlaceState state)
    {
        _states[state.Place] = state;
        return state;
    }

    /// <summary>
    /// Refuses an id the world does not have, so state can never be created for a place no content
    /// declares and a mistyped id fails by name instead of becoming an unvisited place of its own.
    /// </summary>
    private void RequireKnown(PlaceId place) => _places.Require(place);
}
