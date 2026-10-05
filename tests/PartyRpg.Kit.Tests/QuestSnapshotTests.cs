using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>Quest projection readings: the last payment reports what actually reached each owner.</summary>
public sealed class QuestSnapshotTests
{
    private static readonly ItemDefinitionId Parcel = new("parcel");
    private static readonly ConditionId Unconscious = new("unconscious");

    [Fact]
    public void Completed_quest_reports_zero_experience_when_every_member_is_ineligible_but_other_payment_lands()
    {
        using PartyEntity party = PartyOf(Member("Tester"));
        QuestDefinition definition = new(
            new QuestId("deliver-parcel"),
            "Deliver the parcel",
            "receiver",
            [new QuestObjective("deliver", QuestObjectiveKind.Deliver, Parcel.Value, label: "Hand over the parcel", person: "met:receiver")],
            new QuestRewards(
                experience: 2400,
                coins: 25,
                records: [new QuestRewardRecord("parcel-delivered")]),
            record: "quest:parcel-delivered");
        PartyQuests quests = new(
            new TestQuests(definition),
            party,
            new PartyResourceLedger(party),
            new PartyProgression(new NoEligibleMembers(), party));

        Assert.True(quests.Offer(definition.Id, "receiver").IsApplied);
        Assert.Equal("The errand 'Deliver the parcel' was offered and is in the party's journal.", QuestSnapshot.From(quests).Message);
        Assert.True(quests.Accept(definition.Id).IsApplied);
        Assert.Equal("The errand 'Deliver the parcel' was taken.", QuestSnapshot.From(quests).Message);
        party.Records.Set("met:receiver", 1);
        ItemInstance parcel = party.AcquireItem(Parcel).Item!;

        QuestResult paid = quests.TurnIn(definition.Id, "receiver");

        Assert.True(paid.IsApplied, paid.Refusal?.Message);
        Assert.Equal(QuestStage.TurnedIn, paid.Stage);
        Assert.Equal(2400, paid.Payment.Experience!.Amount);
        Assert.Equal(0, paid.Payment.Experience.Awarded);
        Assert.Equal("progression-award-unshared", paid.Payment.Experience.Refusal!.Code);
        Assert.Equal(25, paid.Payment.Coins);
        Assert.Equal(25, party.Purse.Coins);
        Assert.Equal([new QuestRewardRecord("parcel-delivered")], paid.Payment.Records);
        Assert.Equal([new QuestRewardItem(Parcel.Value)], paid.Payment.Delivered);
        Assert.Null(party.FindItem(parcel.Id));
        Assert.True(party.Records.Has("parcel-delivered"));
        Assert.True(party.Records.Has("quest:parcel-delivered"));

        QuestSnapshot snapshot = QuestSnapshot.From(quests);

        Assert.Equal("turn-in", snapshot.Action);
        Assert.Equal("applied", snapshot.Outcome);
        Assert.Equal(0, snapshot.Experience);
        Assert.Equal(25, snapshot.Coins);
        Assert.Equal(["parcel-delivered (1)"], snapshot.Records);
        Assert.Equal(["1 × parcel"], snapshot.Delivered);
        Assert.Equal(string.Empty, snapshot.Code);
        Assert.Equal("turned-in", Assert.Single(snapshot.Journal).State);
        Assert.Equal("The errand 'Deliver the parcel' was finished.", snapshot.Message);

        QuestResult repeated = quests.TurnIn(definition.Id, "receiver");
        Assert.Equal(QuestCodes.QuestAlreadyFinished, repeated.Refusal!.Code);
    }

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
        conditions: [new ActiveCondition(Unconscious)],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    private sealed class TestQuests(params QuestDefinition[] definitions) : IQuestRule
    {
        public IReadOnlyList<QuestDefinition> Definitions { get; } = definitions;

        public QuestDefinition? Definition(QuestId quest) =>
            Definitions.FirstOrDefault(definition => definition.Id == quest);

        public int Counts(QuestKillRequest request) => 0;

        public bool Holds(QuestConditionRequest request) => true;
    }

    private sealed class NoEligibleMembers : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division)
        {
            ArgumentNullException.ThrowIfNull(division);
            List<ProgressionShare> shares = [];
            foreach (PartyMember member in division.Party.Members)
            {
                if (member.Conditions.Has(Unconscious)) continue;
                shares.Add(new ProgressionShare(member.Id, member.Profile.Name, division.Amount));
            }

            return shares;
        }

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }
}
