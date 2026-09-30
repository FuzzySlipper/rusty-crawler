using System.Globalization;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// What this game's party learns, through the whole product: the discovery the shipped potion table's own row
/// records, the finds a search yields and this game judges worth knowing, the notes book that reads them, what
/// the save carries, and the two discoveries that cannot be made yet — a fountain's effect and an obelisk's
/// clue, whose words and whose effect are both instructions of a map event nothing executes.
/// </summary>
/// <remarks>
/// <para>
/// The kit's own suite proves the owner with discoveries it states itself. What only this suite can prove is
/// that this game's readings reach it: the mixture whose row carries a discovery number is reported by the
/// mixing workflow that made the potion, the artifact a search yields is reported by the search that produced
/// it, a detection spell writes nothing because it teaches nothing durable, and a landmark whose event nothing
/// runs teaches nothing at all — refused by name rather than recorded as if the party had drunk from it.
/// </para>
/// <para>
/// The composed case is written the way this game's own content is — places, the chests a map records, the
/// shipped item and potion rows by their own ids, and a party that knows the spell a detection is cast with —
/// so the same suite proves the shipped policy rather than a fixture invented for it.
/// </para>
/// </remarks>
public sealed class KnowledgePolicyTests
{
    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UiActionContract);

    private static readonly CastIntentNames CastControls = new(
        ProductIdentity.UiActionContract);

    /// <summary>The shipped potion rows this suite mixes: two real potions and the one they make.</summary>
    private static readonly ItemDefinitionId CureWounds = new("222");

    private static readonly ItemDefinitionId MagicPotion = new("223");

    private static readonly ItemDefinitionId CurePoison = new("226");

    /// <summary>The discovery the shipped table's own cell for that pair records.</summary>
    private const int CurePoisonDiscovery = 58;

    [Fact]
    public void The_shipped_mixture_rows_teach_a_recipe_the_party_keeps_once()
    {
        (MightAndMagic7Alchemy alchemy, ContentCatalog catalog) = ReadAlchemy();
        using PartyEntity party = Party();
        GameClock clock = Clock();
        PartyKnowledge knowledge = new(new MightAndMagic7Knowledge(Loot(catalog)), clock);
        PotionMixing mixing = new(party, alchemy.Catalog, alchemy, knowledge);

        // The shipped table records a discovery for this pair, and the mixture makes the potion its own row
        // states: mixing a red potion into a blue one makes a purple one, and the discovery index the cell
        // carries is what says the party learned something by doing it (OpenEnroth
        // src/GUI/UI/UIPopup.cpp:2157-2160).
        MixingResult first = mixing.Mix(new MixingRequest(0, First(party, CureWounds).Id, First(party, MagicPotion).Id));
        Assert.True(first.IsMixed, first.Message);
        Assert.Equal(CurePoison.Value, first.Result);
        Assert.Equal(CurePoisonDiscovery, first.Note);

        // The note is this game's own phrase around what the mixture made and what it was made of, it is
        // attributed to the workflow that owns the moment, and its identity is the pair, in one canonical
        // order, so the same two things mixed the other way round are the same recipe.
        KnowledgeNote note = Assert.Single(knowledge.Notes);
        Assert.Equal(KnowledgeKind.Recipe, note.Kind);
        Assert.Equal("alchemy", note.Source);
        Assert.Equal("222+223", note.Subject);
        Assert.Equal("Learned the recipe for Cure Poison (Cure Wounds + Magic Potion)", note.Text);
        Assert.Equal((1168, 1, 1, 9), (note.Date.Year, note.Date.Month, note.Date.Day, note.Date.Hour));

        // Mixing the same pair a second time is the same fact: the workflow reports it again, the knowledge
        // owner answers that it is already known, and the party still knows one recipe. Reversing the two
        // things is the same pair, which is what the canonical subject is for.
        Assert.True(mixing.Mix(new MixingRequest(0, First(party, MagicPotion).Id, First(party, CureWounds).Id)).IsMixed);
        Assert.Single(knowledge.Notes);
        Assert.True(knowledge.Knows(new KnowledgeReport(KnowledgeKind.Recipe, "alchemy", "222+223", "anything")));

        // The threshold is this game's: a recipe is worth keeping, and an ordinary find is not.
        Assert.True(knowledge.Record(new KnowledgeReport(KnowledgeKind.Recipe, "alchemy", "200+220", "a red potion")));
        Assert.False(knowledge.Record(new KnowledgeReport(KnowledgeKind.Find, "search", "1", "a rusty sword", "1")));
        Assert.Equal(2, knowledge.Notes.Count);
    }

    [Fact]
    public void A_session_writes_what_its_party_learns_into_its_notes_book_and_its_save()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = Content();

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls, Cast = CastControls });
        session.Start();

        // A party that has just arrived knows nothing worth noting: the book is the knowledge owner's page
        // and says what it holds rather than pretending nobody keeps it.
        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("The party has learned nothing worth noting yet.", Book(Notes(ui), "notes").Field("state").AsString());
        Assert.Equal(0, Book(Notes(ui), "notes").Field("rows").Length());

        // A gilded chest in the first place holds an artifact, and searching it is the moment the party learns
        // the thing exists: the note is written by the search that produced it and reads in this game's own
        // words, with the day it was learned and where.
        Arrive(session, ui, "1");
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.Contains("Ruby of Ultimate Power", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString(), StringComparison.Ordinal);
        ProjectedNode notes = Book(Notes(ui), "notes");
        Assert.True(notes.Field("available").AsBoolean());
        Assert.Equal("1 note", notes.Field("state").AsString());
        ProjectedNode row = notes.Field("rows").Item(0);
        Assert.Equal("Found The Ruby of Ultimate Power", row.Field("label").AsString());
        Assert.Equal("search", row.Field("source").AsString());
        Assert.Equal("find|500|1", row.Field("id").AsString());
        Assert.StartsWith(Today(ui), row.Field("detail").AsString(), StringComparison.Ordinal);
        Assert.Equal("Erathia", row.Field("state").AsString());

        // A plain chest in the next place yields an ordinary sword, which is what a party carries rather than
        // what it remembers: the find is reported and this game's threshold refuses it, so the book is
        // unchanged and the sword is in the pack.
        Arrive(session, ui, "2");
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.Contains("rusty sword", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(1, ((MightAndMagic7Session)session).Party!.Inventory.TotalOf(new ItemDefinitionId("1")));
        Assert.Equal(1, Book(Notes(ui), "notes").Field("rows").Length());

        // A landmark whose effect and whose note are both instructions of a map event: nothing in this build
        // executes one, so the use is refused by name and the party learns nothing. This is the fountain and
        // the obelisk of the operator's own data, and the note is not written for an event that never ran.
        Arrive(session, ui, "3");
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode interaction = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("interaction-event-not-executed", interaction.Field("code").AsString());
        Assert.Contains("event 150", interaction.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("neither what the event gives nor what it teaches is learned", interaction.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(1, Book(Notes(ui), "notes").Field("rows").Length());

        // What the party knows is the save's own section, carried as the game time it was learned and never as
        // a date: the calendar and the starting date are this ruleset's policy and are supplied again on the
        // way back.
        SessionSave document = MightAndMagic7Ruleset.Instance.Save(session);
        KnowledgeNoteSave recorded = Assert.Single(document.Knowledge.Notes);
        Assert.Equal("find", recorded.Kind);
        Assert.Equal("search", recorded.Source);
        Assert.Equal("500", recorded.Subject);
        Assert.Equal("1", recorded.Place);
        Assert.True(recorded.ElapsedMilliseconds >= 0 && recorded.ElapsedMilliseconds <= document.Clock.ElapsedMilliseconds);
        Assert.DoesNotContain("1168", recorded.Text, StringComparison.Ordinal);

        // A detection spell reports over the places and the population the world holds, and it teaches the
        // party nothing durable: what it shows is state the world already keeps — the places it has been to
        // are its own, and who is standing here now is a moment — so no note is written for it. It is cast
        // after the save because it is a timed effect like a ward, and the automap suite proves what it marks
        // and why a save while one runs is refused by name.
        session.Update(ProductTestContext.Update(
            5,
            1,
            ProductTestContext.Payload("""{"action":"party.cast","member":0,"spell":"12","target":""}""")));
        Assert.Equal("detection", ProjectedNode.Of(ui.Latest().Value).Field("magic").Field("effect").AsString());
        Assert.Equal(1, Book(Notes(ui), "notes").Field("rows").Length());
    }

    [Fact]
    public void What_the_party_knows_survives_a_place_reset_and_reads_as_the_day_it_was_learned()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = Content();

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Use = UseControls });
        session.Start();

        // The party searches the gilded chest on its first day and learns the artifact exists.
        Arrive(session, ui, "1");
        session.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        string learned = Book(Notes(ui), "notes").Field("rows").Item(0).Field("label").AsString();
        Assert.Equal("Found The Ruby of Ultimate Power", learned);
        string day = Today(ui);

        // Two days pass, which is longer than the first place's own interval, so the world restores its
        // population through its own path: the place it was learned in is not the place it was when the party
        // searched it, and what the party knows about it is exactly as it was.
        session.Update(ProductTestContext.Update(2, admittedSteps: 2 * 2880, stepSeconds: 1.0));
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PlaceState state = live.World!.Places.StateOf(new PlaceId("1"));
        Assert.True(state.RespawnCount >= 1, "the place's population was restored by the clock");
        Assert.False(state.Cleared);
        Assert.Equal(learned, Book(Notes(ui), "notes").Field("rows").Item(0).Field("label").AsString());
        Assert.NotEqual(day, Today(ui));

        // A resumed session reads every note back against the clock it was composed with, so the note still
        // reads as the day it was learned rather than the day it was loaded, and the party plays on with it.
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Single(written.Knowledge.Notes);

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            ProductTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume, Use = UseControls });
        resumed.Start();

        ProjectedNode resumedNotes = Book(Notes(resumedUi), "notes");
        Assert.Equal("1 note", resumedNotes.Field("state").AsString());
        Assert.Equal(learned, resumedNotes.Field("rows").Item(0).Field("label").AsString());
        Assert.StartsWith(day, resumedNotes.Field("rows").Item(0).Field("detail").AsString(), StringComparison.Ordinal);
        Assert.NotEqual(day, Today(resumedUi));
    }

    [Fact]
    public void A_party_that_has_just_been_created_can_mix_and_keeps_what_it_learns_beside_it()
    {
        // The product's own play path is the creation screen, so the workflow a recipe is learned through has
        // to exist for a party that has just come into being and not only for one a scenario fixes: the same
        // one admitted update composes the casting workflow and the mixing workflow when creation is
        // accepted, and the knowledge owner that a learned recipe is reported to is composed with them.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [.. CreationContent(), .. ProductTestContext.CreationTables()]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, creation: true));
        session.Start();
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("creation").Field("active").AsBoolean());

        session.Update(ProductTestContext.Update(
            1,
            1,
            ProductTestContext.Payload("""{"action":"creation.accept"}""")));
        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("alchemy").Field("available").AsBoolean());
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("journal").Field("books").Item(1).Field("available").AsBoolean());
    }

    /// <summary>
    /// The content a created party needs: one place to start in and the shipped mixture rows, with the
    /// classes and skills the creation tables already declare.
    /// </summary>
    /// <remarks>
    /// The creation tables declare every class and skill this game's creation offers, so this pack declares
    /// neither: a second declaration of one skill would be a document and an entry twice rather than two
    /// readings of one table.
    /// </remarks>
    private static (string Path, string Text)[] CreationContent() =>
    [
        // The creation tables are named because this content is used by a case that creates its party: the
        // selection is what loads, so the pack declaring the classes and skills creation offers has to be in it.
        ProductTestContext.Bundle("partyrpg-default", "creation", "creation-tables"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/creation/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "creation",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "creation-places", "definitionKind": "place" },
                { "path": "potions.json", "documentId": "creation-potions", "definitionKind": "potion" },
                { "path": "start.json", "documentId": "creation-start", "definitionKind": "scenario-start" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/creation/places.json",
            """
            {
              "documentId": "creation-places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Alchemist's Shop", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/creation/potions.json",
            """
            {
              "documentId": "creation-potions",
              "definitionKind": "potion",
              "entries": [
                { "id": "222", "name": "Cure Wounds", "description": "Red Potion", "effect": "Heal 10+skill HP", "kind": "potion", "units": [1, 0, 0], "tier": 0, "mixtures": { "222": "none", "223": "226" }, "notes": { "223": 58 } },
                { "id": "223", "name": "Magic Potion", "description": "Blue Potion", "effect": "Restore 10+skill MP", "kind": "potion", "units": [0, 1, 0], "tier": 0, "mixtures": { "222": "226", "223": "none" } },
                { "id": "226", "name": "Cure Poison", "description": "Purple Potion", "effect": "Remove Poison 1,2,3 cond", "kind": "potion", "units": [1, 0, 1], "tier": 1, "mixtures": { "222": "none", "223": "none" } }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/creation/start.json",
            """
            { "documentId": "creation-start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
    ];

    /// <summary>Reads this game's mixture table over this suite's own content, as the session does.</summary>
    private static (MightAndMagic7Alchemy Alchemy, ContentCatalog Catalog) ReadAlchemy()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(Content());
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
        MightAndMagic7Alchemy alchemy = MightAndMagic7Alchemy.Read(catalog)
            ?? throw new InvalidOperationException("The content declares mixtures, so reading them must produce a table.");
        return (alchemy, catalog);
    }

    /// <summary>
    /// This game's loot over this suite's content, which is the one reading of which item rows are artifacts.
    /// </summary>
    private static MightAndMagic7Loot Loot(ContentCatalog catalog) =>
        MightAndMagic7Loot.Compose(catalog, random: null);

    /// <summary>The party this suite's scenario declares, read the way the session reads it.</summary>
    private static PartyEntity Party()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(Content());
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
        return MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The content declares a scenario party, so composing one must produce it.");
    }

    /// <summary>The pack's own first instance of a definition, which the scenario put there.</summary>
    private static ItemInstance First(PartyEntity party, ItemDefinitionId definition) =>
        party.Items.First(item => item.Definition == definition);

    /// <summary>The session's one clock, at this game's own rate and on its own calendar.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>The journal block of the newest projection.</summary>
    private static ProjectedNode Notes(RecordingUiService ui) =>
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

    /// <summary>The day the projection says it is, which is what a note written now has to read as.</summary>
    private static string Today(RecordingUiService ui) =>
        ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("date").AsString();

    /// <summary>Puts the party in a place the way a journey would, and lets the update read it.</summary>
    private static void Arrive(IGameSession session, RecordingUiService ui, string place)
    {
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId(place), PlacePose.Origin);
        session.Update(ProductTestContext.Update(1000ul + ulong.Parse(place, CultureInfo.InvariantCulture), 1));
        Assert.Equal(place, ProjectedNode.Of(ui.Latest().Value).Field("world").Field("place").AsString());
    }

    /// <summary>
    /// The content this suite reads: three places of this game's own shape — a chest holding an artifact, a
    /// chest holding an ordinary sword, and a landmark raising a map event — the shipped item and potion rows,
    /// and a party that knows the detection spell and has the two potions to mix.
    /// </summary>
    /// <remarks>
    /// The ids are the shipped ones because this game's own readings are keyed by them: the artifact is the
    /// first row the shipped table spawns as one (<c>OpenEnroth src/Engine/Objects/ItemEnums.h:956-977</c>),
    /// the two potions and the one they make are <c>POTION.TXT</c>'s own rows, and the mixture cell and the
    /// discovery number are that table's and its notes table's own — a red potion mixed with a blue one makes
    /// a purple one and records the discovery the shipped row states.
    /// </remarks>
    private static (string Path, string Text)[] Content() =>
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
                { "path": "potions.json", "documentId": "potions", "definitionKind": "potion" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
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
                { "id": "1", "kind": "region", "name": "Erathia", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "chest-1", "kind": "container", "name": "a gilded chest", "x": 100, "y": 0, "z": 0,
                      "contents": [ { "item": 500 } ] } ] },
                { "id": "2", "kind": "interior", "name": "A cellar", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "chest-2", "kind": "container", "name": "a plain chest", "x": 100, "y": 0, "z": 0,
                      "contents": [ { "item": 1 } ] } ] },
                { "id": "3", "kind": "interior", "name": "The old well", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "fount-1", "kind": "decoration", "name": "fount1", "descriptionId": 3, "eventId": 150,
                      "x": 100, "y": 0, "z": 0, "flags": 0, "cog": 0, "triggerRange": 0, "eventVarId": 0 } ] }
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
                { "id": "500", "name": "The Ruby of Ultimate Power", "material": "Artifact", "value": 5000 },
                { "id": "222", "name": "Cure Wounds", "material": "Bottle", "value": 5 },
                { "id": "223", "name": "Magic Potion", "material": "Bottle", "value": 5 },
                { "id": "226", "name": "Cure Poison", "material": "Bottle", "value": 5 }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/potions.json",
            """
            {
              "documentId": "potions",
              "definitionKind": "potion",
              "entries": [
                { "id": "222", "name": "Cure Wounds", "description": "Red Potion", "effect": "Heal 10+skill HP", "kind": "potion", "units": [1, 0, 0], "tier": 0, "mixtures": { "222": "none", "223": "226" }, "notes": { "223": 58 } },
                { "id": "223", "name": "Magic Potion", "description": "Blue Potion", "effect": "Restore 10+skill MP", "kind": "potion", "units": [0, 1, 0], "tier": 0, "mixtures": { "222": "226", "223": "none" } },
                { "id": "226", "name": "Cure Poison", "description": "Purple Potion", "effect": "Remove Poison 1,2,3 cond", "kind": "potion", "units": [1, 0, 1], "tier": 1, "mixtures": { "222": "none", "223": "none" } }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [ { "id": "12", "school": "Air", "level": 1, "name": "Wizard Eye", "resist": "0" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Air" }, { "id": "Alchemy" } ]
            }
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
                {
                  "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "222", "count": 2 }, { "item": "223", "count": 2 } ],
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 5, "hitPoints": 30,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 40 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 20 },
                                      { "id": "Accuracy", "value": 20 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Air", "level": 3, "tier": 1, "pointsSpent": 3 },
                                  { "id": "Alchemy", "level": 3, "tier": 1, "pointsSpent": 3 } ],
                      "spells": [ "12" ], "conditions": [] }
                  ]
                }
              ]
            }
            """),
    ];
}
