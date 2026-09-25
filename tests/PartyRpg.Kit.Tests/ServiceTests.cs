using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
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
    private static readonly UseIntentNames UseControls = new("test.use", "test.use", "test.ui.action.v1");
    private static readonly ServiceIntentNames ServiceControls = new("test.service.leave", "test.ui.action.v1");
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
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);

        // A purchase takes coin out of the party's one purse and puts the goods in its one shared pack.
        ServiceResult bought = services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword", Count: 2));
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
        ServiceResult identified = services.Transact(new ServiceCommand(ServiceCommandKind.Identify, first.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        ServiceResult repaired = services.Transact(new ServiceCommand(ServiceCommandKind.Repair, second.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.True(first.State.IsIdentified);
        Assert.Equal(0, second.State.Damage);
        Assert.Equal(25, identified.Paid);
        Assert.Equal(40, repaired.Paid);
        Assert.Equal(235, party.Purse.Coins);

        // A lesson moves the same purse and changes a member's own skill state.
        ServiceResult taught = services.Transact(new ServiceCommand(ServiceCommandKind.Teach, "Sword", Member: 0));
        Assert.True(party.Members[0].Skills.Knows(new SkillId("Sword")));
        Assert.Equal(1, party.Members[0].Skills.LevelOf(new SkillId("Sword")));
        Assert.Equal(25, taught.Paid);

        // A sale is the earning direction of the same path: the item leaves the shared pack and coin arrives.
        ServiceResult sold = services.Transact(new ServiceCommand(ServiceCommandKind.Sell, first.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
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
    public void A_refusal_names_what_stopped_it_and_changes_nothing()
    {
        using PartyEntity party = Party(coins: 40);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop());
        rule.Eligibility = request => request.Operation == ServiceOperationKind.Teach
            ? ServiceEligibility.Refused("test-not-a-member", "This counter teaches members only.")
            : ServiceEligibility.Allowed;
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);

        // Not enough coin: the party's own settlement path names the shortfall, and nothing moved.
        ServiceResult poor = services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword"));
        Assert.False(poor.IsApplied);
        Assert.Equal("purse-short", poor.Code);
        Assert.Contains("60 coin(s) short", poor.Message, StringComparison.Ordinal);
        Assert.Equal(40, party.Purse.Coins);
        Assert.Equal(2, rule.StockOf(services, "stock:sword"));

        // Not eligible: the ruleset's own refusal reaches the player unchanged.
        ServiceResult ineligible = services.Transact(new ServiceCommand(ServiceCommandKind.Teach, "Sword"));
        Assert.Equal("test-not-a-member", ineligible.Code);
        Assert.Contains("members only", ineligible.Message, StringComparison.Ordinal);

        // Out of stock and no such item are two different answers, which is why the mechanism resolves what
        // a command names before it asks policy anything.
        ServiceResult missing = services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:potion"));
        Assert.Equal("service-no-such-lot", missing.Code);
        ServiceResult absent = services.Transact(new ServiceCommand(ServiceCommandKind.Sell, "99"));
        Assert.Equal("service-no-such-item", absent.Code);

        // And an operation the counter does not offer is its own refusal rather than a silent nothing.
        ShopRule closed = new(Shop() with { Operations = [ServiceOperationKind.Buy] });
        PartyServices buying = new(closed, party, accounts, Clock());
        Assert.True(buying.Open(closed.Service).IsApplied);
        ServiceResult notOffered = buying.Transact(new ServiceCommand(ServiceCommandKind.Sell, "1"));
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
    public void A_counter_s_cures_training_provisions_rooms_deposits_and_passages_run_the_same_way()
    {
        using PartyEntity party = Party(coins: 1000);
        PartyResourceLedger accounts = new(party);
        PartyMember member = party.Members[0];
        member.Conditions.Apply(new ActiveCondition(new ConditionId("Cursed"), 3));
        member.Conditions.Apply(new ActiveCondition(new ConditionId("Poisoned"), 1));
        member.Resources.TakeDamage(6);
        GameClock clock = Clock();

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
                new ServiceOffer(ServiceOfferKind.Cure, "Healing", "affliction", Value: 20, Clears: [new ConditionId("Cursed")]),
                new ServiceOffer(ServiceOfferKind.Training, "Training", Value: 0, Limit: 2),
                new ServiceOffer(ServiceOfferKind.Provision, "Food and drink", Value: 8, Amount: 6),
                new ServiceOffer(ServiceOfferKind.Stay, "A room for the night", Value: 12, Amount: 8, Clears: [new ConditionId("Tired")]),
                new ServiceOffer(ServiceOfferKind.Holding, "The counter's keeping", "vault", Value: 0),
                new ServiceOffer(ServiceOfferKind.Fare, "A passage to Elsewhere", "9", Value: 25, Amount: 3),
                new ServiceOffer(ServiceOfferKind.Notice, "Travellers speak of the roads to Elsewhere."),
            ],
        };

        PartyServices services = new(rule, party, accounts, clock);
        Assert.True(services.Open(rule.Service).IsApplied);

        // What the counter offers is published with what each would cost, and a notice is published at no
        // price because it is read rather than taken.
        ServiceBrowse browse = services.Browse()!;
        Assert.Equal(7, browse.Offers.Count);
        Assert.Equal(20, browse.Offers.Single(line => line.Offer.Kind == ServiceOfferKind.Cure).Price);
        Assert.Equal(0, browse.Offers.Single(line => line.Offer.Kind == ServiceOfferKind.Notice).Price);

        // A cure ends exactly the conditions it claims, restores the body, and moves the one purse.
        ServiceResult cured = services.Transact(new ServiceCommand(ServiceCommandKind.Cure, "affliction", Member: 0));
        Assert.True(cured.IsApplied);
        Assert.Equal(20, cured.Paid);
        Assert.False(member.Conditions.Has(new ConditionId("Cursed")));
        Assert.True(member.Conditions.Has(new ConditionId("Poisoned")));
        Assert.Equal(10, member.Resources.HitPoints.Current);

        // Training converts what the member has banked into one level, and the counter's own ceiling is
        // where it stops: a step past it is refused rather than clamped.
        ServiceResult trained = services.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.True(trained.IsApplied);
        Assert.Equal(2, member.Progression.Level);
        ServiceResult capped = services.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.Equal("service-training-capped", capped.Code);
        Assert.Equal(2, member.Progression.Level);

        // Provisions are the party's larder rather than its pack, and the larder is credited where a
        // purchase would mint an item.
        int larder = party.Food.Portions;
        ServiceResult provisioned = services.Transact(new ServiceCommand(ServiceCommandKind.Provision));
        Assert.True(provisioned.IsApplied);
        Assert.Equal(larder + 6, party.Food.Portions);

        // A room spends the session's one clock and rests the party: the hour it gives up at is what passes,
        // and what a night clears is the room's own list rather than a rule the kit keeps.
        member.Conditions.Apply(new ActiveCondition(new ConditionId("Tired"), 2));
        member.Resources.TakeDamage(4);
        int hour = clock.Now.Hour;
        ServiceResult lodged = services.Transact(new ServiceCommand(ServiceCommandKind.Stay));
        Assert.True(lodged.IsApplied);
        Assert.Equal((hour + 8) % 24, clock.Now.Hour);
        Assert.False(member.Conditions.Has(new ConditionId("Tired")));
        Assert.Equal(10, member.Resources.HitPoints.Current);

        // A deposit and a withdrawal move coin between the purse and what the counter keeps, both ways, and
        // a withdrawal of more than is held is refused whole.
        int purse = party.Purse.Coins;
        ServiceResult deposited = services.Transact(new ServiceCommand(ServiceCommandKind.Deposit, "vault", Count: 300));
        Assert.True(deposited.IsApplied);
        Assert.Equal(300, deposited.Paid);
        Assert.Equal(purse - 300, party.Purse.Coins);
        Assert.Equal(300, ServiceHolding.Coins(party, "vault"));

        ServiceResult over = services.Transact(new ServiceCommand(ServiceCommandKind.Withdraw, "vault", Count: 500));
        Assert.Equal("service-holding-short", over.Code);
        Assert.Equal(300, ServiceHolding.Coins(party, "vault"));

        ServiceResult withdrew = services.Transact(new ServiceCommand(ServiceCommandKind.Withdraw, "vault", Count: 120));
        Assert.True(withdrew.IsApplied);
        Assert.Equal(120, withdrew.Earned);
        Assert.Equal(purse - 180, party.Purse.Coins);
        Assert.Equal(180, ServiceHolding.Coins(party, "vault"));

        // A passage is what a fare buys: the party holds the ticket and the journey's length, and the
        // counter's own account of it is what the road reads.
        ServiceResult fare = services.Transact(new ServiceCommand(ServiceCommandKind.Fare, "9"));
        Assert.True(fare.IsApplied);
        Assert.Equal(25, fare.Paid);
        Assert.Equal(3, ServicePassage.DaysTo(party, new PlaceId("9")));

        // A command the counter cannot resolve is its own refusal, and a counter with one offer of a kind
        // takes a command that names nothing.
        ServiceResult unknown = services.Transact(new ServiceCommand(ServiceCommandKind.Fare, "77"));
        Assert.Equal("service-no-such-offer", unknown.Code);
        ServiceResult absent = services.Transact(new ServiceCommand(ServiceCommandKind.Cure, "eradication", Member: 0));
        Assert.Equal("service-no-such-offer", absent.Code);
        Assert.Equal("service-no-such-member", services.Transact(new ServiceCommand(ServiceCommandKind.Cure, "affliction", Member: 4)).Code);

        // Every result reported the party's own purse, which is the same number the party holds.
        Assert.Equal(party.Purse.Coins, services.Browse() is not null ? party.Purse.Coins : 0);
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
                new ServiceOffer(ServiceOfferKind.Fare, "A passage north", "8", Value: 25, Amount: 2),
                new ServiceOffer(ServiceOfferKind.Fare, "A passage south", "9", Value: 25, Amount: 3),
            ],
        };

        PartyServices services = new(rule, party, new PartyResourceLedger(party), Clock());
        Assert.True(services.Open(rule.Service).IsApplied);
        Assert.Equal("service-offer-ambiguous", services.Transact(new ServiceCommand(ServiceCommandKind.Fare)).Code);

        // Naming one takes that one, and the ticket says which journey it is.
        Assert.True(services.Transact(new ServiceCommand(ServiceCommandKind.Fare, "9")).IsApplied);
        Assert.Equal(3, ServicePassage.DaysTo(party, new PlaceId("9")));
        Assert.Equal(0, ServicePassage.DaysTo(party, new PlaceId("8")));
        Assert.Equal(75, party.Purse.Coins);
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
        Assert.True(services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword")).IsApplied);
        Assert.Equal(1, rule.StockOf(services, "stock:sword"));
        Assert.True(services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword")).IsApplied);

        // Sold out: the line stays on the shelves at zero so the shop can say so, and the next purchase is
        // refused by name rather than quietly finding nothing.
        Assert.Equal(0, rule.StockOf(services, "stock:sword"));
        Assert.Equal("service-out-of-stock", services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword")).Code);

        // Six days on, the schedule has not come due and the shelves are still bare.
        services.Observe(clock.Advance(GameDuration.FromHours(24 * 6)));
        Assert.Equal(0, rule.StockOf(services, "stock:sword"));

        // The seventh day brings the deadline due, and the shelves are full again: the restock is the
        // clock's own deadline rather than a count of frames nobody could point at.
        services.Observe(clock.Advance(GameDuration.FromHours(24)));
        Assert.Equal(2, rule.StockOf(services, "stock:sword"));

        // A second interval is a second restock, so the schedule repeats rather than firing once.
        Assert.True(services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword")).IsApplied);
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
        Assert.Equal(120, services.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:sword")).Paid);

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
        ServiceLesson membership = new(ServiceLessonKind.Effect, "guild.fire", 1, 50, "Fire Guild membership");
        ServiceLesson lesson = new(ServiceLessonKind.Skill, "Fire", 1, 25, "Basic Fire");
        using PartyEntity party = Party(coins: 1000);
        PartyResourceLedger accounts = new(party);
        ShopRule rule = new(Shop(membership: "guild.fire", lessons: [membership, lesson]));
        // The guild serves members only — except at the counter that sells the membership itself.
        rule.Eligibility = request =>
        {
            bool joins = request.Operation == ServiceOperationKind.Teach &&
                request.Subject.Lesson is { Kind: ServiceLessonKind.Effect };
            return joins || request.Party.Effects.Has(new EffectId("guild.fire"))
                ? ServiceEligibility.Allowed
                : ServiceEligibility.Refused("service-membership-required", "The Fire Guild serves members only.");
        };
        PartyServices services = new(rule, party, accounts, Clock());
        Assert.True(services.Open(rule.Service).IsApplied);

        // Not a member: the gated operation names what stopped it, and the membership lesson does not.
        Assert.Equal("service-membership-required", services.Transact(new ServiceCommand(ServiceCommandKind.Teach, "Fire")).Code);
        Assert.Empty(services.Browse()!.Memberships);

        // Buying the membership is one lesson, and it puts a party-wide effect on the band: the same state
        // the access check reads, so buying one and being one cannot disagree.
        ServiceResult joined = services.Transact(new ServiceCommand(ServiceCommandKind.Teach, "guild.fire"));
        Assert.True(joined.IsApplied);
        Assert.True(party.Effects.Has(new EffectId("guild.fire")));
        Assert.Equal(50, joined.Paid);
        // What the membership reads as is the ruleset's word for it; the mechanism carries whatever the
        // access answer states.
        Assert.Equal(["guild.fire"], services.Browse()!.Memberships);

        // Now the same operation the guild refused is served.
        Assert.True(services.Transact(new ServiceCommand(ServiceCommandKind.Teach, "Fire")).IsApplied);
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
            counter.World,
            clock: counter.Clock,
            party: party,
            accounts: counter.Accounts,
            service: counter.Rule,
            useInput: new InteractionUseInput(UseControls),
            serviceInput: ServiceControls);
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
        // interaction mechanism reached the person, and the service mechanism is what opened the counter.
        session.Update(Update(1, 1));
        Assert.Equal("The Sword and Shield, kept by Bertram", channel.Latest().Field(SessionProjection.InteractionField).Field("label").AsString());
        session.Update(Update(2, 1, Digital("test.use", InputEdge.Pressed)));
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
        session.Update(Update(3, 1, Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":1}""")));
        ProjectedNode bought = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.Equal("buy", bought.Field("action").AsString());
        Assert.Equal("applied", bought.Field("outcome").AsString());
        Assert.Equal(100, bought.Field("paid").AsNumber());
        Assert.Equal(400, bought.Field("coins").AsNumber());
        Assert.Equal(400, party.Purse.Coins);
        Assert.Equal(1, bought.Field("stock").Item(0).Field("count").AsNumber());

        // A refusal the party cannot cover is reported with its own code and moves nothing.
        session.Update(Update(4, 1, Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":2}""")));
        ProjectedNode refused = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("service-not-enough-stock", refused.Field("code").AsString());
        Assert.Equal(400, refused.Field("coins").AsNumber());

        // A command that names nothing this counter has is refused by name rather than doing nothing.
        session.Update(Update(5, 1, Payload(ServiceControls.ActionContract, """{"action":"service.sell","target":"99"}""")));
        Assert.Equal("service-no-such-item", channel.Latest().Field(SessionProjection.ServiceField).Field("code").AsString());

        // The leave control the host declared ends the visit, and the panel says the party walked away.
        session.Update(Update(6, 1, Digital("test.service.leave", InputEdge.Pressed)));
        ProjectedNode left = channel.Latest().Field(SessionProjection.ServiceField);
        Assert.False(left.Field("open").AsBoolean());
        Assert.Equal("leave", left.Field("action").AsString());
        Assert.Equal("applied", left.Field("outcome").AsString());
        Assert.Null(session.Services!.Visit);
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
            counter.World,
            movementInput: new MovementInput(MovementControls, turnRatePerSecond: 2048),
            clock: counter.Clock,
            party: party,
            accounts: counter.Accounts,
            service: counter.Rule,
            useInput: new InteractionUseInput(UseControls),
            serviceInput: ServiceControls);
        session.Start();

        // Before any counter is open, a held forward control walks the party, which is what makes the next
        // assertion mean something.
        session.Update(Update(1, 1, Digital("test.move-forward", InputEdge.Held)));
        PlacePose walked = counter.World.Party.PlacePose;
        Assert.NotEqual(0, walked.Y);

        session.Update(Update(2, 0));
        session.Update(Update(3, 1, Digital("test.use", InputEdge.Pressed)));
        Assert.True(session.Services!.IsOpen);
        Assert.Equal(2, session.Services.Browse()!.Stock[0].Count);

        // At the counter the party does not step: the shop owns the player's controls, so a held forward
        // control walks nowhere and the visit cannot be left by accident. The world keeps its own time.
        double before = session.SimulationSeconds;
        session.Update(Update(4, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(walked, counter.World.Party.PlacePose);
        Assert.True(session.SimulationSeconds > before);

        // Buying the shelf out leaves it empty, and it stays empty while the schedule has not come due.
        session.Update(Update(5, 1, Payload(ServiceControls.ActionContract, """{"action":"service.buy","target":"stock:sword","count":2}""")));
        Assert.Equal(0, session.Services.Browse()!.Stock[0].Count);
        for (ulong step = 6; step <= 9; step++) session.Update(Update(step, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(0, session.Services.Browse()!.Stock[0].Count);
        Assert.Equal(walked, counter.World.Party.PlacePose);

        // The hour the shelves refresh on passes in the same clock the street reads, so the schedule is game
        // time rather than a loop: the shelf is full again while the visit is still open.
        session.Update(Update(10, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(2, session.Services.Browse()!.Stock[0].Count);

        // Leaving gives the controls back: the same held control walks the party again.
        session.Update(Update(11, 1, Digital("test.service.leave", InputEdge.Pressed)));
        Assert.False(session.Services.IsOpen);
        session.Update(Update(12, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.NotEqual(walked, counter.World.Party.PlacePose);
    }

    private static GameClock Clock(GameDate? start = null) =>
        new(
            Calendar,
            start ?? new GameDate(1168, 1, 1, 9, 0, 0),
            // One admitted second is one game day, so a test that steps a few seconds crosses the deadlines
            // it states without measuring a real day of engine time.
            new GameTimeScale(86_400),
            new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

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

    private static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            StepSeconds);
        return new ProductUpdate(facts, input);
    }

    private static ProductInputEvent Digital(string intent, InputEdge edge) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static ProductInputEvent Payload(string contract, string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(contract), Encoding.UTF8.GetBytes(json));

    /// <summary>
    /// The rules a test states: what a counter is, what its shelves hold, who it serves, and what it charges.
    /// </summary>
    /// <remarks>
    /// The default answers are the plain ones — content's own shelves, no gate, and a price equal to the
    /// base value — so a test that wants to prove a seam changes exactly that answer and nothing else.
    /// </remarks>
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
            Service.Membership.Length > 0 && request.Party.Effects.Has(new EffectId(Service.Membership))
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
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Document()),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog);
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
                new FreeCostRule(),
                time: clock,
                mover: walking ? new WalkingMover(owner) : null,
                clock: clock,
                resources: accounts,
                partyEntity: party,
                interaction: policy);
            return new Counter(world, rule, clock, accounts);
        }

        public void Dispose() => World.Dispose();

        private static string Document() =>
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "The Sword and Shield", "respawnDays": 3,
                "entryPoints": [ { "id": "Door", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "sword-and-shield", "kind": "service", "x": 0, "y": 100, "z": 0 } ] } ] }
            """;

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
            }
            """;
    }

    /// <summary>The interaction answers a test's world uses: a counter is a person to talk to.</summary>
    private sealed class CounterInteraction(ShopRule rule) : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            rule.Describe(new ServiceTargetRequest(request.Place, request.Placement)) is { } service
                ? new InteractionTargetDefinition(new InteractionTargetKind("service"), service.Describe(), InteractionVerb.Talk, 512)
                : null;

        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
            InteractionRequirementVerdict.Satisfied;

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

    /// <summary>A world where nothing is charged for walking, so a test's coins are spent at the counter.</summary>
    private sealed class FreeCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
