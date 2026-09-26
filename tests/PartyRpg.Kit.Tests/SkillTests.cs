using System.Text;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Skills;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The kit's own half of skills: the catalog content's rows are read into, the ceiling vocabulary, the one
/// path that spends a skill point, and the control a screen's raise arrives on.
/// </summary>
/// <remarks>
/// The numbers are the ruleset's, so the rules these tests run on are their own — a catalog of three skills, a
/// ceiling of twelve levels at the master rung, and two points a level — and what is under test is that the
/// owner asks for every one of them rather than deciding any. What this game's own table answers, and what a
/// mastery teacher requires, are proved in the host suite, which is the one suite allowed to read the ruleset.
/// </remarks>
public sealed class SkillTests
{
    private static readonly SkillId Blades = new("blades");
    private static readonly SkillId Wards = new("wards");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");

    [Fact]
    public void A_catalog_reads_every_row_content_declares_and_says_which_blocks_do_not_carry_it()
    {
        SkillCatalog catalog = new(
        [
            new SkillDefinition(Blades, SkillBlock.Weapon),
            new SkillDefinition(new SkillId("mail"), SkillBlock.Armour),
            new SkillDefinition(Wards, SkillBlock.Magic),
            new SkillDefinition(new SkillId("haggling"), SkillBlock.Miscellaneous),
            new SkillDefinition(new SkillId("relic"), SkillBlock.Unused),
        ]);

        // Every row content declares is in the catalog, the ones no block carries included: a row this game
        // does not use is reported rather than dropped, because that is the difference between data this
        // product chose not to use and data it never had.
        Assert.Equal(5, catalog.Count);
        Assert.Equal(4, catalog.Used.Count);
        SkillDefinition unused = Assert.Single(catalog.Unused);
        Assert.Equal("relic", unused.Id.Value);
        Assert.False(unused.IsUsed);

        // The blocks are counted, which is what an operator checking a shipped table reads.
        Assert.Equal(1, catalog.CountOf(SkillBlock.Weapon));
        Assert.Equal(1, catalog.CountOf(SkillBlock.Armour));
        Assert.Equal(1, catalog.CountOf(SkillBlock.Magic));
        Assert.Equal(1, catalog.CountOf(SkillBlock.Miscellaneous));
        Assert.Equal(1, catalog.CountOf(SkillBlock.Unused));

        // A skill content does not declare reads as belonging to no block rather than throwing: a character,
        // a rule, and a command can all name a skill this build has never heard of.
        Assert.Equal(SkillBlock.Unused, catalog.Read(new SkillId("nothing")).Block);
        Assert.False(catalog.Declares(new SkillId("nothing")));

        // One identity, one row: two rows claiming a skill would make the level a character has ambiguous.
        Assert.Throws<ArgumentException>(() => new SkillCatalog(
        [
            new SkillDefinition(Blades, SkillBlock.Weapon),
            new SkillDefinition(Blades, SkillBlock.Magic),
        ]));
    }

    [Fact]
    public void The_spend_path_raises_a_skill_and_charges_what_the_rule_prices()
    {
        using PartyEntity party = Party(skillPoints: 9);
        PartyMember student = party.Members[0];
        PartyProgression progression = new(new Curve(), party, new Rules());

        SkillRaiseResult raised = progression.RaiseSkill(student.Id, Blades, levels: 2);

        Assert.True(raised.IsRaised);
        Assert.Equal(3, raised.Level);
        Assert.Equal(4, raised.Points);
        Assert.Equal(5, raised.Remaining);
        Assert.Equal(3, student.Skills.LevelOf(Blades));
        Assert.Equal(4, student.Skills.Entries.Single().PointsSpent);
        Assert.Equal(5, student.Progression.SkillPoints);

        // The same answer a screen is shown before it asks: the plan and the raise are one arithmetic.
        Assert.Same(raised, progression.LastRaise);
        SkillRaisePlan plan = progression.Plan(student.Id, Blades);
        Assert.True(plan.IsPossible);
        // This test's rule charges two points a level, so the next one costs two of the five left.
        Assert.Equal(2, plan.Points);
        Assert.Equal(3, plan.Level);
        Assert.Equal(4, plan.Reached);
        Assert.Equal(3, student.Skills.LevelOf(Blades));
    }

