using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// What a member wears, changed through the panel's own control and read into every sum the fight prices a
/// character by.
/// </summary>
/// <remarks>
/// The live half goes the whole way: the equip payload the companion sends, the party's own equip judged by this
/// game's use rule, and the act control the product reads — and the fight's own panel says what the blow was worth.
/// The numbers are the donor's sums over a staged character whose accuracy, might and speed are each worth what
/// the attribute table states (OpenEnroth <c>src/Engine/Objects/Character.cpp</c>, cited at each sum in
/// <c>MightAndMagic7Combat</c>), and each is written out here independently of the code that computes it.
/// </remarks>
public sealed class EquipmentPolicyTests
{
    private static readonly EquipIntentNames EquipControl = new(Declared.UiActionContract);

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_member_who_equips_a_weapon_through_the_panel_attacks_with_its_dice_and_recovery()
    {
        // Bare hands: a three-sided die, and the engine fake answers its maximum, so three plus the might bonus
        // of two. One point of attack bonus (an accuracy of fifteen) against five points of armour is thirty-seven
        // outcomes of which seventeen land (Character.cpp:6263-6300). The recovery is a staff's hundred ticks
        // for a character holding nothing, less the speed bonus of two (Character.cpp:1636-1750).
        Blow bare = Strike();
        Assert.Equal(5d, bare.Rolled);
        Assert.Equal(Chance(17, 37), bare.Chance);
        Assert.Equal(Seconds(98), bare.RecoverySeconds);

        // A longsword the member has learned: its row's three three-sided dice, its modifier of nothing, a sword's
        // skill adding no damage below grand master, and the might bonus — nine and two. The sword level adds a
        // point of attack bonus, so thirty-nine outcomes of which nineteen land; and the sword's ninety ticks less
        // two pace the next blow.
        Blow armed = Strike(Sword);
        Assert.Equal(11d, armed.Rolled);
        Assert.Equal(Chance(19, 39), armed.Chance);
        Assert.Equal(Seconds(88), armed.RecoverySeconds);

        // Leather armour worn at novice adds its ten ticks to every blow (Character.cpp:1673-1691).
        Blow armoured = Strike(Sword, Leather);
        Assert.Equal(11d, armoured.Rolled);
        Assert.Equal(Seconds(98), armoured.RecoverySeconds);

        // A bow makes the member's attack a shot: its own five two-sided dice and nothing for might, the bow level
        // as attack bonus, and the bow's hundred ticks less two.
        Blow shot = Strike(Bow);
        Assert.Equal("ranged", shot.Kind);
        Assert.Equal(10d, shot.Rolled);
        Assert.Equal(Chance(19, 39), shot.Chance);
        Assert.Equal(Seconds(98), shot.RecoverySeconds);
    }

