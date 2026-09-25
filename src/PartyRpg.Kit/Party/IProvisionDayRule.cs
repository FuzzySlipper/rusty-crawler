using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>What a day on the road costs the party, and what a short larder does to it.</summary>
/// <remarks>
/// <para>
/// Two questions, both the ruleset's: how much food a travelling or camping day takes, and where the larder
/// crosses the line into weakening the party. Neither is a number this kit holds, and neither is defaulted:
/// a ruleset that has not answered supplies no rule, and then a day costs nothing and nothing follows from
/// a larder running low.
/// </para>
/// <para>
/// The shape follows the donor's day boundary, where a new day takes one ration and leaves every character
/// weak once the party has gone a day past rest, and where a short larder is spent down to empty rather
/// than refused (<c>src/Engine/Engine.cpp</c>, the timed-effects party update; <c>src/Engine/Party.cpp</c>,
/// <c>SetFood</c>). Its rest command separately refuses to start a rest the larder cannot provision
/// (<c>src/Application/Game.cpp</c>): that is a decision about an action, so a rest owner asks
/// <see cref="PartyFood.CanCover"/> for the charge before it begins, while the day's own accounting is
/// what this rule prices.
/// </para>
/// <para>
/// Followers are counted in the charge because they eat, but the consequence is a member's condition: a
/// follower carries no condition state, so what a hungry follower suffers is nothing the party holds yet.
/// </para>
/// </remarks>
public interface IProvisionDayRule
{
    /// <summary>How much food one day of travelling or camping costs the party.</summary>
    /// <param name="members">How many members the party has, which a charge per head counts.</param>
    /// <param name="followers">How many followers travel with it.</param>
    /// <returns>The day's charge, in the unit the party's larder measures.</returns>
    Provisions DailyCharge(int members, int followers);

    /// <summary>The consequence a larder at this level has for every member, or null when nobody is weakened.</summary>
    /// <param name="portionsAfter">What the larder holds once the day has been spent.</param>
    /// <param name="members">How many members the party has.</param>
    /// <param name="followers">How many followers travel with it.</param>
    /// <returns>The condition every member suffers, or null when the party comes through the day fed.</returns>
    ActiveCondition? Consequence(int portionsAfter, int members, int followers);
}