    [Fact]
    public void A_raise_past_the_ceiling_is_refused_with_the_limit_named()
    {
        using PartyEntity party = Party(skillPoints: 40, level: 1);
        PartyMember student = party.Members[0];
        PartyProgression progression = new(new Curve(), party, new Rules { MaximumLevel = 12 });

        // Nine levels fit inside the ceiling and the tenth does not: the raise is refused whole rather than
        // taking the levels it could.
        Assert.True(progression.RaiseSkill(student.Id, Blades, levels: 9).IsRaised);
        Assert.Equal(10, student.Skills.LevelOf(Blades));

        SkillRaisePlan plan = progression.Plan(student.Id, Blades, levels: 3);
        Assert.False(plan.IsPossible);
        Assert.Equal("skill-ceiling-reached", plan.Refusal!.Code);
        Assert.Contains("level 10", plan.Refusal.Message, StringComparison.Ordinal);
        Assert.Contains("12", plan.Refusal.Message, StringComparison.Ordinal);
        Assert.Contains("fighter", plan.Refusal.Message, StringComparison.Ordinal);

        SkillRaiseResult refused = progression.RaiseSkill(student.Id, Blades, levels: 3);
        Assert.False(refused.IsRaised);
        Assert.Equal("skill-ceiling-reached", refused.Refusal!.Code);
        Assert.Equal(10, student.Skills.LevelOf(Blades));
        // The pool is untouched by a refusal: nine levels at this test's two points each left twenty-two.
        Assert.Equal(22, student.Progression.SkillPoints);
    }

    [Fact]
    public void A_raise_the_pool_cannot_cover_is_refused_with_what_it_costs_and_what_remains()
    {
        using PartyEntity party = Party(skillPoints: 3);
        PartyMember student = party.Members[0];
        PartyProgression progression = new(new Curve(), party, new Rules());

        SkillRaisePlan plan = progression.Plan(student.Id, Blades, levels: 2);
        Assert.False(plan.IsPossible);
        Assert.Equal("insufficient-skill-points", plan.Refusal!.Code);
        Assert.Contains("4 skill point(s)", plan.Refusal.Message, StringComparison.Ordinal);
        Assert.Contains("3 remain", plan.Refusal.Message, StringComparison.Ordinal);

        SkillRaiseResult refused = progression.RaiseSkill(student.Id, Blades, levels: 2);
        Assert.False(refused.IsRaised);
        Assert.Equal(3, student.Progression.SkillPoints);
        Assert.Equal(1, student.Skills.LevelOf(Blades));
    }

    [Fact]
    public void A_skill_the_member_has_not_learned_is_refused_rather_than_raised()
    {
        using PartyEntity party = Party(skillPoints: 10);
        PartyMember student = party.Members[0];
        PartyProgression progression = new(new Curve(), party, new Rules());

        // Learning is a lesson's business, not a point's: what is missing is a teacher.
        SkillRaisePlan plan = progression.Plan(student.Id, Wards);
        Assert.False(plan.IsPossible);
        Assert.Equal("skill-not-learned", plan.Refusal!.Code);
        Assert.Equal(0, plan.Level);

        SkillRaiseResult refused = progression.RaiseSkill(student.Id, Wards);
        Assert.Equal("skill-not-learned", refused.Refusal!.Code);
        Assert.Equal(10, student.Progression.SkillPoints);
    }