    [Fact]
    [Trait(Pins.Trait, Pins.Tuning)]
    public void A_member_wearing_armour_is_hit_less_often_and_a_worn_weapon_changes_the_blow()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content());
        using IGameSession session = Session(context, ui);
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat rule = MightAndMagic7Combat.Compose(Catalog(context), random: null, party: () => live.Party);
        (CombatSubject member, CombatSubject beast) = Subjects(live, rule);

        // A level-two creature against a character of two points of armour class (the speed bonus): sixteen
        // outcomes, nine of which land (Actor.cpp:3691-3707).
        Assert.Equal(Chance(9, 16), (double)rule.PlanOf(beast, member, AttackKind.Melee).Chance.BasisPoints);
        AttackPlan bare = rule.PlanOf(member, beast, AttackKind.Melee);
        Assert.Equal(new DamageRoll(1, 3, 2, floor: 1), bare.Damage);

        Wear(session, ui, 2, Leather);
        Wear(session, ui, 3, Sword);

        // Leather armour is its row's four points, and the leather skill at novice one more (Character.cpp:2299-2304,
        // :2596-2648): seven points of armour class, so twenty-one outcomes of which still nine land.
        Assert.Equal(Chance(9, 21), (double)rule.PlanOf(beast, member, AttackKind.Melee).Chance.BasisPoints);
        AttackPlan armed = rule.PlanOf(member, beast, AttackKind.Melee);
        Assert.Equal(new DamageRoll(3, 3, 2, floor: 1), armed.Damage);
        Assert.True(armed.Chance.BasisPoints > bare.Chance.BasisPoints, "the sword's skill adds to the chance to land");

        // The sword's ninety ticks and the leather's ten, less the speed bonus of two.
        Assert.Equal(Seconds(98), rule.RecoveryAfter(member, AttackKind.Melee).Milliseconds / 1000d);
    }

    [Fact]
    public void A_change_this_game_refuses_is_refused_by_name_and_leaves_the_figure_as_it_was()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content());
        using IGameSession session = Session(context, ui);
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember roderick = live.Party!.Members[0];

        // Plate needs the plate skill, which this character has not learned.
        Assert.Equal(MightAndMagic7Codes.EquipmentSkillMissing, Refused(session, ui, 2, Plate));

        // A sword in the boots is in the wrong place whoever holds it.
        Assert.Equal(MightAndMagic7Codes.EquipmentWrongSlot, Refused(session, ui, 3, Sword, slot: "boots"));

        // A place the figure does not have is refused before anything is judged.
        Assert.Equal(PartyCodes.SlotNotOnFigure, Refused(session, ui, 4, Sword, slot: "tail"));

        // A dagger goes in the off hand only for an expert of the dagger (UICharacter.cpp:1908-1910).
        Assert.Equal(MightAndMagic7Codes.EquipmentOffHandUntrained, Refused(session, ui, 5, Dagger, slot: "off hand"));

        // A shield in the off hand leaves no room for a weapon held in both hands, and the refusal says what to take off.
        Wear(session, ui, 6, Shield);
        Assert.Equal(MightAndMagic7Codes.EquipmentHandsFull, Refused(session, ui, 7, Greatsword));

        // A potion is not something a character wears at all, and the pack's list never offered it.
        ProjectedNode equipment = Equipment(ui);
        Assert.DoesNotContain(Pack(equipment), row => row.Definition == Potion);
        Assert.Equal(PartyCodes.ItemNotWearable, Refused(session, ui, 8, Potion));

        // Every refusal left the figure exactly as it was: the shield, and nothing else.
        Assert.Equal([MightAndMagic7Figure.OffHand], roderick.Equipment.Items.Select(worn => worn.Slot));

        // Taking the shield off puts it back in the pack, and the greatsword then goes in.
        session.Update(RulesetTestContext.Update(9, 1, RulesetTestContext.Payload("""{"action":"party.unequip","member":0,"slot":"off hand"}""")));
        Assert.Equal("unequipped", Equipment(ui).Field("outcome").Field("outcome").AsString());
        Wear(session, ui, 10, Greatsword);
        Assert.Equal("main hand", Equipment(ui).Field("members").Item(0).Field("worn").Item(0).Field("slot").AsString());
    }

    [Fact]
    public void A_save_carries_what_each_member_wears_and_the_resumed_fight_reads_it()
    {
        InMemoryPersistenceService persistence = new();
        (string Path, string Text)[] content = Content();
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = Session(context, ui);
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        Wear(session, ui, 2, Sword);
        Wear(session, ui, 3, Leather);
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Equal(2, written.Party.Items.Count(item => item.Custody.IsEquipped));

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumedContext, resumedUi, combat: true) with { Start = SessionStart.Resume, Equip = EquipControl });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1));

        ProjectedNode worn = Equipment(resumedUi).Field("members").Item(0).Field("worn");
        Assert.Equal(["main hand", "armour"], Enumerable.Range(0, worn.Length()).Select(index => worn.Item(index).Field("slot").AsString()));

        MightAndMagic7Session live = (MightAndMagic7Session)resumed;
        MightAndMagic7Combat rule = MightAndMagic7Combat.Compose(Catalog(resumedContext), random: null, party: () => live.Party);
        (CombatSubject member, CombatSubject beast) = Subjects(live, rule);
        Assert.Equal(new DamageRoll(3, 3, 2, floor: 1), rule.PlanOf(member, beast, AttackKind.Melee).Damage);
        Assert.Equal(Chance(9, 21), (double)rule.PlanOf(beast, member, AttackKind.Melee).Chance.BasisPoints);
    }

    private const string Sword = "1";
    private const string Greatsword = "2";
    private const string Dagger = "15";
    private const string Bow = "60";
    private const string Leather = "66";
    private const string Plate = "76";
    private const string Shield = "84";
    private const string Potion = "222";

    /// <summary>One blow of the party's member, read off the fight's own panel.</summary>
    private sealed record Blow(string Kind, double Chance, double Rolled, double RecoverySeconds);

    /// <summary>Equips each definition through the panel, then orders one attack, and reads what the fight published.</summary>
    private static Blow Strike(params string[] wear)
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content());
        using IGameSession session = Session(context, ui);
        session.Start();
        ulong step = 0;
        session.Update(RulesetTestContext.Update(++step, 1));
        foreach (string definition in wear) Wear(session, ui, ++step, definition);
        session.Update(RulesetTestContext.Update(++step, 1, RulesetTestContext.Digital(Declared.AttackIntent)));

        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(combat.Field("resolved").AsBoolean(), combat.Field("message").AsString());
        return new Blow(
            combat.Field("kind").AsString(),
            combat.Field("chance").AsNumber(),
            combat.Field("damageRolled").AsNumber(),
            combat.Field("recoverySeconds").AsNumber());
    }

    /// <summary>Puts on the pack's one item of a definition, as the panel's own control does, and requires it was worn.</summary>
    private static void Wear(IGameSession session, RecordingUiService ui, ulong step, string definition)
    {
        Equip(session, ui, step, definition, slot: null);
        ProjectedNode outcome = Equipment(ui).Field("outcome");
        Assert.True(outcome.Field("outcome").AsString() == "equipped", outcome.Field("message").AsString());
    }

    /// <summary>Asks for one change through the panel's own control, and returns the refusal's code.</summary>
    private static string Refused(IGameSession session, RecordingUiService ui, ulong step, string definition, string? slot = null)
    {
        Equip(session, ui, step, definition, slot);
        ProjectedNode outcome = Equipment(ui).Field("outcome");
        Assert.Equal("refused", outcome.Field("outcome").AsString());
        return outcome.Field("code").AsString();
    }

    private static void Equip(IGameSession session, RecordingUiService ui, ulong step, string definition, string? slot)
    {
        PartyEntity party = ((MightAndMagic7Session)session).Party!;
        ItemInstance item = party.Inventory.Items.First(candidate => candidate.Definition.Value == definition);
        string named = slot is null ? string.Empty : $", \"slot\": \"{slot}\"";
        session.Update(RulesetTestContext.Update(
            step,
            1,
            RulesetTestContext.Payload(string.Create(CultureInfo.InvariantCulture, $$"""{"action":"party.equip","member":0,"item":"{{item.Id}}"{{named}}}"""))));
    }

    private static ProjectedNode Equipment(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("equipment");

    private static IEnumerable<(string Item, string Definition)> Pack(ProjectedNode equipment)
    {
        ProjectedNode items = equipment.Field("items");
        return Enumerable.Range(0, items.Length())
            .Select(index => (items.Item(index).Field("item").AsString(), items.Item(index).Field("definition").AsString()))
            .ToArray();
    }

    private static IGameSession Session(ProductCreateContext context, RecordingUiService ui) =>
        MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with { Equip = EquipControl });

    private static ContentCatalog Catalog(ProductCreateContext context) =>
        ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();

    /// <summary>The party's member and the creature, as the fight itself knows them.</summary>
    private static (CombatSubject Member, CombatSubject Beast) Subjects(MightAndMagic7Session live, MightAndMagic7Combat rule)
    {
        CombatState fight = new(Capabilities.Combat(rule), live.Party!, live.World);
        fight.Step();
        return (
            fight.Combatants.First(combatant => combatant.Subject.IsMember).Subject,
            fight.Combatants.First(combatant => !combatant.Subject.IsMember).Subject);
    }

    /// <summary>A chance of so many outcomes in so many, in the ten-thousandths the fight publishes.</summary>
    private static double Chance(int hits, int outcomes) =>
        Math.Round(hits * 10000d / outcomes, MidpointRounding.AwayFromZero);

    /// <summary>What a recovery of so many of the donor's ticks is in game seconds, as the fight publishes it.</summary>
    private static double Seconds(int ticks) =>
        Math.Round(
            ticks * 1000d / MightAndMagic7Combat.TicksPerRealSecond * MightAndMagic7Time.Scale.GameSecondsPerRealSecond,
            MidpointRounding.AwayFromZero) / 1000d;

    /// <summary>A world of one creature beside the party, a skill and an item table, and a party of one fighter.</summary>
    private static (string Path, string Text)[] Content() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            TestPacks.Manifest(
                "world",
                ("places", "place"),
                ("monsters", "monster"),
                ("skills", "skill"),
                ("items", "item"),
                ("start", "scenario-start"),
                ("party", "scenario-party"))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Home", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 512 } ],
                  "placements": [ { "id": "beast", "kind": "monster", "monster": "7", "x": 100, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "level": 2, "hitPoints": 400, "armorClass": 5, "hostility": 2,
                  "recovery": 100, {{MonsterRows.Combat(7, "Phys", "2D8+10", "0", "0")}} }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Sword" }, { "id": "Dagger" }, { "id": "Bow" }, { "id": "Shield" }, { "id": "Leather" }, { "id": "Plate" } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "1", "name": "A longsword", "value": 50, "type": "single-handed", "skill": "sword", "damageDice": "3d3", "damageModifier": "0" },
                { "id": "2", "name": "A greatsword", "value": 90, "type": "two-handed", "skill": "sword", "damageDice": "3d4", "damageModifier": "1" },
                { "id": "15", "name": "A dagger", "value": 8, "type": "single-handed", "skill": "dagger", "damageDice": "2d2", "damageModifier": "0" },
                { "id": "60", "name": "A longbow", "value": 80, "type": "bow", "skill": "bow", "damageDice": "5d2", "damageModifier": "0" },
                { "id": "66", "name": "Leather armour", "value": 150, "type": "armour", "skill": "leather", "damageDice": "4", "damageModifier": "0" },
                { "id": "76", "name": "Plate armour", "value": 900, "type": "armour", "skill": "plate", "damageDice": "20", "damageModifier": "0" },
                { "id": "84", "name": "A buckler", "value": 100, "type": "shield", "skill": "shield", "damageDice": "4", "damageModifier": "0" },
                { "id": "222", "name": "A potion", "value": 10, "type": "potion", "skill": "misc", "damageDice": "", "damageModifier": "0" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 200, "food": 6, "reputation": 0, "fame": 0,
                  "pack": [
                    { "item": "1", "count": 1 }, { "item": "2", "count": 1 }, { "item": "15", "count": 1 },
                    { "item": "60", "count": 1 }, { "item": "66", "count": 1 }, { "item": "76", "count": 1 },
                    { "item": "84", "count": 1 }, { "item": "222", "count": 1 }
                  ],
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1,
                      "hitPoints": 40, "spellPoints": 0, "attributes": [
                        { "id": "Might", "value": 17 }, { "id": "Accuracy", "value": 15 },
                        { "id": "Endurance", "value": 15 }, { "id": "Luck", "value": 11 },
                        { "id": "Speed", "value": 17 }, { "id": "Personality", "value": 11 },
                        { "id": "Intellect", "value": 11 } ],
                      "skills": [
                        { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 },
                        { "id": "Bow", "level": 1, "tier": 1, "pointsSpent": 1 },
                        { "id": "Shield", "level": 1, "tier": 1, "pointsSpent": 1 },
                        { "id": "Leather", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
    ];
}
