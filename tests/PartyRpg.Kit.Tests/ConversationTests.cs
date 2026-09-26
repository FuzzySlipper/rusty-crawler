using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one conversation mechanism: the party speaks with whoever is here, is offered what the state allows,
/// chooses, and hears what the content says.
/// </summary>
/// <remarks>
/// <para>
/// Every rule a game would recognize here belongs to the test's own rule — who is present, what they say,
/// what a topic waits for, and what an answer carries — which is the point of the seam: the kit holds no
/// greeting, no topic, and no condition of its own, so the same mechanism serves a test that invents them
/// and a ruleset that owns them. The places and the people are written inline, so what a person is comes
/// from content in these tests exactly as it does from an imported pack in the product.
/// </para>
/// <para>
/// The state the conditions read is real: a party with a purse, effects, and members of its own, and the
/// kit's own clock. That is what makes "a topic appears only when its conditions hold" a fact about the
/// mechanism rather than about the test's bookkeeping, and it is why the same assertion can be made about a
/// party-carried flag, a standing, a class, a race, an hour, and a finished errand.
/// </para>
/// </remarks>
public sealed class ConversationTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId HallPlace = new("1");
    private static readonly UseIntentNames UseControls = new("test.use", "test.use", "test.ui.action.v1");
    private static readonly ConversationIntentNames ConversationControls = new("test.conversation.leave", "test.ui.action.v1");
    private static readonly ServiceIntentNames ServiceControls = new("test.service.leave", "test.ui.action.v1");

    [Fact]
    public void A_conversation_opens_with_whoever_is_here_and_says_what_content_gives_them()
    {
        using Hall hall = Hall.Build(new TestRule());
        PartyConversations conversations = hall.Conversations;

        // Nobody is spoken with until somebody is reached: the mechanism holds no conversation, answers that
        // it holds none, and refuses every command by name rather than doing nothing quietly.
        Assert.False(conversations.IsOpen);
        Assert.Empty(conversations.Offers);
        Assert.Equal("conversation-not-open", conversations.Choose("topic-1").Code);
        Assert.Equal("conversation-not-open", conversations.Turn("anybody").Code);
        Assert.Equal("conversation-not-open", conversations.Close().Code);

        // A placement nobody stands at is not somebody the party can speak with, which is how a door, a chest,
        // and a sign stay out of this mechanism entirely.
        Assert.Null(conversations.OpenTarget(HallPlace, hall.PlacementOf("chest-0")));

        ConversationResult opened = conversations.OpenTarget(HallPlace, hall.PlacementOf("person-0"))!;
        Assert.True(opened.IsApplied);
        Assert.Equal("open", opened.Action);
        Assert.Equal("Mira", conversations.Speaker?.Name);
        Assert.Equal("Mira: 'You again.'", opened.Message);
        Assert.Equal("'You again.'", conversations.Greeting);

        // What was said is the conversation's own memory, and closing forgets it: the same person greets the
        // party afresh the next time they speak.
        Assert.Equal(["mira"], conversations.Said.Select(line => line.Speaker));
        Assert.True(conversations.Close().IsApplied);
        Assert.False(conversations.IsOpen);
        Assert.Empty(conversations.Said);
        Assert.Empty(conversations.Greeting);
        Assert.Empty(ConversationSnapshot.From(conversations).Topics);

        // What the projection says about a conversation that is over is the leave itself: the mechanism is
        // still there, nobody is being spoken with, and the last thing said was goodbye.
        ConversationSnapshot closed = ConversationSnapshot.From(conversations);
        Assert.True(closed.Available);
        Assert.False(closed.Open);
        Assert.Equal("leave", closed.Action);
        Assert.Equal("applied", closed.Outcome);
    }

    [Fact]
    public void A_topic_appears_only_while_its_conditions_hold_and_is_hidden_again_when_they_stop()
    {
        using PartyEntity party = Party(reputation: 10);
        using Hall hall = Hall.Build(new TestRule(), party);
        PartyConversations conversations = hall.Conversations;
        conversations.OpenTarget(HallPlace, hall.PlacementOf("person-0"));

        // Every kind of state the vocabulary names, one topic each. Six topics are on offer and six are not,
        // and the verdict for each names what is missing.
        Assert.Equal(
            ["standing", "class", "race", "hour", "taken", "counter", "unrouted"],
            conversations.OnOffer.Select(offer => offer.Id));
        Assert.Equal(
            ["carried", "secondclass", "secondrace", "errand", "flag"],
            conversations.Withheld.Select(offer => offer.Id));
        Assert.Contains("does not carry", conversations.Availability("flag")!.Reason, StringComparison.Ordinal);

        // The party-carried flag: applying the effect the topic waits for puts it on offer, and removing it
        // hides the topic again — nothing was invalidated, because nothing is remembered between reads.
        party.Effects.Apply(new PartyEffect(new EffectId("invited"), 1));
        party.Effects.Apply(new PartyEffect(new EffectId("invitation"), 1));
        Assert.Equal(
            ["carried", "standing", "class", "race", "hour", "taken", "flag", "counter", "unrouted"],
            conversations.OnOffer.Select(offer => offer.Id));
        party.Effects.Remove(new EffectId("invited"));
        party.Effects.Remove(new EffectId("invitation"));
        Assert.Contains("flag", conversations.Withheld.Select(offer => offer.Id));
        Assert.Contains("carried", conversations.Withheld.Select(offer => offer.Id));

        // The standing: the party's own reputation, moved by its owner. One point below what the topic asks
        // for takes it off the list, and the reason states the standing the party actually has.
        party.Reputation.ChangeReputation(-1);
        Assert.DoesNotContain("standing", conversations.OnOffer.Select(offer => offer.Id));
        Assert.Contains("standing is 9", conversations.Availability("standing")!.Reason, StringComparison.Ordinal);
        party.Reputation.ChangeReputation(1);
        Assert.Contains("standing", conversations.OnOffer.Select(offer => offer.Id));

        // A member's class and race: the party's own members, not a flag about them. One topic per kind is
        // on offer because somebody in the band is that, and the other is withheld because nobody is.
        Assert.Contains("class", conversations.OnOffer.Select(offer => offer.Id));
        Assert.Contains("race", conversations.OnOffer.Select(offer => offer.Id));

        // A class and a race nobody in the band holds are withheld by the same read of the same owner: the
        // members the party actually has, one of whom is a fighter of the other folk and one a scholar.
        Assert.Contains("secondclass", conversations.Withheld.Select(offer => offer.Id));
        Assert.Contains("secondrace", conversations.Withheld.Select(offer => offer.Id));

        // The hour: the session's one clock, whose daylight window decides which of the two holds.
        Assert.Equal("day", conversations.Availability("hour")!.IsOnOffer ? "day" : "night");
        hall.Clock.Advance(GameDuration.FromHours(14));
        Assert.False(conversations.Availability("hour")!.IsOnOffer);
        Assert.Contains("clock stands at", conversations.Availability("hour")!.Reason, StringComparison.Ordinal);

        // An errand: a flag the quest owner will set, which nothing in this build sets yet.
        Assert.Contains("errand", conversations.Withheld.Select(offer => offer.Id));
        party.Effects.Apply(new PartyEffect(new EffectId("errand:7"), 1));
        Assert.Contains("errand", conversations.OnOffer.Select(offer => offer.Id));

        // A condition about state the world does not hold is unmet rather than invented: a session whose
        // ruleset composed no clock cannot know the hour, and says so rather than assuming one.
        PartyConversations timeless = new(hall.Rule, party, clock: null);
        timeless.OpenTarget(HallPlace, hall.PlacementOf("person-0"));
        Assert.Contains("hour", timeless.Withheld.Select(offer => offer.Id));
        Assert.Contains("nothing in this world keeps the hour", timeless.Availability("hour")!.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Taking_a_topic_says_what_content_says_and_records_what_the_party_carries()
    {
        using PartyEntity party = Party();
        using Hall hall = Hall.Build(new TestRule(), party);
        PartyConversations conversations = hall.Conversations;
        conversations.OpenTarget(HallPlace, hall.PlacementOf("person-0"));

        // The topic waits for a flag the party does not carry yet, which is what makes taking it a fact
        // about the state rather than about the list.
        party.Effects.Apply(new PartyEffect(new EffectId("invited"), 1));
        ConversationResult said = conversations.Choose("carried");
        Assert.True(said.IsApplied);
        Assert.Equal("say", said.Action);
        Assert.Equal("carried", said.Topic);
        Assert.Equal("You carry what I asked for.", said.Text);
        Assert.Equal("the errand behind this line is not run", said.Residue);
        Assert.Contains("You carry what I asked for.", said.Message, StringComparison.Ordinal);

        // What the answer records is party-carried state through the party's own owner, which is what a save
        // keeps and what a later topic can be gated on.
        Assert.True(party.Effects.Has(new EffectId("heard:carried")));

        // The line is in the transcript, so a screen can still show it after it leaves the list of things to
        // bring up.
        Assert.Contains(conversations.Said, line => line.Topic == "carried" && line.Text == "You carry what I asked for.");

        // A topic the ruleset withholds because the person has already answered it is refused with the
        // reason, and the conversation stays exactly where it was.
        ConversationResult again = conversations.Choose("carried");
        Assert.False(again.IsApplied);
        Assert.Equal("conversation-topic-withheld", again.Code);
        Assert.Contains("already said this", again.Message, StringComparison.Ordinal);

        // A topic nobody has is refused by name rather than silently doing nothing.
        ConversationResult unknown = conversations.Choose("nothing-like-this");
        Assert.Equal("conversation-topic-unknown", unknown.Code);
    }

    [Fact]
    public void A_topic_whose_condition_stopped_holding_is_refused_with_the_reason_it_states()
    {
        using PartyEntity party = Party(reputation: 12);
        using Hall hall = Hall.Build(new TestRule(), party);
        PartyConversations conversations = hall.Conversations;
        conversations.OpenTarget(HallPlace, hall.PlacementOf("person-0"));

        // The offer list is read at the moment a choice is made rather than taken from whatever a screen was
        // showing: the standing falls between the two, and the topic is refused with the reason the condition
        // reaches rather than applied from a stale answer.
        Assert.Contains("standing", conversations.OnOffer.Select(offer => offer.Id));
        party.Reputation.ChangeReputation(-4);
        ConversationResult refused = conversations.Choose("standing");
        Assert.False(refused.IsApplied);
        Assert.Equal("conversation-topic-withheld", refused.Code);
        Assert.Contains("standing is 8", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_conversation_turns_between_the_people_present_and_refuses_anybody_who_is_not()
    {
        using PartyEntity party = Party();
        using Hall hall = Hall.Build(new TestRule(), party);
        PartyConversations conversations = hall.Conversations;
        conversations.OpenTarget(HallPlace, hall.PlacementOf("household-0"));

        // A household is one conversation with everybody in it: the first person speaks, and turning to
        // another is the conversation's own state changing rather than a topic being taken.
        Assert.Equal(2, conversations.Subject!.People.Count);
        Assert.Equal("Mira", conversations.Speaker?.Name);

        ConversationResult turned = conversations.Turn("simon");
        Assert.True(turned.IsApplied);
        Assert.Equal("turn", turned.Action);
        Assert.Equal("Simon", conversations.Speaker?.Name);
        Assert.Equal("'Yes?'", conversations.Greeting);
        Assert.Equal(["mira", "simon"], conversations.Said.Select(line => line.Speaker));

        // Turning to whoever is already speaking changes nothing, and says so.
        ConversationResult same = conversations.Turn("simon");
        Assert.True(same.IsApplied);
        Assert.Contains("already the one speaking", same.Message, StringComparison.Ordinal);

        // Nobody else is here, and a rule that hands the conversation to somebody absent is refused by name
        // rather than leaving it pointed at nobody.
        Assert.Equal("conversation-person-unknown", conversations.Turn("nobody").Code);
        Assert.Equal("conversation-speaker-unknown", conversations.Choose("hands-to-nobody").Code);
        Assert.Equal("Simon", conversations.Speaker?.Name);
    }

    [Fact]
    public void A_use_that_reaches_somebody_opens_the_conversation_and_the_screen_shows_what_it_says()
    {
        using PartyEntity party = Party();
        using Hall hall = Hall.Build(new TestRule(), party, interactive: true);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            hall.World,
            clock: hall.Clock,
            party: party,
            useInput: new InteractionUseInput(UseControls),
            conversation: hall.Rule,
            conversationInput: ConversationControls);
        session.Start();

        // The update faces the person, and the declared use control is what speaks with them.
        session.Update(Update(1, 1));
        Assert.Equal("Mira", channel.Latest().Field(SessionProjection.InteractionField).Field("label").AsString());
        Assert.Equal("talk", channel.Latest().Field(SessionProjection.InteractionField).Field("verb").AsString());
        session.Update(Update(2, 1, Digital("test.use", InputEdge.Pressed)));

        // What the panel shows is the conversation's own facts: who is here, who is speaking, what was said,
        // and which topics the state offers and withholds.
        ProjectedNode shown = channel.Latest().Field(SessionProjection.ConversationField);
        Assert.True(shown.Field("available").AsBoolean());
        Assert.True(shown.Field("open").AsBoolean());
        Assert.Equal("person-0", shown.Field("subject").AsString());
        Assert.Equal("Mira", shown.Field("speaker").AsString());
        Assert.Equal("'You again.'", shown.Field("greeting").AsString());
        Assert.Equal("applied", shown.Field("outcome").AsString());
        Assert.Equal("open", shown.Field("action").AsString());
        Assert.Equal(1, shown.Field("people").Length());
        Assert.Equal("class", shown.Field("topics").Item(0).Field("id").AsString());
        Assert.True(shown.Field("topics").Item(0).Field("available").AsBoolean());
        Assert.Equal("carried", shown.Field("withheld").Item(0).Field("id").AsString());
        Assert.False(shown.Field("withheld").Item(0).Field("available").AsBoolean());
        Assert.Contains("does not carry", shown.Field("withheld").Item(0).Field("reason").AsString(), StringComparison.Ordinal);

        // What a topic said arrives in the same projection as the choice, with the residue the ruleset
        // stated beside it.
        party.Effects.Apply(new PartyEffect(new EffectId("invited"), 1));
        session.Update(Update(3, 1, Payload("""{"action":"conversation.topic","target":"carried"}""")));
        ProjectedNode said = channel.Latest().Field(SessionProjection.ConversationField);
        Assert.Equal("say", said.Field("action").AsString());
        Assert.Equal("carried", said.Field("topic").AsString());
        Assert.Contains("You carry what I asked for.", said.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("the errand behind this line is not run", said.Field("residue").AsString(), StringComparison.Ordinal);

        // The declared leave control ends it, and the panel says the party walked away.
        session.Update(Update(4, 1, Digital("test.conversation.leave", InputEdge.Pressed)));
        ProjectedNode left = channel.Latest().Field(SessionProjection.ConversationField);
        Assert.False(left.Field("open").AsBoolean());
        Assert.Equal("leave", left.Field("action").AsString());
        Assert.Equal("applied", left.Field("outcome").AsString());
        Assert.False(session.Conversations!.IsOpen);
    }

    [Fact]
    public void A_conversation_holds_the_party_still_and_a_service_handoff_takes_the_controls_from_it()
    {
        using PartyEntity party = Party();
        using Hall hall = Hall.Build(new TestRule(), party, interactive: true, mover: true);
        using RecordingUiProjectionChannel channel = new();
        TestServiceRule services = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            hall.World,
            movementInput: new MovementInput(MovementControls, turnRatePerSecond: 2048),
            clock: hall.Clock,
            party: party,
            diagnostics: hall.Diagnostics,
            service: services,
            useInput: new InteractionUseInput(UseControls),
            serviceInput: ServiceControls,
            conversation: hall.Rule,
            conversationInput: ConversationControls);
        session.Start();

        // A held forward control walks the party while nothing owns the controls.
        session.Update(Update(1, 1, Digital("test.move-forward", InputEdge.Held)));
        PlacePose walked = hall.World.Party.PlacePose;
        Assert.NotEqual(0, walked.Y);

        // Talking owns them: the same held control walks nowhere, and the world keeps its own time.
        session.Update(Update(2, 0));
        session.Update(Update(3, 1, Digital("test.use", InputEdge.Pressed)));
        Assert.True(session.Conversations!.IsOpen);
        double before = session.SimulationSeconds;
        session.Update(Update(4, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(walked, hall.World.Party.PlacePose);
        Assert.True(session.SimulationSeconds > before);

        // The offer the person makes names the counter they keep, and the session hands the party to the
        // service mechanism: the counter opens through its own entry, and the conversation ends where the
        // counter begins because the counter now owns the controls.
        session.Update(Update(5, 1, Payload("""{"action":"conversation.topic","target":"counter"}""")));
        Assert.True(session.Services!.IsOpen);
        Assert.False(session.Conversations.IsOpen);
        session.Update(Update(6, 1, Digital("test.move-forward", InputEdge.Held)));
        Assert.Equal(walked, hall.World.Party.PlacePose);

        // A handoff naming an owner nothing routes is reported by name and changes nothing: the conversation
        // stays open, because nothing took the party anywhere.
        session.Update(Update(7, 1, Digital("test.service.leave", InputEdge.Pressed)));
        session.Update(Update(8, 1, Digital("test.use", InputEdge.Pressed)));
        session.Update(Update(9, 1, Payload("""{"action":"conversation.topic","target":"unrouted"}""")));
        Assert.True(session.Conversations.IsOpen);
        Assert.Contains(
            hall.Diagnostics.Published,
            report => string.Equals(report.Code, "conversation-handoff-unowned", StringComparison.Ordinal));
    }

    /// <summary>One admitted update of the kit's own session, in the shape the engine admits one.</summary>
    private static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            Generation: 1,
            ControlRevision: 1,
            ObservedHostTimeNanoseconds: 0,
            SimulationStep: step,
            FixedStepHz: 60,
            AdmittedStepCount: admitted,
            DroppedStepCount: 0,
            FixedDeltaSeconds: 1.0 / 60.0);
        return new ProductUpdate(facts, input);
    }

    /// <summary>One digital event on a product intent, in the shape the engine admits it.</summary>
    private static ProductInputEvent Digital(string intent, InputEdge edge) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>One semantic action on a declared payload contract, as a screen sends it.</summary>
    private static ProductInputEvent Payload(string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(ConversationControls.ActionContract), Encoding.UTF8.GetBytes(json));

    private static readonly MovementIntentNames MovementControls = new(
        "test.move-forward",
        "test.move-back",
        "test.strafe-left",
        "test.strafe-right",
        "test.turn-left",
        "test.turn-right",
        "test.jump");

    /// <summary>A party of two whose class, race, standing, and effects a test moves by hand.</summary>
    private static PartyEntity Party(int reputation = 0)
    {
        PartyEntityFactory factory = new();
        return factory.Create(new PartyCreation(
            [
                Member("Mira", "testfolk", "fighter"),
                Member("Simon", "otherfolk", "scholar"),
            ],
            coins: 120,
            foodPortions: 4,
            ProvisionUnit.Portions,
            reputation,
            fame: 0));
    }

    private static MemberCreation Member(string name, string race, string characterClass) =>
        new(new PartyMemberSeed(
            name,
            new RaceId(race),
            new ClassId(characterClass),
            [new AttributeScore(new AttributeId("vigour"), 12)],
            skills: [],
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(10),
            spellPoints: ResourcePool.Full(5)));

    /// <summary>
    /// The conversation answers a test supplies: one household of two people, a person on their own, and one
    /// topic per kind of state the vocabulary names.
    /// </summary>
    private sealed class TestRule : IConversationRule
    {
        public ConversationSubject? Describe(ConversationTargetRequest request) => request.Placement.Content.Kind switch
        {
            "person" => new ConversationSubject("person-0", [new ConversationPerson("mira", "Mira", "709")]),
            "household" => new ConversationSubject(
                "household-0",
                [new ConversationPerson("mira", "Mira", "709"), new ConversationPerson("simon", "Simon", "707")]),
            _ => null,
        };

        public ConversationAnswer Greeting(ConversationContext context) =>
            context.Speaker == "simon"
                ? new ConversationAnswer("'Yes?'", records: ["met:simon"])
                : new ConversationAnswer("'You again.'", records: ["met:mira"]);

        public IReadOnlyList<ConversationOffer> Offers(ConversationContext context)
        {
            List<ConversationOffer> offers = [];
            foreach (ConversationTopic topic in TopicList(context))
            {
                ConversationAvailability availability = ConversationAvailability.OnOffer;
                foreach (ConversationCondition condition in topic.Conditions)
                {
                    availability = Judge(condition, context);
                    if (!availability.IsOnOffer) break;
                }

                if (availability.IsOnOffer &&
                    context.Said.Any(line => string.Equals(line.Topic, topic.Id, StringComparison.Ordinal)))
                {
                    availability = ConversationAvailability.Withheld("they have already said this in this conversation");
                }

                offers.Add(new ConversationOffer(topic, availability));
            }

            return offers;
        }

        public ConversationAnswer Take(ConversationTopic topic, ConversationContext context) => topic.Id switch
        {
            "counter" => new ConversationAnswer("The party steps up to the counter.", handoff: new ConversationHandoff(ConversationHandoffs.Service)),
            "unrouted" => new ConversationAnswer("Something else entirely.", handoff: new ConversationHandoff("errand", "some-errand")),
            "hands-to-nobody" => new ConversationAnswer("You should hear this from somebody else.", speaker: "absent"),
            "carried" => new ConversationAnswer(
                "You carry what I asked for.",
                "the errand behind this line is not run",
                records: ["heard:carried"]),
            _ => new ConversationAnswer($"About {topic.Label}: nothing more than that."),
        };

        /// <summary>The topics each speaker has, in the order a screen shows them.</summary>
        private static IEnumerable<ConversationTopic> TopicList(ConversationContext context)
        {
            // A household of more than one person also offers to hand the conversation to somebody else,
            // which is what makes a rule that names somebody absent visible.
            bool household = context.Subject.People.Count > 1;
            yield return Track("carried", "Tell me about the road", new ConversationCondition(ConversationConditionKind.Flag, "invited"));
            yield return Track("standing", "Ask for work", new ConversationCondition(ConversationConditionKind.Reputation, "reputation", 10));
            yield return Track("class", "Talk shop", new ConversationCondition(ConversationConditionKind.Class, "fighter"));
            yield return Track("race", "Talk of home", new ConversationCondition(ConversationConditionKind.Race, "otherfolk"));
            yield return Track("secondclass", "Talk to a priest", new ConversationCondition(ConversationConditionKind.Class, "priest"));
            yield return Track("secondrace", "Talk to a native", new ConversationCondition(ConversationConditionKind.Race, "nativefolk"));
            yield return Track("hour", "About the daylight", new ConversationCondition(ConversationConditionKind.Hour, "day"));
            yield return Track("errand", "About the errand", new ConversationCondition(ConversationConditionKind.Errand, "errand:7", 1, "the errand the table calls 7"));
            yield return Track("taken", "What you already told me", new ConversationCondition(ConversationConditionKind.Flag, "met:mira"));
            yield return Track("flag", "Ask for the invitation", new ConversationCondition(ConversationConditionKind.Flag, "invitation"));
            yield return Track("counter", "Step up to the counter");
            yield return Track("unrouted", "Ask about the errand");
            if (household) yield return Track("hands-to-nobody", "Ask Simon instead");
        }

        private static ConversationTopic Track(string id, string label, params ConversationCondition[] conditions) =>
            new(id, label, conditions);

        /// <summary>What one condition makes of the state, read from the owner that holds it.</summary>
        private static ConversationAvailability Judge(ConversationCondition condition, ConversationContext context) =>
            condition.Kind switch
            {
                ConversationConditionKind.Flag => context.Party is { } party && party.Effects.Has(new EffectId(condition.Name))
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld($"the party does not carry {condition.Label}"),
                ConversationConditionKind.Reputation => (context.Party?.Reputation.Reputation ?? 0) >= condition.Amount
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld($"the party's standing is {context.Party?.Reputation.Reputation ?? 0}"),
                ConversationConditionKind.Class => context.Party is { } members && members.Members.Any(member => member.Profile.Class.Value == condition.Name)
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld("nobody here is that"),
                ConversationConditionKind.Race => context.Party is { } band && band.Members.Any(member => member.Profile.Race.Value == condition.Name)
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld("nobody here is that"),
                ConversationConditionKind.Hour => context.Clock is { } clock && clock.IsDaylight
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld(context.Clock is null ? "nothing in this world keeps the hour" : $"the clock stands at {context.Clock.Now.Hour:00}:00"),
                ConversationConditionKind.Errand => context.Party is { } carrier && carrier.Effects.Has(new EffectId(condition.Name))
                    ? ConversationAvailability.OnOffer
                    : ConversationAvailability.Withheld($"{condition.Label} is not finished"),
                _ => ConversationAvailability.Withheld("nothing knows what that asks for"),
            };
    }

    /// <summary>
    /// A service the tests' counters are served by: one counter per person who keeps one, with nothing on
    /// its shelves, so what is proved is the handoff rather than the shop.
    /// </summary>
    private sealed class TestServiceRule : IServiceRule
    {
        public ServiceDefinition? Describe(ServiceTargetRequest request)
        {
            if (!string.Equals(request.Placement.Content.Kind, "person", StringComparison.Ordinal)) return null;
            return new ServiceDefinition(
                new ServiceId(request.Placement.Content.Id),
                new ServiceKind("Fire Guild"),
                "The hall's guild",
                [ServiceOperationKind.Buy],
                stock: [],
                lessons: []);
        }

        public IReadOnlyList<ServiceStockLine> Stock(ServiceStockRequest request) => [];

        public IReadOnlyList<ServiceLesson> Lessons(ServiceLessonRequest request) => [];

        public IReadOnlyList<ServiceOffer> Offers(ServiceOfferRequest request) => [];

        public IReadOnlyList<string> Access(ServiceAccessRequest request) => [];

        public ServiceEligibility Judge(ServiceEligibilityRequest request) => ServiceEligibility.Allowed;

        public ServiceQuote Quote(ServiceQuoteRequest request) => ServiceQuote.Free;
    }

    /// <summary>
    /// A hall holding a person, a household, a counter, and a chest, with the party that walks in and the
    /// clock the hour is judged against.
    /// </summary>
    private sealed class Hall : IDisposable
    {
        private Hall(SessionWorld world, TestRule rule, GameClock clock, RecordingDiagnosticsService diagnostics)
        {
            World = world;
            Rule = rule;
            Clock = clock;
            Diagnostics = diagnostics;
        }

        internal SessionWorld World { get; }

        internal TestRule Rule { get; }

        internal GameClock Clock { get; }

        internal RecordingDiagnosticsService Diagnostics { get; }

        internal PartyConversations Conversations => WorldConversations ?? throw new InvalidOperationException("This hall holds no conversation mechanism.");

        private PartyConversations? WorldConversations { get; set; }

        internal static Hall Build(
            TestRule rule,
            PartyEntity? party = null,
            bool interactive = false,
            bool mover = false)
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Document("places", "place", Place())),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog);
            GameClock clock = new(
                GameCalendar.TwelveMonthsOfFourWeeks,
                new GameDate(1168, 1, 1, 9, 0, 0),
                new GameTimeScale(1),
                new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));
            PartyPoseOwner owner = new(
                new PartyPose(HallPlace, new PlacePose(0, 0, 0, 0, 0)),
                new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));

            // The hall's interaction answers are the test's: what stands at a placement is the conversation
            // rule's own answer, so a person, a household, and a counter are one kind of thing to the use.
            InteractionPolicy policy = new(
                new PeopleInteraction(rule),
                PlaceSpace.HeightIsThird(new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512), radiansAtZeroFacing: 0),
                new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31));

            RecordingDiagnosticsService diagnostics = new();
            SessionWorld world = new(
                graph,
                owner,
                new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new FreeCostRule(),
                time: clock,
                mover: mover ? new WalkingMover(owner) : null,
                diagnostics: diagnostics,
                clock: clock,
                partyEntity: party,
                interaction: interactive ? policy : null);
            Hall hall = new(world, rule, clock, diagnostics);
            hall.WorldConversations = new PartyConversations(rule, party, clock);
            return hall;
        }

        /// <summary>One placement of the hall, so a test speaks with exactly what it names.</summary>
        internal PlacementDefinition PlacementOf(string id)
        {
            PlacePopulationContent population = PlacePopulationContent.Read(World.Graph);
            return population.PlacementsOf(HallPlace).First(placement => placement.Content.Id == id);
        }

        public void Dispose() => World.Dispose();

        private static string Place() =>
            """
            { "id": "1", "kind": "interior", "name": "Hall", "respawnDays": 3,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "person-0", "kind": "person", "x": 0, "y": 100, "z": 0 },
                { "id": "household-0", "kind": "household", "x": 100, "y": 0, "z": 0, "name": "The hall's guild" },
                { "id": "chest-0", "kind": "chest", "x": -100, "y": 0, "z": 0 } ] }
            """;

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
            }
            """;

        private static string Document(string documentId, string definitionKind, params string[] entries) =>
            $$"""
            { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
            """;
    }

    /// <summary>The interaction answers a test's hall uses: whoever the conversation rule says is here.</summary>
    private sealed class PeopleInteraction(TestRule rule) : IInteractionRule
    {
        public InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
            rule.Describe(new ConversationTargetRequest(request.Place, request.Placement)) is { } subject
                ? new InteractionTargetDefinition(new InteractionTargetKind("person"), subject.First.Name, InteractionVerb.Talk, 512)
                : null;

        public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) =>
            InteractionRequirementVerdict.Satisfied;

        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) => null;

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            InteractionOutcome.Applied("spoken", $"The party speaks with {target.Name}.");
    }

    /// <summary>A mover that walks the party when it is asked to, so a test can show where it did not go.</summary>
    private sealed class WalkingMover(PartyPoseOwner party) : IPartyMover
    {
        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds)
        {
            PlacePose pose = party.PlacePose;
            if (intent.IsStill)
            {
                return new MovementOutcome(pose, System.Numerics.Vector3.Zero, Grounded: true, default, CharacterBlockFlags.None, default, SurfaceEffect.Ordinary, FallOutcome.None);
            }

            PlacePose moved = pose with { Y = pose.Y + (intent.Forward * 32) + (intent.Strafe * 32) };
            party.Enter(party.Place, moved);
            return new MovementOutcome(moved, System.Numerics.Vector3.Zero, Grounded: true, default, CharacterBlockFlags.None, default, SurfaceEffect.Ordinary, FallOutcome.None);
        }

        public bool InSight(System.Numerics.Vector3 from, System.Numerics.Vector3 to) => true;

        public void Dispose()
        {
        }
    }

    /// <summary>A world where nothing is charged for walking, so a test's coins stay where they are.</summary>
    private sealed class FreeCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }
}
