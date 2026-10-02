using PartyRpg.Kit.World;
using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// The one owner of what the party has done to the targets of every place it has been in.
/// </summary>
/// <remarks>
/// <para>
/// State is keyed by the place and the content placement, exactly as the target's identity is: a door the
/// party opened stays open when it walks out and back, because the door is the place's and not the visit's.
/// Nothing here is keyed by a runtime entity, so leaving a place — which destroys the entities standing in
/// it — cannot lose what the party did there.
/// </para>
/// <para>
/// <b>A restored place forgets.</b> The world restoring a place's population restores the place, and a door
/// that was forced and a container that was emptied belong to the visit that did it; forgetting them here is
/// what makes a respawn mean the party finds the place as it was, rather than a ruin it left behind.
/// </para>
/// <para>
/// <b>A place keeps values of its own.</b> Beside each target's word, a place holds named whole numbers that
/// belong to the place rather than to any one target: a counter two levers of one room both read, or when a
/// timer of the place last ran. Every target of the place reads the same values and a use writes them through
/// its outcome, so one lever's pull is what the other lever finds. The names and what they mean are the
/// ruleset's; the kit keeps them, forgets them with the place, and hands them to a save.
/// </para>
/// <para>
/// <b>What a save carries.</b> The ledger is the one owner of per-place interaction state, and
/// <see cref="Capture"/> is its one durable reading. It carries each place's values, target words and incarnations, defeated placements, and remaining
/// personal purses. A resumed visit reads these memories before composing its population and targets.
/// </para>
/// </remarks>
public sealed class InteractionLedger
{
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, InteractionTargetState>> _places = [];
    private readonly Dictionary<PlaceId, SortedDictionary<string, long>> _values = [];
    private readonly Dictionary<PlaceId, HashSet<PlacementContentId>> _deaths = [];
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, PlacementPurseSnapshot>> _purses = [];

    /// <summary>Notifies owners whose projection depends on a place's target words or values.</summary>
    public event Action<PlaceId>? Changed;

    /// <summary>Creates an empty ledger: nothing has happened to any target of any place.</summary>
    public InteractionLedger()
    {
    }

    /// <summary>Rebuilds a ledger from what a save carried.</summary>
    /// <remarks>
    /// The snapshot is taken as the save states it: whether every place and value in it belongs to the world
    /// is judged before a load composes anything (<c>SessionSave.Problems</c>), so this does not judge again.
    /// </remarks>
    /// <param name="snapshot">The captured ledger.</param>
    /// <exception cref="ArgumentNullException">The snapshot is null.</exception>
    public InteractionLedger(InteractionLedgerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (PlaceInteractionSnapshot place in snapshot.Places)
        {
            SortedDictionary<string, long> values = new(StringComparer.Ordinal);
            foreach (PlaceValue value in place.Values) values[value.Key] = value.Value;
            if (values.Count > 0) _values[place.Place] = values;
            if (place.Targets.Count > 0) _places[place.Place] = place.Targets.ToDictionary(target => target.Target, target => new InteractionTargetState(target.State, target.Revision));
            if (place.Deaths.Count > 0) _deaths[place.Place] = [.. place.Deaths];
            if (place.Purses.Count > 0) _purses[place.Place] = place.Purses.ToDictionary(purse => purse.Target);
        }
    }

    /// <summary>Whether this placement was defeated before the place was last restored.</summary>
    public bool IsDefeated(PlaceId place, PlacementContentId target) => _deaths.TryGetValue(place, out var down) && down.Contains(target);

    /// <summary>Remembers one defeat for subsequent visits and a save.</summary>
    public void Defeated(PlaceId place, PlacementContentId target)
    {
        if (!_deaths.TryGetValue(place, out var down)) _deaths[place] = down = [];
        down.Add(target);
    }

    /// <summary>A placement's purse if it has already been drawn.</summary>
    public PlacementPurseSnapshot? PurseOf(PlaceId place, PlacementContentId target) =>
        _purses.TryGetValue(place, out var purses) ? purses.GetValueOrDefault(target) : null;

    /// <summary>Remembers what a placement still carries.</summary>
    public void KeepPurse(PlaceId place, PlacementPurseSnapshot purse)
    {
        if (!_purses.TryGetValue(place, out var purses)) _purses[place] = purses = [];
        purses[purse.Target] = purse;
    }

