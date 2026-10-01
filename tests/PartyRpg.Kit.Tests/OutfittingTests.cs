using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one way a player changes what a member wears: which slot an unnamed change goes to, that every change and
/// every refusal goes through the party's own equip, and what the panel is told.
/// </summary>
/// <remarks>
/// The figure here is the test's own: two hands, a body, and two fingers, with a rule that refuses one item by name.
/// Nothing in the kit knows any of those words.
/// </remarks>
public sealed class OutfittingTests
{
    private static readonly EquipmentSlot Hand = new("hand");
    private static readonly EquipmentSlot OtherHand = new("other hand");
    private static readonly EquipmentSlot Body = new("body");
    private static readonly EquipmentSlot FingerOne = new("finger one");
    private static readonly EquipmentSlot FingerTwo = new("finger two");
    private static readonly ItemDefinitionId Blade = new("blade");
    private static readonly ItemDefinitionId Ring = new("ring");
    private static readonly ItemDefinitionId Coat = new("coat");
    private static readonly ItemDefinitionId Rock = new("rock");
    private static readonly ItemDefinitionId Cursed = new("cursed");

    [Fact]
    public void An_unnamed_change_fills_a_free_slot_the_figure_offers_before_displacing_anything()
    {
        using PartyEntity party = Party();
        PartyOutfitting outfitting = new(party, new Figure());
        ItemInstance first = party.AcquireItem(Ring).Item!;
        ItemInstance second = party.AcquireItem(Ring).Item!;
        ItemInstance third = party.AcquireItem(Ring).Item!;

        Assert.Equal(FingerOne.Value, outfitting.Equip(0, first.Id).Slot);
        Assert.Equal(FingerTwo.Value, outfitting.Equip(0, second.Id).Slot);

        // Both fingers are full, so the third ring displaces the first finger's, which goes back to the pack.
        OutfittingResult swapped = outfitting.Equip(0, third.Id);
        Assert.Equal(OutfittingResult.Equipped, swapped.Outcome);
        Assert.Equal(FingerOne.Value, swapped.Slot);
        Assert.Equal(first.Id.ToString(), swapped.Displaced);
        Assert.Contains(first, party.Inventory.Items);
        Assert.Same(swapped, outfitting.Last);
    }

    [Fact]
    public void A_refusal_is_the_rules_own_and_changes_nothing()
    {
        using PartyEntity party = Party(new RefuseCursed());
        PartyOutfitting outfitting = new(party, new Figure());
        ItemInstance cursed = party.AcquireItem(Cursed).Item!;
        ItemInstance rock = party.AcquireItem(Rock).Item!;
        ItemInstance blade = party.AcquireItem(Blade).Item!;

        // The use rule refuses at every slot the figure offers, and the first try's refusal is the answer.
        OutfittingResult refused = outfitting.Equip(0, cursed.Id);
        Assert.Equal(OutfittingResult.Refused, refused.Outcome);
        Assert.Equal("cursed-refused", refused.Code);
        Assert.Equal(0, party.Members[0].Equipment.Count);

        Assert.Equal(PartyCodes.ItemNotWearable, outfitting.Equip(0, rock.Id).Code);
        Assert.Equal(PartyCodes.SlotNotOnFigure, outfitting.Equip(0, blade.Id, new EquipmentSlot("tail")).Code);
        Assert.Equal(PartyCodes.EquipMemberUnknown, outfitting.Equip(3, blade.Id).Code);
        Assert.Equal(PartyCodes.SlotEmpty, outfitting.Unequip(0, Body).Code);
        Assert.Equal(3, party.Inventory.Count);
    }

    [Fact]
    public void The_session_applies_the_panels_change_and_publishes_the_figure()
    {
        using PartyEntity party = Party();
        PartyOutfitting outfitting = new(party, new Figure());
        ItemInstance blade = party.AcquireItem(Blade).Item!;
        ItemInstance coat = party.AcquireItem(Coat).Item!;
        EquipInput input = new(new EquipIntentNames("test.action"));

        ActionInbox inbox = new([
            Admitted.Payload("test.action", $$"""{"action":"party.equip","member":0,"item":"{{blade.Id}}","slot":"other hand"}"""),
            Admitted.Payload("test.action", $$"""{"action":"party.equip","member":0,"item":"{{coat.Id}}"}"""),
            Admitted.Payload("test.action", """{"action":"party.unequip","member":0}"""),
        ]);
        IReadOnlyList<EquipRequest> requests = input.Read(inbox);

        // An unequip that names no slot says nothing to change, so it is not read.
        Assert.Equal(2, requests.Count);
        foreach (EquipRequest request in requests) outfitting.Equip(request.Member, request.Item!.Value, request.Slot);

        EquipmentSnapshot snapshot = EquipmentSnapshot.From(outfitting);
        Assert.True(snapshot.Available);
        Assert.Equal(["hand", "other hand", "body", "finger one", "finger two"], snapshot.Slots);
        Assert.Equal(["other hand", "body"], snapshot.Members[0].Worn.Select(worn => worn.Slot));
        Assert.Empty(snapshot.Items);

        outfitting.Unequip(0, Body);
        Assert.Equal(["body"], EquipmentSnapshot.From(outfitting).Items.Single().Slots);
        Assert.False(EquipmentSnapshot.From(null).Available);
    }

    private static PartyEntity Party(IEquipmentUseRule? rule = null) =>
        new PartyEntityFactory(equipmentUse: rule).Create(new PartyCreation(
            [new MemberCreation(new PartyMemberSeed(
                "Ann",
                new RaceId("testfolk"),
                new ClassId("fighter"),
                [new AttributeScore(new AttributeId("vigour"), 12)],
                [],
                [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(10),
                spellPoints: ResourcePool.Full(5)))],
            coins: 0,
            foodPortions: 0));

    private sealed class Figure : IEquipmentFigure
    {
        public IReadOnlyList<EquipmentSlot> Slots { get; } = [Hand, OtherHand, Body, FingerOne, FingerTwo];

        public IReadOnlyList<EquipmentSlot> SlotsFor(ItemInstance item) => item.Definition.Value switch
        {
            "blade" or "cursed" => [Hand, OtherHand],
            "coat" => [Body],
            "ring" => [FingerOne, FingerTwo],
            _ => [],
        };
    }

    private sealed class RefuseCursed : IEquipmentUseRule
    {
        public Refusal? Judge(PartyMember member, EquipmentSlot slot, ItemInstance item) =>
            item.Definition == Cursed ? new Refusal("cursed-refused", $"Nobody wears the cursed thing in '{slot}'.") : null;
    }
}
