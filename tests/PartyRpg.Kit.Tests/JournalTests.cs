using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The journal: the dated lines a party writes down, the owner that keeps them, what its five books read, and
/// what a save carries.
/// </summary>
/// <remarks>
/// <para>
/// The journal this suite drives is the mechanism with events its own test states, which is the point of the
/// seam: the kit holds no place, no quest, no person, and no item of any game's, so the test reports its own
/// events and demands the same mechanism record them. What only this suite can prove is that a line is dated
/// by the one clock and not by the machine, that the same event reported twice is one entry, that the history
/// is bounded and survives a place reset and a save, that a loaded line reads as the day it happened, and
/// that the five books are readings of their owners rather than copies.
/// </para>
/// <para>
/// The save round trip goes through the document a product really writes: the party's, the quests', and the
/// journal's sections are serialized and read back with the one current schema's own metadata, so a section
/// that could not be written fails here rather than in a session.
/// </para>
/// </remarks>
public sealed class JournalTests
{
    private static readonly PlaceId Keep = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    [Fact]
    public void A_quest_taken_progressed_and_finished_writes_dated_lines_and_the_book_reads_the_owner()
    {
        GameClock clock = TestClock.Create();
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyQuests quests = new(new TestQuests(Errand()), party, new PartyResourceLedger(party));
        PartyJournal journal = new(new TestJournal(), clock);

        // The world reports the place the party stands in, and the journal is told about it: the report is a
        // state the world repeats every update, and the first one that is news is the only line written.
        JournalEvent standing = Place(Keep);
        Assert.True(journal.Record(standing));
        Assert.False(journal.Record(standing));

        // The errand's three moments arrive from the quest owner's own answers, exactly as the session hands
        // them over, and the day they happened on is the clock's.
        clock.Advance(GameDuration.FromHours(2));
        Assert.True(journal.Record(Quest(JournalEntryKind.QuestOffered, quests.Offer(Errand().Id, "marshal", Keep))));
        clock.Advance(GameDuration.FromHours(24));
        Assert.True(journal.Record(Quest(JournalEntryKind.QuestTaken, quests.Accept(Errand().Id))));
        clock.Advance(GameDuration.FromHours(48));

        // The errand asks for a place, and the world reports the party standing in it: the progress is the
        // quest owner's own reading of that report, and the journal is told nothing about it.
        quests.Observe(Cave);
        Assert.True(quests.Read(Errand().Id)!.IsComplete);
        Assert.True(journal.Record(Quest(JournalEntryKind.QuestFinished, quests.TurnIn(Errand().Id, "marshal"))));

        // Four lines, oldest first, in the game's own phrasing around the owner's own name for the thing.
        Assert.Equal(4, journal.Entries.Count);
        Assert.Equal(
            ["Entered the keep", "Was offered The seal of office", "Took on The seal of office", "Finished The seal of office"],
            journal.Entries.Select(entry => entry.Text));
        Assert.Equal(
            ["world", "quest", "quest", "quest"],
            journal.Entries.Select(entry => entry.Source));

        // Every line is dated by the game clock it happened on and not by the order it was written in: the
        // offer is two hours into the first day, the taking a day later, and the finish two days after that.
        Assert.Equal(
            [(1168, 1, 1, 9), (1168, 1, 1, 11), (1168, 1, 2, 11), (1168, 1, 4, 11)],
            journal.Entries.Select(entry => (entry.Date.Year, entry.Date.Month, entry.Date.Day, entry.Date.Hour)));

        // The quests book is a reading of the owner and not a copy of what was written down: the errand stands
        // turned in, its objectives are read live, and the journal's own lines still say what happened.
        JournalSnapshot books = JournalSnapshot.From(journal, quests, world: null, clock);
        JournalBookSnapshot questsBook = books.Books.Single(book => book.Kind == "quests");
        Assert.True(questsBook.Available);
        Assert.Equal("1 errand in the journal", questsBook.State);
        Assert.Empty(questsBook.Rows);
        Assert.Equal("turned-in", quests.Read(Errand().Id)!.State);
        Assert.Equal(4, journal.Entries.Count);
    }

