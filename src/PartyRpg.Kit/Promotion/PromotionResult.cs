using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Promotion;

/// <summary>The owner a promotion offer is handed to, as the word a conversation and a session share.</summary>
/// <remarks>
/// <para>
/// A person who is empowered to grant a rank does not raise anybody themselves: they offer the rank, and the
/// mechanism that owns progression takes it from there. That is what a handoff is — a topic naming the owner
/// it belongs to and the rank it means — so a conversation never grows a second copy of what the progression
/// owner already does.
/// </para>
/// <para>
/// The word is named here rather than in the conversation's own list of kinds because it is this owner's
/// word: the conversation names the owners it knows about, and an owner added later brings its own, exactly
/// as the conversation's list says a later mechanism would.
/// </para>
/// </remarks>
public static class PromotionHandoffs
{
    /// <summary>The progression owner, taking the rank a conversation offered.</summary>
    public const string Offer = "promotion";
}

/// <summary>One requirement as it stands against the party: what was asked, whether it holds, and how it reads.</summary>
/// <remarks>
/// A verdict is a reading and not a change: the same judgement is what a refusal lists and what a panel
/// shows, so "the rank asks for this" and "the party does not have it" are one answer rather than two that
/// could disagree. The statement is composed where the state is read, because the owner is what knows
/// whether an item is a count or a record is a magnitude.
/// </remarks>
/// <param name="Requirement">What the rank asked for.</param>
/// <param name="IsMet">Whether the party holds what it asked for.</param>
/// <param name="Statement">How it reads to a person, with the party's own standing in it.</param>
public readonly record struct PromotionRequirementVerdict(
    PromotionRequirement Requirement,
    bool IsMet,
    string Statement);

/// <summary>One member a rank was given to, and what they met to take it.</summary>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="FromClass">The class the member stood in.</param>
/// <param name="FromRank">The rank the member stood at.</param>
/// <param name="ToClass">The class the member now belongs to.</param>
/// <param name="Rank">The rank the member now holds.</param>
/// <param name="Choice">The alternative the rank took, or empty when it is not a split of two.</param>
/// <param name="Met">What the rank asked for and the member met, in the order the ladder stated it.</param>
public sealed record PromotionGrant(
    PartyMemberId Member,
    string Name,
    string FromClass,
    int FromRank,
    string ToClass,
    int Rank,
    string Choice,
    IReadOnlyList<string> Met);

/// <summary>One member a rank was not given to, and what they were missing.</summary>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Class">The class the member stands in.</param>
/// <param name="Rank">The rank the member stands at.</param>
/// <param name="Missing">What the rank asked for and the member did not have, in the order the ladder stated it.</param>
public sealed record PromotionDenial(
    PartyMemberId Member,
    string Name,
    string Class,
    int Rank,
    IReadOnlyList<string> Missing);

/// <summary>What one rank did: who rose and what they met, who could not and what was missing, or why nobody did.</summary>
/// <remarks>
/// <para>
/// A rank is given to every member of the class it promotes from, which is what a giver's own line promises
/// when it says it will make the party's <em>members</em> of a class into the rank above — so a promotion is
/// per member even though it is offered once, and the report says what became of each of them. A member who
/// meets the requirements rises; one who does not is named with what they were missing, and the promotion is
/// <em>refused for that member</em> without being refused for the others.
/// </para>
/// <para>
/// <see cref="Refusal"/> is the other case: nothing at all happened, because the rank is not one this class
/// leads to, because it is not this person's to give, or because nobody in the party stands in the class it
/// promotes from. A caller reads <see cref="IsGranted"/> and, when nothing rose, the refusal's own code and
/// sentence rather than the absence of grants.
/// </para>
/// </remarks>
/// <param name="Promotion">The rank's identity in the ladder.</param>
/// <param name="FromClass">The class the rank promotes from.</param>
/// <param name="ToClass">The class the rank names.</param>
/// <param name="Rank">The rank the promotion reaches.</param>
/// <param name="Choice">The alternative the rank takes, or empty when it is not a split of two.</param>
/// <param name="Granted">Who rose, in the party's own order.</param>
/// <param name="Denied">Who did not and what they were missing, in the party's own order.</param>
/// <param name="Refusal">Why nobody rose at all, or null when at least one member did.</param>
public sealed record PromotionResult(
    string Promotion,
    string FromClass,
    string ToClass,
    int Rank,
    string Choice,
    IReadOnlyList<PromotionGrant> Granted,
    IReadOnlyList<PromotionDenial> Denied,
    PartyRefusal? Refusal)
{
    /// <summary>Whether at least one member rose to the rank.</summary>
    public bool IsGranted => Granted.Count > 0;

    /// <summary>A rank nobody took, with the party exactly where it stood.</summary>
    /// <param name="promotion">The rank's identity.</param>
    /// <param name="fromClass">The class the rank promotes from.</param>
    /// <param name="toClass">The class the rank names.</param>
    /// <param name="rank">The rank the promotion reaches.</param>
    /// <param name="choice">The alternative the rank takes, or empty.</param>
    /// <param name="refusal">Why nobody rose.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static PromotionResult Refused(
        string promotion,
        string fromClass,
        string toClass,
        int rank,
        string choice,
        PartyRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new PromotionResult(promotion, fromClass, toClass, rank, choice, [], [], refusal);
    }
}
