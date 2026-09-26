using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Skills;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Ranks: the ladder a game states, the requirements it asks for, the one owner that moves a member's class
/// and rank together, the path a second promotion chooses, and the record a rank leaves behind.
/// </summary>
/// <remarks>
/// <para>
/// The ladder is the test's own, which is the point of the seam: the kit holds no class, no rank, and no
/// requirement of any game's, so a test states a ladder of its own and demands the same mechanism serve it.
/// What only this suite can prove is that a rank moves through the progression owner — the source scan
/// beside the progression tests fails the kit if any other source names that transition — that every
/// requirement kind is judged against state the party really holds, that an errand is named and routed
/// rather than faked, and that a second promotion's choice is recorded where a save already carries it.
/// </para>
/// <para>
/// The save round trip is the party's own capture and restore rather than a document on disk: a member's
/// class and rank are two fields of the record a save writes, so the round trip through the party's own
/// owner is the same one a file makes.
/// </para>
/// </remarks>
public sealed class PromotionTests
{
    private static readonly ClassId Recruit = new("recruit");
    private static readonly ClassId Sergeant = new("sergeant");
    private static readonly ClassId Captain = new("captain");
    private static readonly ClassId Deserter = new("deserter");
    private static readonly SkillId Blades = new("blades");

    [Fact]
    public void A_rank_is_given_by_its_giver_and_moves_the_class_and_the_rank_together()
    {
        using PartyEntity party = PartyOf(Member("Roderick", Recruit));
        PartyProgression progression = new(new TestRule(), party, promotions: Ladder());
        PartyMember member = party.Members[0];
        party.AcquireItem(new ItemDefinitionId("token"));

        // The rank asks for its giver and for what the party carries, and both hold: the class the member
        // belongs to changes with the rank it holds, because every ceiling, growth table, and class condition
        // reads the class, and a promotion that moved one of the two would leave a character whose name and
        // abilities disagree.
        PromotionResult given = progression.Promote("recruit-sergeant", "quartermaster");
        Assert.True(given.IsGranted);
        PromotionGrant grant = Assert.Single(given.Granted);
        Assert.Equal("Roderick", grant.Name);
        Assert.Equal("recruit", grant.FromClass);
        Assert.Equal(1, grant.FromRank);
        Assert.Equal("sergeant", grant.ToClass);
        Assert.Equal(2, grant.Rank);
        Assert.Equal(string.Empty, grant.Choice);
        Assert.Equal(2, grant.Met.Count);
        Assert.Equal("sergeant", member.Profile.Class.Value);
        Assert.Equal(2, member.Progression.ClassRank);

        // The record the rank leaves is the party's own carried state, under the rank's own name, which is
        // what a later rank, a person, or a quest's turn-in reads.
        Assert.True(party.Effects.Has(new EffectId("promotion:recruit-sergeant")));
        Assert.Same(given, progression.LastPromotion);

        // A rank the ladder does not carry is refused by name and moves nothing.
        PromotionResult unknown = progression.Promote("sergeant-fish", "quartermaster");
        Assert.False(unknown.IsGranted);
        Assert.Equal("promotion-unknown", unknown.Refusal!.Code);
        Assert.Equal(2, member.Progression.ClassRank);

        // The rank the promotion continues from is the ladder's own statement, so the same ladder carries a
        // member on: the sergeant's next rank is the one this ladder states for it.
        PromotionResult next = progression.Promote("sergeant-captain", "quartermaster");
        Assert.True(next.IsGranted);
        Assert.Equal("captain", member.Profile.Class.Value);
        Assert.Equal(3, member.Progression.ClassRank);
    }