    [Fact]
    public void A_line_is_dated_when_it_happened_and_not_when_the_save_was_loaded()
    {
        GameClock clock = TestClock.Create();
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyJournal journal = new(new TestJournal(), clock);
        journal.Record(Place(Keep));

        // The party lives on for three days after the line was written, and then saves.
        clock.Advance(GameDuration.FromHours(72));
        PlaceStateLedger places = new(Graph(), PlaceRespawnRule.FromContent());
        SessionSave document = new(party.Capture(), ClockSave.Capture(clock), new WorldSave(new PartyPose(Keep, PlacePose.Origin), places.Capture()), journal: journal.Capture());
        string json = JsonSerializer.Serialize(document, SessionSaveJson.TypeInfo);
        SessionSave read = JsonSerializer.Deserialize(json, SessionSaveJson.TypeInfo)!;
        Assert.Single(read.Journal.Entries);

        // What the document carries is the game time the line happened at, and never a date: a date stored
        // beside the calendar and starting date a ruleset composes would move a party's whole history the
        // moment either changed.
        Assert.Equal(0, read.Journal.Entries[0].ElapsedMilliseconds);

        // A resumed session composes its clock at the starting date again and moves it to the recorded game
        // time, which is what makes a loaded line read as the day it happened rather than the day it was read.
        GameClock resumedClock = TestClock.Create();
        read.Clock.ApplyTo(resumedClock);
        Assert.Equal(3, resumedClock.ElapsedGameDays);
        PartyJournal resumed = new(new TestJournal(), resumedClock, read.Journal);
        JournalEntry loaded = Assert.Single(resumed.Entries);
        Assert.Equal((1168, 1, 1), (loaded.Date.Year, loaded.Date.Month, loaded.Date.Day));
        Assert.NotEqual(resumedClock.Now.Day, loaded.Date.Day);
    }

    [Fact]
    public void Every_kind_of_line_travels_the_save_by_its_own_word()
    {
        // A chronicle line — a page of the world's own story the party brought about — is written and read back
        // under its own word like every other kind, and a document naming a kind this build lacks is refused.
        foreach (JournalEntryKind kind in Enum.GetValues<JournalEntryKind>())
        {
            Assert.Equal(kind, JournalEntrySave.ReadKind(JournalEntrySave.Word(kind)));
        }

        Assert.Equal("chronicle", JournalEntrySave.Word(JournalEntryKind.Chronicle));
        GameClock clock = TestClock.Create();
        PartyJournal journal = new(new TestJournal(), clock);
        Assert.True(journal.Record(new JournalEvent(JournalEntryKind.Chronicle, "fixture", "history:1", "The bell was rung.")));
        Assert.Empty(journal.Capture().Problems(clock.Elapsed.Milliseconds, JournalHistory.MaxEntries));
    }

    [Fact]
    public void The_same_event_reported_twice_is_one_entry()
    {
        GameClock clock = TestClock.Create();
        PartyJournal journal = new(new TestJournal(), clock);

        // A place the party stands in, a person it keeps speaking with, and an errand it was told about are
        // all reported again and again by the owners that hold them; each is written once.
        Assert.True(journal.Record(Place(Keep)));
        Assert.False(journal.Record(Place(Keep)));
        Assert.True(journal.Record(Meeting("clerk", "the clerk")));
        Assert.False(journal.Record(Meeting("clerk", "the clerk")));
        Assert.True(journal.Record(Quest(JournalEntryKind.QuestOffered, "seal-of-office", "The seal of office")));
        Assert.False(journal.Record(Quest(JournalEntryKind.QuestOffered, "seal-of-office", "The seal of office")));
        Assert.Equal(3, journal.Entries.Count);

        // What is not the same event is a second line: another place, another person, another stage of the
        // same errand, and the same thing found somewhere else.
        clock.Advance(GameDuration.FromHours(1));
        Assert.True(journal.Record(Place(Cave)));
        Assert.True(journal.Record(Meeting("marshal", "the marshal")));
        Assert.True(journal.Record(Quest(JournalEntryKind.QuestTaken, "seal-of-office", "The seal of office")));
        Assert.True(journal.Record(Find("511", "the Ruby of Ultimate Power", Cave)));
        Assert.True(journal.Record(Find("511", "the Ruby of Ultimate Power", Keep)));
        Assert.Equal(8, journal.Entries.Count);
    }

