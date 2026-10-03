using System.Text.Json.Nodes;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class SpecialItemPowerTests
{
    [Theory]
    [InlineData("500", "Speed", 40)]
    [InlineData("501", "Might", 40)]
    [InlineData("525", "Speed", 50)]
    [InlineData("525", "Luck", 50)]
    public void Ordinary_equipment_adds_the_fixed_property_to_the_actual_reader_and_resumes(string id, string attribute, int gain)
    {
        InMemoryPersistenceService persistence = new();
        var (context, ui) = Context(persistence);
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        var member = live.Party!.Members[0];
        var policy = (MightAndMagic7Combat)live.Owners.Rules.Combat!.Rule;
        ItemInstance item = Take(live.Party, id);
        int before = policy.ActualAttribute(member, new(attribute));
        Wear(session, item);
        Assert.Equal(before + gain, policy.ActualAttribute(member, new(attribute)));
        Assert.Contains(attribute, Equipment(ui).Field("members").Item(0).Field("powers").AsString());
        MightAndMagic7Ruleset.Instance.Save(session);
        var (againContext, againUi) = Context(persistence);
        using IGameSession resumed = Session(againContext, againUi, true);
        var again = (MightAndMagic7Session)resumed;
        Assert.Equal(item.Id, again.Party!.FindItem(item.Id)!.Id);
        Assert.Equal(item.Definition, again.Party.FindItem(item.Id)!.Definition);
        Assert.Equal(item.Custody, again.Party.FindItem(item.Id)!.Custody);
        var resumedPolicy = (MightAndMagic7Combat)again.Owners.Rules.Combat!.Rule;
        Assert.Equal(before + gain, resumedPolicy.ActualAttribute(again.Party.Members[0], new(attribute)));
        item.TakeDamage(1);
        Assert.Equal(before, policy.ActualAttribute(member, new(attribute)));
    }

    [Fact]
    public void Fixed_resistance_and_relic_penalty_reach_the_canonical_damage_reader_and_depart_immediately()
    {
        var (context, ui) = Context();
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        var rule = (MightAndMagic7Combat)live.Owners.Rules.Combat!.Rule;
        ItemInstance splitter = Take(live.Party!, "506"), twilight = Take(live.Party!, "525");
        Wear(session, splitter);
        var (member, beast) = Subjects(live);
        Assert.Equal(50, rule.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points);
        Wear(session, twilight, 3);
        Assert.Equal(35, rule.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points);
        Assert.True(live.Party!.Unequip(live.Party.Members[0].Id, MightAndMagic7Figure.Cloak).Admitted);
        Assert.Equal(50, rule.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points);
        splitter.TakeDamage(1);
        Assert.Equal(0, rule.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points);
    }

    [Fact]
    public void Elfbane_halves_hostile_missiles_through_the_actual_plan_and_not_melee_or_party_orders()
    {
        var (context, ui) = Context();
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        var rule = (MightAndMagic7Combat)live.Owners.Rules.Combat!.Rule;
        ItemInstance item = Take(live.Party!, "531");
        Wear(session, item);
        var (member, beast) = Subjects(live);
        Assert.Equal(2, rule.PlanOf(beast, member, AttackKind.Ranged).Divisor);
        Assert.Equal(1, rule.PlanOf(beast, member, AttackKind.Melee).Divisor);
        Assert.Equal(1, rule.PlanOf(member, beast, AttackKind.Ranged).Divisor);
        item.TakeDamage(1);
        Assert.Equal(1, rule.PlanOf(beast, member, AttackKind.Ranged).Divisor);
    }

    [Fact]
    public void An_ordinary_lamp_use_consumes_the_instance_and_carries_the_permanent_gift_through_save()
    {
        InMemoryPersistenceService persistence = new();
        var (context, ui) = Context(persistence, "Body");
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        ItemInstance lamp = Take(live.Party!, "616");
        var policy = (MightAndMagic7Combat)live.Owners.Rules.Combat!.Rule;
        var (member, beast) = Subjects(live);
        int resisted = policy.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points;
        Use(session, lamp, 2);
        Assert.Null(live.Party!.FindItem(lamp.Id));
        Assert.True(live.Owners.ItemUses!.Last!.Applied);
        ResistanceScore gift = Assert.Single(live.Party.Members[0].Resistances.Scores);
        Assert.Equal(MightAndMagic7Damage.Body, gift.Kind);
        Assert.Equal(resisted + 1, policy.PlanOf(beast, member, AttackKind.Ranged).Resistance.Points);
        Assert.Equal(1, gift.Points); // first calendar week
        Assert.Contains(gift.Kind.Value, Equipment(ui).Field("members").Item(0).Field("powers").AsString());
        Assert.Equal("used", Equipment(ui).Field("useOutcome").Field("outcome").AsString());
        Use(session, lamp, 3);
        Assert.False(live.Owners.ItemUses.Last!.Applied);
        Assert.Equal(gift, Assert.Single(live.Party.Members[0].Resistances.Scores));
        MightAndMagic7Ruleset.Instance.Save(session);
        var (againContext, againUi) = Context(persistence, "Body");
        using IGameSession resumed = Session(againContext, againUi, true);
        var again = (MightAndMagic7Session)resumed;
        Assert.Null(again.Party!.FindItem(lamp.Id));
        Assert.Equal(gift, Assert.Single(again.Party.Members[0].Resistances.Scores));
        Assert.Equal(live.Owners.Rules.ItemUses!.Describe(live.Party.Members[0]), again.Owners.Rules.ItemUses!.Describe(again.Party.Members[0]));
    }

    [Fact]
    public void A_laid_out_member_cannot_consume_the_lamp_and_unknown_special_powers_are_named()
    {
        var (context, ui) = Context();
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        ItemInstance lamp = Take(live.Party!, "616");
        live.Party!.Members[0].Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Dead));
        Use(session, lamp, 2);
        Assert.False(live.Owners.ItemUses!.Last!.Applied);
        Assert.Same(lamp, live.Party.FindItem(lamp.Id));
        Assert.Empty(live.Party.Members[0].Resistances.Scores);
        ItemInstance unknown = Take(live.Party, "504");
        Assert.Contains("not compiled", live.Owners.Rules.ItemUses!.Describe(unknown));
    }

    private static ProjectedNode Equipment(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("equipment");
    private static void Wear(IGameSession session, ItemInstance item, ulong step = 2) => session.Update(RulesetTestContext.Update(step, 1,
        RulesetTestContext.Payload($$"""{"action":"party.equip","member":0,"item":"{{item.Id}}"}""")));
    private static void Use(IGameSession session, ItemInstance item, ulong step) => session.Update(RulesetTestContext.Update(step, 1,
        RulesetTestContext.Payload($$"""{"action":"party.item.use","member":0,"item":"{{item.Id}}"}""")));
    private static ItemInstance Take(PartyEntity party, string id)
    {
        var item = party.CreateItem(new(id)); Assert.True(party.AcquireItem(item).Admitted); return item;
    }
    private static (CombatSubject, CombatSubject) Subjects(MightAndMagic7Session live) => (
        live.Combat!.Combatants.First(combatant => combatant.Subject.IsMember).Subject,
        live.Combat.Combatants.First(combatant => !combatant.Subject.IsMember).Subject);
    private static IGameSession Session(ProductCreateContext context, RecordingUiService ui, bool resume = false)
    {
        var composition = RulesetTestContext.RulesetContext(context, ui, combat: true) with { Equip = new(Declared.UiActionContract) };
        var session = resume ? MightAndMagic7Ruleset.Instance.ResumeSession(composition) : MightAndMagic7Ruleset.Instance.CreateSession(composition);
        session.Start(); session.Update(RulesetTestContext.Update(1, 1)); return session;
    }
    private static (ProductCreateContext, RecordingUiService) Context(InMemoryPersistenceService? persistence = null, string attackKind = "Fire")
    {
        var content = EquipmentPolicyTests.Content().Select(file =>
        {
            var json = JsonNode.Parse(file.Text)!;
            if (file.Path.EndsWith("/pack.json", StringComparison.Ordinal))
                json["documents"]!.AsArray().Add(JsonNode.Parse("""{"path":"spells.json","documentId":"spells","definitionKind":"spell"}"""));
            if (file.Path.EndsWith("/items.json", StringComparison.Ordinal))
            {
                foreach (var entry in JsonNode.Parse("""
                    [{"id":"500","name":"Puck","type":"single-handed","skill":"sword","material":"Artifact","value":20000,"damageDice":"3d3","damageModifier":"14"},
                     {"id":"501","name":"Iron Feather","type":"two-handed","skill":"sword","material":"Artifact","value":20000,"damageDice":"4d5","damageModifier":"10"},
                     {"id":"506","name":"Splitter","type":"single-handed","skill":"axe","material":"Artifact","value":20000,"damageDice":"4d2","damageModifier":"11"},
                     {"id":"525","name":"Twilight","type":"cloak","skill":"misc","material":"Relic","value":30000,"damageDice":"1","damageModifier":"12"},
                     {"id":"531","name":"Elfbane","type":"two-handed","skill":"sword","material":"Artifact","value":15000,"damageDice":"4d6","damageModifier":"12"},
                     {"id":"504","name":"Governor's Armor","type":"armour","skill":"plate","material":"Artifact","value":30000,"damageDice":"1","damageModifier":"10"},
                     {"id":"616","name":"Genie Lamp","type":"misc","skill":"misc","material":"5","value":2000}]
                    """)!.AsArray()) json["entries"]!.AsArray().Add(entry!.DeepClone());
            }
            if (file.Path.EndsWith("/skills.json", StringComparison.Ordinal)) json["entries"]!.AsArray().Add(JsonNode.Parse("""{"id":"Axe"}"""));
            if (file.Path.EndsWith("/party.json", StringComparison.Ordinal))
                json["entries"]![0]!["members"]![0]!["skills"]!.AsArray().Add(JsonNode.Parse("""{"id":"Axe","level":1,"tier":1,"pointsSpent":1}"""));
            if (file.Path.EndsWith("/monsters.json", StringComparison.Ordinal)) json["entries"]![0]!["attack"]!["kind"] = attackKind;
            return (Path: file.Path, Text: json.ToJsonString());
        }).ToList();
        content.Add(($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json", """{"documentId":"spells","definitionKind":"spell","entries":[{"id":"2","name":"Fire Bolt","school":"Fire"}]}"""));
        return RulesetTestContext.Create(persistence ?? new(), content.ToArray());
    }
}
