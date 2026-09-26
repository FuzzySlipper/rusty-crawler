using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's per-kind service policy: what each kind of building in the shipped table offers, teaches,
/// stocks, and charges.
/// </summary>
/// <remarks>
/// <para>
/// The content here is written the way the importer writes it — a service entry carries the building
/// table's own columns and nothing else, so which operations a counter has, what its shelves hold, what it
/// teaches, and what it offers are all this ruleset's answers about the kind rather than the pack's. That
/// is what makes the same suite prove the shipped policy: a weapon shop's shelves come from the item table,
/// a guild's spell books from its school and rung, a temple's cures from the conditions it claims, and a
/// stable's passages from the routes its entry names.
/// </para>
/// <para>
/// Every kind is driven through the one mechanism and the party's one purse: a counter is opened exactly as
/// the interaction mechanism opens one, a command goes through <see cref="PartyServices.Transact"/>, and
/// what is asserted is the party's own state and the coins that moved.
/// </para>
/// </remarks>
public sealed class ServiceKindPolicyTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void A_weapon_shop_and_an_alchemist_stock_the_goods_their_kind_trades()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices services = fixture.Services;

        // The shelves are the item table's own goods for what the kind trades: the weapon shop holds the
        // weapons and bows of the catalogue and not the armour, the potions, or the spell books, and the
        // artifact in the table is not for sale.
        PartyServices shop = fixture.Open("1");
        IReadOnlyList<ServiceStockLine> shelves = fixture.Rule.Stock(new ServiceStockRequest(shop.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(shelves, line => line.Definition.Value == "10");
        Assert.Contains(shelves, line => line.Definition.Value == "11");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "20");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "30");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "40");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "12");

        // A weapon shop teaches the trades of the goods it stocks, which is the item table's own skill
        // groups filtered by the skills this game has: a club is a weapon the table files under a skill the
        // skill table does not carry, so no counter teaches it.
        IReadOnlyList<ServiceLesson> lessons = fixture.Rule.Lessons(new ServiceLessonRequest(shop.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(lessons, lesson => lesson.Subject == "Sword");
        Assert.Contains(lessons, lesson => lesson.Subject == "Bow");
        Assert.DoesNotContain(lessons, lesson => lesson.Subject == "Club");

        // Buying one takes it off the shelf and out of the one purse.
        int before = fixture.Party.Purse.Coins;
        ServiceResult bought = shop.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:10"));
        Assert.True(bought.IsApplied);
        Assert.Equal(75, bought.Paid);
        Assert.Equal(before - 75, fixture.Party.Purse.Coins);
        Assert.Equal(1, fixture.Party.Inventory.TotalOf(new ItemDefinitionId("10")));

        // An alchemist trades potions, reagents, and gems, and repairs nothing, which is the donor's own
        // division of the shop kinds (OpenEnroth src/GUI/UI/Houses/Shops.cpp:264-275).
        PartyServices alchemist = fixture.Open("2");
        Assert.Contains(ServiceOperationKind.Buy, alchemist.Current!.Operations);
        Assert.DoesNotContain(ServiceOperationKind.Repair, alchemist.Current.Operations);
        IReadOnlyList<ServiceStockLine> potions = fixture.Rule.Stock(new ServiceStockRequest(alchemist.Current, fixture.Party, fixture.Clock));
        Assert.Contains(potions, line => line.Definition.Value == "30");
        Assert.Contains(potions, line => line.Definition.Value == "31");
    }

    [Fact]
    public void A_guild_sells_its_membership_teaches_its_school_and_gates_its_spell_books_by_tier()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices guild = fixture.Open("139");

        // The guild's rung in its own school is what its shelves may hold: an initiate guild sells the
        // first four spell levels of its school (OpenEnroth src/Engine/Objects/CharacterEnumFunctions.h:33-46),
        // so the school's first- and second-level books are on the shelf and its fifth-level one is not —
        // and no other school's book is either.
        IReadOnlyList<ServiceStockLine> shelves = fixture.Rule.Stock(new ServiceStockRequest(guild.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(shelves, line => line.Definition.Value == "40");
        Assert.Contains(shelves, line => line.Definition.Value == "41");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "42");
        Assert.DoesNotContain(shelves, line => line.Definition.Value == "43");

        // The shelf is behind the membership, which is party-carried state the counter sells: buying a book
        // before joining is refused by name and moves nothing.
        ServiceResult refused = guild.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:40"));
        Assert.Equal("service-membership-required", refused.Code);
        Assert.Equal(fixture.Party.Purse.Coins, fixture.FixtureCoins);

        // The membership is one lesson, and a guild teaches its own school and its second skill.
        IReadOnlyList<ServiceLesson> lessons = fixture.Rule.Lessons(new ServiceLessonRequest(guild.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(lessons, lesson => lesson.Kind == ServiceLessonKind.Effect && lesson.Subject == "guild.fire");
        Assert.Contains(lessons, lesson => lesson.Kind == ServiceLessonKind.Skill && lesson.Subject == "Fire");
        Assert.Contains(lessons, lesson => lesson.Kind == ServiceLessonKind.Skill && lesson.Subject == "Learning");

        ServiceResult joined = guild.Transact(new ServiceCommand(ServiceCommandKind.Teach, "guild.fire", Member: 0));
        Assert.True(joined.IsApplied);
        Assert.True(fixture.Party.Effects.Has(new EffectId("guild.fire")));
        Assert.Equal(1000, joined.Paid);

        // A member is served, and the same membership is not sold twice.
        Assert.True(guild.Transact(new ServiceCommand(ServiceCommandKind.Buy, "stock:40")).IsApplied);
        Assert.Equal("service-membership-held", guild.Transact(new ServiceCommand(ServiceCommandKind.Teach, "guild.fire", Member: 0)).Code);

        // An adept guild of the same school reaches further up the ladder, which is the tier gate rather
        // than a second kind of counter.
        PartyServices adept = fixture.Open("140");
        IReadOnlyList<ServiceStockLine> deeper = fixture.Rule.Stock(new ServiceStockRequest(adept.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(deeper, line => line.Definition.Value == "42");
        Assert.DoesNotContain(deeper, line => line.Definition.Value == "43");
    }

    [Fact]
    public void A_temple_cures_every_condition_family_it_claims_including_death_and_eradication()
    {
        using Fixture fixture = Fixture.Build();
        PartyMember member = fixture.Party.Members[0];

        // A whole party is offered nothing, because there is nothing to heal.
        PartyServices temple = fixture.Open("87");
        Assert.DoesNotContain(
            fixture.Rule.Offers(new ServiceOfferRequest(temple.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Kind == ServiceOfferKind.Cure);

        // A curable affliction is one family, priced by how severe it is. The cure ends what it claims and
        // restores the body, and the one purse moves.
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Cursed, 2));
        member.Resources.TakeDamage(5);
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.PoisonWeak, 1));
        IReadOnlyList<ServiceOffer> afflictions = fixture.Rule.Offers(new ServiceOfferRequest(temple.Current!, fixture.Party, fixture.Clock));
        ServiceOffer healing = Assert.Single(afflictions, offer => offer.Subject == "affliction");
        Assert.Contains(MightAndMagic7Conditions.Cursed, healing.Conditions);
        Assert.Contains(MightAndMagic7Conditions.PoisonWeak, healing.Conditions);
        Assert.DoesNotContain(MightAndMagic7Conditions.Dead, healing.Conditions);

        ServiceResult cured = temple.Transact(new ServiceCommand(ServiceCommandKind.Cure, "affliction", Member: 0));
        Assert.True(cured.IsApplied);
        Assert.Equal(4, cured.Paid);
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.Cursed));
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.PoisonWeak));
        Assert.Equal(member.Resources.HitPoints.Maximum, member.Resources.HitPoints.Current);

        // Death and petrification are the serious family at the donor's five times, and eradication is the
        // dearest at ten times — the two a party cannot come back from without a temple.
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Dead));
        ServiceOffer raise = Assert.Single(
            fixture.Rule.Offers(new ServiceOfferRequest(temple.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Subject == "death");
        Assert.Contains(MightAndMagic7Conditions.Dead, raise.Conditions);
        ServiceResult raised = temple.Transact(new ServiceCommand(ServiceCommandKind.Cure, "death", Member: 0));
        Assert.True(raised.IsApplied);
        Assert.Equal(10, raised.Paid);
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.Dead));

        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Eradicated));
        ServiceResult restored = temple.Transact(new ServiceCommand(ServiceCommandKind.Cure, "eradication", Member: 0));
        Assert.True(restored.IsApplied);
        Assert.Equal(20, restored.Paid);
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.Eradicated));

        // A temple also teaches the three skills the donor gives it, and a cure for somebody who suffers
        // nothing is refused rather than charged.
        IReadOnlyList<ServiceLesson> lessons = fixture.Rule.Lessons(new ServiceLessonRequest(temple.Current!, fixture.Party, fixture.Clock));
        Assert.Contains(lessons, lesson => lesson.Subject == "Unarmed");
        Assert.Contains(lessons, lesson => lesson.Subject == "Dodging");
        Assert.Contains(lessons, lesson => lesson.Subject == "Merchant");
        // A party that suffers nothing is offered no cure at all, so the command names an offer the counter
        // does not have rather than being charged for a healing nobody needs.
        Assert.Equal("service-no-such-offer", temple.Transact(new ServiceCommand(ServiceCommandKind.Cure, "affliction", Member: 0)).Code);
    }

    [Fact]
    public void A_training_hall_turns_banked_experience_into_a_level_up_to_its_own_ceiling()
    {
        using Fixture fixture = Fixture.Build();
        PartyMember member = fixture.Party.Members[0];
        PartyServices hall = fixture.Open("89");

        // A member who has not earned the level is told how much more is wanted, and pays nothing.
        ServiceOffer training = Assert.Single(
            fixture.Rule.Offers(new ServiceOfferRequest(hall.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Kind == ServiceOfferKind.Training);
        Assert.Equal(5, training.Limit);
        Assert.Equal("service-experience-short", hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0)).Code);
        Assert.Equal(1, member.Progression.Level);

        // With the experience banked, one step is one level at the donor's own fee, and the purse pays it.
        // The experience arrives through the one award entry, exactly as a kill's does.
        ProgressionAwardResult earned = fixture.Progression.Award(new PartyExperienceAward("test", 100_000));
        Assert.True(earned.IsAwarded);
        Assert.Equal(100_000, member.Progression.Experience);
        int before = fixture.Party.Purse.Coins;
        ServiceResult trained = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.True(trained.IsApplied);
        Assert.Equal(2, member.Progression.Level);
        Assert.Equal(10, trained.Paid);
        Assert.Equal(before - 10, fixture.Party.Purse.Coins);

        // A level is not only a number: the knight's own row of the donor's table adds five hit points, and
        // the new level grants five skill points (OpenEnroth src/Engine/Objects/Character.cpp:135-172 and
        // src/GUI/UI/Houses/Training.cpp:72).
        Assert.Equal(45, member.Resources.HitPoints.Maximum);
        Assert.Equal(45, member.Resources.HitPoints.Current);
        Assert.Equal(5, member.Progression.SkillPoints);

        // The hall's ceiling is content's own number from the building table: it trains to five and no
        // further, and the refusal names the ceiling rather than clamping the member.
        for (int level = 2; level < 5; level++)
        {
            Assert.True(hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0)).IsApplied);
        }

        Assert.Equal(5, member.Progression.Level);
        ServiceResult capped = hall.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0));
        Assert.Equal("service-training-capped", capped.Code);
        Assert.Equal(5, member.Progression.Level);

        // A hall whose cap the table writes as free text trains without one, which is the donor's own
        // no-limit hall (OpenEnroth src/GUI/UI/Houses/Training.cpp:18-29).
        PartyServices uncapped = fixture.Open("99");
        ServiceOffer open = Assert.Single(
            fixture.Rule.Offers(new ServiceOfferRequest(uncapped.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Kind == ServiceOfferKind.Training);
        Assert.Equal(MightAndMagic7Services.UncappedTraining, open.Limit);
        Assert.True(uncapped.Transact(new ServiceCommand(ServiceCommandKind.Train, Member: 0)).IsApplied);
        Assert.Equal(6, member.Progression.Level);
    }

    [Fact]
    public void A_tavern_fills_the_packs_rents_a_room_and_tells_what_travellers_say()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices tavern = fixture.Open("107");
        IReadOnlyList<ServiceOffer> offers = fixture.Rule.Offers(new ServiceOfferRequest(tavern.Current!, fixture.Party, fixture.Clock));

        // The tavern's own multiplier is the number of days of food it fills, which is the donor's own
        // reading of that column (OpenEnroth src/GUI/UI/Houses/Tavern.cpp:99-118).
        ServiceOffer provisions = Assert.Single(offers, offer => offer.Kind == ServiceOfferKind.Provision);
        Assert.Equal(6, provisions.Amount);
        int larder = fixture.Party.Food.Portions;
        ServiceResult filled = tavern.Transact(new ServiceCommand(ServiceCommandKind.Provision));
        Assert.True(filled.IsApplied);
        Assert.Equal(larder + 6, fixture.Party.Food.Portions);

        // A party whose packs are as full as the tavern fills them is refused rather than sold more.
        Assert.Equal("service-packs-full", tavern.Transact(new ServiceCommand(ServiceCommandKind.Provision)).Code);

        // A room is a night: game time passes on the session's one clock, the party rests, and what a night
        // ends is the room's own list rather than everything a temple would cure.
        PartyMember member = fixture.Party.Members[0];
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Weak));
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.DiseaseWeak));
        member.Resources.TakeDamage(4);
        long taken = fixture.Clock.Elapsed.Milliseconds;
        ServiceResult lodged = tavern.Transact(new ServiceCommand(ServiceCommandKind.Stay));
        Assert.True(lodged.IsApplied);
        Assert.True(fixture.Clock.Elapsed.Milliseconds > taken);
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.Weak));
        Assert.True(member.Conditions.Has(MightAndMagic7Conditions.DiseaseWeak));
        Assert.Equal(member.Resources.HitPoints.Maximum, member.Resources.HitPoints.Current);

        // What travellers say is the roads the place's own map issues, published as a surface rather than
        // sold: it costs nothing and changes nothing.
        ServiceOffer rumour = Assert.Single(offers, offer => offer.Kind == ServiceOfferKind.Notice);
        Assert.Contains("Harmondale", rumour.Name, StringComparison.Ordinal);
        Assert.Equal(0, rumour.Value);
    }

    [Fact]
    public void A_bank_keeps_the_party_s_coins_and_gives_them_back()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices bank = fixture.Open("128");
        int purse = fixture.Party.Purse.Coins;

        // A deposit leaves the purse through the party's one settlement path and is kept as party-carried
        // state under the bank's own name.
        ServiceResult deposited = bank.Transact(new ServiceCommand(ServiceCommandKind.Deposit, MightAndMagic7Services.BankHolding, Count: 200));
        Assert.True(deposited.IsApplied);
        Assert.Equal(200, deposited.Paid);
        Assert.Equal(purse - 200, fixture.Party.Purse.Coins);
        Assert.Equal(200, ServiceHolding.Coins(fixture.Party, MightAndMagic7Services.BankHolding));

        // More than is held is refused whole, and what is held comes back.
        Assert.Equal("service-holding-short", bank.Transact(new ServiceCommand(ServiceCommandKind.Withdraw, MightAndMagic7Services.BankHolding, Count: 500)).Code);
        ServiceResult withdrew = bank.Transact(new ServiceCommand(ServiceCommandKind.Withdraw, MightAndMagic7Services.BankHolding, Count: 150));
        Assert.True(withdrew.IsApplied);
        Assert.Equal(150, withdrew.Earned);
        Assert.Equal(purse - 50, fixture.Party.Purse.Coins);
        Assert.Equal(50, ServiceHolding.Coins(fixture.Party, MightAndMagic7Services.BankHolding));

        // A deposit the party cannot afford is refused by name before anything moves, and the state is shown
        // on the counter's own surface so a player reads the balance before deciding.
        Assert.Equal("purse-short", bank.Transact(new ServiceCommand(ServiceCommandKind.Deposit, MightAndMagic7Services.BankHolding, Count: 100_000)).Code);
        Assert.Contains(
            fixture.Rule.Access(new ServiceAccessRequest(bank.Current!, fixture.Party)),
            line => line.Contains("50 coin", StringComparison.Ordinal));
    }

    [Fact]
    public void A_stable_sells_a_passage_and_the_paid_travel_path_honours_it()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices stable = fixture.Open("54");
        PlaceGraph graph = PlaceGraphLoader.Load(fixture.Catalog);
        PlaceTransition road = Assert.Single(graph.Transitions, transition => transition.Source == "fare-54-2");

        // The passages a stable sells are the routes its own entry names, each with the days the journey
        // takes.
        ServiceOffer fare = Assert.Single(
            fixture.Rule.Offers(new ServiceOfferRequest(stable.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Kind == ServiceOfferKind.Fare);
        Assert.Equal("2", fare.Subject);
        Assert.Equal(2, fare.Amount);

        // Without a passage the paid transition is refused by name, and nothing about the party changes.
        TransitionRequest request = new(graph, road, TransitionKind.PaidService, new PlaceId("1"), PlacePose.Origin);
        MightAndMagic7TravelCostRule rule = new(fixture.Party);
        Assert.Equal("travel-fare-unpaid", rule.Quote(request).Refusal!.Code);

        // Buying the fare puts the passage on the party, and the road then quotes the journey the counter
        // sold: the days are the route's own and the fare already paid for the board.
        ServiceResult bought = stable.Transact(new ServiceCommand(ServiceCommandKind.Fare, "2"));
        Assert.True(bought.IsApplied);
        Assert.Equal(50, bought.Paid);
        Assert.Equal(2, ServicePassage.DaysTo(fixture.Party, new PlaceId("2")));

        TravelCostQuote boarded = rule.Quote(request);
        Assert.Null(boarded.Refusal);
        Assert.Equal(new TravelTime(2, TravelTimeUnit.Days), boarded.Cost.Time);
        Assert.True(boarded.Cost.Food.IsNone);

        // The ticket is torn by the boarding, so the same fare does not pay for a second journey.
        Assert.Equal(0, ServicePassage.DaysTo(fixture.Party, new PlaceId("2")));
        Assert.Equal("travel-fare-unpaid", rule.Quote(request).Refusal!.Code);
    }

    [Fact]
    public void A_town_hall_posts_the_month_s_bounty_and_a_house_answers_its_door()
    {
        using Fixture fixture = Fixture.Build();
        PartyServices hall = fixture.Open("131");

        // The bounty is drawn from the place's own encounter row by the month the clock stands in, and it
        // pays the donor's hundred times the beast's level (OpenEnroth src/GUI/UI/Houses/TownHall.cpp:143-176).
        ServiceOffer bounty = Assert.Single(
            fixture.Rule.Offers(new ServiceOfferRequest(hall.Current!, fixture.Party, fixture.Clock)),
            offer => offer.Kind == ServiceOfferKind.Notice);
        Assert.Equal("Goblin", bounty.Subject);
        Assert.Equal(300, bounty.Amount);
        Assert.Contains("bounty", bounty.Name, StringComparison.Ordinal);

        // A town hall transacts nothing: what it has is a surface, because claiming a bounty needs a kill
        // this build has no combat for.
        Assert.Empty(hall.Current!.Operations);

        // A house is somebody's home rather than a counter: the interaction mechanism reaches it as a person
        // to talk to, and the use names whoever the table says lives there. The name comes from the dialogue
        // policy the conversation itself asks, so what the reticle shows and who answers are one reading.
        PlacePopulationContent population = PlacePopulationContent.Read(PlaceGraphLoader.Load(fixture.Catalog));
        PlacementDefinition residence = Assert.Single(
            population.PlacementsOf(new PlaceId("1")),
            placement => placement.Content.Kind == "residence");
        MightAndMagic7PeopleInteraction interaction = new(fixture.Conversation, new MightAndMagic7Interaction());
        InteractionTargetDefinition target = interaction.Describe(new InteractionTargetRequest(new PlaceId("1"), residence, string.Empty))!;
        Assert.Equal(MightAndMagic7Conversation.PersonTargetKind, target.Kind.Value);
        Assert.Equal(InteractionVerb.Talk, target.Verb);
        Assert.Equal("Mira", target.Name);

        InteractionOutcome outcome = interaction.Apply(target, new InteractionContext(
            new PlaceId("1"),
            residence,
            target,
            fixture.Party,
            fixture.Clock));
        Assert.Equal("The party speaks with Mira.", outcome.Message);
    }

    /// <summary>A party, the content the counters are read from, and the one mechanism that serves them.</summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(
            ContentCatalog catalog,
            MightAndMagic7Services rule,
            MightAndMagic7Conversation conversation,
            PartyEntity party,
            GameClock clock,
            PartyServices services,
            PartyProgression progression)
        {
            Conversation = conversation;
            Catalog = catalog;
            Rule = rule;
            Party = party;
            Clock = clock;
            Services = services;
            Progression = progression;
            FixtureCoins = party.Purse.Coins;
        }

        internal ContentCatalog Catalog { get; }

        internal MightAndMagic7Services Rule { get; }

        /// <summary>This game's answers about people, read over the fixture's own content.</summary>
        internal MightAndMagic7Conversation Conversation { get; }

        internal PartyEntity Party { get; }

        internal GameClock Clock { get; }

        internal PartyServices Services { get; }

        /// <summary>The progression owner the counters train through, which is what grants a level.</summary>
        internal PartyProgression Progression { get; }

        /// <summary>What the party's purse held when the fixture was built, for a test that asserts it did not move.</summary>
        internal int FixtureCoins { get; }

        /// <summary>Opens one counter by the identity its placement names, as the interaction mechanism does.</summary>
        internal PartyServices Open(string service)
        {
            PlacePopulationContent population = PlacePopulationContent.Read(PlaceGraphLoader.Load(Catalog));
            PlacementDefinition placement = population.PlacementsOf(new PlaceId("1"))
                .First(item => item.Content.Kind == "service" && Rule.Describe(new ServiceTargetRequest(new PlaceId("1"), item))?.Id.Value == service);
            Assert.NotNull(Services.OpenTarget(new PlaceId("1"), placement));
            return Services;
        }

        internal static Fixture Build()
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new PolicyContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Places())
                    .Add("packs/world/services.json", Services_())
                    .Add("packs/world/items.json", Items())
                    .Add("packs/world/spells.json", Spells())
                    .Add("packs/world/skills.json", Skills())
                    .Add("packs/world/monsters.json", Monsters())
                    .Add("packs/world/roads.json", Roads()),
                Layout).RequireValid();

            MightAndMagic7Services rule = MightAndMagic7Services.Read(catalog)
                ?? throw new InvalidOperationException("The content declares services, so the policy must be read.");
            MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, rule)
                ?? throw new InvalidOperationException("The content declares places, so the dialogue policy must be read.");
            GameClock clock = new(
                GameCalendar.TwelveMonthsOfFourWeeks,
                new GameDate(1168, 1, 1, 9, 0, 0),
                new GameTimeScale(1),
                new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));
            PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(
                [
                    new MemberCreation(new PartyMemberSeed(
                        "Roderick",
                        new RaceId("human"),
                        new ClassId("knight"),
                        [new AttributeScore(new AttributeId("Might"), 13)],
                        skills: [new SkillEntry(new SkillId("Sword"), 1, new SkillTier(1), 1)],
                        spells: [],
                        experience: 0,
                        level: 1,
                        skillPoints: 0,
                        classRank: 1,
                        conditions: [],
                        hitPoints: ResourcePool.Full(40),
                        spellPoints: ResourcePool.Full(10))),
                ],
                coins: 3000,
                foodPortions: 2,
                ProvisionUnit.Portions,
                reputation: 0,
                fame: 0));
            PartyResourceLedger accounts = new(party);
            // The owner a hall settles through is composed over the same party the services serve, so a
            // training step charges this party's purse and rises this party's member.
            PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
            return new Fixture(
                catalog,
                rule,
                conversation,
                party,
                clock,
                new PartyServices(rule, party, accounts, clock, progression),
                progression);
        }

        public void Dispose() => Party.Dispose();

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                { "path": "roads.json", "documentId": "roads", "definitionKind": "travel-link" }
              ]
            }
            """;

        private static string Places() =>
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Erathia", "respawnDays": 7,
                  "monsters": [ "Goblin", "Dragon" ],
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "service-1", "kind": "service", "houseId": 1, "x": 100, "y": 0, "z": 0 },
                    { "id": "service-2", "kind": "service", "houseId": 2, "x": 200, "y": 0, "z": 0 },
                    { "id": "service-139", "kind": "service", "houseId": 139, "x": 300, "y": 0, "z": 0 },
                    { "id": "service-140", "kind": "service", "houseId": 140, "x": 400, "y": 0, "z": 0 },
                    { "id": "service-87", "kind": "service", "houseId": 87, "x": 500, "y": 0, "z": 0 },
                    { "id": "service-89", "kind": "service", "houseId": 89, "x": 600, "y": 0, "z": 0 },
                    { "id": "service-99", "kind": "service", "houseId": 99, "x": 700, "y": 0, "z": 0 },
                    { "id": "service-107", "kind": "service", "houseId": 107, "x": 800, "y": 0, "z": 0 },
                    { "id": "service-128", "kind": "service", "houseId": 128, "x": 900, "y": 0, "z": 0 },
                    { "id": "service-54", "kind": "service", "houseId": 54, "x": 1000, "y": 0, "z": 0 },
                    { "id": "service-131", "kind": "service", "houseId": 131, "x": 1100, "y": 0, "z": 0 },
                    { "id": "residence-7", "kind": "residence", "houseId": 7, "name": "House of Ash", "proprietor": "Mira", "fixture": "House R7", "x": 1200, "y": 0, "z": 0 }
                  ] },
                { "id": "2", "kind": "region", "name": "Harmondale", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """;

        /// <summary>
        /// The counters, written the way the importer writes them: the building table's own columns and
        /// nothing about what the kind offers.
        /// </summary>
        private static string Services_() =>
            """
            {
              "documentId": "services",
              "definitionKind": "service",
              "entries": [
                { "id": "1", "kind": "Weapon Shop", "name": "The Knight's Blade", "proprietor": "Tor", "mapId": 1, "typeSequence": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 1.5, "skillPriceMultiplier": 1, "stockIntervalDays": 7 },
                { "id": "2", "kind": "Alchemist", "name": "The Apothecary", "proprietor": "Mistress Vale", "mapId": 1, "typeSequence": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 2, "skillPriceMultiplier": 1.5, "stockIntervalDays": 7 },
                { "id": "139", "kind": "Fire Guild", "name": "Initiate Guild of Fire", "proprietor": "Sethric", "mapId": 1, "typeSequence": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 2, "skillPriceMultiplier": 1, "stockIntervalDays": 14 },
                { "id": "140", "kind": "Fire Guild", "name": "Adept Guild of Fire", "proprietor": "Jani", "mapId": 1, "typeSequence": 2, "openHour": 6, "closedHour": 18, "priceMultiplier": 3, "skillPriceMultiplier": 1.5, "stockIntervalDays": 14 },
                { "id": "87", "kind": "Temple", "name": "Sanctuary", "proprietor": "Father Brom", "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 2, "skillPriceMultiplier": 1 },
                { "id": "89", "kind": "Training", "name": "Island Training Grounds", "proprietor": "Sergeant Hale", "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 10, "skillPriceMultiplier": 1, "trainingCap": 5, "trainingCapText": "5" },
                { "id": "99", "kind": "Training", "name": "Applied Instruction", "proprietor": "Master Vohn", "mapId": 10, "openHour": 6, "closedHour": 18, "priceMultiplier": 50, "skillPriceMultiplier": 1, "trainingCapText": "No Max" },
                { "id": "107", "kind": "Tavern", "name": "Two Palms Tavern", "proprietor": "Aaron", "mapId": 1, "openHour": 5, "closedHour": 2, "priceMultiplier": 6, "skillPriceMultiplier": 1 },
                { "id": "128", "kind": "Bank", "name": "Halls of Gold", "proprietor": "Coinvale", "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 1, "skillPriceMultiplier": 1 },
                { "id": "54", "kind": "Stables", "name": "The J.V.C Corral", "proprietor": "Christian", "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 2, "skillPriceMultiplier": 1, "fares": [ { "toPlace": 2, "place": "2", "name": "Harmondale", "days": 2, "link": "fare-54-2" } ] },
                { "id": "131", "kind": "Town Hall", "name": "The Town Hall", "proprietor": "Clerk Alden", "mapId": 1, "openHour": 6, "closedHour": 18, "priceMultiplier": 1, "skillPriceMultiplier": 1 }
              ]
            }
            """;

        private static string Items() =>
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "10", "name": "Crude Longsword", "value": 50, "equipStat": "Weapon", "skillGroup": "Sword", "material": "8" },
                { "id": "11", "name": "Crude Bow", "value": 40, "equipStat": "Missile", "skillGroup": "Bow", "material": "8" },
                { "id": "12", "name": "Puck", "value": 20000, "equipStat": "Weapon", "skillGroup": "Sword", "material": "Artifact" },
                { "id": "13", "name": "Spiked Club", "value": 30, "equipStat": "Weapon", "skillGroup": "Club", "material": "8" },
                { "id": "20", "name": "Leather Armor", "value": 60, "equipStat": "Armor", "skillGroup": "Leather", "material": "6" },
                { "id": "30", "name": "Cure Wounds", "value": 25, "equipStat": "Bottle", "skillGroup": "Misc", "material": "2" },
                { "id": "31", "name": "Widowsweep Berries", "value": 5, "equipStat": "Reagent", "skillGroup": "Misc", "material": "1" },
                { "id": "40", "name": "Torch Light", "value": 100, "equipStat": "Book", "skillGroup": "Misc", "material": "3" },
                { "id": "41", "name": "Fire Bolt", "value": 200, "equipStat": "Book", "skillGroup": "Misc", "material": "3" },
                { "id": "42", "name": "Fireball", "value": 750, "equipStat": "Book", "skillGroup": "Misc", "material": "3" },
                { "id": "43", "name": "Light Bolt", "value": 1000, "equipStat": "Book", "skillGroup": "Misc", "material": "3" }
              ]
            }
            """;

        private static string Spells() =>
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "1", "school": "Fire", "level": 1, "name": "Torch Light" },
                { "id": "2", "school": "Fire", "level": 2, "name": "Fire Bolt" },
                { "id": "5", "school": "Fire", "level": 5, "name": "Fireball" },
                { "id": "78", "school": "Light", "level": 1, "name": "Light Bolt" }
              ]
            }
            """;

        private static string Skills() =>
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [
                { "id": "Sword" }, { "id": "Bow" }, { "id": "Leather" }, { "id": "Fire" }, { "id": "Light" },
                { "id": "Learning" }, { "id": "Meditation" }, { "id": "Alchemy" }, { "id": "Identify Monster" },
                { "id": "Identify Item" }, { "id": "Repair" }, { "id": "Unarmed" }, { "id": "Dodging" },
                { "id": "Merchant" }, { "id": "Stealing" }, { "id": "Disarm Traps" }, { "id": "Perception" },
                { "id": "Armsmaster" }, { "id": "Bodybuilding" }
              ]
            }
            """;

        private static string Monsters() =>
            """
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "1", "name": "Goblin", "level": 3 },
                { "id": "2", "name": "Dragon", "level": 20 }
              ]
            }
            """;

        private static string Roads() =>
            """
            {
              "documentId": "roads",
              "definitionKind": "travel-link",
              "entries": [
                { "id": "fare-54-2", "fromPlace": 1, "fromName": "Erathia", "toPlace": 2, "toName": "Harmondale", "entryPoint": "Party Start", "houseId": 54, "fare": true, "days": 2 },
                { "id": "road-1-2", "fromPlace": 1, "fromName": "Erathia", "toPlace": 2, "toName": "Harmondale", "entryPoint": "Party Start", "houseId": 0, "fare": false }
              ]
            }
            """;
    }
}

/// <summary>The content a policy test states, held in memory rather than staged on disk.</summary>
/// <remarks>
/// The ruleset's own policy is read from a catalog rather than from a running product, so this suite needs
/// the one thing a catalog is built from: a source of files. It is a test double for the operator's disk and
/// nothing more.
/// </remarks>
internal sealed class PolicyContentSource : IContentSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    /// <summary>Adds one file to the source.</summary>
    /// <param name="path">The file's path, relative to the content root.</param>
    /// <param name="text">The file's text.</param>
    internal PolicyContentSource Add(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListDirectories(string relativePath)
    {
        string prefix = Normalize(relativePath);
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string path in _files.Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string remainder = path[prefix.Length..];
            int separator = remainder.IndexOf('/', StringComparison.Ordinal);
            if (separator > 0) names.Add(remainder[..separator]);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string prefix = Normalize(relativePath);
        return [.. _files.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..])
            .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

    /// <inheritdoc />
    public string ReadText(string relativePath) =>
        _files.TryGetValue(relativePath, out string? text) ? text : throw new FileNotFoundException(relativePath);

    private static string Normalize(string relativePath) =>
        relativePath.Length == 0 ? string.Empty : relativePath.Trim('/') + "/";
}
