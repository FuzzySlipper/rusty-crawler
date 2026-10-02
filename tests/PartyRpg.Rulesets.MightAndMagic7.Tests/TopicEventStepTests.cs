using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The topic-event steps a fresh party used to stop at besides hirelings and counted deeds, each interpreted through the
/// one run and the owner that keeps what it names — over global events staged as the importer writes them.
/// </summary>
public sealed class TopicEventStepTests
{
    [Fact]
    public void A_bank_step_adds_to_the_one_balance_takes_only_what_it_holds_and_is_compared_as_the_run_left_it()
    {
        (MightAndMagic7Interaction rule, PartyEntity party) = Compose(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "add", "variable": "bank-gold", "value": 5000 },
              { "step": 1, "op": "compare", "variable": "bank-gold", "value": 5000, "target": 3 },
              { "step": 2, "op": "exit" },
              { "step": 3, "op": "subtract", "variable": "bank-gold", "value": 9000 },
              { "step": 4, "op": "subtract", "variable": "bank-gold", "value": 1000 } ] }
            """);
        using (party)
        {
            InteractionOutcome outcome = PromoterTopicTests.Answer(rule, party, "topic-1");
            Assert.Equal(string.Empty, outcome.Residue);
            Assert.Equal(4000, party.Holdings.BalanceOf(MightAndMagic7Services.BankHolding));
            Assert.Equal(100, party.Purse.Coins);
        }
    }

    [Fact]
    public void A_reputation_step_moves_the_one_standing_through_the_donors_sign_and_is_compared_the_same_way()
    {
        (MightAndMagic7Interaction rule, PartyEntity party) = Compose(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "subtract", "variable": "reputation", "value": 5 },
              { "step": 1, "op": "compare", "variable": "reputation", "value": -5, "target": 3 },
              { "step": 2, "op": "exit" },
              { "step": 3, "op": "add", "variable": "reputation", "value": 12 } ] }
            """,
            """
            { "id": "2", "event": 2, "topic": true, "steps": [
              { "step": 0, "op": "set", "variable": "reputation", "value": -20000 } ] }
            """);
        using (party)
        {
            // The donor's figure is sign-flipped, so its "subtract five" is the party's standing rising five, its
            // comparison of at least minus five holds at five, and its "add twelve" lowers the standing to minus seven.
            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
            Assert.Equal(-7, party.Reputation.Reputation);

            // A set stays within the donor's bound of ten thousand.
            PromoterTopicTests.Answer(rule, party, "topic-2");
            Assert.Equal(MightAndMagic7Standing.EventStepLimit, party.Reputation.Reputation);
        }
    }

    [Fact]
    public void A_food_step_finds_eats_and_states_the_larder_and_is_compared_against_it()
    {
        (MightAndMagic7Interaction rule, PartyEntity party) = Compose(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "compare", "variable": "food", "value": 3, "target": 3 },
              { "step": 1, "op": "add", "variable": "food", "value": 5 },
              { "step": 2, "op": "exit" },
              { "step": 3, "op": "subtract", "variable": "food", "value": 4 },
              { "step": 4, "op": "set", "variable": "food", "value": 0 } ] }
            """);
        using (party)
        {
            // Two portions are under three, so the run finds five.
            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
            Assert.Equal(7, party.Food.Portions);

            // Seven are at least three, so the run eats four and then states the larder empty.
            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
            Assert.Equal(0, party.Food.Portions);
        }
    }

    [Fact]
    public void An_item_step_gives_a_person_something_to_carry_that_a_thief_lifts_first_and_takes_it_back()
    {
        (MightAndMagic7Interaction rule, PartyEntity party) = Compose(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "npc-set-item", "person": 7, "item": 631, "on": true } ] }
            """,
            """
            { "id": "2", "event": 2, "topic": true, "steps": [
              { "step": 0, "op": "npc-set-item", "person": 7, "item": 631, "on": false } ] }
            """,
            """
            { "id": "3", "event": 3, "topic": true, "steps": [
              { "step": 0, "op": "add", "variable": "gold", "value": 10 },
              { "step": 1, "op": "npc-set-item", "person": 99, "item": 631, "on": true } ] }
            """);
        using (party)
        {
            PlacementDefinition hatter = PromoterTopicTests.Standing("npc-7");
            Assert.Null(MightAndMagic7Theft.Handed(party, hatter));

            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
            Assert.Equal([631], MightAndMagic7PersonState.Carried(party.Records, "npc-7", []));
            Assert.Equal(("npc-7", 631), MightAndMagic7Theft.Handed(party, hatter));

            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-2").Residue);
            Assert.Empty(MightAndMagic7PersonState.Carried(party.Records, "npc-7", []));

            // A person the people table does not hold is refused by name, before the coin the run would find is found.
            InteractionOutcome unknown = PromoterTopicTests.Answer(rule, party, "topic-3");
            Assert.Contains("person 99", unknown.Residue, StringComparison.Ordinal);
            Assert.Equal(100, party.Purse.Coins);
        }
    }

    [Fact]
    public void An_item_step_takes_the_item_a_persons_own_record_starts_them_with_and_gives_it_back()
    {
        (MightAndMagic7Interaction rule, PartyEntity party, MightAndMagic7Fixtures fixtures) = ComposeWithFixtures(
            person => person == "npc-7" ? [43] : [],
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "npc-set-item", "person": 7, "item": 43, "on": false } ] }
            """,
            """
            { "id": "2", "event": 2, "topic": true, "steps": [
              { "step": 0, "op": "npc-set-item", "person": 7, "item": 43, "on": true } ] }
            """);
        using (party)
        {
            // The placement the map's own record stands carries the item it starts the person with, and a thief's hand
            // reaches it before any record says anything.
            PlacementDefinition carrier = Carrying("npc-7", 43);
            Assert.Equal([43], MightAndMagic7PersonState.Starting(carrier));
            Assert.Equal(("npc-7", 43), MightAndMagic7Theft.Handed(party, carrier));

            // A take the donor runs over every creature standing for the person empties the starting item, which the
            // party's records keep as taken (OpenEnroth src/Engine/Objects/Actor.cpp:157-158).
            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-1").Residue);
            Assert.Null(MightAndMagic7Theft.Handed(party, carrier));
            Assert.True(party.Records.Has($"{MightAndMagic7PersonState.TakenPrefix}npc-7:43"));
            Assert.Null(fixtures.JudgeRecord($"{MightAndMagic7PersonState.TakenPrefix}npc-7:43", 1));

            // Giving it back takes the record of the take off rather than giving a second one.
            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-2").Residue);
            Assert.Equal([43], MightAndMagic7PersonState.Carried(party.Records, "npc-7", MightAndMagic7PersonState.Starting(carrier)));
            Assert.False(party.Records.Has($"{MightAndMagic7PersonState.TakenPrefix}npc-7:43"));
            Assert.False(party.Records.Has($"{MightAndMagic7PersonState.ItemPrefix}npc-7:43"));

            // A save that says an item the person never started with was taken from them is one no writer could leave.
            Assert.Contains("no map record starts 'npc-7' with item 44", fixtures.JudgeRecord($"{MightAndMagic7PersonState.TakenPrefix}npc-7:44", 1), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_hiding_flag_step_keeps_a_groups_hidden_state_which_stands_its_creatures_or_holds_them_off_the_field()
    {
        (MightAndMagic7Interaction rule, PartyEntity party) = Compose(
            """
            { "id": "1", "event": 1, "topic": true, "steps": [
              { "step": 0, "op": "toggle-actor-group-flag", "group": 33, "flag": 65536, "on": true },
              { "step": 1, "op": "toggle-actor-group-flag", "group": 34, "flag": 65536, "on": false },
              { "step": 2, "op": "toggle-actor-group-flag", "group": 0, "flag": 65536, "on": true } ] }
            """);
        using (party)
        {
            InteractionOutcome outcome = PromoterTopicTests.Answer(rule, party, "topic-1");
            Assert.Equal(string.Empty, outcome.Residue);
            Assert.Equal(1, outcome.Kept[MightAndMagic7Fixtures.HiddenGroupKey(33)]);
            Assert.Equal(0, outcome.Kept[MightAndMagic7Fixtures.HiddenGroupKey(34)]);
            Assert.Equal(2, outcome.Kept.Count);

            // What the place keeps is what its population reads: a creature of the hidden group does not stand, a record
            // the level holds hidden in the shown group does, and one in a group nothing touched keeps its record's word.
            PlaceId place = new("2");
            Dictionary<string, long> kept = new(outcome.Kept, StringComparer.Ordinal);
            MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(null, random: null, _ => kept);
            Assert.False(spawns.Stands(place, Actor(group: 33, hidden: false)));
            Assert.True(spawns.Stands(place, Actor(group: 34, hidden: true)));
            Assert.False(spawns.Stands(place, Actor(group: 35, hidden: true)));
            Assert.True(spawns.Stands(place, Actor(group: 35, hidden: false)));

            // A record held hidden in no group is one nothing can show, and stands as nothing at all.
            Assert.Empty(MightAndMagic7Spawns.Stand(Record(group: 0, hidden: true)));
            Assert.Single(MightAndMagic7Spawns.Stand(Record(group: 34, hidden: true)));

            // A save carrying the value is judged as one a place's events could have left.
            MightAndMagic7Fixtures judge = new(MightAndMagic7MapEvents.None);
            Assert.Null(judge.Judge(place, MightAndMagic7Fixtures.HiddenGroupKey(33), 1, 0));
            Assert.NotNull(judge.Judge(place, MightAndMagic7Fixtures.HiddenGroupKey(33), 2, 0));
            Assert.NotNull(judge.Judge(place, MightAndMagic7Fixtures.HiddenGroupKey(0), 1, 0));
        }
    }

    [Fact]
    public void A_person_a_maps_own_record_holds_hidden_does_not_stand_until_their_group_is_shown()
    {
        PlaceId place = new("21");
        Dictionary<string, long> kept = new(StringComparer.Ordinal);
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(null, random: null, _ => kept);
        Assert.False(spawns.Stands(place, Person(group: 0, hidden: true)));
        Assert.True(spawns.Stands(place, Person(group: 0, hidden: false)));

        // Hidden in a group, the person stands once the place's events show the group again.
        PlacementDefinition grouped = Person(group: 34, hidden: true);
        Assert.False(spawns.Stands(place, grouped));
        kept[MightAndMagic7Fixtures.HiddenGroupKey(34)] = 0;
        Assert.True(spawns.Stands(place, grouped));
    }

    [ImportedFact("people.json")]
    public void The_one_person_a_maps_own_record_holds_hidden_is_nobody_to_speak_with()
    {
        // Over the operator's install the one person record the level holds hidden is Castle Harmondale's first actor,
        // which the donor keeps Disabled: it is placed, does not stand, and answers nobody.
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(catalog, random: null);
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, MightAndMagic7Services.Read(catalog), stands: spawns.Stands)!;
        PlaceId castle = new("21");
        PlacementDefinition[] people = [.. PlacePopulationContent.Read(MightAndMagic7World.Graph(catalog), spawns).PlacementsOf(castle)
            .Where(placement => placement.Content.Kind == MightAndMagic7Conversation.PersonPlacementKind)];
        PlacementDefinition hidden = Assert.Single(people, placement => !spawns.Stands(castle, placement));
        Assert.Equal("person-0", hidden.Content.Id);
        Assert.Null(conversation.Describe(new ConversationTargetRequest(castle, hidden)));
        Assert.All(people.Where(placement => placement != hidden), placement => Assert.NotNull(conversation.Describe(new ConversationTargetRequest(castle, placement))));
    }

    private static PlacementDefinition Person(int group, bool hidden)
    {
        string marked = hidden ? ", \"hidden\": true" : string.Empty;
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "person-0", "kind": "person", "sourceField": "actors", "sourceIndex": 0, "x": 0, "y": 0, "z": 0, "group": {{group}}{{marked}}, "people": [ "npc-56" ] }""");
        return new PlacementDefinition(new PlacementContentId("person", "person-0"), "actors", 0, PlacePose.Origin, new ContentEntry("person-0", JsonDocument.Parse(json).RootElement.Clone()));
    }

    /// <summary>A rule running the given global events, with one person the people table holds, and a party.</summary>
    private static (MightAndMagic7Interaction Rule, PartyEntity Party) Compose(params string[] globalEvents)
    {
        (MightAndMagic7Interaction rule, PartyEntity party, _) = ComposeWithFixtures(_ => [], globalEvents);
        return (rule, party);
    }

    /// <summary>A person placement whose own record starts the person with an item, as the importer writes it.</summary>
    internal static PlacementDefinition Carrying(string person, int item)
    {
        string json = string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "person-0", "kind": "person", "sourceField": "actors", "sourceIndex": 0, "x": 0, "y": 0, "z": 0, "carriedItem": {{item}}, "people": [ {{JsonSerializer.Serialize(person)}} ] }""");
        return new PlacementDefinition(new PlacementContentId("person", "person-0"), "actors", 0, PlacePose.Origin, new ContentEntry("person-0", JsonDocument.Parse(json).RootElement.Clone()));
    }

    private static (MightAndMagic7Interaction Rule, PartyEntity Party, MightAndMagic7Fixtures Fixtures) ComposeWithFixtures(
        Func<string, IReadOnlyList<int>> starting,
        params string[] globalEvents)
    {
        ContentCatalog catalog = PromoterTopicTests.Catalog(globalEvents);
        PartyEntity party = PromoterTopicTests.Party(("Lasse", "Thief", 1));
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
        MightAndMagic7Fixtures fixtures = new(
            MightAndMagic7MapEvents.Read(catalog),
            progression: () => progression,
            people: id => id == "npc-7" ? new ConversationPerson("npc-7", "The Hatter") : null,
            topics: topic => new SpokenTopic(topic, "Topic", string.Empty, int.Parse(topic["topic-".Length..], CultureInfo.InvariantCulture)),
            starting: starting);
        return (new MightAndMagic7Interaction(fixtures: fixtures), party, fixtures);
    }

    private static JsonElement Record(int group, bool hidden) =>
        JsonDocument.Parse(string.Create(
            CultureInfo.InvariantCulture,
            $$"""{ "id": "actor-{{group}}", "kind": "actor", "sourceField": "actors", "sourceIndex": {{group}}, "x": 0, "y": 0, "z": 0, "monster": "1", "group": {{group}}{{(hidden ? ", \"hidden\": true" : string.Empty)}} }""")).RootElement.Clone();

    private static PlacementDefinition Actor(int group, bool hidden) => MightAndMagic7Spawns.Stand(Record(group, hidden)) switch
    {
        [var creature] => creature,
        _ => throw new InvalidOperationException("The record stands as nothing."),
    };
}
