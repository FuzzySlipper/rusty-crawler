using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>What the ruleset is asked about one placement the party can reach.</summary>
/// <remarks>
/// The placement is handed over whole, with its entry, exactly as the world's own placements are: the fields
/// this layer has no opinion about are content's, and a ruleset that needs a door's stored state or a
/// decoration's event reads them from the entry rather than from a vocabulary the kit invented for them.
/// What has already happened to the target travels beside it as <paramref name="State"/>, so a rule can
/// answer differently about a door it has already opened.
/// </remarks>
/// <param name="Place">The place the placement stands in.</param>
/// <param name="Placement">The placement content declares.</param>
/// <param name="State">
/// What the party has already done to this target, as the word the ruleset last recorded for it; empty when
/// nothing has happened to it yet, which is also when content's own state is what the target is.
/// </param>
public readonly record struct InteractionTargetRequest(PlaceId Place, PlacementDefinition Placement, string State)
{
    /// <summary>
    /// What a person's word raised at the placement, as the ruleset's own name for it, or empty for a use the party
    /// made itself (<see cref="PartyInteraction.Answer"/>).
    /// </summary>
    /// <remarks>
    /// A person's word can set something going that the party never aimed at — the ruleset's answer to the topic it
    /// was asked — and it is run as one use of the placement the person stands at, so what it changes is settled by the
    /// owners every other use settles through. The word is the ruleset's, carried back to it unread.
    /// </remarks>
    public string Raised { get; init; } = string.Empty;
}

/// <summary>What one granted use is resolved against: the target, its state, and the party that uses it.</summary>
/// <remarks>
/// <para>
/// The party and the clock are handed over whole rather than as the answers to questions the kit thought a
/// rule would ask. A requirement's meaning is the ruleset's — whether a key is carried, which member's skill
/// counts, what a flag is, and what time of day means — so a rule reads the owners it needs instead of the
/// kit guessing at one shape for every game.
/// </para>
/// <para>
/// Both are optional because a world can exist without either: a place with no scenario party is still a
/// place whose doors can be opened, and a session whose ruleset composed no clock has no time of day. A
/// requirement that needs one of them is then unmet, named by the rule that could not answer it, rather than
/// satisfied by an invented fact.
/// </para>
/// </remarks>
/// <param name="Place">The place the target stands in.</param>
/// <param name="Placement">
/// The placement content declared. It travels with the definition because a definition restates only what
/// every target of its kind shares, while the fields a particular placement carries — a door's stored
/// position, a fixture's event — are read from the entry rather than copied into a vocabulary here.
/// </param>
/// <param name="Target">
/// The definition the ruleset gave the target, including the word it currently reads as: a use that changes
/// a target answers from the same answer it was described by, rather than from a second copy of its state.
/// </param>
/// <param name="Party">The party the use is made by, or null when the world holds none.</param>
/// <param name="Clock">The session's one clock, or null when its ruleset composed none.</param>
public sealed record InteractionContext(
    PlaceId Place,
    PlacementDefinition Placement,
    InteractionTargetDefinition Target,
    PartyEntity? Party,
    GameClock? Clock)
{
    /// <summary>
    /// The values the target's place keeps, by the ruleset's own names; empty when the place keeps none.
    /// </summary>
    /// <remarks>
    /// Every target of a place reads the same values, which is what lets two of them share one: a use that
    /// changes one states it in its outcome (<see cref="InteractionOutcome.Kept"/>), and the mechanism writes
    /// it into the ledger only when the use is applied.
    /// </remarks>
    public IReadOnlyDictionary<string, long> PlaceValues { get; init; } = new Dictionary<string, long>();

    /// <summary>What the target's place holds, in content order, which a use that changes another target reads.</summary>
    public IReadOnlyList<PlacementDefinition> PlaceTargets { get; init; } = [];

    /// <summary>
    /// The state word the party has left another target of the place in, or empty when nothing has happened to it.
    /// </summary>
    public Func<PlacementContentId, string> TargetState { get; init; } = static _ => string.Empty;

    /// <summary>
    /// The transitions the place issues, which a use that leads somewhere names its journey from; empty when the
    /// world issues none from here.
    /// </summary>
    /// <remarks>
    /// A rule names the way a use leads by the transition content declared, and judges it before anything is
    /// settled: a use whose way the place does not issue is refused rather than settled and then left standing.
    /// </remarks>
    public IReadOnlyList<PlaceTransition> PlaceTransitions { get; init; } = [];

    /// <summary>
    /// What a person's word raised, when the use is one (<see cref="PartyInteraction.Answer"/>), or empty for a use the
    /// party made itself: the same word the target was described from (<see cref="InteractionTargetRequest.Raised"/>).
    /// </summary>
    public string Raised { get; init; } = string.Empty;
}
