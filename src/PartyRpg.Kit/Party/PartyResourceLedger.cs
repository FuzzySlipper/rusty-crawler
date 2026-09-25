using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Party;

/// <summary>The one path that settles a charge against the party's own accounts, and the one that spends a day.</summary>
/// <remarks>
/// <para>
/// Every charge a service, a fare, a training fee, a temple donation, or a road makes is settled here, and
/// a settlement is whole: the price is judged against both accounts before either moves, so a party that
/// can pay in coin but not in food is refused with both shortfalls named rather than half charged. The
/// accounts are the party's own — one purse and one larder — and nothing here can reach a character's
/// belongings, because a character owns nothing but what it wears.
/// </para>
/// <para>
/// <b>The ruleset supplies the policy; the kit supplies the mechanism.</b> An <see cref="ISettlementRule"/>
/// decides what a charge becomes and whether the party is served at all — its price, and every threshold
/// over reputation and fame behind it. An <see cref="IProvisionDayRule"/> decides what a day costs and when
/// a larder leaves the party weakened. A product that supplies neither settles at the quoted price and
/// spends nothing for a day, which is the honest state of a ruleset that has not answered yet: no constant
/// here stands in for an answer nobody gave.
/// </para>
/// <para>
/// <b>The purse never goes below zero</b>, and nothing here reaches around that. A debit that cannot be paid
/// is refused by name with the shortfall stated, so a service can say what the party lacks instead of
/// quietly taking what it has. A game that wanted credit would have to change the purse and the save that
/// records it together and deliberately, because a debt the save refuses to load is worse than no debt at
/// all.
/// </para>
/// <para>
/// A charge stated in a unit the larder does not measure is a defect rather than an ordinary shortfall, and
/// the larder's own guard refuses it loudly: reading a foreign number as portions would pay a charge nobody
/// agreed on. Everything else a charge can go wrong about comes back as a refusal.
/// </para>
/// </remarks>
public sealed class PartyResourceLedger
{
    private readonly PartyEntity _party;
    private readonly ISettlementRule? _settlement;
    private readonly IProvisionDayRule? _provisioning;

