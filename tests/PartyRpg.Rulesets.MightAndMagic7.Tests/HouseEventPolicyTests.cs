using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// A house's own event, run when the party uses the house, and a person an event moves to another house — over the
/// operator's own packs.
/// </summary>
/// <remarks>
/// The staged figures are the operator's install: the hostels of Celeste and The Pit (houses 428 and 433) are opened
/// by event 376 of each map, which first compares quest bit 127 (Celeste) or 109 (The Pit) and, holding it, moves the
/// party to the Small House instead (links 38 and 47); Harmondale's bank (house 128, event 31) is shut while quest
/// bit 183 is held; Castle Harmondale's door (event 301 of <c>out02.evt</c>) calls the butler over and moves him to house
/// 108.
/// </remarks>
public sealed partial class FixturePolicyTests
{
    private static readonly PlaceId Celeste = new("7");

    [ImportedFact("place-events.json")]
    public void A_house_door_runs_its_own_event_takes_the_move_its_branch_reaches_and_otherwise_opens_the_house()
    {
        ContentCatalog catalog = ImportedContent.Load();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlacePopulationContent population = PlacePopulationContent.Read(graph, MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        (MightAndMagic7Conversation conversation, IInteractionRule rule) = Housing(catalog, () => party, population, clock);

        // A party holding no bit is let into Celeste's hostel: the event reaches its house step, which opens the house
        // it names — this one — and nothing leads anywhere.
        PlacementDefinition hostel = Placed(population, Celeste, "residence-428");
        InteractionOutcome opened = House(rule, hostel, Celeste, party, clock, population, graph);
        Assert.True(opened.IsApplied, opened.Refusal?.Message);
        Assert.Null(opened.Travels);
        Assert.False(opened.KeptOut);
        Assert.Equal("residence-428", opened.Speaks?.Id);

        // Holding quest bit 127, the same door takes the party to the Small House, link 38, and nobody is spoken with.
        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("127"));
        InteractionOutcome away = House(rule, hostel, Celeste, party, clock, population, graph);
        Assert.Equal("38", away.Travels?.Transition.Source);
        Assert.Equal(new PlaceId("75"), away.Travels?.Transition.To);
        Assert.Null(away.Speaks);

        // Robert the Wise is in nobody's house until Gavin Magnus's topic (global event 146, once quest bit 102 is held)
        // moves the four advisors to their houses — Robert to Celeste's hostel, which then holds him.
        Assert.DoesNotContain(conversation.Describe(new ConversationTargetRequest(Celeste, hostel))!.People, person => person.Id == "npc-83");
        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("102"));
        (InteractionOutcome moved, _) = Answer(rule, Standing("npc-79"), Celeste, party, clock, "topic-146", graph);
        Assert.True(moved.IsApplied, moved.Refusal?.Message);
        Assert.True(moved.Residue.Length == 0, moved.Residue);
        Assert.Equal(428, MightAndMagic7PersonState.House(party.Records, "npc-83"));
        Assert.Contains(conversation.Describe(new ConversationTargetRequest(Celeste, hostel))!.People, person => person.Id == "npc-83");

