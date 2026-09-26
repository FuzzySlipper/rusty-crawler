using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Promotion;

/// <summary>One rank a class leads to: what it is called, who it comes from, and what it asks for.</summary>
/// <remarks>
/// <para>
/// A rank row is content's or a ruleset's statement about one promotion, and this type carries it without
/// deciding any of it. <see cref="From"/> is the class a member must stand in and <see cref="To"/> is the
/// class the rank names, because in this game family a promotion <em>is</em> a change of class: the rank a
/// character holds, the ceiling its skills may reach, and the growth a level gives are all read from the
/// class it now belongs to, which is what makes a promotion one fact rather than three that could drift.
/// </para>
/// <para>
/// <b><see cref="Choice"/> is what a second promotion splits on.</b> A rank whose ladder offers alternatives
/// carries the identity of the alternative it takes — the ruleset's own word for it — and a first promotion
/// carries none. The identity is never interpreted here: which alternatives exist, what each opens, and what
/// each closes are the ladder's business, and this mechanism only records which one was taken.
/// </para>
/// <para>
/// <b><see cref="Award"/> is the record the promotion leaves behind.</b> A game that marks an earned rank
/// with a deed of record states that record's identity here, and this mechanism writes it onto the party
/// when the rank lands; a game that marks nothing leaves it empty. It is how a rank can be asked for later —
/// by another rank's requirement, by a person's topic, or by a caller reading what the party has done.
/// </para>
/// <para>
/// <see cref="Words"/> is the giver's own line when the rank is taken, authored by the game rather than
/// composed here: a promotion is given by a person, and what that person says about it is their content.
/// </para>
/// </remarks>
/// <param name="Id">The rank's identity in the ladder, which must not be blank.</param>
/// <param name="From">The class a member must stand in for this rank to be given.</param>
/// <param name="To">The class the rank names, which the member belongs to once it lands.</param>
/// <param name="Rank">The rank in the class ladder this promotion reaches, which is at least two.</param>
/// <param name="Requirements">What the rank asks for; empty when it asks for nothing but its giver.</param>
/// <param name="Choice">The alternative this rank takes, or empty when it is not a split of two.</param>
/// <param name="Award">The record the rank leaves on the party, or empty when it leaves none.</param>
/// <param name="Words">What the giver says when the rank is taken, or empty when they say nothing.</param>
/// <exception cref="ArgumentException">The identity is blank.</exception>
/// <exception cref="ArgumentOutOfRangeException">The rank is below two, which no promotion reaches.</exception>
public sealed record PromotionRank
{
    /// <summary>Creates a rank row.</summary>
    /// <param name="id">The rank's identity in the ladder.</param>
    /// <param name="from">The class a member must stand in.</param>
    /// <param name="to">The class the rank names.</param>
    /// <param name="rank">The rank this promotion reaches, which is at least two.</param>
    /// <param name="requirements">What the rank asks for; empty when it asks for nothing.</param>
    /// <param name="choice">The alternative this rank takes, or empty when it is not a split of two.</param>
    /// <param name="award">The record the rank leaves on the party, or empty when it leaves none.</param>
    /// <param name="words">What the giver says when the rank is taken, or empty when they say nothing.</param>
    /// <exception cref="ArgumentException">The identity is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The rank is below two, which no promotion reaches.</exception>
    public PromotionRank(
        string id,
        ClassId from,
        ClassId to,
        int rank,
        IReadOnlyList<PromotionRequirement>? requirements = null,
        string? choice = null,
        string? award = null,
        string? words = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(rank, 2);
        Id = id;
        From = from;
        To = to;
        Rank = rank;
        Requirements = requirements ?? [];
        Choice = choice ?? string.Empty;
        Award = award ?? string.Empty;
        Words = words ?? string.Empty;
    }

    /// <summary>The rank's identity in the ladder.</summary>
    public string Id { get; init; }

    /// <summary>The class a member must stand in for this rank to be given.</summary>
    public ClassId From { get; init; }

    /// <summary>The class the rank names.</summary>
    public ClassId To { get; init; }

    /// <summary>The rank in the class ladder this promotion reaches.</summary>
    public int Rank { get; init; }

    /// <summary>What the rank asks for.</summary>
    public IReadOnlyList<PromotionRequirement> Requirements { get; init; }

    /// <summary>The alternative this rank takes, or empty when it is not a split of two.</summary>
    public string Choice { get; init; }

    /// <summary>The record the rank leaves on the party, or empty when it leaves none.</summary>
    public string Award { get; init; }

    /// <summary>What the giver says when the rank is taken, or empty when they say nothing.</summary>
    public string Words { get; init; }

