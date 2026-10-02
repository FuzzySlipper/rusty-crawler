using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// A person's topic runs the global program's event of its number through this game's one interpretation of event
/// steps, offered when the event's own offer check allows it — over the operator's own packs.
/// </summary>
/// <remarks>
/// The staged figures are the operator's install: Robert the Wise's only slot raises global event 155, offered once
/// quest bits 114 to 117 are held, which says his line, sets quest bit 109 and turns his slot to row 156; quest bit 109
/// is what The Pit's door event 376 (<c>d26.evt</c>) compares before its move to the Small House, link 47.
/// </remarks>
public sealed partial class FixturePolicyTests
{
    private static readonly PlaceId ThePit = new("8");

    [ImportedFact("global-events.json")]
    public void A_persons_topic_runs_its_global_event_and_the_quest_bit_it_sets_opens_the_move_it_gates()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        (MightAndMagic7Conversation conversation, MightAndMagic7Interaction rule) = Speaking(catalog, events);
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        PlacementDefinition robert = Standing("npc-83");
        ConversationContext talking = new(
            ThePit,
            robert,
            new ConversationSubject("person-robert", [conversation.PersonOf("npc-83")!]),
            "npc-83",
            [],
            party,
            clock);

        // The topic is offered under the topic table's row and withheld until the event's own offer check allows it.
        ConversationOffer withheld = Assert.Single(conversation.Offers(talking), offer => offer.Id == "topic-155");
        Assert.False(withheld.IsOnOffer);
        foreach (int bit in new[] { 114, 115, 116, 117 }) party.Records.Mark(MightAndMagic7Quests.ErrandRecord(bit.ToString(CultureInfo.InvariantCulture)));
        ConversationOffer offered = Assert.Single(conversation.Offers(talking), offer => offer.Id == "topic-155");
        Assert.True(offered.IsOnOffer, offered.Availability.Explanation);

