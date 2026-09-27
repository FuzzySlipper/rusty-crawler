using System.Text.Json;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// What a party knows: the owner that keeps it, the vocabulary of a discovery, the book it is read through,
/// and what a save carries.
/// </summary>
/// <remarks>
/// <para>
/// The knowledge this suite drives is the mechanism with discoveries its own test states, which is the point
/// of the seam: the kit holds no landmark, no recipe, and no game's discovery table, so the test reports its
/// own facts and demands the same mechanism keep them. What only this suite can prove is that a fact learned
/// twice is one note, that a note is dated by the one clock and not by the machine, that a place the world
/// restores clears nothing here, that a note survives a save and reads as the day it was learned, and that
/// the Auto Notes book is a reading of this owner rather than a second place facts are kept.
/// </para>
/// <para>
/// Every discovery the product can actually make today is driven here through the owner that makes it: a
/// mixture whose own row records one, and the outcome of a use. The fountain and the obelisk are the two
/// that cannot be driven, because their effect and their note are instructions of a map event this build
/// does not execute; what this suite proves about them is the honest half — a use that was refused teaches
/// nothing, and no note is written for a landmark nothing ran.
/// </para>
/// </remarks>
public sealed class KnowledgeTests
{
    private static readonly PlaceId Keep = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly ItemDefinitionId Berry = new("berry");
    private static readonly ItemDefinitionId Bottle = new("bottle");
    private static readonly ItemDefinitionId Draught = new("draught");
    private static readonly ItemDefinitionId Tonic = new("tonic");
    private static readonly SkillId AlchemySkill = new("Alchemy");

    [Fact]
    public void A_fact_learned_once_is_one_note_and_learning_it_again_is_still_one()
    {
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);

        // A recipe the party mixes is reported the moment it is made, and the owner that made it is what the
        // note is attributed to. Mixing the same pair again is the same fact: the report is made again and the
        // knowledge owner answers that it is already known.
        KnowledgeReport recipe = new(KnowledgeKind.Recipe, "alchemy", "berry+bottle", "draught (berry + bottle)");
        Assert.True(knowledge.Record(recipe));
        Assert.False(knowledge.Record(recipe));
        Assert.True(knowledge.Knows(recipe));
        KnowledgeNote note = Assert.Single(knowledge.Notes);
        Assert.Equal(KnowledgeKind.Recipe, note.Kind);
        Assert.Equal("alchemy", note.Source);
        Assert.Equal("berry+bottle", note.Subject);

