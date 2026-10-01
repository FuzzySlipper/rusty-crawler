using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What the session reports, and refuses, as it applies a player's acts inside the one admitted update — each
/// reached through <see cref="PartyRpgSession.Update"/> and asserted by the code it is reported under.
/// </summary>
/// <remarks>
/// Every rule behind the session is this suite's own — a person who offers a counter, an errand, and a rank; a
/// night under a roof; a counter selling a passage; a creature to fight — so what is proved is the session's
/// own glue between the player's request and the owner it belongs to, not any game's tuning.
/// </remarks>
public sealed class SessionDiagnosticsTests
{
    private const string Contract = "test.actions";
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test");
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly PlaceId HallPlace = new("1");
    private static readonly UseIntentNames UseControls = new("test.use", Contract);
    private static readonly ConversationIntentNames ConversationControls = new("test.conversation.leave", Contract);

    [Fact]
    public void A_counter_offered_where_no_service_mechanism_is_composed_is_reported_as_conversation_handoff_unavailable()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using Hall hall = Hall.Build(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(hall.Clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules { Conversation = new Speakers() },
            new SessionControls { Use = UseControls, Conversation = ConversationControls });
        session.Start();

        Talk(session);
        session.Update(Admitted.Update(3, 1, Topic("counter")));

        // Nothing took the party anywhere, so the conversation stays open beside the refusal.
        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Code == "conversation-handoff-unavailable");
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Null(session.Services);
        Assert.True(session.Conversations!.IsOpen);
    }

    [Fact]
    public void An_errand_offered_where_no_quest_owner_is_composed_is_reported_as_quest_unavailable()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using Hall hall = Hall.Build(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(hall.Clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules { Conversation = new Speakers() },
            new SessionControls { Use = UseControls, Conversation = ConversationControls });
        session.Start();

        Talk(session);
        session.Update(Admitted.Update(3, 1, Topic("errand")));

        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Code == "quest-unavailable");
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Null(session.Quests);
        Assert.True(session.Conversations!.IsOpen);
    }

    [Fact]
    public void A_rank_taken_from_its_giver_in_conversation_is_reported_as_promotion_granted()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using Hall hall = Hall.Build(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(hall.Clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules
            {
                Conversation = new Speakers(),
                Progression = new ProgressionRules(new NeverLevels(), new VeteranRank()),
            },
            new SessionControls { Use = UseControls, Conversation = ConversationControls });
        session.Start();

        Talk(session);
        session.Update(Admitted.Update(3, 1, Topic("rank")));

        // The giver is the person being spoken with, so the rank lands: both fighters rise together and the
        // business the party came for is over.
        DiagnosticsPublishRequest granted = Assert.Single(diagnostics.Published, report => report.Code == "promotion-granted");
        Assert.Equal(DiagnosticsDisposition.Accepted, granted.Disposition);
        Assert.Equal("promotion", granted.Source);
        Assert.Equal("veteran", party.Members[0].Profile.Class.Value);
        Assert.Equal(2, party.Members[0].Progression.ClassRank);
        Assert.False(session.Conversations!.IsOpen);
    }

    [Fact]
    public void A_casting_while_a_conversation_owns_the_controls_is_refused_as_spell_screen_open()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0);
        using Hall hall = Hall.Build(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(hall.Clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules
            {
                Conversation = new Speakers(),
                Magic = Capabilities.Magic(new AlchemyTests.Spells(AlchemyTests.Draught)),
            },
            new SessionControls { Use = UseControls, Conversation = ConversationControls, Cast = new CastIntentNames(Contract) });
        session.Start();

        Talk(session);
        int points = party.Members[0].Resources.SpellPoints.Current;
        session.Update(Admitted.Update(3, 1, Payload("""{"action":"party.cast","member":0,"spell":"potion:draught"}""")));

        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Source == "magic");
        Assert.Equal("spell-screen-open", refused.Code);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal(points, party.Members[0].Resources.SpellPoints.Current);
    }

    [Fact]
    public void A_mixture_while_a_conversation_owns_the_controls_is_refused_as_mixture_screen_open()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = AlchemyTests.Party(alchemyLevel: 3, alchemyTier: 1);
        ItemInstance berry = AlchemyTests.Take(party, AlchemyTests.Berry);
        ItemInstance bottle = AlchemyTests.Take(party, AlchemyTests.Bottle);
        AlchemyCatalog mixtures = new([
            new PotionMixture(AlchemyTests.Berry, AlchemyTests.Bottle, MixtureOutcome.Produces(AlchemyTests.Draught), new SkillTier(1), Power: 5, Note: 58),
        ]);
        using Hall hall = Hall.Build(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(hall.Clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules
            {
                Conversation = new Speakers(),
                Alchemy = new AlchemyRules(new AlchemyTests.Rule(), mixtures),
            },
            new SessionControls { Use = UseControls, Conversation = ConversationControls, Mix = new MixIntentNames(Contract) });
        session.Start();

        Talk(session);
        session.Update(Admitted.Update(3, 1, Payload($$"""{"action":"party.mix","member":0,"first":"{{berry.Id}}","second":"{{bottle.Id}}"}""")));

        // The pair the game would mix is refused whole: both ingredients are where they were.
        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Source == "alchemy");
        Assert.Equal("mixture-screen-open", refused.Code);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal([AlchemyTests.Berry, AlchemyTests.Bottle], party.Items.Select(item => item.Definition));
    }

    [Fact]
    public void A_raise_naming_a_member_the_party_does_not_have_is_refused_as_skill_member_unknown()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(diagnostics: diagnostics),
            new SessionParty.Playing(Party: party),
            new SessionRules { Progression = new ProgressionRules(new NeverLevels()) },
            new SessionControls { Skills = new SkillRaiseIntentNames(Contract) });
        session.Start();

        session.Update(Admitted.Update(1, 1, Payload("""{"action":"party.raise-skill","member":6,"skill":"blades"}""")));

        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published);
        Assert.Equal("skill-member-unknown", refused.Code);
        Assert.Equal("progression", refused.Source);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal(1, party.Members[0].Skills.LevelOf(new SkillId("blades")));
    }

    [Fact]
    public void A_stop_that_is_taken_reports_rest_applied_and_one_the_rule_refuses_reports_rest_refused()
    {
        RecordingDiagnosticsService diagnostics = new();
        GameClock clock = TestClock.At(hour: 22);
        using PartyEntity party = TestParty.OfFour();
        using Hall hall = Hall.Build(party, clock);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(clock, diagnostics),
            new SessionParty.Playing(World: hall.World, Party: party),
            new SessionRules { Rest = new RoofedNights() },
            new SessionControls
            {
                Rest = new RestIntentNames("test.rest", "test.camp", "test.wait-dawn", "test.wait-hour", "test.wait-short", Contract),
            });
        session.Start();

        // A night under the hall's roof is taken whole: eight hours pass on the one clock.
        session.Update(Admitted.Update(1, 0, Admitted.Digital("test.rest")));
        DiagnosticsPublishRequest rested = Assert.Single(diagnostics.Published, report => report.Source == "rest");
        Assert.Equal("rest-applied", rested.Code);
        Assert.Equal(DiagnosticsDisposition.Accepted, rested.Disposition);
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), clock.Now);

        // Camping under that roof is the rule's refusal, and nothing passes for it.
        session.Update(Admitted.Update(2, 0, Admitted.Digital("test.camp")));
        Assert.Equal(["rest-applied", "rest-refused"], diagnostics.Published.Where(report => report.Source == "rest").Select(report => report.Code));
        Assert.Equal(DiagnosticsSeverity.Warning, diagnostics.Published.Last(report => report.Source == "rest").Severity);
        Assert.Equal(new GameDate(1168, 1, 2, 6, 0, 0), clock.Now);
    }

    [Fact]
    public void A_passage_bought_where_the_session_holds_no_world_is_reported_as_fare_no_world()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Playing(Party: party, Accounts: new PartyResourceLedger(party)),
            new SessionRules { Service = new Stable() },
            new SessionControls { Service = new ServiceIntentNames("test.service.leave", Contract) });
        session.Start();

        Assert.True(session.Services!.Open(Stable.Counter).IsApplied);
        session.Update(Admitted.Update(1, 1, Payload("""{"action":"service.fare","target":"2"}""")));

        // The counter sold the passage and wrote it on the party; there is no world to board it into, so nothing
        // was boarded and the visit is still open.
        Assert.Equal("coach", party.Passages.RouteTo(new PlaceId("2")));
        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Source == "travel");
        Assert.Equal("fare-no-world", refused.Code);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.True(session.Services.IsOpen);
    }

    [Fact]
    public void Waiting_twice_in_one_round_of_a_paced_fight_is_refused_as_already_waited()
    {
        RecordingDiagnosticsService diagnostics = new();
        using PartyEntity party = TestParty.OfFour();
        using SessionWorld world = Arena(party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Playing(World: world, Party: party),
            new SessionRules { Combat = Capabilities.Combat(new CombatStateTests.TestCombatRule(null)) },
            new SessionControls
            {
                Combat = new CombatIntentNames(
                    "test.attack",
                    Contract,
                    new TurnIntentNames("test.turn-based", "test.turn-skip", "test.turn-wait", Contract)),
            });
        session.Start();
        session.Update(Admitted.Update(1, 1));
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.turn-based")));
        Assert.True(session.Combat!.Turns.WaitsForPlayer);

        // Every member defers its turn once; the round then comes back to the first of them, who has already
        // deferred it this round and may not do so again.
        CombatantId first = session.Combat.Turns.Current!.Id;
        ulong step = 3;
        for (int member = 0; member < party.Members.Count; member++)
        {
            session.Update(Admitted.Update(step++, 1, Admitted.Digital("test.turn-wait")));
        }

        Assert.DoesNotContain(diagnostics.Published, report => report.Code == "already-waited");
        Assert.Equal(first, session.Combat.Turns.Current!.Id);
        session.Update(Admitted.Update(step, 1, Admitted.Digital("test.turn-wait")));

        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Code == "already-waited");
        Assert.Equal("combat", refused.Source);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal(first, session.Combat.Turns.Current!.Id);
    }

    [Fact]
    public void A_finished_party_whose_world_refuses_to_be_composed_is_refused_as_creation_refused()
    {
        RecordingDiagnosticsService diagnostics = new();
        List<PartyEntity> built = [];
        using RecordingUiProjectionChannel channel = new();
        PlaceGraph graph = Graph("""{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 7 }""");
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Creating(new SessionCreation(
                new PartyCreationFlow(CreationOptions, CreationDefaults),
                creation =>
                {
                    PartyEntity party = new PartyEntityFactory().Create(creation);
                    built.Add(party);
                    return party;
                },
                party => new SessionWorld(
                    graph,
                    // The place the world would start the party in admits no pose at all, so the world cannot be
                    // composed over the party that was just built.
                    new PartyPoseOwner(new PartyPose(HallPlace, PlacePose.Origin), Facing, Nowhere),
                    new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                    new FreeTravel(),
                    resources: new PartyResourceLedger(party)))),
            controls: new SessionControls { Creation = new CreationIntentNames("test.creation-advance", "test.creation-accept", Contract) });
        session.Start();
        Assert.True(session.Creation!.IsComplete);

        session.Update(Admitted.Update(1, 1, Admitted.Digital("test.creation-accept")));

        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Source == "creation");
        Assert.Equal(CreationCodes.CreationRefused, refused.Code);
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal(CreationCodes.CreationRefused, session.CreationRefusal!.Code);
        Assert.Equal(SessionMode.Creating, session.Mode);
        Assert.Null(session.Party);
        Assert.Single(built);
    }

    /// <summary>Faces the person standing in the hall and speaks with them, which opens the conversation.</summary>
    private static void Talk(PartyRpgSession session)
    {
        session.Update(Admitted.Update(1, 1));
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.use")));
        Assert.True(session.Conversations!.IsOpen);
    }

    /// <summary>A topic chosen on the conversation's screen.</summary>
    private static ProductInputEvent Topic(string topic) =>
        Payload($$"""{"action":"conversation.topic","target":"{{topic}}"}""");

    /// <summary>One payload action on this suite's contract, as the companion sends it.</summary>
    private static ProductInputEvent Payload(string json) => Admitted.Payload(Contract, json);

    /// <summary>A place rule that admits no pose anywhere.</summary>
    private static bool Nowhere(PlaceId place, PlacePose pose, out PlacePose admitted)
    {
        admitted = pose;
        return false;
    }

    /// <summary>One place holding the placements a case states.</summary>
    private static PlaceGraph Graph(string place) => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", TestPacks.PlacesOnly)
                .Add("packs/world/places.json", TestPacks.Document("places", "place", place)),
            Layout).RequireValid());

    /// <summary>A region with a creature near enough to fight, so a fight can be paced.</summary>
    private static SessionWorld Arena(PartyEntity party)
    {
        PlaceGraph graph = Graph(
            """
            { "id": "1", "kind": "region", "name": "Arena", "respawnDays": 7,
              "placements": [ { "id": "beast", "kind": "creature", "x": 120, "y": 0, "z": 0, "monster": "7", "hitPoints": 40 } ] }
            """);
        GameClock clock = TestClock.Create();
        return new SessionWorld(
            graph,
            new PartyPoseOwner(new PartyPose(HallPlace, PlacePose.Origin), Facing),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            time: clock,
            clock: clock,
            partyEntity: party,
            vitals: Capabilities.PlacementHitPoints);
    }

    /// <summary>One member's creation, finished by its defaults: a race, a class, a portrait, and a skill.</summary>
    private static PartyCreationOptions CreationOptions { get; } = new(
        memberCount: 1,
        races:
        [
            new CreationRace(new RaceId("testfolk"), "Testfolk",
            [
                new AttributeCreationRange(new AttributeId("vigour"), "Vigour", start: 8, minimum: 6, maximum: 12, stepSize: 1, stepCost: 1),
            ]),
        ],
        classes:
        [
            new CreationClass(new ClassId("fighter"), "Fighter", [new SkillId("blades")], [new SkillId("axes"), new SkillId("bows")], startingHitPoints: 40, startingSpellPoints: 0, startingRank: 1),
        ],
        portraits: [new CreationPortrait(new PortraitId("folk-a"), new RaceId("testfolk"), "Folk A")],
        attributePool: 2,
        chosenSkillCount: 1,
        nameMaximumLength: 8,
        startingSkillTier: new SkillTier(1),
        startingLevel: 1,
        startingCoins: 10,
        startingFoodPortions: 2);

    private static PartyCreationDefaults CreationDefaults { get; } = new(
    [
        new CreationMemberDefaults(
            new PortraitId("folk-a"),
            new ClassId("fighter"),
            "Ann",
            [new AttributeScore(new AttributeId("vigour"), 10)],
            [new SkillId("axes")]),
    ]);

    /// <summary>
    /// A hall with one person standing where the party faces, the clock the session keeps, and the one use that
    /// speaks with them.
    /// </summary>
    private sealed class Hall : IDisposable
    {
        private Hall(SessionWorld world, GameClock clock)
        {
            World = world;
            Clock = clock;
        }

        internal SessionWorld World { get; }

        internal GameClock Clock { get; }

        internal static Hall Build(PartyEntity party, GameClock? clock = null)
        {
            PlaceGraph graph = Graph(
                """
                { "id": "1", "kind": "interior", "name": "Hall", "respawnDays": 3,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "person-0", "kind": "person", "x": 0, "y": 100, "z": 0 } ] }
                """);
            GameClock time = clock ?? TestClock.Create();
            SessionWorld world = new(
                graph,
                new PartyPoseOwner(new PartyPose(HallPlace, PlacePose.Origin), Facing),
                new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new FreeTravel(),
                time: time,
                clock: time,
                resources: new PartyResourceLedger(party),
                partyEntity: party,
                interaction: new InteractionPolicy(
                    new PeopleInteraction(new Speakers()),
                    PlaceSpace.HeightIsThird(Facing, radiansAtZeroFacing: 0),
                    new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31)));
            return new Hall(world, time);
        }

        public void Dispose() => World.Dispose();
    }

    /// <summary>
    /// This suite's person: Mira, who keeps a counter, offers an errand, and gives the fighters' next rank.
    /// </summary>
    private sealed class Speakers : IConversationRule
    {
        public ConversationSubject? Describe(ConversationTargetRequest request) =>
            string.Equals(request.Placement.Content.Kind, "person", StringComparison.Ordinal)
                ? new ConversationSubject("person-0", [new ConversationPerson("mira", "Mira")])
                : null;

        public ConversationAnswer Greeting(ConversationContext context) => new("'Well met.'");

        public IReadOnlyList<ConversationOffer> Offers(ConversationContext context) =>
        [
            new(new ConversationTopic("counter", "Step up to the counter"), Verdict.Met),
            new(new ConversationTopic("errand", "Ask for work"), Verdict.Met),
            new(new ConversationTopic("rank", "Ask to be made a veteran"), Verdict.Met),
        ];

        public ConversationAnswer Take(ConversationTopic topic, ConversationContext context) => topic.Id switch
        {
            "counter" => new ConversationAnswer("'What will it be?'", handoff: new ConversationHandoff(HandoffOwner.Counter)),
            "errand" => new ConversationAnswer("'There is a cellar that wants clearing.'", handoff: new ConversationHandoff(HandoffOwner.ErrandOffer, "cellar")),
            "rank" => new ConversationAnswer("'Veterans, the both of you.'", handoff: new ConversationHandoff(HandoffOwner.Rank, "fighter-veteran")),
            _ => new ConversationAnswer("'Nothing more to say.'"),
        };
    }

    /// <summary>The interaction answers of the hall: whoever the conversation rule says is here is spoken with.</summary>
    private sealed class PeopleInteraction(Speakers rule) : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            rule.Describe(new ConversationTargetRequest(request.Place, request.Placement)) is { } subject
                ? new InteractionTargetDefinition(new InteractionTargetKind("person"), subject.First.Name, InteractionVerb.Talk, 512)
                : null;

        public Verdict Judge(InteractionRequirement requirement, InteractionContext context) => Verdict.Met;

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            InteractionOutcome.Applied("spoken", $"The party speaks with {target.Name}.");
    }

    /// <summary>A growth policy with a curve no party climbs, so only a rank moves anything.</summary>
    private sealed class NeverLevels : IProgressionRule
    {
        public long ExperienceForLevel(int level) => long.MaxValue;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }

    /// <summary>One rank: a fighter becomes a veteran, given by Mira.</summary>
    private sealed class VeteranRank : IPromotionRule
    {
        public PromotionLadder Ladder { get; } = new(
        [
            new PromotionRank(
                "fighter-veteran",
                new ClassId("fighter"),
                new ClassId("veteran"),
                2,
                [PromotionRequirement.FromGiver("mira", "Mira")]),
        ]);
    }

    /// <summary>A night under a roof takes eight hours and costs nothing; camping under one is refused.</summary>
    private sealed class RoofedNights : IRestRule
    {
        public RestQuote Quote(RestRequest request) => request.Kind == RestKind.Camp
            ? RestQuote.Refused(new Refusal("camp-under-a-roof", "The party stands under a roof and will not camp here."))
            : RestQuote.Planned(GameDuration.FromHours(8), Provisions.None);

        public RestInterruption? Interrupt(RestRequest request) => null;

        public IReadOnlyList<ConditionId> RecoveredBy(RestRequest request) => [];

        public ActiveCondition Fatigue => new(new ConditionId("weary"), 1);

        public GameDuration SleepInterval => GameDuration.FromHours(24);
    }

    /// <summary>A stable whose one counter sells a free two-day passage to place 2.</summary>
    private sealed class Stable : IServiceRule
    {
        internal static ServiceDefinition Counter { get; } = new(
            new ServiceId("stable"),
            new ServiceKind("Stable"),
            "The stable",
            [ServiceOperationKind.Fare],
            stock: [],
            lessons: []);

        public ServiceDefinition? Describe(ServiceTargetRequest request) => null;

        public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request) => [];

        public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request) => [];

        public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request) =>
            [new ServiceOffer(ServiceOfferKind.Fare, "A passage to the cave", "2", Value: 0, Amount: 2, Route: "coach")];

        public IReadOnlyList<string> Access(ServiceAccessRequest request) => [];

        public ServiceEligibility Judge(ServiceEligibilityRequest request) => ServiceEligibility.Allowed;

        public ServiceQuote Quote(ServiceQuoteRequest request) => ServiceQuote.Free;
    }
}
