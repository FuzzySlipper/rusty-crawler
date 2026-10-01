using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The four map-event steps that reach past the fixture into the world: a change of which topic a person's slot
/// raises, a count of a place's dead, a group of its creatures turned hostile, and a line of the history book.
/// </summary>
/// <remarks>
/// The staged events are shaped as the operator's install writes them — the School of Sorcery's bookcase changes the
/// information desk's second slot (event 196 of <c>d14.evt</c>, step 29) and first asks whether the desk's own actor
/// is dead (step 0); the doors of a guild turn group 5 hostile (event 3 of <c>d18.evt</c>, step 5); a history line is
/// written by slot (event 376 of <c>d27.evt</c>, step 4) — with this suite's own people, topics and lines.
/// </remarks>
public sealed partial class FixturePolicyTests
{
    [Fact]
    public void A_bookcase_changes_which_topic_a_persons_slot_raises_and_the_conversation_reads_the_change()
    {
        ContentCatalog catalog = WorldStepsCatalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, services: null)!;
        MightAndMagic7Fixtures fixtures = new(MightAndMagic7MapEvents.Read(catalog), people: conversation.PersonOf);
        MightAndMagic7Interaction rule = new(fixtures: fixtures);

        // Before anything changed, the desk offers what its own rows say: the old business and the desk.
        Assert.Equal(["topic-39", "topic-40"], Topics(conversation, party, clock));

        // The bookcase's step changes the desk's second slot from row 39 to row 41; the change is a party record,
        // judged before anything is settled and applied when the run completes.
        (InteractionOutcome used, _) = Use(rule, Fixture(330, "Bookcase", string.Empty), EmeraldIsle, party, clock);
        Assert.True(used.IsApplied, used.Refusal?.Message);
        Assert.Equal(41, MightAndMagic7TopicSlots.Raised(party.Records, "npc-7", 1));

        // The conversation reads the change: the row the slot raised is withdrawn, and the row it raises now is
        // offered with the table's own words.
        Assert.Equal(["topic-40", "topic-41"], Topics(conversation, party, clock));
        ConversationAnswer answer = conversation.Take(
            new ConversationTopic("topic-41", "New business"),
            Speaking(conversation, party, clock));
        Assert.Equal("The guild takes new members at the turn of the year.", answer.Text);

        // A second change of the same slot replaces the first rather than standing beside it, and a slot changed
        // to row zero raises nothing at all.
        MightAndMagic7TopicSlots.Change(party.Records, "npc-7", 1, 0);
        Assert.Single(party.Records.All, record => record.Name.StartsWith(MightAndMagic7TopicSlots.Prefix, StringComparison.Ordinal));
        Assert.Equal(["topic-40"], Topics(conversation, party, clock));

        // A step naming somebody the people table does not hold is refused by name and changes nothing.
        (InteractionOutcome stranger, _) = Use(rule, Fixture(340, "Bookcase", string.Empty), EmeraldIsle, party, clock);
        Assert.Equal(MightAndMagic7Codes.FixturePersonUnknown, stranger.Refusal!.Code);
        Assert.Equal(0, MightAndMagic7TopicSlots.Raised(party.Records, "npc-7", 1));

