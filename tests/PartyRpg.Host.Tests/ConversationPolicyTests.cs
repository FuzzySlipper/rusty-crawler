using System.Text.RegularExpressions;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's answers about talking to somebody: who the content places, what they say, what a topic waits
/// for, and how a counter behind them is reached.
/// </summary>
/// <remarks>
/// <para>
/// The policy is exercised directly over a catalog and a real party, because that is where availability is
/// decided: a topic's conditions are judged against the party's own effects, standing, members, and the one
/// clock, and a test that moved any of those through a session would be proving the session rather than the
/// policy. What the product does with the answers — the conversation a use opens and the projection a screen
/// reads — is proved over the product as well, in the walk-in case below.
/// </para>
/// <para>
/// The person and topic entries are written the way the importer writes them, so what the policy reads here
/// is the shape an imported pack carries rather than one invented for a test.
/// </para>
/// </remarks>
public sealed class ConversationPolicyTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId SomewherePlace = new("1");

    [Fact]
    public void The_conversation_controls_are_declared_in_code_in_the_project_file_and_in_the_companion()
    {
        string source = File.ReadAllText(Path.Combine(SourceDirectory(), "ProductIdentity.cs"));
        string project = File.ReadAllText(ProjectFile());
        string product = File.ReadAllText(Path.Combine(SourceDirectory(), "CrawlerProduct.cs"));
        string ui = File.ReadAllText(Path.Combine(SourceDirectory(), "..", "ui", "main.ts"));

        string intent = Constant(source, "ConversationLeaveIntent");

        // Declared in code and in the project file, and mapped there: the engine refuses a mapping whose
        // intent was never declared, so a name that exists in code alone is a key that does nothing.
        Assert.Contains($"RustyEngineProductInputIntent Include=\"{intent}\" Value=\"digital\"", project, StringComparison.Ordinal);
        Assert.Contains($"Intent=\"{intent}\"", project, StringComparison.Ordinal);

        // The key the product actually declared, named so a change to it is a decision rather than a silent
        // edit: the original leaves a house's dialogue with Escape, and this build keeps it.
        Assert.Contains("Trigger=\"key:escape:pressed\"", project, StringComparison.Ordinal);

        // The host hands the ruleset the declared names and the companion sends the same three actions on
        // the product's own contract, so a button and a key ask for exactly the same thing.
        Assert.Contains("new ConversationIntentNames(", product, StringComparison.Ordinal);
        Assert.Contains("Conversation: _conversation", product, StringComparison.Ordinal);
        AssertUiConstant(ui, "ACTION_CONVERSATION_TOPIC", ConversationActions.Topic);
        AssertUiConstant(ui, "ACTION_CONVERSATION_PERSON", ConversationActions.Person);
        AssertUiConstant(ui, "ACTION_CONVERSATION_LEAVE", ConversationActions.Leave);
    }

    [Fact]
    public void A_person_says_what_content_gives_them_and_a_line_the_party_has_not_heard()
    {
        using Fixture fixture = Fixture.Build();
        PlacementDefinition person = fixture.Placement("person-0");
        ConversationSubject subject = fixture.Conversation.Describe(new ConversationTargetRequest(SomewherePlace, person))!;

        // Who is here and what they are called come from content's own person entry, which the placement
        // names; the greeting is the one the game's greeting table gives them for a first meeting.
        Assert.Equal("person-0", subject.Id);
        ConversationPerson speaker = Assert.Single(subject.People);
        Assert.Equal("np-2", speaker.Id);
        Assert.Equal("Tester Two", speaker.Name);
        Assert.Equal("707", speaker.Portrait);
        Assert.Equal("'A fine day for it.'", Greeting(fixture, subject, speaker.Id).Text);

        // A person the party has met is greeted the other way, which is party-carried state rather than
        // anything the conversation remembers: the flag is the same one the greeting records.
        fixture.Party.Effects.Apply(new PartyEffect(new EffectId("met:np-2"), 1));
        Assert.Equal("'Still at it, then?'", Greeting(fixture, subject, speaker.Id).Text);
        fixture.Party.Effects.Remove(new EffectId("met:np-2"));

        // What they can be asked about is the topic table's own rows, with a line from the text table and
        // the errand the row states read as a condition.
        IReadOnlyList<ConversationOffer> offers = fixture.Offers(person);
        ConversationOffer plain = Assert.Single(offers, offer => offer.Id == "topic-1");
        Assert.True(plain.IsOnOffer);
        Assert.Empty(plain.Topic.Conditions);
        ConversationOffer gated = Assert.Single(offers, offer => offer.Id == "topic-2");
        Assert.False(gated.IsOnOffer);
        Assert.Equal(ConversationConditionKind.Errand, Assert.Single(gated.Topic.Conditions).Kind);
        Assert.Contains("the errand the table calls 7 is not finished", gated.Availability.Reason, StringComparison.Ordinal);

        // Taking the line says what content says, records that it was heard on the party, and states the
        // residue: the original runs an event program behind a reply and nothing here does.
        ConversationAnswer answer = fixture.Take(plain.Topic, person, "np-2");
        Assert.Equal("'The first to bring the items wins.'", answer.Text);
        Assert.Equal(["heard:topic-1"], answer.Records);
        Assert.Contains("event programs", Greeting(fixture, subject, speaker.Id).Residue, StringComparison.Ordinal);

        // A line the table gives in more than one version says so beside the one it hands over: the original
        // chooses between them by the state of its event programs, which this build does not run.
        ConversationOffer branched = Assert.Single(fixture.Offers(person), offer => offer.Id == "topic-3");
        Assert.Contains("2 versions of this answer", fixture.Take(branched.Topic, person, "np-2").Residue, StringComparison.Ordinal);
    }

    [Fact]
    public void A_topic_waits_for_the_state_it_names_and_appears_when_that_state_holds()
    {
        using Fixture fixture = Fixture.Build();
        PlacementDefinition person = fixture.Placement("person-0");

        // A topic whose condition is an errand appears when the party has finished it, which is the flag the
        // quest owner will set: the mechanism reads the party's own effects, so nothing here is remembered.
        Assert.DoesNotContain("topic-2", fixture.OnOffer(person));
        fixture.Party.Effects.Apply(new PartyEffect(new EffectId("errand:7"), 1));
        Assert.Contains("topic-2", fixture.OnOffer(person));
        fixture.Party.Effects.Remove(new EffectId("errand:7"));

        // A topic that waits for a class or a race reads the party's own members, and one that waits for a
        // standing reads the party's own reputation: neither is a fact about the conversation.
        Assert.Contains("topic-4", fixture.OnOffer(person));
        Assert.Contains("topic-5", fixture.OnOffer(person));
        fixture.Party.Reputation.ChangeReputation(-9);
        Assert.DoesNotContain("topic-6", fixture.OnOffer(person));
        Assert.Contains("standing is 1", fixture.Offer(person, "topic-6").Availability.Reason, StringComparison.Ordinal);

        // A topic that waits for the hour reads the session's one clock, and a place whose clock says night
        // offers a different set: the same content, read against the state it names.
        Assert.Contains("topic-7", fixture.OnOffer(person));
        fixture.Clock.Advance(GameDuration.FromHours(14));
        Assert.DoesNotContain("topic-7", fixture.OnOffer(person));
        Assert.Contains("clock stands at", fixture.Offer(person, "topic-7").Availability.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_keeper_offers_the_counter_while_it_is_open_and_hands_the_party_to_the_service_mechanism()
    {
        using Fixture fixture = Fixture.Build();
        PlacementDefinition counter = fixture.Placement("service-7");
        ConversationSubject subject = fixture.Conversation.Describe(new ConversationTargetRequest(SomewherePlace, counter))!;

        // A building's people come from the person entries its placement names, so the keeper is whoever the
        // game's own tables put behind it rather than a name the building states.
        ConversationPerson keeper = Assert.Single(subject.People);
        Assert.Equal("Tester One", keeper.Name);

        // What the keeper offers is the counter itself, and it is on offer while the shop's own hours say it
        // serves: the window is the one the counter reads, so the two cannot disagree about when it opens.
        ConversationOffer offer = fixture.Offer(counter, MightAndMagic7Conversation.CounterTopicId);
        Assert.True(offer.IsOnOffer);
        ConversationAnswer answer = fixture.Take(offer.Topic, counter, keeper.Id);
        Assert.Equal("service", answer.Handoff!.Kind);

        // Shut for the night, the same offer is withheld with the hours as the reason: the topic appears and
        // disappears with the state it names rather than with an invalidation.
        fixture.Clock.Advance(GameDuration.FromHours(12));
        ConversationOffer shut = fixture.Offer(counter, MightAndMagic7Conversation.CounterTopicId);
        Assert.False(shut.IsOnOffer);
        Assert.Contains("it is shut", shut.Availability.Reason, StringComparison.Ordinal);
        Assert.Contains("06:00–18:00", shut.Availability.Reason, StringComparison.Ordinal);

        // A building the tables name nobody for still has somebody behind the door: the proprietor the
        // counter's own definition states, because a shop nobody could speak with is a shop nobody can enter.
        ConversationSubject residence = fixture.Conversation.Describe(
            new ConversationTargetRequest(SomewherePlace, fixture.Placement("residence-9")))!;
        Assert.Equal("Mira", Assert.Single(residence.People).Name);
    }

    [Fact]
    public void A_use_on_somebody_opens_the_conversation_and_a_topic_that_stopped_holding_is_refused()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Staged());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = new UseIntentNames(ProductIdentity.UseIntent, ProductIdentity.UseAction, ProductIdentity.UiActionContract),
                Service = new ServiceIntentNames(ProductIdentity.ServiceLeaveIntent, ProductIdentity.UiActionContract),
                Conversation = new ConversationIntentNames(ProductIdentity.ConversationLeaveIntent, ProductIdentity.UiActionContract),
            });
        session.Start();

        // The reticle reaches whoever content places, and the use speaks with them: what the panel shows is
        // the greeting, the topics on offer, and what the state withholds with its reason.
        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("Tester Two", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Tester Two", talking.Field("speaker").AsString());
        Assert.Equal("'A fine day for it.'", talking.Field("greeting").AsString());
        Assert.Equal("topic-1", talking.Field("topics").Item(0).Field("id").AsString());
        Assert.Equal("topic-2", talking.Field("withheld").Item(0).Field("id").AsString());
        Assert.Contains("is not finished", talking.Field("withheld").Item(0).Field("reason").AsString(), StringComparison.Ordinal);

        // A topic the state withholds is refused by name when it is asked for anyway, and the conversation
        // stays exactly where it was rather than applying an answer from a list that no longer holds.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("topic-2")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("conversation-topic-withheld", refused.Field("code").AsString());

        // What was said is on the panel beside the line, and the line leaves the list for the rest of this
        // conversation because the person has already said it.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("topic-1")));
        ProjectedNode said = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.Equal("applied", said.Field("outcome").AsString());
        Assert.Contains("The first to bring the items wins.", said.Field("message").AsString(), StringComparison.Ordinal);

        // What the person's replies cannot deliver is stated where they start rather than on every line: the
        // original runs an event program behind a reply, and nothing in this build executes one.
        Assert.Contains(
            "event programs",
            said.Field("said").Item(0).Field("residue").AsString(),
            StringComparison.Ordinal);
        Assert.Equal(0, ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("topics").Length());
        ProjectedNode withheld = ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("withheld");
        Assert.Contains(
            "topic-1",
            Enumerable.Range(0, withheld.Length()).Select(position => withheld.Item(position).Field("id").AsString()));
        ProjectedNode line = Assert.Single(
            Enumerable.Range(0, withheld.Length()).Select(withheld.Item),
            offer => offer.Field("id").AsString() == "topic-1");
        Assert.Contains("already said this", line.Field("reason").AsString(), StringComparison.Ordinal);

        // Leaving and speaking again greets the party the other way, because meeting somebody is state the
        // party carries rather than something the conversation kept.
        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.Digital(ProductIdentity.ConversationLeaveIntent)));
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode again = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.Equal("'Still at it, then?'", again.Field("greeting").AsString());
        Assert.Equal("topic-1", again.Field("topics").Item(0).Field("id").AsString());
    }

    /// <summary>The content the walk-in case stages, written the way the importer writes a pack.</summary>
    private static (string Path, string Text)[] Staged() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:np-2" ] },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "Somewhere", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "person-0", "kind": "person", "x": 100, "y": 0, "z": 0, "people": [ "np-2" ] } ] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "np-2", "npcId": 2, "name": "Tester Two", "portrait": "707",
                "greeting": "'A fine day for it.'", "greetingAgain": "'Still at it, then?'", "dialogueEvents": 2,
                "topics": [
                  { "id": "topic-1", "label": "The contest", "text": "'The first to bring the items wins.'", "textCount": 1 },
                  { "id": "topic-2", "label": "The errand", "text": "'I have work for you.'", "textCount": 1, "requires": 7 } ] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 120, "food": 4, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Tester", "race": "testfolk", "class": "fighter", "level": 1,
                      "hitPoints": 10, "spellPoints": 5,
                      "attributes": [ { "id": "vigour", "value": 12 } ],
                      "skills": [], "spells": [], "conditions": [] } ] }
              ]
            }
            """),
    ];

    /// <summary>The policy, a party, and a clock the availability cases move by hand.</summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(ContentCatalog catalog, MightAndMagic7Conversation conversation, PartyEntity party, GameClock clock)
        {
            Catalog = catalog;
            Conversation = conversation;
            Party = party;
            Clock = clock;
        }

        internal ContentCatalog Catalog { get; }

        internal MightAndMagic7Conversation Conversation { get; }

        internal PartyEntity Party { get; }

        internal GameClock Clock { get; }

        internal static Fixture Build()
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new PolicyContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Places())
                    .Add("packs/world/people.json", People())
                    .Add("packs/world/services.json", Services()),
                Layout).RequireValid();
            MightAndMagic7Services services = MightAndMagic7Services.Read(catalog)
                ?? throw new InvalidOperationException("The content declares services, so the policy must be read.");
            MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, services)
                ?? throw new InvalidOperationException("The content declares people, so the dialogue policy must be read.");
            GameClock clock = new(
                GameCalendar.TwelveMonthsOfFourWeeks,
                new GameDate(1168, 1, 1, 9, 0, 0),
                new GameTimeScale(1),
                new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));
            PartyEntityFactory factory = new();
            PartyEntity party = factory.Create(new PartyCreation(
                [
                    new MemberCreation(new PartyMemberSeed(
                        "Tester",
                        new RaceId("otherfolk"),
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
                coins: 120,
                foodPortions: 4,
                ProvisionUnit.Portions,
                reputation: 10,
                fame: 0));
            return new Fixture(catalog, conversation, party, clock);
        }

        /// <summary>One placement of the fixture's place, so a case speaks with exactly what it names.</summary>
        internal PlacementDefinition Placement(string id)
        {
            PlacePopulationContent population = PlacePopulationContent.Read(PlaceGraphLoader.Load(Catalog));
            return population.PlacementsOf(SomewherePlace).First(placement => placement.Content.Id == id);
        }

        /// <summary>What the person at a placement has to say, with each topic's verdict.</summary>
        internal IReadOnlyList<ConversationOffer> Offers(PlacementDefinition placement)
        {
            ConversationSubject subject = Conversation.Describe(new ConversationTargetRequest(SomewherePlace, placement))!;
            return Conversation.Offers(Context(placement, subject, subject.First.Id));
        }

        /// <summary>The topics on offer at a placement.</summary>
        internal IReadOnlyList<string> OnOffer(PlacementDefinition placement) =>
            [.. Offers(placement).Where(offer => offer.IsOnOffer).Select(offer => offer.Id)];

        /// <summary>One topic of a placement's list, whatever its verdict.</summary>
        internal ConversationOffer Offer(PlacementDefinition placement, string topic) =>
            Offers(placement).First(offer => string.Equals(offer.Id, topic, StringComparison.Ordinal));

        /// <summary>One thing said, resolved against the party and the clock as they stand.</summary>
        internal ConversationAnswer Take(ConversationTopic topic, PlacementDefinition placement, string speaker)
        {
            ConversationSubject subject = Conversation.Describe(new ConversationTargetRequest(SomewherePlace, placement))!;
            return Conversation.Take(topic, Context(placement, subject, speaker));
        }

        public void Dispose() => Party.Dispose();

        private ConversationContext Context(PlacementDefinition placement, ConversationSubject subject, string speaker) =>
            new(SomewherePlace, placement, subject, speaker, [], Party, Clock);

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" }
              ]
            }
            """;

        private static string Places() =>
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "Somewhere", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [
                  { "id": "person-0", "kind": "person", "x": 0, "y": 100, "z": 0, "people": [ "np-2" ] },
                  { "id": "service-7", "kind": "service", "houseId": 7, "x": 100, "y": 0, "z": 0, "people": [ "np-1" ] },
                  { "id": "residence-9", "kind": "residence", "houseId": 9, "name": "House of Ash", "proprietor": "Mira",
                    "fixture": "House R9", "x": -100, "y": 0, "z": 0 } ] } ] }
            """;

        private static string People() =>
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "np-1", "npcId": 1, "name": "Tester One", "portrait": "709",
                "greeting": "'Well met, travellers.'", "greetingAgain": "'Back again, are you?'", "house": 7,
                "dialogueEvents": 2, "topics": [] },
              { "id": "np-2", "npcId": 2, "name": "Tester Two", "portrait": "707",
                "greeting": "'A fine day for it.'", "greetingAgain": "'Still at it, then?'", "dialogueEvents": 2,
                "topics": [
                  { "id": "topic-1", "label": "The contest", "text": "'The first to bring the items wins.'", "textCount": 1 },
                  { "id": "topic-2", "label": "The errand", "text": "'I have work for you.'", "textCount": 1, "requires": 7 },
                  { "id": "topic-3", "label": "The long answer", "text": "'First of two versions.'", "textCount": 2 },
                  { "id": "topic-4", "label": "Talk shop", "text": "'A fighter, are you?'", "textCount": 1,
                    "conditions": [ { "kind": "class", "name": "fighter", "label": "a fighter" } ] },
                  { "id": "topic-5", "label": "Talk of home", "text": "'One of the other folk.'", "textCount": 1,
                    "conditions": [ { "kind": "race", "name": "otherfolk", "label": "of the other folk" } ] },
                  { "id": "topic-6", "label": "Ask for work", "text": "'You are not known here.'", "textCount": 1,
                    "conditions": [ { "kind": "reputation", "name": "reputation", "amount": 10, "label": "the party's standing" } ] },
                  { "id": "topic-7", "label": "About the daylight", "text": "'Fine weather for it.'", "textCount": 1,
                    "conditions": [ { "kind": "hour", "name": "day", "label": "daylight" } ] } ] } ] }
            """;

        private static string Services() =>
            """
            { "documentId": "services", "definitionKind": "service", "entries": [
              { "id": "7", "kind": "Weapon Shop", "name": "The Sword and Shield", "proprietor": "Bertram",
                "operations": [ "buy" ], "openHour": 6, "closedHour": 18, "priceMultiplier": 1.5, "skillPriceMultiplier": 1.5 } ] }
            """;
    }

    private static ConversationAnswer Greeting(Fixture fixture, ConversationSubject subject, string speaker) =>
        fixture.Conversation.Greeting(new ConversationContext(SomewherePlace, fixture.Placement("person-0"), subject, speaker, [], fixture.Party, fixture.Clock));

    private static string SourceDirectory() => Path.Combine(RepositoryRoot(), "src", "PartyRpg.Host");

    private static string ProjectFile() => Path.Combine(SourceDirectory(), "PartyRpg.Host.csproj");

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found above the test output.");
    }

    private static string Constant(string source, string name)
    {
        Match match = Regex.Match(source, $@"internal const string {name} = ""([^""]+)"";");
        Assert.True(match.Success, $"ProductIdentity.cs must declare {name}.");
        return match.Groups[1].Value;
    }

    private static void AssertUiConstant(string ui, string name, string value) =>
        Assert.Contains($"const {name} = '{value}';", ui, StringComparison.Ordinal);
}
