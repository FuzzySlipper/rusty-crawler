using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Awards and standing: what a party has accomplished as records content can test, what the world's opinion
/// of it does to a threshold, the words a screen reads both by, and the one owner that moves either.
/// </summary>
/// <remarks>
/// <para>
/// <b>An award is not a kind of state, it is a record the party already carries.</b> A finished errand leaves
/// one, a promotion leaves one, an answer leaves one, and all three are party-wide effects — the same owner
/// the party's own flags live in, read back through the same condition vocabulary the conversation and the
/// quests judge, which is why this suite adds no award store to prove them. What these cases prove is the
/// wiring: a turn-in writes the record, a later errand's condition reads it, a threshold crossed through the
/// progression owner changes what a person offers, the reading reaches the projection, and both the standing
/// and the records survive a save.
/// </para>
/// <para>
/// The rules here are the test's own — a progression rule that states what a deed is worth to the world, a
/// standing rule that names the bands and reads the accomplishments back into words, and a conversation whose
/// one line waits for a standing. That is the seam working as designed: the kit holds no band, no award
/// family, and no reputation economy, so a test states its own and the same mechanism serves it.
/// </para>
/// </remarks>
public sealed class AwardTests
{
    private static readonly PlaceId Keep = new("1");
    private static readonly string ErrandRecord = "errand:seal-of-office";

    [Fact]
    public void An_award_a_turn_in_sets_is_the_record_a_later_errand_and_a_later_line_read()
    {
        using PartyEntity party = PartyOf(Member("Roderick"), Member("Ysolde"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        PartyQuests quests = new(new TestQuests(Errand(), Aftermath()), party, accounts, progression);

        // Nothing is on record before anything is done: the record a later errand waits for is not carried,
        // and the completion condition that names it does not hold either.
        Assert.False(party.Records.Has(ErrandRecord));
        TestQuests rule = new(Errand(), Aftermath());
        Assert.False(rule.Holds(Condition(party, new ConversationCondition(ConversationConditionKind.Flag, ErrandRecord))));

        // The errand is taken, walked, and handed back: the turn-in writes what the definition states — the
        // record of the errand itself and the deed its rewards name — through the party's own effects owner.
        quests.Offer(Errand().Id, Giver);
        quests.Accept(Errand().Id);
        quests.Observe(Keep);
        QuestResult paid = quests.TurnIn(Errand().Id, Giver);
        Assert.True(paid.IsApplied, paid.Refusal?.Message);

        Assert.True(party.Records.Has(ErrandRecord));
        Assert.True(party.Records.Has(FirstDeed));

        // A later errand's own offer condition is that record, judged through the vocabulary the conversation
        // and the quests share: the same flag now holds, where it did not before anything was done.
        Assert.True(rule.Holds(Condition(party, new ConversationCondition(ConversationConditionKind.Flag, ErrandRecord))));
        Assert.False(rule.Holds(Condition(party, new ConversationCondition(ConversationConditionKind.Flag, "errand:never-run"))));

        // A line that waits for the same record waits no longer: the conversation offers what it withholds
        // before the deed, which is a topic appearing because the party really did something.
        PartyConversations conversation = Conversation(party, new TestConversation());
        conversation.Open(Keep, ThePlacement, new ConversationSubject("person-0", [new ConversationPerson("marshal", "The Marshal", "709")]));
        Assert.Contains("deed", conversation.OnOffer.Select(offer => offer.Id));

        // And an award a conversation leaves is the same state again: what an answer records is a record the
        // party carries like any other.
        conversation.Choose("regard");
        Assert.True(party.Records.Has("heard:regard"));
    }

    [Fact]
    public void A_command_that_names_no_topic_is_refused_rather_than_thrown_out_of_the_update()
    {
        // The choice arrives from a screen's own payload inside the one admitted update, so a payload that
        // named nothing must be an answer rather than an exception: an exception here escapes the product's
        // callback and takes the running session with it, which is a lost game where a refusal is a sentence.
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyConversations conversation = Conversation(party, new TestConversation());
        conversation.Open(Keep, ThePlacement, TheSubject());

        ConversationResult nothing = conversation.Choose(string.Empty);
        Assert.False(nothing.IsApplied);
        Assert.Equal(ConversationCodes.ConversationTopicUnnamed, nothing.Code);

        // The conversation is exactly where it stood: the speaker is still speaking and the topics are
        // still what the state makes of them.
        Assert.True(conversation.IsOpen);
        Assert.Equal("The Marshal", conversation.Speaker?.Name);
        Assert.Contains("regard", conversation.Offers.Select(offer => offer.Id));
    }

    [Fact]
    public void A_standing_threshold_crossed_through_the_owner_changes_what_a_person_offers()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyProgression progression = new(new TestProgression(), party);
        PartyConversations conversation = Conversation(party, new TestConversation());
        conversation.Open(Keep, ThePlacement, TheSubject());

        // The line waits for a standing this party does not have, and says which standing it wants in the
        // party's own numbers rather than in words a screen would have to work out.
        ConversationOffer withheld = conversation.Offers.Single(offer => offer.Id == "regard");
        Assert.False(withheld.IsOnOffer);
        Assert.False(withheld.Availability.IsMet);
        Assert.Contains("0", withheld.Availability.Explanation, StringComparison.Ordinal);
        Assert.Contains("6", withheld.Availability.Explanation, StringComparison.Ordinal);

        // The threshold is crossed the only way it may be: through the progression owner, at the one entry
        // every award arrives at, with the source the quest owner names. Nothing else in this product writes
        // either number, which the source scan beside this case proves over the kit's own sources.
        ProgressionAwardResult award = progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, 6000));
        Assert.True(award.IsAwarded);
        Assert.Equal(6, award.Standing.Reputation);
        Assert.Equal(6, party.Reputation.Reputation);

