using PartyRpg.Kit.Persistence;
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
/// afresh, and that is when this ground forgets every body it held. Saving carries the current visit's bodies,
/// search incarnations and held yields; resuming that visit restores them without reporting a new death or roll.
/// </para>
/// </remarks>
public sealed class CorpseGround
{
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, Corpse>> _places = [];
    private readonly Dictionary<long, LootYield> _held = [];
    private long _serials;

    /// <summary>The next death will be named after this already-used serial.</summary>
    internal long Serial => _serials;

    internal void Restore(PlaceId place, long serial, IReadOnlyList<CorpseSave> bodies, IReadOnlyDictionary<PlacementContentId, PlacementDefinition> content)
    {
        Repopulated();
        _serials = serial;
        foreach (CorpseSave saved in bodies)
        {
            Corpse body = new(place, content[saved.Placement] with { Pose = saved.Pose }, saved.Name, saved.Serial);
            if (!_places.TryGetValue(place, out var held)) _places[place] = held = [];
            held.Add(body.Content, body);
            if (saved.Held is { } loot) _held.Add(body.Serial, loot);
        }
    }

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

    /// <summary>
    /// Takes a body off the ground, with whatever its death held, because something stood it back up.
    /// </summary>
    /// <remarks>
    /// A body that got up is not lying there to be searched, and what it held goes with it rather than staying
    /// behind as a lot nobody can reach: the creature it became is what the place holds now.
    /// </remarks>
    /// <param name="body">The body.</param>
    /// <returns>Whether it was lying there.</returns>
    public bool Remove(Corpse body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!_places.TryGetValue(body.Place, out Dictionary<PlacementContentId, Corpse>? lying) ||
            !lying.TryGetValue(body.Content, out Corpse? held) || held.Serial != body.Serial)
        {
            return false;
        }

        lying.Remove(body.Content);
        _held.Remove(body.Serial);
        return true;
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
