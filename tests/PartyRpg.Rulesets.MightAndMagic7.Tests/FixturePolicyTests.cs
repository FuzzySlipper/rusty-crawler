using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using PartyRpg.Kit;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
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
public sealed partial class FixturePolicyTests(ITestOutputHelper output)
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
        InteractionLedger ledger = new();

        // The well's charges are a map variable its daily timer sets to thirty, and the timer runs the first
        // time anybody uses the well — the donor's own reading of a timer on a map the party has not visited
        // — so the first drink finds thirty and leaves twenty-nine. What the event gives lands on the active
        // character, which this build reads as the first member able to act.
        (InteractionOutcome first, string state) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.True(first.IsApplied, first.Refusal?.Message);
        Assert.Equal("+5 Hit points restored.", first.Message);
        Assert.Equal(25, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal("used", state);
        Assert.Equal(29, Variable(ledger, EmeraldIsle, 0));

        // The note the event writes is the shipped discovery row's own, and the knowledge owner keeps it as what
        // a landmark gives, in this game's words around the row's.
        KnowledgeReport taught = Assert.Single(first.Learned);
        Assert.Equal(KnowledgeKind.Effect, taught.Kind);
        Assert.Equal("discovery:3", taught.Subject);
        Assert.True(knowledge.Record(taught));
        Assert.Equal("Learned 5 Hit Points regained from the well east of the Temple on Emerald Island.", knowledge.Notes[0].Text);

        // A second drink spends a second charge; the note it reports again is the same fact.
        (InteractionOutcome second, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.True(second.IsApplied);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(28, Variable(ledger, EmeraldIsle, 0));
        Assert.False(knowledge.Record(Assert.Single(second.Learned)));

        // A well whose charges are spent says what the event says and gives nothing.
        ledger.Keep(EmeraldIsle, new Dictionary<string, long> { ["map-variable:0"] = 0 });
        (InteractionOutcome dry, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.True(dry.IsApplied);
        Assert.Equal("Refreshing!", dry.Message);
        Assert.Equal(30, party.Members[0].Resources.HitPoints.Current);
        Assert.Empty(dry.Learned);
        Assert.Equal(0, Variable(ledger, EmeraldIsle, 0));

        // A day later the timer has run again, so the dry well is full and the drink spends one of thirty.
        clock.Advance(GameDuration.FromHours(25));
        (InteractionOutcome refilled, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.Equal("+5 Hit points restored.", refilled.Message);
        Assert.Equal(29, Variable(ledger, EmeraldIsle, 0));
    }

    [Fact]
    public void The_luck_well_raises_luck_for_good_until_luck_reaches_its_limit()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(luck: 13);
        MightAndMagic7Interaction rule = Rule(catalog);
        PlacementDefinition well = Fixture(114, "Drink from the Well", "Well_E");
        InteractionLedger ledger = new();

        // The event's own monthly timer sets its eight charges, and a character whose luck is below fifteen
        // gains two points of it for good: the attribute itself changes, not a running effect.
        (InteractionOutcome raised, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.Equal("+2 Luck permanent", raised.Message);
        Assert.True(party.Members[0].Attributes.TryGet(new AttributeId("Luck"), out int luck));
        Assert.Equal(15, luck);
        Assert.Equal(7, Variable(ledger, EmeraldIsle, 2));

        // At fifteen the well has nothing more to give that character, and no charge is spent.
        (InteractionOutcome refused, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.Equal("Refreshing!", refused.Message);
        Assert.True(party.Members[0].Attributes.TryGet(new AttributeId("Luck"), out luck));
        Assert.Equal(15, luck);
        Assert.Equal(7, Variable(ledger, EmeraldIsle, 2));
    }

    [Fact]
    public void The_lucky_well_pays_a_poor_and_lucky_party_once_a_week_and_three_times_in_all()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(luck: 15);
        MightAndMagic7Interaction rule = Rule(catalog);
        PlacementDefinition well = Fixture(115, "Drink from the Well", "Well_E");
        InteractionLedger ledger = new();

        // A party with no more than two hundred gold and luck of fifteen finds a thousand, through the outcome
        // the kit credits to the purse; the well's weekly bit and its count of three are the place's map variables.
        (InteractionOutcome paid, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.Equal("Drink from the Well: the party finds 1000 gold.", paid.Message);
        Assert.Equal(1000, party.Purse.Coins);
        Assert.Equal(1, Variable(ledger, EmeraldIsle, 3));
        Assert.Equal(1, Variable(ledger, EmeraldIsle, 4));

        // The same week it is only water.
        (InteractionOutcome again, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
        Assert.Equal("Refreshing!", again.Message);
        Assert.Equal(1000, party.Purse.Coins);

        // A week later the bit is cleared, and a party that has spent its gold is paid again.
        clock.Advance(GameDuration.FromHours((24 * 7) + 1));
        Assert.True(party.Purse.TryDebit(1000));
        (InteractionOutcome week, _) = Use(rule, well, EmeraldIsle, party, clock, ledger);
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
        InteractionLedger ledger = new();
        (InteractionOutcome read, _) = Use(rule, obelisk, Harmondale, party, clock, ledger);
        Assert.True(read.IsApplied, read.Refusal?.Message);
        Assert.Equal("pohuwwba", read.Message);
        Assert.True(party.Records.Has("errand:164"));
        KnowledgeReport clue = Assert.Single(read.Learned);
        Assert.Equal(KnowledgeKind.Clue, clue.Kind);
        Assert.True(knowledge.Record(clue));
        Assert.Equal("Read Obelisk message #1: pohuwwba", knowledge.Notes[0].Text);

        // Once the bit is set the event ends at its first step: nothing is printed and nothing is taught again.
        (InteractionOutcome again, _) = Use(rule, obelisk, Harmondale, party, clock, ledger);
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
    public void Two_levers_of_one_place_share_the_map_variable_that_counts_them()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Interaction rule = Rule(catalog);
        InteractionLedger ledger = new();
        PlacementDefinition west = Fixture(401, "Pull the Lever", string.Empty);
        PlacementDefinition east = Fixture(402, "Pull the Lever", string.Empty);

        // Each lever marks itself pulled in a variable of its own and adds one to the count both read, as the
        // donor's interior puzzles do: the map variables are the place's, so the second lever finds the count
        // the first one left and opens the gate.
        (InteractionOutcome first, _) = Use(rule, west, EmeraldIsle, party, clock, ledger);
        Assert.Equal("Click.", first.Message);
        Assert.Equal(1, Variable(ledger, EmeraldIsle, 5));

        // Pulling the same lever again changes nothing: its own variable says it is pulled.
        (InteractionOutcome again, _) = Use(rule, west, EmeraldIsle, party, clock, ledger);
        Assert.Equal("Pull the Lever: nothing comes of it.", again.Message);
        Assert.Equal(1, Variable(ledger, EmeraldIsle, 5));

        (InteractionOutcome second, _) = Use(rule, east, EmeraldIsle, party, clock, ledger);
        Assert.Equal("The gate grinds open.", second.Message);
        Assert.Equal(2, Variable(ledger, EmeraldIsle, 5));

        // Another place keeps its own variables: the same levers there start from nothing.
        Assert.Empty(ledger.ValuesOf(Harmondale));
    }

    [Fact]
    public void A_save_taken_after_drinking_from_a_well_keeps_its_spent_charges_and_its_timer()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = SessionContent(WellPlaces);
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));

        // Two drinks: the daily timer fills the well to thirty on the first, and each drink spends one.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("+5 Hit points restored.", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("message").AsString());
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        PlaceInteractionSnapshot kept = Assert.Single(written.World.Interaction.Places);
        Assert.Equal(EmeraldIsle, kept.Place);
        Assert.Contains(new PlaceValue("map-variable:0", 28), kept.Values);
        PlaceValue timer = Assert.Single(kept.Values, value => value.Key == "timer:111.0");
        Assert.InRange(timer.Value, 0, written.Clock.ElapsedMilliseconds);

        // The resumed session reads the same well: the timer is not due again, so the next drink spends the
        // twenty-ninth charge rather than finding the well full, and the timer's last run is the one saved.
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with
            {
                Start = SessionStart.Resume,
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
            });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1));
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("+5 Hit points restored.", ProjectedNode.Of(resumedUi.Latest().Value).Field("interaction").Field("message").AsString());
        PlaceInteractionSnapshot after = Assert.Single(MightAndMagic7Ruleset.Instance.Save(resumed).World.Interaction.Places);
        Assert.Contains(new PlaceValue("map-variable:0", 27), after.Values);
        Assert.Contains(timer, after.Values);
    }

    [Fact]
    public void A_save_naming_a_value_no_fixture_keeps_is_refused_by_name()
    {
        ContentCatalog catalog = Catalog();
        MightAndMagic7Fixtures fixtures = new(MightAndMagic7MapEvents.Read(catalog));

        // A slot past the place's seventy-five, a figure past a byte, a timer the place's events do not hold, a
        // timer that ran after the save's own clock, and a name no fixture writes are each contradictions.
        Assert.Null(fixtures.Judge(EmeraldIsle, "map-variable:74", 255, 0));
        Assert.Null(fixtures.Judge(EmeraldIsle, "timer:111.0", 10, 10));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "map-variable:75", 1, 0));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "map-variable:0", 256, 0));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "timer:112.0", 1, 10));
        Assert.NotNull(fixtures.Judge(Harmondale, "timer:111.0", 1, 10));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "timer:111.0", 11, 10));
        Assert.NotNull(fixtures.Judge(EmeraldIsle, "door:3", 1, 10));
    }

    [Fact]
    public void A_lever_moves_the_door_its_event_names_and_passes_over_what_only_a_player_would_see()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Interaction rule = Rule(catalog);
        InteractionLedger ledger = new();
        PlacementDefinition lever = Fixture(310, "Pull the Lever", string.Empty);
        PlacementDefinition gate = Door(7, stored: 2);
        PlacementDefinition[] placements = [lever, gate];

        // The texture, the sound and a face group hidden are presentation the product does not draw, so the run
        // passes over them; the toggle moves the closed gate open through the door owner's own word, recorded
        // under the gate's identity, and says the collision does not follow (#8594).
        (InteractionOutcome pulled, _) = Use(rule, lever, EmeraldIsle, party, clock, ledger, placements);
        Assert.True(pulled.IsApplied, pulled.Refusal?.Message);
        Assert.Equal("The gate moves.", pulled.Message);
        Assert.Equal(new InteractionTargetChange(gate.Content, MightAndMagic7Interaction.OpenState), Assert.Single(pulled.Changes));
        Assert.Equal(MightAndMagic7Fixtures.CollisionResidue, pulled.Residue);
        Assert.Equal(MightAndMagic7Interaction.OpenState, ledger.StateOf(EmeraldIsle, gate.Content).State);

        // The gate's own use reads what the lever left: it already stands open.
        InteractionTargetDefinition door = rule.Describe(new InteractionTargetRequest(EmeraldIsle, gate, ledger.StateOf(EmeraldIsle, gate.Content).State))!;
        Assert.Equal(MightAndMagic7Interaction.OpenState, door.State);

        // A second pull toggles the open gate shut again.
        (InteractionOutcome again, _) = Use(rule, lever, EmeraldIsle, party, clock, ledger, placements);
        Assert.Equal(new InteractionTargetChange(gate.Content, MightAndMagic7Interaction.ClosedState), Assert.Single(again.Changes));

        // A lever in a place that holds no such door moves nothing, which the donor's own lookup does too, and
        // says so.
        (InteractionOutcome nowhere, _) = Use(rule, lever, EmeraldIsle, party, clock, placements: [lever]);
        Assert.True(nowhere.IsApplied);
        Assert.Empty(nowhere.Changes);
        Assert.Contains("Door 7 is not one this place holds", nowhere.Residue, StringComparison.Ordinal);
    }

    [Fact]
    public void An_item_gift_is_the_named_item_through_the_acquisition_path_and_a_drawn_one_needs_a_loot_table()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        MightAndMagic7Interaction rule = Rule(catalog);

        (InteractionOutcome given, _) = Use(rule, Fixture(311, "Chest", string.Empty), EmeraldIsle, party, clock);
        Assert.True(given.IsApplied, given.Refusal?.Message);
        Assert.Equal(new ItemDefinitionId("401"), Assert.Single(given.Items).Definition);
        Assert.Equal(1, party.Inventory.TotalOf(new ItemDefinitionId("401")));

        // A gift drawn at a treasure level is the loot owner's draw; a product with no loot table and no random
        // service has nothing to draw it with, and says so rather than giving nothing.
        (InteractionOutcome drawn, _) = Use(rule, Fixture(315, "Bookcase", string.Empty), EmeraldIsle, party, clock);
        Assert.Equal(MightAndMagic7Codes.FixtureNothingToRoll, drawn.Refusal!.Code);
    }

    [Fact]
    public void An_altar_gives_a_permanent_resistance_and_skill_points_through_their_owners()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(MightAndMagic7MapEvents.Read(catalog), progression: () => progression));
        PlacementDefinition altar = Fixture(312, "Altar", string.Empty);

        // The stored base resistance is the member's own, kept and saved, and the base resistance the fight
        // reads adds it to the racial bonus; skill points are a gift through the one writer of them.
        (InteractionOutcome given, _) = Use(rule, altar, EmeraldIsle, party, clock);
        Assert.True(given.IsApplied, given.Refusal?.Message);
        PartyMember zoltan = party.Members[0];
        Assert.Equal(10, zoltan.Resistances.Of(MightAndMagic7Damage.Fire));
        Assert.Equal(10, MightAndMagic7BaseResistance.Of(zoltan, MightAndMagic7Damage.Fire));
        Assert.Equal(2, zoltan.Progression.SkillPoints);
        Assert.Contains(new ResistanceScore(MightAndMagic7Damage.Fire, 10), party.Capture().Members[0].Seed.Resistances);

        // The altar's first step reads the stored figure, so a character it already raised is not raised again.
        (InteractionOutcome again, _) = Use(rule, altar, EmeraldIsle, party, clock);
        Assert.True(again.IsApplied);
        Assert.Equal(10, zoltan.Resistances.Of(MightAndMagic7Damage.Fire));
        Assert.Equal(2, zoltan.Progression.SkillPoints);
    }

    [Fact]
    public void A_statue_calls_a_person_over_a_well_reads_the_bank_and_a_sundial_keeps_a_counter()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party();
        ConversationPerson guard = new("npc-5", "Guard");
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(
            MightAndMagic7MapEvents.Read(catalog),
            people: id => id == guard.Id ? guard : null));

        // A person the event calls over is handed to the conversation, the way using a person would be.
        (InteractionOutcome spoken, _) = Use(rule, Fixture(313, "Statue", string.Empty), EmeraldIsle, party, clock);
        Assert.True(spoken.IsApplied, spoken.Refusal?.Message);
        Assert.Equal(guard, spoken.Speaks!.First);

        // The well compares the party's one bank balance.
        PlacementDefinition well = Fixture(314, "Drink from the Well", string.Empty);
        Assert.Equal("Refreshing!", Use(rule, well, EmeraldIsle, party, clock).Outcome.Message);
        party.Holdings.Hold(MightAndMagic7Services.BankHolding, 100);
        Assert.True(Use(rule, well, EmeraldIsle, party, clock).Outcome.IsApplied);
        Assert.Equal(10, party.Purse.Coins);

        // A counter is set to now, and holds once the stated hours have passed since.
        InteractionLedger ledger = new();
        PlacementDefinition sundial = Fixture(316, "Sundial", string.Empty);
        Assert.Equal("The shadow is marked.", Use(rule, sundial, EmeraldIsle, party, clock, ledger).Outcome.Message);
        Assert.Equal("The shadow is marked.", Use(rule, sundial, EmeraldIsle, party, clock, ledger).Outcome.Message);
        clock.Advance(GameDuration.FromHours(25));
        Assert.Equal("A day has passed.", Use(rule, sundial, EmeraldIsle, party, clock, ledger).Outcome.Message);
    }

    [Fact]
    public void A_session_s_shrine_raises_might_and_armour_for_a_while_through_the_running_effects_the_fight_reads()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(SessionContent(OneFixture(317, "Shrine of Might")));
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));

        PartyMember zoltan = ((MightAndMagic7Session)session).Party!.Members[0];
        Assert.Equal(5, zoltan.Effects.MagnitudeOf(SpellEffectIds.Attribute(new AttributeId("Might"))));
        Assert.Equal(20, zoltan.Effects.MagnitudeOf(SpellEffectIds.Armour));
    }

    [Fact]
    public void A_permanent_resistance_and_skill_points_an_altar_gave_survive_a_save()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = SessionContent(OneFixture(312, "Altar"));
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        MightAndMagic7Ruleset.Instance.Save(session);

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume });
        resumed.Start();
        PartyMember zoltan = ((MightAndMagic7Session)resumed).Party!.Members[0];
        Assert.Equal(10, zoltan.Resistances.Of(MightAndMagic7Damage.Fire));
        Assert.Equal(2, zoltan.Progression.SkillPoints);
    }

    [ImportedFact("place-events.json")]
    public void A_travel_event_takes_the_move_its_branches_reach_and_only_when_the_condition_holds()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        KeyedTestRandom random = new();
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(events, random: random));
        GameClock clock = TestClock.Create(scale: 1);
        PlaceId giants = new("12");

        // Harmondale's shrine to the Land of the Giants compares quest bit 246 (OUT02.EVT event 221): a party
        // without it reads the shrine's line and stays, and nothing leads it anywhere.
        using PartyEntity party = Party();
        (InteractionOutcome closed, _) = Use(rule, Fixture(221, "Shrine", string.Empty), Harmondale, party, clock, transitions: graph.TransitionsFrom(Harmondale));
        Assert.True(closed.IsApplied, closed.Refusal?.Message);
        Assert.Null(closed.Travels);

        // The Giants' side of the shrine sets the bit and leads to Harmondale (OUT12.EVT event 452, link 174).
        (InteractionOutcome back, _) = Use(rule, Fixture(452, "Shrine", string.Empty), giants, party, clock, transitions: graph.TransitionsFrom(giants));
        Assert.True(back.IsApplied, back.Refusal?.Message);
        Assert.Equal("174", back.Travels?.Transition.Source);
        Assert.Equal(Harmondale, back.Travels?.Transition.To);
        Assert.Equal(TransitionKind.Walking, back.Travels?.Kind);
        Assert.True(party.Records.Has(MightAndMagic7Quests.ErrandRecord("246")));

        // With the bit held, Harmondale's shrine takes its move — link 139, a walk between two regions.
        (InteractionOutcome open, _) = Use(rule, Fixture(221, "Shrine", string.Empty), Harmondale, party, clock, transitions: graph.TransitionsFrom(Harmondale));
        Assert.Equal("139", open.Travels?.Transition.Source);
        Assert.Equal(giants, open.Travels?.Transition.To);

        // A barrow's door compares map variable 0 and leads to one barrow or the other (MDK01.EVT event 502).
        PlaceId barrow = new("53");
        InteractionLedger ledger = new();
        (InteractionOutcome first, _) = Use(rule, Fixture(502, "Door", string.Empty), barrow, party, clock, ledger, transitions: graph.TransitionsFrom(barrow));
        Assert.Equal("72", first.Travels?.Transition.Source);
        ledger.Keep(barrow, new Dictionary<string, long> { [MightAndMagic7Fixtures.VariableKey(0)] = 2 });
        (InteractionOutcome second, _) = Use(rule, Fixture(502, "Door", string.Empty), barrow, party, clock, ledger, transitions: graph.TransitionsFrom(barrow));
        Assert.Equal("73", second.Travels?.Transition.Source);

        // A move whose link the place does not issue is refused by name before anything is settled.
        (InteractionOutcome unissued, _) = Use(rule, Fixture(221, "Shrine", string.Empty), Harmondale, party, clock);
        Assert.Equal(MightAndMagic7Codes.FixtureTravelUnknown, unissued.Refusal?.Code);
    }

    [ImportedFact("place-events.json")]
    public void A_floor_trigger_is_trodden_on_and_a_move_within_the_place_sets_the_party_down_there()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(events, random: new KeyedTestRandom()));
        GameClock clock = TestClock.Create(scale: 1);
        PlaceId celeste = new("7");

        // Celeste's edge (D25.EVT event 451) is a floor trigger: trodden on, never aimed at.
        PlacementDefinition edge = Fixture(451, "Edge", string.Empty, FloorTrigger);
        Assert.Equal(InteractionVerb.Tread, rule.Describe(new InteractionTargetRequest(celeste, edge, string.Empty))?.Verb);

        // Its random pick either names a drop to the desert or falls through a move within Celeste into the next
        // step's drop, as the donor's steps do: every run leads to the Bracada Desert.
        for (int draw = 0; draw < 12; draw++)
        {
            using PartyEntity party = Party();
            (InteractionOutcome fell, _) = Use(rule, edge, celeste, party, clock, transitions: graph.TransitionsFrom(celeste));
            Assert.True(fell.IsApplied, fell.Refusal?.Message);
            Assert.Equal(new PlaceId("6"), fell.Travels?.Transition.To);
        }

        // A teleport within a place sets the party down where it names and crosses nothing.
        MapEvent pad = events.Events.First(candidate =>
            candidate.Steps.Any(step => step.Op == "move-to-map" && step.WithinPlace) &&
            candidate.Steps.All(step => step.Op is "move-to-map" or "exit" or "play-sound") &&
            candidate.Steps.First(step => step.Op == "move-to-map").Position != (0, 0, 0));
        using PartyEntity walker = Party();
        (InteractionOutcome moved, _) = Use(rule, Fixture(pad.Id, pad.Label, string.Empty, pad.Stepped && !pad.Raised ? FloorTrigger : "fixture"), pad.Place, walker, clock, transitions: graph.TransitionsFrom(pad.Place));
        Assert.True(moved.IsApplied, moved.Refusal?.Message);
        Assert.Null(moved.Travels);
        MapEventStep teleport = pad.Steps.First(step => step.Op == "move-to-map");
        Assert.Equal(new InteractionRelocation(teleport.Position.X, teleport.Position.Y, teleport.Position.Z, teleport.Yaw == -1 ? null : teleport.Yaw), moved.Relocates);
    }

    [ImportedFact("place-events.json")]
    public void The_operators_fire_trap_casts_its_spell_at_the_party_and_a_bookcase_draws_a_scroll()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7MapEvents events = MightAndMagic7MapEvents.Read(catalog);
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog, MightAndMagic7Skills.Read(catalog, MightAndMagic7Promotions.Read(catalog)))!;
        KeyedTestRandom random = new();
        MightAndMagic7Loot loot = MightAndMagic7Loot.Compose(catalog, random);
        MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(events, random: random, loot: loot, spells: spells));
        GameClock clock = TestClock.Create(scale: 1);

        // The second region's fire trap picks one of its casts and lands it on the active character.
        using PartyEntity party = Party(hitPoints: ResourcePool.Full(400));
        (InteractionOutcome trap, _) = Use(rule, Fixture(231, "Fire", string.Empty), Harmondale, party, clock);
        Assert.True(trap.IsApplied, trap.Refusal?.Message);
        Assert.Contains("strikes Zoltan", trap.Message, StringComparison.Ordinal);
        Assert.True(party.Members[0].Resources.HitPoints.Current < 400);

        // Every gift drawn at a treasure level that runs gives a spell scroll from the shipped table.
        int drawn = 0;
        foreach (MapEvent gift in events.Events.Where(candidate => candidate.Raised && candidate.Steps.Any(step => step.Op == "give-item")))
        {
            using PartyEntity reader = Party();
            (InteractionOutcome outcome, _) = Use(rule, Fixture(gift.Id, gift.Label, string.Empty), gift.Place, reader, clock);
            if (!outcome.IsApplied) continue;
            drawn += outcome.Items.Count;
        }

        Assert.True(drawn > 0, "no shipped gift drew an item");
    }

    [Fact]
    public void A_run_that_reaches_a_step_or_a_variable_this_game_does_not_interpret_changes_nothing()
    {
        ContentCatalog catalog = Catalog();
        GameClock clock = TestClock.Create(scale: 1);
        using PartyEntity party = Party(hitPoints: new ResourcePool(20, 40));
        MightAndMagic7Interaction rule = Rule(catalog);

        // The lever restores hit points and then changes a person's greeting, which this game does not interpret:
        // the run is refused at that step, by name, and the hit points it reached first are not given.
        (InteractionOutcome lever, _) = Use(rule, Fixture(300, "Lever", string.Empty), EmeraldIsle, party, clock);
        Assert.False(lever.IsApplied);
        Assert.Equal(MightAndMagic7Codes.FixtureStepNotInterpreted, lever.Refusal!.Code);
        Assert.Contains("'set-npc-greeting' instruction", lever.Refusal.Message, StringComparison.Ordinal);
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
        // Every event a fixture or a plate of the operator's install raises is run once, by a fresh party on a fresh
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
        MightAndMagic7Loot loot = MightAndMagic7Loot.Compose(catalog, random);
        MightAndMagic7Conversation? conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog));

        // A count of the dead reads what each place holds as a session populates it — its creatures as the
        // encounters resolve them and the people its actor records stand — none of them down on a first visit.
        PlaceGraph graph = MightAndMagic7World.Graph(catalog);
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(catalog, random);
        PlacePopulationContent placed = PlacePopulationContent.Read(graph, spawns);

        // A summoning puts its creatures into the live population of the place the party stands in, so the place an
        // event belongs to is populated before it runs, as walking into it populates it in a session.
        using PlacePopulation live = new(graph, new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()), expansion: spawns);
        int applied = 0;
        int travelled = 0;
        int relocated = 0;
        int summoned = 0;
        SortedDictionary<string, int> refused = new(StringComparer.Ordinal);
        List<string> unrouted = [];

        // A floor trigger's event is run the same way: treading on a plate is a use of the trigger.
        foreach (MapEvent mapEvent in events.Events.Where(candidate => candidate.Raised || candidate.Stepped))
        {
            // A session's running effects are kept over its one party, so each fresh party has its own.
            using PartyEntity party = Party();
            live.Step(mapEvent.Place, []);
            int before = live.Entities.Count;
            PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
            PartyJournal journal = new(new MightAndMagic7Journal(loot), clock);
            MightAndMagic7Interaction rule = new(fixtures: new MightAndMagic7Fixtures(
                events,
                effects: new MightAndMagic7SpellEffects(spells, clock),
                random: random,
                loot: loot,
                spells: spells,
                progression: () => progression,
                people: person => conversation?.PersonOf(person),
                actors: place => [.. placed.PlacementsOf(place).Where(MightAndMagic7Fixtures.IsActor).Select(placement => new PlaceActor(placement, Down: false))],
                journal: () => journal,
                population: place => live.Place == place ? live : null));
            (InteractionOutcome outcome, _) = Use(rule, Fixture(mapEvent.Id, mapEvent.Label, string.Empty), mapEvent.Place, party, clock, transitions: graph.TransitionsFrom(mapEvent.Place));
            if (outcome.IsApplied)
            {
                applied++;
                summoned += live.Entities.Count - before;
                if (outcome.Travels is not null) travelled++;
                if (outcome.Relocates is not null) relocated++;
                continue;
            }

            Assert.Contains(outcome.Refusal!.Code, new[] { MightAndMagic7Codes.FixtureStepNotInterpreted, MightAndMagic7Codes.FixtureVariableNotInterpreted });

            string reason = outcome.Refusal.Code == MightAndMagic7Codes.FixtureStepNotInterpreted
                ? Between(outcome.Refusal.Message, "a '", "' instruction")
                : Between(outcome.Refusal.Message, "the variable '", "'");
            refused[reason] = refused.GetValueOrDefault(reason) + 1;

            // Every refusal left over names the task that would interpret it.
            if (!Regex.IsMatch(outcome.Refusal.Message, @"#\d{4}")) unrouted.Add($"{mapEvent.Place}.{mapEvent.Id}: {outcome.Refusal.Message}");
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"applied {applied}, travelled {travelled}, relocated {relocated}, summoned {summoned}"));
        foreach ((string reason, int count) in refused) output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"refused {reason}: {count}"));
        foreach (string missing in unrouted) output.WriteLine($"unrouted {missing}");
        Assert.Empty(unrouted);
        Assert.Equal(events.Events.Count(candidate => candidate.Raised || candidate.Stepped), applied + refused.Values.Sum());
        // The figures the ruleset README states for the operator's install: 810 events, 653 a fixture raises and 168 a
        // plate does (11 both).
        Assert.Equal(810, events.Events.Count(candidate => candidate.Raised || candidate.Stepped));
        Assert.Equal(808, applied);

        // Of those, a fresh party's use of a travel event takes it along a link 145 times — the rest stop at a
        // condition it does not meet — and sets it down elsewhere in its own place 54 times; and the three plates
        // that spring an ambush put 80 creatures on the field.
        Assert.Equal(145, travelled);
        Assert.Equal(54, relocated);
        Assert.Equal(80, summoned);
        string[] stated = ["hireling: 2"];
        Assert.Equal(stated, refused.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{entry.Key}: {entry.Value}")));
        Assert.Equal(2, refused.Values.Sum());
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

    /// <summary>
    /// Describes and uses a fixture as the interaction mechanism does — reading the place's kept values from the
    /// ledger, and writing what an applied use kept and its state word back into it — answering with the word.
    /// </summary>
    private static (InteractionOutcome Outcome, string State) Use(
        MightAndMagic7Interaction rule,
        PlacementDefinition placement,
        PlaceId place,
        PartyEntity party,
        GameClock clock,
        InteractionLedger? ledger = null,
        IReadOnlyList<PlacementDefinition>? placements = null,
        IReadOnlyList<PlaceTransition>? transitions = null)
    {
        InteractionLedger kept = ledger ?? new InteractionLedger();
        string recorded = kept.StateOf(place, placement.Content).State;
        InteractionTargetDefinition target = rule.Describe(new InteractionTargetRequest(place, placement, recorded))!;
        InteractionOutcome outcome = rule.Apply(target, new InteractionContext(place, placement, target, party, clock)
        {
            PlaceValues = kept.ValuesOf(place),
            PlaceTargets = placements ?? [placement],
            TargetState = content => kept.StateOf(place, content).State,
            PlaceTransitions = transitions ?? [],
        });
        if (!outcome.IsApplied) return (outcome, recorded);
        if (outcome.Gain is { IsFree: false } gain) party.Purse.Credit(gain.Coins);
        foreach (InteractionItemYield item in outcome.Items) Assert.True(party.AcquireItem(item.Definition, item.Count).Admitted);
        kept.Keep(place, outcome.Kept);
        foreach (InteractionTargetChange change in outcome.Changes) kept.Record(place, change.Target, change.State);
        return (outcome, kept.Record(place, placement.Content, outcome.State).State);
    }

    /// <summary>A door placement as the importer writes an interior's door slot.</summary>
    private static PlacementDefinition Door(int doorId, int stored)
    {
        string id = string.Create(CultureInfo.InvariantCulture, $"door-{doorId}");
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "{{id}}", "kind": "door", "sourceField": "doors", "sourceIndex": 0, "x": -900, "y": 0, "z": 0, "doorId": {{doorId}}, "state": {{stored}} }""");
        return new PlacementDefinition(new PlacementContentId("door", id), "doors", 0, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));
    }

    /// <summary>One of a place's map variables as the ledger keeps it; one nothing wrote is zero.</summary>
    private static long Variable(InteractionLedger ledger, PlaceId place, int slot) =>
        ledger.ValuesOf(place).GetValueOrDefault(string.Create(CultureInfo.InvariantCulture, $"map-variable:{slot}"));

    /// <summary>The placement kind the importer writes a floor trigger as.</summary>
    private const string FloorTrigger = "floor-trigger";

    /// <summary>A fixture placement as the importer writes one, or a floor trigger when the kind says so.</summary>
    private static PlacementDefinition Fixture(int eventId, string name, string model, string kind = "fixture")
    {
        string id = string.Create(CultureInfo.InvariantCulture, $"{(kind == FloorTrigger ? "trigger" : "fixture")}-{eventId}");
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "{{id}}", "kind": "{{kind}}", "sourceField": "events", "sourceIndex": {{eventId}}, "x": 100, "y": 0, "z": 0, "positionSource": "event-face-centroid", "eventId": {{eventId}}, "name": {{JsonSerializer.Serialize(name)}}, "faceCount": 1, "sourceModelName": {{JsonSerializer.Serialize(model)}} }""");
        return new PlacementDefinition(new PlacementContentId(kind, id), "events", eventId, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));
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
                { "step": 1, "op": "set-npc-greeting" },
                { "step": 2, "op": "exit" } ] },
            { "id": "1.310", "place": "1", "event": 310, "label": "Pull the Lever", "raised": true,
              "steps": [
                { "step": 0, "op": "set-texture" },
                { "step": 1, "op": "play-sound" },
                { "step": 2, "op": "set-faces-bit", "group": 3, "flag": 8192, "on": true },
                { "step": 3, "op": "change-door-state", "door": 7, "action": "toggle" },
                { "step": 4, "op": "status-text", "textId": 3, "text": "The gate moves." },
                { "step": 5, "op": "exit" } ] },
            { "id": "1.311", "place": "1", "event": 311, "label": "Chest", "raised": true,
              "steps": [
                { "step": 0, "op": "give-item", "level": 3, "itemKind": "", "itemSkill": "", "item": 401 },
                { "step": 1, "op": "exit" } ] },
            { "id": "1.312", "place": "1", "event": 312, "label": "Altar", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "resistance", "which": "fire", "value": 10, "target": 4 },
                { "step": 1, "op": "for-party-member", "who": "party" },
                { "step": 2, "op": "add", "variable": "resistance", "which": "fire", "value": 10 },
                { "step": 3, "op": "add", "variable": "skill-points", "value": 2 },
                { "step": 4, "op": "exit" } ] },
            { "id": "1.313", "place": "1", "event": 313, "label": "Statue", "raised": true,
              "steps": [
                { "step": 0, "op": "speak-npc", "person": 5 },
                { "step": 1, "op": "exit" } ] },
            { "id": "1.314", "place": "1", "event": 314, "label": "Drink from the Well", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "bank-gold", "value": 99, "target": 3 },
                { "step": 1, "op": "status-text", "textId": 11, "text": "Refreshing!" },
                { "step": 2, "op": "exit" },
                { "step": 3, "op": "add", "variable": "gold", "value": 10 },
                { "step": 4, "op": "exit" } ] },
            { "id": "1.315", "place": "1", "event": 315, "label": "Bookcase", "raised": true,
              "steps": [
                { "step": 0, "op": "give-item", "level": 5, "itemKind": "spell-scroll" },
                { "step": 1, "op": "exit" } ] },
            { "id": "1.316", "place": "1", "event": 316, "label": "Sundial", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "counter", "index": 3, "value": 24, "target": 4 },
                { "step": 1, "op": "set", "variable": "counter", "index": 3, "value": 0 },
                { "step": 2, "op": "status-text", "textId": 1, "text": "The shadow is marked." },
                { "step": 3, "op": "exit" },
                { "step": 4, "op": "status-text", "textId": 2, "text": "A day has passed." },
                { "step": 5, "op": "exit" } ] },
            { "id": "1.317", "place": "1", "event": 317, "label": "Shrine of Might", "raised": true,
              "steps": [
                { "step": 0, "op": "add", "variable": "attribute-bonus", "which": "might", "value": 5 },
                { "step": 1, "op": "add", "variable": "armour-class-bonus", "value": 20 },
                { "step": 2, "op": "exit" } ] },
            { "id": "1.301", "place": "1", "event": 301, "label": "Drink from the Fountain", "raised": true,
              "steps": [
                { "step": 0, "op": "set", "variable": "member-bit", "value": 1 },
                { "step": 1, "op": "add", "variable": "attribute-bonus", "which": "might", "value": 10 },
                { "step": 2, "op": "exit" } ] },
            { "id": "1.401", "place": "1", "event": 401, "label": "Pull the Lever", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "map-variable", "index": 6, "value": 1, "target": 7 },
                { "step": 1, "op": "set", "variable": "map-variable", "index": 6, "value": 1 },
                { "step": 2, "op": "add", "variable": "map-variable", "index": 5, "value": 1 },
                { "step": 3, "op": "compare", "variable": "map-variable", "index": 5, "value": 2, "target": 6 },
                { "step": 4, "op": "status-text", "textId": 1, "text": "Click." },
                { "step": 5, "op": "exit" },
                { "step": 6, "op": "status-text", "textId": 2, "text": "The gate grinds open." },
                { "step": 7, "op": "exit" } ] },
            { "id": "1.402", "place": "1", "event": 402, "label": "Pull the Lever", "raised": true,
              "steps": [
                { "step": 0, "op": "compare", "variable": "map-variable", "index": 7, "value": 1, "target": 7 },
                { "step": 1, "op": "set", "variable": "map-variable", "index": 7, "value": 1 },
                { "step": 2, "op": "add", "variable": "map-variable", "index": 5, "value": 1 },
                { "step": 3, "op": "compare", "variable": "map-variable", "index": 5, "value": 2, "target": 6 },
                { "step": 4, "op": "status-text", "textId": 1, "text": "Click." },
                { "step": 5, "op": "exit" },
                { "step": 6, "op": "status-text", "textId": 2, "text": "The gate grinds open." },
                { "step": 7, "op": "exit" } ] },
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

    /// <summary>The two regions, with one fixture raising the given staged event standing where the party starts.</summary>
    private static string OneFixture(int eventId, string name) =>
        $$"""
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "fixture-{{eventId}}", "kind": "fixture", "sourceField": "events", "sourceIndex": {{eventId}}, "x": 100, "y": 0, "z": 0,
                  "positionSource": "event-face-centroid", "eventId": {{eventId}}, "name": "{{name}}", "faceCount": 1, "sourceModel": 60, "sourceModelName": "Altar" } ] },
            { "id": "2", "kind": "region", "name": "Harmondale", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
          ]
        }
        """;

    /// <summary>The first region with its healing well east of the temple standing where the party starts.</summary>
    private const string WellPlaces =
        """
        {
          "documentId": "places",
          "definitionKind": "place",
          "entries": [
            { "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 1,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "fixture-112", "kind": "fixture", "sourceField": "events", "sourceIndex": 112, "x": 100, "y": 0, "z": 0,
                  "positionSource": "event-face-centroid", "eventId": 112, "name": "Drink from the Well", "faceCount": 1, "sourceModel": 52, "sourceModelName": "Well_E" } ] },
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
    private static (string Path, string Text)[] SessionContent(string places = Places) =>
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
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", places),
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
