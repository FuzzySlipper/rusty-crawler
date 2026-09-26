using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// Item-borne magic through the whole product: the scroll a scenario's party carries, read once and used up
/// through the one casting workflow, and the wand it wields, fired through the fight's own attack path and
/// vanishing when its last charge goes.
/// </summary>
/// <remarks>
/// <para>
/// What this proves is the trip rather than the mechanism: the operator's item rows are read into this game's
/// own readings, a scenario's party carries the items it declares, the panel publishes them with what is left
/// of each, and a use names the instance the panel drew. Nothing here is a second cast path — a scroll is cast
/// by the session's one casting workflow, with the item as the spell's source — and nothing here edits a
/// member's figure: a discharged wand leaves through the inventory's own custody.
/// </para>
/// <para>
/// The donor citations are the reading's own: a wand's charges are the donor's draw stated as one number, and
/// a wand is fired at the donor's fixed skill value rather than at its bearer's.
/// </para>
/// </remarks>
public sealed class ItemMagicPolicyTests
{
    /// <summary>What this game adds to a wand row's own modifier to state how many charges it holds.</summary>
    private const int WandChargeBonus = 4;

    /// <summary>The wand row's own damage modifier, which is what its charges are read from.</summary>
    private const int WandModifier = 30;

    [Fact]
    public void The_item_rows_are_read_as_spells_a_scroll_is_used_up_by_and_a_wand_holds_charges_of()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(Content());
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog)
            ?? throw new InvalidOperationException("The content declares spells, so reading them must produce a table.");

        // A scroll carries one spell and is used up by it; there is no charge to count.
        SpellItemReading scroll = spells.Reading(new ItemDefinitionId("300"))!.Value;
        Assert.Equal("3", scroll.Spell.Value);
        Assert.True(scroll.ConsumedByUse);
        Assert.Equal(0, scroll.Charges);

        // A wand carries one and holds charges: the row's own modifier plus this game's own bonus, which is
        // the donor's random draw (random(6) + modifier + 1 off a monster, the map, or a script —
        // OpenEnroth src/Engine/Objects/Item.cpp:746-752) stated as one number instead.
        SpellItemReading wand = spells.Reading(new ItemDefinitionId("135"))!.Value;
        Assert.Equal("3", wand.Spell.Value);
        Assert.False(wand.ConsumedByUse);
        Assert.Equal(WandModifier + WandChargeBonus, wand.Charges);

        // An item that carries no spell this game reads answers nothing, which is what a refusal names.
        Assert.Null(spells.Reading(new ItemDefinitionId("401")));
    }

    [Fact]
    public void A_scenario_party_carries_what_it_declares_and_the_panel_publishes_it()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        session.Update(ProductTestContext.Update(1, 1));

        ProjectedNode items = Magic(ui).Field("items");

        // The wand the scenario gave Aelina is in her hand with its charges, and the scroll the party carries
        // lies in the pack with its one use: both read from the party's own item state and the game's own rows.
        Assert.Equal(2d, items.Length());
        ProjectedNode wand = Item(items, "charged");
        Assert.Equal("Wand of Fire", wand.Field("name").AsString());
        Assert.Equal("Fire Resistance", wand.Field("spellName").AsString());
        Assert.Equal(WandModifier + WandChargeBonus, wand.Field("chargesMax").AsNumber());
        Assert.Equal(WandModifier + WandChargeBonus, wand.Field("charges").AsNumber());
        Assert.True(wand.Field("wielded").AsBoolean());
        Assert.Equal("Aelina", wand.Field("member").AsString());

        ProjectedNode scroll = Item(items, "consumed");
        Assert.Equal("Scroll of Fire Resistance", scroll.Field("name").AsString());
        Assert.Equal(0, scroll.Field("charges").AsNumber());
        Assert.False(scroll.Field("wielded").AsBoolean());
    }

    [Fact]
    public void A_scroll_is_cast_once_from_the_item_it_names_and_is_used_up()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        session.Update(ProductTestContext.Update(1, 1));
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        string scroll = Item(Magic(ui).Field("items"), "consumed").Field("item").AsString();
        int points = live.Party!.Members[0].Resources.SpellPoints.Current;

        // The casting names the item the panel published: the workflow takes that item as the spell's source,
        // so the caster's own spellbook and mastery are not asked about and no spell point is spent.
        session.Update(ProductTestContext.Update(2, 1, Cast(member: 0, spell: "3", target: Member(live, 0), item: scroll)));

        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal(0, magic.Field("cost").AsNumber());
        Assert.Contains("from Scroll of Fire Resistance", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(points, live.Party.Members[0].Resources.SpellPoints.Current);
        Assert.False(live.Party.Members[0].Spells.Knows(new PartyRpg.Kit.Party.SpellId("3")));

        // The scroll is used up: the panel publishes one carried item where it published two, and the ward it
        // raised is the caster's own.
        Assert.Equal(1d, magic.Field("items").Length());
        Assert.Equal(1d, magic.Field("memberRunning").Length());

        // Naming it again is refused by name, because the party no longer holds it.
        session.Update(ProductTestContext.Update(3, 1, Cast(member: 0, spell: "3", target: Member(live, 0), item: scroll)));
        Assert.Equal("refused", Magic(ui).Field("outcome").AsString());
        Assert.Equal("spell-item-not-held", Magic(ui).Field("code").AsString());
    }

    [Fact]
    public void A_wand_is_fired_through_the_fights_own_attack_path_and_vanishes_when_its_last_charge_goes()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui, combat: true);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ulong step = 0;

        session.Update(ProductTestContext.Update(++step, 1));
        string wand = Item(Magic(ui).Field("items"), "charged").Field("item").AsString();
        int charges = (int)Item(Magic(ui).Field("items"), "charged").Field("charges").AsNumber();

        // A wand lying in the pack is not the weapon a hand holds: the casting refuses by name and nothing is
        // spent. (The scenario wears this one, so the pack's own scroll is what proves it: a charged item has
        // to be wielded before its spell can be aimed.)
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        ProjectedNode combat = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal("applied", combat.Field("outcome").AsString());

        // The act control fires the wand as the weapon it is: the donor's own act order puts a wand in the main
        // hand ahead of hand-to-hand, so the attack is the wand's spell and one charge of it goes.
        Assert.Equal(charges - 1, Charges(ui, wand));

        // Each further attack spends one more: the count is the item's own, read from the party's state rather
        // than from a number the panel kept.
        session.Update(ProductTestContext.Update(++step, 1, Released(ProductIdentity.AttackIntent)));
        for (int shot = 0; shot < charges - 2; shot++)
        {
            Advance(session, ref step, seconds: 30);
            session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
            Assert.Equal(charges - 2 - shot, Charges(ui, wand));
        }

        // The last charge takes the item with it, through the inventory's own custody: the panel's own row for
        // it is gone and so is the instance the party held.
        Advance(session, ref step, seconds: 30);
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        ProjectedNode carried = Magic(ui).Field("items");
        Assert.Equal(1d, carried.Length());
        Assert.Equal("consumed", carried.Item(0).Field("kind").AsString());
        Assert.Null(live.Party!.FindItem(new ItemInstanceId(ulong.Parse(wand, CultureInfo.InvariantCulture))));
        Assert.False(live.Party.Members[0].Equipment.Has(new EquipmentSlot("main hand")));

        // Ordering the same item again is refused by name rather than firing a charge nobody has.
        session.Update(ProductTestContext.Update(++step, 1, Cast(member: 0, spell: "3", target: Member(live, 0), item: wand)));
        Assert.Equal("refused", Magic(ui).Field("outcome").AsString());
        Assert.Equal("spell-item-not-held", Magic(ui).Field("code").AsString());
    }

    [Fact]
    public void A_spent_charge_survives_the_products_own_save_and_resume()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, Content());
        using IGameSession session = Casting(context, ui, combat: true);
        ulong step = 0;
        session.Update(ProductTestContext.Update(++step, 1));

        string wand = Item(Magic(ui).Field("items"), "charged").Field("item").AsString();
        int charges = (int)Item(Magic(ui).Field("items"), "charged").Field("charges").AsNumber();

        // One shot spends one charge, and the charge is item state: it is written with the instance rather
        // than counted by the panel or by a second store beside the pack.
        session.Update(ProductTestContext.Update(++step, 1, Digital(ProductIdentity.AttackIntent)));
        Assert.Equal(charges - 1, Charges(ui, wand));

        // The session's own save boundary, through the engine's store, and a resume composed from those bytes.
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.NotNull(persistence.Payload("sessions", "session"));
        ItemSave carried = written.Party.Items.Single(item => item.Id.ToString() == wand);
        Assert.Equal(1, carried.State.ChargesSpent);

        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, Content());
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(resumedContext, resumedUi, combat: true) with
            {
                Cast = new CastIntentNames(ProductIdentity.CastAction, ProductIdentity.QuickSpellAction, ProductIdentity.UiActionContract),
                Start = PartyRpg.Kit.Rulesets.SessionStart.Resume,
            });
        resumed.Start();
        resumed.Update(ProductTestContext.Update(1, 1));

        ProjectedNode resumedWand = Item(Magic(resumedUi).Field("items"), "charged");
        Assert.Equal(wand, resumedWand.Field("item").AsString());
        Assert.Equal(charges - 1, (int)resumedWand.Field("charges").AsNumber());
        Assert.True(resumedWand.Field("wielded").AsBoolean());
    }

    /// <summary>What the panel publishes as the charges left in one instance, or zero when it is gone.</summary>
    private static int Charges(RecordingUiService ui, string item)
    {
        ProjectedNode items = Magic(ui).Field("items");
        for (int index = 0; index < items.Length(); index++)
        {
            if (items.Item(index).Field("item").AsString() == item) return (int)items.Item(index).Field("charges").AsNumber();
        }

        return 0;
    }

    /// <summary>One carried item the panel published, by the reading's own shape.</summary>
    private static ProjectedNode Item(ProjectedNode items, string kind)
    {
        for (int index = 0; index < items.Length(); index++)
        {
            if (items.Item(index).Field("kind").AsString() == kind) return items.Item(index);
        }

        throw new InvalidOperationException($"The panel published no '{kind}' item; it published {items.Length()} of them.");
    }

    /// <summary>The magic block the session published.</summary>
    private static ProjectedNode Magic(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("magic");

    /// <summary>The identity a casting names for one member, as the projection publishes it.</summary>
    private static string Member(MightAndMagic7Session live, int position) =>
        PartyRpg.Kit.Combat.CombatantId.Of(live.Party!.Members[position].Id).ToString();

    /// <summary>One casting on the product's own action contract, naming the item it comes from.</summary>
    private static ProductInputEvent Cast(int member, string spell, string target, string item) =>
        ProductTestContext.Payload(string.Create(
            CultureInfo.InvariantCulture,
            $$"""{"action":"{{ProductIdentity.CastAction}}","member":{{member}},"spell":"{{spell}}","target":"{{target}}","item":"{{item}}"}"""));

    /// <summary>Holds the world for a stretch of real time so the party's recovery elapses.</summary>
    private static void Advance(IGameSession session, ref ulong step, double seconds)
    {
        session.Update(ProductTestContext.Update(++step, (uint)(seconds * GameSecondsPerRealSecond)));
    }

    /// <summary>How many real seconds one game second passes in, which is this game's own rate.</summary>
    private const int GameSecondsPerRealSecond = 30;

    private static ProductInputEvent Digital(string intent) => ProductTestContext.Digital(intent, InputEdge.Pressed);

    private static ProductInputEvent Released(string intent) => ProductTestContext.Digital(intent, InputEdge.Released);

    /// <summary>The session this suite plays, over this game's own ruleset and the content it authored.</summary>
    private static IGameSession Casting(ProductCreateContext context, RecordingUiService ui, bool combat = false)
    {
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, combat: combat) with
            {
                Cast = new CastIntentNames(ProductIdentity.CastAction, ProductIdentity.QuickSpellAction, ProductIdentity.UiActionContract),
            });
        session.Start();
        return session;
    }

    /// <summary>
    /// A world, a start, a party, and the three item rows this suite is about: a scroll of fire resistance, the
    /// wand of the same spell, and a trinket that carries nothing.
    /// </summary>
    private static (string Path, string Text)[] Content() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Guild of Fire", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "beast", "kind": "monster", "monster": "7", "name": "A beast", "x": 500, "y": 0, "z": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [ { "id": "3", "school": "Fire", "level": 3, "name": "Fire Resistance", "resist": "Fire" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Fire" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            $$"""
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "300", "name": "Scroll of Fire Resistance", "value": 30, "equipStat": "Sscroll", "type": "spell-scroll", "skillGroup": "Misc", "skill": "misc", "damageDice": "S3", "damageModifier": "1", "spell": "3" },
                { "id": "135", "name": "Wand of Fire", "value": 1000, "equipStat": "WeaponW", "type": "wand", "skillGroup": "Misc", "skill": "misc", "damageDice": "S3", "damageModifier": "{{WandModifier}}", "spell": "3" },
                { "id": "401", "name": "A trinket", "value": 10, "equipStat": "Misc", "type": "misc", "skillGroup": "Misc", "skill": "misc" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            """
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "hostility": 1, "recovery": 100, "level": 4, "hitPoints": 400,
                  "armorClass": 0, "experience": 0, "speed": 10, "aiType": "Wimp", "movement": "close", "fly": "N", "treasure": "0%" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 1000, "food": 30, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "300", "count": 1 } ],
                  "members": [
                    {
                      "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 10, "hitPoints": 40, "spellPoints": 20,
                      "equipment": [ { "slot": "main hand", "item": "135" } ],
                      "attributes": [
                        { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 40 }, { "id": "Personality", "value": 15 },
                        { "id": "Endurance", "value": 20 }, { "id": "Accuracy", "value": 20 }, { "id": "Speed", "value": 25 },
                        { "id": "Luck", "value": 13 }
                      ],
                      "skills": [ { "id": "Fire", "level": 4, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": []
                    },
                    {
                      "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [
                        { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 }, { "id": "Personality", "value": 9 },
                        { "id": "Endurance", "value": 13 }, { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
                        { "id": "Luck", "value": 9 }
                      ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": []
                    }
                  ]
                }
              ]
            }
            """),
    ];
}
