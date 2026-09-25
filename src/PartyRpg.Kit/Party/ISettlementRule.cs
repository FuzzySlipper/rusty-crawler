namespace PartyRpg.Kit.Party;

/// <summary>What one charge asks of this party, decided by whoever owns the game's prices and thresholds.</summary>
/// <remarks>
/// <para>
/// A price is not a number this kit holds. What a service charges, whether a fare is discounted, whether a
/// guild, a trainer, or a temple will deal with the party at all, and where the party's standing crosses
/// the line are the ruleset's decisions over its own tables, and this is the seam that hands them to the
/// settlement path. A rule that reads no standing and answers the quoted charge is equally legal, because
/// the kit never assumes which parts of a game's policy exist.
/// </para>
/// <para>
/// Reputation and fame arrive together, as the party's one standing, because only the ruleset knows whether
/// its thresholds count what people think, what people have heard, or both.
/// </para>
/// </remarks>
public interface ISettlementRule
{
    /// <summary>Prices one charge for the party asked to pay it, or refuses to serve the party.</summary>
    /// <param name="quoted">What the service, fare, fee, or donation quoted.</param>
    /// <param name="standing">The party's reputation and fame, over which the rule's thresholds are its own.</param>
    /// <returns>The price the party actually pays, or why the charge is refused.</returns>
    SettlementQuote Quote(PartyCost quoted, PartyReputation standing);
}
