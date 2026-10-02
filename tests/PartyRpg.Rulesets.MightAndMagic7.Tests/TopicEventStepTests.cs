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
            Assert.Equal([631], MightAndMagic7PersonState.Carried(party.Records, "npc-7"));
            Assert.Equal(("npc-7", 631), MightAndMagic7Theft.Handed(party, hatter));

            Assert.Equal(string.Empty, PromoterTopicTests.Answer(rule, party, "topic-2").Residue);
            Assert.Empty(MightAndMagic7PersonState.Carried(party.Records, "npc-7"));

            // A person the people table does not hold is refused by name, before the coin the run would find is found.
            InteractionOutcome unknown = PromoterTopicTests.Answer(rule, party, "topic-3");
            Assert.Contains("person 99", unknown.Residue, StringComparison.Ordinal);
            Assert.Equal(100, party.Purse.Coins);
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

    /// <summary>A rule running the given global events, with one person the people table holds, and a party.</summary>
    private static (MightAndMagic7Interaction Rule, PartyEntity Party) Compose(params string[] globalEvents)
    {
        ContentCatalog catalog = PromoterTopicTests.Catalog(globalEvents);
        PartyEntity party = PromoterTopicTests.Party(("Lasse", "Thief", 1));
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party);
        MightAndMagic7Fixtures fixtures = new(
            MightAndMagic7MapEvents.Read(catalog),
            progression: () => progression,
            people: id => id == "npc-7" ? new ConversationPerson("npc-7", "The Hatter") : null,
            topics: topic => new SpokenTopic(topic, "Topic", string.Empty, int.Parse(topic["topic-".Length..], CultureInfo.InvariantCulture)));
        return (new MightAndMagic7Interaction(fixtures: fixtures), party);
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