    [Fact]
    public void A_class_that_may_not_hold_the_skill_at_all_is_refused_with_the_class_named()
    {
        using PartyEntity party = Party(skillPoints: 10);
        PartyProgression progression = new(new Curve(), party, new Rules { None = true });

        SkillRaiseResult refused = progression.RaiseSkill(party.Members[0].Id, Blades);
        Assert.Equal("skill-not-permitted", refused.Refusal!.Code);
        Assert.Contains("fighter", refused.Refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_session_whose_ruleset_states_no_skill_policy_refuses_a_raise_by_name()
    {
        using PartyEntity party = Party(skillPoints: 10);
        PartyProgression progression = new(new Curve(), party);

        // A ruleset that never said how far a skill may grow has not said a raise is free: the refusal names
        // the missing policy rather than raising for nothing.
        SkillRaiseResult refused = progression.RaiseSkill(party.Members[0].Id, Blades);
        Assert.Equal("skill-policy-missing", refused.Refusal!.Code);
        Assert.Equal(1, party.Members[0].Skills.LevelOf(Blades));
    }

    [Fact]
    public void A_raise_control_reads_the_member_the_skill_and_the_levels_a_screen_names()
    {
        SkillRaiseInput input = new(new SkillRaiseIntentNames(SkillRaiseActions.Raise, "test.actions"));

        IReadOnlyList<SkillRaiseRequest> raises = input.Read(
        [
            Payload("""{"action":"party.raise-skill","member":2,"skill":"Sword","levels":2}"""),
            // Another contract's action is not ours, and neither is a payload that names no skill.
            Payload("""{"action":"service.teach","member":0,"skill":"Sword"}""", "other.actions"),
            Payload("""{"action":"party.raise-skill","member":0}"""),
            // A malformed payload carries nothing rather than throwing: an input channel must not fail on
            // hostile bytes.
            Payload("""{"action":"party.raise-skill","member":"""),
            Payload("""{"action":"party.raise-skill","member":1,"skill":"Fire"}"""),
        ]);

        Assert.Equal(2, raises.Count);
        Assert.Equal(2, raises[0].Member);
        Assert.Equal("Sword", raises[0].Skill.Value);
        Assert.Equal(2, raises[0].Levels);
        Assert.Equal(1, raises[1].Member);
        Assert.Equal("Fire", raises[1].Skill.Value);
        // A raise that states no levels is a request to raise the skill: the owner words what it will do.
        Assert.Equal(1, raises[1].Levels);
    }

    /// <summary>One payload on a declared contract, as the companion sends it.</summary>
    private static ProductInputEvent Payload(string json, string contract = "test.actions") => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(contract), Encoding.UTF8.GetBytes(json));

    /// <summary>One member holding one skill and a pool of points, so a raise has something to spend.</summary>
    private static PartyEntity Party(int skillPoints, int level = 1) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                new MemberCreation(new PartyMemberSeed(
                    "Ann",
                    TestRace,
                    Fighter,
                    [new AttributeScore(new AttributeId("vigour"), 12)],
                    [new SkillEntry(Blades, 1, new SkillTier(1), 0)],
                    spells: [],
                    experience: 0,
                    level: level,
                    skillPoints: skillPoints,
                    classRank: 1,
                    conditions: [],
                    hitPoints: ResourcePool.Full(20),
                    spellPoints: ResourcePool.Full(0))),
            ],
            coins: 0,
            foodPortions: 0,
            reputation: 0,
            fame: 0));

    /// <summary>
    /// The skill policy these tests run on: three rows content declares, a ceiling the test may lower, and
    /// this test's own two points a level.
    /// </summary>
    private sealed class Rules : ISkillRule
    {
        public SkillCatalog Catalog { get; } = new(
        [
            new SkillDefinition(Blades, SkillBlock.Weapon),
            new SkillDefinition(Wards, SkillBlock.Magic),
            new SkillDefinition(new SkillId("mail"), SkillBlock.Armour),
        ]);

        /// <summary>The highest level the test's ceiling permits, well above what a raise needs by default.</summary>
        internal int MaximumLevel { get; init; } = 60;

        /// <summary>Whether the test's class may hold the skill at all.</summary>
        internal bool None { get; init; }

        public SkillCeiling Ceiling(PartyMember member, SkillId skill) =>
            None ? SkillCeiling.None : new SkillCeiling(MaximumLevel, new SkillTier(4));

        public int RaiseCost(SkillEntry skill, int levels) => 2 * levels;

        public string TierName(SkillTier tier) => tier.Value == 0 ? "untrained" : "trained";
    }

    /// <summary>A growth curve flat enough that a test can award its way to a level in one step.</summary>
    private sealed class Curve : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) => [];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }
}