    [Fact]
    public void Every_unmet_requirement_is_named_and_an_errand_is_routed_rather_than_faked()
    {
        using PartyEntity party = PartyOf(Member("Roderick", Recruit));
        PartyProgression progression = new(new TestRule(), party, promotions: FourKinds());
        PartyMember member = party.Members[0];

        // Nothing at all has been met: the party is speaking with the wrong person, carries none of what the
        // rank asks for, holds none of the deed's record, and owns no errand's state.
        PromotionResult refused = progression.Promote("recruit-sergeant", "somebody-else");
        Assert.False(refused.IsGranted);
        Assert.Equal("promotion-requirements-unmet", refused.Refusal!.Code);
        PromotionDenial denial = Assert.Single(refused.Denied);
        Assert.Equal(4, denial.Missing.Count);

        // Each requirement is named by its own words, with the party's own standing in them: a player reads
        // which of the four blocked the rank rather than that something did.
        Assert.Contains(denial.Missing, line => line.Contains("granted by quartermaster", StringComparison.Ordinal));
        Assert.Contains(denial.Missing, line => line.Contains("needs 2 × token, and the party carries 0", StringComparison.Ordinal));
        Assert.Contains(denial.Missing, line => line.Contains("needs the record of three victories (3), and the party's record stands at 0", StringComparison.Ordinal));
        Assert.Contains(denial.Missing, line => line.Contains("nothing in this build owns an errand's state to judge it", StringComparison.Ordinal));
        Assert.Contains("granted by quartermaster", refused.Refusal.Message, StringComparison.Ordinal);

        // Three of the four are state the party really holds, so they can be met; the errand cannot be, and
        // the rank stays refused with that one requirement named. Nothing invents a flag to stand in for it.
        party.AcquireItem(new ItemDefinitionId("token"), 2);
        party.Effects.Apply(new PartyEffect(new EffectId("victories"), 3));
        PromotionResult stillRefused = progression.Promote("recruit-sergeant", "quartermaster");
        Assert.False(stillRefused.IsGranted);
        PromotionDenial remaining = Assert.Single(stillRefused.Denied);
        string only = Assert.Single(remaining.Missing);
        Assert.Contains("the errand", only, StringComparison.Ordinal);
        Assert.Equal("recruit", member.Profile.Class.Value);
        Assert.Equal(1, member.Progression.ClassRank);
    }

    [Fact]
    public void A_rank_is_given_to_the_class_and_each_member_is_reported_for_themselves()
    {
        // What a rank asks the party to carry is the party's own: one shared pack is one answer to "does the
        // party hold it", so a proof is brought by the band rather than by one character, exactly as the
        // giver's own line promises when it says it will make the members of a class into the rank above.
        using PartyEntity party = PartyOf(Member("Ann", Sergeant, rank: 1), Member("Borin", Sergeant, rank: 2), Member("Cass", Recruit));
        PartyProgression progression = new(new TestRule(), party, promotions: Ladder());
        party.AcquireItem(new ItemDefinitionId("token"));

        // One promotion over one class: the member standing where the rank continues from rises, the member
        // of the same class standing below it is named with where they stand, and the member of another class
        // is not part of it at all — "two sergeants" is one answer per member rather than one for the rank.
        PromotionResult given = progression.Promote("sergeant-captain", "quartermaster");
        Assert.True(given.IsGranted);
        Assert.Equal("Borin", Assert.Single(given.Granted).Name);
        PromotionDenial denied = Assert.Single(given.Denied);
        Assert.Equal("Ann", denied.Name);
        Assert.Contains("continues from rank 2", Assert.Single(denied.Missing), StringComparison.Ordinal);
        Assert.Equal(3, party.Members[1].Progression.ClassRank);
        Assert.Equal(1, party.Members[0].Progression.ClassRank);
        Assert.Equal(1, party.Members[2].Progression.ClassRank);

        // A member of another class is not part of the promotion at all, which is what makes the rank a fact
        // about a class rather than about a party.
        using PartyEntity mixed = PartyOf(Member("Cass", Captain));
        PartyProgression other = new(new TestRule(), mixed, promotions: Ladder());
        PromotionResult absent = other.Promote("recruit-sergeant", "quartermaster");
        Assert.False(absent.IsGranted);
        Assert.Empty(absent.Denied);
        Assert.Equal("promotion-class-absent", absent.Refusal!.Code);
    }

