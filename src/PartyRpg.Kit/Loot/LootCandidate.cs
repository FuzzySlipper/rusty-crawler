using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Loot;

/// <summary>Which of a treasure level's things may be drawn, as content tags rather than as a game's words.</summary>
/// <remarks>
/// <para>
/// A treasure request that names what it wants — a weapon, a cloak, a scroll — narrows the pool it is drawn
/// from. The narrowing is stated here as two opaque tags because the mechanism has no opinion about what any
/// of them mean: the tags are content's own, the candidate carries its own, and a request whose tag is empty
/// accepts everything. A game that names its kinds differently changes its content, not this type.
/// </para>
/// <para>
/// Two tags rather than one because a thing can be wanted by its shape or by the skill it is used with — a
/// sword is a one-handed weapon and a sword — and a game states which of the two it means per request.
/// </para>
/// </remarks>
/// <param name="Kind">The kind of thing the request wants, empty when any kind will do.</param>
/// <param name="Skill">The skill the thing is used with, empty when any skill will do.</param>
public readonly record struct LootFilter(string Kind, string Skill)
{
    /// <summary>A request that accepts anything the level offers.</summary>
    public static LootFilter Any => new(string.Empty, string.Empty);

    /// <summary>Whether one candidate is what this request asks for.</summary>
    /// <param name="candidate">The candidate to judge.</param>
    /// <returns>Whether it may be drawn.</returns>
    /// <exception cref="ArgumentNullException">No candidate was supplied.</exception>
    public bool Accepts(LootCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return (Kind.Length == 0 || string.Equals(Kind, candidate.Kind, StringComparison.Ordinal)) &&
               (Skill.Length == 0 || string.Equals(Skill, candidate.Skill, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public override string ToString() => Kind.Length == 0 && Skill.Length == 0
        ? "any"
        : string.Join('/', new[] { Kind, Skill }.Where(tag => tag.Length > 0));
}

/// <summary>One thing a treasure level can yield, with how likely content says it is at each level.</summary>
/// <remarks>
/// <para>
/// The weights are content's own, one per treasure level from the first: the table's rows weigh each item by
/// how often it may appear at each level, and a level whose weight is zero never yields that item. That is
/// the whole of what "a random item of level <em>n</em>" means — a weighted draw from the things the level
/// weighs at all.
/// </para>
/// <para>
/// The candidate carries the tags a filter matches against, and nothing else: what the thing is worth, what
/// it is called, and what it does are content's, read by whoever wants them from the definition the
/// candidate names.
/// </para>
/// </remarks>
/// <param name="Definition">The content definition this candidate is a copy of.</param>
/// <param name="Weights">How likely it is at each treasure level, the first level first.</param>
/// <param name="Kind">The kind of thing it is, as content tags it, empty when content says nothing.</param>
/// <param name="Skill">The skill it is used with, as content tags it, empty when content says nothing.</param>
public sealed record LootCandidate(ItemDefinitionId Definition, IReadOnlyList<int> Weights, string Kind = "", string Skill = "")
{
    /// <summary>How likely this candidate is at one treasure level, nothing when the level does not reach it.</summary>
    /// <param name="level">The treasure level, counted from one.</param>
    /// <returns>The weight, zero when the item is not drawn at that level.</returns>
    public int WeightAt(int level) => level >= 1 && level <= Weights.Count ? Math.Max(0, Weights[level - 1]) : 0;

    /// <inheritdoc />
    public override string ToString() => $"{Definition} ({Kind}{(Skill.Length > 0 ? "/" + Skill : string.Empty)})";
}
