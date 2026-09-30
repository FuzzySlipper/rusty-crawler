namespace PartyRpg.Kit.World;

/// <summary>How many game days a journey a counter sells takes, as the game's own rule decides it.</summary>
/// <remarks>
/// <para>
/// Content states that a crossing is sold as a passage and which route it runs on — a name the game gives
/// meaning to, such as the network a counter belongs to — and nothing about how long it takes. The length is
/// a rule: it is the same number the counter writes on the ticket and the road charges the clock, so it has
/// one owner, and that owner is the game's ruleset rather than whatever wrote the content. A game that retunes
/// how long a route takes changes this answer, and no content is written again.
/// </para>
/// <para>
/// The kit asks once per sold crossing while the world graph is built, so a route the rule does not know is a
/// defect the world names at load rather than a journey that fails the day somebody boards it.
/// </para>
/// </remarks>
public interface IFareDurationRule
{
    /// <summary>How many game days one sold crossing takes, or null when the rule states no such route.</summary>
    /// <param name="from">The place the crossing leaves, or null when the world itself issues it.</param>
    /// <param name="to">The place the crossing reaches.</param>
    /// <param name="route">The route content says the crossing runs on.</param>
    /// <returns>The days, which a journey needs at least one of; null when the route is not one this game states.</returns>
    int? DaysOf(PlaceId? from, PlaceId to, string route);
}
