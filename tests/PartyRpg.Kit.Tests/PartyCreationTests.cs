using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The party-creation flow: the steps it takes, what each step refuses, how the default party is applied,
/// and how a finished creation reaches the factory and a save.
/// </summary>
/// <remarks>
/// Every definition here is the test's own — races, classes, portraits, skills and prices — because that is
/// the seam: the kit knows no race, class or skill name, so a test states its own and demands that the same
/// mechanism serve it. The pool is five points, which is small enough to spend by hand and still lets a
/// default overspend it.
/// </remarks>
public sealed class PartyCreationTests
{
    private static readonly AttributeId Vigour = new("vigour");
    private static readonly AttributeId Wit = new("wit");
    private static readonly RaceId Testfolk = new("testfolk");
    private static readonly RaceId Stonefolk = new("stonefolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly ClassId Adept = new("adept");
    private static readonly PortraitId FolkA = new("folk-a");
    private static readonly PortraitId FolkB = new("folk-b");
    private static readonly PortraitId StoneA = new("stone-a");
    private static readonly PortraitId StoneB = new("stone-b");
    private static readonly SkillId Blades = new("blades");
    private static readonly SkillId Bulwark = new("bulwark");
    private static readonly SkillId Axes = new("axes");
    private static readonly SkillId Bows = new("bows");
    private static readonly SkillId Lore = new("lore");
    private static readonly SkillId Aim = new("aim");
    private static readonly SkillId Wards = new("wards");

    [Fact]
    public void Creation_walks_its_steps_in_order_and_refuses_a_choice_made_out_of_step()
    {
        PartyCreationFlow flow = new(Options);

        // Nothing is answered at the start, and an action that belongs to a later step is refused by name
        // rather than quietly obeyed.
        Assert.Equal(CreationStep.Portrait, flow.Step);
        Assert.Null(flow.Member(0).Portrait);
        Assert.Equal("portrait-unchosen", flow.Advance()!.Code);
        Assert.Equal("creation-step", flow.SelectClass(Fighter)!.Code);
        Assert.Equal("creation-step", flow.SetName("Ann")!.Code);
        Assert.Equal("creation-step", flow.RaiseAttribute(Vigour)!.Code);
        Assert.Equal("creation-step", flow.ChooseSkill(Axes)!.Code);
        Assert.Contains("steps are taken in order", flow.SelectClass(Fighter)!.Message, StringComparison.Ordinal);
        Assert.Equal("portrait-unknown", flow.SelectPortrait(new PortraitId("nobody"))!.Code);

        Assert.Null(flow.SelectPortrait(FolkA));
        Assert.Null(flow.Advance());
        Assert.Equal(CreationStep.Class, flow.Step);
        Assert.Null(flow.SelectClass(Fighter));
        Assert.Null(flow.Advance());
        Assert.Equal(CreationStep.Name, flow.Step);
        Assert.Null(flow.SetName("Ann"));
        Assert.Null(flow.Advance());
        Assert.Equal(CreationStep.Attributes, flow.Step);
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Equal(6, AttributeOf(flow, Vigour));
    }

    [Fact]
    public void A_member_that_skipped_a_step_cannot_be_confirmed_past_it()
    {
        // A portrait is chosen but no class: confirming the class step names what is missing instead of
        // letting the member walk on into a step that depends on a class.
        PartyCreationFlow withoutClass = new(Options);
        Assert.Null(withoutClass.SelectPortrait(FolkA));
        Assert.Null(withoutClass.Advance());
        PartyRefusal classUnchosen = withoutClass.Advance()!;
        Assert.Equal("class-unchosen", classUnchosen.Code);
        Assert.Contains("which skills may be chosen", classUnchosen.Message, StringComparison.Ordinal);
        Assert.Equal(CreationStep.Class, withoutClass.Step);

        PartyCreationFlow withoutName = new(Options);
        Assert.Null(withoutName.SelectPortrait(FolkA));
        Assert.Null(withoutName.Advance());
        Assert.Null(withoutName.SelectClass(Fighter));
        Assert.Null(withoutName.Advance());
        PartyRefusal nameBlank = withoutName.Advance()!;
        Assert.Equal("name-blank", nameBlank.Code);
        Assert.Contains("every member of the party is named", nameBlank.Message, StringComparison.Ordinal);
        Assert.Equal(CreationStep.Name, withoutName.Step);
    }

    [Fact]
    public void The_attribute_pool_is_spent_exactly_before_the_step_is_confirmed()
    {
        PartyCreationFlow flow = AttributesStep();

        // The pool is five points and all five must be spent; an unfinished pool names how many are left.
        PartyRefusal unspent = flow.Advance()!;
        Assert.Equal("attribute-pool-unspent", unspent.Code);
        Assert.Contains("spent exactly", unspent.Message, StringComparison.Ordinal);
        Assert.Contains("5 remain", unspent.Message, StringComparison.Ordinal);
        Assert.Equal(5, flow.PoolRemaining);

        // Vigour moves one point per pool point; wit gives two points for one.
        Assert.Null(flow.RaiseAttribute(Wit));
        Assert.Equal(8, AttributeOf(flow, Wit));
        Assert.Equal(4, flow.PoolRemaining);
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Equal(9, AttributeOf(flow, Vigour));
        Assert.Equal(1, flow.PoolRemaining);
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Equal(10, AttributeOf(flow, Vigour));
        Assert.Equal(0, flow.PoolRemaining);

        Assert.Null(flow.Advance());
        Assert.Equal(CreationStep.Skills, flow.Step);
        Assert.Null(flow.ChooseSkill(Axes));
        Assert.Null(flow.ChooseSkill(Bows));
        Assert.Null(flow.Advance());
        Assert.True(flow.Member(0).IsComplete);
    }

    [Fact]
    public void An_attribute_cannot_be_bought_past_its_ceiling_its_floor_or_the_pool()
    {
        PartyCreationFlow flow = AttributesStep();

        for (int click = 0; click < 4; click++) Assert.Null(flow.RaiseAttribute(Vigour));
        PartyRefusal ceiling = flow.RaiseAttribute(Vigour)!;
        Assert.Equal("attribute-ceiling", ceiling.Code);
        Assert.Contains("vigour", ceiling.Message, StringComparison.Ordinal);
        Assert.Contains("at most to 10", ceiling.Message, StringComparison.Ordinal);

        // Lowering an attribute refunds what raising it charged, which is why the exchange is exact: the
        // points that went in come back out.
        Assert.Equal(1, flow.PoolRemaining);
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Equal(9, AttributeOf(flow, Vigour));
        Assert.Equal(2, flow.PoolRemaining);
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Equal(7, AttributeOf(flow, Vigour));
        Assert.Equal(4, flow.PoolRemaining);
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Equal(5, AttributeOf(flow, Vigour));
        Assert.Null(flow.LowerAttribute(Vigour));
        Assert.Equal(4, AttributeOf(flow, Vigour));
        PartyRefusal floor = flow.LowerAttribute(Vigour)!;
        Assert.Equal("attribute-floor", floor.Code);
        Assert.Contains("at most to 4", floor.Message, StringComparison.Ordinal);

        // A race whose points cost two each cannot buy one with the single point that is left.
        PartyCreationFlow costly = AttributesStep(StoneA);
        Assert.Null(costly.RaiseAttribute(Vigour));
        Assert.Null(costly.RaiseAttribute(Vigour));
        Assert.Equal(1, costly.PoolRemaining);
        PartyRefusal short1 = costly.RaiseAttribute(Vigour)!;
        Assert.Equal("attribute-pool-short", short1.Code);
        Assert.Contains("costs 2 of the 1 attribute point left", short1.Message, StringComparison.Ordinal);

        PartyCreationFlow spent = AttributesStep(StoneA);
        Assert.Null(spent.RaiseAttribute(Vigour));
        Assert.Null(spent.RaiseAttribute(Vigour));
        Assert.Null(spent.RaiseAttribute(Wit));
        Assert.Equal(0, spent.PoolRemaining);
        Assert.Contains("of the 0 attribute points left", spent.RaiseAttribute(Vigour)!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chosen_skill_must_be_one_the_class_offers()
    {
        PartyCreationFlow flow = SkillsStep();

        PartyRefusal notLegal = flow.ChooseSkill(Wards)!;
        Assert.Equal("skill-not-legal", notLegal.Code);
        Assert.Contains("'wards' is not a skill the fighter class may learn at creation", notLegal.Message, StringComparison.Ordinal);
        Assert.Contains("the class decides which skills may be chosen", notLegal.Message, StringComparison.Ordinal);

        PartyRefusal fixedSkill = flow.ChooseSkill(Blades)!;
        Assert.Equal("skill-fixed", fixedSkill.Code);
        Assert.Contains("the two chosen skills are picked from the rest", fixedSkill.Message, StringComparison.Ordinal);

        Assert.Null(flow.ChooseSkill(Axes));
        Assert.Equal("skill-already-chosen", flow.ChooseSkill(Axes)!.Code);
        Assert.Null(flow.ChooseSkill(Bows));
        Assert.Equal("skills-complete", flow.ChooseSkill(Aim)!.Code);
        Assert.Equal("skill-not-chosen", flow.RemoveSkill(Lore)!.Code);
        Assert.Null(flow.RemoveSkill(Bows));
        Assert.Null(flow.ChooseSkill(Aim));
        Assert.Null(flow.Advance());
        Assert.True(flow.Member(0).IsComplete);
    }

    [Fact]
    public void The_portrait_decides_the_race_and_re_derives_the_attribute_scores()
    {
        PartyCreationFlow flow = new(Options);
        Complete(flow, FolkA, Fighter, "Ann", [(Vigour, 4), (Wit, 1)], Axes, Bows);
        Assert.Equal(Testfolk, flow.Member(0).Race);
        Assert.Equal(10, AttributeOf(flow, Vigour));
        Assert.Equal(8, AttributeOf(flow, Wit));
        Assert.Equal(0, flow.Member(0).PoolRemaining);

        // A finished member can be customized again: its answers stay, and the portrait is the first step.
        Assert.Null(flow.SelectMember(0));
        Assert.Equal(CreationStep.Portrait, flow.Step);
        Assert.False(flow.IsComplete);

        // The portrait is what fixes the race, so changing it re-derives the attribute table and returns
        // every spent point to the pool; the class and the chosen skills are the class's business and stay.
        Assert.Null(flow.SelectPortrait(StoneA));
        Assert.Equal(Stonefolk, flow.Member(0).Race);
        Assert.Equal(8, AttributeOf(flow, Vigour));
        Assert.Equal(5, AttributeOf(flow, Wit));
        Assert.Equal(5, flow.PoolRemaining);
        Assert.Equal(Fighter, flow.Member(0).Class);
        Assert.Equal(new[] { Axes, Bows }, flow.Member(0).ChosenSkills);

        // The other face of the same race keeps the race and its table.
        Assert.Null(flow.SelectPortrait(StoneB));
        Assert.Equal(Stonefolk, flow.Member(0).Race);
        Assert.Equal(8, AttributeOf(flow, Vigour));
    }

    [Fact]
    public void A_name_must_be_well_formed()
    {
        PartyCreationFlow flow = NameStep();

        Assert.Equal("name-blank", flow.SetName("   ")!.Code);
        Assert.Equal("name-too-long", flow.SetName("Annabellex")!.Code);
        Assert.Equal("name-invalid", flow.SetName("An\u0007na")!.Code);
        Assert.Contains("at most 8 characters", flow.SetName("Annabellex")!.Message, StringComparison.Ordinal);

        // A stray space around a name is not part of the name; the trimmed value is what the character keeps.
        Assert.Null(flow.SetName("  Ann  "));
        Assert.Equal("Ann", flow.Member(0).Name);
    }

    [Fact]
    public void Changing_the_class_takes_back_the_skills_it_may_not_teach()
    {
        PartyCreationFlow flow = new(Options);
        Complete(flow, FolkA, Fighter, "Ann", [(Vigour, 4), (Wit, 1)], Axes, Bows);
        Assert.Null(flow.SelectMember(0));

        // Legality is the class's answer, so a class change cannot leave a skill behind that the new class
        // may not learn: the picks are taken back with it.
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(Adept));
        Assert.Empty(flow.Member(0).ChosenSkills);
        Assert.Equal(new[] { Wards }, flow.Member(0).FixedSkills);

        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName("Ann"));
        Assert.Null(flow.Advance());
        Assert.Equal(0, flow.PoolRemaining);
        Assert.Null(flow.Advance());
        Assert.Equal("skill-not-legal", flow.ChooseSkill(Axes)!.Code);
        Assert.Null(flow.ChooseSkill(Aim));
        Assert.Null(flow.ChooseSkill(Lore));
    }

    [Fact]
    public void An_unfinished_party_cannot_be_created()
    {
        PartyCreationFlow flow = new(Options);
        Assert.False(flow.IsComplete);
        InvalidOperationException nothing = Assert.Throws<InvalidOperationException>(() => flow.ToCreation());
        Assert.Contains("member 1 is at the Portrait step", nothing.Message, StringComparison.Ordinal);

        Complete(flow, FolkA, Fighter, "Ann", [(Vigour, 4), (Wit, 1)], Axes, Bows);
        Assert.False(flow.IsComplete);
        InvalidOperationException half = Assert.Throws<InvalidOperationException>(() => flow.ToCreation());
        Assert.Contains("member 2 is at the Portrait step", half.Message, StringComparison.Ordinal);
        Assert.Contains("Every member must be finished and confirmed", half.Message, StringComparison.Ordinal);

        Complete(flow, StoneB, Adept, "Bo", [(Vigour, 2), (Wit, 1)], Aim, Lore);
        Assert.True(flow.IsComplete);
        Assert.Equal(2, flow.ToCreation().Members.Count);
    }

    [Fact]
    public void The_default_party_is_applied_through_the_same_validation_and_a_broken_one_is_refused()
    {
        PartyCreationFlow flow = new(Options, Defaults);

        // The default arrives as a finished party, applied through the same steps a player takes.
        Assert.True(flow.IsComplete);
        Assert.True(flow.HasDefault);
        Assert.Equal(new[] { "Ann", "Bo" }, MembersOf(flow).Select(member => member.Name));
        Assert.Equal(new[] { 0, 0 }, MembersOf(flow).Select(member => member.PoolRemaining));
        Assert.Equal(FolkA, flow.Member(0).Portrait);
        Assert.Equal(new[] { Axes, Bows }, flow.Member(0).ChosenSkills);
        Assert.Equal(0, flow.MemberIndex);

        // Asking for the default again re-applies it through the same validation.
        Assert.Null(flow.SelectMember(0));
        Assert.Null(flow.ApplyDefault());
        Assert.True(flow.IsComplete);
        Assert.Equal(0, flow.Member(0).PoolRemaining);

        // Every way a player can break a rule is a way a default can break it, and the refusal names the
        // same rule with the default's member attached.
        ArgumentException overspent = Assert.Throws<ArgumentException>(
            () => new PartyCreationFlow(Options, BrokenDefault(attributeSpend: true)));
        Assert.Contains("attribute-pool-short", overspent.Message, StringComparison.Ordinal);
        Assert.Contains("The default party's member 1 broke a creation rule", overspent.Message, StringComparison.Ordinal);

        ArgumentException illegal = Assert.Throws<ArgumentException>(
            () => new PartyCreationFlow(Options, BrokenDefault(skill: "axes")));
        Assert.Contains("skill-not-legal", illegal.Message, StringComparison.Ordinal);
        Assert.Contains("'axes' is not a skill the adept class may learn at creation", illegal.Message, StringComparison.Ordinal);

        ArgumentException unreachable = Assert.Throws<ArgumentException>(
            () => new PartyCreationFlow(Options, BrokenDefault(unreachableAttribute: true)));
        Assert.Contains("attribute-unreachable", unreachable.Message, StringComparison.Ordinal);

        PartyCreationFlow withoutDefault = new(Options);
        Assert.False(withoutDefault.HasDefault);
        Assert.Equal("no-default", withoutDefault.ApplyDefault()!.Code);
    }

    [Fact]
    public void A_created_party_is_what_the_factory_builds_and_what_a_save_round_trips()
    {
        PartyCreationFlow flow = new(Options, Defaults);
        PartyCreation creation = flow.ToCreation();
        Assert.Equal(2, creation.Members.Count);
        Assert.Equal(25, creation.Coins);
        Assert.Equal(3, creation.FoodPortions);
        Assert.Equal(ProvisionUnit.Portions, creation.FoodUnit);
        Assert.Equal(2, creation.Reputation);
        Assert.Equal(1, creation.Fame);

        // Creation mints nothing: the members it describes carry no identity until the factory creates them.
        PartyEntityFactory factory = new();
        using PartyEntity party = factory.Create(creation);

        Assert.Equal(new PartyMemberId(1), party.Members[0].Id);
        Assert.Equal(new PartyMemberId(2), party.Members[1].Id);
        Assert.Equal(new[] { "Ann", "Bo" }, party.Members.Select(member => member.Profile.Name));
        Assert.Equal(new[] { Testfolk, Stonefolk }, party.Members.Select(member => member.Profile.Race));
        Assert.Equal(new[] { Fighter, Adept }, party.Members.Select(member => member.Profile.Class));
        Assert.Equal(10, party.Members[0].Attributes[Vigour]);
        Assert.Equal(8, party.Members[0].Attributes[Wit]);
        Assert.Equal(10, party.Members[1].Attributes[Vigour]);
        Assert.Equal(6, party.Members[1].Attributes[Wit]);
        Assert.Equal(
            new[] { Blades, Bulwark, Axes, Bows },
            party.Members[0].Skills.Entries.Select(entry => entry.Skill));
        Assert.All(party.Members[0].Skills.Entries, entry => Assert.Equal(new SkillTier(1), entry.Tier));
        Assert.All(party.Members[0].Skills.Entries, entry => Assert.Equal(1, entry.Level));
        Assert.Equal(12, party.Members[0].Resources.HitPoints.Maximum);
        Assert.Equal(0, party.Members[0].Resources.SpellPoints.Maximum);
        Assert.Equal(8, party.Members[1].Resources.HitPoints.Maximum);
        Assert.Equal(6, party.Members[1].Resources.SpellPoints.Maximum);
        Assert.Equal(1, party.Members[0].Progression.Level);
        Assert.Equal(1, party.Members[0].Progression.ClassRank);
        Assert.Equal(25, party.Purse.Coins);
        Assert.Equal(3, party.Food.Portions);

        // The durable identities a save carries are stable: creating, capturing and restoring twice gives
        // one party by identity, which is the shape the save schema round-trips.
        PartySave save = party.Capture();
        using PartyEntity first = factory.Restore(save);
        using PartyEntity second = factory.Restore(save);
        Assert.Equal(party.Members.Select(member => member.Id), first.Members.Select(member => member.Id));
        Assert.Equal(first.Members.Select(member => member.Id), second.Members.Select(member => member.Id));
        Assert.Equal(save.Members.Select(member => member.Seed.Name), first.Members.Select(member => member.Profile.Name));
        Assert.Equal(save.Members.Select(member => member.Seed.Class), first.Members.Select(member => member.Profile.Class));
        Assert.Equal(save.NextMemberValue, first.Identity.NextMemberValue);
        Assert.Equal(3UL, save.NextMemberValue);
    }

    [Fact]
    public void Reopening_a_finished_member_starts_its_steps_again()
    {
        PartyCreationFlow flow = new(Options, Defaults);
        Assert.Null(flow.SelectMember(0));
        Assert.Equal(CreationStep.Portrait, flow.Step);
        Assert.False(flow.IsComplete);
        Assert.Throws<InvalidOperationException>(() => flow.ToCreation());

        // Walking the steps again keeps every answer and confirms the member without changing it.
        Assert.Equal(FolkA, flow.Member(0).Portrait);
        Assert.Null(flow.Advance());
        Assert.Null(flow.Advance());
        Assert.Equal("Ann", flow.Member(0).Name);
        Assert.Null(flow.Advance());
        Assert.Equal(0, flow.PoolRemaining);
        Assert.Null(flow.Advance());
        Assert.Equal(new[] { Axes, Bows }, flow.Member(0).ChosenSkills);
        Assert.Null(flow.Advance());
        Assert.True(flow.Member(0).IsComplete);
        Assert.True(flow.IsComplete);

        // Confirming a member that is already finished changes nothing.
        Assert.Null(flow.Advance());
        Assert.True(flow.IsComplete);
        Assert.Equal("member-unknown", flow.SelectMember(2)!.Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => flow.Member(2));
    }

    [Fact]
    public void Choices_that_contradict_each_other_are_refused_when_creation_is_offered()
    {
        // A portrait drawn as a race creation does not offer would leave a player with a face and no
        // attribute table behind it.
        ArgumentException unknownRace = Assert.Throws<ArgumentException>(() => new PartyCreationOptions(
            1,
            [TestfolkRace()],
            [FighterClass()],
            [new CreationPortrait(StoneA, Stonefolk, "Stone A")],
            attributePool: 5,
            chosenSkillCount: 2,
            nameMaximumLength: 8,
            startingSkillTier: new SkillTier(1),
            startingLevel: 1));
        Assert.Contains("which creation does not offer", unknownRace.Message, StringComparison.Ordinal);

        ArgumentException repeated = Assert.Throws<ArgumentException>(() => new PartyCreationOptions(
            1,
            [TestfolkRace()],
            [FighterClass(), FighterClass()],
            [new CreationPortrait(FolkA, Testfolk, "Folk A")],
            attributePool: 5,
            chosenSkillCount: 2,
            nameMaximumLength: 8,
            startingSkillTier: new SkillTier(1),
            startingLevel: 1));
        Assert.Contains("more than once", repeated.Message, StringComparison.Ordinal);

        ArgumentException empty = Assert.Throws<ArgumentException>(() => new PartyCreationOptions(
            1,
            [],
            [FighterClass()],
            [new CreationPortrait(FolkA, Testfolk, "Folk A")],
            attributePool: 5,
            chosenSkillCount: 2,
            nameMaximumLength: 8,
            startingSkillTier: new SkillTier(1),
            startingLevel: 1));
        Assert.Contains("offers no race", empty.Message, StringComparison.Ordinal);

        // A class that fixes a skill and offers it again would grant the same skill twice.
        ArgumentException bothWays = Assert.Throws<ArgumentException>(() => new CreationClass(
            Fighter,
            "fighter",
            [Blades],
            [Blades],
            startingHitPoints: 1,
            startingSpellPoints: 0,
            startingRank: 1));
        Assert.Contains("also offers it as a choice", bothWays.Message, StringComparison.Ordinal);
    }

    /// <summary>The choices creation offers in this suite.</summary>
    private static PartyCreationOptions Options { get; } = new(
        memberCount: 2,
        races: [TestfolkRace(), StonefolkRace()],
        classes: [FighterClass(), AdeptClass()],
        portraits:
        [
            new CreationPortrait(FolkA, Testfolk, "Folk A"),
            new CreationPortrait(FolkB, Testfolk, "Folk B"),
            new CreationPortrait(StoneA, Stonefolk, "Stone A"),
            new CreationPortrait(StoneB, Stonefolk, "Stone B"),
        ],
        attributePool: 5,
        chosenSkillCount: 2,
        nameMaximumLength: 8,
        startingSkillTier: new SkillTier(1),
        startingLevel: 1,
        startingCoins: 25,
        startingFoodPortions: 3,
        startingReputation: 2,
        startingFame: 1);

    /// <summary>The default party creation starts from in this suite: two members whose pools are spent.</summary>
    private static PartyCreationDefaults Defaults { get; } = new(
    [
        new CreationMemberDefaults(
            FolkA,
            Fighter,
            "Ann",
            [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 8)],
            [Axes, Bows]),
        new CreationMemberDefaults(
            StoneA,
            Adept,
            "Bo",
            [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 6)],
            [Aim, Lore]),
    ]);

    /// <summary>A flow standing at the name step of its first member.</summary>
    private static PartyCreationFlow NameStep()
    {
        PartyCreationFlow flow = new(Options);
        Assert.Null(flow.SelectPortrait(FolkA));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(Fighter));
        Assert.Null(flow.Advance());
        return flow;
    }

    /// <summary>A flow standing at the attribute step of its first member.</summary>
    private static PartyCreationFlow AttributesStep(PortraitId? portrait = null)
    {
        PartyCreationFlow flow = new(Options);
        Assert.Null(flow.SelectPortrait(portrait ?? FolkA));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(Fighter));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName("Ann"));
        Assert.Null(flow.Advance());
        return flow;
    }

    /// <summary>A flow standing at the skill step of its first member, with the pool already spent.</summary>
    private static PartyCreationFlow SkillsStep()
    {
        PartyCreationFlow flow = AttributesStep();
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.RaiseAttribute(Wit));
        Assert.Null(flow.RaiseAttribute(Vigour));
        Assert.Null(flow.Advance());
        return flow;
    }

    /// <summary>Walks one member through every step with the answers given.</summary>
    private static void Complete(
        PartyCreationFlow flow,
        PortraitId portrait,
        ClassId characterClass,
        string name,
        IReadOnlyList<(AttributeId Attribute, int Clicks)> attributes,
        params SkillId[] chosenSkills)
    {
        int index = flow.MemberIndex;
        Assert.Null(flow.SelectPortrait(portrait));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SelectClass(characterClass));
        Assert.Null(flow.Advance());
        Assert.Null(flow.SetName(name));
        Assert.Null(flow.Advance());
        foreach ((AttributeId attribute, int clicks) in attributes)
        {
            for (int click = 0; click < clicks; click++) Assert.Null(flow.RaiseAttribute(attribute));
        }

        Assert.Null(flow.Advance());
        foreach (SkillId skill in chosenSkills) Assert.Null(flow.ChooseSkill(skill));
        Assert.Null(flow.Advance());
        Assert.True(flow.Member(index).IsComplete);
    }

    /// <summary>Reads one of the first member's attributes, whatever member creation has moved on to.</summary>
    private static int AttributeOf(PartyCreationFlow flow, AttributeId attribute)
    {
        foreach (AttributeScore score in flow.Member(0).Attributes)
        {
            if (score.Attribute == attribute) return score.Value;
        }

        throw new InvalidOperationException($"The first member has no '{attribute}'.");
    }

    /// <summary>Reads every member's answers, which is what a creation screen does.</summary>
    private static IReadOnlyList<CreationMember> MembersOf(PartyCreationFlow flow) =>
        [.. Enumerable.Range(0, flow.MemberCount).Select(flow.Member)];

    /// <summary>A default party that breaks one rule, so the flow's refusal of it can be read.</summary>
    private static PartyCreationDefaults BrokenDefault(
        bool attributeSpend = false,
        string? skill = null,
        bool unreachableAttribute = false)
    {
        AttributeScore[] attributes = attributeSpend
            ? [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 10)]
            : unreachableAttribute
                ? [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 7)]
                : [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 8)];
        return new PartyCreationDefaults(
        [
            new CreationMemberDefaults(FolkA, Fighter, "Ann", attributes, [Axes, Bows]),
            new CreationMemberDefaults(
                StoneA,
                Adept,
                "Bo",
                [new AttributeScore(Vigour, 10), new AttributeScore(Wit, 6)],
                skill is null ? [Aim, Lore] : [new SkillId(skill), Lore]),
        ]);
    }

    private static CreationRace TestfolkRace() => new(
        Testfolk,
        "testfolk",
        [
            new AttributeCreationRange(Vigour, "vigour", start: 6, minimum: 4, maximum: 10, stepSize: 1, stepCost: 1),
            new AttributeCreationRange(Wit, "wit", start: 6, minimum: 4, maximum: 10, stepSize: 2, stepCost: 1),
        ]);

    private static CreationRace StonefolkRace() => new(
        Stonefolk,
        "stonefolk",
        [
            new AttributeCreationRange(Vigour, "vigour", start: 8, minimum: 6, maximum: 12, stepSize: 1, stepCost: 2),
            new AttributeCreationRange(Wit, "wit", start: 5, minimum: 3, maximum: 9, stepSize: 1, stepCost: 1),
        ]);

    private static CreationClass FighterClass() => new(
        Fighter,
        "fighter",
        [Blades, Bulwark],
        [Axes, Bows, Lore, Aim],
        startingHitPoints: 12,
        startingSpellPoints: 0,
        startingRank: 1);

    private static CreationClass AdeptClass() => new(
        Adept,
        "adept",
        [Wards],
        [Aim, Lore, Blades],
        startingHitPoints: 8,
        startingSpellPoints: 6,
        startingRank: 1);
}
