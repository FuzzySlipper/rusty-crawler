using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// A party of four this suite owns: what a save or a load is proved against, stated here rather than borrowed from
/// a game's own creation, so a retune of the game's starting purse cannot move a kit test.
/// </summary>
/// <remarks>
/// Every member differs from its neighbours in portrait, race, class, and what it knows, so a value a save failed
/// to carry cannot pass for one it carried.
/// </remarks>
internal static class TestParty
{
    /// <summary>What the party's purse starts with.</summary>
    internal const int StartingCoins = 200;

    /// <summary>What the party's larder starts with.</summary>
    internal const int StartingFoodPortions = 10;

    /// <summary>A freshly created party of four, as the factory builds one.</summary>
    internal static PartyEntity OfFour() =>
        new PartyEntityFactory().Create(new PartyCreation(
            [
                Member("Ann", "testfolk", "fighter", "folk-a", "blades", might: 16),
                Member("Bo", "stonefolk", "adept", "stone-a", "wards", might: 9),
                Member("Cid", "testfolk", "adept", "folk-b", "lore", might: 11),
                Member("Dee", "stonefolk", "fighter", "stone-b", "axes", might: 14),
            ],
            coins: StartingCoins,
            foodPortions: StartingFoodPortions,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));

    private static MemberCreation Member(string name, string race, string characterClass, string portrait, string skill, int might) =>
        new(new PartyMemberSeed(
            name,
            new RaceId(race),
            new ClassId(characterClass),
            [new AttributeScore(new AttributeId("vigour"), might), new AttributeScore(new AttributeId("wit"), 20 - might)],
            skills: [new SkillEntry(new SkillId(skill), 1, new SkillTier(1), 0)],
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(30),
            spellPoints: ResourcePool.Full(characterClass == "adept" ? 10 : 0),
            portrait: new PortraitId(portrait)));
}
