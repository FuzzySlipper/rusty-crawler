using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// What a place holds besides the content it declares: the bodies the party's own violence left, as
/// placements the mechanism can reach for.
/// </summary>
/// <remarks>
/// <para>
/// <b>A place's content cannot state this.</b> The world's placement list is read once, from the places a
/// session was built from, and a creature that goes down during a visit is not in it — no pack could declare
/// where a body will lie. The bodies therefore arrive beside the content, and they arrive as ordinary
/// placements: a body is the creature's own placement lying where it fell, so the reticle discovers it, the
/// ruleset describes it, its requirements are judged, and what it holds is transferred by the same workflow
/// every other target goes through. Nothing about searching a body is a second mechanism.
/// </para>
/// <para>
/// <b>Why the answer travels with the ruleset and not with the world.</b> The kit's mechanism is handed the
/// world it resolves uses against, and that world is the session's; the fight is handed the same world
/// through its own seam. The one composition point both halves already share is this game's ruleset — it
/// composes the fight, the world, and the interaction answers — so a body source that both halves can see
/// is stated there, as an answer the mechanism can ask for, rather than by widening the world's own contract
/// with state a world does not own.
/// </para>
/// </remarks>
public interface ICorpseSource
{
    /// <summary>Every body lying in one place right now, in the order it fell.</summary>
    /// <param name="place">The place to read.</param>
    /// <returns>The bodies, empty when nothing lies there.</returns>
    IReadOnlyList<PlacementDefinition> CorpsesOf(PlaceId place);
}
