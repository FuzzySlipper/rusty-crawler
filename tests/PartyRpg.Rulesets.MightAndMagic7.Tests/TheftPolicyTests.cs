using System.Text;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's theft: who may try, how a hand is measured against what being seen would cost, what a thief seen at
/// a counter is fined, how far the world's opinion falls, how long the counter stays shut, and how the fine is
/// carried as a debt the party saves and a town hall collects.
/// </summary>
/// <remarks>
/// <para>
/// The measures are the donor's (OpenEnroth <c>src/Engine/Objects/Character.cpp:1156-1279</c>), drawn on the
/// engine's keyed random service, which the session cases script by the purpose of each draw: whether the thief is
/// seen whatever the measures say, and which of the five draws of luck is added to the thief's reach.
/// </para>
/// <para>
/// The session cases are the flow a player takes: the counter reached through its keeper, the theft asked for on
/// the declared payload contract, the save written to the engine's store and resumed from it, and the fine paid at
/// a hall reached the same way.
/// </para>
/// </remarks>
public sealed class TheftPolicyTests
{
    private static readonly UseIntentNames UseControls = new(Declared.UseIntent, Declared.UiActionContract);
    private static readonly ServiceIntentNames ServiceControls = new(Declared.ServiceLeaveIntent, Declared.UiActionContract);
    private static readonly ConversationIntentNames ConversationControls = new(Declared.ConversationLeaveIntent, Declared.UiActionContract);

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_thief_caught_at_a_shop_is_fined_as_a_debt_falls_in_the_town_s_opinion_and_is_barred_for_a_day()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, TownContent());

        // The thief's luck is the donor's worst draw — two hundred under — and the one-in-twenty is not drawn, so
        // a novice's reach of a hundred falls a hundred under the cost: short of it by less than five hundred, the
        // thief comes away with the goods and is seen doing it.
        ((FakeEngineContext)context.Engine).RandomService.Answer = request =>
            request.Scope != MightAndMagic7Theft.RollScope ? null
            : request.Key.EndsWith("/pick", StringComparison.Ordinal) ? 0
            : request.Key.EndsWith("/seen", StringComparison.Ordinal) ? 50
            : null;
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = UseControls, Service = ServiceControls, Conversation = ConversationControls });
        session.Start();
        WalkIn(session, ui, step: 1);

        ProjectedNode shop = Latest(ui).Field("service");
        Assert.True(shop.Field("open").AsBoolean());
        Assert.True(shop.Field("canSteal").AsBoolean());
        Assert.Equal("Robin", shop.Field("thieves").Item(0).Field("name").AsString());
        Assert.Equal("Marian", shop.Field("thieves").Item(1).Field("name").AsString());
        Assert.Equal(2, shop.Field("thieves").Length());

        session.Update(RulesetTestContext.Update(10, 1, RulesetTestContext.Payload("""{"action":"service.steal","target":"stock:sword","member":0}""")));
        ProjectedNode stolen = Latest(ui).Field("service");
        Assert.Equal("steal", stolen.Field("action").AsString());
        Assert.Equal("applied", stolen.Field("outcome").AsString());
        Assert.Contains("gets away with A fine sword, but is seen", stolen.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(0, stolen.Field("paid").AsNumber());

        // The cost is the donor's: a hundred for each point of the place's base fine (two) and of the party's
        // standing in the donor's sign (none yet), and the sword's own worth on top — three hundred, added to the
        // fine the party owes rather than taken from its purse. The world's opinion falls the donor's two points,
        // the sword is in the pack, and the counter has shut the party out.
        ProjectedNode party = Latest(ui).Field("party");
        Assert.Equal(500, party.Field("coins").AsNumber());
        Assert.Equal(-2, party.Field("reputation").AsNumber());
        Assert.Equal(1, party.Field("pack").AsNumber());
        Assert.Equal(MightAndMagic7Theft.FineAccount, party.Field("debts").Item(0).Field("account").AsString());
        Assert.Equal(300, party.Field("debts").Item(0).Field("coins").AsNumber());
        Assert.False(stolen.Field("open").AsBoolean());
        Assert.Contains("will not serve the party for 24 hour(s)", stolen.Field("message").AsString(), StringComparison.Ordinal);

        // Walking back up to the keeper and asking for the counter is refused by name while the day runs.
        WalkIn(session, ui, step: 20);
        Assert.Equal("service-barred", Latest(ui).Field("service").Field("code").AsString());

        // The debt and the ban are the party's own state, so the product's save carries them, and a resumed
        // product still owes the fine and is still turned away.
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Equal([new PartyDebt(MightAndMagic7Theft.FineAccount, 300)], written.Party.Debts);
        Assert.Single(written.Party.Bans);
        Assert.True(written.Party.Items.Single().State.IsStolen);
        byte[]? payload = persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot);
        Assert.NotNull(payload);
        Assert.Contains("\"debts\"", Encoding.UTF8.GetString(payload), StringComparison.Ordinal);

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, TownContent());
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Use = UseControls, Service = ServiceControls, Conversation = ConversationControls });
        resumed.Start();
        ProjectedNode after = Latest(resumedUi).Field("party");
        Assert.Equal(300, after.Field("debts").Item(0).Field("coins").AsNumber());
        Assert.Equal(-2, after.Field("reputation").AsNumber());
        WalkIn(resumed, resumedUi, step: 30);
        Assert.Equal("service-barred", Latest(resumedUi).Field("service").Field("code").AsString());

        // The town hall collects the fine: the counter offers the debt at what the purse would pay toward it, and
        // a repayment takes it from the purse and off what is owed.
        // A keeper who turned the party away is still being spoken with; the party takes its leave and walks on.
        resumed.Update(RulesetTestContext.Update(35, 1, RulesetTestContext.Digital(Declared.ConversationLeaveIntent)));
        Assert.False(Latest(resumedUi).Field("conversation").Field("open").AsBoolean());
        ((MightAndMagic7Session)resumed).World!.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        WalkIn(resumed, resumedUi, step: 40);
        ProjectedNode hall = Latest(resumedUi).Field("service");
        Assert.True(hall.Field("open").AsBoolean(), hall.Field("message").AsString());
        Assert.Equal("Town Hall", hall.Field("kind").AsString());
        Assert.Equal(MightAndMagic7Theft.FineAccount, hall.Field("debts").Item(0).Field("subject").AsString());
        Assert.Equal(300, hall.Field("debts").Item(0).Field("owed").AsNumber());
        Assert.Equal(300, hall.Field("debts").Item(0).Field("price").AsNumber());
        Assert.False(hall.Field("canSteal").AsBoolean());

        resumed.Update(RulesetTestContext.Update(50, 1, RulesetTestContext.Payload("""{"action":"service.repay","target":"fine","count":120}""")));
        ProjectedNode part = Latest(resumedUi).Field("service");
        Assert.Equal("applied", part.Field("outcome").AsString());
        Assert.Equal(120, part.Field("paid").AsNumber());
        Assert.Equal(380, part.Field("coins").AsNumber());
        Assert.Equal(180, Latest(resumedUi).Field("party").Field("debts").Item(0).Field("coins").AsNumber());

        // Asking to pay more than is owed pays what is owed and no more, as the donor's hall does.
        resumed.Update(RulesetTestContext.Update(51, 1, RulesetTestContext.Payload("""{"action":"service.repay","target":"fine","count":1000}""")));
        ProjectedNode cleared = Latest(resumedUi).Field("service");
        Assert.Equal("applied", cleared.Field("outcome").AsString());
        Assert.Equal(180, cleared.Field("paid").AsNumber());
        Assert.Equal(200, cleared.Field("coins").AsNumber());
        Assert.Equal(0, Latest(resumedUi).Field("party").Field("debts").Length());
        Assert.Equal(0, cleared.Field("debts").Length());

        // Nothing is owed now, so the hall offers nothing to pay, and a repayment is refused by name.
        resumed.Update(RulesetTestContext.Update(52, 1, RulesetTestContext.Payload("""{"action":"service.repay","target":"fine","count":10}""")));
        Assert.Equal("service-no-such-offer", Latest(resumedUi).Field("service").Field("code").AsString());
    }

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void The_donor_s_measures_decide_whether_a_thief_is_seen_and_with_what()
    {
        TestRandomService random = new();
        using Town town = Town.Build(random);
        PartyMember novice = town.Party.Members[0];
        PartyServices services = town.Services;
        Assert.True(services.Open(town.Shop).IsApplied);

        // One draw in twenty is seen whatever the measures say, and the thief comes away with nothing: the fine is
        // the whole cost, the world's opinion falls the donor's one point, and the counter shuts for a day.
        random.Answer = Draws(luck: 2, seen: 5);
        ServiceResult caught = services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", 0));
        Assert.True(caught.IsApplied, caught.Message);
        Assert.Contains("is caught reaching for A fine sword", caught.Message, StringComparison.Ordinal);
        Assert.Equal(0, town.Party.Inventory.Count);
        Assert.Equal(300, town.Party.Debts.OwedOn(MightAndMagic7Theft.FineAccount));
        Assert.Equal(-1, town.Party.Reputation.Reputation);
        Assert.Equal(MightAndMagic7Theft.CaughtSource, town.Progression.LastDeed!.Source);
        Assert.Equal(
            town.Clock.Elapsed + GameDuration.FromHours((long)MightAndMagic7Tuning.TheftBanHours.Default),
            town.Party.Bans.BarredUntil(town.Shop.Id.Value, town.Clock.Elapsed));

        // A day later the counter serves the party again; the standing the party now carries costs it a hundred
        // more, and a reach that covers the cost — a grand master's ten levels — goes unseen. Nobody saw it, so
        // nothing is fined and the counter stays open, but the town notices what is missing: two points.
        town.Clock.Advance(GameDuration.FromHours(24));
        Assert.True(services.Open(town.Shop).IsApplied);
        random.Answer = Draws(luck: 2, seen: 50);
        ServiceResult unseen = services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", 1));
        Assert.True(unseen.IsApplied, unseen.Message);
        Assert.Contains("takes A fine sword from the shelf unseen", unseen.Message, StringComparison.Ordinal);
        Assert.Equal(300, town.Party.Debts.OwedOn(MightAndMagic7Theft.FineAccount));
        Assert.Equal(-3, town.Party.Reputation.Reputation);
        Assert.True(services.IsOpen);
        ItemInstance sword = town.Party.Inventory.Items.Single();
        Assert.True(sword.State.IsStolen);

        // A stolen thing is one no counter will buy, identify, or repair (OpenEnroth src/Engine/Objects/Item.cpp:684-686).
        string id = sword.Id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(MightAndMagic7Codes.ServiceItemStolen, services.Transact(new ServiceCommand(ServiceOperationKind.Sell, id)).Code);
        Assert.Equal(MightAndMagic7Codes.ServiceItemStolen, services.Transact(new ServiceCommand(ServiceOperationKind.Identify, id)).Code);

        // Reaching more than five hundred short is seen empty-handed: the novice's hundred against a cost the
        // party's worse standing has raised to six hundred.
        random.Answer = Draws(luck: 2, seen: 50);
        ServiceResult shortHanded = services.Transact(new ServiceCommand(ServiceOperationKind.Steal, "stock:sword", 0));
        Assert.Contains("is caught reaching", shortHanded.Message, StringComparison.Ordinal);
        Assert.Equal(900, town.Party.Debts.OwedOn(MightAndMagic7Theft.FineAccount));
        Assert.Equal(-4, town.Party.Reputation.Reputation);
        Assert.False(services.IsOpen);

        // Who may try is the donor's: somebody who has learned to steal and is in a state to act.
        Assert.Equal(1, novice.Skills.LevelOf(MightAndMagic7Theft.Stealing));
        Assert.Equal(MightAndMagic7Codes.TheftNoSkill, town.Rule.JudgeTheft(new ServiceTheftRequest(town.Party, town.Party.Members[2].Id, town.Clock, town.Shop))!.Code);
        Assert.Equal(MightAndMagic7Codes.TheftNotAShop, town.Rule.JudgeTheft(new ServiceTheftRequest(town.Party, novice.Id, town.Clock, town.Hall))!.Code);
        novice.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Unconscious));
        Assert.Equal(MightAndMagic7Codes.TheftCannotAct, town.Rule.JudgeTheft(new ServiceTheftRequest(town.Party, novice.Id, town.Clock, town.Shop))!.Code);

        // A product with no random service draws nothing, so it offers nobody the act.
        using Town undrawn = Town.Build(random: null);
        Assert.Equal(
            MightAndMagic7Codes.TheftChanceUnavailable,
            undrawn.Rule.JudgeTheft(new ServiceTheftRequest(undrawn.Party, undrawn.Party.Members[0].Id, undrawn.Clock, undrawn.Shop))!.Code);
    }

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_hand_in_a_person_s_purse_costs_a_point_every_time_and_a_fine_when_it_is_seen()
    {
        TestRandomService random = new();
        using Town town = Town.Build(random, personLevel: 4);
        PlacementDefinition stranger = Person("stranger");
        PlacementDefinition building = new(
            new PlacementContentId("service", "the-shop"),
            "placements",
            0,
            PlacePose.Origin,
            new ContentEntry("the-shop", JsonDocument.Parse("""{ "id": "the-shop", "kind": "service" }""").RootElement));

        // Only a person standing in the world carries a purse a hand could reach.
        Assert.Equal(MightAndMagic7Codes.TheftNobodyToRob, town.Rule.JudgeTheft(new ServiceTheftRequest(town.Party, town.Party.Members[0].Id, town.Clock, Place: new PlaceId("1"), Person: building))!.Code);
        Assert.Equal(2, town.Services.ThievesFrom(new PlaceId("1"), stranger).Count);

        // Unseen, the thief finds what the person carries — nothing, where no row says what they carry — and the
        // town still hears of the attempt: the donor's one point (OpenEnroth src/Engine/Objects/Actor.cpp:1236).
        random.Answer = Draws(luck: 4, seen: 50, find: 10);
        ServiceResult unseen = town.Services.StealFrom(new PlaceId("1"), stranger, 1);
        Assert.True(unseen.IsApplied, unseen.Message);
        Assert.Contains("finds nothing worth taking", unseen.Message, StringComparison.Ordinal);
        Assert.False(town.Services.LastTheft!.Caught);
        Assert.Equal(-1, town.Party.Reputation.Reputation);
        Assert.Equal(0, town.Party.Debts.OwedOn(MightAndMagic7Theft.FineAccount));

        // Seen, the fine is the person's own level and a hundred for each point of the place's base and the party's
        // standing in the donor's sign: four, two hundred, and a hundred — owed, as every fine is.
        random.Answer = Draws(luck: 0, seen: 50);
        ServiceResult seen = town.Services.StealFrom(new PlaceId("1"), stranger, 0);
        Assert.True(seen.IsApplied, seen.Message);
        Assert.True(town.Services.LastTheft!.Caught);
        Assert.Contains("is caught with a hand in somebody's purse", seen.Message, StringComparison.Ordinal);
        Assert.Equal(304, town.Party.Debts.OwedOn(MightAndMagic7Theft.FineAccount));
        Assert.Equal(-2, town.Party.Reputation.Reputation);
        Assert.Equal(MightAndMagic7Theft.PickpocketSource, town.Progression.LastDeed!.Source);
        Assert.Empty(town.Party.Bans.All);
    }

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_person_who_catches_a_hand_in_their_purse_stops_talking_and_turns_on_the_party()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(StreetContent());

        // The thief's worst luck: a novice's reach of a hundred less two hundred against a cost of the person's own
        // level, three, and a hundred for each of the street's two points of base fine.
        ((FakeEngineContext)context.Engine).RandomService.Answer = Draws(luck: 0, seen: 50);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui, combat: true) with { Use = UseControls, Service = ServiceControls, Conversation = ConversationControls });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));

        ProjectedNode talking = Latest(ui).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Robin", talking.Field("thieves").Item(0).Field("name").AsString());
        Assert.Equal(0, Latest(ui).Field("combat").Field("opposition").AsNumber());

        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Payload("""{"action":"conversation.steal","member":0}""")));

        // Seen: the person stops talking, the town fines the party what the donor reckons and lowers its opinion a
        // point, and the person robbed is put into the fight as an attack on them would put them there.
        ProjectedNode caught = Latest(ui);
        Assert.False(caught.Field("conversation").Field("open").AsBoolean());
        Assert.Equal("steal", caught.Field("service").Field("action").AsString());
        Assert.Contains("is caught with a hand in somebody's purse", caught.Field("service").Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(203, caught.Field("party").Field("debts").Item(0).Field("coins").AsNumber());
        Assert.Equal(-1, caught.Field("party").Field("reputation").AsNumber());
        session.Update(RulesetTestContext.Update(4, 1));
        Assert.Equal(1, Latest(ui).Field("combat").Field("opposition").AsNumber());
    }

    [Fact]
    public void The_place_s_base_fine_is_read_from_the_column_the_importer_carries()
    {
        using Town town = Town.Build(new TestRandomService());
        Assert.Equal(2, town.Theft.BaseFine(new PlaceId("1")));
        Assert.Equal(0, town.Theft.BaseFine(new PlaceId("2")));

        // The whole owed is kept between nothing and the donor's ceiling, whatever one deed is fined.
        Assert.Equal(100, MightAndMagic7Theft.Added(0, 100));
        Assert.Equal(50, MightAndMagic7Theft.Added(MightAndMagic7Crimes.FineCeiling - 50, 100));
        Assert.Equal(0, MightAndMagic7Theft.Added(10, -40));
    }

    /// <summary>The draws one theft takes, scripted by their purpose; every other draw of the session is left alone.</summary>
    private static Func<KeyedRngRequest, long?> Draws(int luck, int seen, int find = 0) => request =>
        request.Scope != MightAndMagic7Theft.RollScope ? null
        : request.Key.EndsWith("/pick", StringComparison.Ordinal) ? luck
        : request.Key.EndsWith("/seen", StringComparison.Ordinal) ? seen
        : request.Key.EndsWith("/find", StringComparison.Ordinal) ? find
        : null;

    /// <summary>A person a place's records stand there.</summary>
    private static PlacementDefinition Person(string id) => new(
        new PlacementContentId("person", id),
        "placements",
        0,
        PlacePose.Origin,
        new ContentEntry(id, JsonDocument.Parse($$"""{ "id": "{{id}}", "kind": "person" }""").RootElement));

    /// <summary>Uses what the party faces and asks its keeper for the counter, as a player does.</summary>
    private static void WalkIn(IGameSession session, RecordingUiService ui, ulong step)
    {
        session.Update(RulesetTestContext.Update(step, 1));
        session.Update(RulesetTestContext.Update(step + 1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(Latest(ui).Field("conversation").Field("open").AsBoolean());
        session.Update(RulesetTestContext.Update(step + 2, 1, RulesetTestContext.ChooseTopic(MightAndMagic7Conversation.CounterTopicId)));
    }

    private static ProjectedNode Latest(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value);

    /// <summary>
    /// Two places: a town whose base fine is two, with a weapon shop, and a town with a hall; a party of a novice
    /// thief, a grand master, and a knight who never learned.
    /// </summary>
    private static (string Path, string Text)[] TownContent() =>
    [
        RulesetTestContext.Bundle(RulesetTestContext.BundleId, "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", Places()),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            $$"""
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [ { "id": "party", "coins": 500, "food": 6, "reputation": 0, "fame": 0, "members": [ {{Members}} ] } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json", Items()),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json", Skills()),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json", Services()),
    ];

    /// <summary>A street with somebody standing in it, and a party with a novice thief.</summary>
    private static (string Path, string Text)[] StreetContent() =>
    [
        RulesetTestContext.Bundle(RulesetTestContext.BundleId, "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Market Row", "respawnDays": 7, "terrain": "grass", "encounterPercent": 0, "stealFine": 2,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "bystander", "kind": "person", "x": 100, "y": 0, "z": 0, "people": [ "person-1" ] } ] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [ { "id": "party", "coins": 100, "food": 6, "reputation": 0, "fame": 0, "members": [
                { "name": "Robin", "race": "Human", "class": "Thief", "level": 1, "hitPoints": 30, "spellPoints": 0,
                  "attributes": [ { "id": "Might", "value": 10 } ],
                  "skills": [ { "id": "Stealing", "level": 1, "tier": 1, "pointsSpent": 0 } ],
                  "spells": [], "conditions": [] } ] } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            { "documentId": "monsters", "definitionKind": "monster", "entries": [
              { "id": "100", "name": "Peasant", "level": 3, "hitPoints": 3, "armorClass": 5, "hostility": 0, "recovery": 100,
                "speed": 140, "aiType": "Wimp", "movement": "Long", {{MonsterRows.Combat(100, "Phys", "1D2")}} } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [ { "id": "person-1", "name": "A bystander", "topics": [] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json", Skills()),
    ];

    private static string Places() =>
        """
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "interior", "name": "Market Row", "respawnDays": 7, "stealFine": 2,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [ { "id": "the-shop", "kind": "service", "x": 100, "y": 0, "z": 0 } ] },
            { "id": "2", "kind": "interior", "name": "Hall Square", "respawnDays": 7, "stealFine": 0,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [ { "id": "the-hall", "kind": "service", "x": 100, "y": 0, "z": 0 } ] }
          ]
        }
        """;

    private static string Items() =>
        """
        {
          "documentId": "items",
          "definitionKind": "item",
          "entries": [ { "id": "sword", "name": "A fine sword", "value": 100 } ]
        }
        """;

    private static string Skills() =>
        """
        { "documentId": "skills", "definitionKind": "skill", "entries": [ { "id": "Sword" }, { "id": "Stealing" } ] }
        """;

    /// <summary>A weapon shop whose kind keeps a shelf a thief can reach, and a town hall that collects fines.</summary>
    private static string Services() =>
        """
        {
          "documentId": "services",
          "definitionKind": "service",
          "entries": [
            {
              "id": "the-shop", "kind": "Weapon Shop", "name": "The Sword and Shield", "proprietor": "Bertram", "mapId": 1,
              "operations": [ "buy", "sell", "identify", "steal" ],
              "openHour": 6, "closedHour": 18, "priceMultiplier": 1.5, "skillPriceMultiplier": 1.5,
              "stock": [ { "item": "sword", "count": 3, "value": 100, "name": "A fine sword" } ]
            },
            {
              "id": "the-hall", "kind": "Town Hall", "name": "The Town Hall", "proprietor": "Clerk Alden", "mapId": 2,
              "openHour": 6, "closedHour": 18, "priceMultiplier": 1, "skillPriceMultiplier": 1
            }
          ]
        }
        """;

    /// <summary>A novice thief, a grand master of the craft, and a knight who never learned it.</summary>
    private const string Members =
        """
        {
          "name": "Robin", "race": "Human", "class": "Thief", "level": 1, "hitPoints": 30, "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 10 } ],
          "skills": [ { "id": "Stealing", "level": 1, "tier": 1, "pointsSpent": 0 } ],
          "spells": [], "conditions": []
        },
        {
          "name": "Marian", "race": "Human", "class": "Thief", "level": 1, "hitPoints": 30, "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 10 } ],
          "skills": [ { "id": "Stealing", "level": 10, "tier": 4, "pointsSpent": 0 } ],
          "spells": [], "conditions": []
        },
        {
          "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
          "attributes": [ { "id": "Might", "value": 13 } ],
          "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
          "spells": [], "conditions": []
        }
        """;

    /// <summary>The same town read as policy: the theft rule, the counters, the scenario's party, and a clock.</summary>
    private sealed class Town : IDisposable
    {
        private Town(
            MightAndMagic7Theft theft,
            MightAndMagic7Services rule,
            PartyEntity party,
            GameClock clock,
            PartyProgression progression,
            PartyServices services)
        {
            Theft = theft;
            Rule = rule;
            Party = party;
            Clock = clock;
            Progression = progression;
            Services = services;
        }

        internal MightAndMagic7Theft Theft { get; }

        internal MightAndMagic7Services Rule { get; }

        internal PartyEntity Party { get; }

        internal GameClock Clock { get; }

        internal PartyProgression Progression { get; }

        internal PartyServices Services { get; }

        internal ServiceDefinition Shop => Rule.Describe(new ServiceTargetRequest(new PlaceId("1"), Placement("the-shop")))!;

        internal ServiceDefinition Hall => Rule.Describe(new ServiceTargetRequest(new PlaceId("2"), Placement("the-hall")))!;

        internal static Town Build(IRandomService? random, int? personLevel = null)
        {
            (ProductCreateContext context, _) = RulesetTestContext.Create(TownContent());
            ContentCatalog catalog = ContentCatalogLoader.Load(
                RulesetTestContext.Content(context),
                ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
            MightAndMagic7Theft theft = MightAndMagic7Theft.Read(catalog, random, loot: null, _ => personLevel);
            MightAndMagic7Services rule = MightAndMagic7Services.Read(catalog, theft: theft)!;
            PartyEntity party = MightAndMagic7Party.Compose(catalog)!;
            GameClock clock = new(
                GameCalendar.TwelveMonthsOfFourWeeks,
                new GameDate(1168, 1, 1, 9, 0, 0),
                new GameTimeScale(1),
                new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));
            PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
            PartyServices services = new(rule, party, new PartyResourceLedger(party), clock, progression);
            return new Town(theft, rule, party, clock, progression, services);
        }

        public void Dispose() => Party.Dispose();

        private static PlacementDefinition Placement(string id) => new(
            new PlacementContentId("service", id),
            "placements",
            0,
            PlacePose.Origin,
            new ContentEntry(id, JsonDocument.Parse($$"""{ "id": "{{id}}", "kind": "service" }""").RootElement));
    }
}