        // A different fact is a different note, and what makes two facts the same is the kind of thing and
        // what it is about and where: the same subject found somewhere else, and the same subject learned as
        // a different kind of thing, are each news.
        clock.Advance(GameDuration.FromHours(3));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Cave.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Keep.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Clue, "use", "511", "a line about the Ruby", Keep.Value)));
        Assert.False(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Cave.Value)));
        Assert.Equal(4, knowledge.Notes.Count);

        // The note is the game's phrase around the reporting owner's own name for the thing, and it is dated
        // by the session's one clock: the recipe at the ninth hour of the first day and the rest three hours
        // later, all on the day the party learned them.
        Assert.Equal("Learned the recipe for draught (berry + bottle)", knowledge.Notes[0].Text);
        Assert.Equal((1168, 1, 1, 9), Hour(knowledge.Notes[0]));
        Assert.Equal((1168, 1, 1, 12), Hour(knowledge.Notes[^1]));
    }

    [Fact]
    public void What_the_party_knows_outlives_a_place_reset()
    {
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);
        SessionWorld world = World(clock);

        // The party walks into a place, the world marks it visited and then cleared, and what it learned
        // there is reported by the owner of that moment.
        world.ArriveAt(Keep, PlacePose.Origin);
        world.Places.MarkCleared(Keep);
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Effect, "use", "well-4", "five spell points", Keep.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Keep.Value)));

        // A reset restores the place's population through the world's own path: the mark the party left is
        // gone from the place's state, and the facts about the place are exactly as they were. This is the
        // boundary the two owners exist for — the place is what the world currently is, and knowledge is what
        // the party carries out of it.
        clock.Advance(GameDuration.FromHours(24 * 8));
        IReadOnlyList<PlaceState> restored = world.Places.AdvanceTo(clock.ElapsedGameDays);
        Assert.Contains(restored, state => state.Place == Keep);
        Assert.False(world.Places.StateOf(Keep).Cleared);
        Assert.True(world.Places.StateOf(Keep).Visited);
        Assert.Equal(2, knowledge.Notes.Count);
        Assert.All(knowledge.Notes, note => Assert.Equal(Keep.Value, note.Place));

        // The save carries the facts across the reset as well: what a load restores is what the party knows,
        // and the place's population is the world's business rather than the knowledge owner's.
        PartyKnowledge loaded = new(new TestKnowledge(), Clock(), knowledge.Capture());
        Assert.Equal(2, loaded.Notes.Count);
    }

    [Fact]
    public void The_notes_book_reads_the_knowledge_owner_and_no_other_owner()
    {
        GameClock clock = Clock();
        PartyJournal journal = new(new TestJournal(), clock);
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);
        SessionWorld world = World(clock);
        world.ArriveAt(Keep, PlacePose.Origin);

        // The journal's own line about where the party has been is not a fact it learned, so the two books
        // say different things about the same visit.
        Assert.True(journal.Record(new JournalEvent(JournalEntryKind.Place, "world", Keep.Value, "the keep", Keep.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Effect, "use", "well-4", "five spell points", Keep.Value)));

        JournalSnapshot books = JournalSnapshot.From(journal, quests: null, world, clock, knowledge);

        // The notes book is fillable, holds the knowledge owner's own facts, and reads each row with when it
        // was learned and where — the panel computes none of it.
        Assert.Equal(["quests", "notes", "maps", "calendar", "history"], books.Books.Select(book => book.Kind));
        JournalBookSnapshot notes = books.Books[1];
        Assert.True(notes.Available);
        Assert.Equal("1 note", notes.State);
        JournalRowSnapshot row = Assert.Single(notes.Rows);
        Assert.Equal("Learned five spell points", row.Label);
        Assert.Equal("1168-01-01 09:00", row.Detail);
        Assert.Equal("the keep", row.State);
        Assert.Equal("use", row.Source);
        Assert.Equal("effect|well-4|1", row.Id);

        // And it is not a second copy of the history: the line the journal holds is not in it.
        Assert.DoesNotContain(notes.Rows, candidate => candidate.Label.Contains("Entered", StringComparison.Ordinal));
        Assert.Single(books.Books[4].Rows);

        // A session whose ruleset states no knowledge has no owner to read: the book says so in the game's
        // own words rather than showing an empty list a player would read as "there is nothing to learn here".
        JournalBookSnapshot unavailable = JournalSnapshot.From(journal, quests: null, world, clock, knowledge: null).Books[1];
        Assert.False(unavailable.Available);
        Assert.Contains("no knowledge owner", unavailable.State, StringComparison.Ordinal);
        Assert.Empty(unavailable.Rows);
    }

    [Fact]
    public void A_note_survives_a_save_and_reads_as_the_day_it_was_learned()
    {
        GameClock clock = Clock();
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);
        knowledge.Record(new KnowledgeReport(KnowledgeKind.Recipe, "alchemy", "berry+bottle", "draught (berry + bottle)"));

        // The party lives on for three days and then saves.
        clock.Advance(GameDuration.FromHours(72));
        PlaceStateLedger places = new(Graph(), PlaceRespawnRule.FromContent());
        SessionSave document = new(
            party.Capture(),
            ClockSave.Capture(clock),
            new WorldSave(new PartyPose(Keep, PlacePose.Origin), places.Capture()),
            knowledge: knowledge.Capture());
        string json = JsonSerializer.Serialize(document, SessionSaveJson.TypeInfo);
        string text = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(json));
        SessionSave read = JsonSerializer.Deserialize(json, SessionSaveJson.TypeInfo)!;

        // The document carries the section under its own name, and what it holds is the game time the fact
        // was learned and never a date: a date stored beside the calendar and starting date a ruleset
        // composes would move what a party knows the moment either changed.
        Assert.Contains("\"knowledge\"", text, StringComparison.Ordinal);
        KnowledgeNoteSave recorded = Assert.Single(read.Knowledge.Notes);
        Assert.Equal(0, recorded.ElapsedMilliseconds);
        Assert.Equal("recipe", recorded.Kind);
        Assert.Equal("alchemy", recorded.Source);
        Assert.DoesNotContain("1168", text, StringComparison.Ordinal);

        // A resumed session composes its clock at the starting date again and moves it to the recorded game
        // time, which is what makes a loaded note read as the day it was learned rather than the day it was
        // read — and the resumed clock is three days on from it.
        GameClock resumedClock = Clock();
        read.Clock.ApplyTo(resumedClock);
        Assert.Equal(3, resumedClock.ElapsedGameDays);
        PartyKnowledge resumed = new(new TestKnowledge(), resumedClock, read.Knowledge);
        KnowledgeNote loaded = Assert.Single(resumed.Notes);
        Assert.Equal((1168, 1, 1, 9), Hour(loaded));
        Assert.Equal(4, resumedClock.Now.Day);
        Assert.True(resumed.Knows(new KnowledgeReport(KnowledgeKind.Recipe, "alchemy", "berry+bottle", "anything")));
    }

    [Fact]
    public void A_save_that_contradicts_its_own_knowledge_is_refused_with_every_problem_named()
    {
        GameClock clock = Clock();
        clock.Advance(GameDuration.FromHours(1));
        long elapsed = clock.Elapsed.Milliseconds;

        KnowledgeSave contradicted = new(
        [
            new KnowledgeNoteSave("recipe", "alchemy", "berry+bottle", "Learned the recipe for draught", elapsed),
            new KnowledgeNoteSave("recipe", "alchemy", "berry+bottle", "Learned the recipe for draught", elapsed),
            new KnowledgeNoteSave("omen", "alchemy", "berry+bottle", "Something was learned", elapsed),
            new KnowledgeNoteSave("clue", "use", "stone", "Read a line", elapsed + 1, Cave.Value),
            new KnowledgeNoteSave("find", "search", "511", string.Empty, -1, Keep.Value),
        ]);

        IReadOnlyList<string> problems = contradicted.Problems(elapsed, PartyKnowledge.MaxNotes);

        // Every contradiction is named at once: the same fact twice, a kind this build has no word for, a
        // note learned after the game time the save had reached, a note that says nothing at all, and one
        // learned before the session began.
        Assert.Equal(5, problems.Count);
        Assert.Contains(problems, problem => problem.Contains("twice", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("'omen'", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("a future the party never lived", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("empty note", StringComparison.Ordinal));

        // A knowledge the product itself wrote is not re-judged: the notes it recorded pass, including one
        // whose place the world no longer carries, because what the party knows outlives the places it
        // learned it in.
        Assert.Empty(new KnowledgeSave([new KnowledgeNoteSave("effect", "use", "long-gone", "Learned something", elapsed)]).Problems(elapsed, PartyKnowledge.MaxNotes));

        // The bound is part of the schema's own judgement: a document holding more facts than this build
        // keeps is refused rather than half-remembered.
        KnowledgeSave oversize = new(
            [.. Enumerable.Range(0, PartyKnowledge.MaxNotes + 1).Select(index =>
                new KnowledgeNoteSave("find", "search", $"item-{index}", $"Found item {index}", elapsed))]);
        Assert.Contains(
            oversize.Problems(elapsed, PartyKnowledge.MaxNotes),
            problem => problem.Contains("this build keeps at most", StringComparison.Ordinal));
    }

    [Fact]
    public void The_knowledge_is_bounded_and_forgets_its_oldest_notes()
    {
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);

        // A party that knows everything forever is a leak: it is carried whole in every save and rebuilt into
        // every projection, so the oldest facts fall off and a fact that has fallen off may be learned again.
        for (int index = 0; index < PartyKnowledge.MaxNotes + 3; index++)
        {
            clock.Advance(GameDuration.FromMinutes(1));
            Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Clue, "use", $"stone-{index}", $"line {index}", Keep.Value)));
        }

        Assert.Equal(PartyKnowledge.MaxNotes, knowledge.Notes.Count);
        Assert.Equal("Read line 3", knowledge.Notes[0].Text);
        Assert.DoesNotContain(knowledge.Notes, note => note.Subject == "stone-0");
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Clue, "use", "stone-0", "line 0", Keep.Value)));
        Assert.Equal(PartyKnowledge.MaxNotes, knowledge.Notes.Count);
    }

    [Fact]
    public void A_mixture_that_records_a_discovery_teaches_its_recipe_once()
    {
        GameClock clock = Clock();
        using PartyEntity party = PartyOf(Member("Nyx", alchemyLevel: 3, alchemyTier: 1));
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);
        AlchemyCatalog catalog = new([
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(1), Power: 5, Note: 58),
            new PotionMixture(Berry, Tonic, MixtureOutcome.Produces(Bottle), new SkillTier(1), Power: 2, Note: 0),
        ]);
        PotionMixing mixing = new(party, catalog, new TestAlchemy(), knowledge);

        // A mixture whose own row records a discovery teaches it, and the workflow that made the potion is
        // the owner of the moment: the note is attributed to it and named what it made.
        MixingResult first = mixing.Mix(new MixingRequest(0, Take(party, Berry).Id, Take(party, Bottle).Id));
        Assert.True(first.IsMixed);
        Assert.Equal(58, first.Note);
        KnowledgeNote note = Assert.Single(knowledge.Notes);
        Assert.Equal(KnowledgeKind.Recipe, note.Kind);
        Assert.Equal("alchemy", note.Source);
        Assert.Equal("berry+bottle", note.Subject);
        Assert.Equal("Learned the recipe for draught (berry + bottle)", note.Text);
        Assert.Equal(1, NoteCount(knowledge));

        // Mixing the same pair again is the same recipe: the reporter reports it again and the knowledge
        // owner writes nothing, which is what "learning the same fact twice is one fact" means.
        clock.Advance(GameDuration.FromHours(1));
        Assert.True(mixing.Mix(new MixingRequest(0, Take(party, Berry).Id, Take(party, Bottle).Id)).IsMixed);
        Assert.Equal(1, NoteCount(knowledge));

        // A mixture the game's table states no discovery for teaches nothing, and neither does a mixture that
        // never happened: a refused attempt leaves both ingredients and writes no note.
        Assert.True(mixing.Mix(new MixingRequest(0, Take(party, Berry).Id, Take(party, Tonic).Id)).IsMixed);
        Assert.Equal(1, NoteCount(knowledge));
        MixingResult refused = mixing.Mix(new MixingRequest(0, Take(party, Bottle).Id, Take(party, Tonic).Id));
        Assert.False(refused.IsMixed);
        Assert.Equal("mixture-unknown", refused.Code);
        Assert.Equal(1, NoteCount(knowledge));
    }

    [Fact]
    public void A_use_reports_what_it_taught_and_a_use_that_was_refused_teaches_nothing()
    {
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);
        InteractionTarget target = new(
            new InteractionTargetId(Keep, new PlacementContentId("chest", "chest-1")),
            0,
            Placement("chest", "chest-1"),
            new InteractionTargetDefinition(new InteractionTargetKind("container"), "A chest", InteractionVerb.Search, 512),
            new InteractionTargetState(string.Empty, 0));

        // What a use taught travels on its own outcome and result, so the one caller that holds the knowledge
        // owner hands the facts over without reading the rest of the outcome. This is the path a landmark's
        // effect will take the moment a map event is executed: the rule that ran it states what was learned.
        InteractionOutcome searched = InteractionOutcome.Applied(
            "searched",
            "The chest holds a Ruby.",
            items: [new InteractionItemYield(new ItemDefinitionId("511"))],
            learned: [new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Keep.Value)]);
        InteractionResult applied = InteractionResult.Applied(target, searched, searched.Message);
        Assert.True(applied.IsApplied);
        KnowledgeReport found = Assert.Single(applied.Learned);
        Assert.True(knowledge.Record(found));
        Assert.Equal("Found the Ruby", Assert.Single(knowledge.Notes).Text);

        // A refused use changed nothing, so it taught nothing: the fountain and the obelisk of this build are
        // exactly this — a fixture whose event nothing executes, refused by name, with no note written for an
        // event that never ran.
        InteractionResult refused = InteractionResult.Refused(target, "interaction-event-not-executed", "Nothing in this build executes map events.");
        Assert.False(refused.IsApplied);
        Assert.Empty(refused.Learned);
        Assert.Single(knowledge.Notes);
    }

    [Fact]
    public void Only_the_owner_of_a_discovery_and_the_session_that_keeps_it_write_a_note()
    {
        // A note is reported by the owner of the moment that taught it — the mixing workflow for a recipe,
        // and, outside the kit, this game's own answers for what a use taught — and the session is the one
        // caller that hands a use's discoveries to the owner. A kit mechanism that reported its own facts
        // would be a second writer of what the party knows, so the scan fails the kit if one appears. What
        // the scan looks for is the construction of a report, so a source that reads the knowledge — a
        // projection, a screen's own value — is not an offender.
        string kit = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit");
        string knowledge = Path.Combine(kit, "Knowledge");
        string alchemy = Path.Combine(kit, "Alchemy", "PotionMixing.cs");
        string session = Path.Combine(kit, "Sessions", "PartyRpgSession.cs");
        List<string> offenders = [];
        foreach (string source in Directory.EnumerateFiles(kit, "*.cs", SearchOption.AllDirectories))
        {
            if (source.StartsWith(knowledge, StringComparison.Ordinal) ||
                string.Equals(source, alchemy, StringComparison.Ordinal) ||
                string.Equals(source, session, StringComparison.Ordinal))
            {
                continue;
            }

            if (source.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || source.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(source);
            if (text.Contains("new KnowledgeReport(", StringComparison.Ordinal))
            {
                offenders.Add($"{Path.GetFileName(source)}: reports a discovery of its own");
            }
        }

        Assert.Empty(offenders);

        // The scan is not vacuous: the two writers it allows are exactly what it looks for, and they name
        // themselves.
        Assert.Contains("new KnowledgeReport(", File.ReadAllText(alchemy), StringComparison.Ordinal);
        Assert.Contains("knowledge.Record(report)", File.ReadAllText(session), StringComparison.Ordinal);
    }

    [Fact]
    public void The_knowledge_owner_reads_no_place_state()
    {
        // The boundary between what the party knows and what a place currently is, stated in code rather
        // than only in prose: nothing under Knowledge/ names the world, its places, or their per-place state,
        // so a place the clock restores cannot reach what the party has learned even by accident. That is
        // the whole point of the split, and a field of the world's creeping into this owner would undo it
        // silently.
        string knowledge = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit", "Knowledge");
        string[] forbidden = ["SessionWorld", "PlaceState", "PlaceId", "World"];
        List<string> offenders = [];
        foreach (string source in Directory.EnumerateFiles(knowledge, "*.cs", SearchOption.AllDirectories))
        {
            string text = string.Join(
                '\n',
                File.ReadAllLines(source).Where(line =>
                    !line.TrimStart().StartsWith("///", StringComparison.Ordinal) &&
                    !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (string token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                {
                    offenders.Add($"{Path.GetFileName(source)} names '{token}': what a party knows is not a reading of the world");
                }
            }
        }

        Assert.Empty(offenders);

        // The scan is not vacuous: the owner's remarks explain the boundary by naming what it is kept apart
        // from, and those words are read past on purpose — a rule that forbade explaining itself would be a
        // trap rather than a boundary.
        Assert.Contains("per-place state", File.ReadAllText(Path.Combine(knowledge, "PartyKnowledge.cs")), StringComparison.Ordinal);
    }

    [Fact]
    public void The_game_decides_what_is_worth_knowing_and_how_a_note_reads()
    {
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new TestKnowledge(), clock);

        // This game's threshold is its own: an ordinary thing found is not worth knowing and a notable one
        // is, which is what keeps what a party knows from becoming a list of everything it picked up.
        Assert.False(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "plain", "a rusty sword", Keep.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "511", "the Ruby", Keep.Value)));
        Assert.Equal("Found the Ruby", Assert.Single(knowledge.Notes).Text);

        // Every kind of discovery is worth keeping by default: a recipe, a landmark's effect, and something
        // read are facts the party gained rather than things it happened to pick up.
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Recipe, "alchemy", "berry+bottle", "draught")));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Effect, "use", "well-4", "five spell points", Keep.Value)));
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Clue, "use", "stone", "a line", Cave.Value)));
        Assert.Equal(4, knowledge.Notes.Count);
    }

    /// <summary>The clock these tests run on: a session that began on the first day of 1168, at nine.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>A world the knowledge is read beside: a region and an interior, with the party at the region.</summary>
    private static SessionWorld World(GameClock clock)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document(
                    "places",
                    "place",
                    """{ "id": "1", "kind": "region", "name": "the keep", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 10, "y": 20, "z": 0, "yaw": 512 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "the cave", "respawnDays": 7, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }"""))
                .Add("packs/world/links.json", Document(
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

    /// <summary>How many notes a party keeps, which is what a screen's count reads.</summary>
    private static int NoteCount(PartyKnowledge knowledge) => knowledge.Notes.Count;

    /// <summary>A note's date and hour, as this suite compares them.</summary>
    private static (int Year, int Month, int Day, int Hour) Hour(KnowledgeNote note) =>
        (note.Date.Year, note.Date.Month, note.Date.Day, note.Date.Hour);

    /// <summary>A placement as the importer writes one, so the use's target is a real one.</summary>
    private static PlacementDefinition Placement(string kind, string id) =>
        new(
            new PlacementContentId(kind, id),
            "placements",
            0,
            PlacePose.Origin,
            new ContentEntry(id, JsonDocument.Parse($$"""{ "id": "{{id}}", "kind": "{{kind}}" }""").RootElement));

    /// <summary>A thing in the pack a mixture spends, put there through the party's own acquisition path.</summary>
    private static ItemInstance Take(PartyEntity party, ItemDefinitionId definition)
    {
        ItemInstance item = party.CreateItem(definition);
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }

    private static PartyEntity PartyOf(params MemberCreation[] members) =>
        new PartyEntityFactory().Create(new PartyCreation(members, coins: 0, foodPortions: 0, reputation: 0, fame: 0));

    private static MemberCreation Member(string name, int alchemyLevel = 0, int alchemyTier = 0) => new(new PartyMemberSeed(
        name,
        new RaceId("testfolk"),
        new ClassId("recruit"),
        [new AttributeScore(new AttributeId("vigour"), 12)],
        alchemyLevel > 0 ? [new SkillEntry(AlchemySkill, alchemyLevel, new SkillTier(alchemyTier), 0)] : [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "authored for a test" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root could not be found from the test's own directory.");
    }

    /// <summary>This suite's knowledge: four kinds with the test's own words and a threshold over finds.</summary>
    private sealed class TestKnowledge : IKnowledgeRule
    {
        public string Phrase(KnowledgeKind kind) => kind switch
        {
            KnowledgeKind.Effect => "Learned",
            KnowledgeKind.Clue => "Read",
            KnowledgeKind.Recipe => "Learned the recipe for",
            _ => "Found",
        };

        /// <summary>This test's threshold: a find is worth knowing unless the test calls it plain.</summary>
        public bool WorthLearning(KnowledgeReport report) =>
            report.Kind != KnowledgeKind.Find || !report.Subject.StartsWith("plain", StringComparison.Ordinal);
    }

    /// <summary>This suite's journal: five books with the test's own words, so the notes book can be read.</summary>
    private sealed class TestJournal : IJournalRule
    {
        public JournalBookWords Book(JournalBookKind book) => book switch
        {
            JournalBookKind.Quests => new("Current Quests", "no errands", "no quests stated"),
            JournalBookKind.Notes => new("Auto Notes", "nothing learned", "no knowledge owner"),
            JournalBookKind.Maps => new("Maps", "nowhere known", "no world"),
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

        public bool WorthRecording(JournalEvent journalEvent) => true;
    }

    /// <summary>This suite's answers about mixing: the mechanism's own seam, with the test's numbers.</summary>
    private sealed class TestAlchemy : IAlchemyRule
    {
        public SkillId Skill => AlchemySkill;

        public string RungName(SkillTier tier) => tier.Value switch
        {
            0 => "untrained",
            1 => "novice",
            2 => "expert",
            3 => "master",
            _ => "grand master",
        };

        public string MasteryRaisedBy(SkillId skill) => $"a lesson in {skill.Value} raises the rung";

        public string NameOf(ItemDefinitionId definition) => definition.Value;

        public PartyRefusal? MayMix(PartyMember mixer) => null;

        public int Strength(PartyMember mixer, PotionMixture mixture, ItemInstance first, ItemInstance second) =>
            Math.Max(1, mixer.Skills.LevelOf(AlchemySkill) + mixture.Power);

        public MixtureBackfire Backfire(int strength) => new(strength, "harm", null);
    }

    /// <summary>Walking is free, which is all this suite's world needs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
