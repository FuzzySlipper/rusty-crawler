using System.Globalization;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Mixing: two things out of the party's own pack and one potion back into it, the rung of the mixing skill a
/// result asks for, the consequence of a mixture the game states as incompatible, and a whole attempt refused
/// when the pack cannot take what would come out.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is the mechanism's own. The mixture table is the suite's, the mixing skill and its rungs are
/// the suite's, and the harm a burst is worth is the suite's, because the kit holds none of those: what it must
/// do is take two instances out of the shared pack through the party's own entry, hand the result back through
/// the acquisition path, judge a rung against the character's own skill entry, apply the game's own burst
/// through the member's one damage entry, and carry an item's own strength into the effect a casting applies.
/// </para>
/// <para>
/// The readings the product's own ruleset states — which reagent makes which potion, what each potion does,
/// what a burst costs — are proved in <c>tests/PartyRpg.Host.Tests</c> over the operator's own content; this
/// suite proves the kit's half, where the numbers are the test's and a boundary is exact.
/// </para>
/// </remarks>
public sealed class AlchemyTests
{
    private static readonly ItemDefinitionId Berry = new("berry");
    private static readonly ItemDefinitionId Bottle = new("bottle");
    private static readonly ItemDefinitionId Draught = new("draught");
    private static readonly ItemDefinitionId Tonic = new("tonic");
    private static readonly ItemDefinitionId Rock = new("rock");
    private static readonly ItemDefinitionId Token = new("token");
    private static readonly SkillId AlchemySkill = new("Alchemy");
    private static readonly EffectId Haste = new("test.haste");
    private static readonly ConditionId Eradicated = new("eradicated");
    private static readonly ConditionId Weak = new("weak");

    [Fact]
    public void A_legal_mixture_takes_both_ingredients_out_of_the_pack_and_puts_the_potion_in()
    {
        using PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1);
        AlchemyCatalog catalog = new([
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(1), Power: 5, Note: 58),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule());

        ItemInstance berry = Take(party, Berry);
        ItemInstance bottle = Take(party, Bottle);
        Assert.Equal(2, party.Inventory.Count);

        MixingResult result = mixing.Mix(new MixingRequest(0, berry.Id, bottle.Id));

        // The mixture made the thing its own row states, at the strength the game's arithmetic came to — the
        // mixer's own level in the mixing skill plus the power the reagent states.
        Assert.True(result.IsMixed);
        Assert.Equal("mixture-mixed", result.Code);
        Assert.Equal("potion", result.Outcome);
        Assert.Equal(Draught.Value, result.Result);
        Assert.Equal(8, result.Power);
        Assert.Equal(58, result.Note);
        Assert.Contains("strength 8", result.Message, StringComparison.Ordinal);