        // Robert the Wise's topic sets quest bit 109 (#9042), and The Pit's hostel door then takes link 47: a topic that
        // sets a gating bit makes the gated link usable.
        foreach (int bit in new[] { 114, 115, 116, 117 }) party.Records.Mark(MightAndMagic7Quests.ErrandRecord(bit.ToString(CultureInfo.InvariantCulture)));
        PlacementDefinition robert = Standing("npc-83");
        ConversationContext talking = new(ThePit, robert, new ConversationSubject("person-robert", [conversation.PersonOf("npc-83")!]), "npc-83", [], party, clock);
        ConversationOffer offered = Assert.Single(conversation.Offers(talking), offer => offer.Id == "topic-155");
        ConversationAnswer answer = conversation.Take(offered.Topic, talking);
        (InteractionOutcome said, _) = Answer(rule, robert, ThePit, party, clock, answer.Handoff!.Target, graph);
        Assert.True(said.IsApplied, said.Refusal?.Message);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("109")));
        InteractionOutcome pit = House(rule, Placed(population, ThePit, "residence-433"), ThePit, party, clock, population, graph);
        Assert.Equal("47", pit.Travels?.Transition.Source);
        Assert.Equal(new PlaceId("75"), pit.Travels?.Transition.To);
    }

    [ImportedFact("place-events.json")]
    public void A_counter_its_own_event_shuts_keeps_the_party_outside_with_what_the_event_says()
    {
        ContentCatalog catalog = ImportedContent.Load();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlacePopulationContent population = PlacePopulationContent.Read(graph, MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        (_, IInteractionRule rule) = Housing(catalog, () => party, population, clock);
        // The bank's actual selected entrance carries the siege gate. The weapon shop's sign
        // carries unconditional event 4; its former event 3 came from an unrelated map's face.
        PlacementDefinition shop = Placed(population, Harmondale, "service-128");
        Assert.Equal(31, shop.Source.GetInt32("sourceEvent"));

        InteractionOutcome open = House(rule, shop, Harmondale, party, clock, population, graph);
        Assert.True(open.IsApplied, $"{open.Refusal?.Message} | {open.Message} | {open.Residue}");
        Assert.False(open.KeptOut);
        Assert.Equal("service-128", open.Speaks?.Id);

        party.Records.Mark(MightAndMagic7Quests.ErrandRecord("183"));
        InteractionOutcome shut = House(rule, shop, Harmondale, party, clock, population, graph);
        Assert.True(shut.IsApplied, shut.Refusal?.Message);
        Assert.True(shut.KeptOut, $"{shut.Message} | {shut.Residue}");
        Assert.Null(shut.Speaks);
        Assert.DoesNotContain("speaks with", shut.Message, StringComparison.Ordinal);
    }

    [ImportedFact("place-events.json")]
    public void The_castle_door_moves_the_butler_to_his_house_and_that_house_then_holds_him()
    {
        ContentCatalog catalog = ImportedContent.Load();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        PlacePopulationContent population = PlacePopulationContent.Read(graph, MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        (MightAndMagic7Conversation conversation, IInteractionRule rule) = Housing(catalog, () => party, population, clock);
        PlacementDefinition tavern = Placed(population, Harmondale, "service-108");
        Assert.DoesNotContain(conversation.Describe(new ConversationTargetRequest(Harmondale, tavern))!.People, person => person.Id == "npc-58");

        // The castle door's first opening calls the butler over and moves him to house 108, a record the party keeps.
        PlacementDefinition door = Placed(population, Harmondale, "fixture-301");
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(Harmondale, door, string.Empty))!;
        InteractionOutcome used = rule.Apply(target, Context(door, target, Harmondale, party, clock, population, graph));
        Assert.True(used.IsApplied, used.Refusal?.Message);
        Assert.Equal("npc-58", Assert.Single(used.Speaks!.People).Id);
        Assert.Equal(108, MightAndMagic7PersonState.House(party.Records, "npc-58"));

        // The house he moved to holds him now, beside whoever content placed there.
        ConversationSubject inside = conversation.Describe(new ConversationTargetRequest(Harmondale, tavern))!;
        Assert.Contains(inside.People, person => person.Id == "npc-58");
        Assert.Contains(inside.People, person => person.Id == "npc-95");

        // A greeting an event changes is the greeting table's row it names, and the person greets the party as if for
        // the first time (OpenEnroth src/Engine/Evt/EvtInterpreter.cpp:541-545).
        party.Records.Set("met:npc-58", 1);
        MightAndMagic7PersonState.Greet(party.Records, "npc-58", 2);
        Assert.False(party.Records.Has("met:npc-58"));
        string row = catalog.Entries(MightAndMagic7Conversation.GreetingDefinitionKind).Single(entry => entry.Entry.Id == "2").Entry.GetString("greeting");
        Assert.Equal(row, conversation.Greeting(new ConversationContext(Harmondale, tavern, inside, "npc-58", [], party, clock)).Text);

        // A save carrying the move is judged like the party's other records: a person the content lacks is refused.
        MightAndMagic7Fixtures judge = new(MightAndMagic7MapEvents.Read(catalog), people: conversation.PersonOf, greetings: conversation.HasGreeting);
        Assert.Null(judge.JudgeRecord(MightAndMagic7PersonState.HouseRecord("npc-58", 108), 1));
        Assert.NotNull(judge.JudgeRecord(MightAndMagic7PersonState.HouseRecord("npc-99999", 108), 1));
        Assert.NotNull(judge.JudgeRecord(MightAndMagic7PersonState.GreetingRecord("npc-58", 9999), 1));
    }

    [Fact]
    public void A_house_used_in_play_opens_when_its_event_lets_the_party_in_keeps_it_out_or_leads_it_away()
    {
        // The staged hostel's door is event 9, shaped as the operator's houses write theirs: holding quest bit 7 the party
        // is moved on (link 7), holding bit 8 the door says it is shut, and otherwise the house opens.
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(HouseWorld());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
                Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
            });
        session.Start();
        MightAndMagic7Session played = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));

        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(played.Owners.Conversations!.IsOpen);
        Assert.Equal("Innkeeper", played.Owners.Conversations.Speaker?.Name);
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.ConversationLeaveIntent)));

        // Shut: the door's own line is the use's message, and nobody is spoken with.
        played.Party!.Records.Mark(MightAndMagic7Quests.ErrandRecord("8"));
        session.Update(RulesetTestContext.Update(4, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.False(played.Owners.Conversations.IsOpen);
        Assert.Equal("The hostel is shut.", played.World!.Interaction!.LastResult!.Message);
        Assert.True(played.World.Interaction.LastResult.KeptOut);

        // Led away: the door's branch takes link 7 through the one transition path, and nobody is spoken with.
        played.Party.Records.Mark(MightAndMagic7Quests.ErrandRecord("7"));
        session.Update(RulesetTestContext.Update(5, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal(new PlaceId("2"), played.World.Party.Place);
        Assert.False(played.Owners.Conversations.IsOpen);
    }

    /// <summary>A hall holding a hostel whose door event can let the party in, keep it out, or move it to a second place.</summary>
    private static (string Path, string Text)[] HouseWorld() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person", "references": [ "person:npc-3" ] },
                { "path": "place-events.json", "documentId": "place-events", "definitionKind": "place-event" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "Hall", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "residence-5", "kind": "residence", "x": 100, "y": 0, "z": 0, "houseId": 5, "name": "Hostel", "sourceEvent": 9, "people": [ "npc-3" ] } ] },
              { "id": "2", "kind": "interior", "name": "Small House", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 5, "y": 5, "z": 0, "yaw": 0 } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            { "documentId": "links", "definitionKind": "travel-link", "entries": [ { "id": "7", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "npc-3", "npcId": 3, "name": "Innkeeper", "portrait": "707", "greeting": "'A bed?'", "house": 5, "topics": [] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/place-events.json",
            """
            { "documentId": "place-events", "definitionKind": "place-event", "entries": [
              { "id": "1.9", "place": "1", "event": 9, "raised": false, "house": true, "timed": false, "steps": [
                { "step": 0, "op": "compare", "variable": "quest-bit", "value": 7, "target": 4 },
                { "step": 1, "op": "compare", "variable": "quest-bit", "value": 8, "target": 5 },
                { "step": 2, "op": "speak-in-house", "house": 5 },
                { "step": 3, "op": "exit" },
                { "step": 4, "op": "move-to-map", "link": "7", "toPlace": 2, "travel": "entrance" },
                { "step": 5, "op": "status-text", "textId": 3, "text": "The hostel is shut." },
                { "step": 6, "op": "exit" } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            { "documentId": "party", "definitionKind": "scenario-party", "entries": [
              { "id": "party", "coins": 120, "food": 4, "reputation": 0, "fame": 0,
                "members": [
                  { "name": "Tester", "race": "testfolk", "class": "fighter", "level": 1, "hitPoints": 10, "spellPoints": 5,
                    "attributes": [ { "id": "vigour", "value": 12 } ], "skills": [], "spells": [], "conditions": [] } ] } ] }
            """),
    ];

    /// <summary>This game's conversation and its people's interaction, composed over the imported content as a world composes them.</summary>
    /// <remarks>
    /// A place's own timers run before the event a use raises when they keep what it compares (Harmondale's siege timer,
    /// event 51, clears the bit that shuts its shops once group 9 is down), so the population a count of the dead reads is
    /// the place's own, none of it down, and the journal a history line writes into is the party's.
    /// </remarks>
    private static (MightAndMagic7Conversation Conversation, IInteractionRule Rule) Housing(
        ContentCatalog catalog,
        Func<PartyEntity?> party,
        PlacePopulationContent population,
        GameClock clock)
    {
        MightAndMagic7Fixtures? fixtures = null;
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), events: () => fixtures, party: party)!;
        PartyJournal journal = new(new MightAndMagic7Journal(MightAndMagic7Loot.Compose(catalog, new KeyedTestRandom())), clock);
        PartyProgression? progression = party() is { } playing ? new PartyProgression(MightAndMagic7Progression.Instance, playing) : null;
        fixtures = new MightAndMagic7Fixtures(
            MightAndMagic7MapEvents.Read(catalog),
            random: new KeyedTestRandom(),
            progression: () => progression,
            people: conversation.PersonOf,
            actors: place => [.. population.PlacementsOf(place).Where(MightAndMagic7Fixtures.IsActor).Select(placement => new PlaceActor(placement, Down: false))],
            journal: () => journal,
            topics: conversation.SpokenTopic,
            greetings: conversation.HasGreeting);
        return (conversation, new MightAndMagic7PeopleInteraction(conversation, new MightAndMagic7Interaction(fixtures: fixtures), fixtures));
    }

    /// <summary>Uses a house as the interaction mechanism does, over the place's own placements and transitions.</summary>
    private static InteractionOutcome House(
        IInteractionRule rule,
        PlacementDefinition house,
        PlaceId place,
        PartyEntity party,
        GameClock clock,
        PlacePopulationContent population,
        PlaceGraph graph)
    {
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(place, house, string.Empty))!;
        Assert.Equal(MightAndMagic7Conversation.PersonTargetKind, target.Kind.Value);
        return rule.Apply(target, Context(house, target, place, party, clock, population, graph));
    }

    private static InteractionContext Context(
        PlacementDefinition placement,
        InteractionTargetDefinition target,
        PlaceId place,
        PartyEntity party,
        GameClock clock,
        PlacePopulationContent population,
        PlaceGraph graph) =>
        new(place, placement, target, party, clock)
        {
            PlaceTargets = population.PlacementsOf(place),
            PlaceTransitions = graph.TransitionsFrom(place),
        };
}