    /// <summary>
    /// What has happened to one target, or the unchanged state when nothing has, which is the honest answer
    /// for a target the party has never used.
    /// </summary>
    /// <param name="place">The place the target stands in.</param>
    /// <param name="content">The placement's identity in that place.</param>
    public InteractionTargetState StateOf(PlaceId place, PlacementContentId content) =>
        _places.TryGetValue(place, out Dictionary<PlacementContentId, InteractionTargetState>? targets) &&
        targets.TryGetValue(content, out InteractionTargetState state)
            ? state
            : InteractionTargetState.None;

    /// <summary>Records what a use made of one target, and returns its state afterwards.</summary>
    /// <remarks>
    /// Recording is what advances the target's incarnation, so a selection or an inspection that named the
    /// target before this call can no longer authorize a second use of it.
    /// </remarks>
    /// <param name="place">The place the target stands in.</param>
    /// <param name="content">The placement's identity in that place.</param>
    /// <param name="state">The state word the ruleset's outcome states, which must not be blank.</param>
    /// <returns>The target's state after the change.</returns>
    /// <exception cref="ArgumentException">The state word is blank, so nothing would be recorded.</exception>
    public InteractionTargetState Record(PlaceId place, PlacementContentId content, string state)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        if (!_places.TryGetValue(place, out Dictionary<PlacementContentId, InteractionTargetState>? targets))
        {
            targets = [];
            _places[place] = targets;
        }

        InteractionTargetState before = StateOf(place, content);
        InteractionTargetState next = before with { State = state, Revision = before.Revision + 1 };
        targets[content] = next;
        if (before.State != state) Changed?.Invoke(place);
        return next;
    }

    /// <summary>The values a place keeps, by name; empty for a place that keeps none.</summary>
    /// <param name="place">The place.</param>
    public IReadOnlyDictionary<string, long> ValuesOf(PlaceId place) =>
        _values.TryGetValue(place, out SortedDictionary<string, long>? values) ? values : Empty;

    /// <summary>Writes values a use left in a place, each replacing the one it names.</summary>
    /// <param name="place">The place the values belong to.</param>
    /// <param name="values">The values, by name.</param>
    /// <exception cref="ArgumentNullException">The values are null.</exception>
    /// <exception cref="ArgumentException">A name is blank, so nothing could read the value back.</exception>
    public void Keep(PlaceId place, IReadOnlyDictionary<string, long> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0) return;
        foreach (string key in values.Keys) ArgumentException.ThrowIfNullOrWhiteSpace(key, nameof(values));
        if (!_values.TryGetValue(place, out SortedDictionary<string, long>? kept))
        {
            kept = new SortedDictionary<string, long>(StringComparer.Ordinal);
            _values[place] = kept;
        }

        bool changed = false;
        foreach ((string key, long value) in values)
        {
            changed |= !kept.TryGetValue(key, out long before) || before != value;
            kept[key] = value;
        }
        if (changed) Changed?.Invoke(place);
    }

    /// <summary>
    /// Forgets everything the party did to a place's targets and every value the place kept, which is what a
    /// restore does to it.
    /// </summary>
    /// <param name="place">The place being restored.</param>
    public void Forget(PlaceId place)
    {
        bool changed = _places.ContainsKey(place) || _values.ContainsKey(place);
        _places.Remove(place);
        _values.Remove(place);
        _deaths.Remove(place);
        _purses.Remove(place);
        if (changed) Changed?.Invoke(place);
    }

    /// <summary>The ledger's durable reading, every place in identity order and its values by name.</summary>
    public InteractionLedgerSnapshot Capture() =>
        new([
            .. _values.Keys.Concat(_places.Keys).Concat(_deaths.Keys).Concat(_purses.Keys).Distinct()
                .OrderBy(place => place.Value, StringComparer.Ordinal)
                .Select(place => new PlaceInteractionSnapshot(place, [.. ValuesOf(place).Select(value => new PlaceValue(value.Key, value.Value))])
                {
                    Targets = _places.TryGetValue(place, out var targets)
                        ? [.. targets.OrderBy(target => target.Key.ToString(), StringComparer.Ordinal).Select(target => new PlacementStateSnapshot(target.Key, target.Value.State, target.Value.Revision))] : [],
                    Deaths = _deaths.TryGetValue(place, out var down) ? [.. down.OrderBy(target => target.ToString(), StringComparer.Ordinal)] : [],
                    Purses = _purses.TryGetValue(place, out var purses) ? [.. purses.Values.OrderBy(purse => purse.Target.ToString(), StringComparer.Ordinal)] : [],
                }),
        ]);

    private static readonly IReadOnlyDictionary<string, long> Empty = new Dictionary<string, long>();
}
