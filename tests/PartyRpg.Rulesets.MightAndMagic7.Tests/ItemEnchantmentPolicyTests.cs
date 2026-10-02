using System.Text.Json;
using System.Text.Json.Nodes;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Time;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>Actual item spells through the composed session, canonical items, current saved bytes and projection.</summary>
public sealed class ItemEnchantmentPolicyTests
{
    [Fact]
    public void Ordinary_enchant_is_visible_saved_and_quest_and_special_items_refuse_before_payment()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyEntity party = live.Party!;
        ItemInstance sword = Take(party, "7");
        Cast(session, ui, 2, "30", sword);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        ItemEnchantment property = Assert.IsType<ItemEnchantment>(sword.State.Enchantment);
        Assert.InRange(property.Strength, 3, 8);
        Assert.Null(property.DueElapsedMilliseconds);
        Assert.Equal(1, party.Records.CountOf("item:enchant-attempts"));
        Assert.Contains(property.Property, Magic(ui).Field("members").Item(0).Field("spells").Item(2).Field("aims").Item(0).Field("name").AsString());
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        SessionSave decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(saved, SessionSaveJson.TypeInfo), SessionSaveJson.TypeInfo)!;
        using PartyEntity restored = new PartyEntityFactory().Restore(decoded.Party);
        Assert.Equal(property, restored.FindItem(sword.Id)!.State.Enchantment);
        Assert.Equal(1, restored.Records.CountOf("item:enchant-attempts"));

        foreach ((string id, string code) in new[] { ("601", MightAndMagic7Codes.ItemMagicQuest), ("500", MightAndMagic7Codes.ItemMagicSpecial) })
        {
            ItemInstance item = Take(party, id);
            int before = party.Members[0].Resources.SpellPoints.Current;
            Cast(session, ui, id == "601" ? 3ul : 4ul, "30", item);
            Assert.Equal(code, Magic(ui).Field("code").AsString());
            Assert.Null(item.State.Enchantment);
            Assert.Equal(before, party.Members[0].Resources.SpellPoints.Current);
        }
    }

    [Fact]
    public void A_specific_artifact_keeps_identity_state_and_custody_through_current_save_json()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyEntity party = live.Party!;
        ItemInstance artifact = Take(party, "500");
        party.Members[1].Skills.Learn(new SkillId("Sword"), new SkillTier(1));
        Assert.True(party.Equip(party.Members[1].Id, MightAndMagic7Figure.MainHand, artifact.Id).Admitted);
        artifact.TakeDamage(2);
        artifact.Identify();
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        SessionSave decoded = JsonSerializer.Deserialize(JsonSerializer.Serialize(saved, SessionSaveJson.TypeInfo), SessionSaveJson.TypeInfo)!;
        using PartyEntity restored = new PartyEntityFactory().Restore(decoded.Party);
        ItemInstance held = restored.FindItem(artifact.Id)!;
        Assert.Equal(new ItemDefinitionId("500"), held.Definition);
        Assert.Equal(artifact.Id, held.Id);
        Assert.Equal(artifact.Custody, held.Custody);
        Assert.Equal(artifact.State, held.State);
    }

    [Fact]
    public void Fire_coating_expires_on_the_same_clock_and_grand_master_coating_is_permanent()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ItemInstance sword = Take(live.Party!, "7");
        Cast(session, ui, 2, "4", sword);
        Assert.Equal("fire", sword.State.Enchantment!.Value.Property);
        Assert.NotNull(sword.State.Enchantment.Value.DueElapsedMilliseconds);
        SpellEffectPolicyTests.Advance(session, 10);
        Assert.Null(sword.State.Enchantment);
        live.Party!.Members[0].Skills.SetTier(new SkillId("Fire"), new SkillTier(4));
        Cast(session, ui, 3, "4", sword);
        Assert.Null(sword.State.Enchantment!.Value.DueElapsedMilliseconds);
        SpellEffectPolicyTests.Advance(session, 24);
        Assert.Equal("fire", sword.State.Enchantment!.Value.Property);
    }

    [Fact]
    public void Failed_enchant_breaks_cheap_gear_but_hardening_spares_it_and_mastery_controls_strength()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ItemInstance cheap = Take(live.Party!, "1");
        Cast(session, ui, 2, "30", cheap);
        Assert.Equal(1, cheap.State.Damage);
        Assert.Null(cheap.State.Enchantment);
        cheap.Repair(1);
        cheap.Harden();
        Cast(session, ui, 3, "30", cheap);
        Assert.Equal(0, cheap.State.Damage);
        Assert.Null(cheap.State.Enchantment);
        ItemInstance valuable = Take(live.Party!, "7");
        live.Party!.Members[0].Skills.SetTier(new SkillId("Water"), new SkillTier(4));
        Cast(session, ui, 4, "30", valuable);
        Assert.InRange(valuable.State.Enchantment!.Value.Strength, 6, 12);
    }

    [Fact]
    public void Recharge_reduces_actual_capacity_and_a_full_wand_refuses_without_payment()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ItemInstance wand = Take(live.Party!, "200");
        Assert.True(live.Party!.SpendItemCharge(wand.Id, 10).Spent);
        Assert.True(live.Party.SpendItemCharge(wand.Id, 10).Spent);
        Assert.True(live.Party.SpendItemCharge(wand.Id, 10).Spent);
        Cast(session, ui, 2, "28", wand);
        Assert.Equal(8, wand.State.ChargeCapacity);
        Assert.Equal(0, wand.State.ChargesSpent);
        int before = live.Party.Members[0].Resources.SpellPoints.Current;
        Cast(session, ui, 3, "28", wand);
        Assert.Equal(MightAndMagic7Codes.ItemMagicCharged, Magic(ui).Field("code").AsString());
        Assert.Equal(before, live.Party.Members[0].Resources.SpellPoints.Current);
    }

    [Fact]
    public void A_saved_temporary_coating_resumes_its_original_clock_deadline()
    {
        InMemoryPersistenceService persistence = new();
        (var context, var ui) = Context(persistence);
        using IGameSession original = Casting(context, ui);
        PartyEntity party = ((MightAndMagic7Session)original).Party!;
        ItemInstance sword = Take(party, "7");
        Cast(original, ui, 2, "4", sword);
        long due = sword.State.Enchantment!.Value.DueElapsedMilliseconds!.Value;
        MightAndMagic7Ruleset.Instance.Save(original);
        (var resumeContext, var resumeUi) = Context(persistence);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumeContext, resumeUi) with
        { Start = SessionStart.Resume, Cast = new CastIntentNames(Declared.UiActionContract), Equip = new EquipIntentNames(Declared.UiActionContract) });
        resumed.Start();
        ItemInstance held = ((MightAndMagic7Session)resumed).Party!.FindItem(sword.Id)!;
        Assert.Equal(due, held.State.Enchantment!.Value.DueElapsedMilliseconds);
        SpellEffectPolicyTests.Advance(resumed, 9);
        Assert.NotNull(held.State.Enchantment);
        resumed.Update(RulesetTestContext.Update(200, 7_200)); // one further game hour at the same authored rate
        Assert.Null(held.State.Enchantment);
    }

    [Theory]
    [InlineData("fire", "Fire")]
    [InlineData("frost", "Water")]
    [InlineData("sparks", "Air")]
    [InlineData("poison", "Body")]
    public void A_weapon_property_contributes_actual_harm_through_the_composed_fight(string property, string damageKind)
    {
        (var context, var ui) = RulesetTestContext.Create(EquipmentPolicyTests.Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with
        { Equip = new EquipIntentNames(Declared.UiActionContract) });
        session.Start();
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        ItemInstance sword = live.Party!.Items.First(item => item.Definition.Value == "1");
        sword.SetEnchantment(new ItemEnchantment(property, 2));
        Assert.True(live.Party.Equip(live.Party.Members[0].Id, MightAndMagic7Figure.MainHand, sword.Id).Admitted);
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.AttackIntent)));
        ProjectedNode fight = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.True(fight.Field("resolved").AsBoolean());
        Assert.Contains($"6 {damageKind}", fight.Field("resolutionMessage").AsString());
        Assert.Equal(fight.Field("damageRolled").AsNumber() + 6, fight.Field("damage").AsNumber());
    }

    [Fact]
    public void Vampiric_property_heals_only_the_wielder_after_a_landed_hit()
    {
        (var context, var ui) = RulesetTestContext.Create(EquipmentPolicyTests.Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with
        { Equip = new EquipIntentNames(Declared.UiActionContract) });
        session.Start();
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember member = live.Party!.Members[0];
        ItemInstance sword = live.Party.Items.First(item => item.Definition.Value == "1");
        sword.SetEnchantment(new ItemEnchantment("vampiric", 3));
        Assert.True(live.Party.Equip(member.Id, MightAndMagic7Figure.MainHand, sword.Id).Admitted);
        member.Resources.TakeDamage(10);
        int before = member.Resources.HitPoints.Current;
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.AttackIntent)));
        ProjectedNode fight = ProjectedNode.Of(ui.Latest().Value).Field("combat");
        Assert.Equal(before + (int)fight.Field("damage").AsNumber() / 2, member.Resources.HitPoints.Current);
    }

    [Fact]
    public void Permanent_enchantment_artifact_and_attempt_count_resume_through_the_actual_store()
    {
        InMemoryPersistenceService persistence = new();
        (var context, var ui) = Context(persistence);
        using IGameSession original = Casting(context, ui);
        PartyEntity party = ((MightAndMagic7Session)original).Party!;
        ItemInstance sword = Take(party, "7");
        ItemInstance artifact = Take(party, "500");
        Cast(original, ui, 2, "30", sword);
        ItemEnchantment property = sword.State.Enchantment!.Value;
        MightAndMagic7Ruleset.Instance.Save(original);
        (var resumeContext, var resumeUi) = Context(persistence);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumeContext, resumeUi) with
        { Start = SessionStart.Resume, Cast = new CastIntentNames(Declared.UiActionContract), Equip = new EquipIntentNames(Declared.UiActionContract) });
        resumed.Start();
        PartyEntity again = ((MightAndMagic7Session)resumed).Party!;
        Assert.Equal(property, again.FindItem(sword.Id)!.State.Enchantment);
        Assert.Equal(artifact.Definition, again.FindItem(artifact.Id)!.Definition);
        Assert.Equal(artifact.Custody, again.FindItem(artifact.Id)!.Custody);
        Assert.Equal(1, again.Records.CountOf("item:enchant-attempts"));
        ItemInstance next = Take(again, "7");
        Assert.NotEqual(sword.Id, next.Id);
        Cast(resumed, resumeUi, 3, "30", next);
        Assert.NotNull(next.State.Enchantment);
        Assert.Equal(2, again.Records.CountOf("item:enchant-attempts"));
    }

    [Fact]
    public void Contradictory_special_item_properties_hardening_and_capacity_are_named_together_before_load()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        ItemInstance artifact = Take(((MightAndMagic7Session)session).Party!, "500");
        artifact.SetEnchantment(new ItemEnchantment("fire", 4));
        artifact.Harden();
        artifact.Recharge(5);
        SessionSave bad = MightAndMagic7Ruleset.Instance.Save(session);
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        SessionSaveException error = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(bad, catalog));
        Assert.Contains(error.Problems, problem => problem.Code == MightAndMagic7Codes.SaveItemProperty);
        Assert.Contains(error.Problems, problem => problem.Code == MightAndMagic7Codes.SaveItemHardening);
        Assert.Contains(error.Problems, problem => problem.Code == MightAndMagic7Codes.SaveItemCapacity);
    }

    [Theory]
    [InlineData(246, "fire")]
    [InlineData(247, "frost")]
    [InlineData(248, "poison")]
    [InlineData(249, "sparks")]
    [InlineData(250, "swift")]
    [InlineData(263, "dragon")]
    public void A_coating_potion_uses_the_same_item_aim_and_consumes_only_after_settlement(int potionId, string property)
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        PartyEntity party = ((MightAndMagic7Session)session).Party!;
        ItemInstance weapon = Take(party, "7");
        ItemInstance potion = Take(party, potionId.ToString());
        int points = party.Members[0].Resources.SpellPoints.Current;
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Payload(
            $$"""{"action":"party.cast","member":0,"spell":"{{MightAndMagic7Potions.EffectId(potionId)}}","item":"{{potion.Id}}","target":"{{weapon.Id}}"}""")));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(property, weapon.State.Enchantment!.Value.Property);
        Assert.NotNull(weapon.State.Enchantment.Value.DueElapsedMilliseconds);
        Assert.Null(party.FindItem(potion.Id));
        Assert.Equal(points, party.Members[0].Resources.SpellPoints.Current);
    }

    [Fact]
    public void Working_equipment_bonuses_change_the_existing_attribute_and_recovery_readings()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyEntity party = live.Party!;
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        var spells = MightAndMagic7Spells.Read(catalog)!;
        MightAndMagic7ItemMagic items = new(catalog, spells, null, null, () => party);
        MightAndMagic7Combat combat = MightAndMagic7Combat.Compose(catalog, null, party: () => party, itemMagic: () => items);
        PartyMember member = party.Members[1];
        ItemInstance ring = Take(party, "300");
        int bare = combat.ActualAttribute(member, MightAndMagic7Combat.MightAttribute);
        ring.SetEnchantment(new ItemEnchantment("Might", 9));
        Assert.True(party.Equip(member.Id, MightAndMagic7Figure.Rings[0], ring.Id).Admitted);
        Assert.Equal(bare + 9, combat.ActualAttribute(member, MightAndMagic7Combat.MightAttribute));
        ring.TakeDamage(1);
        Assert.Equal(bare, combat.ActualAttribute(member, MightAndMagic7Combat.MightAttribute));
        member.Skills.Learn(new SkillId("Sword"), new SkillTier(1));
        ItemInstance sword = Take(party, "7");
        Assert.True(party.Equip(member.Id, MightAndMagic7Figure.MainHand, sword.Id).Admitted);
        CombatState fight = new(Capabilities.Combat(combat), party, live.World);
        fight.Step();
        CombatSubject subject = fight.Combatants.First(actor => actor.Subject.Member == member).Subject;
        CombatSubject beast = fight.Combatants.First(actor => !actor.Subject.IsMember).Subject;
        ring.Repair(1);
        ring.SetEnchantment(null);
        int unprotected = combat.PlanOf(beast, subject, AttackKind.Melee).Chance.BasisPoints;
        ring.SetEnchantment(new ItemEnchantment("armour", 9));
        Assert.True(combat.PlanOf(beast, subject, AttackKind.Melee).Chance.BasisPoints < unprotected);
        GameDuration slow = combat.RecoveryAfter(subject, AttackKind.Melee);
        sword.SetEnchantment(new ItemEnchantment("swift", 1));
        Assert.True(combat.RecoveryAfter(subject, AttackKind.Melee).Milliseconds < slow.Milliseconds);
        sword.SetEnchantment(null);
        Assert.Equal(slow, combat.RecoveryAfter(subject, AttackKind.Melee));
    }

    [Fact]
    public void Hardening_and_recharge_potions_settle_through_actual_item_casting()
    {
        (var context, var ui) = Context();
        using IGameSession session = Casting(context, ui);
        PartyEntity party = ((MightAndMagic7Session)session).Party!;
        ItemInstance sword = Take(party, "1");
        ItemInstance hardener = Take(party, "236");
        Drink(2, 236, hardener, sword);
        Assert.True(sword.State.IsHardened);
        Assert.Null(party.FindItem(hardener.Id));
        ItemInstance wand = Take(party, "200");
        for (int charge = 0; charge < 8; charge++) Assert.True(party.SpendItemCharge(wand.Id, 10).Spent);
        ItemInstance recharge = Take(party, "233");
        Drink(3, 233, recharge, wand);
        Assert.Equal(3, wand.State.ChargeCapacity);
        Assert.Equal(0, wand.State.ChargesSpent);
        Assert.Null(party.FindItem(recharge.Id));

        void Drink(ulong step, int id, ItemInstance potion, ItemInstance target)
        {
            session.Update(RulesetTestContext.Update(step, 1, RulesetTestContext.Payload(
                $$"""{"action":"party.cast","member":0,"spell":"{{MightAndMagic7Potions.EffectId(id)}}","item":"{{potion.Id}}","target":"{{target.Id}}"}""")));
            Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        }
    }

    private static IGameSession Casting(Rusty.Engine.ProductCreateContext context, RecordingUiService ui)
    {
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui) with
        {
            Cast = new CastIntentNames(Declared.UiActionContract),
            Equip = new EquipIntentNames(Declared.UiActionContract),
        });
        session.Start();
        return session;
    }

    private static void Cast(IGameSession session, RecordingUiService ui, ulong step, string spell, ItemInstance item) =>
        session.Update(RulesetTestContext.Update(step, 4_000, RulesetTestContext.Payload(
            $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"{{item.Id}}"}""")));
    private static ProjectedNode Magic(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("magic");
    private static ItemInstance Take(PartyEntity party, string definition)
    {
        ItemInstance item = party.CreateItem(new ItemDefinitionId(definition));
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }
    private static (Rusty.Engine.ProductCreateContext, RecordingUiService) Context(InMemoryPersistenceService? persistence = null)
    {
        string root = $"{RulesetTestContext.ContentDirectory}/content-packs/world/";
        var files = SpellEffectPolicyTests.Content(spells: (root + "spells.json", """
            {"documentId":"spells","definitionKind":"spell","entries":[
              {"id":"2","name":"Fire Bolt","school":"Fire"},
              {"id":"4","name":"Fire Aura","school":"Fire"},
              {"id":"28","name":"Recharge Item","school":"Water"},
              {"id":"30","name":"Enchant Item","school":"Water"},
              {"id":"91","name":"Vampiric Weapon","school":"Dark"}]}
            """), extra: [(root + "items.json", """
            {"documentId":"items","definitionKind":"item","entries":[
              {"id":"1","name":"Crude Longsword","value":50,"type":"single-handed","skill":"sword","damageDice":"3d3","damageModifier":"0"},
              {"id":"7","name":"Fine Longsword","value":1000,"type":"single-handed","skill":"sword","damageDice":"3d3","damageModifier":"2"},
              {"id":"200","name":"Test wand","value":1000,"type":"wand","spell":"2","damageModifier":"6"},
              {"id":"500","name":"Puck","material":"Artifact","value":20000,"type":"single-handed","skill":"sword","damageDice":"3d3","damageModifier":"14"},
              {"id":"300","name":"Test Ring","value":1000,"type":"ring"},
              {"id":"233","name":"Recharge Potion","type":"potion"},
              {"id":"236","name":"Hardening Potion","type":"potion"},
              {"id":"246","name":"Flaming Potion","type":"potion"},
              {"id":"247","name":"Freezing Potion","type":"potion"},
              {"id":"248","name":"Noxious Potion","type":"potion"},
              {"id":"249","name":"Shocking Potion","type":"potion"},
              {"id":"250","name":"Swift Potion","type":"potion"},
              {"id":"263","name":"Slaying Potion","type":"potion"},
              {"id":"601","name":"Lich Jar","value":0,"type":"misc"}]}
            """, """{"path":"items.json","documentId":"items","definitionKind":"item"}""")]);
        files = files.Select(file =>
        {
            if (file.Path.EndsWith("skills.json", StringComparison.Ordinal))
            {
                JsonNode skills = JsonNode.Parse(file.Text)!;
                skills["entries"]!.AsArray().Add(new JsonObject { ["id"] = "Sword" });
                return (file.Path, skills.ToJsonString());
            }
            if (!file.Path.EndsWith("party.json", StringComparison.Ordinal)) return file;
            JsonNode document = JsonNode.Parse(file.Text)!;
            JsonObject member = document["entries"]![0]!["members"]![0]!.AsObject();
            member["skills"]!.AsArray().Add(new JsonObject { ["id"] = "Dark", ["level"] = 10, ["tier"] = 3, ["pointsSpent"] = 1 });
            member["spells"] = new JsonArray("2", "4", "28", "30", "91");
            foreach (JsonNode? skill in member["skills"]!.AsArray())
                if (skill!["id"]!.GetValue<string>() is "Fire" or "Water" or "Dark") { skill["level"] = 10; skill["tier"] = 3; }
            return (file.Path, document.ToJsonString());
        }).ToArray();
        return RulesetTestContext.Create(persistence ?? new InMemoryPersistenceService(), files);
    }
}
