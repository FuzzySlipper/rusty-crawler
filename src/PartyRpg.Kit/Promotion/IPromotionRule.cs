namespace PartyRpg.Kit.Promotion;

/// <summary>
/// What this game answers about rank: which classes lead to which ranks, what each rank asks for, and which
/// alternative a second promotion takes.
/// </summary>
/// <remarks>
/// <para>
/// This is a ruleset's whole contribution to promotion, and it is one answer rather than several:
/// <see cref="Ladder"/> is the table, and everything a caller can ask — which ranks a class leads to, which
/// ranks a person gives, what one rank asks for — is a lookup over it. <see cref="Progression.PartyProgression"/>
/// is what judges a rank against the party and what moves a member's class and rank; nothing here is called
/// to change anything, because a rule that promoted a character itself would be a second writer of one fact.
/// </para>
/// <para>
/// <b>A ruleset may state no ladder at all.</b> A session composed without one has no promotion: nobody is
/// handed a rank, every promotion a conversation could offer is absent, and the panel says the mechanism is
/// not there rather than showing a ladder nobody can climb. That is the honest state of a game that has not
/// said how its classes advance.
/// </para>
/// </remarks>
public interface IPromotionRule
{
    /// <summary>Every rank this game's classes lead to.</summary>
    PromotionLadder Ladder { get; }
}
