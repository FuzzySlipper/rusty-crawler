using PartyRpg.Kit.Loot;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The one owner of what the creatures a fight brought down left: the body lying where it fell, and what
/// that death holds.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body is the fight's own reading, kept so something else can reach it.</b> A fight re-reads the place
/// every update and knows exactly which creatures are down and where they lie; nothing else does. This holds
/// that reading keyed by the place and the creature's placement, which is the same identity the interaction
/// mechanism discovers targets by, so a body is reachable by the mechanism that already serves a chest
/// rather than by a second path of its own.
/// </para>
/// <para>
/// <b>What a death left is decided once, when it falls.</b> A body's loot is generated the moment the fight
/// first reads the creature as down, under a key that names that death, and held here. Searching the body
/// hands over what it holds; nothing is rolled again, so the same body cannot yield two different lots and a
/// search the party could not carry away leaves the body exactly as full as it was. That is the donor's own
/// reading of a death — an actor's item and gold are settled when it dies
/// (<c>src/Engine/Objects/Actor.cpp:192-212</c>, <c>Actor::SetRandomGoldIfTheresNoItem</c>) — and it is why
/// this ground holds the loot rather than only the body.
/// </para>
/// <para>
/// <b>A body lasts for the visit.</b> It is an entity the place's population created, and those live exactly
/// as long as the party stands there: leaving destroys them, a restore rebuilds the place from content, and
/// a cleared place holds nobody until its interval elapses. A body therefore goes with the visit that made
/// it, and this ground forgets every other place the moment the fight reads a new one. That is the same rule
/// a creature's own health follows, and it is why nothing here is durable state: there is nothing to save
/// that a rebuilt place would not contradict.
/// </para>
/// <para>
/// <b>The ground is replaced, never accumulated.</b> Every observation states what the fight read as down in
/// the place it is in, whole, so a creature that is standing again — the world restored the place under the
/// party, or the party walked out and back in — cannot leave a body behind. A creature that is still down
/// keeps the serial it was given and the loot that death left, so the party finds the body it left standing
/// over rather than a new one.
/// </para>
/// </remarks>
public sealed class CorpseGround
{
    private readonly Dictionary<PlaceId, Dictionary<PlacementContentId, Corpse>> _places = [];
    private readonly Dictionary<long, LootYield> _held = [];
    private long _serials;

    /// <summary>States what a fight read as down in one place, and answers with the bodies lying there now.</summary>
    /// <remarks>
    /// The place is the one the fight stands in, and a body in any other place is forgotten here: the party
    /// left it, the entities of that visit are gone, and a body that outlived them would be a corpse standing
    /// where a living creature now walks. The answer is what the ground holds after the reading, so whoever
    /// generates a body's loot reads the serials this call just gave out rather than guessing at them.
    /// </remarks>
    /// <param name="place">The place the fight read.</param>
    /// <param name="fallen">Every creature it read as down there, in the order it read them.</param>
    /// <returns>The bodies lying in that place now, in the order the fight read them.</returns>
    /// <exception cref="ArgumentNullException">No list of the fallen was supplied.</exception>
    public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
    {
        ArgumentNullException.ThrowIfNull(fallen);

        // What was lying here before this reading is read before the ground is emptied, because a body that
        // is still down keeps the serial it was given — and with it the loot that death left.
        Dictionary<PlacementContentId, Corpse>? before =
            _places.TryGetValue(place, out Dictionary<PlacementContentId, Corpse>? held) ? held : null;
        _places.Clear();
        if (fallen.Count == 0)
        {
            _held.Clear();
            return [];
        }

        Dictionary<PlacementContentId, Corpse> lying = [];
        List<Corpse> bodies = [];
        foreach (FallenCreature creature in fallen)
        {
            Corpse body = before is not null && before.TryGetValue(creature.Placement.Content, out Corpse? known)
                ? known with { Body = creature.Placement with { Pose = creature.Fell }, Name = creature.Name }
                : new Corpse(place, creature.Placement with { Pose = creature.Fell }, creature.Name, ++_serials);
            lying[creature.Placement.Content] = body;
            bodies.Add(body);
        }

        // What the deaths that are gone left goes with them: a body nothing is lying at is a body the party
        // has left behind, and holding its loot would be holding the loot of a place this ground no longer
        // speaks about.
        foreach (long serial in _held.Keys.Where(serial => !bodies.Any(body => body.Serial == serial)).ToList())
        {
            _held.Remove(serial);
        }

        _places[place] = lying;
        return bodies;
    }

    /// <summary>
    /// The body lying at one placement, or null when nothing is down there.
    /// </summary>
    /// <remarks>
    /// This is the question a ruleset asks about a placement it is describing: a creature's placement reads
    /// as a living creature when no body lies at it, and as a body when one does. The answer is a reading of
    /// the fight's own last observation, so nothing has to be remembered twice.
    /// </remarks>
    /// <param name="place">The place the placement stands in.</param>
    /// <param name="content">The placement's identity in that place.</param>
    /// <returns>The body, or null when the placement is not down.</returns>
    public Corpse? At(PlaceId place, PlacementContentId content) =>
        _places.TryGetValue(place, out Dictionary<PlacementContentId, Corpse>? held) && held.TryGetValue(content, out Corpse? body)
            ? body
            : null;

    /// <summary>Every body lying in one place, in the order the fight read them.</summary>
    /// <param name="place">The place to read.</param>
    /// <returns>The bodies, empty when nothing is down there.</returns>
    public IReadOnlyList<Corpse> In(PlaceId place) =>
        _places.TryGetValue(place, out Dictionary<PlacementContentId, Corpse>? held) ? [.. held.Values] : [];

    /// <summary>What one death left, or null when nothing has said yet.</summary>
    /// <remarks>
    /// Nothing is rolled here: this is what the body holds, generated once by whoever owns generation. A body
    /// nothing has generated for holds nothing, which is the honest answer for a product that cannot draw.
    /// </remarks>
    /// <param name="body">The body to read.</param>
    /// <returns>What it holds, or null when nothing was generated for it.</returns>
    /// <exception cref="ArgumentNullException">No body was supplied.</exception>
    public LootYield? Held(Corpse body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return _held.GetValueOrDefault(body.Serial);
    }

    /// <summary>Records what one death left, once.</summary>
    /// <param name="body">The body the loot belongs to.</param>
    /// <param name="loot">What the death left.</param>
    /// <returns>Whether it was recorded; false when this body already held something.</returns>
    /// <exception cref="ArgumentNullException">No body or no loot was supplied.</exception>
    public bool Hold(Corpse body, LootYield loot)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(loot);
        return _held.TryAdd(body.Serial, loot);
    }
}