    /// <summary>Creates the one settlement path over the party whose accounts it moves.</summary>
    /// <param name="party">
    /// The party that owns the purse, the larder, and the standing a charge is priced against. The ledger
    /// borrows it and does not outlive it.
    /// </param>
    /// <param name="settlement">
    /// The rule that prices a charge and decides whether the party is served. Without one the quoted price
    /// is what the party pays.
    /// </param>
    /// <param name="provisioning">
    /// The rule that prices a day and states what a short larder does. Without one a day costs nothing and
    /// nothing follows from an empty larder.
    /// </param>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    public PartyResourceLedger(
        PartyEntity party,
        ISettlementRule? settlement = null,
        IProvisionDayRule? provisioning = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        _party = party;
        _settlement = settlement;
        _provisioning = provisioning;
    }

    /// <summary>Settles one charge against the party's purse and larder, whole or not at all.</summary>
    /// <remarks>
    /// The rule prices the charge first, because what a service asks and what the party pays are different
    /// numbers; then both accounts are judged; then both are debited. A refusal at any step leaves the party
    /// exactly as it was, which is what lets a caller offer a price, take the answer, and show it.
    /// </remarks>
    /// <param name="quoted">What the service, fare, fee, or donation quoted.</param>
    /// <returns>What was paid and where the accounts stand, or why nothing was paid.</returns>
    public ResourceSettlement Settle(PartyCost quoted)
    {
        SettlementQuote quote = _settlement?.Quote(quoted, _party.Reputation) ?? SettlementQuote.Payable(quoted);
        if (quote.Refusal is { } refusal) return ResourceSettlement.Refused(refusal);

        PartyCost price = quote.Cost;
        PartyPurse purse = _party.Purse;
        PartyFood food = _party.Food;

        // Nothing is paid until both accounts can cover their part: a charge met in coin and refused in food
        // would leave the party paying for something it did not receive.
        int missingCoins = Math.Max(0, price.Coins - purse.Coins);
        bool foodCovered = price.Food.Amount == 0 || food.CanCover(price.Food);
        int missingFood = foodCovered ? 0 : price.Food.Amount - food.Portions;
        if (missingCoins > 0 || missingFood > 0)
        {
            return ResourceSettlement.Refused(Shortfall(price, purse, food, missingCoins, missingFood));
        }

        // Both debits were judged a moment ago and nothing has touched the party since, so neither can
        // refuse now; their answers are discarded so the only refusal a caller can see is the one composed
        // above.
        purse.TryDebit(price.Coins);
        food.TrySpend(price.Food);
        return ResourceSettlement.Paid(price, purse.Coins, food.Portions);
    }

    /// <summary>Puts value into the party's accounts: a sale's takings, a reward, or provisions found.</summary>
    /// <remarks>
    /// This is the earning counterpart of <see cref="Settle"/>, so what a shop pays the party and what a
    /// shop takes from it land in the same two accounts under the same owner. A gain is never refused: an
    /// account that could not hold what it was given would be a purse or a larder whose own bounds had
    /// failed.
    /// </remarks>
    /// <param name="gained">What arrives in the purse and the larder.</param>
    /// <exception cref="OverflowException">An account would leave the numbers it is described in.</exception>
    /// <exception cref="ArgumentException">The gain states food in a unit the larder does not measure.</exception>
    public void Credit(PartyCost gained)
    {
        PartyPurse purse = _party.Purse;
        PartyFood food = _party.Food;
        if (gained.Food.Amount > 0 && gained.Food.Unit != food.Unit)
        {
            throw new ArgumentException(
                $"The gain is stated in {gained.Food.Unit} while the larder measures {food.Unit}; the kit converts between units of food for nobody.",
                nameof(gained));
        }

        if (gained.Coins > 0) purse.Credit(gained.Coins);
        if (gained.Food.Amount > 0) food.Credit(gained.Food.Amount);
    }

    /// <summary>Spends one camping or resting day, charged by the ruleset's day rule.</summary>
    /// <remarks>
    /// This is the day a party spends resting rather than travelling: the charge comes from
    /// <see cref="IProvisionDayRule.DailyCharge"/>, so what a night on the road costs is the ruleset's
    /// answer rather than a number here. A rest owner that refuses to begin a rest the larder cannot
    /// provision asks <see cref="PartyFood.CanCover"/> for the same charge before it starts.
    /// </remarks>
    /// <returns>What the day took and what it did.</returns>
    public ProvisionDay SpendDay()
    {
        int members = _party.Members.Count;
        int followers = _party.Followers.Count;
        Provisions charged = _provisioning?.DailyCharge(members, followers) ?? Provisions.None;
        return SpendDay(charged, members, followers);
    }

    /// <summary>Spends a day the world already priced: a transition's quoted provisions.</summary>
    /// <remarks>
    /// The world states what a road costs and charges nothing, so this is where its part of a transition
    /// reaches an account. The rule is still asked what a larder at the resulting level does to the party,
    /// because a journey that ate the last of the food weakens the party on the road, not at the next camp.
    /// </remarks>
    /// <param name="charged">What the day costs, as the world quoted it, in the unit the larder measures.</param>
    /// <returns>What the day took and what it did.</returns>
    public ProvisionDay SpendDay(Provisions charged) =>
        SpendDay(charged, _party.Members.Count, _party.Followers.Count);

    /// <summary>Spends a day's food and applies the ruleset's consequence for what the larder holds afterwards.</summary>
    private ProvisionDay SpendDay(Provisions charged, int members, int followers)
    {
        PartyFood food = _party.Food;
        int covered = 0;
        if (charged.Amount > 0)
        {
            // The larder gives what it holds and no more: a short day eats the remainder rather than
            // refusing the road or counting portions the party does not have.
            covered = food.CanCover(charged) ? charged.Amount : food.Portions;
            food.TryDebit(covered);
        }

        ActiveCondition? shortage = _provisioning?.Consequence(food.Portions, members, followers);
        if (shortage is { } condition)
        {
            // Hungry members carry the ruleset's own condition; ending it belongs to recovery, which is
            // where a cure or a full meal will clear it.
            foreach (PartyMember member in _party.Members) member.Conditions.Apply(condition);
        }

        return new ProvisionDay(charged, covered, shortage);
    }

    /// <summary>Names what a charge the party cannot cover is short of, in one refusal a caller can show.</summary>
    private static PartyRefusal Shortfall(PartyCost price, PartyPurse purse, PartyFood food, int missingCoins, int missingFood)
    {
        string code = (missingCoins > 0, missingFood > 0) switch
        {
            (true, true) => "purse-and-larder-short",
            (true, false) => "purse-short",
            _ => "larder-short",
        };

        List<string> missing = [];
        if (missingCoins > 0) missing.Add($"{missingCoins} coin(s)");
        if (missingFood > 0) missing.Add($"{missingFood} {food.Unit.ToString().ToLowerInvariant()}");

        return new PartyRefusal(
            code,
            $"The price is {Amounts(price.Coins, price.Food.Amount, food.Unit)} and the party holds {Amounts(purse.Coins, food.Portions, food.Unit)}: {string.Join(" and ", missing)} short.");
    }

    /// <summary>Describes two amounts of party value the way a person reads them.</summary>
    private static string Amounts(int coins, int portions, ProvisionUnit unit)
    {
        if (coins == 0) return $"{portions} {unit.ToString().ToLowerInvariant()}";
        return portions == 0 ? $"{coins} coin(s)" : $"{coins} coin(s) and {portions} {unit.ToString().ToLowerInvariant()}";
    }
}