    [Fact]
    public void The_history_outlives_a_place_reset()
    {
        GameClock clock = TestClock.Create();
        PartyJournal journal = new(new TestJournal(), clock);
        SessionWorld world = World(clock);

        // The party walks into a place, and the world marks it visited and then cleared: two facts about the
        // place, one of which a reset takes away.
        world.ArriveAt(Keep, PlacePose.Origin);
        world.Places.MarkCleared(Keep);
        Assert.True(journal.Record(Place(Keep)));
        Assert.True(journal.Record(Find("511", "the Ruby of Ultimate Power", Keep)));

        // A reset restores the place's population, which is a fact about the place and nothing at all about
        // the party's record: every line stands, and what the party has seen of the place is its own map
        // rather than the place's state — which the automap suite proves beside this one.
        clock.Advance(GameDuration.FromHours(24 * 8));
        IReadOnlyList<PlaceState> restored = world.Places.AdvanceTo(clock.ElapsedGameDays);
        Assert.Contains(restored, state => state.Place == Keep);
        Assert.False(world.Places.StateOf(Keep).Cleared);
        Assert.True(world.Places.StateOf(Keep).Visited);

        Assert.Equal(2, journal.Entries.Count);
        Assert.Equal(["Entered the keep", "Found the Ruby of Ultimate Power"], journal.Entries.Select(entry => entry.Text));

        JournalSnapshot books = JournalSnapshot.From(journal, quests: null, world, clock);
        JournalBookSnapshot maps = books.Books.Single(book => book.Kind == "maps");
        Assert.False(maps.Available);
        Assert.Equal(journal.Rule.Book(JournalBookKind.Maps).Unavailable, maps.State);

        // The save carries the lines across a reset as well: what a load restores is the party's own record,
        // and the place's population is the world's business rather than the journal's.
        JournalSave captured = journal.Capture();
        PartyJournal loaded = new(new TestJournal(), TestClock.Create(), captured);
        Assert.Equal(2, loaded.Entries.Count);
    }

    [Fact]
    public void The_history_is_bounded_and_forgets_its_oldest_lines()
    {
        GameClock clock = TestClock.Create();
        PartyJournal journal = new(new TestJournal(), clock);

        // A journal that grows without limit is a leak: it is carried whole in every save and rebuilt into
        // every projection, so the history keeps the most recent lines and lets the oldest fall off.
        for (int index = 0; index < JournalHistory.MaxEntries + 3; index++)
        {
            clock.Advance(GameDuration.FromMinutes(1));
            Assert.True(journal.Record(new JournalEvent(JournalEntryKind.Meeting, "conversation", $"person-{index}", $"person {index}", Keep.Value)));
        }

        Assert.Equal(JournalHistory.MaxEntries, journal.Entries.Count);
        Assert.Equal("Met person 3", journal.Entries[0].Text);
        Assert.Equal($"Met person {JournalHistory.MaxEntries + 2}", journal.Entries[^1].Text);
        Assert.DoesNotContain(journal.Entries, entry => entry.Subject == "person-0");

        // A line that has fallen off is forgotten rather than remembered against the record: the same event
        // may be written again, which is what a bound that drops the oldest pages means.
        Assert.True(journal.Record(new JournalEvent(JournalEntryKind.Meeting, "conversation", "person-0", "person 0", Keep.Value)));
        Assert.Equal(JournalHistory.MaxEntries, journal.Entries.Count);

        // The bound is part of the schema's own judgement: a document holding more lines than this build keeps
        // is refused rather than half-remembered.
        JournalSave oversize = new([.. journal.Capture().Entries, .. journal.Capture().Entries, .. journal.Capture().Entries, .. journal.Capture().Entries, .. journal.Capture().Entries]);
        Assert.Contains(
            oversize.Problems(clock.Elapsed.Milliseconds, JournalHistory.MaxEntries),
            problem => problem.Code == SaveCodes.SaveJournalOversize);
    }