    [Fact]
    public void The_second_promotion_records_the_choice_and_leaves_the_other_alternative_untakeable()
    {
        using PartyEntity party = PartyOf(Member("Ann", Sergeant));
        PartyProgression progression = new(new TestRule(), party, promotions: Ladder());
        PartyMember member = party.Members[0];
        party.AcquireItem(new ItemDefinitionId("token"));

        // The two alternatives are two ranks from one class, and taking one is what removes the other: the
        // member belongs to the class the chosen rank named, and the rank it did not take promotes from a
        // class nobody in the party is any more. Nothing has to remember that a choice was made.
        PromotionResult light = progression.Promote("sergeant-captain", "quartermaster");
        Assert.True(light.IsGranted);
        Assert.Equal("light", Assert.Single(light.Granted).Choice);
        Assert.Equal("captain", member.Profile.Class.Value);
        Assert.Equal(3, member.Progression.ClassRank);

        PromotionResult dark = progression.Promote("sergeant-deserter", "quartermaster");
        Assert.False(dark.IsGranted);
        Assert.Equal("promotion-class-absent", dark.Refusal!.Code);
        Assert.Contains("Nobody in the party is a sergeant", dark.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal("captain", member.Profile.Class.Value);

        // The class the member now belongs to leads nowhere, which is what an earned third rank looks like
        // from the ladder's own side — and the rank it did not take is still stated, waiting for a member of
        // the class it promotes from rather than removed with the choice.
        Assert.True(Ladder().Ladder.IsTop(Captain));
        Assert.Empty(Ladder().Ladder.From(Captain));
        Assert.Equal(2, Ladder().Ladder.From(Sergeant).Count);
    }

    [Fact]
    public void The_choice_survives_the_partys_own_capture_and_restore()
    {
        using PartyEntity party = PartyOf(Member("Ann", Sergeant));
        PartyProgression progression = new(new TestRule(), party, promotions: Ladder());
        party.AcquireItem(new ItemDefinitionId("token"));
        Assert.True(progression.Promote("sergeant-deserter", "quartermaster").IsGranted);

        // A member's class and rank are what a save writes for it, so the choice the promotion recorded is
        // the class the character is restored into: a restored member is of the class it chose and stands at
        // the rank that came with it, with no separate choice to round-trip or to disagree with the class.
        PartySave save = party.Capture();
        using PartyEntity restored = new PartyEntityFactory().Restore(save);
        PartyMember back = restored.Members[0];
        Assert.Equal("deserter", back.Profile.Class.Value);
        Assert.Equal(3, back.Progression.ClassRank);
        Assert.True(restored.Effects.Has(new EffectId("promotion:sergeant-deserter")));

        // And the rank it did not take is still out of reach after the round trip, for the same reason.
        PartyProgression again = new(new TestRule(), restored, promotions: Ladder());
        Assert.Equal("promotion-class-absent", again.Promote("sergeant-captain", "quartermaster").Refusal!.Code);
    }

    [Fact]
    public void A_closed_ceiling_carries_the_games_own_reason_into_the_raise_and_the_panel()
    {
        using PartyEntity party = PartyOf(Member("Ann", Sergeant));
        PartyProgression progression = new(new TestRule(), party, new ClosedSkills(), Ladder());
        PartyMember member = party.Members[0];
        member.Skills.Learn(Blades, new SkillTier(1));

        // The ceiling a class imposes may be none, and a game that can say why says it: the refusal a raise
        // gets is the game's own sentence rather than one the kit composed about a class that holds no such
        // skill, which would say nothing about the choice that closed it.
        SkillRaisePlan plan = progression.Plan(member.Id, Blades);
        Assert.True(plan.Ceiling.IsNone);
        Assert.Equal("skill-closed-by-path", plan.Refusal!.Code);
        Assert.Contains("took the dark path", plan.Refusal.Message, StringComparison.Ordinal);

        SkillRaiseResult refused = progression.RaiseSkill(member.Id, Blades);
        Assert.False(refused.IsRaised);
        Assert.Equal("skill-closed-by-path", refused.Refusal!.Code);
        Assert.Equal(1, member.Skills.LevelOf(Blades));

        // The panel reads the same answer: the row publishes the sentence the raise would be refused with,
        // so a screen shows why a skill cannot grow rather than a bare limit.
        SkillsSnapshot skills = SkillsSnapshot.From(progression);
        SkillRowSnapshot row = Assert.Single(Assert.Single(skills.Members).Skills);
        Assert.Equal(0, row.CeilingLevel);
        Assert.Contains("took the dark path", row.Refusal, StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_with_no_ladder_and_a_party_at_the_top_of_one_are_different_facts()
    {
        using PartyEntity party = PartyOf(Member("Ann", Recruit));
        PartyProgression bare = new(new TestRule(), party);
        Assert.Null(bare.Promotions);

        PromotionResult refused = bare.Promote("recruit-sergeant", "quartermaster");
        Assert.False(refused.IsGranted);
        Assert.Equal("promotion-policy-missing", refused.Refusal!.Code);
        Assert.False(PromotionSnapshot.From(bare).Available);

        // A session that holds a ladder whose classes lead nowhere publishes an available block with no rank
        // rows: "the game states ranks, and this party is at the top of them" is not "there are no ranks".
        using PartyEntity topped = PartyOf(Member("Cass", new ClassId("marshal")));
        PartyProgression composed = new(new TestRule(), topped, promotions: Ladder());
        PromotionSnapshot snapshot = PromotionSnapshot.From(composed);
        Assert.True(snapshot.Available);
        Assert.Empty(Assert.Single(snapshot.Members).Promotions);
        Assert.Equal("none", snapshot.Outcome);
    }

    [Fact]
    public void The_projection_publishes_the_ladder_the_requirement_kinds_and_what_the_last_rank_did()
    {
        using PartyEntity party = PartyOf(Member("Ann", Sergeant));
        PartyProgression progression = new(new TestRule(), party, promotions: Ladder());

        // What the party may become, as the panel needs it: the class and rank each member holds and every
        // rank that class leads to, with each requirement's own kind and words. Nothing here is judged — a
        // ladder is what a rank asks for, and the report is what an attempt made of it.
        PromotionSnapshot before = PromotionSnapshot.From(progression);
        PromotionMemberSnapshot memberRow = Assert.Single(before.Members);
        Assert.Equal("Ann", memberRow.Name);
        Assert.Equal("sergeant", memberRow.Class);
        Assert.Equal(2, memberRow.Rank);
        Assert.Equal(2, memberRow.Promotions.Count);
        PromotionRankSnapshot light = memberRow.Promotions[0];
        Assert.Equal("sergeant-captain", light.Promotion);
        Assert.Equal("captain", light.ToClass);
        Assert.Equal(3, light.Rank);
        Assert.Equal("light", light.Choice);
        Assert.Equal("quartermaster", light.Giver);
        Assert.Equal("The quartermaster", light.GiverName);
        Assert.Equal(2, light.Requirements.Count);
        Assert.Equal("giver", light.Requirements[0].Kind);
        Assert.Equal("item", light.Requirements[1].Kind);
        Assert.Equal("token", light.Requirements[1].Text);
        Assert.Equal(1, light.Requirements[1].Amount);
        Assert.Equal("token", light.Requirements[1].Label);

        // What the last rank did, as the owner recorded it: who rose, from where to where, which alternative
        // they took, and what each of them met.
        party.AcquireItem(new ItemDefinitionId("token"));
        Assert.True(progression.Promote("sergeant-captain", "quartermaster").IsGranted);
        PromotionSnapshot after = PromotionSnapshot.From(progression);
        Assert.Equal("granted", after.Outcome);
        Assert.Equal("sergeant-captain", after.Promotion);
        Assert.Equal("captain", after.ToClass);
        Assert.Equal(3, after.Rank);
        Assert.Equal("light", after.Choice);
        PromotionGrantSnapshot grant = Assert.Single(after.Granted);
        Assert.Equal("sergeant", grant.FromClass);
        Assert.Equal(2, grant.FromRank);
        Assert.Equal("captain", grant.ToClass);
        Assert.Equal(3, grant.Rank);
        Assert.Equal(2, grant.Met.Count);
        Assert.Contains("rose to captain at rank 3", after.Message, StringComparison.Ordinal);
        Assert.Contains("captain", after.Message, StringComparison.Ordinal);
        Assert.Empty(after.Denied);
        Assert.Equal(string.Empty, after.Code);

        // A refusal travels the same way, with what was missing on the row rather than only in a sentence.
        using PartyEntity other = PartyOf(Member("Borin", Sergeant));
        PartyProgression refusing = new(new TestRule(), other, promotions: Ladder());
        Assert.False(refusing.Promote("sergeant-captain", "quartermaster").IsGranted);
        PromotionSnapshot refused = PromotionSnapshot.From(refusing);
        Assert.Equal("refused", refused.Outcome);
        Assert.Equal("promotion-requirements-unmet", refused.Code);
        PromotionDenialSnapshot denial = Assert.Single(refused.Denied);
        Assert.Equal("Borin", denial.Name);
        Assert.Contains("needs token, and the party carries 0", Assert.Single(denial.Missing), StringComparison.Ordinal);
        Assert.Contains("needs token, and the party carries 0", refused.Message, StringComparison.Ordinal);
    }

    /// <summary>A ladder of the test's own: two classes deep, with a second promotion that splits in two.</summary>
    private static TestPromotions Ladder() => new(
        new PromotionLadder(
        [
            new PromotionRank(
                "recruit-sergeant",
                Recruit,
                Sergeant,
                2,
                [
                    PromotionRequirement.FromGiver("quartermaster", "The quartermaster"),
                    PromotionRequirement.ForItem("token", 1, "token"),
                ],
                award: "promotion:recruit-sergeant",
                words: "'Bring me a token and I will make a sergeant of you.'"),
            new PromotionRank(
                "sergeant-captain",
                Sergeant,
                Captain,
                3,
                [
                    PromotionRequirement.FromGiver("quartermaster", "The quartermaster"),
                    PromotionRequirement.ForItem("token", 1, "token"),
                ],
                choice: "light",
                award: "promotion:sergeant-captain",
                words: "'The captain's rank is yours.'"),
            new PromotionRank(
                "sergeant-deserter",
                Sergeant,
                Deserter,
                3,
                [
                    PromotionRequirement.FromGiver("quartermaster", "The quartermaster"),
                    PromotionRequirement.ForItem("token", 1, "token"),
                ],
                choice: "dark",
                award: "promotion:sergeant-deserter",
                words: "'The deserter's road is shorter.'"),
        ]));

    /// <summary>A ladder whose one rank asks for all four kinds, so each can be judged in one place.</summary>
    private static TestPromotions FourKinds() => new(
        new PromotionLadder(
        [
            new PromotionRank(
                "recruit-sergeant",
                Recruit,
                Sergeant,
                2,
                [
                    PromotionRequirement.FromGiver("quartermaster", "quartermaster"),
                    PromotionRequirement.ForItem("token", 2, "token"),
                    PromotionRequirement.ForAward("victories", 3, "three victories"),
                    PromotionRequirement.ForQuest("12", "the errand the table states"),
                ]),
        ]));

    private static readonly SkillId Shield = new("shield");

    /// <summary>Two members, so a division and a per-member report are both visible.</summary>
    private static PartyEntity PartyOf(params MemberCreation[] members) =>
        new PartyEntityFactory().Create(new PartyCreation(members, coins: 0, foodPortions: 0, reputation: 0, fame: 0));

    private static MemberCreation Member(string name, ClassId characterClass, int rank = 0) => new(new PartyMemberSeed(
        name,
        new RaceId("testfolk"),
        characterClass,
        [new AttributeScore(new AttributeId("vigour"), 12)],
        skills: [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: rank > 0 ? rank : characterClass.Value == "recruit" ? 1 : 2,
        conditions: [],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    /// <summary>The ladder a test states, as the session's own rule seam carries it.</summary>
    private sealed class TestPromotions(PromotionLadder ladder) : IPromotionRule
    {
        public PromotionLadder Ladder { get; } = ladder;
    }

    /// <summary>
    /// The policy these tests run on, with a curve that never advances a level: what a promotion changes is
    /// the class and the rank, and a level rising underneath it would be a second thing under test.
    /// </summary>
    private sealed class TestRule : IProgressionRule
    {
        public long ExperienceForLevel(int level) => long.MaxValue;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }

    /// <summary>
    /// A skill policy whose one skill is closed for the game's own reason, which is the case a ceiling's
    /// reason exists for: the kit would otherwise say only that the class holds no such skill.
    /// </summary>
    private sealed class ClosedSkills : ISkillRule
    {
        public SkillCatalog Catalog { get; } = new(
        [
            new SkillDefinition(Blades, SkillBlock.Weapon),
            new SkillDefinition(Shield, SkillBlock.Armour),
        ]);

        public SkillCeiling Ceiling(PartyMember member, SkillId skill) => string.Equals(skill.Value, "blades", StringComparison.Ordinal)
            ? SkillCeiling.None with
            {
                Reason = new PartyRefusal(
                    "skill-closed-by-path",
                    $"{member.Profile.Name} took the dark path of the {member.Profile.Class}: it takes Dark and leaves Light to the other alternative."),
            }
            : new SkillCeiling(60, new SkillTier(4));

        public int RaiseCost(SkillEntry skill, int levels) => levels;

        public string TierName(SkillTier tier) => tier.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
