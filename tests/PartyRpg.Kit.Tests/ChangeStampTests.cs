using System.Reflection;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Every change to the party's state moves the party's change stamp, which is what lets the projection hand back a
/// block nothing has changed under; a mutator that forgot to take a stamp would leave its block showing what was.
/// </summary>
public sealed class ChangeStampTests
{
    private static readonly ItemDefinitionId Coat = new("coat");
    private static readonly EquipmentSlot Body = new("body");

    /// <summary>
    /// What each owner of party state may do, split into what changes it and what only reads it. Every instance
    /// method the owner declares is one or the other, so a method added to an owner fails here until it is
    /// sorted — and a mutator sorted here is one the next test proves moves the stamp.
    /// </summary>
    private static readonly Dictionary<Type, (string[] Changes, string[] Reads)> Owners = new()
    {
        [typeof(PartyPurse)] = (["Credit", "TryDebit"], ["CanAfford"]),
        [typeof(PartyFood)] = (["Credit", "TrySpend", "TryDebit"], ["CanCover", "RequireMeasured"]),
        [typeof(PartyReputation)] = (["ChangeReputation", "ChangeFame"], []),
        [typeof(PartyRecords)] = (["Mark", "Set", "Remove"], ["Has", "CountOf"]),
        [typeof(PartyHoldings)] = (["Hold"], ["BalanceOf"]),
        [typeof(PartyPassages)] = (["Hold", "Spend"], ["Holds", "RouteTo", "IndexOf"]),
        [typeof(PartyMemberships)] = (["Grant"], ["Holds"]),
        [typeof(PartyInventory)] = (["Append", "Remove"], ["Contains", "Find", "TotalOf"]),
        [typeof(ItemInstance)] = (["Identify", "TakeDamage", "Repair", "SpendCharge", "Place", "RemoveFromStack"], ["ToString"]),
        [typeof(ActiveEffects)] = (["Apply", "Remove"], ["Has", "MagnitudeOf", "IndexOf"]),
        [typeof(CharacterProfile)] = (["Rename", "ChangeClass"], []),
        [typeof(CharacterAttributes)] = (["Set", "Change"], ["TryGet", "IndexOf"]),
        [typeof(CharacterSkills)] = (["Learn", "RaiseLevel", "SetTier"], ["Knows", "LevelOf", "TierOf", "TryGet", "IndexOf"]),
        [typeof(CharacterSpells)] = (["SetQuickSpell", "Learn", "Forget"], ["Knows"]),
        [typeof(CharacterProgression)] = (["Age", "Rejuvenate", "AwardExperience", "SetLevel", "GrantSkillPoints", "SpendSkillPoints", "SetClassRank"], []),
        [typeof(CharacterConditions)] = (["Apply", "Clear", "ClearAll"], ["Has", "SeverityOf", "IndexOf"]),
        [typeof(CharacterResources)] = (["TakeDamage", "RestoreHitPoints", "SetMaximumHitPoints", "TrySpendSpellPoints", "RestoreSpellPoints", "SetMaximumSpellPoints", "RestoreAll"], []),
        [typeof(CharacterEquipment)] = (["Attach", "Detach"], ["Has", "TryGet", "ItemIn", "IndexOf"]),
        [typeof(CharacterResistances)] = (["Set"], ["Of"]),
    };

    /// <summary>Each way the party's state changes, through the owners' own doors.</summary>
    public static TheoryData<string> Changes() =>
    [
        "purse credit", "purse debit", "food credit", "food spend", "food debit", "reputation", "fame",
        "record mark", "record set", "record remove", "holding", "passage hold", "passage spend", "membership",
        "acquire", "release", "consume", "acquire part of a stack", "identify", "damage item", "repair item", "spend a charge",
        "party effect start", "party effect end", "member effect start",
        "rename", "change class", "attribute set", "attribute change",
        "skill learn", "skill raise", "skill tier", "spell learn", "quick spell", "spell forget",
        "age", "rejuvenate", "experience award", "teach", "gift", "stored resistance",
        "condition apply", "condition clear", "conditions clear all",
        "take damage", "restore hit points", "maximum hit points", "spend spell points", "restore spell points",
        "maximum spell points", "restore all", "equip", "unequip",
    ];

