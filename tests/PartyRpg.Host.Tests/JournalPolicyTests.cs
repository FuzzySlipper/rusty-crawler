using System.Globalization;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's journal against the real composition: the lines its own events write, the words its five
/// books carry, and what a save does to the day each line happened on.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the owner with events it states itself. What only this suite can prove is that
/// this game's readings reach it: the place the world reports, the person a use opens onto, the three
/// moments of a shipped errand, the rank a promotion grants, and the artifact a search yields are each
/// written down by the owner that reports them, in this game's own voice, on the day the clock stood at.
/// </para>
/// <para>
/// The composed case is written the way this game's own content is — a place, the person standing in it, the
/// tables around them, and the containers a map records — so the same suite proves the shipped policy rather
/// than a fixture invented for it.
/// </para>
/// </remarks>
public sealed class JournalPolicyTests
{
    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UseAction,
        ProductIdentity.UiActionContract);

    private static readonly ConversationIntentNames ConversationControls = new(
        ProductIdentity.ConversationLeaveIntent,
        ProductIdentity.UiActionContract);

    [Fact]
    public void This_game_writes_its_own_record_from_the_owners_that_report_each_event()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(JournalContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Conversation = ConversationControls,
            });
        session.Start();

        // The first update puts the party where the scenario says and reads the place from the world: the
        // party's own record starts with where it has been, dated on the day it arrived.
        session.Update(ProductTestContext.Update(1, 1));
        ProjectedNode books = Journal(ui);
        Assert.True(books.Field("available").AsBoolean());
        Assert.Equal(
            ["quests", "notes", "maps", "calendar", "history"],
            BookKinds(books));
        Assert.Equal(
            ["Current Quests", "Auto Notes", "Maps", "Calendar", "History"],
            Titles(books));
        Assert.Equal([true, false, true, true, true], Availability(books));

        // The notes book is the seam the knowledge stone lands behind: this build composes no owner that
        // records what the party has learned, and the book says so in this game's own words rather than
        // showing an empty list a player would read as "there is nothing to know here".
        Assert.Contains("knowledge owner", Book(books, "notes").Field("state").AsString(), StringComparison.Ordinal);

        // What the world reports is one line, with the day it happened and the calendar's date beside it.
        Assert.Equal(["Entered Erathia"], History(books));
        Assert.Equal(Today(ui), HistoryDates(books)[0].Split(' ')[0]);

        // A use opens the conversation with the person standing in the residence, and meeting them is the
        // conversation's own report: one line, written the moment the party speaks with them.
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        Assert.Equal("Frederick Org", ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("speaker").AsString());
        Assert.Equal(["Entered Erathia", "Met Frederick Org"], History(Journal(ui)));

        // The errand's own three moments are written by the quest owner's answers: hearing it, taking it, and
        // — later, once it is done — finishing it.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("errand:35")));
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("accept:35")));

        // The errand is named in the words the shipped table states for it, which is what makes a record read
        // as the game's own rather than as a second name this mechanism gave it.
        string errand = ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("name").AsString();
        Assert.Equal(
            ["Entered Erathia", "Met Frederick Org", $"Was offered {errand}", $"Took on {errand}"],
            History(Journal(ui)));

        // The quests book is a reading of the quest owner rather than a copy of the record: it counts the
        // errands the party stands with, and the errand itself is the quests block's own page.
        ProjectedNode journal = Journal(ui);
        Assert.Equal("1 errand in the journal", Book(journal, "quests").Field("state").AsString());
        Assert.Equal(0, Book(journal, "quests").Field("rows").Length());
        Assert.Equal("accepted", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());

        // The conversation owns the player's controls while it is open, so the party takes its leave before
        // walking on: a use behind a screen is not a use at all, which is what keeps a key from ordering the
        // world through a menu.
        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.Digital(ProductIdentity.ConversationLeaveIntent)));
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());

        // A plain chest yields a rusty sword, which is what a party carries rather than what it remembers.
        Arrive(session, ui, "3");
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.Contains("rusty sword", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(1, ((MightAndMagic7Session)session).Party!.Inventory.TotalOf(new ItemDefinitionId("1")));
        Assert.DoesNotContain(History(Journal(ui)), line => line.Contains("sword", StringComparison.OrdinalIgnoreCase));

        // An artifact is a find this game writes down, and it is written by the search that produced it: the
        // journal is told what the container held, and this game's own threshold decides it is worth a line.
        Arrive(session, ui, "4");
        session.Update(ProductTestContext.Update(7, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.Contains("Ruby of Ultimate Power", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString(), StringComparison.Ordinal);
        ProjectedNode found = Journal(ui);
        Assert.Contains("Found The Ruby of Ultimate Power", History(found));
        ProjectedNode findRow = Row(found, "history", History(found).Count - 1);
        Assert.Equal("search", findRow.Field("source").AsString());
        Assert.Equal(Today(ui), findRow.Field("detail").AsString().Split(' ')[0]);

        // Every line is dated: the history's rows carry the day the clock stood at when it happened, and the
        // calendar book is the same clock read as a date rather than a second reading of it.
        Assert.Equal(7, History(found).Count);
        Assert.All(HistoryDates(found), date => Assert.Equal(Today(ui), date.Split(' ')[0]));
        Assert.Equal(Today(ui), Book(found, "calendar").Field("state").AsString());

        // Walking into the castle the errand asks for is the reach objective met, and handing the errand back
        // to its giver is the third moment: the errand is finished in the record exactly as it is in the quest
        // owner.
        Arrive(session, ui, "1");
        session.Update(ProductTestContext.Update(8, 1));
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("canTurnIn").AsBoolean());
        Arrive(session, ui, "2");
        session.Update(ProductTestContext.Update(9, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(10, 1, ProductTestContext.ChooseTopic("turn-in:35")));
        Assert.Equal("turned-in", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());
        Assert.Equal(
            ["Entered Erathia", "Met Frederick Org", $"Was offered {errand}", $"Took on {errand}", "Entered A cellar", "Entered The vault", "Found The Ruby of Ultimate Power", "Entered Castle Navan", $"Finished {errand}"],
            History(Journal(ui)));

        // A rank is one more moment, written where the progression owner's own answer arrives, and named by
        // the class the rank reaches.
        session.Update(ProductTestContext.Update(11, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(12, 1, ProductTestContext.ChooseTopic("promote:knight-cavalier")));
        Assert.Equal("granted", ProjectedNode.Of(ui.Latest().Value).Field("promotion").Field("outcome").AsString());
        ProjectedNode ranked = Journal(ui);
        Assert.Contains("Was raised to Cavalier", History(ranked));

        // The maps book is the world's own knowledge of where the party has been, read at the moment the
        // projection is built rather than remembered by the journal.
        ProjectedNode maps = Book(ranked, "maps");
        Assert.Equal("4 of 4 places known", maps.Field("state").AsString());
        Assert.All(
            Enumerable.Range(0, maps.Field("rows").Length()),
            position => Assert.Equal("visited", maps.Field("rows").Item(position).Field("state").AsString()));
    }

    [Fact]
    public void A_saved_record_keeps_the_day_each_line_happened_on()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = JournalContent();

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls });
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));

        // Two lines are written on the party's first day: where it arrived, and who it met there.
        List<string> written = History(Journal(ui));
        Assert.Equal(["Entered Erathia", "Met Frederick Org"], written);
        string day = Row(Journal(ui), "history", 0).Field("detail").AsString();
        Assert.StartsWith("1168-01-01", day, StringComparison.Ordinal);

        // The party lives on for a day before anything is written down again: a save taken now records lines
        // that happened on the day the party arrived, not on the day it saved.
        session.Update(ProductTestContext.Update(3, admittedSteps: 2880, stepSeconds: 1.0));
        string later = ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("date").AsString();
        Assert.NotEqual(day.Split(' ')[0], later);
        SessionSave document = MightAndMagic7Ruleset.Instance.Save(session);

        // The document carries the game time each line happened at and no dates at all: the calendar and the
        // date a session begins at are this ruleset's policy and are supplied again on the way back, so a
        // save records how far into the expedition a thing happened rather than which day it fell on.
        Assert.Equal(written.Count, document.Journal.Entries.Count);
        Assert.All(document.Journal.Entries, entry => Assert.True(entry.ElapsedMilliseconds < document.Clock.ElapsedMilliseconds));
        Assert.All(document.Journal.Entries, entry => Assert.True(entry.ElapsedMilliseconds < 1000));
        Assert.All(document.Journal.Entries, entry => Assert.DoesNotContain("1168", entry.Text, StringComparison.Ordinal));

        // A resumed session composes its clock and reads the lines back against it, so every line still reads
        // as the day it happened rather than as the day it was loaded.
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            ProductTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume, Use = UseControls });
        resumed.Start();

        ProjectedNode record = Journal(resumedUi);
        Assert.Equal(later, ProjectedNode.Of(resumedUi.Latest().Value).Field("clock").Field("date").AsString());
        Assert.Equal(written, History(record));
        Assert.Equal(day, Row(record, "history", 0).Field("detail").AsString());
        Assert.StartsWith("1168-01-01", HistoryDates(record)[0], StringComparison.Ordinal);
        Assert.NotEqual(later, HistoryDates(record)[0].Split(' ')[0]);

        // And the record is one the resumed session plays on with rather than a document held in memory: a
        // place entered after the load is written down beside the lines that were loaded, dated on the day
        // that load left the clock at.
        Arrive(resumed, resumedUi, "3");
        resumed.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.Equal([.. written, "Entered A cellar"], History(Journal(resumedUi)));
        Assert.Equal(later, Row(Journal(resumedUi), "history", written.Count).Field("detail").AsString().Split(' ')[0]);
        Assert.Equal(1, ((MightAndMagic7Session)resumed).Party!.Inventory.TotalOf(new ItemDefinitionId("1")));
    }

    /// <summary>The journal block of the newest projection.</summary>
    private static ProjectedNode Journal(RecordingUiService ui) =>
        ProjectedNode.Of(ui.Latest().Value).Field("journal");

    /// <summary>One book, found by the kind the wire spells.</summary>
    private static ProjectedNode Book(ProjectedNode journal, string kind)
    {
        ProjectedNode books = journal.Field("books");
        for (int position = 0; position < books.Length(); position++)
        {
            ProjectedNode book = books.Item(position);
            if (string.Equals(book.Field("kind").AsString(), kind, StringComparison.Ordinal)) return book;
        }

        throw new KeyNotFoundException($"The projection carries no book '{kind}'.");
    }

    /// <summary>The five kinds, in the order the projection published them.</summary>
    private static string[] BookKinds(ProjectedNode journal) =>
        [.. Enumerable.Range(0, journal.Field("books").Length()).Select(position => journal.Field("books").Item(position).Field("kind").AsString())];

    /// <summary>The five titles, in the order the projection published them.</summary>
    private static string[] Titles(ProjectedNode journal) =>
        [.. Enumerable.Range(0, journal.Field("books").Length()).Select(position => journal.Field("books").Item(position).Field("title").AsString())];

    /// <summary>Whether each book can be filled, in the order the projection published them.</summary>
    private static bool[] Availability(ProjectedNode journal) =>
        [.. Enumerable.Range(0, journal.Field("books").Length()).Select(position => journal.Field("books").Item(position).Field("available").AsBoolean())];

    /// <summary>What the history book reads, oldest first.</summary>
    private static List<string> History(ProjectedNode journal) => Rows(journal, "history", "label");

    /// <summary>When each line of the history happened, as the projection dated it.</summary>
    private static List<string> HistoryDates(ProjectedNode journal) => Rows(journal, "history", "detail");

    /// <summary>One field of every history row, in the order the book published them.</summary>
    private static List<string> Rows(ProjectedNode journal, string kind, string field)
    {
        ProjectedNode rows = Book(journal, kind).Field("rows");
        return [.. Enumerable.Range(0, rows.Length()).Select(position => rows.Item(position).Field(field).AsString())];
    }

    /// <summary>One row of one book, by its position.</summary>
    private static ProjectedNode Row(ProjectedNode journal, string kind, int position) =>
        Book(journal, kind).Field("rows").Item(position);

    /// <summary>The day the projection says it is, which is what a line written now has to read as.</summary>
    private static string Today(RecordingUiService ui) =>
        ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("date").AsString();

    /// <summary>Puts the party in a place the way a journey would, and lets the update read it.</summary>
    private static void Arrive(IGameSession session, RecordingUiService ui, string place)
    {
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId(place), PlacePose.Origin);
        session.Update(ProductTestContext.Update(1000ul + ulong.Parse(place, CultureInfo.InvariantCulture), 1));
        Assert.Equal(place, ProjectedNode.Of(ui.Latest().Value).Field("world").Field("place").AsString());
    }

    /// <summary>The places, the person, the errand, the chests, and the party an errand and a find are read over.</summary>
    private static (string Path, string Text)[] JournalContent() =>
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
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:npc-43" ] },
                { "path": "quests.json", "documentId": "quests", "definitionKind": "quest" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "2", "kind": "region", "name": "Erathia", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "residence-304", "kind": "residence", "name": "Org House", "proprietor": "Placeholder",
                      "fixture": "House", "x": 100, "y": 0, "z": 0, "people": [ "npc-43" ] } ] },
                { "id": "1", "kind": "interior", "name": "Castle Navan", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] },
                { "id": "3", "kind": "interior", "name": "A cellar", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "chest-1", "kind": "container", "name": "a plain chest", "x": 100, "y": 0, "z": 0,
                      "contents": [ { "item": 1 } ] } ] },
                { "id": "4", "kind": "interior", "name": "The vault", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "chest-2", "kind": "container", "name": "a gilded chest", "x": 100, "y": 0, "z": 0,
                      "contents": [ { "item": 500 } ] } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "1", "name": "A rusty sword", "material": "Steel", "value": 10 },
                { "id": "500", "name": "The Ruby of Ultimate Power", "material": "Artifact", "value": 5000 }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            {
              "documentId": "people",
              "definitionKind": "person",
              "entries": [
                { "id": "npc-43", "npcId": 43, "name": "Frederick Org", "portrait": "700",
                  "greeting": "'Well met, travellers.'", "greetingAgain": "'You again.'", "dialogueEvents": 1,
                  "topics": [ { "id": "topic-1", "label": "The castle", "text": "'This is my castle.'", "textCount": 1 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/quests.json",
            """
            {
              "documentId": "quests",
              "definitionKind": "quest",
              "entries": [
                { "id": "35", "text": "Raid the Elven Treasury at Castle Navan and return to Frederick Org.", "owner": "authored" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "2", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "classRank": 1,
                      "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] } ] }
              ]
            }
            """),
    ];
}
