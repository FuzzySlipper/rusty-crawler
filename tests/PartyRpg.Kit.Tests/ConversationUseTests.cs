using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// A topic whose answer is a use: the conversation hands the party to something the ruleset runs over the speaker's
/// placement, and what the run said comes back as the person's answer.
/// </summary>
public sealed class ConversationUseTests
{
    private static readonly PlaceId Hall = new("1");

    [Fact]
    public void Only_an_answer_that_hands_to_a_use_may_leave_its_words_to_the_use()
    {
        ConversationAnswer spoken = new(string.Empty, handoff: new ConversationHandoff(HandoffOwner.Use, "topic-9"));
        Assert.Equal(string.Empty, spoken.Text);
        Assert.Equal("use:topic-9", spoken.Handoff!.ToString());

        Assert.Throws<ArgumentException>(() => new ConversationAnswer(string.Empty));
        Assert.Throws<ArgumentException>(() => new ConversationAnswer(" ", handoff: new ConversationHandoff(HandoffOwner.Counter)));
    }

    [Fact]
    public void A_use_answer_waits_for_its_words_and_hearing_them_puts_one_line_in_the_transcript_under_the_topic()
    {
        PartyConversations conversations = new(new UseRule());
        conversations.Open(Hall, Person(), new ConversationSubject("person-0", [new ConversationPerson("mira", "Mira")]));

        // Nothing waits for an answer yet, so words arriving are refused by name.
        Assert.Equal(ConversationCodes.ConversationTopicUnknown, conversations.Hear("Out of nowhere.").Code);

        // Choosing the topic hands to the use and puts nothing in the transcript: the run has not spoken yet.
        int lines = conversations.Said.Count;
        ConversationResult chosen = conversations.Choose("castle");
        Assert.True(chosen.IsApplied);
        Assert.Equal(HandoffOwner.Use, chosen.Handoff!.Owner);
        Assert.Equal(lines, conversations.Said.Count);

        // What the run said is the person's answer, once, under the topic that set it going.
        ConversationResult heard = conversations.Hear("The castle is yours.", "nothing else came of it");
        Assert.True(heard.IsApplied);
        Assert.Equal("Mira: The castle is yours.", heard.Message);
        ConversationLine line = conversations.Said[^1];
        Assert.Equal(("mira", "castle", "The castle is yours.", "nothing else came of it"), (line.Speaker, line.Topic, line.Text, line.Residue));
        Assert.Equal(ConversationCodes.ConversationTopicUnknown, conversations.Hear("Again?").Code);

        // A conversation closed while a use was pending forgets it: words that arrive afterwards answer nobody.
        conversations.Choose("castle");
        conversations.Close();
        Assert.Equal(ConversationCodes.ConversationNotOpen, conversations.Hear("Too late.").Code);
    }

    [Fact]
    public void A_use_takes_the_worlds_own_scripted_move_only_along_a_transition_the_world_issues()
    {
        PlaceTransition worlds = new(null, new PlaceId("2"), PlaceArrival.AtEntryPoint("Party Start"), "68");
        PlaceTransition places = new(Hall, new PlaceId("2"), PlaceArrival.AtEntryPoint("Party Start"), "7");

        Assert.Equal(TransitionKind.Scripted, new InteractionTravel(worlds, TransitionKind.Scripted).Kind);
        Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionTravel(places, TransitionKind.Scripted));
        Assert.Throws<ArgumentOutOfRangeException>(() => new InteractionTravel(places, TransitionKind.PaidService));
        Assert.Equal(TransitionKind.Entrance, new InteractionTravel(places, TransitionKind.Entrance).Kind);
    }

    private static PlacementDefinition Person()
    {
        const string json = """{ "id": "person-0", "kind": "person", "x": 0, "y": 0, "z": 0 }""";
        return new PlacementDefinition(new PlacementContentId("person", "person-0"), "people", 0, PlacePose.Origin, new ContentEntry("person-0", JsonDocument.Parse(json).RootElement));
    }

    /// <summary>A rule with one topic, answered by a use.</summary>
    private sealed class UseRule : IConversationRule
    {
        public ConversationSubject? Describe(ConversationTargetRequest request) => null;

        public ConversationAnswer Greeting(ConversationContext context) => new("'Yes?'");

        public IReadOnlyList<ConversationOffer> Offers(ConversationContext context) =>
            [new ConversationOffer(new ConversationTopic("castle", "The castle"), Verdict.Met)];

        public ConversationAnswer Take(ConversationTopic topic, ConversationContext context) =>
            new(string.Empty, handoff: new ConversationHandoff(HandoffOwner.Use, topic.Id));
    }
}