    /// <summary>The person this rank must be taken from, or empty when the ladder names nobody.</summary>
    public string Giver
    {
        get
        {
            foreach (PromotionRequirement requirement in Requirements)
            {
                if (requirement.Kind == PromotionRequirementKind.Giver) return requirement.Name;
            }

            return string.Empty;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{From} -> {To} (rank {Rank})";
}

/// <summary>Every rank a game's classes lead to, as its ruleset states it.</summary>
/// <remarks>
/// <para>
/// The ladder is one table and no logic: which classes promote to which, what each rank asks for, and which
/// alternative each second promotion takes. Two lookups are what the mechanism needs from it — the ranks one
/// class leads to, and the ranks one person gives — and both are answered here so that a session, a
/// conversation, and a panel read the same table rather than three readings of it.
/// </para>
/// <para>
/// <b>A ladder does not have to be complete, and a class at its top simply has no rows.</b> That is what
/// makes a second promotion irreversible without a flag anywhere: a member who has taken one alternative
/// stands in a class the ladder gives no further ranks for, and the other alternative no longer names the
/// class the member is in. A ladder that named rows for a top class would be a game whose ranks never end,
/// which is a state this type can express and no ruleset has to.
/// </para>
/// </remarks>
public sealed class PromotionLadder
{
    private readonly IReadOnlyList<PromotionRank> _ranks;

    /// <summary>Creates a ladder over the ranks a game states.</summary>
    /// <param name="ranks">Every rank row, in the order the game states them.</param>
    /// <exception cref="ArgumentNullException">No ranks were supplied.</exception>
    /// <exception cref="ArgumentException">Two rows carry one identity, which would leave one of them unreachable.</exception>
    public PromotionLadder(IReadOnlyList<PromotionRank> ranks)
    {
        ArgumentNullException.ThrowIfNull(ranks);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (PromotionRank rank in ranks)
        {
            if (!seen.Add(rank.Id))
            {
                throw new ArgumentException(
                    $"The promotion '{rank.Id}' is stated more than once, so which rank a member rose to would depend on which row was found first.",
                    nameof(ranks));
            }
        }

        _ranks = [.. ranks];
    }

    /// <summary>An empty ladder: no class leads anywhere, and a member may never be promoted.</summary>
    public static PromotionLadder None { get; } = new([]);

    /// <summary>Every rank row, in the order the game stated them.</summary>
    public IReadOnlyList<PromotionRank> Ranks => _ranks;

    /// <summary>One rank by the identity content and a conversation name it, or null when the ladder lacks it.</summary>
    /// <param name="id">The rank's identity.</param>
    /// <returns>The rank row, or null.</returns>
    public PromotionRank? Rank(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (PromotionRank rank in _ranks)
        {
            if (string.Equals(rank.Id, id, StringComparison.Ordinal)) return rank;
        }

        return null;
    }

    /// <summary>Every rank a member of one class may be promoted to, in the ladder's own order.</summary>
    /// <remarks>
    /// A member's class is read from its own profile, so the two alternatives of a second promotion are the
    /// two rows whose <see cref="PromotionRank.From"/> is the class a first promotion left the member in:
    /// choosing one is what removes the other, which is why nothing here needs to remember a choice.
    /// </remarks>
    /// <param name="memberClass">The class the member belongs to.</param>
    /// <returns>The ranks that class leads to, which is empty at the top of a ladder.</returns>
    public IReadOnlyList<PromotionRank> From(ClassId memberClass)
    {
        List<PromotionRank> rows = [];
        foreach (PromotionRank rank in _ranks)
        {
            if (string.Equals(rank.From.Value, memberClass.Value, StringComparison.Ordinal)) rows.Add(rank);
        }

        return rows;
    }

    /// <summary>Every rank one person gives, in the ladder's own order.</summary>
    /// <param name="giver">The person's identity among the people the world carries.</param>
    /// <returns>The ranks this person hands out, which is empty for everybody who is not a giver.</returns>
    public IReadOnlyList<PromotionRank> GivenBy(string giver)
    {
        if (string.IsNullOrEmpty(giver)) return [];
        List<PromotionRank> rows = [];
        foreach (PromotionRank rank in _ranks)
        {
            if (string.Equals(rank.Giver, giver, StringComparison.Ordinal)) rows.Add(rank);
        }

        return rows;
    }

    /// <summary>Whether one class is the top of its ladder, so no rank leads out of it.</summary>
    /// <param name="memberClass">The class to ask about.</param>
    /// <returns>Whether the ladder states no rank this class may be promoted to.</returns>
    public bool IsTop(ClassId memberClass) => From(memberClass).Count == 0;
}
