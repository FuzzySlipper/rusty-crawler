using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one service mechanism: a party walks up to a counter, browses it, transacts, and leaves, over the
/// operations buy, sell, identify, repair, and teach.
/// </summary>
/// <remarks>
/// <para>
/// Every rule a game would recognize here belongs to the test's own rule — what a counter is, what its
/// shelves hold, who it serves, and what it charges — which is the point of the seam: the kit holds no
/// price, no membership, and no kind of building, so the same mechanism serves a test that invents them and
/// a ruleset that owns them. The party is a real party with a real purse and a real shared pack, because
/// the mechanism's contract is that those are the only accounts a transaction touches.
/// </para>
/// <para>
/// The places are written inline, so what a counter is comes from content in these tests exactly as it does
/// from an imported pack in the product, and the clock is the kit's own clock, so a restock is proved to be
/// game time rather than a counter in a frame loop.
/// </para>
/// </remarks>
public sealed class ServiceTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId CounterPlace = new("1");
    private static readonly UseIntentNames UseControls = new("test.use", "test.ui.action.v1");
    private static readonly ServiceIntentNames ServiceControls = new("test.service.leave", "test.ui.action.v1");
    private static readonly ConversationIntentNames ConversationControls = new("test.conversation.leave", "test.ui.action.v1");
    private static readonly MovementIntentNames MovementControls = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");
    private const double StepSeconds = 1.0 / 60.0;

    private static GameCalendar Calendar => GameCalendar.TwelveMonthsOfFourWeeks;

    /// <summary>A shop with a shelf of swords, a lesson, and every operation the mechanism has.</summary>
    private static ServiceDefinition Shop(
        int swords = 2,
        int swordWorth = 100,
        GameDuration? refresh = null,
        string membership = "",
        IReadOnlyList<ServiceLesson>? lessons = null) =>
        new(
            new ServiceId("sword-and-shield"),
            new ServiceKind("Weapon Shop"),
            "The Sword and Shield",
            [
                ServiceOperationKind.Buy,
                ServiceOperationKind.Sell,
                ServiceOperationKind.Identify,
                ServiceOperationKind.Repair,
                ServiceOperationKind.Teach,
            ],
            [new ServiceStockLine(new ItemDefinitionId("sword"), swords, swordWorth, "A fine sword")],
            lessons ??
            [
                new ServiceLesson(ServiceLessonKind.Skill, "Sword", 1, 25, "Basic Sword"),
            ],
            proprietor: "Bertram",
            hours: new ServiceHours(6, 18),
            refreshInterval: refresh,
            membership: membership);

    [Fact]
    public void Every_operation_settles_through_the_same_purse_and_the_same_path()
    {
        using PartyEntity party = Party(coins: 500);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop());
        PartyServices services = new(rule, party, accounts, Clock(), new PartyProgression(new TrainingRule(), party));
        Assert.True(services.Open(rule.Service).IsApplied);

        // A purchase takes coin out of the party's one purse and puts the goods in its one shared pack.
        ServiceResult bought = services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword", Count: 2));
        Assert.True(bought.IsApplied);
        Assert.Equal(200, bought.Paid);
        Assert.Equal(0, bought.Earned);
        Assert.Equal(300, bought.Coins);
        Assert.Equal(300, party.Purse.Coins);
        Assert.Equal(2, party.Inventory.TotalOf(new ItemDefinitionId("sword")));

        // An identification and a repair are fees for item state, and they move the same purse.
        ItemInstance first = party.Inventory.Items[0];
        ItemInstance second = party.Inventory.Items[1];
        second.TakeDamage(4);
        ServiceResult identified = services.Transact(new ServiceCommand(ServiceOperationKind.Identify, first.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        ServiceResult repaired = services.Transact(new ServiceCommand(ServiceOperationKind.Repair, second.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(first.State.IsIdentified);
        Assert.Equal(0, second.State.Damage);
        Assert.Equal(25, identified.Paid);
        Assert.Equal(40, repaired.Paid);
        Assert.Equal(235, party.Purse.Coins);

        // A lesson moves the same purse and changes a member's own skill state.
        ServiceResult taught = services.Transact(new ServiceCommand(ServiceOperationKind.Teach, "Sword", Member: 0));
        Assert.True(party.Members[0].Skills.Knows(new SkillId("Sword")));
        Assert.Equal(1, party.Members[0].Skills.LevelOf(new SkillId("Sword")));
        Assert.Equal(25, taught.Paid);

        // A sale is the earning direction of the same path: the item leaves the shared pack and coin arrives.
        ServiceResult sold = services.Transact(new ServiceCommand(ServiceOperationKind.Sell, first.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(sold.IsApplied);
        Assert.Equal(100, sold.Earned);
        Assert.Equal(0, sold.Paid);
        Assert.Equal(310, party.Purse.Coins);
        Assert.Equal(1, party.Inventory.TotalOf(new ItemDefinitionId("sword")));

        // Every result reported the party's own purse, which is the same number the party holds: there is
        // one balance and no second one for a service to keep.
        Assert.Equal(party.Purse.Coins, sold.Coins);
    }

    [Fact]
    public void Cure_choices_quote_each_patient_and_the_command_changes_only_the_named_member()
    {
        ConditionId condition = new("test-affliction");
        MemberCreation Member(string name) => new(new PartyMemberSeed(
            name, new RaceId("testfolk"), new ClassId("fighter"), [], skills: [], spells: [],
            experience: 0, level: 1, skillPoints: 0, classRank: 1,
            conditions: [new ActiveCondition(condition)], hitPoints: ResourcePool.Full(10), spellPoints: ResourcePool.Full(5)));
        using PartyEntity party = new PartyEntityFactory().Create(new PartyCreation([Member("First"), Member("Second")], coins: 100));
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Cure] })
        {
            Offerings = [new ServiceOffer(ServiceOfferKind.Cure, "A cure", Subject: "cure", Clears: [condition])],
            Price = request => ServiceQuote.Charging(request.Member == party.Members[0].Id ? 12 : 27),
            Eligibility = request => party.Member(request.Member).Conditions.Has(condition)
                ? ServiceEligibility.Allowed
                : ServiceEligibility.Refused(new Refusal("test-no-cure-needed", "This patient needs no cure.")),
        };
        PartyServices services = new(rule, party, new PartyResourceLedger(party), Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        ServiceOfferSnapshot offer = Assert.Single(ServiceSnapshot.From(services).Offers);
        Assert.Equal(new[] { 12, 27 }, offer.Choices.Select(choice => choice.Price));
        ServiceOfferChoiceSnapshot second = offer.Choices[1];
        Assert.Equal("Second", second.Name);
        Assert.True(second.Enabled);
        ServiceResult cured = services.Transact(new ServiceCommand(ServiceOperationKind.Cure, offer.Subject, Member: second.Member));
        Assert.True(cured.IsApplied);
        Assert.Equal(27, cured.Paid);
        Assert.Equal(73, party.Purse.Coins);
        Assert.True(party.Members[0].Conditions.Has(condition));
        Assert.False(party.Members[1].Conditions.Has(condition));
        Assert.False(Assert.Single(ServiceSnapshot.From(services).Offers).Choices[1].Enabled);
    }

    [Fact]
    public void A_refusal_names_what_stopped_it_and_changes_nothing()
    {
        using PartyEntity party = Party(coins: 40);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop());
        Refusal notAMember = new("test-not-a-member", "This counter teaches members only.");
        rule.Eligibility = request => request.Operation == ServiceOperationKind.Teach
            ? ServiceEligibility.Refused(notAMember)
            : ServiceEligibility.Allowed;
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);

        // Not enough coin: the party's own settlement path names the shortfall, and nothing moved.
        ServiceResult poor = services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword"));
        Assert.False(poor.IsApplied);
        Assert.Equal(PartyCodes.PurseShort, poor.Code);
        Assert.Contains("60 coin(s)", poor.Message, StringComparison.Ordinal);
        Assert.Equal(40, party.Purse.Coins);
        Assert.Equal(2, rule.StockOf(services, "stock:sword"));

        // Not eligible: the ruleset's own refusal reaches the player unchanged.
        ServiceResult ineligible = services.Transact(new ServiceCommand(ServiceOperationKind.Teach, "Sword"));
        Assert.Equal(notAMember.Code, ineligible.Code);
        Assert.Contains(notAMember.Message, ineligible.Message, StringComparison.Ordinal);

        // Out of stock and no such item are two different answers, which is why the mechanism resolves what
        // a command names before it asks policy anything.
        ServiceResult missing = services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:potion"));
        Assert.Equal("service-no-such-lot", missing.Code);
        ServiceResult absent = services.Transact(new ServiceCommand(ServiceOperationKind.Sell, "99"));
        Assert.Equal("service-no-such-item", absent.Code);

        // And an operation the counter does not offer is its own refusal rather than a silent nothing.
        ShopRule closed = new(Shop() with { Operations = [ServiceOperationKind.Buy] });
        PartyServices buying = new(closed, party, accounts, Clock());
        Assert.True(buying.Open(closed.Service).IsApplied);
        ServiceResult notOffered = buying.Transact(new ServiceCommand(ServiceOperationKind.Sell, "1"));
        Assert.Equal("service-operation-unavailable", notOffered.Code);
    }


    /// <summary>
    /// Every capability a kind needs is one operation of the same mechanism, over the same purse.
    /// </summary>
    /// <remarks>
    /// A cure, a training step, provisions, a room, a deposit, a withdrawal, and a passage are the
    /// operations the twenty-one kinds of building need beyond buying, selling, identifying, repairing, and
    /// teaching. What this proves is that they are the same workflow over the same accounts: the counter is
    /// opened the same way, a command is judged and priced by the rule the same way, the charge settles
    /// through the party's own ledger, the change lands on the party's own state, and a refusal changes
    /// nothing at all.
    /// </remarks>
    [Fact]
    public void A_ward_whose_end_falls_inside_a_night_at_an_inn_ends_that_night()
    {
        using PartyEntity party = Party(coins: 100);
        PartyResourceLedger accounts = new(party);
        GameClock clock = Clock();
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Stay] })
        {
            Offerings = [new ServiceOffer(ServiceOfferKind.Stay, "A room for the night", Value: 12, Amount: 8)],
        };

        // The owners of game time are composed as a session composes them: each hears the one clock.
        RunningSpellEffects running = new(party, clock);
        clock.Observe(running);
        PartyRest rest = new(new TestNights(), party, clock, new TestRoom(), accounts);
        clock.Observe(rest);
        PartyServices services = new(rule, party, accounts, clock, rest: rest);
        clock.Observe(services);
        Assert.True(services.Open(rule.Service).IsApplied);

        // Two hours of ward and an eight-hour night: the night is the clock's to tell, so the ward ends in it
        // rather than outliving a deadline the clock stopped holding.
        EffectId ward = new("ward:magic");
        running.Start(ward, magnitude: 5, GameDuration.FromHours(2));
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Stay)).IsApplied);

        Assert.False(running.IsRunning(ward));
        Assert.False(party.Effects.Has(ward));
    }

    [Fact]
    public void A_counter_publishes_every_offer_with_its_price_and_a_notice_at_none()
    {
        using OpenCounter counter = new();

        // What the counter offers is published with what each would cost, and a notice is published at no price
        // because it is read rather than taken.
        ServiceBrowse browse = counter.Services.Browse()!;
        Assert.Equal(7, browse.Offers.Count);
        Assert.Equal(20, browse.Offers.Single(line => line.Offer.Kind == ServiceOfferKind.Cure).Price);
        Assert.Equal(0, browse.Offers.Single(line => line.Offer.Kind == ServiceOfferKind.Notice).Price);
    }

    [Fact]
    public void A_cure_ends_exactly_the_conditions_it_claims_restores_the_body_and_is_paid_from_the_purse()
    {
        using OpenCounter counter = new();
        int purse = counter.Party.Purse.Coins;

        ServiceResult cured = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Cure, "affliction", Member: 0));
        Assert.True(cured.IsApplied);
        Assert.Equal(20, cured.Paid);
        Assert.Equal(purse - 20, counter.Party.Purse.Coins);
        Assert.Equal(counter.Party.Purse.Coins, cured.Coins);
        Assert.False(counter.Member.Conditions.Has(new ConditionId("Cursed")));
        Assert.True(counter.Member.Conditions.Has(new ConditionId("Poisoned")));
        Assert.Equal(10, counter.Member.Resources.HitPoints.Current);

        // What the cure says it leaves is left on the member it ended something for.
        Assert.True(counter.Member.Conditions.Has(new ConditionId("Marked")));
    }

    [Fact]
    public void Training_raises_one_level_through_the_progression_owner_and_stops_at_the_counters_ceiling()
    {
        using OpenCounter counter = new();

        // The counter trains through the progression owner, which is what grants a level, and the counter's own
        // ceiling is where it stops: a step past it is refused rather than clamped. The refusal is the owner's,
        // because this counter's own eligibility rule allows everything — a ruleset that judges the hall's ceiling
        // refuses the step before the charge and names it there, which is what this game does.
        ServiceResult trained = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Train, Member: 0));
        Assert.True(trained.IsApplied);
        Assert.Equal(2, counter.Member.Progression.Level);
        ServiceResult capped = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Train, Member: 0));
        Assert.Equal("progression-training-capped", capped.Code);
        Assert.Equal(2, counter.Member.Progression.Level);
    }

    [Fact]
    public void Provisions_are_credited_to_the_larder_rather_than_the_pack()
    {
        using OpenCounter counter = new();
        int larder = counter.Party.Food.Portions;
        int items = counter.Party.Items.Count;

        ServiceResult provisioned = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Provision));
        Assert.True(provisioned.IsApplied);
        Assert.Equal(larder + 6, counter.Party.Food.Portions);
        Assert.Equal(items, counter.Party.Items.Count);
    }

    [Fact]
    public void A_room_is_a_night_of_the_rest_mechanism_on_the_one_clock_and_pays_the_debt_of_sleep()
    {
        using OpenCounter counter = new();
        PartyMember member = counter.Member;

        // The hours a room gives pass on the session's one clock, the night ends what every night ends and the
        // room's own list beside it, the larder is not drawn on, and the debt of going without sleep is paid.
        member.Conditions.Apply(new ActiveCondition(new ConditionId("Tired"), 2));
        member.Conditions.Apply(new ActiveCondition(TestNights.Weakness, 1));
        member.Resources.TakeDamage(4);
        int hour = counter.Clock.Now.Hour;
        int portions = counter.Party.Food.Portions;
        ServiceResult lodged = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Stay));
        Assert.True(lodged.IsApplied);
        Assert.Equal((hour + 8) % 24, counter.Clock.Now.Hour);
        Assert.False(member.Conditions.Has(new ConditionId("Tired")));
        Assert.False(member.Conditions.Has(TestNights.Weakness));
        Assert.Equal(member.Resources.HitPoints.Maximum, member.Resources.HitPoints.Current);
        Assert.Equal(portions, counter.Party.Food.Portions);

        // The night paid the debt and it runs again from waking: a day less an hour later the party is not weak,
        // and once the day is out it is.
        counter.Clock.Advance(GameDuration.FromHours(23));
        Assert.False(member.Conditions.Has(TestNights.Weakness));
        counter.Clock.Advance(GameDuration.FromHours(1));
        Assert.True(member.Conditions.Has(TestNights.Weakness));
    }

    [Fact]
    public void A_deposit_and_a_withdrawal_move_coin_both_ways_and_an_overdraw_is_refused_whole()
    {
        using OpenCounter counter = new();
        PartyEntity party = counter.Party;
        int purse = party.Purse.Coins;

        ServiceResult deposited = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Deposit, "vault", Count: 300));
        Assert.True(deposited.IsApplied);
        Assert.Equal(300, deposited.Paid);
        Assert.Equal(purse - 300, party.Purse.Coins);
        Assert.Equal(party.Purse.Coins, deposited.Coins);
        Assert.Equal(300, party.Holdings.BalanceOf("vault"));

        ServiceResult over = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Withdraw, "vault", Count: 500));
        Assert.Equal("service-holding-short", over.Code);
        Assert.Equal(300, party.Holdings.BalanceOf("vault"));
        Assert.Equal(purse - 300, party.Purse.Coins);

        ServiceResult withdrew = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Withdraw, "vault", Count: 120));
        Assert.True(withdrew.IsApplied);
        Assert.Equal(120, withdrew.Earned);
        Assert.Equal(purse - 180, party.Purse.Coins);
        Assert.Equal(party.Purse.Coins, withdrew.Coins);
        Assert.Equal(180, party.Holdings.BalanceOf("vault"));
    }

    [Fact]
    public void A_fare_buys_a_passage_the_road_reads_and_names_the_journey_it_sold()
    {
        using OpenCounter counter = new();

        // A passage is what a fare buys: the party holds the ticket and the route it was sold on, and the road
        // reads the route and times it by the game's rule rather than by a length written on the ticket.
        ServiceResult fare = counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Fare, "9"));
        Assert.True(fare.IsApplied);
        Assert.Equal(25, fare.Paid);
        Assert.Equal("coach", counter.Party.Passages.RouteTo(new PlaceId("9")));

        // The result says what the fare was about, which is how the session that owns the road learns which journey
        // the counter sold without keeping its own copy of the screen's request.
        Assert.Equal("9", fare.Subject);
    }

    [Fact]
    public void A_command_the_counter_cannot_resolve_is_its_own_refusal_and_moves_nothing()
    {
        using OpenCounter counter = new();
        int purse = counter.Party.Purse.Coins;

        Assert.Equal("service-no-such-offer", counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Fare, "77")).Code);
        Assert.Equal("service-no-such-offer", counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Cure, "eradication", Member: 0)).Code);
        Assert.Equal("service-no-such-member", counter.Services.Transact(new ServiceCommand(ServiceOperationKind.Cure, "affliction", Member: 4)).Code);
        Assert.Equal(purse, counter.Party.Purse.Coins);
    }

    [Fact]
    public void A_theft_at_a_counter_is_drawn_by_the_rule_and_laid_on_the_party_through_the_owners_it_reaches()
    {
        using PartyEntity party = Party(coins: 50);
        PartyResourceLedger accounts = new(party);
        GameClock clock = Clock();
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Buy, ServiceOperationKind.Steal] });
        PartyProgression progression = new(new DeedStanding(), party);
        PartyServices services = new(rule, party, accounts, clock, progression);
        Assert.True(services.Open(rule.Service).IsApplied);

        // A counter whose rule answers no theft offers nobody the act; one whose rule does offers it to each member
        // the rule lets try, which is what a panel draws its rows from.
        Assert.Empty(services.Browse()!.Thieves);
        rule.Theft = _ => ServiceTheft.Tried(
            caught: true,
            taken: true,
            coins: 0,
            items: null,
            marked: true,
            fine: 300,
            account: "fine",
            deed: "seen",
            ban: GameDuration.FromHours(24),
            message: "Tester gets away with a sword, but is seen.");
        Assert.Equal("Tester", services.Browse()!.Thieves.Single().Name);

        // One sword comes away whatever the command counted, marked as stolen; nothing is charged; the fine is owed
        // rather than taken; the deed reaches the world's opinion; and the counter shuts against the party.
        ServiceResult stolen = services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", Member: 0, Count: 5));
        Assert.True(stolen.IsApplied, stolen.Message);
        Assert.Equal("steal", stolen.Action);
        Assert.Equal(0, stolen.Paid);
        Assert.Equal(50, party.Purse.Coins);
        ItemInstance sword = party.Inventory.Items.Single();
        Assert.True(sword.State.IsStolen);
        Assert.Equal(300, party.Debts.OwedOn("fine"));
        Assert.Equal("seen", progression.LastDeed!.Source);
        Assert.Equal(-2, party.Reputation.Reputation);
        Assert.Contains("owes 300 on fine", stolen.Message, StringComparison.Ordinal);
        Assert.Contains("will not serve the party for 24 hour(s)", stolen.Message, StringComparison.Ordinal);
        Assert.True(services.LastTheft!.Caught);
        Assert.False(services.IsOpen);
        Assert.Equal("The Sword and Shield", services.Current!.Name);

        // The ban is the party's own state on the one clock: the counter refuses the party by name until it runs
        // out, and serves it again from that moment, with one sword fewer on the shelf.
        ServiceResult barred = services.Open(rule.Service);
        Assert.Equal("service-barred", barred.Code);
        Assert.Contains("24 hour(s)", barred.Message, StringComparison.Ordinal);
        clock.Advance(GameDuration.FromHours(23));
        Assert.Equal("service-barred", services.Open(rule.Service).Code);
        clock.Advance(GameDuration.FromHours(1));
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal(1, rule.StockOf(services, "stock:sword"));
        Assert.Empty(party.Bans.All);

        // A theft the rule refuses moves nothing.
        rule.Thieves = _ => new Refusal("test-no-hand", "Tester has not learned to steal.");
        ServiceResult refused = services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", Member: 0));
        Assert.Equal("test-no-hand", refused.Code);
        Assert.Equal(1, rule.StockOf(services, "stock:sword"));
        Assert.Equal(300, party.Debts.OwedOn("fine"));
        Assert.Equal("service-no-such-member", services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", Member: 3)).Code);
    }

    [Fact]
    public void A_repayment_takes_what_the_rule_quotes_off_what_the_party_owes_and_never_more()
    {
        using PartyEntity party = Party(coins: 100);
        party.Debts.Owe("fine", 250);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Repay] })
        {
            Offerings = [new ServiceOffer(ServiceOfferKind.Debt, "The fine", "fine", Amount: 250)],
            // The rule's quote is what the purse would hand over toward the debt: no more than it holds.
            Price = request => ServiceQuote.Charging(Math.Min(request.Subject.Count, request.Party.Purse.Coins)),
        };
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal(100, services.Browse()!.Offers.Single().Price);

        ServiceResult paid = services.Transact(new ServiceCommand(ServiceOperationKind.Repay, "fine", Count: 250));
        Assert.True(paid.IsApplied, paid.Message);
        Assert.Equal(100, paid.Paid);
        Assert.Equal(0, party.Purse.Coins);
        Assert.Equal(150, party.Debts.OwedOn("fine"));

        // A repayment of nothing pays nothing off, and one the quote makes larger than what is owed is refused whole.
        Assert.Equal("service-count-invalid", services.Transact(new ServiceCommand(ServiceOperationKind.Repay, "fine", Count: 0)).Code);
        party.Purse.Credit(500);
        ServiceResult over = services.Transact(new ServiceCommand(ServiceOperationKind.Repay, "fine", Count: 400));
        Assert.Equal("service-debt-exceeded", over.Code);
        Assert.Equal(500, party.Purse.Coins);
        Assert.Equal(150, party.Debts.OwedOn("fine"));

        ServiceResult cleared = services.Transact(new ServiceCommand(ServiceOperationKind.Repay, "fine", Count: 150));
        Assert.True(cleared.IsApplied);
        Assert.Equal(0, party.Debts.OwedOn("fine"));
        Assert.Empty(party.Debts.All);
    }

    [Fact]
    public void A_theft_from_a_person_is_carried_out_by_the_same_step_a_counter_s_is()
    {
        using PartyEntity party = Party(coins: 10);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop())
        {
            Theft = request => request.Person is null
                ? ServiceTheft.Refused(new Refusal("test-not-a-person", "Nobody is here."))
                : ServiceTheft.Tried(
                    caught: false,
                    taken: false,
                    coins: 12,
                    items: [new ItemDefinitionId("ring")],
                    marked: false,
                    fine: 0,
                    account: "fine",
                    deed: "lifted",
                    ban: GameDuration.None,
                    message: "Tester lifts a purse unseen."),
        };
        PartyProgression progression = new(new DeedStanding(), party);
        PartyServices services = new(rule, party, accounts, Clock(), progression);
        PlacementDefinition stranger = new(
            new PlacementContentId("person", "stranger"),
            "placements",
            0,
            PlacePose.Origin,
            new ContentEntry("stranger", JsonDocument.Parse("""{ "id": "stranger", "kind": "person" }""").RootElement));

        Assert.Single(services.ThievesFrom(CounterPlace, stranger));

        // No counter is open, and none is needed: the coins come through the one ledger, the thing lifted goes into
        // the shared pack, and the deed reaches the world's opinion as a counter's theft does.
        ServiceResult lifted = services.StealFrom(CounterPlace, stranger, 0);
        Assert.True(lifted.IsApplied, lifted.Message);
        Assert.Equal(12, lifted.Earned);
        Assert.Equal(22, party.Purse.Coins);
        Assert.False(party.Inventory.Items.Single().State.IsStolen);
        Assert.Equal("lifted", progression.LastDeed!.Source);
        Assert.Same(lifted, services.Last);
        Assert.False(services.IsOpen);

        Assert.Equal("service-no-such-member", services.StealFrom(CounterPlace, stranger, 2).Code);

        // A session with no progression owner could not tell the world of the deed, so it is refused before anything is drawn.
        PartyServices untold = new(rule, party, accounts, Clock());
        Assert.Equal("service-no-progression", untold.StealFrom(CounterPlace, stranger, 0).Code);
        Assert.Equal(22, party.Purse.Coins);
    }

    /// <summary>A standing rule under which every deed lowers the world's opinion by two.</summary>
    private sealed class DeedStanding : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 0;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) =>
            request.Event == ProgressionEventKind.Deed ? new ProgressionStanding(-2, 0) : ProgressionStanding.None;
    }

    /// <summary>
    /// A counter that offers one of every kind the mechanism serves, open to a wounded, cursed, and poisoned party
    /// with a thousand coins, over the session's one clock and rest.
    /// </summary>
    private sealed class OpenCounter : IDisposable
    {
        internal OpenCounter()
        {
            Party = ServiceTests.Party(coins: 1000);
            PartyResourceLedger accounts = new(Party);
            Member = Party.Members[0];
            Member.Conditions.Apply(new ActiveCondition(new ConditionId("Cursed"), 3));
            Member.Conditions.Apply(new ActiveCondition(new ConditionId("Poisoned"), 1));
            Member.Resources.TakeDamage(6);
            Clock = ServiceTests.Clock();

            ShopRule rule = new(Shop() with
            {
                Operations =
                [
                    ServiceOperationKind.Buy,
                    ServiceOperationKind.Teach,
                    ServiceOperationKind.Cure,
                    ServiceOperationKind.Train,
                    ServiceOperationKind.Provision,
                    ServiceOperationKind.Stay,
                    ServiceOperationKind.Deposit,
                    ServiceOperationKind.Withdraw,
                    ServiceOperationKind.Fare,
                ],
            })
            {
                Offerings =
                [
                    new ServiceOffer(ServiceOfferKind.Cure, "Healing", "affliction", Value: 20, Clears: [new ConditionId("Cursed")], Leaves: [new ConditionId("Marked")]),
                    new ServiceOffer(ServiceOfferKind.Training, "Training", Value: 0, Limit: 2),
                    new ServiceOffer(ServiceOfferKind.Provision, "Food and drink", Value: 8, Amount: 6),
                    new ServiceOffer(ServiceOfferKind.Stay, "A room for the night", Value: 12, Amount: 8, Clears: [new ConditionId("Tired")]),
                    new ServiceOffer(ServiceOfferKind.Holding, "The counter's keeping", "vault", Value: 0),
                    new ServiceOffer(ServiceOfferKind.Fare, "A passage to Elsewhere", "9", Value: 25, Amount: 3, Route: "coach"),
                    new ServiceOffer(ServiceOfferKind.Notice, "Travellers speak of the roads to Elsewhere."),
                ],
            };

            PartyRest rest = new(new TestNights(), Party, Clock, new TestRoom(), accounts);
            Clock.Observe(rest);
            Services = new PartyServices(rule, Party, accounts, Clock, new PartyProgression(new TrainingRule(), Party), rest);
            Assert.True(Services.Open(rule.Service).IsApplied);
        }

        internal PartyEntity Party { get; }

        internal PartyMember Member { get; }

        internal GameClock Clock { get; }

        internal PartyServices Services { get; }

        public void Dispose() => Party.Dispose();
    }

    /// <summary>
    /// A counter with two offers of one kind is refused rather than guessed at when a command names neither.
    /// </summary>
    [Fact]
    public void A_command_that_names_nothing_at_a_counter_with_two_offerings_is_refused()
    {
        using PartyEntity party = Party(coins: 100);
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Fare] })
        {
            Offerings =
            [
                new ServiceOffer(ServiceOfferKind.Fare, "A passage north", "8", Value: 25, Amount: 2, Route: "coach"),
                new ServiceOffer(ServiceOfferKind.Fare, "A passage south", "9", Value: 25, Amount: 3, Route: "boat"),
            ],
        };

        PartyServices services = new(rule, party, new PartyResourceLedger(party), Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal("service-offer-ambiguous", services.Transact(new ServiceCommand(ServiceOperationKind.Fare)).Code);

        // Naming one takes that one, and the ticket says which journey it is.
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Fare, "9")).IsApplied);
        Assert.Equal("boat", party.Passages.RouteTo(new PlaceId("9")));
        Assert.False(party.Passages.Holds(new PlaceId("8")));
        Assert.Equal(75, party.Purse.Coins);
    }

    /// <summary>A fare offered on no route is refused before anything is settled, because no ticket could name it.</summary>
    [Fact]
    public void A_fare_offered_on_no_route_is_refused_and_charges_nothing()
    {
        using PartyEntity party = Party(coins: 100);
        ShopRule rule = new(Shop() with { Operations = [ServiceOperationKind.Fare] })
        {
            Offerings = [new ServiceOffer(ServiceOfferKind.Fare, "A passage north", "8", Value: 25, Amount: 2)],
        };

        PartyServices services = new(rule, party, new PartyResourceLedger(party), Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal(ServiceCodes.ServiceFareUnrouted, services.Transact(new ServiceCommand(ServiceOperationKind.Fare, "8")).Code);
        Assert.False(party.Passages.Holds(new PlaceId("8")));
        Assert.Equal(100, party.Purse.Coins);
    }

    [Fact]
    public void Stock_depletes_and_refreshes_when_the_clock_passes_the_interval()
    {
        using PartyEntity party = Party(coins: 1000);
        PartyResourceLedger accounts = new(party);
        GameClock clock = Clock();
        ShopRule rule = new(Shop(swords: 2, refresh: GameDuration.FromHours(24 * 7)));
        PartyServices services = new(rule, party, accounts, clock);
        Assert.True(services.Open(rule.Service).IsApplied);

        Assert.Equal(2, rule.StockOf(services, "stock:sword"));
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword")).IsApplied);
        Assert.Equal(1, rule.StockOf(services, "stock:sword"));
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword")).IsApplied);

        // Sold out: the line stays on the shelves at zero so the shop can say so, and the next purchase is
        // refused by name rather than quietly finding nothing.
        Assert.Equal(0, rule.StockOf(services, "stock:sword"));
        Assert.Equal("service-out-of-stock", services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword")).Code);

        // Six days on, the schedule has not come due and the shelves are still bare.
        services.Observe(clock.Advance(GameDuration.FromHours(24 * 6)));
        Assert.Equal(0, rule.StockOf(services, "stock:sword"));

        // The seventh day brings the deadline due, and the shelves are full again: the restock is the
        // clock's own deadline rather than a count of frames nobody could point at.
        services.Observe(clock.Advance(GameDuration.FromHours(24)));
        Assert.Equal(2, rule.StockOf(services, "stock:sword"));

        // A second interval is a second restock, so the schedule repeats rather than firing once.
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword")).IsApplied);
        services.Observe(clock.Advance(GameDuration.FromHours(24 * 7)));
        Assert.Equal(2, rule.StockOf(services, "stock:sword"));
    }

    [Fact]
    public void The_price_is_the_policy_s_own_and_the_arithmetic_is_the_kit_s()
    {
        using PartyEntity party = Party(coins: 1000);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop(swordWorth: 100));
        // A rule that prices by the party's own standing: the same shelf line costs a reputable party less.
        rule.Price = request => request.Operation == ServiceOperationKind.Buy
            ? ServiceQuote.Charging(request.Party.Reputation.Reputation >= 10 ? 80 : 120, request.Subject.Value)
            : ServiceQuote.Free;
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal(120, rule.PriceOf(services, "stock:sword"));
        Assert.Equal(120, services.Transact(new ServiceCommand(ServiceOperationKind.Buy, "stock:sword")).Paid);

        // The same service over a party the world thinks better of: nothing about the counter changed.
        using PartyEntity reputable = Party(coins: 1000, reputation: 20);
        PartyServices favoured = new(rule, reputable, new PartyResourceLedger(reputable), Clock());
        Assert.True(favoured.Open(rule.Service).IsApplied);
        Assert.Equal(80, rule.PriceOf(favoured, "stock:sword"));

        // And a second rule over the same content prices it differently again, which is the seam: the
        // numbers are the ruleset's and the mechanism carries whatever it is handed.
        ShopRule flat = new(Shop(swordWorth: 100));
        flat.Price = request => request.Operation == ServiceOperationKind.Buy
            ? ServiceQuote.Charging(7, request.Subject.Value)
            : ServiceQuote.Free;
        PartyServices cheap = new(flat, party, accounts, Clock());
        Assert.True(cheap.Open(flat.Service).IsApplied);
        Assert.Equal(7, flat.PriceOf(cheap, "stock:sword"));

        // The kit's own arithmetic is one place: a multiplier truncates to whole coins, a price that would
        // be nothing is one coin, and a percentage is applied in whole coins.
        Assert.Equal(13, ServicePricing.Coins(7, 1.9));
        Assert.Equal(1, ServicePricing.Coins(3, 0.1));
        Assert.Equal(0, ServicePricing.Coins(0, 5));
        Assert.Equal(90, ServicePricing.Percent(100, 10));
        Assert.Equal(110, ServicePricing.Percent(100, -10));
        Assert.Equal(12, ServicePricing.Share(120, 10));
    }

    [Fact]
    public void Membership_is_party_carried_state_the_service_checks()
    {
        ServiceLesson membership = new(ServiceLessonKind.Membership, "guild.fire", 1, 50, "Fire Guild membership");
        ServiceLesson lesson = new(ServiceLessonKind.Skill, "Fire", 1, 25, "Basic Fire");
        using PartyEntity party = Party(coins: 1000);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop(membership: "guild.fire", lessons: [membership, lesson]));
        // The guild serves members only — except at the counter that sells the membership itself.
        rule.Eligibility = request =>
        {
            bool joins = request.Operation == ServiceOperationKind.Teach &&
                request.Subject.Lesson is { Kind: ServiceLessonKind.Membership };
            return joins || request.Party.Memberships.Holds("guild.fire")
                ? ServiceEligibility.Allowed
                : ServiceEligibility.Refused(new Refusal("service-membership-required", "The Fire Guild serves members only."));
        };
        PartyServices services = new(rule, party, accounts, Clock(), new PartyProgression(new TrainingRule(), party));
        Assert.True(services.Open(rule.Service).IsApplied);

        // Not a member: the gated operation names what stopped it, and the membership lesson does not.
        Assert.Equal("service-membership-required", services.Transact(new ServiceCommand(ServiceOperationKind.Teach, "Fire")).Code);
        Assert.Empty(services.Browse()!.Memberships);

        // Buying the membership is one lesson, and it grants the band the membership: the same state the
        // access check reads, so buying one and being one cannot disagree.
        ServiceResult joined = services.Transact(new ServiceCommand(ServiceOperationKind.Teach, "guild.fire"));
        Assert.True(joined.IsApplied);
        Assert.True(party.Memberships.Holds("guild.fire"));
        Assert.Equal(50, joined.Paid);
        // What the membership reads as is the ruleset's word for it; the mechanism carries whatever the
        // access answer states.
        Assert.Equal(["guild.fire"], services.Browse()!.Memberships);

        // Now the same operation the guild refused is served.
        Assert.True(services.Transact(new ServiceCommand(ServiceOperationKind.Teach, "Fire")).IsApplied);
        Assert.Equal(1, party.Members[0].Skills.LevelOf(new SkillId("Fire")));
    }

    [Fact]
    public void A_counter_that_states_hours_is_shut_outside_them()
    {
        using PartyEntity party = Party(coins: 1000);
        ShopRule rule = new(Shop());
        // The clock stands at nine in the morning, inside the shop's 6 to 18 window.
        PartyServices services = new(rule, party, new PartyResourceLedger(party), Clock(new GameDate(1168, 1, 1, 9, 0, 0)));
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal("open", services.State);

        // A party that walks up at two in the morning is turned away by name, with the hours it keeps.
        PartyServices night = new(rule, party, new PartyResourceLedger(party), Clock(new GameDate(1168, 1, 1, 2, 0, 0)));
        ServiceResult shut = night.Open(rule.Service)!;
        Assert.False(shut.IsApplied);
        Assert.Equal("service-closed", shut.Code);
        Assert.Contains("06:00–18:00", shut.Message, StringComparison.Ordinal);
        Assert.False(night.IsOpen);
        Assert.Equal("closed", night.State);

        // A window that wraps past midnight serves through it: a tavern open 18 to 6 is open at two.
        ShopRule tavern = new(Shop() with { Hours = new ServiceHours(18, 6) });
        PartyServices nightTrade = new(tavern, party, new PartyResourceLedger(party), Clock(new GameDate(1168, 1, 1, 2, 0, 0)));
        Assert.True(nightTrade.Open(tavern.Service).IsApplied);

        // A service with no clock cannot be known to be open, so it says so rather than assuming.
        ShopRule untimed = new(Shop());
        PartyServices timeless = new(untimed, party, new PartyResourceLedger(party), clock: null);
        Assert.Equal("service-no-clock", timeless.Open(untimed.Service)!.Code);
    }

    [Fact]
    public void A_session_talks_its_way_into_a_counter_buys_and_leaves()
    {
        using PartyEntity party = Party(coins: 500);
        using Counter counter = Counter.Build(new ShopRule(Shop(refresh: GameDuration.FromHours(1))), party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(counter.Clock),
            new SessionParty.Playing(World: counter.World, Party: party, Accounts: counter.Accounts),
            rules: new SessionRules
            {
                Service = counter.Rule,
                Conversation = new CounterConversation(counter.Rule),
            },
            controls: new SessionControls
            {
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
            });
        // A session that was never started admits no interval, so the world would never be stepped at all;
        // starting it is what makes the next updates the session's own admitted time.
        session.Start();

        // The projection that exists before any update says the session holds a service mechanism and that
        // the party stands at no counter yet.
        ProjectedNode before = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.True(before.Field("available").AsBoolean());
        Assert.False(before.Field("open").AsBoolean());
        Assert.Equal("none", before.Field("outcome").AsString());

        // The first update puts the service placement in front of the party, and the press uses it: the
        // interaction mechanism reached the person, and the conversation with them is what opened.
        session.Update(Admitted.Update(1, 1));
        // What the reticle shows is the interaction answers' own word for the target, which in this test is
        // the counter rather than a person: the kit holds no kind of thing, so a ruleset that names a person
        // there names one and a ruleset that names a counter names that.
        Assert.Equal("The Sword and Shield, kept by Bertram", channel.Latest().Field(SessionProjection.InteractionField).Field("label").AsString());
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.use", InputEdge.Pressed)));
        ProjectedNode talking = channel.Latest().Field(SessionProjection.ConversationField);
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Bertram", talking.Field("speaker").AsString());

        // What the keeper offers hands the party to the counter, which is the one mechanism that serves it:
        // the conversation names the owner and the session routes it, so the counter's own hours and prices
        // are read in one place rather than two.
        Assert.Equal("counter", talking.Field("topics").Item(0).Field("id").AsString());
        session.Update(Admitted.Update(3, 1, Admitted.Payload(ConversationControls.ActionContract, """{"action":"conversation.topic","target":"counter"}""")));
        Assert.False(channel.Latest().Field(SessionProjection.ConversationField).Field("open").AsBoolean());
        ProjectedNode opened = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.True(opened.Field("open").AsBoolean());
        Assert.Equal("applied", opened.Field("outcome").AsString());
        Assert.Equal("The Sword and Shield", opened.Field("name").AsString());
        Assert.Equal("open", opened.Field("state").AsString());

        // What the counter offers is browsable: the shelf with its price, the lesson with its fee, and the
        // party's own items with what the counter would pay.
        ProjectedNode shop = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.Equal(100, shop.Field("stock").Item(0).Field("price").AsNumber());
        Assert.Equal(2, shop.Field("stock").Item(0).Field("count").AsNumber());
        Assert.Equal(25, shop.Field("lessons").Item(0).Field("price").AsNumber());
        Assert.Equal("Sword", shop.Field("lessons").Item(0).Field("subject").AsString());
        Assert.Equal(0, shop.Field("sales").Length());

        // A purchase the screen asked for arrives on the declared contract, moves the purse, and is part of
        // the same projection the command arrived in.
        session.Update(Admitted.Update(4, 1, Admitted.Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":1}""")));
        ProjectedNode bought = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.Equal("buy", bought.Field("action").AsString());
        Assert.Equal("applied", bought.Field("outcome").AsString());
        Assert.Equal(100, bought.Field("paid").AsNumber());
        Assert.Equal(400, bought.Field("coins").AsNumber());
        Assert.Equal(400, party.Purse.Coins);
        Assert.Equal(1, bought.Field("stock").Item(0).Field("count").AsNumber());

        // A refusal the party cannot cover is reported with its own code and moves nothing.
        session.Update(Admitted.Update(5, 1, Admitted.Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":2}""")));
        ProjectedNode refused = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-not-enough-stock", refused.Field("code").AsString());
        Assert.Equal(400, refused.Field("coins").AsNumber());

        // A command that names nothing this counter has is refused by name rather than doing nothing.
        session.Update(Admitted.Update(6, 1, Admitted.Payload(ServiceControls.ActionContract, """{"action":"service.sell","target":"99"}""")));
        Assert.Equal("service-no-such-item", channel.Latest().Field(SessionProjection.ServiceField).Field("code").AsString());

        // The leave control the host declared ends the visit, and the panel says the party walked away.
        session.Update(Admitted.Update(7, 1, Admitted.Digital("test.service.leave", InputEdge.Pressed)));
        ProjectedNode left = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.False(left.Field("open").AsBoolean());
        Assert.Equal("leave", left.Field("action").AsString());
        Assert.Equal("applied", left.Field("outcome").AsString());
        Assert.Null(session.Services!.Visit);

        // The counter's shelf holds a restock deadline on the one clock from the first visit on. The counter
        // rebuilds its shelf on load, so a save after a visit is taken rather than refused for the rest of the
        // session.
        Assert.True(counter.Clock.PendingDeadlines > 0);
        Assert.NotNull(session.Capture());
    }

    [Fact]
    public void A_passage_the_panel_buys_is_boarded_in_the_update_that_bought_it()
    {
        using PartyEntity party = Party(coins: 500);
        using Counter counter = Counter.Build(new ShopRule(Shop() with { Operations = [ServiceOperationKind.Fare] }), party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(counter.Clock),
            new SessionParty.Playing(World: counter.World, Party: party, Accounts: counter.Accounts),
            rules: new SessionRules
            {
                Service = counter.Rule,
                Conversation = new CounterConversation(counter.Rule),
            },
            controls: new SessionControls
            {
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
            });
        session.Start();

        // The counter sells one passage, to the town the world's own fare reaches.
        counter.Rule.Offerings = [new ServiceOffer(ServiceOfferKind.Fare, "A passage to Elsewhere", "2", Value: 25, Amount: 3, Route: "coach")];

        // Walk in through the person who keeps the counter, exactly as the product reaches one.
        session.Update(Admitted.Update(1, 1));
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.use", InputEdge.Pressed)));
        session.Update(Admitted.Update(3, 1, Admitted.Payload(ConversationControls.ActionContract, """{"action":"conversation.topic","target":"counter"}""")));
        Assert.True(session.Services!.IsOpen);

        // What the counter offers besides goods and lessons is published with what each would cost, and a
        // passage carries the place it reaches, which is what the screen sends back when a player presses it.
        ProjectedNode passages = channel.Latest().Field(SessionProjection.ServiceField).Field("offers");
        Assert.Equal(1, passages.Length());
        Assert.Equal("fare", passages.Item(0).Field("kind").AsString());
        Assert.Equal("2", passages.Item(0).Field("subject").AsString());
        Assert.Equal("A passage to Elsewhere", passages.Item(0).Field("name").AsString());
        Assert.Equal(3, passages.Item(0).Field("amount").AsNumber());
        Assert.Equal(25, passages.Item(0).Field("price").AsNumber());
        Assert.Equal(CounterPlace, counter.World.Place);

        session.Update(Admitted.Update(4, 1, Admitted.Payload(ServiceControls.ActionContract, """{"action":"service.fare","target":"2"}""")));

        // The counter settled the fare and the road honoured it: the party is at the town the passage named
        // through the world's own transition path, and the counter it was bought at is no longer open in
        // front of it. What the journey then costs — the days on the ticket and the tearing of it — is the
        // cost rule's answer, which this fixture's free rule answers with nothing and the ruleset's own
        // suite proves against this game's fares.
        Assert.Equal(Counter.Elsewhere, counter.World.Place);
        Assert.Equal(new PlacePose(40, 50, 0, 0, 0), counter.World.Party.PlacePose);
        Assert.False(session.Services.IsOpen);
        Assert.Equal(475, party.Purse.Coins);

        // The panel reads the same facts: what the counter did, and where the party now stands.
        Assert.Equal("fare", channel.Latest().Field(SessionProjection.ServiceField).Field("action").AsString());
        Assert.Equal("applied", channel.Latest().Field(SessionProjection.ServiceField).Field("outcome").AsString());
    }

    [Fact]
    public void A_key_released_while_a_screen_is_open_does_not_walk_the_party_when_it_closes()
    {
        using PartyEntity party = Party(coins: 500);
        using Counter counter = Counter.Build(new ShopRule(Shop()), party, walking: true);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(counter.Clock),
            new SessionParty.Playing(World: counter.World, Party: party, Accounts: counter.Accounts),
            rules: new SessionRules
            {
                Service = counter.Rule,
                Conversation = new CounterConversation(counter.Rule),
            },
            controls: new SessionControls
            {
                Movement = new MovementInput(MovementControls, turnRatePerSecond: 2048),
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
            });
        session.Start();

        // Forward goes down and the party walks; then it talks to the keeper with the key still down.
        session.Update(Admitted.Update(1, 1, Admitted.Digital("test.move-forward", InputEdge.Pressed)));
        session.Update(Admitted.Update(2, 0));
        session.Update(Admitted.Update(3, 1, Admitted.Digital("test.use", InputEdge.Pressed)));
        Assert.True(channel.Latest().Field(SessionProjection.ConversationField).Field("open").AsBoolean());
        PlacePose standing = counter.World.Party.PlacePose;

        // The key comes up while the conversation owns the controls, and the conversation is left: the release
        // was read even though nothing walked, so the party stands where it stood.
        session.Update(Admitted.Update(4, 1, Admitted.Digital("test.move-forward", InputEdge.Released)));
        session.Update(Admitted.Update(5, 1, Admitted.Digital("test.conversation.leave", InputEdge.Pressed)));
        Assert.False(channel.Latest().Field(SessionProjection.ConversationField).Field("open").AsBoolean());
        session.Update(Admitted.Update(6, 1));
        session.Update(Admitted.Update(7, 1));
        Assert.Equal(standing, counter.World.Party.PlacePose);
    }

    [Fact]
    public void A_visit_holds_the_party_still_while_the_world_keeps_its_clock()
    {
        using PartyEntity party = Party(coins: 500);
        // One admitted step is twenty-four game minutes at this clock's rate, so a shelf that refreshes
        // every three game hours comes due a few steps into the visit rather than on every one of them.
        using Counter counter = Counter.Build(new ShopRule(Shop(refresh: GameDuration.FromHours(3))), party, walking: true);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(counter.Clock),
            new SessionParty.Playing(World: counter.World, Party: party, Accounts: counter.Accounts),
            rules: new SessionRules
            {
                Service = counter.Rule,
                Conversation = new CounterConversation(counter.Rule),
            },
            controls: new SessionControls
            {
                Movement = new MovementInput(MovementControls, turnRatePerSecond: 2048),
                Use = UseControls,
                Service = ServiceControls,
                Conversation = ConversationControls,
            });
        session.Start();

        // Before any counter is open, a held forward control walks the party, which is what makes the next
        // assertion mean something.
        session.Update(Admitted.Update(1, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        PlacePose walked = counter.World.Party.PlacePose;
        Assert.NotEqual(0, walked.Y);

        session.Update(Admitted.Update(2, 0));
        session.Update(Admitted.Update(3, 1, Admitted.Digital("test.use", InputEdge.Pressed)));

        // Talking stops the party where it stands, exactly as standing at a counter does: the conversation is
        // a screen that owns the player's controls, so a held forward control cannot walk away from it.
        Assert.True(channel.Latest().Field(SessionProjection.ConversationField).Field("open").AsBoolean());
        session.Update(Admitted.Update(4, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        Assert.True(session.Services!.IsOpen == false);
        Assert.Equal(walked, counter.World.Party.PlacePose);
        session.Update(Admitted.Update(5, 1, Admitted.Payload(ConversationControls.ActionContract, """{"action":"conversation.topic","target":"counter"}""")));
        Assert.True(session.Services!.IsOpen);
        Assert.Equal(2, session.Services.Browse()!.Stock[0].Count);

        // At the counter the party does not step: the shop owns the player's controls, so a held forward
        // control walks nowhere and the visit cannot be left by accident. The world keeps its own time.
        double before = session.SimulationSeconds;
        session.Update(Admitted.Update(6, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(walked, counter.World.Party.PlacePose);
        Assert.True(session.SimulationSeconds > before);

        // Buying the shelf out leaves it empty, and it stays empty while the schedule has not come due.
        session.Update(Admitted.Update(7, 1, Admitted.Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":2}""")));
        Assert.Equal(0, session.Services.Browse()!.Stock[0].Count);
        for (ulong step = 8; step <= 11; step++) session.Update(Admitted.Update(step, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(0, session.Services.Browse()!.Stock[0].Count);
        Assert.Equal(walked, counter.World.Party.PlacePose);

        // The hour the shelves refresh on passes in the same clock the street reads, so the schedule is game
        // time rather than a loop: the shelf is full again while the visit is still open.
        session.Update(Admitted.Update(12, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(2, session.Services.Browse()!.Stock[0].Count);

        // Leaving gives the controls back: the same held control walks the party again.
        session.Update(Admitted.Update(13, 1, Admitted.Digital("test.service.leave", InputEdge.Pressed)));
        Assert.False(session.Services.IsOpen);
        session.Update(Admitted.Update(14, 1, Admitted.Digital("test.move-forward", InputEdge.Held)));
        Assert.NotEqual(walked, counter.World.Party.PlacePose);
    }

    /// <remarks>
    /// One admitted second is one game day, so a test that steps a few seconds crosses the deadlines it states
    /// without measuring a real day of engine time.
    /// </remarks>
    private static GameClock Clock(GameDate? start = null) => TestClock.Create(86_400, start);

    /// <summary>A party that carries what a test gives it, with a real purse, pack, and members.</summary>
    private static PartyEntity Party(int coins = 0, int reputation = 0) =>
        new PartyEntityFactory().Create(
            new PartyCreation(
                [
                    new MemberCreation(new PartyMemberSeed(
                        "Tester",
                        new RaceId("testfolk"),
                        new ClassId("fighter"),
                        [new AttributeScore(new AttributeId("vigour"), 12)],
                        skills: [],
                        spells: [],
                        experience: 0,
                        level: 1,
                        skillPoints: 0,
                        classRank: 1,
                        conditions: [],
                        hitPoints: ResourcePool.Full(10),
                        spellPoints: ResourcePool.Full(5))),
                ],
                coins,
                foodPortions: 4,
                ProvisionUnit.Portions,
                reputation,
                fame: 0));

    /// <summary>
    /// The rules a test states: what a counter is, what its shelves hold, who it serves, and what it charges.
    /// </summary>
    /// <remarks>
    /// The default answers are the plain ones — content's own shelves, no gate, and a price equal to the
    /// base value — so a test that wants to prove a seam changes exactly that answer and nothing else.
    /// </remarks>
    /// <summary>
    /// The progression policy this counter trains through: every level is already earned, and a level gives
    /// nothing.
    /// </summary>
    /// <remarks>
    /// What is under test here is the service mechanism, so the curve is the cheapest one a rule can state
    /// and the growth is empty: the level's own consequences are the progression owner's suite's subject.
    /// </remarks>
    private sealed class TrainingRule : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 0;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }

    private sealed class ShopRule : IServiceRule
    {
        internal ShopRule(ServiceDefinition service) => Service = service;

        internal ServiceDefinition Service { get; }

        /// <summary>What the counter charges, or the plain price when a test states none.</summary>
        internal Func<ServiceQuoteRequest, ServiceQuote>? Price { get; set; }

        /// <summary>Whether a party is served at all, or everybody when a test states nothing.</summary>
        internal Func<ServiceEligibilityRequest, ServiceEligibility>? Eligibility { get; set; }

        public ServiceDefinition? Describe(ServiceTargetRequest request) =>
            string.Equals(request.Placement.Content.Kind, "service", StringComparison.Ordinal) &&
            string.Equals(request.Placement.Content.Id, Service.Id.Value, StringComparison.Ordinal)
                ? Service
                : null;

        public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request) => Service.Stock;

        public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request) => Service.Lessons;

        /// <summary>What this counter offers besides goods and lessons, which a test states as its own list.</summary>
        internal IReadOnlyList<ServiceOffer> Offerings { get; set; } = [];

        public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request) => Offerings;

        public IReadOnlyList<string> Access(ServiceAccessRequest request) =>
            Service.Membership.Length > 0 && request.Party.Memberships.Holds(Service.Membership)
                ? [Service.Membership]
                : [];

        public ServiceEligibility Judge(ServiceEligibilityRequest request) =>
            Eligibility?.Invoke(request) ?? ServiceEligibility.Allowed;

        public ServiceQuote Quote(ServiceQuoteRequest request) => Price?.Invoke(request) ?? request.Operation switch
        {
            ServiceOperationKind.Buy => ServiceQuote.Charging(request.Subject.TotalValue, request.Subject.Value),
            // What a shop pays for an item is the ruleset's answer over an item table the kit does not read,
            // so this counter pays a flat hundred and the ruleset's own suite proves the table-driven price.
            ServiceOperationKind.Sell => ServiceQuote.Paying(100, 100),
            ServiceOperationKind.Identify => ServiceQuote.Charging(25),
            ServiceOperationKind.Repair => ServiceQuote.Charging(request.Subject.Item!.State.Damage * 10),
            // A bank's two directions are the same coins: what is deposited leaves the purse and what is
            // withdrawn is paid back into it, both counted by the command rather than priced by a base.
            ServiceOperationKind.Deposit => ServiceQuote.Charging(request.Subject.Count, request.Subject.Count),
            ServiceOperationKind.Withdraw => ServiceQuote.Paying(request.Subject.Count, request.Subject.Count),
            _ => ServiceQuote.Charging(request.Subject.Value, request.Subject.Value),
        };

        /// <summary>What a theft here comes to, or null when this counter keeps nothing a thief is tried for.</summary>
        internal Func<ServiceTheftRequest, ServiceTheft>? Theft { get; set; }

        /// <summary>Which members may try, or every member when a test states nothing.</summary>
        internal Func<ServiceTheftRequest, Refusal?>? Thieves { get; set; }

        public Refusal? JudgeTheft(ServiceTheftRequest request) =>
            Theft is null
                ? new Refusal("test-no-theft", "This counter keeps nothing a thief is tried for.")
                : Thieves?.Invoke(request);

        public ServiceTheft Steal(ServiceTheftRequest request) =>
            Theft?.Invoke(request) ?? ServiceTheft.Refused(JudgeTheft(request)!);

        /// <summary>What one lot of an open counter holds, or null when the shelves hold none of it.</summary>
        internal int? StockOf(PartyServices services, string lot) =>
            services.Browse()!.Stock.Where(offer => offer.Lot.Value == lot).Select(offer => (int?)offer.Count).FirstOrDefault();

        /// <summary>What one lot of an open counter costs, or zero when the shelves hold none of it.</summary>
        internal int PriceOf(PartyServices services, string lot) =>
            services.Browse()!.Stock.Where(offer => offer.Lot.Value == lot).Select(offer => offer.Price).FirstOrDefault();
    }

    /// <summary>
    /// A hall holding one service counter, the party that walks in, and the clock its hours and schedule
    /// are judged against.
    /// </summary>
    private sealed class Counter : IDisposable
    {
        private Counter(SessionWorld world, ShopRule rule, GameClock clock, PartyResourceLedger accounts)
        {
            World = world;
            Rule = rule;
            Clock = clock;
            Accounts = accounts;
        }

        internal SessionWorld World { get; }

        internal ShopRule Rule { get; }

        internal GameClock Clock { get; }

        internal PartyResourceLedger Accounts { get; }

        internal static Counter Build(ShopRule rule, PartyEntity party, bool walking = false)
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json", TestPacks.World)
                    .Add("packs/world/places.json", Document())
                    .Add("packs/world/links.json", Links()),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog, ThreeDayCoach.Instance);
            GameClock clock = Clock();
            PartyPoseOwner owner = new(
                // The party faces the counter, which stands along the place's second ground axis.
                new PartyPose(CounterPlace, new PlacePose(0, 0, 0, 0, 0)),
                new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

            // A world's interaction answers are the ruleset's, and here the ruleset is the test's: a
            // placement of kind 'service' is a person the party talks to, which is exactly what the game's
            // own service answers make of it.
            InteractionPolicy policy = new(
                new CounterInteraction(rule),
                PlaceSpace.HeightIsThird(new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512), radiansAtZeroFacing: 0),
                new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31));

            PartyResourceLedger accounts = new(party);
            SessionWorld world = new(
                graph,
                owner,
                new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new FreeTravel(),
                time: clock,
                mover: walking ? new WalkingMover(owner) : null,
                clock: clock,
                resources: accounts,
                partyEntity: party,
                interaction: policy);
            return new Counter(world, rule, clock, accounts);
        }

        public void Dispose() => World.Dispose();

        /// <summary>The town a passage sold at this counter reaches, which is a fare the world honours.</summary>
        internal static readonly PlaceId Elsewhere = new("2");

        private static string Document() =>
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "The Sword and Shield", "respawnDays": 3,
                "entryPoints": [ { "id": "Door", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "sword-and-shield", "kind": "service", "x": 0, "y": 100, "z": 0 } ] },
              { "id": "2", "kind": "interior", "name": "Elsewhere", "respawnDays": 3,
                "entryPoints": [ { "id": "Door", "x": 40, "y": 50, "z": 0, "yaw": 0 } ] } ] }
            """;

        private static string Links() =>
            """
            { "documentId": "links", "definitionKind": "travel-link", "entries": [
              { "id": "coach", "fromPlace": "1", "toPlace": "2", "entryPoint": "Door", "fare": true, "route": "coach" } ] }
            """;
    }

    /// <summary>The test's own fare rule: the one coach route this suite's content names takes three days.</summary>
    private sealed class ThreeDayCoach : IFareDurationRule
    {
        internal static readonly ThreeDayCoach Instance = new();

        public int? DaysOf(PlaceId? from, PlaceId to, string route) => route == "coach" ? 3 : null;
    }

    /// <summary>The interaction answers a test's world uses: a counter is a person to talk to.</summary>
    private sealed class CounterInteraction(ShopRule rule) : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            rule.Describe(new ServiceTargetRequest(request.Place, request.Placement)) is { } service
                ? new InteractionTargetDefinition(new InteractionTargetKind("service"), service.Describe(), InteractionVerb.Talk, 512)
                : null;

        public Verdict Judge(InteractionRequirement requirement, InteractionContext context) =>
            Verdict.Met;

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            InteractionOutcome.Applied("spoken", "The party speaks with the keeper of the counter.");
    }

    /// <summary>
    /// A mover that walks the party when it is asked to, so a test can show where the party did and did not
    /// go. It holds no collision and refuses nothing: what it proves is whether the session stepped the
    /// party at all.
    /// </summary>
    private sealed class WalkingMover(PartyPoseOwner party) : IPartyMover
    {
        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            PlacePose pose = party.PlacePose;
            if (intent.IsStill)
            {
                return new MovementOutcome(pose, System.Numerics.Vector3.Zero, Grounded: true, default, CharacterBlockFlags.None, default, SurfaceEffect.Ordinary, FallOutcome.None);
            }

            PlacePose moved = pose with { Y = pose.Y + (intent.Forward * 32) + (intent.Strafe * 32) };
            party.Enter(party.Place, moved);
            return new MovementOutcome(moved, System.Numerics.Vector3.Zero, Grounded: true, default, CharacterBlockFlags.None, default, SurfaceEffect.Ordinary, FallOutcome.None);
        }

        public bool InSight(System.Numerics.Vector3 from, System.Numerics.Vector3 to) => true;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The conversation answers a test's sessions use: whoever keeps a counter greets the party and offers
    /// the counter itself, so a counter is reached the way the product reaches one — through a person.
    /// </summary>
    /// <remarks>
    /// The kit's own service tests prove the composition rather than assuming it: the interaction mechanism
    /// reaches a person, the conversation mechanism is what opens, and the one offer it makes hands the party
    /// to the service mechanism. Nothing here is a game's rule; it is the shape a ruleset supplies.
    /// </remarks>
    private sealed class CounterConversation(ShopRule rule) : IConversationRule
    {
        public ConversationSubject? Describe(ConversationTargetRequest request) =>
            rule.Describe(new ServiceTargetRequest(request.Place, request.Placement)) is { } service
                ? new ConversationSubject(request.Placement.Content.Id, [new ConversationPerson("keeper", service.Proprietor)])
                : null;

        public ConversationAnswer Greeting(ConversationContext context) =>
            new($"'Welcome to {context.Subject.Id}.'");

        public IReadOnlyList<ConversationOffer> Offers(ConversationContext context) =>
            [new ConversationOffer(new ConversationTopic("counter", "Step up to the counter"), Verdict.Met)];

        public ConversationAnswer Take(ConversationTopic topic, ConversationContext context) =>
            new("The party steps up to the counter.", handoff: new ConversationHandoff(HandoffOwner.Counter));
    }
}