        // The same conversation, read again, offers the line: nothing invalidated anything, because
        // availability is recomputed from the party on every read.
        Assert.True(conversation.Offers.Single(offer => offer.Id == "regard").IsOnOffer);
        ConversationResult said = conversation.Choose("regard");
        Assert.True(said.IsApplied);
        Assert.Contains("Trusted", said.Message, StringComparison.Ordinal);

        // An award that is not a deed the world sees moves the opinion not at all: the same owner, the same
        // entry, a different source, and the number stands where it was.
        party.Reputation.ChangeReputation(-1);
        ProgressionAwardResult kill = progression.Award(new PartyExperienceAward("kill", 5000));
        Assert.True(kill.IsAwarded);
        Assert.Equal(0, kill.Standing.Reputation);
        Assert.Equal(5, party.Reputation.Reputation);
    }

    [Fact]
    public void Standing_and_accomplishments_reach_the_projection_in_the_games_own_words()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyProgression progression = new(new TestProgression(), party);
        TestStanding standing = new();

        // What the reading is asked for is the party's own state: the band its reputation falls in, and the
        // records it carries that this game counts.
        progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, 6000));
        party.Records.Set(ErrandRecord, 1);
        party.Passages.Hold(new PlaceId("somewhere"), 1);

        PartySnapshot snapshot = PartySnapshot.From(party, standing);
        Assert.True(snapshot.Present);
        Assert.True(snapshot.StandingRead);
        Assert.Equal("Trusted", snapshot.Standing);
        Assert.Equal(6, snapshot.Reputation);
        Assert.Equal("the world has taken the party's measure", snapshot.StandingDetail);

        // Only the records this game counts are accomplishments, in the party's own order: the errand is one
        // and the passage the party bought is not, which is the reading rather than the party's whole state.
        AwardSnapshot award = Assert.Single(snapshot.Awards!);
        Assert.Equal(ErrandRecord, award.Id);
        Assert.Equal("errand", award.Kind);
        Assert.Equal(ErrandRecord, award.Label);

        // The same reading reaches the panel through the one projection, so the DOM prints it and computes
        // nothing: the band, what it means, and one row per accomplishment.
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(TestClock.Create(scale: 1)),
            new SessionParty.Playing(Party: party),
            rules: new SessionRules
            {
                Standing = standing,
                Progression = new ProgressionRules(new TestProgression()),
            });
        session.Start();

        ProjectedNode shown = channel.Latest().Field(SessionProjection.PartyField);
        Assert.True(shown.Field("standingRead").AsBoolean());
        Assert.Equal("Trusted", shown.Field("standing").AsString());
        Assert.Equal("the world has taken the party's measure", shown.Field("standingDetail").AsString());
        Assert.Equal(1, shown.Field("awards").Length());
        Assert.Equal(ErrandRecord, shown.Field("awards").Item(0).Field("id").AsString());
        Assert.Equal("errand", shown.Field("awards").Item(0).Field("kind").AsString());

        // A session whose ruleset reads no standing publishes that rather than a band nobody named, which is
        // a different fact from a party with nothing to its name. The party is a second one because a session
        // owns the party it plays and releases it with itself.
        using PartyEntity other = PartyOf(Member("Ysolde"));
        using RecordingUiProjectionChannel bare = new();
        using PartyRpgSession plain = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            bare,
            new SessionOwners(TestClock.Create(scale: 1)),
            new SessionParty.Playing(Party: other),
            rules: new SessionRules
            {
                Progression = new ProgressionRules(new TestProgression()),
            });
        plain.Start();
        ProjectedNode noReading = bare.Latest().Field(SessionProjection.PartyField);
        Assert.False(noReading.Field("standingRead").AsBoolean());
        Assert.Equal(string.Empty, noReading.Field("standing").AsString());
        Assert.Equal(0, noReading.Field("awards").Length());
    }

    [Fact]
    public void The_progression_owner_is_the_only_source_that_moves_reputation_or_fame()
    {
        // Reputation and fame are the party's own component and the progression owner is the one path into them. The
        // law reads every runtime source for a call to either mutator, bound to the component's own members, so a
        // second writer anywhere in the product fails here even if it compiled.
        SourceCode code = ProductSource.Runtime;
        ProductSource.OnlyIn(
            code.Uses([.. code.Members(typeof(PartyReputation), nameof(PartyReputation.ChangeReputation)),
                .. code.Members(typeof(PartyReputation), nameof(PartyReputation.ChangeFame))]),
            file => file.StartsWith("src/PartyRpg.Kit/Progression/", StringComparison.Ordinal),
            "The world's opinion of the party and how widely it is known are moved by the progression owner and by nothing else.");
    }

    [Fact]
    public void Reputation_fame_and_an_award_survive_a_save_round_trip()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        PartyQuests quests = new(new TestQuests(Errand(), Aftermath()), party, accounts, progression);

        // A deed done for real, so the state that is saved is the state a turn-in left rather than state a
        // test set: the errand's record, the deed its rewards name, and the standing the owner moved.
        quests.Offer(Errand().Id, Giver);
        quests.Accept(Errand().Id);
        quests.Observe(Keep);
        Assert.True(quests.TurnIn(Errand().Id, Giver).IsApplied);

        int reputation = party.Reputation.Reputation;
        int fame = party.Reputation.Fame;
        Assert.Equal(6, reputation);
        Assert.Equal(6, fame);

        GameClock clock = TestClock.Create(scale: 1);
        PlaceStateLedger places = new(PlaceGraph.From([], []), PlaceRespawnRule.FromContent());
        SessionSave document = new(
            party.Capture(),
            ClockSave.Capture(clock),
            new WorldSave(new PartyPose(Keep, PlacePose.Origin), places.Capture()),
            quests.Capture());

        // The document really written and read back under the one current schema's own metadata: a party
        // section that could not carry standing or records fails here rather than in a running product.
        string json = JsonSerializer.Serialize(document, SessionSaveJson.TypeInfo);
        SessionSave read = JsonSerializer.Deserialize(json, SessionSaveJson.TypeInfo)!;

        using PartyEntity restored = new PartyEntityFactory().Restore(read.Party);
        Assert.Equal(reputation, restored.Reputation.Reputation);
        Assert.Equal(fame, restored.Reputation.Fame);
        Assert.True(restored.Records.Has(ErrandRecord));
        Assert.True(restored.Records.Has(FirstDeed));

        // And what the game reads of the restored party is the same band and the same accomplishments: a
        // load is not a promotion or a demotion, and nothing about the deed is lost on the way through.
        PartySnapshot before = PartySnapshot.From(party, new TestStanding());
        PartySnapshot after = PartySnapshot.From(restored, new TestStanding());
        Assert.Equal(before.Standing, after.Standing);
        Assert.Equal(before.Awards, after.Awards);
    }

    /// <summary>The person the conversation tests speak with, and the placement they stand at.</summary>
    private const string Giver = "marshal";

    private static readonly PlacementDefinition ThePlacement = Placement();

    private static PlacementDefinition Placement()
    {
        using JsonDocument payload = JsonDocument.Parse("""{ "kind": "person", "id": "person-0", "people": [ "marshal" ] }""");
        return new PlacementDefinition(
            new PlacementContentId("person", "person-0"),
            "people",
            0,
            PlacePose.Origin,
            new ContentEntry("person-0", payload.RootElement.Clone()));
    }

    /// <summary>The record the errand's own rewards leave beside the errand's own.</summary>
    private const string FirstDeed = "award:first-deed";

    /// <summary>One question asked of a quest's stated condition, so the shared reading can be called directly.</summary>
    private static QuestConditionRequest Condition(PartyEntity party, ConversationCondition condition) =>
        new(Errand(), condition, party, TestClock.Create(scale: 1));

    private static PartyConversations Conversation(PartyEntity party, IConversationRule rule) =>
        new(rule, party, TestClock.Create(scale: 1));

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

    /// <summary>An errand that leaves a record and pays a deed worth six points of standing.</summary>
    private static QuestDefinition Errand() => new(
        new QuestId("seal-of-office"),
        "The seal of office",
        Giver,
        [new QuestObjective("reach", QuestObjectiveKind.Reach, Keep.Value, label: "Reach the keep")],
        new QuestRewards(experience: 6000, records: [new QuestRewardRecord(FirstDeed, 1)]),
        record: ErrandRecord,
        note: "Walk the keep and hand it back.");

    /// <summary>An errand offered only to a party that has finished the first one.</summary>
    private static QuestDefinition Aftermath() => new(
        new QuestId("aftermath"),
        "What comes after",
        Giver,
        [new QuestObjective("reach", QuestObjectiveKind.Reach, Keep.Value, label: "Walk the keep again")],
        offerConditions: [new ConversationCondition(ConversationConditionKind.Flag, ErrandRecord, 1, "the seal of office")]);

    /// <summary>The quests a test states, with the one reading this mechanism cannot make itself.</summary>
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

        public int Counts(QuestKillRequest request) => 0;

        /// <summary>A stated condition holds exactly when the party carries what it names, or stands where it asks.</summary>
        public bool Holds(QuestConditionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.Condition.Kind switch
            {
                ConversationConditionKind.Flag or ConversationConditionKind.Errand =>
                    request.Party.Records.Has(request.Condition.Name),
                ConversationConditionKind.Reputation =>
                    request.Party.Reputation.Reputation >= request.Condition.Amount,
                _ => false,
            };
        }
    }

    /// <summary>
    /// A progression rule whose standing reading is the one a deed the world sees earns: one point per
    /// thousand experience a finished errand paid and never less than one, and fame read from the party's own
    /// total as the donor reads it.
    /// </summary>
    private sealed class TestProgression : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000L * Math.Max(1, level);

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division)
        {
            ArgumentNullException.ThrowIfNull(division);
            List<ProgressionShare> shares = [];
            long each = division.Amount / Math.Max(1, division.Party.Members.Count);
            foreach (PartyMember member in division.Party.Members)
            {
                shares.Add(new ProgressionShare(member.Id, member.Profile.Name, each));
            }

            return shares;
        }

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            long total = 0;
            foreach (PartyMember member in request.Party.Members) total = checked(total + member.Progression.Experience);
            int fame = Math.Max(0, (int)(total / 1000) - request.Party.Reputation.Fame);
            int reputation = request.Event == ProgressionEventKind.Award
                && string.Equals(request.Source, PartyQuests.QuestSource, StringComparison.Ordinal)
                    ? (int)Math.Max(1, request.Amount / 1000)
                    : 0;
            return new ProgressionStanding(reputation, fame);
        }
    }

    /// <summary>
    /// A standing rule of a game's own: two bands with the game's words, and the records it counts as
    /// accomplishments, named by the identity the party carries them under rather than by a table — a test
    /// has no tables, and naming a record by itself is exactly what a game with none would do.
    /// </summary>
    private sealed class TestStanding : IStandingRule
    {
        public StandingReading Read(PartyEntity party)
        {
            ArgumentNullException.ThrowIfNull(party);
            return party.Reputation.Reputation >= 6
                ? StandingReading.Of("Trusted", "the world has taken the party's measure")
                : StandingReading.Of("Known", "the world has heard the party's name and nothing more");
        }

        public IReadOnlyList<AwardReading> Awards(PartyEntity party)
        {
            ArgumentNullException.ThrowIfNull(party);
            List<AwardReading> awards = [];
            foreach (PartyRecord held in party.Records.All)
            {
                string record = held.Name;
                int separator = record.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0) continue;
                string family = record[..separator];
                if (family is not ("errand" or "promotion" or "award")) continue;
                awards.Add(AwardReading.Of(record, family, record));
            }

            return awards;
        }
    }

    /// <summary>A conversation whose one line waits for the standing a party is worth an opinion at.</summary>
    private sealed class TestConversation : IConversationRule
    {
        /// <summary>The standing this game's people will speak about the party at.</summary>
        private const int WellRegarded = 6;

        public ConversationSubject? Describe(ConversationTargetRequest request) =>
            string.Equals(request.Placement.Content.Kind, "person", StringComparison.Ordinal) ? TheSubject() : null;

        public ConversationAnswer Greeting(ConversationContext context) =>
            new("'Well met.'", records: [$"met:{context.Speaker}"]);

        public IReadOnlyList<ConversationOffer> Offers(ConversationContext context)
        {
            ConversationCondition condition = new(
                ConversationConditionKind.Reputation,
                "reputation",
                WellRegarded,
                "the party's standing");
            ConversationTopic topic = new("regard", "What do people say about us?", [condition]);
            Verdict availability = context.Party is { } party && party.Reputation.Reputation >= WellRegarded
                ? Verdict.Met
                : Verdict.Unmet(
                    $"the party's standing is {context.Party?.Reputation.Reputation ?? 0} and this needs {WellRegarded}");
            List<ConversationOffer> offers = [new(topic, availability)];
            if (context.Party is { } carrier && carrier.Records.Has(ErrandRecord))
            {
                offers.Add(new ConversationOffer(new ConversationTopic("deed", "About the seal"), Verdict.Met));
            }

            return offers;
        }

        public ConversationAnswer Take(ConversationTopic topic, ConversationContext context) => topic.Id switch
        {
            "regard" => new ConversationAnswer(
                context.Party is { } party && party.Reputation.Reputation >= WellRegarded
                    ? "'Trusted is the word.'"
                    : "'Nothing worth repeating.'",
                records: ["heard:regard"]),
            _ => new ConversationAnswer("'It is done, then.'"),
        };
    }

    /// <summary>The one person these cases speak with.</summary>
    private static ConversationSubject TheSubject() =>
        new("person-0", [new ConversationPerson(Giver, "The Marshal", "709")]);
}
