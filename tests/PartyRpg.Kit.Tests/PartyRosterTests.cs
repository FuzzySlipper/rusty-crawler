using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The party block's roster: each member as the adventure frame draws them — their face granted through the Engine,
/// their pools with the percentage a bar is drawn at, their conditions, and whether they are the one selected.
/// </summary>
public sealed class PartyRosterTests
{
    [Fact]
    public void Each_member_is_read_with_their_face_pools_and_selection()
    {
        using PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(
            [Member("Roderick", "human-man", hitPoints: 40, spellPoints: 0), Member("Aelina", "elf-woman", hitPoints: 20, spellPoints: 15)],
            coins: 0, foodPortions: 0, reputation: 0, fame: 0));
        party.Members[1].TakeDamage(15);
        Assert.Null(party.Roster.Select(party.Members[1].Id, Verdict.Met));

        RecordingUiService ui = new();
        (IContentService content, _) = RecordingEngineService<IContentService>.Create();
        using ContentImages portraits = new(new FakeEngineContext(ui, content: content),
            portrait => portrait == "human-man" ? "packs/media/icons/pc01-01.png" : null, "portraits");

        PartySnapshot snapshot = PartySnapshot.From(party, portraits: portraits);

        PartyMemberSnapshot knight = snapshot.Roster[0];
        Assert.Equal(("Roderick", "human-man", 40, 100, 0, false), (knight.Name, knight.Portrait, knight.HitPoints, knight.HitPointsPercent, knight.SpellPointsPercent, knight.Selected));
        Assert.Equal("/__rusty/product/runtime/ui-images/1", knight.PortraitImage);

        // A face content gives no image is published without a URL, and the wounded member's bar stands at a quarter.
        PartyMemberSnapshot sorcerer = snapshot.Roster[1];
        Assert.Equal((string.Empty, 5, 25, 100, true), (sorcerer.PortraitImage, sorcerer.HitPoints, sorcerer.HitPointsPercent, sorcerer.SpellPointsPercent, sorcerer.Selected));

        // One grant per face, however often it is read, and every grant revoked with the owner.
        PartySnapshot.From(party, portraits: portraits);
        Assert.Single(ui.Images);
        portraits.Dispose();
        Assert.Contains(1UL, ui.ReleasedImages);
    }

    [Fact]
    public void Selecting_a_member_moves_the_party_stamp_but_refusal_feedback_does_not()
    {
        using PartyEntity party = new PartyEntityFactory().Create(new PartyCreation(
            [Member("Roderick", "human-man", hitPoints: 40, spellPoints: 0), Member("Aelina", "elf-woman", hitPoints: 20, spellPoints: 15)],
            coins: 0, foodPortions: 0, reputation: 0, fame: 0));

        long before = party.Stamp;
        Assert.Null(party.Roster.Select(party.Members[1].Id, Verdict.Met));
        Assert.Equal(party.Members[1].Id, party.Roster.SelectedMember);
        Assert.NotEqual(before, party.Roster.Stamp);
        Assert.NotEqual(before, party.Stamp);

        long after = party.Stamp;
        Refusal refused = Assert.IsType<Refusal>(party.Roster.Select(new PartyMemberId(999), Verdict.Met));
        Assert.Equal(PartySelectionCodes.UnknownMember, refused.Code);
        Assert.Equal(after, party.Stamp);
    }

    [Fact]
    public void A_pool_below_empty_or_without_a_measure_draws_an_empty_bar()
    {
        PartyMemberSnapshot member = new("1", "Borin", "Cleric", string.Empty, string.Empty, -12, 25, 3, 0, string.Empty, false);
        Assert.Equal((0, 0), (member.HitPointsPercent, member.SpellPointsPercent));
    }

    private static MemberCreation Member(string name, string portrait, int hitPoints, int spellPoints) => new(new PartyMemberSeed(
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
        hitPoints: ResourcePool.Full(hitPoints),
        spellPoints: ResourcePool.Full(spellPoints),
        portrait: new PortraitId(portrait)));
}