        // What a save says about a changed slot is judged against the content it is resumed over.
        Assert.Null(fixtures.JudgeRecord(MightAndMagic7TopicSlots.Record("npc-7", 1, 41), 1));
        Assert.NotNull(fixtures.JudgeRecord(MightAndMagic7TopicSlots.Record("npc-99", 1, 41), 1));
        Assert.NotNull(fixtures.JudgeRecord(MightAndMagic7TopicSlots.Record("npc-7", 6, 41), 1));
        Assert.NotNull(fixtures.JudgeRecord("topic-slot:npc-7", 1));
        Assert.Null(fixtures.JudgeRecord("errand:12", 1));
    }

    [Fact]
    public void A_save_carrying_a_changed_topic_slot_of_somebody_the_content_lacks_is_refused_by_name()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), SessionContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        ((MightAndMagic7Session)session).Party!.Records.Mark(MightAndMagic7TopicSlots.Record("npc-99", 0, 12));
        SessionSave save = MightAndMagic7Ruleset.Instance.Save(session);

        // The session's content carries nobody of that id, so the record is a slot of nobody and the load refuses it.
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        SessionSaveException refused = Assert.Throws<SessionSaveException>(
            () => MightAndMagic7Persistence.RequireLoadable(save, catalog, fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog))));
        SaveProblem problem = Assert.Single(refused.Problems, problem => problem.Code == SaveCodes.SaveRecordUnknown);
        Assert.Contains("npc-99", problem.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_count_of_the_dead_reads_the_places_population_by_group_kind_creature_or_all()
    {
        ContentCatalog catalog = WorldStepsCatalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();

        // The place holds two creatures of group 5 (row 7), one of no group (row 8), and the desk's own actor 3.
        List<PlaceActor> actors =
        [
            new(Creature("guard-a", monster: 7, group: 5), Down: false),
            new(Creature("guard-b", monster: 7, group: 5), Down: false),
            new(Creature("rat", monster: 8, group: 0), Down: false),
            new(Person("person-3", actor: 3, group: 0), Down: false),
        ];
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog), actors: _ => actors));
        string Ask(int eventId) => Use(rule, Fixture(eventId, "Bell", string.Empty), EmeraldIsle, party, clock).Outcome.Message;

        // Without a number, every matching actor must be down; with one, at least that many (OpenEnroth
        // src/Engine/Objects/Actor.cpp:2811-2834).
        Assert.Equal("Standing.", Ask(332));
        Assert.Equal("Standing.", Ask(333));
        Assert.Equal("Standing.", Ask(334));
        Assert.Equal("Standing.", Ask(335));

        actors[0] = actors[0] with { Down = true };
        Assert.Equal("Standing.", Ask(332));
        Assert.Equal("Standing.", Ask(333));
        Assert.Equal("All down.", Ask(335));

        actors[1] = actors[1] with { Down = true };
        Assert.Equal("All down.", Ask(332));
        Assert.Equal("All down.", Ask(333));

        // One creature is the map's own actor record by its index, which only its people carry here.
        Assert.Equal("Standing.", Ask(334));
        actors[3] = actors[3] with { Down = true };
        Assert.Equal("All down.", Ask(334));

        // A session that keeps no population to count refuses the step by name rather than inventing an answer.
        MightAndMagic7Interaction blind = new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog)));
        InteractionOutcome unanswered = Use(blind, Fixture(332, "Bell", string.Empty), EmeraldIsle, party, clock).Outcome;
        Assert.Equal(MightAndMagic7Codes.FixtureStepNotInterpreted, unanswered.Refusal!.Code);
        Assert.Contains("population", unanswered.Refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_gong_turns_a_group_hostile_as_a_value_the_place_keeps_and_a_flag_it_does_not_read_is_refused()
    {
        ContentCatalog catalog = WorldStepsCatalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Fixtures fixtures = new(MightAndMagic7MapEvents.Read(catalog));
        MightAndMagic7Interaction rule = new(fixtures: fixtures);
        InteractionLedger ledger = new();

        (InteractionOutcome struck, _) = Use(rule, Fixture(337, "Gong", string.Empty), EmeraldIsle, party, clock, ledger);
        Assert.Equal("Gong: The creatures here turn on the party.", struck.Message);
        Assert.True(MightAndMagic7Fixtures.IsGroupHostile(ledger.ValuesOf(EmeraldIsle), 5));
        Assert.False(MightAndMagic7Fixtures.IsGroupHostile(ledger.ValuesOf(EmeraldIsle), 4));
        Assert.False(MightAndMagic7Fixtures.IsGroupHostile(ledger.ValuesOf(Harmondale), 5));

        // The calming step clears it again, as the donor's clearing of the bit does.
        Use(rule, Fixture(338, "Gong", string.Empty), EmeraldIsle, party, clock, ledger);
        Assert.False(MightAndMagic7Fixtures.IsGroupHostile(ledger.ValuesOf(EmeraldIsle), 5));

        // A creature flag other than the aggressor bit is not one this game reads.
        InteractionOutcome other = Use(rule, Fixture(339, "Gong", string.Empty), EmeraldIsle, party, clock, ledger).Outcome;
        Assert.Equal(MightAndMagic7Codes.FixtureStepNotInterpreted, other.Refusal!.Code);
        Assert.Contains("0x40", other.Refusal.Message, StringComparison.Ordinal);

        // What a save says the place keeps is judged: a group by its number, hostile or not.
        Assert.Null(fixtures.Judge(EmeraldIsle, "hostile-group:5", 1, 0));
        Assert.Null(fixtures.Judge(EmeraldIsle, "hostile-group:5", 0, 0));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "hostile-group:5", 2, 0));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "hostile-group:0", 1, 0));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "hostile-group:x", 1, 0));
    }

    [Fact]
    public void A_history_book_writes_its_line_into_the_journal_once_dated_and_with_the_partys_names()
    {
        ContentCatalog catalog = WorldStepsCatalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        PartyJournal journal = new(new MightAndMagic7Journal(loot: null), clock);
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog), journal: () => journal));

        (InteractionOutcome written, _) = Use(rule, Fixture(336, "History Book", string.Empty), EmeraldIsle, party, clock);
        Assert.True(written.IsApplied, written.Refusal?.Message);
        Assert.Equal("History Book: A page is written in the party's history: \"The Bell\".", written.Message);
        JournalEntry line = Assert.Single(journal.Entries);
        Assert.Equal(JournalEntryKind.Chronicle, line.Kind);
        Assert.Equal("history:1", line.Subject);
        Assert.Equal($"On {clock.Now.DayText}, Zoltan and  rang the bell.", line.Text);

        // The slot is written once: the second use writes nothing and says nothing of it.
        clock.Advance(GameDuration.FromHours(3));
        (InteractionOutcome again, _) = Use(rule, Fixture(336, "History Book", string.Empty), EmeraldIsle, party, clock);
        Assert.True(again.IsApplied);
        Assert.Single(journal.Entries);
        Assert.Equal("History Book: nothing comes of it.", again.Message);

        // The line is carried in the journal's own save under its own kind.
        Assert.Equal("chronicle", Assert.Single(journal.Capture().Entries).Kind);

        // A slot the table holds no line for is refused by name; a session with no journal refuses the step too.
        Assert.Equal(MightAndMagic7Codes.FixtureHistoryUnknown, Use(rule, Fixture(341, "History Book", string.Empty), EmeraldIsle, party, clock).Outcome.Refusal!.Code);
        MightAndMagic7Interaction unwritten = new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog)));
        Assert.Equal(MightAndMagic7Codes.FixtureVariableNotInterpreted, Use(unwritten, Fixture(336, "History Book", string.Empty), EmeraldIsle, party, clock).Outcome.Refusal!.Code);
    }

    [Fact]
    public void A_session_s_gong_turns_the_group_on_the_party_in_its_fight_its_save_and_its_count_of_the_dead()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = SpellEffectPolicyTests.Content(
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", GongPlaces),
            extra:
            [
                ($"{RulesetTestContext.ContentDirectory}/content-packs/world/events.json", GongEvents, """{ "path": "events.json", "documentId": "events", "definitionKind": "place-event" }"""),
                ($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json", GongPeople, """{ "path": "people.json", "documentId": "people", "definitionKind": "person" }"""),
            ]);
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Session live = (MightAndMagic7Session)session;

        // Both creatures stand beyond what their row notices, and a person starts no fight, so nothing is fighting
        // the party yet.
        Assert.Equal(CombatSide.Neutral, Side(live, "guard"));
        Assert.Equal(CombatSide.Neutral, Side(live, "loner"));
        Assert.Equal(CombatSide.Neutral, Side(live, "doorman"));

        // The gong first counts its group's dead — the guard stands, so the count does not hold — and then turns
        // group 5 on the party: the place keeps it, and the fight reads it as the guard's own nature at the
        // longest band (OpenEnroth src/Engine/Objects/Actor.cpp:2155-2156), while the loner of no group is left be.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("Gong: The creatures here turn on the party.", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString());
        session.Update(RulesetTestContext.Update(3, 1));
        Assert.Equal(CombatSide.Opposition, Side(live, "guard"));
        Assert.Equal(CombatSide.Neutral, Side(live, "loner"));

        // A person of the group — a doorman the map's own actor record stands — turns on the party the same way.
        Assert.Equal(CombatSide.Opposition, Side(live, "doorman"));

        // It is the place's state rather than a provocation the fight remembers, so a save carries it and the
        // resumed session's fight reads the guard as hostile again.
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Contains(new PlaceValue("hostile-group:5", 1), Assert.Single(written.World.Interaction.Places).Values);
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with
            {
                Start = SessionStart.Resume,
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
            });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Session again = (MightAndMagic7Session)resumed;
        Assert.Equal(CombatSide.Opposition, Side(again, "guard"));

        // Once the guard and the doorman are down, the count of group 5's dead holds and the gong says so.
        foreach (string fallen in new[] { "guard", "doorman" })
        {
            Combatant down = again.Combat!.Combatants.Single(combatant => combatant.Subject.Placement?.Content.Id == fallen);
            Assert.True(CreatureHealth.Find(down.Subject.Entity!.Actor)!.Wound(100_000));
            resumed.Update(RulesetTestContext.Update(2, 1));
            if (fallen == "guard")
            {
                resumed.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
                Assert.Equal("Gong: nothing comes of it.", ProjectedNode.Of(resumedUi.Latest().Value).Field("interaction").Field("message").AsString());
            }
        }

        resumed.Update(RulesetTestContext.Update(4, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("Nobody answers the gong.", ProjectedNode.Of(resumedUi.Latest().Value).Field("interaction").Field("message").AsString());
    }

    [Fact]
    public void A_session_counts_the_dead_of_a_levels_own_creature_by_the_index_its_level_gives_it()
    {
        (string Path, string Text)[] content = SpellEffectPolicyTests.Content(
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", AltarPlaces),
            extra:
            [
                ($"{RulesetTestContext.ContentDirectory}/content-packs/world/events.json", AltarEvents, """{ "path": "events.json", "documentId": "events", "definitionKind": "place-event" }"""),
            ]);
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Session live = (MightAndMagic7Session)session;

        // The place's own actor records are populated through the same seam as its encounters: the standing one is a
        // creature the fight holds, under the index the level gives it, and the hidden one stands as nothing.
        IReadOnlyList<PlaceActor> actors = MightAndMagic7Fixtures.ActorsOf(live.World!, EmeraldIsle)!;
        PlaceActor priest = Assert.Single(actors, actor => actor.Placement.SourceField == MightAndMagic7Fixtures.ActorSourceField);
        Assert.Equal(34, priest.Placement.SourceIndex);
        Assert.Equal("monster-actor-34", priest.Placement.Content.Id);
        Assert.False(priest.Down);
        Combatant standing = live.Combat!.Combatants.Single(combatant => combatant.Subject.Placement?.Content.Id == "monster-actor-34");

        // The altar asks whether actor 34 is dead, as the Temple of Baa's own leaving event asks (event 2 of
        // d04.evt, step 1): it is not, until the creature falls.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("The priest still stands.", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString());

        Assert.True(CreatureHealth.Find(standing.Subject.Entity!.Actor)!.Wound(100_000));
        session.Update(RulesetTestContext.Update(3, 1));
        Assert.True(Assert.Single(MightAndMagic7Fixtures.ActorsOf(live.World!, EmeraldIsle)!, actor => actor.Placement.SourceIndex == 34).Down);
        session.Update(RulesetTestContext.Update(4, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("The priest is dead.", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString());
    }

    [ImportedFact("place-events.json")]
    public void The_operators_Temple_of_Baa_holds_its_own_creatures_and_its_actor_34_check_reads_the_priests_death()
    {
        ContentCatalog catalog = ImportedContent.Load();
        PlaceId temple = new("45");
        PlacePopulationContent placed = PlacePopulationContent.Read(MightAndMagic7World.Graph(catalog), MightAndMagic7Spawns.Compose(catalog, new KeyedTestRandom()));

        // The dungeon holds the 35 creatures its own actor records stand, beside what its spawn records resolve to,
        // each under the index the level gives it; actor 34 is a priest of the moon (row 18) with a name of its own.
        List<PlacementDefinition> own = [.. placed.PlacementsOf(temple).Where(placement => placement.SourceField == MightAndMagic7Fixtures.ActorSourceField)];
        Assert.Equal(35, own.Count);
        Assert.All(own, placement => Assert.Equal(MightAndMagic7Combat.CreaturePlacementKind, placement.Content.Kind));
        Assert.Equal(Enumerable.Range(0, 35), own.Select(placement => placement.SourceIndex ?? -1).Order());
        PlacementDefinition priest = Assert.Single(own, placement => placement.SourceIndex == 34);
        Assert.Equal("18", priest.Source.GetId(MightAndMagic7Combat.MonsterField));
        Assert.Equal(3, priest.Source.GetInt32("uniqueNameIndex"));

        // The temple's own leaving event asks whether actor 34 is dead (event 2 of d04.evt, step 1). That event is
        // raised on leaving the map, which no fixture runs, so its step is run as the one step of an altar here.
        MapEventStep check = MightAndMagic7MapEvents.Read(catalog).Find(temple, 2)!.Steps.Single(step => step.Op == "is-actor-killed");
        Assert.Equal(("creature", 34, 1), (check.Which, check.Value, check.Amount));
        string events = string.Create(
            CultureInfo.InvariantCulture,
            $$"""
            { "documentId": "events", "definitionKind": "place-event", "entries": [
              { "id": "45.351", "place": "45", "event": 351, "label": "Altar", "raised": true,
                "steps": [
                  { "step": 0, "op": "is-actor-killed", "which": "{{check.Which}}", "value": {{check.Value}}, "target": 3, "amount": {{check.Amount}} },
                  { "step": 1, "op": "status-text", "text": "Standing." }, { "step": 2, "op": "exit" },
                  { "step": 3, "op": "status-text", "text": "Dead." } ] } ] }
            """);
        ContentCatalog altar = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    { "schemaVersion": 1, "packId": "world", "kind": "definitions", "provenance": { "description": "test content" },
                      "documents": [ { "path": "events.json", "documentId": "events", "definitionKind": "place-event" } ] }
                    """)
                .Add("packs/world/events.json", events),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

        // Read as the session's count of the dead reads the place: the imported population, the priest down or not.
        bool priestDown = false;
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(
            MightAndMagic7MapEvents.Read(altar),
            actors: place => [.. placed.PlacementsOf(place)
                .Where(MightAndMagic7Fixtures.IsActor)
                .Select(placement => new PlaceActor(placement, Down: priestDown && placement == priest))]));
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        Assert.Equal("Standing.", Use(rule, Fixture(351, "Altar", string.Empty), temple, party, clock).Outcome.Message);
        priestDown = true;
        Assert.Equal("Dead.", Use(rule, Fixture(351, "Altar", string.Empty), temple, party, clock).Outcome.Message);
    }

    /// <summary>The side the session's fight puts a creature on, by its placement.</summary>
    private static CombatSide Side(MightAndMagic7Session live, string placement) =>
        live.Combat!.Combatants.Single(combatant => combatant.Subject.Placement?.Content.Id == placement).Side;

    /// <summary>The topics the desk offers the party, in the order it offers them, without this game's own lines.</summary>
    private static List<string> Topics(MightAndMagic7Conversation conversation, PartyEntity party, GameClock clock) =>
        [.. conversation.Offers(Speaking(conversation, party, clock))
            .Select(offer => offer.Topic.Id)
            .Where(id => id.StartsWith(MightAndMagic7Conversation.TopicIdPrefix, StringComparison.Ordinal))];

    private static ConversationContext Speaking(MightAndMagic7Conversation conversation, PartyEntity party, GameClock clock)
    {
        PlacementDefinition desk = Person("person-3", actor: 3, group: 0);
        ConversationSubject subject = new("person-3", [conversation.PersonOf("npc-7")!]);
        return new ConversationContext(EmeraldIsle, desk, subject, "npc-7", [], party, clock);
    }

    [Fact]
    public void A_trap_plate_walked_onto_harms_the_party_and_springs_its_ambush_on_each_step_onto_it()
    {
        // A plate whose event harms the party and summons creatures is a floor trigger with a reach raising it, as the
        // importer writes every plate; walking into the reach runs its event through the one use workflow.
        ContentCatalog catalog = TrapPlateCatalog();
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        GameClock clock = TestClock.Create(scale: 1);
        PartyPoseOwner pose = new(new PartyPose(EmeraldIsle, new PlacePose(0, 0, 0, 0, 0)), MightAndMagic7Movement.Facing);
        using PartyEntity party = Party(hitPoints: new ResourcePool(40, 40));
        SessionWorld? world = null;
        MightAndMagic7Fixtures fixtures = new(
            MightAndMagic7MapEvents.Read(catalog),
            random: new KeyedTestRandom(),
            population: place => world is { } live && live.Population.Place == place ? live.Population : null);

        // Onto the plate, standing on it, off it, onto it again; then, invisible, off and onto it once more.
        (double, double, double)[] walk = [(100, 0, 0), (100, 0, 0), (0, 0, 0), (-150, 0, 0), (150, 0, 0), (-150, 0, 0), (150, 0, 0)];
        using SessionWorld built = new(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            mover: RecordingMover.Scripted(pose, walk),
            entrances: PlaceEntranceLoader.Load(catalog, graph),
            clock: clock,
            partyEntity: party,
            interaction: new InteractionPolicy(new MightAndMagic7Interaction(fixtures: fixtures), MightAndMagic7Movement.Space, MightAndMagic7Interaction.Aim));
        world = built;

        // The place is populated as a session's update populates the party's place: it holds no creature of its own.
        built.Populate();
        PlacePopulationEntity[] Creatures() =>
            [.. built.Population.Entities.Where(entity => entity.Content.Kind == MightAndMagic7Combat.CreaturePlacementKind)];
        Assert.Empty(Creatures());

        // The first step ends short of the plate's reach, and nothing happens.
        built.Step(MovementIntent.Still, 1);
        Assert.Equal(40, party.Members[0].Resources.HitPoints.Current);

        // The step onto it springs the trap: the fire lands on the party and two goblins stand at the event's point.
        built.Step(MovementIntent.Still, 1);
        InteractionResult sprung = built.Interaction!.LastResult!;
        Assert.True(sprung.IsApplied, sprung.Refusal?.Message);
        Assert.Equal(35, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(2, Creatures().Length);
        Assert.All(Creatures(), goblin =>
        {
            Assert.Equal("73", goblin.Placement.Source.GetId(MightAndMagic7Combat.MonsterField));
            Assert.Equal(3, goblin.Placement.Source.GetInt32(MightAndMagic7MonsterAi.GroupField));
        });
        Assert.Contains("2 Goblin appear.", sprung.Message, StringComparison.Ordinal);

        // Standing on it does nothing more: the donor raises a plate's event on the step onto a new floor face, not
        // on every step on it (OpenEnroth src/Engine/Graphics/Indoor.cpp:1488-1494, Outdoor.cpp:966-980).
        built.Step(MovementIntent.Still, 1);
        Assert.Equal(35, party.Members[0].Resources.HitPoints.Current);

        // Off it and back onto it is another step onto it, and the trap springs again.
        built.Step(MovementIntent.Still, 1);
        built.Step(MovementIntent.Still, 1);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(4, Creatures().Length);

        // An invisible party walks over it unnoticed: the event's first step compares the party's invisibility.
        new RunningSpellEffects(party, clock).Start(SpellEffectIds.Invisibility, 1, GameDuration.FromHours(1));
        built.Step(MovementIntent.Still, 1);
        built.Step(MovementIntent.Still, 1);
        Assert.True(built.Interaction.LastResult!.IsApplied);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(4, Creatures().Length);
    }

    /// <summary>
    /// A place with one trap plate, staged as the importer writes one: the floor trigger, the reach raising it, and
    /// its event — shaped as the ambush plates of the operator's install are (event 236 of <c>out02.evt</c>), with a
    /// harm in place of its spells.
    /// </summary>
    private static ContentCatalog TrapPlateCatalog() =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", TestPacks.Manifest("world", TestPacks.Places, ("events", "place-event"), ("entrances", "place-entrance")))
                .Add("packs/world/places.json", TestPacks.Document("places", "place",
                    """
                    { "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                      "placements": [
                        { "id": "trigger-7", "kind": "floor-trigger", "sourceField": "events", "sourceIndex": 7, "x": 200, "y": 0, "z": 0,
                          "positionSource": "event-face-centroid", "eventId": 7, "faceCount": 1 } ] }
                    """))
                .Add("packs/world/entrances.json", TestPacks.Document("entrances", "place-entrance",
                    """{ "id": "1.7.0", "fromPlace": "1", "raises": "trigger-7", "raisesKind": "floor-trigger", "eventId": 7, "x": 200, "y": 0, "z": 0, "radius": 60 }"""))
                .Add("packs/world/events.json", TestPacks.Document("events", "place-event",
                    """
                    { "id": "1.7", "place": "1", "event": 7, "raised": false, "stepped": true, "timed": false, "mapFile": "test.odm",
                      "steps": [
                        { "step": 0, "op": "compare", "variable": "invisible", "value": 0, "target": 3 },
                        { "step": 1, "op": "receive-damage", "who": "party", "kind": "fire", "amount": 5 },
                        { "step": 2, "op": "summon-monsters", "amount": 2, "group": 3, "x": 400, "y": 0, "z": 0, "encounter": 4, "uniqueName": 0,
                          "summons": { "encounter": 4, "slot": 1, "grade": "A", "monsterKind": "Goblin", "difficulty": 3, "appearMin": 2, "appearMax": 5,
                            "variants": [ { "grade": "A", "monster": 73, "monsterName": "Goblin" }, { "grade": "B", "monster": 74, "monsterName": "Hobgoblin" } ] } },
                        { "step": 3, "op": "exit" } ] }
                    """)),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

    /// <summary>A creature placement as the ruleset resolves an encounter into one.</summary>
    private static PlacementDefinition Creature(string id, int monster, int group)
    {
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "{{id}}", "kind": "monster", "sourceField": "spawnPoints", "x": 0, "y": 0, "z": 0, "monster": "{{monster}}", "group": {{group}} }""");
        return new PlacementDefinition(new PlacementContentId("monster", id), "spawnPoints", null, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));
    }

    /// <summary>A person placement as the importer writes the map's own actor record.</summary>
    private static PlacementDefinition Person(string id, int actor, int group)
    {
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "{{id}}", "kind": "person", "sourceField": "actors", "sourceIndex": {{actor}}, "x": 0, "y": 0, "z": 0, "monster": 9, "group": {{group}}, "people": [ "npc-7" ] }""");
        return new PlacementDefinition(new PlacementContentId("person", id), "actors", actor, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));
    }

    /// <summary>The staged events of the four steps, with the people, topics and history lines they name.</summary>
    private static ContentCatalog WorldStepsCatalog() =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [
                        { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                        { "path": "events.json", "documentId": "events", "definitionKind": "place-event" },
                        { "path": "people.json", "documentId": "people", "definitionKind": "person" },
                        { "path": "topics.json", "documentId": "topics", "definitionKind": "person-topic" },
                        { "path": "history.json", "documentId": "history", "definitionKind": "history-line" }
                      ]
                    }
                    """)
                .Add("packs/world/places.json", Places)
                .Add("packs/world/events.json", WorldStepEvents)
                .Add(
                    "packs/world/people.json",
                    """
                    {
                      "documentId": "people",
                      "definitionKind": "person",
                      "entries": [
                        { "id": "npc-7", "npcId": 7, "name": "Eric", "greeting": "Welcome to the desk.", "dialogueEvents": 2,
                          "topicSlots": [ 40, 39, 0, 0, 0, 0 ],
                          "topics": [
                            { "id": "topic-39", "label": "Old business", "text": "There is nothing new.", "textCount": 1 },
                            { "id": "topic-40", "label": "The desk", "text": "Ask me anything.", "textCount": 1 } ] }
                      ]
                    }
                    """)
                .Add(
                    "packs/world/topics.json",
                    """
                    {
                      "documentId": "topics",
                      "definitionKind": "person-topic",
                      "entries": [
                        { "id": "topic-39", "label": "Old business", "text": "There is nothing new.", "textCount": 1 },
                        { "id": "topic-40", "label": "The desk", "text": "Ask me anything.", "textCount": 1 },
                        { "id": "topic-41", "label": "New business", "text": "The guild takes new members at the turn of the year.", "textCount": 1 }
                      ]
                    }
                    """)
                .Add(
                    "packs/world/history.json",
                    """
                    {
                      "documentId": "history",
                      "definitionKind": "history-line",
                      "entries": [ { "id": "1", "text": "On {date}, {member:1} and {member:2} rang the bell.", "title": "The Bell" } ]
                    }
                    """),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

    /// <summary>The bookcase, the four counts of the dead, the history book and the gongs.</summary>
    private const string WorldStepEvents =
        """
        {
          "documentId": "events",
          "definitionKind": "place-event",
          "entries": [
            { "id": "1.330", "place": "1", "event": 330, "label": "Bookcase", "raised": true,
              "steps": [ { "step": 0, "op": "set-npc-topic", "index": 1, "person": 7, "raises": 41 }, { "step": 1, "op": "exit" } ] },
            { "id": "1.340", "place": "1", "event": 340, "label": "Bookcase", "raised": true,
              "steps": [ { "step": 0, "op": "set-npc-topic", "index": 1, "person": 99, "raises": 41 }, { "step": 1, "op": "exit" } ] },
            { "id": "1.332", "place": "1", "event": 332, "label": "Bell", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "group", "value": 5, "target": 3, "amount": 0 },
                { "step": 1, "op": "status-text", "text": "Standing." }, { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "All down." } ] },
            { "id": "1.333", "place": "1", "event": 333, "label": "Bell", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "kind", "value": 7, "target": 3, "amount": 2 },
                { "step": 1, "op": "status-text", "text": "Standing." }, { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "All down." } ] },
            { "id": "1.334", "place": "1", "event": 334, "label": "Bell", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "creature", "value": 3, "target": 3, "amount": 0 },
                { "step": 1, "op": "status-text", "text": "Standing." }, { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "All down." } ] },
            { "id": "1.335", "place": "1", "event": 335, "label": "Bell", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "any", "value": 0, "target": 3, "amount": 1 },
                { "step": 1, "op": "status-text", "text": "Standing." }, { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "All down." } ] },
            { "id": "1.336", "place": "1", "event": 336, "label": "History Book", "raised": true,
              "steps": [ { "step": 0, "op": "add", "variable": "history", "index": 1, "value": 0 }, { "step": 1, "op": "exit" } ] },
            { "id": "1.341", "place": "1", "event": 341, "label": "History Book", "raised": true,
              "steps": [ { "step": 0, "op": "add", "variable": "history", "index": 9, "value": 0 }, { "step": 1, "op": "exit" } ] },
            { "id": "1.337", "place": "1", "event": 337, "label": "Gong", "raised": true,
              "steps": [ { "step": 0, "op": "toggle-actor-group-flag", "group": 5, "flag": 524288, "on": true }, { "step": 1, "op": "exit" } ] },
            { "id": "1.338", "place": "1", "event": 338, "label": "Gong", "raised": true,
              "steps": [ { "step": 0, "op": "toggle-actor-group-flag", "group": 5, "flag": 524288, "on": false }, { "step": 1, "op": "exit" } ] },
            { "id": "1.339", "place": "1", "event": 339, "label": "Gong", "raised": true,
              "steps": [ { "step": 0, "op": "toggle-actor-group-flag", "group": 5, "flag": 64, "on": true }, { "step": 1, "op": "exit" } ] }
          ]
        }
        """;

    /// <summary>A region with a gong where the party starts, a guard of group 5 and a loner of none, both far off.</summary>
    private const string GongPlaces =
        """
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "The Guild of Fire", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "fixture-350", "kind": "fixture", "sourceField": "events", "sourceIndex": 350, "x": 100, "y": 0, "z": 0,
                  "positionSource": "event-face-centroid", "eventId": 350, "name": "Gong", "faceCount": 1, "sourceModel": 60, "sourceModelName": "Gong" },
                { "id": "guard", "kind": "monster", "monster": "7", "name": "A guard", "group": 5, "x": 0, "y": 4000, "z": 0 },
                { "id": "loner", "kind": "monster", "monster": "7", "name": "A loner", "x": 0, "y": -4000, "z": 0 },
                { "id": "doorman", "kind": "person", "sourceField": "actors", "sourceIndex": 0, "x": -4000, "y": 0, "z": 0,
                  "monster": 7, "group": 5, "people": [ "npc-7" ] } ] },
            { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
          ]
        }
        """;

    /// <summary>
    /// A region with an altar where the party starts, and two of the level's own creature records as the importer
    /// writes them: a priest standing far off as actor 34, and one the level holds hidden as actor 35.
    /// </summary>
    private const string AltarPlaces =
        """
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "The Temple", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "fixture-351", "kind": "fixture", "sourceField": "events", "sourceIndex": 351, "x": 100, "y": 0, "z": 0,
                  "positionSource": "event-face-centroid", "eventId": 351, "name": "Altar", "faceCount": 1, "sourceModel": 61, "sourceModelName": "Altar" },
                { "id": "actor-34", "kind": "actor", "sourceField": "actors", "sourceIndex": 34, "x": 0, "y": 4000, "z": 0, "yaw": 512,
                  "positionSource": "actor-record", "actorName": "Priest", "monster": 7, "monsterName": "Priest",
                  "group": 0, "attributes": 0, "aiState": 0, "hitPoints": 180, "sectorId": 0, "uniqueNameIndex": 3 },
                { "id": "actor-35", "kind": "actor", "sourceField": "actors", "sourceIndex": 35, "x": 0, "y": -4000, "z": 0, "yaw": 0,
                  "positionSource": "actor-record", "actorName": "Priest", "monster": 7, "monsterName": "Priest",
                  "group": 0, "attributes": 65536, "aiState": 19, "hitPoints": 180, "sectorId": 0, "hidden": true } ] },
            { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
          ]
        }
        """;

    /// <summary>The altar: it asks whether the level's actor 34 is dead, with the count the Temple of Baa's event states.</summary>
    private const string AltarEvents =
        """
        {
          "documentId": "events",
          "definitionKind": "place-event",
          "entries": [
            { "id": "1.351", "place": "1", "event": 351, "label": "Altar", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "creature", "value": 34, "target": 3, "amount": 1 },
                { "step": 1, "op": "status-text", "text": "The priest still stands." },
                { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "The priest is dead." },
                { "step": 4, "op": "exit" } ] }
          ]
        }
        """;

    /// <summary>The doorman the gong's place stands.</summary>
    private const string GongPeople =
        """
        {
          "documentId": "people",
          "definitionKind": "person",
          "entries": [ { "id": "npc-7", "npcId": 7, "name": "A doorman", "topics": [] } ]
        }
        """;

    /// <summary>The gong: it counts group 5's dead first, and turns the group on the party while any stand.</summary>
    private const string GongEvents =
        """
        {
          "documentId": "events",
          "definitionKind": "place-event",
          "entries": [
            { "id": "1.350", "place": "1", "event": 350, "label": "Gong", "raised": true,
              "steps": [
                { "step": 0, "op": "is-actor-killed", "which": "group", "value": 5, "target": 3, "amount": 0 },
                { "step": 1, "op": "toggle-actor-group-flag", "group": 5, "flag": 524288, "on": true },
                { "step": 2, "op": "exit" },
                { "step": 3, "op": "status-text", "text": "Nobody answers the gong." },
                { "step": 4, "op": "exit" } ] }
          ]
        }
        """;
}