        // Both ingredients left the pack through the party's own entry, and the potion entered it through the
        // acquisition path every other transfer takes: one instance, identified because the party made it, and
        // carrying the strength it came out at.
        Assert.Null(party.FindItem(berry.Id));
        Assert.Null(party.FindItem(bottle.Id));
        ItemInstance made = Assert.Single(party.Items);
        Assert.Equal(Draught, made.Definition);
        Assert.True(made.State.IsIdentified);
        Assert.Equal(8, made.State.Potency);
        Assert.True(made.Custody.IsInSharedInventory);
        Assert.Equal(1, party.Inventory.Count);
    }

    [Fact]
    public void A_mixture_the_mixers_mastery_does_not_reach_is_refused_by_name_and_spends_nothing()
    {
        using PartyEntity party = Party(alchemyLevel: 4, alchemyTier: 1, secondLevel: 4, secondTier: 3);
        AlchemyCatalog catalog = new([
            // The donor's own gate is on what a mixture makes: a white potion asks for the third rung, and the
            // character here stands at the first.
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Tonic), new SkillTier(3), Power: 5),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule());

        ItemInstance berry = Take(party, Berry);
        ItemInstance bottle = Take(party, Bottle);

        MixingResult refused = mixing.Mix(new MixingRequest(0, berry.Id, bottle.Id));

        Assert.False(refused.IsMixed);
        Assert.Equal("mixture-mastery-too-low", refused.Code);

        // The refusal names the rung the result asks for, the rung the character stands at, and what would
        // raise it — the same tone the skill ceilings use, where a player is told what to go and do.
        Assert.Contains("stands at novice", refused.Message, StringComparison.Ordinal);
        Assert.Contains("mixed at master", refused.Message, StringComparison.Ordinal);
        Assert.Contains("a mastery lesson in Alchemy at a counter that teaches it raises the rung", refused.Message, StringComparison.Ordinal);

        // Nothing was spent: both ingredients are exactly where they lay.
        Assert.NotNull(party.FindItem(berry.Id));
        Assert.NotNull(party.FindItem(bottle.Id));
        Assert.Equal(2, party.Inventory.Count);

        // The gate is the character's own, so the same mixture from a character who has reached the rung goes
        // ahead: what blocked the first attempt was the mastery and nothing about the pair.
        ItemInstance secondBottle = Take(party, Bottle);
        MixingResult mixed = mixing.Mix(new MixingRequest(1, berry.Id, secondBottle.Id));
        Assert.True(mixed.IsMixed);
        Assert.Equal(Tonic.Value, mixed.Result);
    }

    [Fact]
    public void An_incompatible_mixture_does_the_games_stated_failure_and_costs_both_ingredients()
    {
        using PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1);
        AlchemyCatalog catalog = new([
            // A pair the table states a burst for: mixing it costs both things and wounds the mixer, which is
            // the donor's own answer rather than a refusal that leaves everything where it was.
            new PotionMixture(Berry, Rock, MixtureOutcome.Bursts(2)),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule { BurstHarm = 42, BurstCondition = Weak });

        ItemInstance berry = Take(party, Berry);
        ItemInstance rock = Take(party, Rock);
        int before = party.Members[0].Resources.HitPoints.Current;

        MixingResult burst = mixing.Mix(new MixingRequest(0, berry.Id, rock.Id));

        Assert.True(burst.IsMixed);
        Assert.Equal("mixture-burst", burst.Code);
        Assert.Equal("burst", burst.Outcome);
        Assert.Equal(2, burst.Burst);
        Assert.Equal(42, burst.Harm);
        Assert.Equal(Weak.Value, burst.Condition);

        // What is lost: both ingredients, and the pack is empty.
        Assert.Null(party.FindItem(berry.Id));
        Assert.Null(party.FindItem(rock.Id));
        Assert.Empty(party.Items);

        // What it did to the character: the harm went through the member's own one damage entry, and the
        // condition the game stated landed on their own condition state.
        Assert.Equal(before - 42, party.Members[0].Resources.HitPoints.Current);
        Assert.True(party.Members[0].Conditions.Has(Weak));
    }

    [Fact]
    public void A_mixture_the_pack_cannot_take_is_refused_whole_and_leaves_both_ingredients_where_they_lay()
    {
        using PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1, capacity: new RoomForTwo());
        AlchemyCatalog catalog = new([
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(1), Power: 5),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule());

        ItemInstance berry = Take(party, Berry);
        ItemInstance bottle = Take(party, Bottle);

        // The pack holds two instances and its own rule lets it hold no more: the potion would be a third, so
        // the whole attempt is refused before either ingredient is spent.
        MixingResult refused = mixing.Mix(new MixingRequest(0, berry.Id, bottle.Id));

        Assert.False(refused.IsMixed);
        Assert.NotNull(refused.Refusal);
        Assert.Equal("pack-full", refused.Refusal!.Code);
        Assert.NotNull(party.FindItem(berry.Id));
        Assert.NotNull(party.FindItem(bottle.Id));
        Assert.Equal(Berry, party.FindItem(berry.Id)!.Definition);
        Assert.Equal(Bottle, party.FindItem(bottle.Id)!.Definition);
        Assert.Equal(2, party.Inventory.Count);
    }

    [Fact]
    public void A_mixture_that_is_not_one_and_a_character_who_cannot_act_are_each_refused_by_name()
    {
        using PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1);
        AlchemyCatalog catalog = new([
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(1), Power: 5),
            new PotionMixture(Rock, Token, MixtureOutcome.NoReaction()),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule());

        ItemInstance berry = Take(party, Berry);
        ItemInstance rock = Take(party, Rock);

        // Two things the table states no mixture for are not "an incompatible mixture": nothing was combined
        // and both are still where they were.
        MixingResult unknown = mixing.Mix(new MixingRequest(0, berry.Id, rock.Id));
        Assert.False(unknown.IsMixed);
        Assert.Equal("mixture-unknown", unknown.Code);
        Assert.Equal(2, party.Inventory.Count);

        // A pair the table does state and states as doing nothing is a mixture that was attempted: the
        // ingredients are still where they lay, and the result says that is what the game's own row says.
        ItemInstance token = Take(party, Token);
        MixingResult nothing = mixing.Mix(new MixingRequest(0, rock.Id, token.Id));
        Assert.True(nothing.IsMixed);
        Assert.Equal("mixture-nothing", nothing.Code);
        Assert.Equal("nothing", nothing.Outcome);
        Assert.Equal(3, party.Inventory.Count);

        // A character the game's own conditions have laid out may not mix at all, and nothing is spent.
        party.Members[0].Conditions.Apply(new ActiveCondition(Eradicated));
        MixingResult unable = mixing.Mix(new MixingRequest(0, berry.Id, token.Id));
        Assert.False(unable.IsMixed);
        Assert.Equal("mixture-cannot-act", unable.Code);
    }

    [Fact]
    public void A_potion_drunk_is_a_cast_whose_source_is_the_item_and_whose_strength_travels_with_it()
    {
        using PartyEntity party = Party(alchemyLevel: 4, alchemyTier: 3);
        AlchemyCatalog catalog = new([
            new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(3), Power: 5),
        ]);
        PotionMixing mixing = new(party, catalog, new Rule());

        ItemInstance berry = Take(party, Berry);
        ItemInstance bottle = Take(party, Bottle);
        Assert.True(mixing.Mix(new MixingRequest(0, berry.Id, bottle.Id)).IsMixed);

        // The potion it made is a thing an item rule reads a spell for, and the strength it came out at is the
        // instance's own state: the effect path reads it there rather than from any character's skill.
        ItemInstance potion = Assert.Single(party.Items);
        Assert.Equal(9, potion.State.Potency);

        Effects effects = new();
        Spellcasting casting = new(party, new Spells(potion.Definition), effects);

        SpellCastResult drunk = casting.Cast(new SpellCastRequest(0, DraughtSpell, Target: string.Empty, Item: potion.Id));

        Assert.True(drunk.IsCast);
        Assert.NotEqual(string.Empty, drunk.Source);

        // The one effect path was handed the casting with the item it came from, which is where a game reads a
        // strength the item states instead of a caster's school level — and the potion paid for the casting:
        // no spell point was spent and the instance left the party.
        SpellApplication application = Assert.Single(effects.Applications);
        Assert.NotNull(application.Source);
        Assert.Equal(potion.Id, application.Source!.Id);
        Assert.Equal(9, application.Source.State.Potency);
        Assert.Equal(party.Members[0].Resources.SpellPoints.Maximum, party.Members[0].Resources.SpellPoints.Current);
        Assert.Empty(party.Items);

        // Drinking a second time names an item the party no longer holds, and is refused by name.
        SpellCastResult gone = casting.Cast(new SpellCastRequest(0, DraughtSpell, Target: string.Empty, Item: potion.Id));
        Assert.False(gone.IsCast);
        Assert.Equal("spell-item-not-held", gone.Code);
    }

    [Fact]
    public void The_recipes_are_the_tables_own_and_the_kit_names_none_of_them()
    {
        // The same two definitions and the same workflow: one table states that the pair makes a potion and the
        // other states that it goes off. Which of the two happens is the table's answer, and no source in the
        // kit decides it.
        using (PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1))
        {
            PotionMixing makes = new(
                party,
                new AlchemyCatalog([new PotionMixture(Berry, Bottle, MixtureOutcome.Produces(Draught), new SkillTier(1), Power: 5)]),
                new Rule());
            ItemInstance berry = Take(party, Berry);
            ItemInstance bottle = Take(party, Bottle);
            Assert.Equal("mixture-mixed", makes.Mix(new MixingRequest(0, berry.Id, bottle.Id)).Code);
        }

        using (PartyEntity party = Party(alchemyLevel: 3, alchemyTier: 1))
        {
            PotionMixing bursts = new(
                party,
                new AlchemyCatalog([new PotionMixture(Berry, Bottle, MixtureOutcome.Bursts(1))]),
                new Rule { BurstHarm = 3 });
            ItemInstance berry = Take(party, Berry);
            ItemInstance bottle = Take(party, Bottle);
            Assert.Equal("mixture-burst", bursts.Mix(new MixingRequest(0, berry.Id, bottle.Id)).Code);
        }

        // And the kit's own sources name no reagent, no potion, and no mixture: a recipe's words are a game's
        // content, and the mechanism states a pair of definitions and what a table says about them.
        string alchemy = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit", "Alchemy");
        string[] sources =
        [
            .. Directory.EnumerateFiles(alchemy, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)),
        ];
        Assert.NotEmpty(sources);

        string[] forbidden = ["Cure Wounds", "Magic Potion", "Widowsweep", "Catalyst", "Philosopher", "Rejuvenation"];
        foreach (string source in sources)
        {
            string text = File.ReadAllText(source);
            foreach (string name in forbidden)
            {
                Assert.False(
                    text.Contains(name, StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(source)} names '{name}': a mixture's own words are the game's content, and the kit would then have a recipe of its own.");
            }
        }
    }

    /// <summary>The spell this game's own rule reads for the potion this suite's mixtures make.</summary>
    private static readonly SpellId DraughtSpell = new("potion:draught");

    /// <summary>A party of two, so a mixture from the wrong character would be visible.</summary>
    private static PartyEntity Party(
        int alchemyLevel,
        int alchemyTier,
        int secondLevel = 0,
        int secondTier = 0,
        IInventoryCapacityRule? capacity = null)
    {
        PartyCreation creation = new(
            [
                Member("Nyx", alchemyLevel, alchemyTier),
                Member("Borin", secondLevel, secondTier),
            ],
            coins: 0,
            foodPortions: 6,
            reputation: 0,
            fame: 0);
        return new PartyEntityFactory(inventoryCapacity: capacity).Create(creation);
    }

    private static MemberCreation Member(string name, int alchemyLevel, int alchemyTier) =>
        new(new PartyMemberSeed(
            name,
            new RaceId("testfolk"),
            new ClassId("mage"),
            [new AttributeScore(new AttributeId("Intellect"), 20)],
            // A character who has not learned the skill at all holds no entry for it, and their rung reads as
            // untrained: the gate has to answer for that character as well as for one who has trained.
            alchemyLevel > 0 ? [new SkillEntry(AlchemySkill, alchemyLevel, new SkillTier(alchemyTier), 0)] : [],
            [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(60),
            spellPoints: ResourcePool.Full(20)));

    /// <summary>Takes one item of a definition into the pack and hands the instance back.</summary>
    private static ItemInstance Take(PartyEntity party, ItemDefinitionId definition)
    {
        ItemInstance item = party.CreateItem(definition);
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be found from the test's own output directory.");
    }

    /// <summary>A pack that holds two instances and no more, so an acquisition past that is refused whole.</summary>
    private sealed class RoomForTwo : IInventoryCapacityRule
    {
        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> items, ItemDefinitionId definition, int count) =>
            items.Count + count > 2
                ? new PartyRefusal("pack-full", "The pack holds two things and no more, so the potion would not fit.")
                : null;
    }

    /// <summary>This suite's own answers about mixing: a skill, its rung names, and what a burst is worth.</summary>
    private sealed class Rule : IAlchemyRule
    {
        internal int BurstHarm { get; init; } = 5;

        internal ConditionId? BurstCondition { get; init; }

        public SkillId Skill => AlchemySkill;

        public string RungName(SkillTier tier) => tier.Value switch
        {
            0 => "untrained",
            1 => "novice",
            2 => "expert",
            3 => "master",
            _ => "grand master",
        };

        public string MasteryRaisedBy(SkillId skill) =>
            $"a mastery lesson in {skill.Value} at a counter that teaches it raises the rung";

        public string NameOf(ItemDefinitionId definition) => definition.Value;

        public PartyRefusal? MayMix(PartyMember mixer) =>
            mixer.Conditions.Has(Eradicated)
                ? new PartyRefusal("mixture-cannot-act", $"{mixer.Profile.Name} is in no condition to mix anything.")
                : null;

        public int Strength(PartyMember mixer, PotionMixture mixture, ItemInstance first, ItemInstance second) =>
            Math.Max(1, mixer.Skills.LevelOf(AlchemySkill) + mixture.Power);

        public MixtureBackfire Backfire(int strength) => new(BurstHarm, "harm", BurstCondition);
    }

    /// <summary>
    /// This suite's item rule: the potion its own table makes carries one spell, and using it uses it up.
    /// </summary>
    private sealed class Spells : ISpellRule, ISpellItemRule
    {
        private readonly SpellCatalog _catalog = new(
        [
            new SpellDefinition(DraughtSpell, "a draught", "potion", AlchemySkill, SkillTier.None, Cost: 0, SpellTargeting.Caster, "utility"),
        ]);

        internal Spells(ItemDefinitionId potion) => Potion = potion;

        private ItemDefinitionId Potion { get; }

        public SpellCatalog Catalog => _catalog;

        public int SpellPointCapacity(PartyMember member) => 20;

        public int CostFor(PartyMember member, SpellDefinition spell) => 0;

        public SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell) => null;

        public SpellItemReading? Reading(ItemDefinitionId definition) =>
            definition == Potion ? SpellItemReading.Consumed(DraughtSpell) : null;
    }

    /// <summary>
    /// This suite's effect path, which records the casting it was handed and says it applied it.
    /// </summary>
    private sealed class Effects : ISpellEffectRule
    {
        internal List<SpellApplication> Applications { get; } = [];

        public SpellRefusal? Judge(SpellApplication application) => null;

        public SpellApplicationOutcome Apply(SpellApplication application)
        {
            Applications.Add(application);
            return SpellApplicationOutcome.Expressed(
                application.Spell.Effect,
                $"{application.Spell.Name} applied at {(application.Source is { } item ? item.State.Potency : 0)}.",
                [new SpellEffectFact("potency", (application.Source?.State.Potency ?? 0).ToString(CultureInfo.InvariantCulture))]);
        }
    }
}
