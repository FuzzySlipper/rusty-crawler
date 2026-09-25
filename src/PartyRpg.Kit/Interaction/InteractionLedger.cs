using PartyRpg.Kit.World;

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
/// <b>This is live state and not durable state.</b> A save records the party, the clock, and what each place
/// remembers about being visited and cleared; where a target's state belongs in that document is the
/// persistence owner's decision, and until it carries one a resumed session finds every door as content
/// says it was.
/// </para>
/// </remarks>
public sealed class InteractionLedger
{
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, InteractionTargetState>> _places = [];

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
        return next;
    }

    /// <summary>Forgets everything the party did to a place's targets, which is what a restore does to it.</summary>
    /// <param name="place">The place being restored.</param>
    public void Forget(PlaceId place) => _places.Remove(place);
}