        // Taking it hands the party to the event, run as a use of the place where Robert stands; the run's message is
        // what he says.
        ConversationAnswer answer = conversation.Take(offered.Topic, talking);
        Assert.Equal(string.Empty, answer.Text);
        Assert.Equal(HandoffOwner.Use, answer.Handoff!.Owner);
        (InteractionOutcome said, string state) = Answer(rule, robert, ThePit, party, clock, answer.Handoff.Target, graph);
        Assert.True(said.IsApplied, said.Refusal?.Message);
        Assert.Contains("command cube", said.Message, StringComparison.Ordinal);
        Assert.Equal(string.Empty, said.Residue);
        Assert.Equal(MightAndMagic7Fixtures.SpokenState, state);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("109")));
        Assert.Equal(156, MightAndMagic7TopicSlots.Raised(party.Records, "npc-83", 0));

        // The slot change is read back: the row Robert's slot raised is withdrawn and the one it raises now offered.
        Assert.DoesNotContain(conversation.Offers(talking), offer => offer.Id == "topic-155");
        Assert.Contains(conversation.Offers(talking), offer => offer.Id == "topic-156");
    }

    [ImportedFact("global-events.json")]
    public void Every_topic_raised_global_event_runs_or_is_refused_by_a_named_step_and_the_split_is_counted()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        GameClock clock = TestClock.Create(scale: 1);
        MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(catalog);
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog, MightAndMagic7Skills.Read(catalog, promotions))!;
        KeyedTestRandom random = new();
        MightAndMagic7Loot loot = MightAndMagic7Loot.Compose(catalog, random);
        MightAndMagic7Fixtures? fixtures = null;
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), events: () => fixtures)!;
        int applied = 0;
        int travelled = 0;
        SortedDictionary<string, int> refused = new(StringComparer.Ordinal);
        foreach (MapEvent spoken in events.GlobalEvents.Where(candidate => candidate.Topic).OrderBy(candidate => candidate.Id))
        {
            using PartyEntity party = Party();
            PartyProgression progression = new(MightAndMagic7Progression.Instance, party, promotions: promotions);
            PartyJournal journal = new(new MightAndMagic7Journal(loot), clock);
            fixtures = new MightAndMagic7Fixtures(
                events,
                effects: new MightAndMagic7SpellEffects(spells, clock),
                random: random,
                loot: loot,
                spells: spells,
                progression: () => progression,
                people: conversation.PersonOf,
                actors: _ => [],
                journal: () => journal,
                topics: conversation.SpokenTopic,
                greetings: conversation.HasGreeting);
            MightAndMagic7Interaction rule = new(fixtures: fixtures);
            string topic = string.Create(CultureInfo.InvariantCulture, $"topic-{spoken.Id}");
            Assert.NotNull(conversation.SpokenTopic(topic));
            (InteractionOutcome outcome, _) = Answer(rule, Standing("npc-1"), Harmondale, party, clock, topic, graph);

            // A run that met a step this game does not interpret still answers — with what was said — and its residue
            // is the refusal, naming the step or the variable.
            Assert.True(outcome.IsApplied, outcome.Refusal?.Message);
            if (!outcome.Residue.Contains("nothing was changed", StringComparison.Ordinal))
            {
                applied++;
                if (outcome.Travels is not null) travelled++;
                continue;
            }

            string reason = outcome.Residue.Contains("which this game does not interpret for that instruction", StringComparison.Ordinal)
                ? Between(outcome.Residue, "the variable '", "'")
                : outcome.Residue.Contains("' instruction", StringComparison.Ordinal)
                    ? Between(outcome.Residue, "a '", "' instruction")
                    : Between(outcome.Residue, " is ", ", which this game does not interpret");
            refused[reason] = refused.GetValueOrDefault(reason) + 1;
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"applied {applied}, travelled {travelled}"));
        foreach ((string reason, int count) in refused) output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"refused {reason}: {count}"));
        Assert.Equal(events.GlobalEvents.Count(candidate => candidate.Topic), applied + refused.Values.Sum());

        // The figures the ruleset README states for the operator's install: what a fresh party's choice of each topic
        // reaches. The three moves are the world's own: the crossing to Harmondale (link 68) and the temples' (69, 70).
        Assert.Equal(353, applied);
        Assert.Equal(3, travelled);
        // Counted deeds read canonical records; only hireling steps remain refused (#8514).
        string[] stated =
        [
            "hireling: 12",
        ];
        Assert.Equal(stated, refused.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}: {entry.Value}")));
    }

    [Fact]
    public void A_topic_chosen_in_play_runs_its_event_says_its_line_settles_what_it_gives_and_takes_the_worlds_move()
    {
        // The staged person's slot raises global event 9, shaped as the operator's install writes one: an offer check
        // on quest bit 7, the line it shows, a quest bit, a purse, and the world's own move (the global program's links
        // are issued from no place). The party holds bit 7, so the topic is offered.
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(TopicWorld());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
                Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
            });
        session.Start();
        MightAndMagic7Session played = (MightAndMagic7Session)session;
        played.Party!.Records.Mark(MightAndMagic7Quests.ErrandRecord("7"));
        int coins = played.Party.Purse.Coins;

        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("topic-9", talking.Field("topics").Item(0).Field("id").AsString());

        // Choosing it runs the event as a use of where the steward stands: the line is what he says, the bit and the
        // purse are settled through their owners, and the move is taken through the one transition path.
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.ChooseTopic("topic-9")));
        Assert.True(played.Party.Records.Has(MightAndMagic7Quests.ErrandRecord("98")));
        Assert.Equal(coins + 50, played.Party.Purse.Coins);
        Assert.Equal(new PlaceId("2"), played.World!.Party.Place);
        InteractionResult spoken = played.World.Interaction!.LastResult!;
        Assert.Contains("The castle is yours", spoken.Message, StringComparison.Ordinal);
        Assert.Equal("68", spoken.Travels?.Transition.Source);
        Assert.Equal(TransitionKind.Scripted, spoken.Travels?.Kind);

        // The steward stayed behind, so the conversation ended when the party left.
        Assert.False(played.Owners.Conversations!.IsOpen);
    }

    /// <summary>A world of two places, a steward whose topic raises a global event, and that event's world-issued move.</summary>
    private static (string Path, string Text)[] TopicWorld() =>
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
                { "path": "global-events.json", "documentId": "global-events", "definitionKind": "global-event" },
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
                "placements": [ { "id": "person-0", "kind": "person", "x": 100, "y": 0, "z": 0, "people": [ "npc-3" ] } ] },
              { "id": "2", "kind": "region", "name": "Castle", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 5, "y": 5, "z": 0, "yaw": 0 } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            { "documentId": "links", "definitionKind": "travel-link", "entries": [ { "id": "68", "toPlace": "2", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "npc-3", "npcId": 3, "name": "Steward", "portrait": "707", "greeting": "'My lords.'", "dialogueEvents": 1,
                "topicSlots": [ 9, 0, 0, 0, 0, 0 ],
                "topics": [ { "id": "topic-9", "label": "The castle", "text": "", "textCount": 0, "event": 9 } ] } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/global-events.json",
            """
            { "documentId": "global-events", "definitionKind": "global-event", "entries": [
              { "id": "9", "event": 9, "topic": true, "steps": [
                { "step": 0, "op": "can-show-dialog-item-compare", "variable": "quest-bit", "value": 7, "target": 3 },
                { "step": 1, "op": "set-can-show-dialog-item", "on": false },
                { "step": 2, "op": "end-can-show-dialog-item" },
                { "step": 3, "op": "set-can-show-dialog-item", "on": true },
                { "step": 4, "op": "end-can-show-dialog-item" },
                { "step": 5, "op": "show-message", "textId": 12, "text": "The castle is yours, my lords." },
                { "step": 6, "op": "set", "variable": "quest-bit", "value": 98 },
                { "step": 7, "op": "add", "variable": "gold", "value": 50 },
                { "step": 8, "op": "move-to-map", "link": "68", "toPlace": 2, "travel": "scripted" } ] } ] }
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

    /// <summary>This game's conversation and interaction composed over the imported content as a session composes them.</summary>
    private static (MightAndMagic7Conversation Conversation, MightAndMagic7Interaction Rule) Speaking(ContentCatalog catalog, MightAndMagic7MapEvents events)
    {
        MightAndMagic7Fixtures? fixtures = null;
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), events: () => fixtures)!;
        fixtures = new MightAndMagic7Fixtures(events, random: new KeyedTestRandom(), people: conversation.PersonOf, topics: conversation.SpokenTopic, greetings: conversation.HasGreeting);
        return (conversation, new MightAndMagic7Interaction(fixtures: fixtures));
    }

    /// <summary>Runs what a person's word raised as the interaction mechanism's answer does, answering with the state it reads as.</summary>
    private static (InteractionOutcome Outcome, string State) Answer(
        IInteractionRule rule,
        PlacementDefinition placement,
        PlaceId place,
        PartyEntity party,
        GameClock clock,
        string raised,
        PlaceGraph graph)
    {
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(place, placement, string.Empty) { Raised = raised })!;
        InteractionOutcome outcome = rule.Apply(target, new InteractionContext(place, placement, target, party, clock)
        {
            PlaceTargets = [placement],
            PlaceTransitions = [.. graph.TransitionsFrom(place), .. graph.WorldIssued],
            Raised = raised,
        });
        if (outcome is { IsApplied: true, Gain.IsFree: false }) party.Purse.Credit(outcome.Gain.Coins);
        foreach (InteractionItemYield item in outcome.IsApplied ? outcome.Items : []) party.AcquireItem(item.Definition, item.Count);
        return (outcome, outcome.State);
    }

    /// <summary>A person standing in the open, as the importer writes a map's own actor record of one.</summary>
    private static PlacementDefinition Standing(string person)
    {
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "person-0", "kind": "person", "sourceField": "actors", "sourceIndex": 0, "x": 0, "y": 0, "z": 0, "people": [ {{JsonSerializer.Serialize(person)}} ] }""");
        return new PlacementDefinition(new PlacementContentId("person", "person-0"), "actors", 0, PlacePose.Origin, new ContentEntry("person-0", JsonDocument.Parse(json).RootElement));
    }
}