    [Fact]
    public void Every_method_an_owner_of_party_state_declares_is_known_to_change_it_or_only_to_read_it()
    {
        foreach ((Type owner, (string[] changes, string[] reads)) in Owners)
        {
            string[] declared = [.. owner
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(method => !method.IsSpecialName && !method.Name.Contains('<', StringComparison.Ordinal))
                .Select(method => method.Name)
                .Where(name => name is not ("Equals" or "GetHashCode"))
                .Distinct()
                .Order(StringComparer.Ordinal)];
            Assert.True(
                declared.SequenceEqual(changes.Concat(reads).Distinct().Order(StringComparer.Ordinal)),
                $"{owner.Name} declares [{string.Join(", ", declared)}]; sort each into what changes the party (and take a change stamp in it) or what only reads it.");
            Assert.NotNull(owner.GetProperty("Stamp"));
        }
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public void Every_change_to_the_party_moves_its_stamp(string change)
    {
        using PartyEntity party = TestParty.OfFour();
        PartyMember ann = party.Members[0];
        PartyMember bo = party.Members[1];
        ItemInstance coat = party.CreateItem(Coat);
        Assert.True(party.AcquireItem(coat).Admitted);
        party.Records.Mark("seen");
        party.Passages.Hold(new PlaceId("harbour"), "ferry");
        GameClock clock = TestClock.Create();
        RunningSpellEffects effects = new(party, clock);
        effects.Start(new EffectId("ward"), 3, GameDuration.FromMinutes(5));
        bo.Spells.Learn(new SpellId("spark"));
        ann.Conditions.Apply(new ActiveCondition(new ConditionId("weak")));
        PartyProgression progression = new(new Shares(), party);

        long before = party.Stamp;
        Action act = change switch
        {
            "purse credit" => () => party.Purse.Credit(1),
            "purse debit" => () => Assert.True(party.Purse.TryDebit(1)),
            "food credit" => () => party.Food.Credit(1),
            "food spend" => () => Assert.True(party.Food.TrySpend(new Provisions(1, ProvisionUnit.Portions))),
            "food debit" => () => Assert.True(party.Food.TryDebit(1)),
            "reputation" => () => party.Reputation.ChangeReputation(-1),
            "fame" => () => party.Reputation.ChangeFame(1),
            "record mark" => () => party.Records.Mark("met"),
            "record set" => () => party.Records.Set("seen", 2),
            "record remove" => () => Assert.True(party.Records.Remove("seen")),
            "holding" => () => party.Holdings.Hold("bank", 50),
            "passage hold" => () => party.Passages.Hold(new PlaceId("island"), "boat"),
            "passage spend" => () => Assert.True(party.Passages.Spend(new PlaceId("harbour"))),
            "membership" => () => party.Memberships.Grant("guild"),
            "acquire" => () => Assert.True(party.AcquireItem(new ItemDefinitionId("rope")).Admitted),
            "release" => () => Assert.NotNull(party.ReleaseItem(coat.Id)),
            "consume" => () => Assert.NotNull(party.ConsumeItem(coat.Id)),
            "acquire part of a stack" => () => Assert.True(party.AcquireItem(party.CreateItem(new ItemDefinitionId("arrow"), stackCount: 3)).Admitted),
            "identify" => () => coat.Identify(),
            "damage item" => () => coat.TakeDamage(1),
            "repair item" => () => coat.Repair(1),
            "spend a charge" => () => Assert.True(party.SpendItemCharge(coat.Id, capacity: 5).Refusal is null),
            "party effect start" => () => effects.Start(new EffectId("light"), 1, GameDuration.FromMinutes(5)),
            "party effect end" => () => Assert.True(effects.End(new EffectId("ward"))),
            "member effect start" => () => effects.StartOn(ann, new EffectId("haste"), 1, GameDuration.FromMinutes(5)),
            "rename" => () => ann.Profile.Rename("Annabel"),
            "change class" => () => ann.Profile.ChangeClass(new ClassId("knight")),
            "attribute set" => () => ann.Attributes.Set(new AttributeId("vigour"), 18),
            "attribute change" => () => ann.Attributes.Change(new AttributeId("wit"), 1),
            "skill learn" => () => ann.Skills.Learn(new SkillId("mail"), new SkillTier(1)),
            "skill raise" => () => ann.Skills.RaiseLevel(new SkillId("blades"), 1, 2),
            "skill tier" => () => ann.Skills.SetTier(new SkillId("blades"), new SkillTier(2)),
            "spell learn" => () => Assert.True(ann.Spells.Learn(new SpellId("spark"))),
            "quick spell" => () => Assert.True(bo.Spells.SetQuickSpell(new SpellId("spark"))),
            "spell forget" => () => Assert.True(bo.Spells.Forget(new SpellId("spark"))),
            "age" => () => ann.Progression.Age(2),
            "rejuvenate" => () => ann.Progression.Rejuvenate(),
            "experience award" => () => Assert.True(progression.Award(new PartyExperienceAward("deed", 40)).IsAwarded),
            "teach" => () => progression.Teach(ann.Id, new SkillId("lore"), new SkillTier(1), 1),
            "gift" => () => progression.Gift(ann.Id, 10, 2),
            "stored resistance" => () => ann.Resistances.Set(new PartyRpg.Kit.Combat.DamageKindId("fire"), 10),
            "condition apply" => () => ann.Conditions.Apply(new ActiveCondition(new ConditionId("cursed"))),
            "condition clear" => () => Assert.True(ann.Conditions.Clear(new ConditionId("weak"))),
            "conditions clear all" => () => ann.Conditions.ClearAll(),
            "take damage" => () => ann.Resources.TakeDamage(3),
            "restore hit points" => () => ann.Resources.RestoreHitPoints(1),
            "maximum hit points" => () => ann.Resources.SetMaximumHitPoints(40),
            "spend spell points" => () => Assert.True(bo.Resources.TrySpendSpellPoints(1)),
            "restore spell points" => () => bo.Resources.RestoreSpellPoints(1),
            "maximum spell points" => () => bo.Resources.SetMaximumSpellPoints(12),
            "restore all" => () => ann.Resources.RestoreAll(),
            "equip" => () => Assert.True(party.Equip(ann.Id, Body, coat.Id).Refusal is null),
            "unequip" => () =>
            {
                Assert.True(party.Equip(ann.Id, Body, coat.Id).Refusal is null);
                before = party.Stamp;
                Assert.True(party.Unequip(ann.Id, Body).Refusal is null);
            },
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, "No such change."),
        };

        act();
        Assert.True(party.Stamp > before, $"'{change}' changed the party and left its stamp where it was.");

        // Reading the party changes nothing, so it leaves the stamp alone.
        long after = party.Stamp;
        _ = party.Items;
        _ = party.Records.All;
        Assert.Equal(after, party.Stamp);
    }

    /// <summary>A rule that shares every award evenly, so an award lands on the members.</summary>
    private sealed class Shares : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000;

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division) =>
            [.. division.Party.Members.Select(member => new ProgressionShare(member.Id, member.Profile.Name, division.Amount / division.Party.Members.Count))];

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }
}
