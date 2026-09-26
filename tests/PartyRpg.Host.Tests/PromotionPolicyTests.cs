using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's ranks against the real ruleset: the shipped class table's ladder, who gives each rank and
/// what each asks for, what a promotion does to a class, its ceilings, and its growth, the alternative a
/// second promotion chooses, and the errand that is stated and routed rather than faked.
/// </summary>
/// <remarks>
/// <para>
/// The kit's suite proves the owner over a ladder it states itself. What only this suite can prove is that
/// this game's own table reaches it: that the 36 shipped class rows and the 9 families the operator's
/// <c>CLASS.TXT</c> carries are the ladder this game states, that every rank's giver and errand are the ones
/// the shipped NPC, topic, and quest tables name, and that a character promoted through the real ruleset
/// holds what its new class states — the ceiling of every skill, the growth of every level, and the magic
/// its chosen path leaves open or shut.
/// </para>
/// <para>
/// The ladder is read without content as well: a pack that declares no classes still gets the ceilings and
/// the ranks this game's compiled table states, exactly as it gets the mastery rows. Where the operator's
/// own import is present the ladder is checked against the class table it is written in — the shipped rows
/// are what names every class a rank promotes from and to — and a machine without that data says so by
/// returning rather than skipping silently.
/// </para>
/// </remarks>
public sealed class PromotionPolicyTests
{
    [Fact]
    public void Every_shipped_class_is_on_the_ladder_with_its_giver_and_what_it_asks_for()
    {
        // The ladder is this game's own table, so it is complete without content: 9 families, each with a
        // first promotion and two second-promotion alternatives, which is 27 ranks over 36 class rows.
        MightAndMagic7Promotions ladder = MightAndMagic7Promotions.Read(null);
        Assert.Equal(27, ladder.RankCount);
        Assert.Equal(18, ladder.GiverCount);
        Assert.Equal(27, ladder.Ladder.Ranks.Count(rank => rank.Requirements.Any(requirement => requirement.Kind == PromotionRequirementKind.Giver)));
        Assert.Equal(17, ladder.QuestRequirementCount);
        Assert.Equal(14, ladder.ItemRequirementCount);
        Assert.Equal(2, ladder.Ladder.Ranks.Count(rank => rank.Requirements.Any(requirement => requirement.Kind == PromotionRequirementKind.Award)));
        Assert.Equal(9, ladder.Ladder.Ranks.Count(rank => rank.Rank == 2));
        Assert.Equal(18, ladder.Ladder.Ranks.Count(rank => rank.Rank == 3));
        Assert.Equal(8, ladder.Paths.Count);

        string content = Path.Combine(RepositoryRoot(), "content");
        string classes = Path.Combine(content, "partyrpg", "imports", "mm7-tables", "classes.json");
        if (!File.Exists(classes)) return;
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new FileContentSource(content),
            new ContentLayout("partyrpg/content-packs", "partyrpg/imports", "partyrpg/bundles"));
        MightAndMagic7Promotions overContent = MightAndMagic7Promotions.Read(catalog);

