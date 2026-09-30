using PartyRpg.Kit.Loot;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The one owner of what the creatures a fight brought down left: the body lying where it fell, and what
/// that death holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body is laid when its creature dies.</b> The death is reported once, from the blow that caused it, and
/// the body is laid then — keyed by the place and the creature's placement, which is the identity the
/// interaction mechanism discovers targets by, so a body is reachable by the mechanism that already serves a
/// chest rather than by a second path of its own. Nothing re-reads the place to find it.
/// </para>
/// <para>
/// <b>What a death left is decided once, when it falls.</b> Whoever rolls a body's loot holds it here under the
/// body's serial. Searching the body hands over what it holds; nothing is rolled again, so the same body cannot
/// yield two different lots and a search the party could not carry away leaves the body exactly as full as it
/// was.
/// </para>
/// <para>
/// <b>A body lasts for the visit.</b> It is an entity the place's population created, and those live exactly
/// as long as the population does: walking into a place and the clock restoring one both build the population
/// afresh, and that is when this ground forgets every body it held. Nothing here is durable state: there is
/// nothing to save that a rebuilt place would not contradict.
/// </para>
/// </remarks>
public sealed class CorpseGround
{
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, Corpse>> _places = [];
    private readonly Dictionary<long, LootYield> _held = [];
    private long _serials;

    /// <summary>Lays the body a death left, where the creature fell.</summary>
    /// <param name="death">The death.</param>
    /// <returns>The body, with the serial that names this death.</returns>
    public Corpse Lay(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);
        Corpse body = new(death.Place, death.Placement, death.Name, ++_serials);
        if (!_places.TryGetValue(death.Place, out Dictionary<PlacementContentId, Corpse>? lying))
        {
            lying = [];
            _places[death.Place] = lying;
        }

        lying[body.Content] = body;
        return body;
    }

    /// <summary>
    /// Forgets every body and what each held, because a place's population was built afresh and the visit the
    /// bodies belonged to is over.
    /// </summary>
    public void Repopulated()
    {
        _places.Clear();
        _held.Clear();
    }

    /// <summary>The body lying at a placement, or null when nothing lies there.</summary>
    /// <param name="place">The place to look in.</param>
    /// <param name="content">The placement the creature was.</param>
    public Corpse? At(PlaceId place, PlacementContentId content) =>
        _places.TryGetValue(place, out Dictionary<PlacementContentId, Corpse>? held) && held.TryGetValue(content, out Corpse? body)
            ? body
            : null;

    /// <summary>Every body lying in a place, in the order they fell.</summary>
    /// <param name="place">The place to read.</param>
    public IReadOnlyList<Corpse> In(PlaceId place) =>
        _places.TryGetValue(place, out Dictionary<PlacementContentId, Corpse>? held) ? [.. held.Values] : [];

    /// <summary>What a body holds, or null when nothing was ever held for it.</summary>
    /// <param name="body">The body.</param>
    public LootYield? Held(Corpse body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return _held.GetValueOrDefault(body.Serial);
    }

    /// <summary>Holds what a body's death left, once.</summary>
    /// <param name="body">The body.</param>
    /// <param name="loot">What it holds.</param>
    /// <returns>Whether it was held, which is false when the body already holds something.</returns>
    public bool Hold(Corpse body, LootYield loot)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(loot);
        return _held.TryAdd(body.Serial, loot);
    }
}