    [Fact]
    public void A_save_that_contradicts_its_own_journal_is_refused_with_every_problem_named()
    {
        GameClock clock = TestClock.Create();
        clock.Advance(GameDuration.FromHours(1));
        long elapsed = clock.Elapsed.Milliseconds;

        JournalSave contradicted = new(
        [
            new JournalEntrySave("place", "world", "1", "Entered the keep", elapsed, "1"),
            new JournalEntrySave("place", "world", "1", "Entered the keep", elapsed, "1"),
            new JournalEntrySave("omen", "world", "1", "Something happened", elapsed, "1"),
            new JournalEntrySave("place", "world", "2", "Entered the cave", elapsed + 1, "2"),
            new JournalEntrySave("place", "world", "3", string.Empty, -1, "3"),
        ]);

        IReadOnlyList<SaveProblem> problems = contradicted.Problems(elapsed, JournalHistory.MaxEntries);

        // Every contradiction is named at once: the same event twice, a kind this build has no word for, a
        // line dated after the game time the save had reached, a line that says nothing at all, and a line
        // that happened before the session began.
        Assert.Equal(5, problems.Count);
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveJournalTwice && problem.Subject == "Entered the keep");
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveJournalKindUnknown && problem.Subject == "Something happened");
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveJournalFuture && problem.Subject == "Entered the cave");
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveJournalEmpty);
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveJournalFuture && problem.Subject.Length == 0);

        // A history the product itself wrote is not re-judged: the lines it recorded pass, including one whose
        // place the world no longer carries, because a record outlives the places it happened in.
        Assert.Empty(new JournalSave([new JournalEntrySave("place", "world", "long-gone", "Entered a lost place", elapsed)]).Problems(elapsed, JournalHistory.MaxEntries));
    }

    [Fact]
    public void The_game_decides_which_finds_are_worth_a_line_and_how_a_line_reads()
    {
        GameClock clock = TestClock.Create();
        PartyJournal journal = new(new TestJournal(), clock);

        // This game's threshold is its own: an ordinary thing found is not worth a line and a notable one is,
        // which is what keeps a record of an expedition from becoming a list of everything it picked up.
        Assert.False(journal.Record(Find("plain", "a rusty sword", Keep)));
        Assert.True(journal.Record(Find("511", "the Ruby of Ultimate Power", Keep)));
        Assert.Equal("Found the Ruby of Ultimate Power", Assert.Single(journal.Entries).Text);

        // The line is the game's phrase around the reporting owner's own name for the thing, so a journal
        // never names a place, a quest, a person, or an item itself.
        Assert.Equal("search", journal.Entries[0].Source);
        Assert.Equal("511", journal.Entries[0].Subject);
        Assert.Equal("1", journal.Entries[0].Place);

        // The books' words are the game's too, and a book no owner fills says so in the game's own sentence
        // rather than showing an empty list.
        JournalBookWords notes = journal.Rule.Book(JournalBookKind.Notes);
        Assert.Equal("Auto Notes", notes.Title);
        Assert.NotEqual(string.Empty, notes.Unavailable);
        Assert.Equal("History", journal.Rule.Book(JournalBookKind.History).Title);
    }

    [Fact]
    public void The_five_books_are_readings_of_their_owners_and_notes_names_its_receiver()
    {
        GameClock clock = TestClock.Create();
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyQuests quests = new(new TestQuests(Errand()), party, new PartyResourceLedger(party));
        PartyJournal journal = new(new TestJournal(), clock);
        SessionWorld world = World(clock);
        world.ArriveAt(Keep, PlacePose.Origin);
        journal.Record(Place(Keep));

        JournalSnapshot books = JournalSnapshot.From(journal, quests, world, clock);

        Assert.True(books.Available);
        Assert.Equal(["quests", "notes", "maps", "calendar", "history"], books.Books.Select(book => book.Kind));

        // Every book is fillable except the two whose owners this call does not hold, and each names the owner
        // it waits for rather than pretending the party has learned or mapped nothing: "no owner keeps this" and
        // "the party holds nothing" are different facts. A session composed with a knowledge owner or a map
        // owner fills them, which the knowledge and automap suites prove beside this one.
        Assert.Equal([true, false, false, true, true], books.Books.Select(book => book.Available));
        Assert.Equal(journal.Rule.Book(JournalBookKind.Notes).Unavailable, books.Books[1].State);
        Assert.Empty(books.Books[1].Rows);
        Assert.Equal(journal.Rule.Book(JournalBookKind.Maps).Unavailable, books.Books[2].State);
        Assert.Empty(books.Books[2].Rows);

        JournalBookSnapshot calendar = books.Books[3];
        Assert.Equal("1168-01-01", calendar.State);
        Assert.Equal("Today", calendar.Rows[0].Label);
        Assert.Equal("1168-01-01", calendar.Rows[0].Detail);

        JournalBookSnapshot history = books.Books[4];
        Assert.Equal("1 entry", history.State);
        JournalRowSnapshot line = Assert.Single(history.Rows);
        Assert.Equal("Entered the keep", line.Label);
        Assert.Equal("1168-01-01 09:00", line.Detail);
        Assert.Equal("the keep", line.State);
        Assert.Equal("world", line.Source);

        // A session that keeps no journal has no books at all, which is a different fact from five books that
        // hold nothing.
        JournalSnapshot none = JournalSnapshot.From(journal: null, quests, world, clock);
        Assert.False(none.Available);
        Assert.Empty(none.Books);
    }

    [Fact]
    public void No_kit_source_outside_the_journal_and_the_session_writes_an_entry()
    {
        // A line is written by the owner of the event or by the one subscriber that reads the reports the session
        // already handles, and by nothing else: a kit mechanism that reported its own events would be a second writer
        // of the party's record. The law finds every call to the journal's one write, bound to the member itself, so
        // a source that reads the journal — a projection, a screen's own value — is not an offender. A ruleset's own
        // owners report through the same entry and are outside the kit, which is where the game's finds come from;
        // the session's reports are written in two files: the update itself, and the router that hands a
        // conversation's offer to the owner whose answer the journal records.
        SourceCode kit = ProductSource.Kit;
        ProductSource.OnlyIn(
            kit.Uses([.. kit.Members(typeof(PartyJournal), nameof(PartyJournal.Record))]),
            file => file is "src/PartyRpg.Kit/Sessions/PartyRpgSession.cs" or "src/PartyRpg.Kit/Sessions/ConversationHandoffRouter.cs"
                || file.StartsWith("src/PartyRpg.Kit/Journal/", StringComparison.Ordinal),
            "The party's record is written by the session's subscriber and the journal itself, and by no other kit mechanism.");
    }

    /// <summary>A world the journal is read beside: a region and an interior, with the party at the region.</summary>
    private static SessionWorld World(GameClock clock)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", TestPacks.World)
                .Add("packs/world/places.json", TestPacks.Document(
                    "places",
                    "place",
                    """{ "id": "1", "kind": "region", "name": "the keep", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 0, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "the cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", TestPacks.Document(
                    "links",
                    "travel-link",
                    """{ "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "1", "fromPlace": "2", "toPlace": "1", "x": 10, "y": 20, "z": 0, "yaw": 512 }""")),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        return new SessionWorld(
            graph,
            new PartyPoseOwner(new PartyPose(Keep, PlacePose.Origin), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512)),
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            clock);
    }

    /// <summary>The graph alone, for the tests that need a world section to save rather than a world to walk.</summary>
    private static PlaceGraph Graph() => PlaceGraph.From([], []);

    /// <summary>The report a place is: the world's own reading of where the party stands.</summary>
    private static JournalEvent Place(PlaceId place) => new(
        JournalEntryKind.Place,
        Source: "world",
        Subject: place.Value,
        Name: place == Keep ? "the keep" : "the cave",
        Place: place.Value);

    /// <summary>The report a meeting is: a person the conversation opened onto.</summary>
    private static JournalEvent Meeting(string person, string name) =>
        new(JournalEntryKind.Meeting, "conversation", person, name, Keep.Value);

    /// <summary>The report a find is: something a search yielded, where it was found.</summary>
    private static JournalEvent Find(string item, string name, PlaceId place) =>
        new(JournalEntryKind.Find, "search", item, name, place.Value);

    /// <summary>The report an errand is, as the session hands one over from the owner's own answer.</summary>
    private static JournalEvent Quest(JournalEntryKind kind, string id, string name) =>
        new(kind, "quest", id, name, Keep.Value);

    /// <summary>The same report from a quest owner's own result, which is what the session really hands over.</summary>
    private static JournalEvent Quest(JournalEntryKind kind, QuestResult result)
    {
        Assert.True(result.IsApplied, result.Refusal?.Message);
        return Quest(kind, result.Quest.Value, "The seal of office");
    }

    private static PartyEntity PartyOf(params MemberCreation[] members) =>
        new PartyEntityFactory().Create(new PartyCreation(members, coins: 0, foodPortions: 0, reputation: 0, fame: 0));

    private static MemberCreation Member(string name) => new(new PartyMemberSeed(
        name,
        new RaceId("testfolk"),
        new ClassId("recruit"),
        [new AttributeScore(new AttributeId("vigour"), 12)],
        skills: [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    /// <summary>The errand the quests in this suite are, stated by the test rather than by a game.</summary>
    private static QuestDefinition Errand() => new(
        new QuestId("seal-of-office"),
        "The seal of office",
        "marshal",
        [new QuestObjective("reach", QuestObjectiveKind.Reach, Cave.Value, label: "Reach the vault")]);

    /// <summary>The quests a test states, with the one ruleset answer this mechanism cannot make itself.</summary>
    private sealed class TestQuests(params QuestDefinition[] definitions) : IQuestRule
    {
        public IReadOnlyList<QuestDefinition> Definitions { get; } = definitions;

        public QuestDefinition? Definition(QuestId quest)
        {
            foreach (QuestDefinition definition in Definitions)
            {
                if (definition.Id == quest) return definition;
            }

            return null;
        }

        /// <summary>No death counts for anything: this suite's errand asks for a place.</summary>
        public int Counts(QuestKillRequest request) => 0;

        /// <summary>A stated condition holds exactly when the party carries what it names.</summary>
        public bool Holds(QuestConditionRequest request) =>
            request.Condition.Kind is ConversationConditionKind.Flag
            && request.Party.Records.Has(request.Condition.Name);
    }

    /// <summary>This suite's journal: five books with the test's own words and a threshold over finds.</summary>
    private sealed class TestJournal : IJournalRule
    {
        public IReadOnlyList<JournalBookKind> Books { get; } =
            [JournalBookKind.Quests, JournalBookKind.Notes, JournalBookKind.Maps, JournalBookKind.Calendar, JournalBookKind.History];

        public JournalBookWords Book(JournalBookKind book) => book switch
        {
            JournalBookKind.Quests => new("Current Quests", "no errands", "no quests stated"),
            JournalBookKind.Notes => new("Auto Notes", "nothing learned", "the knowledge owner keeps no notes in this build"),
            JournalBookKind.Maps => new("Maps", "nowhere mapped", "the map owner keeps no maps in this build"),
            JournalBookKind.Calendar => new("Calendar", "no day", "no clock"),
            _ => new("History", "nothing written", "no journal"),
        };

        public string Phrase(JournalEntryKind kind) => kind switch
        {
            JournalEntryKind.Place => "Entered",
            JournalEntryKind.QuestOffered => "Was offered",
            JournalEntryKind.QuestTaken => "Took on",
            JournalEntryKind.QuestFinished => "Finished",
            JournalEntryKind.Rank => "Was raised to",
            JournalEntryKind.Meeting => "Met",
            _ => "Found",
        };

        /// <summary>This test's threshold: a find is worth a line unless the test calls it plain.</summary>
        public bool WorthRecording(JournalEvent journalEvent) =>
            journalEvent.Kind != JournalEntryKind.Find || !journalEvent.Subject.StartsWith("plain", StringComparison.Ordinal);
    }
}