        // The shipped class table's own rows, in its own order: four per family, the base class, its first
        // promotion, and the two second promotions, which the notes column keys by base class
        // (docs/research/mm7-data-inventory.md, Classes).
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(classes));
        List<(string Name, string Base)> rows = [];
        foreach (JsonElement entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            rows.Add((entry.GetProperty("id").GetString()!, entry.GetProperty("baseClass").GetString()!));
        }

        Assert.Equal(36, rows.Count);
        Assert.Equal(9, rows.Select(row => row.Base).Distinct(StringComparer.Ordinal).Count());

        // Every class the shipped table carries is either a base class or the class one rank names, and no
        // rank names a class the table does not carry: the ladder's classes are the shipped table's.
        HashSet<string> shipped = [.. rows.Select(row => row.Name)];
        HashSet<string> promoted = [.. overContent.Ladder.Ranks.Select(rank => rank.To.Value)];
        HashSet<string> bases = [.. rows.Select(row => row.Base)];
        Assert.Equal(27, promoted.Count);
        Assert.All(promoted, name => Assert.Contains(name, shipped));
        Assert.Equal(9, bases.Count);
        Assert.All(bases, name => Assert.Contains(name, shipped));

        // Each family's ladder is the shipped table's own four rows in the shipped order: base → first
        // promotion → light → dark, which is the donor's own ordering (CharacterEnumFunctions.h:149-152,
        // getClassTier: the second promotions are the third rank, and their positions within the family are
        // what the light and dark alternatives are).
        foreach (string family in bases.Order(StringComparer.Ordinal))
        {
            string[] four = [.. rows.Where(row => string.Equals(row.Base, family, StringComparison.Ordinal)).Select(row => row.Name)];
            Assert.Equal(4, four.Length);
            PromotionRank first = Assert.Single(overContent.Ladder.From(new ClassId(four[0])));
            Assert.Equal(four[1], first.To.Value);
            Assert.Equal(2, first.Rank);
            Assert.Equal(string.Empty, first.Choice);
            List<PromotionRank> seconds = [.. overContent.Ladder.From(new ClassId(four[1]))];
            Assert.Equal(2, seconds.Count);
            Assert.Equal(four[2], seconds[0].To.Value);
            Assert.Equal(MightAndMagic7Promotions.LightChoice, seconds[0].Choice);
            Assert.Equal(four[3], seconds[1].To.Value);
            Assert.Equal(MightAndMagic7Promotions.DarkChoice, seconds[1].Choice);
            Assert.All(seconds, rank => Assert.Equal(3, rank.Rank));
        }

        // The four families whose pairs split on magic state the schools, and the five that do not state
        // none: the light alternative takes Light and leaves Dark, the dark one the reverse.
        Assert.Equal(MightAndMagic7Promotions.LightSchool, overContent.Paths["Master Archer"].Opens);
        Assert.Equal(MightAndMagic7Promotions.DarkSchool, overContent.Paths["Master Archer"].Closes);
        Assert.Equal(MightAndMagic7Promotions.DarkSchool, overContent.Paths["Lich"].Opens);
        Assert.Equal(MightAndMagic7Promotions.LightSchool, overContent.Paths["Lich"].Closes);
        Assert.DoesNotContain("Champion", overContent.Paths.Keys);
        Assert.DoesNotContain("Assassin", overContent.Paths.Keys);
    }

    [Fact]
    public void A_rank_asks_for_its_giver_its_errand_and_the_proof_the_errand_names()
    {
        MightAndMagic7Promotions ladder = MightAndMagic7Promotions.Read(null);

        // The giver is the person the shipped NPC table's notes column names and the topic table agrees
        // with: npc.txt row 48 is the good Sorcerer promoter and npctopic 92-93 "Wizard" are his; npc.txt
        // row 49 is the evil one and npctopic 96-97 "Lich" are his. The errand is the quest table's own row,
        // which names the same person: bit 45 is Thomas Grey's golem, bit 48 Halfgild Wynac's lich jars.
        PromotionRank wizard = ladder.Ladder.Rank("sorcerer-wizard")!;
        Assert.Equal("Sorcerer", wizard.From.Value);
        Assert.Equal("Wizard", wizard.To.Value);
        Assert.Equal("npc-48", wizard.Giver);
        Assert.Equal("Thomas Grey", wizard.Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Giver).Label);
        Assert.Equal(
            ["639", "641", "642", "643", "644", "645"],
            wizard.Requirements.Where(requirement => requirement.Kind == PromotionRequirementKind.Item).Select(requirement => requirement.Name));
        Assert.Equal("promotion:sorcerer-wizard", wizard.Award);
        Assert.DoesNotContain(wizard.Requirements, requirement => requirement.Kind == PromotionRequirementKind.Quest);

        PromotionRank lich = ladder.Ladder.Rank("wizard-lich")!;
        Assert.Equal("npc-49", lich.Giver);
        Assert.Equal(
            ["601", "602"],
            lich.Requirements.Where(requirement => requirement.Kind == PromotionRequirementKind.Item).Select(requirement => requirement.Name));
        Assert.Equal(MightAndMagic7Promotions.DarkChoice, lich.Choice);

        // An errand whose words name a deed rather than a thing is stated as the quest table's own bit, which
        // nothing in this build judges: the Priest's first rank is bit 43, the Spy's is bit 19, and both are
        // refused by name until the owner of quests exists.
        PromotionRequirement priest = ladder.Ladder.Rank("cleric-priest")!
            .Requirements.Single(requirement => requirement.Kind == PromotionRequirementKind.Quest);
        Assert.Equal("43", priest.Name);
        Assert.Contains("find the lost pirate map", priest.Label, StringComparison.Ordinal);
        Assert.Equal(
            "19",
            ladder.Ladder.Rank("rogue-spy")!.Requirements.Single(requirement => requirement.Kind == PromotionRequirementKind.Quest).Name);

        // The two errands the original keeps as counts rather than as quests are stated as records with a
        // magnitude: five arena wins for the Champion (AwardEnums.h:88-91) and ten thousand gold of bounties
        // for the Bounty Hunter (AwardEnums.h:86, EvtEnums.h:67).
        PromotionRank champion = ladder.Ladder.Rank("cavalier-champion")!;
        PromotionRequirement arena = champion.Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Award);
        Assert.Equal("award:arena-wins", arena.Name);
        Assert.Equal(5, arena.Amount);
        PromotionRequirement bounties = ladder.Ladder.Rank("hunter-bounty-hunter")!
            .Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Award);
        Assert.Equal("award:bounties", bounties.Name);
        Assert.Equal(10000, bounties.Amount);

        // The two ranks whose errand's own words name the thing brought back, and the shipped item table's
        // own rows for it: the vase of the Thief's first promotion and the Perfect Bow of the Archer's two
        // second promotions.
        Assert.Equal(
            "624",
            ladder.Ladder.Rank("thief-rogue")!.Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Item).Name);
        Assert.Equal(
            "542",
            ladder.Ladder.Rank("warrior-mage-master-archer")!.Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Item).Name);
        Assert.Equal(
            "542",
            ladder.Ladder.Rank("warrior-mage-sniper")!.Requirements.First(requirement => requirement.Kind == PromotionRequirementKind.Item).Name);
    }

    [Fact]
    public void A_promotion_moves_the_class_the_ceilings_and_the_growth_this_game_states()
    {
        using Fixture fixture = Fixture.Build("Sorcerer");
        PartyProgression progression = fixture.Progression;
        PartyMember member = fixture.Party.Members[0];
        MightAndMagic7Skills skills = fixture.Skills;

        // Where a Sorcerer starts, as the shipped mastery row states it: the four element schools reach the
        // expert rung, and no school of Light or Dark is theirs at all.
        Assert.Equal("Sorcerer", member.Profile.Class.Value);
        Assert.Equal(1, member.Progression.ClassRank);
        Assert.Equal(new SkillCeiling(9, new SkillTier(2)), skills.Ceiling(member, fixture.Fire));
        Assert.Equal(ProgressionGrowth.None with { HitPoints = 2, SpellPoints = 3, SkillPoints = 5 }, progression.Rule.Growth(new ProgressionGrowthRequest(member, 2)));

        // The first rank's own terms: the giver the shipped topic table names, and the six golem parts the
        // shipped item table carries. Without them the rank is refused by name, and nothing moves.
        PromotionResult refused = progression.Promote("sorcerer-wizard", "npc-48");
        Assert.False(refused.IsGranted);
        Assert.Equal("promotion-requirements-unmet", refused.Refusal!.Code);
        Assert.Contains(
            "First, a Sorcerer of rank 1, is missing needs Golem chest, and the party carries 0",
            refused.Refusal.Message,
            StringComparison.Ordinal);
        Assert.Equal("Sorcerer", member.Profile.Class.Value);

        // With the proof on the party the rank lands, and the class and the rank move together: the ceilings
        // the new class states rise, and so does what every further level gives.
        fixture.Carry("639", "641", "642", "643", "644", "645");
        PromotionResult given = progression.Promote("sorcerer-wizard", "npc-48");
        Assert.True(given.IsGranted);
        Assert.Equal("Wizard", member.Profile.Class.Value);
        Assert.Equal(2, member.Progression.ClassRank);
        Assert.Equal(new SkillCeiling(18, new SkillTier(3)), skills.Ceiling(member, fixture.Fire));
        Assert.Equal(new ProgressionGrowth(3, 4, 5), progression.Rule.Growth(new ProgressionGrowthRequest(member, 3)));
        Assert.True(fixture.Party.Effects.Has(new EffectId("promotion:sorcerer-wizard")));

        // The second rank's terms are the dark path's, given by the person the shipped tables name for it,
        // and they ask for the lich jars: the light alternative is not this giver's to hand out.
        PromotionResult wrongGiver = progression.Promote("wizard-lich", "npc-48");
        Assert.False(wrongGiver.IsGranted);
        Assert.Contains(
            "granted by Halfgild Wynac, and the party is speaking with npc-48",
            wrongGiver.Refusal!.Message,
            StringComparison.Ordinal);

        fixture.Carry("601", "602");
        PromotionResult dark = progression.Promote("wizard-lich", "npc-49");
        Assert.True(dark.IsGranted);
        Assert.Equal("Lich", member.Profile.Class.Value);
        Assert.Equal(3, member.Progression.ClassRank);
        Assert.Equal("dark", Assert.Single(dark.Granted).Choice);
        Assert.Equal(new SkillCeiling(60, new SkillTier(4)), skills.Ceiling(member, fixture.Fire));
        Assert.Equal(new SkillCeiling(60, new SkillTier(4)), skills.Ceiling(member, fixture.Dark));
        Assert.Equal(new ProgressionGrowth(3, 6, 5), progression.Rule.Growth(new ProgressionGrowthRequest(member, 4)));
    }

    [Fact]
    public void The_choice_is_recorded_per_character_and_holds_the_opposed_school_shut()
    {
        using Fixture fixture = Fixture.Build("Warrior Mage", "Rogue", rank: 2);
        PartyProgression progression = fixture.Progression;
        PartyMember archer = fixture.Party.Members[0];
        PartyMember rogue = fixture.Party.Members[1];

        // Two characters, two ladders: taking one alternative is a fact about the character that took it,
        // and the other member's class is not touched by it.
        fixture.Carry("542");
        Assert.Equal("Warrior Mage", archer.Profile.Class.Value);
        PromotionResult light = progression.Promote("warrior-mage-master-archer", "npc-40");
        Assert.True(light.IsGranted);
        Assert.Equal("Master Archer", archer.Profile.Class.Value);
        Assert.Equal("Rogue", rogue.Profile.Class.Value);
        Assert.Equal(2, rogue.Progression.ClassRank);

        // The chosen path opens its school and shuts the other, and the refusal says which choice did it:
        // the light path of the Master Archer takes Light, and Dark is the other alternative's.
        Assert.Equal(new SkillCeiling(18, new SkillTier(1)), fixture.Skills.Ceiling(archer, fixture.Light));
        SkillCeiling dark = fixture.Skills.Ceiling(archer, fixture.Dark);
        Assert.True(dark.IsNone);
        Assert.Equal("skill-closed-by-path", dark.Reason!.Code);
        Assert.Contains("took the light path of the Master Archer", dark.Reason.Message, StringComparison.Ordinal);
        Assert.Contains("leaves Dark to the other alternative", dark.Reason.Message, StringComparison.Ordinal);

        // A lesson at a counter that teaches the shut school is refused in the same words, which is where a
        // player meets it: the refusal is the game's own, not the kit's sentence about a class.
        PartyRefusal lesson = fixture.Skills.Lesson(archer, fixture.Dark, tier: 1, level: 1)!;
        Assert.Equal("skill-closed-by-path", lesson.Code);
        Assert.Contains("took the light path", lesson.Message, StringComparison.Ordinal);

        // The alternative it did not take is refused because the character is no longer of the class that
        // rank promotes from: the choice is recorded in the class, which is what a save already carries.
        PromotionResult switch2 = progression.Promote("warrior-mage-sniper", "npc-41");
        Assert.False(switch2.IsGranted);
        Assert.Equal("promotion-class-absent", switch2.Refusal!.Code);
        Assert.Contains("Nobody in the party is a Warrior Mage", switch2.Refusal.Message, StringComparison.Ordinal);
        Assert.Equal("Master Archer", archer.Profile.Class.Value);
    }

    [Fact]
    public void The_intersection_of_both_alternatives_resolves_once_a_path_is_chosen()
    {
        // A member who holds the rank both alternatives split from has taken neither, and this game's table
        // answers the intersection of the two: both of them leave Light and Dark to a choice, so neither
        // school is theirs until one is taken. That is the state the skill stone left and this stone closes.
        using Fixture top = Fixture.Build("Warrior Mage", rank: 3);
        PartyMember member = top.Party.Members[0];
        Assert.Equal("Warrior Mage", member.Profile.Class.Value);
        Assert.Equal(3, member.Progression.ClassRank);

        SkillCeiling light = top.Skills.Ceiling(member, top.Light);
        Assert.True(light.IsNone);
        Assert.Equal("skill-closed-by-unchosen-path", light.Reason!.Code);
        Assert.Contains("has taken neither alternative", light.Reason.Message, StringComparison.Ordinal);
        Assert.Contains("Master Archer (light) would take Light", light.Reason.Message, StringComparison.Ordinal);
        SkillCeiling dark = top.Skills.Ceiling(member, top.Dark);
        Assert.True(dark.IsNone);
        Assert.Contains("Sniper (dark) would take Dark", dark.Reason!.Message, StringComparison.Ordinal);

        // Taking one alternative resolves it: the chosen school is the character's at the rung its class
        // states — grand master for a priest of this game — and the opposed one is closed with the choice
        // named rather than with the intersection's sentence.
        top.Carry("542");
        PromotionResult chosen = top.Progression.Promote("warrior-mage-master-archer", "npc-40");
        Assert.True(chosen.IsGranted);
        Assert.Equal("Master Archer", member.Profile.Class.Value);
        Assert.Equal(3, member.Progression.ClassRank);
        Assert.Equal(new SkillCeiling(18, new SkillTier(1)), top.Skills.Ceiling(member, top.Light));
        SkillCeiling opposed = top.Skills.Ceiling(member, top.Dark);
        Assert.True(opposed.IsNone);
        Assert.Equal("skill-closed-by-path", opposed.Reason!.Code);
        Assert.Contains("took the light path of the Master Archer", opposed.Reason.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rank_is_taken_from_the_person_who_gives_it_and_reaches_the_projection()
    {
        // Staged the way an imported pack carries it: a place, the two people the shipped NPC table names
        // for this family's ranks, a scripted party of one sorcerer, and the golem parts in its pack.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Staged());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = new UseIntentNames(ProductIdentity.UseIntent, ProductIdentity.UseAction, ProductIdentity.UiActionContract),
                Conversation = new ConversationIntentNames(ProductIdentity.ConversationLeaveIntent, ProductIdentity.UiActionContract),
            });
        session.Start();

        // What the panel publishes before anybody is promoted: the ladder itself, member by member, with
        // every rank their class leads to and what each of them asks for.
        session.Update(ProductTestContext.Update(1, 1));
        ProjectedNode block = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        Assert.True(block.Field("available").AsBoolean());
        ProjectedNode row = block.Field("members").Item(0);
        Assert.Equal("Sorcerer", row.Field("class").AsString());
        Assert.Equal(1d, row.Field("rank").AsNumber());
        Assert.Equal(1, row.Field("promotions").Length());
        ProjectedNode wizard = row.Field("promotions").Item(0);
        Assert.Equal("sorcerer-wizard", wizard.Field("promotion").AsString());
        Assert.Equal("Wizard", wizard.Field("toClass").AsString());
        Assert.Equal("npc-48", wizard.Field("giver").AsString());
        Assert.Equal("Thomas Grey", wizard.Field("giverName").AsString());
        Assert.Equal(7, wizard.Field("requirements").Length());
        ProjectedNode proof = wizard.Field("requirements").Item(1);
        Assert.Equal("item", proof.Field("kind").AsString());
        Assert.Equal("639", proof.Field("name").AsString());
        Assert.Equal("Golem chest", proof.Field("label").AsString());
        Assert.Equal("Golem chest", proof.Field("text").AsString());

        // The party talks to the person who gives the rank: the use opens the conversation, and the rank is
        // offered as a topic on that person's own list.
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Thomas Grey", talking.Field("speaker").AsString());
        ProjectedNode offer = Enumerable.Range(0, talking.Field("topics").Length())
            .Select(talking.Field("topics").Item)
            .Single(topic => topic.Field("id").AsString() == "promote:sorcerer-wizard");
        Assert.Equal("Wizard", offer.Field("label").AsString());

        // Taking it hands the party to the progression owner, which gives the rank: the report names who
        // rose, from which class to which, which alternative they took, and what they met.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("promote:sorcerer-wizard")));
        ProjectedNode after = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        Assert.Equal("granted", after.Field("outcome").AsString());
        Assert.Equal("sorcerer-wizard", after.Field("promotion").AsString());
        Assert.Equal("Wizard", after.Field("toClass").AsString());
        Assert.Equal(2d, after.Field("rank").AsNumber());
        ProjectedNode grant = after.Field("granted").Item(0);
        Assert.Equal("Sorcerer", grant.Field("fromClass").AsString());
        Assert.Equal(1d, grant.Field("fromRank").AsNumber());
        Assert.Equal(7, grant.Field("met").Length());
        Assert.Contains("granted by Thomas Grey", grant.Field("met").Item(0).AsString(), StringComparison.Ordinal);
        Assert.Equal("carries Golem chest", grant.Field("met").Item(1).AsString());
        Assert.Equal("carries Golem left arm", grant.Field("met").Item(6).AsString());
        ProjectedNode landed = after.Field("members").Item(0);
        Assert.Equal("Wizard", landed.Field("class").AsString());
        Assert.Equal(2d, landed.Field("rank").AsNumber());
        Assert.Equal(2, landed.Field("promotions").Length());
        Assert.Equal("wizard-arch-mage", landed.Field("promotions").Item(0).Field("promotion").AsString());
        Assert.Equal("light", landed.Field("promotions").Item(0).Field("choice").AsString());
        Assert.Equal("wizard-lich", landed.Field("promotions").Item(1).Field("promotion").AsString());
        Assert.Equal("dark", landed.Field("promotions").Item(1).Field("choice").AsString());

        // What the rank changed is published where it applies: the member's own row in the skills block now
        // states the ceiling this game's table gives a wizard, and the class it belongs to.
        ProjectedNode skills = ProjectedNode.Of(ui.Latest().Value).Field("skills");
        ProjectedNode member = skills.Field("members").Item(0);
        Assert.Equal("Wizard", member.Field("class").AsString());
        Assert.Equal(2d, member.Field("rank").AsNumber());
        ProjectedNode fire = Enumerable.Range(0, member.Field("skills").Length())
            .Select(member.Field("skills").Item)
            .Single(entry => entry.Field("skill").AsString() == "Fire");
        Assert.Equal(18d, fire.Field("ceilingLevel").AsNumber());
        Assert.Equal("master", fire.Field("ceilingTier").AsString());

        // The party has been promoted and the conversation is over, so speaking again offers the rank above:
        // the light alternative is on offer, and taking it is refused with what it is still missing — the
        // book the shipped item table carries for the Light school — rather than quietly succeeding.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        session.Update(ProductTestContext.Update(5, 1, ProductTestContext.ChooseTopic("promote:wizard-arch-mage")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("promotion-requirements-unmet", refused.Field("code").AsString());
        ProjectedNode missing = refused.Field("denied").Item(0).Field("missing");
        Assert.Equal(
            "needs Divine Intervention, and the party carries 0",
            Assert.Single(Enumerable.Range(0, missing.Length()).Select(missing.Item)).AsString());
        Assert.Equal(0, refused.Field("granted").Length());
    }

    [Fact]
    public void A_party_the_player_created_is_offered_the_rank_by_the_person_who_gives_it()
    {
        // The player's own path: the host declared a creation screen, so the party is the one this game's
        // creation tables build, and the world it steps into is composed when it is accepted. The rank is
        // offered by the person standing in front of it, and it is judged against the party the player
        // built — which is the state a conversation composed before that party existed could not read.
        List<(string Path, string Text)> files =
        [
            ProductTestContext.Bundle("partyrpg-default", "creation-tables", "world"),
            .. ProductTestContext.CreationTables(),
            .. CreatedWorld(),
        ];
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create([.. files]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, creation: true) with
            {
                Use = new UseIntentNames(ProductIdentity.UseIntent, ProductIdentity.UseAction, ProductIdentity.UiActionContract),
                Conversation = new ConversationIntentNames(ProductIdentity.ConversationLeaveIntent, ProductIdentity.UiActionContract),
            });
        session.Start();

        // The default party this game's creation opens on is finished, so accepting it starts the game.
        session.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        Assert.Equal("running", ProjectedNode.Of(ui.Latest().Value).Field("session").Field("mode").AsString());

        // Using the person in front opens the conversation, and the rank that person gives is on offer: a
        // Sorcerer is in the party, so the offer is not withheld for want of anybody to give it to.
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.Equal("Thomas Grey", talking.Field("speaker").AsString());
        Assert.Contains(
            "promote:sorcerer-wizard",
            Enumerable.Range(0, talking.Field("topics").Length()).Select(talking.Field("topics").Item).Select(node => node.Field("id").AsString()));

        // Taking it reaches the progression owner, which judges the rank against the party the player built:
        // the person who gives it is met, and the six golem parts the shipped item table carries are not —
        // so the party is told exactly what its own characters are missing.
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("promote:sorcerer-wizard")));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("promotion-requirements-unmet", refused.Field("code").AsString());
        Assert.Contains("Aelina", refused.Field("message").AsString(), StringComparison.Ordinal);
        ProjectedNode missing = refused.Field("denied").Item(0).Field("missing");
        Assert.Equal(6, missing.Length());
        Assert.Contains("Golem chest", missing.Item(0).AsString(), StringComparison.Ordinal);
        Assert.Contains("carries 0", missing.Item(0).AsString(), StringComparison.Ordinal);

        // The ladder itself is published for the party the player built, member by member, with the rank
        // that member's class leads to.
        ProjectedNode ladder = ProjectedNode.Of(ui.Latest().Value).Field("promotion");
        ProjectedNode sorcerer = Enumerable.Range(0, ladder.Field("members").Length())
            .Select(ladder.Field("members").Item)
            .Single(member => member.Field("class").AsString() == "Sorcerer");
        Assert.Equal("Wizard", sorcerer.Field("promotions").Item(0).Field("toClass").AsString());
        Assert.Equal("npc-48", sorcerer.Field("promotions").Item(0).Field("giver").AsString());
    }

    /// <summary>The world a created party steps into: one place, one person in front, and the start.</summary>
    private static (string Path, string Text)[] CreatedWorld() =>
    [
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:npc-48" ] },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "The School of Sorcery", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "person-0", "kind": "person", "x": 120, "y": 0, "z": 0, "people": [ "npc-48" ] } ] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "npc-48", "npcId": 48, "name": "Thomas Grey", "portrait": "700",
                "greeting": "'You have the look of someone who wants to learn.'", "dialogueEvents": 92, "topics": [] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
    ];

    /// <summary>
    /// A party, this game's skill policy over content a test declares, and the classes the ladder is read
    /// against.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(PartyEntity party, MightAndMagic7Skills skills, PartyProgression progression)
        {
            Party = party;
            Skills = skills;
            Progression = progression;
        }

        internal PartyEntity Party { get; }

        internal MightAndMagic7Skills Skills { get; }

        internal PartyProgression Progression { get; }

        internal SkillId Fire { get; } = new("Fire");

        internal SkillId Light { get; } = new("Light");

        internal SkillId Dark { get; } = new("Dark");

        /// <summary>Builds a party of the classes a case needs, and this game's policy over the shipped rows.</summary>
        internal static Fixture Build(string characterClass, string? second = null, int rank = 1)
        {
            // The skill rows are the shipped table's own names in this game's own blocks; the classes and
            // ranks are stated by the case, because what the ladder answers is the class, not the pack.
            string skills = string.Join(",\n", new[] { "Fire", "Air", "Water", "Earth", "Spirit", "Mind", "Body", "Light", "Dark" }
                .Select(name => $$"""{ "id": "{{name}}", "block": "magic" }"""));
            (ProductCreateContext context, _) = ProductTestContext.Create(
                ProductTestContext.Bundle("partyrpg-default", "policy"),
                ($"{ProductTestContext.ContentDirectory}/content-packs/policy/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "policy",
                      "kind": "definitions",
                      "provenance": { "description": "authored for a test" },
                      "documents": [ { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" } ]
                    }
                    """),
                ($"{ProductTestContext.ContentDirectory}/content-packs/policy/skills.json",
                    $$"""{ "documentId": "skills", "definitionKind": "skill", "entries": [ {{skills}} ] }"""));
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(
                new ProductContentSource(context.Content),
                ContentLayout.Under(ProductTestContext.ContentDirectory),
                BuiltInBundles.Default);
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            ContentCatalog catalog = bootstrap.Catalog!;

            MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(catalog);
            MightAndMagic7Skills policy = MightAndMagic7Skills.Read(catalog, promotions)
                ?? throw new InvalidOperationException("The test's content declares skills, so the policy is read.");
            List<MemberCreation> members =
            [
                Member("First", characterClass, rank),
            ];
            if (second is not null) members.Add(Member("Second", second, rank: 2));
            PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(members, coins: 0, foodPortions: 0, reputation: 0, fame: 0));
            return new Fixture(party, policy, new PartyProgression(MightAndMagic7Progression.Instance, party, policy, promotions));
        }

        /// <summary>Puts items in the party's one pack, which is where a rank's proof is read from.</summary>
        internal void Carry(params string[] definitions)
        {
            foreach (string definition in definitions) Party.AcquireItem(new ItemDefinitionId(definition));
        }

        public void Dispose() => Party.Dispose();

        private static MemberCreation Member(string name, string characterClass, int rank) => new(new PartyMemberSeed(
            name,
            new RaceId("human"),
            new ClassId(characterClass),
            [new AttributeScore(new AttributeId("Might"), 12)],
            skills: [],
            spells: [],
            experience: 0,
            level: rank + 1,
            skillPoints: 0,
            classRank: rank,
            conditions: [],
            hitPoints: ResourcePool.Full(40),
            spellPoints: ResourcePool.Full(20)));
    }

    /// <summary>The content the session case stages, written the way the importer writes a pack.</summary>
    private static (string Path, string Text)[] Staged() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world", "policy"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/policy/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "policy",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/policy/skills.json",
            """
            { "documentId": "skills", "definitionKind": "skill", "entries": [
              { "id": "Fire", "block": "magic" }, { "id": "Air", "block": "magic" },
              { "id": "Water", "block": "magic" }, { "id": "Earth", "block": "magic" },
              { "id": "Spirit", "block": "magic" }, { "id": "Mind", "block": "magic" },
              { "id": "Body", "block": "magic" }, { "id": "Light", "block": "magic" },
              { "id": "Dark", "block": "magic" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:npc-48", "person:npc-49" ] },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "The School of Sorcery", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [
                  { "id": "person-0", "kind": "person", "x": 120, "y": 0, "z": 0, "people": [ "npc-48" ] },
                  { "id": "person-1", "kind": "person", "x": -4000, "y": 0, "z": 0, "people": [ "npc-49" ] } ] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "npc-48", "npcId": 48, "name": "Thomas Grey", "portrait": "700",
                "greeting": "'You have the look of someone who wants to learn.'", "dialogueEvents": 92, "topics": [] },
              { "id": "npc-49", "npcId": 49, "name": "Halfgild Wynac", "portrait": "701",
                "greeting": "'Knowledge has a price, and I set it.'", "dialogueEvents": 96, "topics": [] } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 120, "food": 4, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "639" }, { "item": "641" }, { "item": "642" }, { "item": "643" },
                            { "item": "644" }, { "item": "645" }, { "item": "542" } ],
                  "members": [
                    { "name": "Aelina", "race": "human", "class": "Sorcerer", "level": 3,
                      "hitPoints": 40, "spellPoints": 20,
                      "attributes": [ { "id": "Might", "value": 12 } ],
                      "skills": [ { "id": "Fire", "level": 3, "tier": 1 } ],
                      "spells": [], "conditions": [] } ] }
              ]
            }
            """),
    ];

    /// <summary>The repository root, found by walking up from the test binary.</summary>
    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "PartyRpg.Kit"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException($"No 'src/PartyRpg.Kit' directory above '{AppContext.BaseDirectory}'.");
    }
}
