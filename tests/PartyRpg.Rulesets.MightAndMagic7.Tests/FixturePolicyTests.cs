using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;
using Xunit.Abstractions;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// What this game's fixtures do: the steps of the map events a well, an obelisk and a sign raise, run against
/// the party's own owners, and every step this game does not interpret refused by name.
/// </summary>
/// <remarks>
/// <para>
/// The staged content is written the way the importer writes a place's fixtures and events — the placement a
/// face group becomes, the event's steps in the donor's own instruction words, the discovery rows by number —
/// and the events are the first two regions' own, step for step as the operator's install states them: the
/// first region's healing well and its daily refill (events 112 and 111 of <c>out01.evt</c>), its luck well
/// and that well's monthly refill (event 114), its town sign (event 1), and the second region's obelisk
/// (event 220 of <c>out02.evt</c>). The first region holds no obelisk — its discovery rows for obelisks begin at
/// the second region — so the obelisk is the second region's.
/// </para>
/// <para>
/// The imported case runs the same against the operator's own packs, and every fixture event the install
/// carries once with a fresh party, so the split between what this game interprets and what it refuses is
/// counted over the shipped programs rather than asserted over a sample.
/// </para>
/// </remarks>
public sealed class FixturePolicyTests(ITestOutputHelper output)
{
    private static readonly PlaceId EmeraldIsle = new("1");

    private static readonly PlaceId Harmondale = new("2");

    [Fact]
    public void A_healing_well_gives_its_points_writes_its_note_and_spends_a_charge_its_timer_refills()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(hitPoints: new ResourcePool(20, 40));
        PartyKnowledge knowledge = new(new MightAndMagic7Knowledge(), clock);
        MightAndMagic7Interaction rule = Rule(catalog, () => knowledge);
        PlacementDefinition well = Fixture(110 + 2, "Drink from the Well", "Well_E");

        // The well's charges are a map variable its daily timer sets to thirty, and the timer runs the first
        // time anybody uses the well — the donor's own reading of a timer on a map the party has not visited
        // — so the first drink finds thirty and leaves twenty-nine. What the event gives lands on the active
        // character, which this build reads as the first member able to act.
        (InteractionOutcome first, string state) = Use(rule, well, EmeraldIsle, party, clock);
        Assert.True(first.IsApplied, first.Refusal?.Message);
        Assert.Equal("+5 Hit points restored.", first.Message);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
        Assert.StartsWith("used v0=29 ", state, StringComparison.Ordinal);

        // The note the event writes is the shipped discovery row's own, and the knowledge owner keeps it as what
        // a landmark gives, in this game's words around the row's.
        KnowledgeReport taught = Assert.Single(first.Learned);
        Assert.Equal(KnowledgeKind.Effect, taught.Kind);
        Assert.Equal("discovery:3", taught.Subject);
        Assert.True(knowledge.Record(taught));
        Assert.Equal("Learned 5 Hit Points regained from the well east of the Temple on Emerald Island.", knowledge.Notes[0].Text);

        // A second drink spends a second charge; the note it reports again is the same fact.
        (InteractionOutcome second, state) = Use(rule, well, EmeraldIsle, party, clock, state);
        Assert.True(second.IsApplied);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.StartsWith("used v0=28 ", state, StringComparison.Ordinal);
        Assert.False(knowledge.Record(Assert.Single(second.Learned)));

        // A well whose charges are spent says what the event says and gives nothing.
        (InteractionOutcome dry, string dryState) = Use(rule, well, EmeraldIsle, party, clock, ReplaceVariable(state, "v0=0"));
        Assert.True(dry.IsApplied);
        Assert.Equal("Refreshing!", dry.Message);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.Empty(dry.Learned);
        Assert.StartsWith("used v0=0 ", dryState, StringComparison.Ordinal);

