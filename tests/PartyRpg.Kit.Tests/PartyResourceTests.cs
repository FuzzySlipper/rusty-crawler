using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The party's owned resources: the one settlement path over its purse and larder, what a day on the road
/// costs it, and the ruleset policy those two accounts are charged under.
/// </summary>
/// <remarks>
/// Every number a game would recognize here belongs to the test's own policy — a price, a reputation or
/// fame threshold, a day's charge, the larder level at which a party is weakened — which is the point of
/// the seams: the kit holds no price, no threshold, and no condition, so the same mechanism serves a test
/// that invents them and a ruleset that owns them.
/// </remarks>
public sealed class PartyResourceTests
{
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly AttributeId Vigour = new("vigour");
    private static readonly ConditionId Weakened = new("weakened");
    private static readonly FollowerDefinitionId Porter = new("porter");

    [Fact]
    public void A_settlement_debits_the_purse_and_the_larder_together()
    {
        using PartyEntity party = Build(coins: 100, foodPortions: 10, memberCount: 2);
        PartyResourceLedger ledger = new(party);

        ResourceSettlement settlement = ledger.Settle(new PartyCost(30, Portions(4)));

        Assert.True(settlement.Admitted);
        Assert.Equal(30, settlement.Cost.Coins);
        Assert.Equal(4, settlement.Cost.Food.Amount);
        Assert.Equal(70, settlement.PurseAfter);
        Assert.Equal(6, settlement.ProvisionsAfter);

        // What moved is the party's own state: no member can hold money or food, so there is no second
        // balance anywhere that could disagree with the party's.
        Assert.Equal(70, party.Purse.Coins);
        Assert.Equal(6, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Actor.Has<PartyPurse>()));
        Assert.All(party.Members, member => Assert.False(member.Actor.Has<PartyFood>()));
    }

    [Fact]
    public void A_charge_the_party_cannot_cover_is_refused_whole_with_both_shortfalls_named()
    {
        using PartyEntity party = Build(coins: 20, foodPortions: 2);
        PartyResourceLedger ledger = new(party);

        ResourceSettlement refused = ledger.Settle(new PartyCost(30, Portions(5)));

        Assert.False(refused.Admitted);
        Assert.Equal("purse-and-larder-short", refused.Refusal!.Code);
        Assert.Contains("10 coin(s)", refused.Refusal.Message, StringComparison.Ordinal);
        Assert.Contains("3 portions", refused.Refusal.Message, StringComparison.Ordinal);

        // A half-paid charge is worse than a refused one, so nothing moved and asking what was paid fails
        // rather than reporting a price the party never parted with.
        Assert.Throws<InvalidOperationException>(() => refused.Cost);
        Assert.Equal(20, party.Purse.Coins);
        Assert.Equal(2, party.Food.Portions);
    }

    [Fact]
    public void A_shortfall_in_one_account_names_that_account_and_leaves_both_alone()
    {
        using PartyEntity poor = Build(coins: 5, foodPortions: 4);
        ResourceSettlement noCoin = new PartyResourceLedger(poor).Settle(PartyCost.OfGold(30));

        Assert.Equal("purse-short", noCoin.Refusal!.Code);
        Assert.Contains("25 coin(s)", noCoin.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal(5, poor.Purse.Coins);
        Assert.Equal(4, poor.Food.Portions);

        using PartyEntity hungry = Build(coins: 100, foodPortions: 1);
        ResourceSettlement noFood = new PartyResourceLedger(hungry).Settle(PartyCost.OfFood(Portions(3)));

        Assert.Equal("larder-short", noFood.Refusal!.Code);
        Assert.Contains("2 portions", noFood.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal(1, hungry.Food.Portions);
        Assert.Equal(100, hungry.Purse.Coins);
    }

    [Fact]
    public void Reputation_changes_what_a_policy_charges_and_whether_it_serves_the_party()
    {
        TempleStanding temple = new();
        using PartyEntity party = Build(coins: 100, reputation: 25);
        PartyResourceLedger ledger = new(party, temple);

        // The rule's own threshold: a party the town thinks well of pays a tenth less than the quoted price.
        ResourceSettlement discounted = ledger.Settle(PartyCost.OfGold(50));

        Assert.True(discounted.Admitted);
        Assert.Equal(45, discounted.Cost.Coins);
        Assert.Equal(55, party.Purse.Coins);

        // The same quoted price after one deed costs the party its standing: below the rule's line it is not
        // served at all, and nothing is taken.
        party.Reputation.ChangeReputation(-30);
        ResourceSettlement refused = ledger.Settle(PartyCost.OfGold(50));

        Assert.False(refused.Admitted);
        Assert.Equal("standing-refused", refused.Refusal!.Code);
        Assert.Contains("-5", refused.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal(55, party.Purse.Coins);

        // The rule was handed the standing the party actually carried and decided for itself, which is what
        // makes this a policy's answer rather than a branch inside the settlement path.
        Assert.Equal(new[] { 25, -5 }, temple.SeenReputations);
    }

    [Fact]
    public void Fame_reaches_the_same_policy_and_changes_its_answer()
    {
        TempleStanding temple = new();
        using PartyEntity party = Build(coins: 100, reputation: 10, fame: 0);
        PartyResourceLedger ledger = new(party, temple);

        ResourceSettlement charged = ledger.Settle(PartyCost.OfGold(50));

        Assert.True(charged.Admitted);
        Assert.Equal(50, charged.Cost.Coins);
        Assert.Equal(50, party.Purse.Coins);

        // Reputation alone earns no waiver here: the rule's other threshold counts what people have heard.
        party.Reputation.ChangeFame(10);
        ResourceSettlement waived = ledger.Settle(PartyCost.OfGold(50));

        Assert.True(waived.Admitted);
        Assert.True(waived.Cost.IsFree);
        Assert.Equal(50, party.Purse.Coins);
    }

    [Fact]
    public void A_day_costs_the_rulesets_charge_and_weakens_a_party_below_its_threshold()
    {
        Rations rations = new(perDay: 2, weakenedBelow: 3, Weakened);
        using PartyEntity party = Build(foodPortions: 5, memberCount: 2, hiredFollowers: 1);
        PartyResourceLedger ledger = new(party, provisioning: rations);

        ProvisionDay fed = ledger.SpendDay();

        Assert.Equal(2, fed.Charged.Amount);
        Assert.Equal(2, fed.Covered);
        Assert.True(fed.PaidInFull);
        Assert.Null(fed.Shortage);
        Assert.Equal(3, party.Food.Portions);
        Assert.All(party.Members, member => Assert.False(member.Conditions.Has(Weakened)));

        // Every head the party has reaches the rule, followers included, so a charge per head is the
        // ruleset's decision to make rather than a number the kit picked.
        Assert.Equal((2, 1), rations.LastAsked);
        Assert.Equal(1, rations.ChargesAsked);

        // One more day leaves the larder below the rule's line, and the condition the rule named lands on
        // every member: the kit applies the consequence, it does not choose one.
        ProvisionDay hungry = ledger.SpendDay();

        // The day itself was covered; it is the larder left behind that crossed the rule's line.
        Assert.Equal(2, hungry.Covered);
        Assert.True(hungry.PaidInFull);
        Assert.Equal(Weakened, hungry.Shortage!.Value.Condition);
        Assert.Equal(1, party.Food.Portions);
        Assert.All(party.Members, member => Assert.Equal(1, member.Conditions.SeverityOf(Weakened)));
    }

    [Fact]
    public void A_day_the_world_priced_spends_exactly_what_the_world_quoted()
    {
        Rations camping = new(perDay: 99, weakenedBelow: 0, Weakened);
        using PartyEntity party = Build(foodPortions: 10);
        PartyResourceLedger ledger = new(party, provisioning: camping);

        // Travel states its cost and applies nothing, so this is where the food a road quoted reaches the
        // larder — and the road's price, not a camping day's charge, is what it spends.
        TravelCost road = new(new TravelTime(3, TravelTimeUnit.Days), new Provisions(4, ProvisionUnit.Portions));
        ProvisionDay day = ledger.SpendDay(road.Food);

        Assert.Equal(4, day.Charged.Amount);
        Assert.Equal(4, day.Covered);
        Assert.Equal(6, party.Food.Portions);
        Assert.Equal(0, camping.ChargesAsked);
    }

    [Fact]
    public void A_day_the_larder_cannot_cover_eats_what_it_holds_and_never_goes_negative()
    {
        Rations rations = new(perDay: 3, weakenedBelow: 2, Weakened);
        using PartyEntity party = Build(foodPortions: 1);
        PartyResourceLedger ledger = new(party, provisioning: rations);

        ProvisionDay day = ledger.SpendDay();

        Assert.Equal(3, day.Charged.Amount);
        Assert.Equal(1, day.Covered);
        Assert.Equal(2, day.Unpaid);
        Assert.False(day.PaidInFull);
        Assert.Equal(0, party.Food.Portions);

        // The day is not cancelled and the larder is not overdrawn: the party carries the shortfall, which
        // is what the ruleset's consequence is for.
        Assert.Equal(Weakened, day.Shortage!.Value.Condition);
    }

    [Fact]
    public void Without_a_ruleset_policy_a_settlement_is_the_quoted_price_and_a_day_costs_nothing()
    {
        using PartyEntity party = Build(coins: 40, foodPortions: 4);
        PartyResourceLedger ledger = new(party);

        ResourceSettlement settlement = ledger.Settle(new PartyCost(15, Portions(1)));

        Assert.True(settlement.Admitted);
        Assert.Equal(15, settlement.Cost.Coins);
        Assert.Equal(1, settlement.Cost.Food.Amount);
        Assert.Equal(25, party.Purse.Coins);
        Assert.Equal(3, party.Food.Portions);

        // No rule is no answer rather than a zero answer: nothing is charged for a day, and nothing follows
        // from whatever the larder holds.
        ProvisionDay day = ledger.SpendDay();

        Assert.Equal(0, day.Charged.Amount);
        Assert.Null(day.Shortage);
        Assert.Equal(3, party.Food.Portions);
        Assert.All(party.Members, member => Assert.Empty(member.Conditions.Active));
    }

    [Fact]
    public void Crediting_the_party_lands_in_the_same_two_accounts_a_charge_comes_from()
    {
        using PartyEntity party = Build(coins: 10, foodPortions: 1);
        PartyResourceLedger ledger = new(party);

        ledger.Credit(new PartyCost(25, Portions(3)));

        Assert.Equal(35, party.Purse.Coins);
        Assert.Equal(4, party.Food.Portions);

        // A gain of nothing is still a legal gain, and moves nothing.
        ledger.Credit(PartyCost.Free);

        Assert.Equal(35, party.Purse.Coins);
        Assert.Equal(4, party.Food.Portions);
    }

    private static Provisions Portions(int amount) => new(amount, ProvisionUnit.Portions);

    /// <summary>Builds the party these resources belong to, with the members and followers a test asks for.</summary>
    private static PartyEntity Build(
        int coins = 0,
        int foodPortions = 0,
        int reputation = 0,
        int fame = 0,
        int memberCount = 1,
        int hiredFollowers = 0)
    {
        List<MemberCreation> members = [];
        for (int index = 0; index < memberCount; index++) members.Add(new MemberCreation(Seed($"Member {index + 1}")));

        PartyEntity party = new PartyEntityFactory(hiredFollowerLimit: hiredFollowers).Create(
            new PartyCreation(members, coins, foodPortions, ProvisionUnit.Portions, reputation, fame));

        for (int index = 0; index < hiredFollowers; index++)
        {
            Assert.Null(party.Followers.Add(new PartyFollower(
                party.Identity.MintMemberId(),
                Porter,
                $"Follower {index + 1}",
                FollowerKind.Hired)));
        }

        return party;
    }

    private static PartyMemberSeed Seed(string name) => new(
        name,
        TestRace,
        Fighter,
        [new AttributeScore(Vigour, 12)],
        skills: [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(10),
        spellPoints: ResourcePool.Full(5));

    /// <summary>
    /// A ruleset policy of the kind a temple writes: it refuses a party the town distrusts, charges a party
    /// it thinks well of less, and waives the price for a party whose fame has reached it. Every number here
    /// is the test's own, which is exactly why the kit has none.
    /// </summary>
    private sealed class TempleStanding : ISettlementRule
    {
        private const int DistrustedBelow = 0;
        private const int WellRegardedAt = 20;
        private const int FamedAt = 10;

        /// <summary>The standings the rule was handed, in the order it was asked.</summary>
        internal List<int> SeenReputations { get; } = [];

        public SettlementQuote Quote(PartyCost quoted, PartyReputation standing)
        {
            SeenReputations.Add(standing.Reputation);
            if (standing.Reputation < DistrustedBelow)
            {
                return SettlementQuote.Refused(new PartyRefusal(
                    "standing-refused",
                    $"A party of reputation {standing.Reputation} is not served here."));
            }

            if (standing.Fame >= FamedAt) return SettlementQuote.Payable(PartyCost.Free);

            int charged = standing.Reputation >= WellRegardedAt ? quoted.Coins - (quoted.Coins / 10) : quoted.Coins;
            return SettlementQuote.Payable(new PartyCost(charged, quoted.Food));
        }
    }

    /// <summary>A ruleset's daily larder policy: what a day costs, and where a party counts as weakened.</summary>
    private sealed class Rations : IProvisionDayRule
    {
        private readonly int _perDay;
        private readonly int _weakenedBelow;
        private readonly ConditionId _weakened;

        internal Rations(int perDay, int weakenedBelow, ConditionId weakened)
        {
            _perDay = perDay;
            _weakenedBelow = weakenedBelow;
            _weakened = weakened;
        }

        /// <summary>How often a camping day asked for its charge; a road already priced asks nothing.</summary>
        internal int ChargesAsked { get; private set; }

        /// <summary>The heads the last camping charge was asked about, members and followers alike.</summary>
        internal (int Members, int Followers) LastAsked { get; private set; }

        public Provisions DailyCharge(int members, int followers)
        {
            ChargesAsked++;
            LastAsked = (members, followers);
            return Portions(_perDay);
        }

        public ActiveCondition? Consequence(int portionsAfter, int members, int followers) =>
            portionsAfter < _weakenedBelow ? new ActiveCondition(_weakened, 1) : null;
    }
}