        // A day later the timer has run again, so the dry well is full and the drink spends one of thirty.
        clock.Advance(GameDuration.FromHours(25));
        (InteractionOutcome refilled, string refilledState) = Use(rule, well, EmeraldIsle, party, clock, dryState);
        Assert.Equal("+5 Hit points restored.", refilled.Message);
        Assert.StartsWith("used v0=29 ", refilledState, StringComparison.Ordinal);
    }

    [Fact]
    public void The_luck_well_raises_luck_for_good_until_luck_reaches_its_limit()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(luck: 13);
        MightAndMagic7Interaction rule = Rule(catalog);
        PlacementDefinition well = Fixture(114, "Drink from the Well", "Well_E");

        // The event's own monthly timer sets its eight charges, and a character whose luck is below fifteen
        // gains two points of it for good: the attribute itself changes, not a running effect.
        (InteractionOutcome raised, string state) = Use(rule, well, EmeraldIsle, party, clock);
        Assert.Equal("+2 Luck permanent", raised.Message);
        Assert.True(party.Members[0].Attributes.TryGet(new AttributeId("Luck"), out int luck));
        Assert.Equal(15, luck);
        Assert.StartsWith("used v2=7 ", state, StringComparison.Ordinal);

        // At fifteen the well has nothing more to give that character, and no charge is spent.
        (InteractionOutcome refused, string after) = Use(rule, well, EmeraldIsle, party, clock, state);
        Assert.Equal("Refreshing!", refused.Message);
        Assert.True(party.Members[0].Attributes.TryGet(new AttributeId("Luck"), out luck));
        Assert.Equal(15, luck);
        Assert.StartsWith("used v2=7 ", after, StringComparison.Ordinal);
    }

    [Fact]
    public void The_lucky_well_pays_a_poor_and_lucky_party_once_a_week_and_three_times_in_all()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(luck: 15);
        MightAndMagic7Interaction rule = Rule(catalog);
        PlacementDefinition well = Fixture(115, "Drink from the Well", "Well_E");

        // A party with no more than two hundred gold and luck of fifteen finds a thousand, through the outcome
        // the kit credits to the purse; the well's weekly bit and its count of three are its own state.
        (InteractionOutcome paid, string state) = Use(rule, well, EmeraldIsle, party, clock);
        Assert.Equal("Drink from the Well: the party finds 1000 gold.", paid.Message);
        Assert.Equal(1000, party.Purse.Coins);
        Assert.StartsWith("used v3=1 v4=1 ", state, StringComparison.Ordinal);

        // The same week it is only water.
        (InteractionOutcome again, state) = Use(rule, well, EmeraldIsle, party, clock, state);
        Assert.Equal("Refreshing!", again.Message);
        Assert.Equal(1000, party.Purse.Coins);

        // A week later the bit is cleared, and a party that has spent its gold is paid again.
        clock.Advance(GameDuration.FromHours((24 * 7) + 1));
        Assert.True(party.Purse.TryDebit(1000));
        (InteractionOutcome week, _) = Use(rule, well, EmeraldIsle, party, clock, state);
        Assert.Equal(1000, party.Purse.Coins);
        Assert.True(week.IsApplied);
    }

    [Fact]
    public void An_obelisk_shows_its_message_writes_its_clue_and_marks_the_bit_that_quiets_it()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        PartyKnowledge knowledge = new(new MightAndMagic7Knowledge(), clock);
        MightAndMagic7Interaction rule = Rule(catalog, () => knowledge);
        PlacementDefinition obelisk = Fixture(220, "Obelisk", "obelisk 3");

        // The obelisk prints its fragment, writes the discovery row that holds the whole message, and sets the
        // quest bit that its first step checks — the errand record the conversation already reads a quest bit
        // as.
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(Harmondale, obelisk, string.Empty))!;
        Assert.Equal("Obelisk", target.Name);
        Assert.Equal(InteractionVerb.Pull, target.Verb);
        (InteractionOutcome read, string state) = Use(rule, obelisk, Harmondale, party, clock);
        Assert.True(read.IsApplied, read.Refusal?.Message);
        Assert.Equal("pohuwwba", read.Message);
        Assert.True(party.Records.Has("errand:164"));
        KnowledgeReport clue = Assert.Single(read.Learned);
        Assert.Equal(KnowledgeKind.Clue, clue.Kind);
        Assert.True(knowledge.Record(clue));
        Assert.Equal("Read Obelisk message #1: pohuwwba", knowledge.Notes[0].Text);

        // Once the bit is set the event ends at its first step: nothing is printed and nothing is taught again.
        (InteractionOutcome again, _) = Use(rule, obelisk, Harmondale, party, clock, state);
        Assert.True(again.IsApplied);
        Assert.Equal("Obelisk: nothing comes of it.", again.Message);
        Assert.Empty(again.Learned);
    }

    [Fact]
    public void A_sign_is_read_and_its_words_are_kept_as_a_clue()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Interaction rule = Rule(catalog);
        PlacementDefinition sign = Fixture(1, "Welcome to Emerald Isle", "TownSign_S");

        // A sign's event is its hint and an exit; what the party does with it is read it. The original keeps
        // nothing of a sign, and this game keeps its words as a clue — ours, stated in the ruleset README.
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(EmeraldIsle, sign, string.Empty))!;
        Assert.Equal(InteractionVerb.Read, target.Verb);
        (InteractionOutcome read, string state) = Use(rule, sign, EmeraldIsle, party, clock);
        Assert.Equal("The sign reads: \"Welcome to Emerald Isle\".", read.Message);
        Assert.Equal("read", state);
        KnowledgeReport clue = Assert.Single(read.Learned);
        Assert.Equal(KnowledgeKind.Clue, clue.Kind);
        Assert.Equal("sign:1.1", clue.Subject);
        Assert.Equal("1", clue.Place);
    }

    [Fact]
    public void A_run_that_reaches_a_step_or_a_variable_this_game_does_not_interpret_changes_nothing()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(hitPoints: new ResourcePool(20, 40));
        MightAndMagic7Interaction rule = Rule(catalog);

        // The lever restores hit points and then moves a door, which this game does not interpret: the run is
        // refused at the door's step, by name, and the hit points it reached first are not given.
        (InteractionOutcome lever, _) = Use(rule, Fixture(300, "Lever", string.Empty), EmeraldIsle, party, clock);
        Assert.False(lever.IsApplied);
        Assert.Equal(MightAndMagic7Codes.FixtureStepNotInterpreted, lever.Refusal!.Code);
        Assert.Contains("change-door-state", lever.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal(20, party.Members[0].Resources.HitPoints.Current);

        // A temporary might bonus is a variable no reading of an attribute adds yet, so it is refused by name too.
        (InteractionOutcome bonus, _) = Use(rule, Fixture(301, "Drink from the Fountain", string.Empty), EmeraldIsle, party, clock);
        Assert.Equal(MightAndMagic7Codes.FixtureVariableNotInterpreted, bonus.Refusal!.Code);
        Assert.Contains("attribute-bonus might", bonus.Refusal.Message, StringComparison.Ordinal);
        Assert.Empty(party.Records.All);

        // An event the content does not carry is a refusal naming it, not a fixture that silently does nothing.
        (InteractionOutcome missing, _) = Use(rule, Fixture(999, "Nothing", string.Empty), EmeraldIsle, party, clock);
        Assert.Equal(MightAndMagic7Codes.FixtureEventMissing, missing.Refusal!.Code);
    }

    [Fact]
    public void A_session_drinks_from_the_fire_well_and_the_resistance_and_the_note_are_the_party_s()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(SessionContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));

        // The town well gives the active character fifty points of fire resistance for a while and writes its
        // note; the resistance is the running effect a ward leaves, so the fight reads it where it reads a ward.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode interaction = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("+50 Fire Resistance temporary.", interaction.Field("message").AsString());
        ProjectedNode notes = Book(ProjectedNode.Of(ui.Latest().Value).Field("journal"), "notes");
        Assert.Equal(
            "Learned 50 points of temporary Fire resistance from the central town well on Emerald Island.",
            notes.Field("rows").Item(0).Field("label").AsString());

        // The second drink reads the resistance the first one left, which is how the event knows it has
        // already given it.
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("Refreshing!", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString());
        Assert.Equal(1, Book(ProjectedNode.Of(ui.Latest().Value).Field("journal"), "notes").Field("rows").Length());
    }

    [ImportedFact("place-events.json")]
    public void The_operators_own_well_obelisk_and_sign_do_what_their_events_say()
    {
        ContentCatalog catalog = ImportedContent.Load();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues.Select(issue => issue.ToString())));
        PlacePopulationContent population = PlacePopulationContent.Read(MightAndMagic7World.Graph(catalog));
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(hitPoints: new ResourcePool(20, 40));
        PartyKnowledge knowledge = new(new MightAndMagic7Knowledge(), clock);
        MightAndMagic7Interaction rule = Rule(catalog, () => knowledge);

        // The first region's healing well, east of its temple.
        (InteractionOutcome well, _) = Use(rule, Placed(population, EmeraldIsle, "fixture-112"), EmeraldIsle, party, clock);
        Assert.True(well.IsApplied, well.Refusal?.Message);
        Assert.Equal("+5 Hit points restored.", well.Message);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
        Assert.True(knowledge.Record(Assert.Single(well.Learned)));

        // Its town sign.
        PlacementDefinition signPlacement = Placed(population, EmeraldIsle, "fixture-1");
        Assert.Equal(InteractionVerb.Read, rule.Describe(new InteractionTargetRequest(EmeraldIsle, signPlacement, string.Empty))!.Verb);
        (InteractionOutcome sign, _) = Use(rule, signPlacement, EmeraldIsle, party, clock);
        Assert.Equal("The sign reads: \"Welcome to Emerald Isle\".", sign.Message);
        Assert.True(knowledge.Record(Assert.Single(sign.Learned)));

        // The second region's obelisk, which is the first of the fourteen.
        (InteractionOutcome obelisk, _) = Use(rule, Placed(population, Harmondale, "fixture-220"), Harmondale, party, clock);
        Assert.True(obelisk.IsApplied, obelisk.Refusal?.Message);
        Assert.True(knowledge.Record(Assert.Single(obelisk.Learned)));
        string[] expected =
        [
            "Learned 5 Hit Points regained from the well east of the Temple on Emerald Island.",
            "Read \"Welcome to Emerald Isle\"",
            "Read Obelisk message #1: pohuwwba",
        ];
        Assert.Equal(expected, knowledge.Notes.Select(note => note.Text));
    }

    [ImportedFact("place-events.json")]
    public void Every_shipped_fixture_runs_or_is_refused_by_a_named_step_and_the_split_is_counted()
    {
        // Every event a fixture of the operator's install raises is run once, by a fresh party on a fresh
        // fixture, and its answer is counted: applied, or refused with the code and the instruction or variable
        // that stopped it. Nothing else may come back — no other refusal and no throw — which is what makes
        // "every instruction this game does not interpret is refused by name" a counted fact.
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        GameClock clock = TestClock.Create(scale: 1);

        // The running effects a temporary resistance is left in are composed as a session composes them, over
        // the shipped spell table, so the wells that give one are counted as a session plays them.
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog, MightAndMagic7Skills.Read(catalog, MightAndMagic7Promotions.Read(catalog)))!;
        KeyedTestRandom random = new();
        int applied = 0;
        SortedDictionary<string, int> refused = new(StringComparer.Ordinal);
        foreach (MapEvent mapEvent in events.Events.Where(candidate => candidate.Raised))
        {
            // A session's running effects are kept over its one party, so each fresh party has its own.
            using PartyEntity party = Party();
            MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(events, effects: new MightAndMagic7SpellEffects(spells, clock), random: random));
            (InteractionOutcome outcome, _) = Use(rule, Fixture(mapEvent.Id, mapEvent.Label, string.Empty), mapEvent.Place, party, clock);
            if (outcome.IsApplied)
            {
                applied++;
                continue;
            }

            Assert.Contains(outcome.Refusal!.Code, new[] { MightAndMagic7Codes.FixtureStepNotInterpreted, MightAndMagic7Codes.FixtureVariableNotInterpreted });
            string reason = outcome.Refusal.Code == MightAndMagic7Codes.FixtureStepNotInterpreted
                ? Between(outcome.Refusal.Message, "a '", "' instruction")
                : Between(outcome.Refusal.Message, "the variable '", "'");
            refused[reason] = refused.GetValueOrDefault(reason) + 1;
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"applied {applied}"));
        foreach ((string reason, int count) in refused) output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"refused {reason}: {count}"));
        Assert.Equal(events.Events.Count(candidate => candidate.Raised), applied + refused.Values.Sum());
        // The figures the ruleset README states for the operator's install.
        Assert.Equal(327, applied);
        string[] stated =
        [
            "armour-class-bonus: 1", "attribute-bonus accuracy: 1", "attribute-bonus endurance: 1",
            "attribute-bonus might: 3", "attribute-bonus personality: 4", "bank-gold: 6", "cast-spell: 6",
            "change-door-state: 75", "character-animation: 2", "counter: 1", "give-item: 14", "gold: 1",
            "hireling: 2", "play-sound: 5", "resistance fire: 1", "resistance mind: 1", "resistance water: 1",
            "set-faces-bit: 4", "set-npc-topic: 1", "set-sprite: 7", "set-texture: 21", "skill-points: 1",
            "speak-npc: 5", "toggle-actor-group-flag: 3", "toggle-indoor-light: 1",
        ];
        Assert.Equal(stated, refused.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}: {entry.Value}")));
        Assert.Equal(168, refused.Values.Sum());
    }

    private static string Between(string text, string before, string after)
    {
        int start = text.IndexOf(before, StringComparison.Ordinal);
        if (start < 0) return text;
        start += before.Length;
        int end = text.IndexOf(after, start, StringComparison.Ordinal);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static MightAndMagic7Interaction Rule(ContentCatalog catalog, Func<PartyKnowledge?>? knowledge = null, IRandomService? random = null) =>
        new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog), knowledge, effects: null, random: random));

    /// <summary>Describes and uses a fixture as the interaction mechanism does, answering with the state it left.</summary>
    private static (InteractionOutcome Outcome, string State) Use(
        MightAndMagic7Interaction rule,
        PlacementDefinition placement,
        PlaceId place,
        PartyEntity party,
        GameClock clock,
        string recorded = "")
    {
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(place, placement, recorded))!;
        InteractionOutcome outcome = rule.Apply(target, new InteractionContext(place, placement, target, party, clock));
        if (outcome.IsApplied && outcome.Gain is { IsFree: false } gain) party.Purse.Credit(gain.Coins);
        return (outcome, outcome.IsApplied ? outcome.State : recorded);
    }

    private static string ReplaceVariable(string state, string variable) =>
        string.Join(' ', state.Split(' ').Select(token => token.StartsWith("v0=", StringComparison.Ordinal) ? variable : token));

    /// <summary>A fixture placement as the importer writes one.</summary>
    private static PlacementDefinition Fixture(int eventId, string name, string model)
    {
        string id = string.Create(CultureInfo.InvariantCulture, $"fixture-{eventId}");
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "{{id}}", "kind": "fixture", "sourceField": "events", "sourceIndex": {{eventId}}, "x": 100, "y": 0, "z": 0, "positionSource": "event-face-centroid", "eventId": {{eventId}}, "name": {{JsonSerializer.Serialize(name)}}, "faceCount": 1, "sourceModelName": {{JsonSerializer.Serialize(model)}} }""");
        return new PlacementDefinition(new PlacementContentId("fixture", id), "events", eventId, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));
    }

    private static PlacementDefinition Placed(PlacePopulationContent population, PlaceId place, string id) =>
        population.PlacementsOf(place).Single(placement => placement.Content.Id == id);

    /// <summary>A party of one, whose hit points and luck a case states.</summary>
    private static PartyEntity Party(ResourcePool? hitPoints = null, int luck = 13) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [new MemberCreation(new PartyMemberSeed(
                "Zoltan",
                new RaceId("Human"),
                new ClassId("Knight"),
                [
                    new AttributeScore(new AttributeId("Might"), 15),
                    new AttributeScore(new AttributeId("Intellect"), 10),
                    new AttributeScore(new AttributeId("Personality"), 10),
                    new AttributeScore(new AttributeId("Endurance"), 15),
                    new AttributeScore(new AttributeId("Accuracy"), 12),
                    new AttributeScore(new AttributeId("Speed"), 12),
                    new AttributeScore(new AttributeId("Luck"), luck),
                ],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: hitPoints ?? ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0)))],
            coins: 0,
            foodPortions: 2,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));

    private static ProjectedNode Book(ProjectedNode journal, string kind)
    {
        ProjectedNode books = journal.Field("books");
        for (int position = 0; position < books.Length(); position++)
        {
            if (string.Equals(books.Item(position).Field("kind").AsString(), kind, StringComparison.Ordinal)) return books.Item(position);
        }

        throw new KeyNotFoundException($"The projection carries no book '{kind}'.");
    }

    /// <summary>The staged events: the first two regions' own, step for step, and two this game refuses.</summary>
    private const string Events =
        """
        {
          "documentId": "events",
          "definitionKind": "place-event",
          "entries": [
            { "id": "1.1", "place": "1", "event": 1, "label": "Welcome to Emerald Isle", "raised": true,
              "steps": [ { "step": 0, "op": "exit" } ] },
            { "id": "1.110", "place": "1", "event": 110, "label": "Drink from the Well", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "resistance-bonus", "which": "fire", "value": 50, "target": 5 },
                { "step": 1, "op": "set", "variable": "resistance-bonus", "which": "fire", "value": 50 },
                { "step": 2, "op": "status-text", "textId": 22, "text": "+50 Fire Resistance temporary." },
                { "step": 3, "op": "add", "variable": "autonote", "value": 2 },
                { "step": 4, "op": "jump", "target": 6 },
                { "step": 5, "op": "status-text", "textId": 11, "text": "Refreshing!" },
                { "step": 6, "op": "exit" } ] },
            { "id": "1.111", "place": "1", "event": 111, "raised": false, "timed": true,
              "steps": [
                { "step": 0, "op": "on-long-timer", "period": "daily", "hour": 0, "minute": 0 },
                { "step": 1, "op": "set", "variable": "map-variable", "index": 0, "value": 30 },
                { "step": 2, "op": "set", "variable": "map-variable", "index": 1, "value": 30 } ] },
            { "id": "1.112", "place": "1", "event": 112, "label": "Drink from the Well", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "map-variable", "index": 0, "value": 1, "target": 3 },
                { "step": 1, "op": "status-text", "textId": 11, "text": "Refreshing!" },
                { "step": 2, "op": "jump", "target": 7 },
                { "step": 3, "op": "subtract", "variable": "map-variable", "index": 0, "value": 1 },
                { "step": 4, "op": "add", "variable": "hit-points", "value": 5 },
                { "step": 5, "op": "add", "variable": "autonote", "value": 3 },
                { "step": 6, "op": "status-text", "textId": 23, "text": "+5 Hit points restored." },
                { "step": 7, "op": "exit" } ] },
            { "id": "1.114", "place": "1", "event": 114, "label": "Drink from the Well", "raised": true, "timed": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "attribute", "which": "luck", "value": 15, "target": 2 },
                { "step": 1, "op": "compare", "variable": "map-variable", "index": 2, "value": 1, "target": 4 },
                { "step": 2, "op": "status-text", "textId": 11, "text": "Refreshing!" },
                { "step": 3, "op": "exit" },
                { "step": 4, "op": "subtract", "variable": "map-variable", "index": 2, "value": 1 },
                { "step": 5, "op": "add", "variable": "attribute", "which": "luck", "value": 2 },
                { "step": 6, "op": "status-text", "textId": 25, "text": "+2 Luck permanent" },
                { "step": 7, "op": "exit" },
                { "step": 8, "op": "on-long-timer", "period": "monthly" },
                { "step": 9, "op": "set", "variable": "map-variable", "index": 2, "value": 8 } ] },
            { "id": "1.115", "place": "1", "event": 115, "label": "Drink from the Well", "raised": true, "timed": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "map-variable", "index": 4, "value": 3, "target": 9 },
                { "step": 1, "op": "compare", "variable": "map-variable", "index": 3, "value": 1, "target": 9 },
                { "step": 2, "op": "compare", "variable": "gold", "value": 201, "target": 9 },
                { "step": 3, "op": "compare", "variable": "attribute", "which": "luck", "value": 15, "target": 5 },
                { "step": 4, "op": "jump", "target": 9 },
                { "step": 5, "op": "add", "variable": "map-variable", "index": 3, "value": 1 },
                { "step": 6, "op": "add", "variable": "gold", "value": 1000 },
                { "step": 7, "op": "add", "variable": "map-variable", "index": 4, "value": 1 },
                { "step": 8, "op": "jump", "target": 10 },
                { "step": 9, "op": "status-text", "textId": 11, "text": "Refreshing!" },
                { "step": 10, "op": "exit" },
                { "step": 11, "op": "on-long-timer", "period": "weekly" },
                { "step": 12, "op": "set", "variable": "map-variable", "index": 3, "value": 0 } ] },
            { "id": "1.300", "place": "1", "event": 300, "label": "Lever", "raised": true,
              "steps": [
                { "step": 0, "op": "add", "variable": "hit-points", "value": 5 },
                { "step": 1, "op": "change-door-state" },
                { "step": 2, "op": "exit" } ] },
            { "id": "1.301", "place": "1", "event": 301, "label": "Drink from the Fountain", "raised": true,
              "steps": [
                { "step": 0, "op": "set", "variable": "member-bit", "value": 1 },
                { "step": 1, "op": "add", "variable": "attribute-bonus", "which": "might", "value": 10 },
                { "step": 2, "op": "exit" } ] },
            { "id": "2.220", "place": "2", "event": 220, "label": "Obelisk", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "quest-bit", "value": 164, "target": 5 },
                { "step": 1, "op": "status-text", "textId": 51, "text": "pohuwwba" },
                { "step": 2, "op": "add", "variable": "autonote", "value": 114 },
                { "step": 3, "op": "for-party-member", "who": "party" },
                { "step": 4, "op": "add", "variable": "quest-bit", "value": 164 },
                { "step": 5, "op": "exit" } ] }
          ]
        }
        """;

    /// <summary>The staged discovery rows, the install's own.</summary>
    private const string Discoveries =
        """
        {
          "documentId": "discoveries",
          "definitionKind": "discovery",
          "entries": [
            { "id": "2", "text": "50 points of temporary Fire resistance from the central town well on Emerald Island.", "category": "stat" },
            { "id": "3", "text": "5 Hit Points regained from the well east of the Temple on Emerald Island.", "category": "stat" },
            { "id": "114", "text": "Obelisk message #1: pohuwwba", "category": "obelisk" }
          ]
        }
        """;

    private const string Places =
        """
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "fixture-110", "kind": "fixture", "sourceField": "events", "sourceIndex": 110, "x": 100, "y": 0, "z": 0,
                  "positionSource": "event-face-centroid", "eventId": 110, "name": "Drink from the Well", "faceCount": 1, "sourceModel": 51, "sourceModelName": "Well_E" } ] },
            { "id": "2", "kind": "region", "name": "Harmondale", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
          ]
        }
        """;

    /// <summary>The staged content as one pack, for the cases that read it without a session.</summary>
    private static ContentCatalog Catalog() =>
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
                        { "path": "discoveries.json", "documentId": "discoveries", "definitionKind": "discovery" }
                      ]
                    }
                    """)
                .Add("packs/world/places.json", Places)
                .Add("packs/world/events.json", Events)
                .Add("packs/world/discoveries.json", Discoveries),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

    /// <summary>
    /// The same content as a session plays it: the places, events and notes, a spell table so the session keeps
    /// running effects, and a party standing at the first region's town well.
    /// </summary>
    private static (string Path, string Text)[] SessionContent() =>
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
                { "path": "events.json", "documentId": "events", "definitionKind": "place-event" },
                { "path": "discoveries.json", "documentId": "discoveries", "definitionKind": "discovery" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", Places),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/events.json", Events),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/discoveries.json", Discoveries),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            { "documentId": "spells", "definitionKind": "spell", "entries": [ { "id": "12", "school": "Air", "level": 1, "name": "Wizard Eye", "resist": "0" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            { "documentId": "skills", "definitionKind": "skill", "entries": [ { "id": "Air" } ] }
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
              "entries": [
                {
                  "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Zoltan", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 15 }, { "id": "Luck", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
    ];
}
