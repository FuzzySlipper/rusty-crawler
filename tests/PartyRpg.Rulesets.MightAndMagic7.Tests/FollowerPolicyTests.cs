using System.Text.Json.Nodes;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class FollowerPolicyTests
{
    [Fact]
    public void Ordinary_conversation_hires_two_story_event_joins_outside_the_limit_and_found_gold_pays_them()
    {
        using Mission mission = new();
        PartyEntity party = mission.Live.Party!;
        mission.UseAt(0);
        mission.Choose("follower-hire");
        Assert.Equal(4900, party.Purse.Coins);
        mission.Turn("npc-2");
        mission.Choose("follower-hire");
        Assert.Equal(4700, party.Purse.Coins);
        mission.Turn("npc-3");
        Assert.False(Assert.Single(mission.Live.Owners.Conversations!.Offers, offer => offer.Id == "follower-hire").IsOnOffer);
        mission.Choose("follower-hire");
        Assert.Equal(2, party.Followers.HiredCount);
        Assert.Equal(4700, party.Purse.Coins);

        mission.UseAt(1000);
        Assert.Equal(FollowerKind.Story, party.Followers.Find(new("npc-4"))!.Kind);
        Assert.Equal(2, party.Followers.HiredCount);
        Assert.Equal(3, party.Followers.All.Count);
        Assert.Empty(party.Items);
        Assert.Equal(30, party.Food.Portions);
        var rows = mission.Live.Inspect().Party.Followers;
        Assert.Equal(["Guide", "Banker", "Witness"], rows.Select(row => row.Name));
        Assert.Equal(["701", "702", "704"], rows.Select(row => row.Portrait));
        Assert.All(rows, row => Assert.True(row.CanTalk));

        mission.UseAt(2000);
        // Finding bonuses are sequential +10%, +20%; salary is the actual profession prices / 10000.
        Assert.Equal(4700 + 1320 - 39, party.Purse.Coins);
        Assert.Contains("companions take 39", mission.Live.World!.LastInteraction!.Message, StringComparison.OrdinalIgnoreCase);
        mission.Live.Owners.Accounts!.Credit(PartyCost.OfGold(100));
        Assert.Equal(6081, party.Purse.Coins); // A sale/refund credit has no finding bonus or salary.

        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(mission.Session);
        Assert.Equal(party.Followers.All, saved.Party.Followers);
        var (context, ui) = RulesetTestContext.Create(mission.Persistence, Content());
        using var resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(context, ui) with { Start = SessionStart.Resume });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        Assert.Equal(party.Followers.All, again.Party!.Followers.All);
        Assert.Equal(6081, again.Party.Purse.Coins);
        Assert.Equal(rows, again.Inspect().Party.Followers);
    }

    [Fact]
    public void A_travelling_companion_can_be_addressed_elsewhere_and_departure_removes_the_conversation_anchor()
    {
        using Mission mission = new();
        mission.UseAt(0);
        mission.Choose("follower-hire");
        mission.Live.World!.Party.Enter(new("2"), PlacePose.Origin);
        mission.Live.World.Populate();
        mission.Action("conversation.follower", "npc-1");
        PartyConversations conversation = mission.Live.Owners.Conversations!;
        Assert.True(conversation.IsOpen);
        Assert.Null(conversation.Placement);
        Assert.Equal("Guide", conversation.Speaker!.Name);
        Assert.Equal("follower-dismiss", Assert.Single(conversation.OnOffer).Id);
        mission.Choose("follower-dismiss");
        Assert.Empty(mission.Live.Party!.Followers.All);
        Assert.False(conversation.IsOpen);
        Assert.Contains("Guide leaves", conversation.Last!.Message, StringComparison.Ordinal);
        Assert.Equal(4900, mission.Live.Party.Purse.Coins);
        mission.Action("conversation.follower", "npc-1");
        Assert.Equal("conversation-follower-absent", conversation.Last!.Refusal!.Code);
    }

    [Fact]
    public void A_refused_fixture_applies_no_companion_join_and_load_names_unknown_and_duplicate_people()
    {
        using Mission mission = new();
        mission.UseAt(3000);
        Assert.NotNull(mission.Live.World!.LastInteraction!.Refusal);
        Assert.Empty(mission.Live.Party!.Followers.All);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(mission.Session);
        PartyFollower unknown = new(new("missing"), FollowerKind.Story);
        var corrupt = new SessionSave(saved.Party with { Followers = [unknown, unknown] }, saved.Clock, saved.World,
            saved.Quests, saved.Journal, saved.Knowledge, saved.Maps, saved.Combat);
        var (context, ui) = RulesetTestContext.Create(Content());
        var catalog = RulesetTestContext.RulesetContext(context, ui).Content;
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(corrupt, catalog));
        Assert.Contains(refused.Problems, problem => problem.Code == SaveCodes.SaveFollowerTwice);
        Assert.Contains(refused.Problems, problem => problem.Code == "save-follower-unknown");
    }

    [Fact]
    public void Sacrifice_refuses_story_before_payment_then_removes_actual_hired_aim_and_fills_pools_without_curing()
    {
        using Mission mission = new([27, 32]);
        PartyEntity party = mission.Live.Party!;
        mission.UseAt(1000);
        int before = party.Members[0].Resources.SpellPoints.Current;
        mission.Action("party.cast", "npc-4", "96");
        Assert.Equal("spell-follower-story", mission.Live.Owners.Casting!.Last!.Refusal!.Code);
        Assert.Equal(before, party.Members[0].Resources.SpellPoints.Current);
        Assert.Equal(0, party.Reputation.Reputation);
        mission.UseAt(0);
        mission.Choose("follower-hire");
        party.Members[1].Resources.TakeDamage(20);
        party.Members[1].Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.PoisonWeak));
        long experience = party.Members[0].Progression.Experience;
        var spell = mission.Live.Owners.Rules.Magic!.Spells!.Catalog.Read(new("96"));
        var aims = ((ISpellAimRule)mission.Live.Owners.Rules.Magic.Effects!).AimsOf(spell);
        Assert.Equal("npc-1", Assert.Single(aims).Aim);
        var combat = (MightAndMagic7Combat)mission.Live.Owners.Rules.Combat!.Rule;
        int beforeLuck = combat.ActualAttribute(party.Members[0], new("Luck"));
        mission.Action("party.cast", "npc-1", "96");
        Assert.Null(mission.Live.Owners.Casting!.Last!.Refusal);
        Assert.Equal(beforeLuck - 5, combat.ActualAttribute(party.Members[0], new("Luck")));
        Assert.Equal(["npc-4"], party.Followers.All.Select(follower => follower.Definition.Value));
        Assert.All(party.Members, member =>
        {
            Assert.Equal(member.Resources.HitPoints.Maximum, member.Resources.HitPoints.Current);
            Assert.Equal(member.Resources.SpellPoints.Maximum, member.Resources.SpellPoints.Current);
        });
        Assert.True(party.Members[1].Conditions.Has(MightAndMagic7Conditions.PoisonWeak));
        Assert.Equal(-15, party.Reputation.Reputation);
        Assert.Equal(experience, party.Members[0].Progression.Experience);
        Assert.Empty(((ISpellAimRule)mission.Live.Owners.Rules.Magic.Effects!).AimsOf(spell));
    }

    [Theory]
    [InlineData(27, 5)]
    [InlineData(28, 20)]
    [InlineData(47, 10)]
    public void Actual_luck_and_damage_resistance_read_joined_professions_and_restore_only_identity(int profession, int bonus)
    {
        using Mission mission = new([profession, 37]);
        PartyEntity party = mission.Live.Party!;
        PartyMember member = party.Members[0];
        var combat = (MightAndMagic7Combat)mission.Live.Owners.Rules.Combat!.Rule;
        var luck = new AttributeId("Luck");
        int before = combat.ActualAttribute(member, luck);
        mission.UseAt(0);
        var from = mission.Live.Combat!.Combatants.First(actor => !actor.Subject.IsMember && actor.Subject.Entity!.Placement.Content.Kind == "monster").Subject;
        var target = mission.Live.Combat.Combatants.First(actor => actor.Subject.IsMember).Subject;
        int resistanceBefore = combat.PlanOf(from, target, AttackKind.Ranged).Resistance.Points;
        mission.Choose("follower-hire");
        Assert.Equal(before + bonus, combat.ActualAttribute(member, luck));
        mission.Turn("npc-2"); mission.Choose("follower-hire");
        Assert.Equal(resistanceBefore + 20, combat.PlanOf(from, target, AttackKind.Ranged).Resistance.Points);
        Assert.Contains($"Luck +{bonus}", mission.Live.Inspect().Party.Followers[0].Benefits);
        Assert.Contains("resistance +20", mission.Live.Inspect().Party.Followers[1].Benefits);
        MightAndMagic7Ruleset.Instance.Save(mission.Session);
        var (context, ui) = RulesetTestContext.Create(mission.Persistence, Content([profession, 37]));
        using var resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with { Start = SessionStart.Resume });
        resumed.Start(); resumed.Update(RulesetTestContext.Update(1, 1)); var again = (MightAndMagic7Session)resumed;
        var restored = (MightAndMagic7Combat)again.Owners.Rules.Combat!.Rule;
        Assert.Equal(before + bonus, restored.ActualAttribute(again.Party!.Members[0], luck));
        Assert.Equal(party.Followers.All, again.Party.Followers.All);
        var restoredTarget = again.Combat!.Combatants.First(actor => actor.Subject.IsMember).Subject;
        Assert.Equal(resistanceBefore + 20, restored.PlanOf(from, restoredTarget, AttackKind.Ranged).Resistance.Points);
        mission.Choose("follower-dismiss");
        Assert.Equal(resistanceBefore, combat.PlanOf(from, target, AttackKind.Ranged).Resistance.Points);
        mission.Action("conversation.follower", "npc-1"); mission.Choose("follower-dismiss");
        Assert.Equal(before, combat.ActualAttribute(member, luck));
    }

    [Fact]
    public void Joined_tutors_change_actual_experience_awards_without_changing_purchased_skills()
    {
        using Mission mission = new([13, 14]);
        var party = mission.Live.Party!;
        mission.UseAt(0); mission.Choose("follower-hire");
        mission.Turn("npc-2"); mission.Choose("follower-hire");
        var before = party.Members.Select(member => member.Progression.Experience).ToArray();
        var skills = party.Members.Select(member => member.Skills.Entries.ToArray()).ToArray();
        mission.Live.Owners.Progression!.Award(new("lesson", 400));
        for (int i = 0; i < party.Members.Count; i++)
        {
            Assert.Equal(before[i] + (400 / party.Members.Count * 125 / 100), party.Members[i].Progression.Experience);
            Assert.Equal(skills[i], party.Members[i].Skills.Entries);
        }
        mission.Choose("follower-dismiss");
        mission.Live.Owners.Progression.Award(new("after dismissal", 400));
        Assert.Equal(before[0] + (400 / party.Members.Count * 235 / 100), party.Members[0].Progression.Experience);
    }

    [Fact]
    public void Merchant_and_perception_readers_count_a_profession_once_and_remove_departed_people()
    {
        using Mission mission = new([20, 20]);
        var party = mission.Live.Party!;
        party.Members[0].Skills.Learn(new("Merchant"), new(1));
        int before = MightAndMagic7Services.MerchantValue(party);
        var shop = new ServiceDefinition(new("test-counter"), new("Tavern"), "A room", [ServiceOperationKind.Stay], [], []);
        var request = new ServiceQuoteRequest(shop, ServiceOperationKind.Stay,
            ServiceSubject.OfOffer(new ServiceOffer(ServiceOfferKind.Stay, "A room", Value: 100)),
            party.Members[0].Id, party, mission.Live.Owners.Clock!);
        int quoteBefore = mission.Live.Owners.Rules.Service!.Quote(request).Charge.Coins;
        mission.UseAt(0); mission.Choose("follower-hire");
        mission.Turn("npc-2"); mission.Choose("follower-hire");
        var followers = ((MightAndMagic7Conversation)mission.Live.Owners.Rules.Conversation!).Followers;
        Assert.Equal(before + 4, MightAndMagic7Services.MerchantValue(party, followers));
        Assert.Equal(quoteBefore - 4, mission.Live.Owners.Rules.Service!.Quote(request).Charge.Coins);
        mission.Choose("follower-dismiss");
        Assert.Equal(before + 4, MightAndMagic7Services.MerchantValue(party, followers));
        mission.Action("conversation.follower", "npc-1"); mission.Choose("follower-dismiss");
        Assert.Equal(before, MightAndMagic7Services.MerchantValue(party, followers));
        Assert.Equal(quoteBefore, mission.Live.Owners.Rules.Service!.Quote(request).Charge.Coins);
        Assert.Equal(0, followers.SkillBonus("Perception"));
    }

    [Fact]
    public void Scout_and_locksmith_change_the_actual_container_guard_and_depart_before_another_guard()
    {
        using Mission mission = new([22, 26]);
        mission.UseAt(0); mission.Choose("follower-hire");
        mission.Turn("npc-2"); mission.Choose("follower-hire");
        mission.UseAt(4000);
        Assert.Equal(MightAndMagic7Containers.TrappedState, mission.Live.World!.LastInteraction!.State);
        mission.UseAt(4000);
        Assert.Equal(MightAndMagic7Containers.DisarmedState, mission.Live.World.LastInteraction!.State);
        mission.Action("conversation.follower", "npc-1"); mission.Choose("follower-dismiss");
        mission.Action("conversation.follower", "npc-2"); mission.Choose("follower-dismiss");
        mission.UseAt(5000);
        Assert.Equal(MightAndMagic7Containers.SprungState, mission.Live.World.LastInteraction!.State);
    }

    private sealed class Mission : IDisposable
    {
        private ulong _step;
        internal readonly InMemoryPersistenceService Persistence = new();
        internal readonly IGameSession Session;
        internal MightAndMagic7Session Live => (MightAndMagic7Session)Session;
        internal Mission(int[]? professions = null)
        {
            var (context, ui) = RulesetTestContext.Create(Persistence, Content(professions));
            Session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with
            {
                Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
                Conversation = new ConversationIntentNames(Declared.ConversationLeaveIntent, Declared.UiActionContract),
                Cast = new CastIntentNames(Declared.UiActionContract),
            });
            Session.Start();
        }
        internal void UseAt(int y)
        {
            if (Live.Owners.Conversations?.IsOpen == true) Action("conversation.leave", "");
            Live.World!.Party.Enter(new("1"), new PlacePose(0, y, 0, 0, 0));
            Session.Update(RulesetTestContext.Update(++_step, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        }
        internal void Choose(string topic) => Action("conversation.topic", topic);
        internal void Turn(string person) => Action("conversation.person", person);
        internal void Action(string action, string target, string spell = "")
        {
            if (action == "party.cast" && Live.Owners.Conversations?.IsOpen == true) Action("conversation.leave", "");
            Session.Update(RulesetTestContext.Update(++_step, 1,
                RulesetTestContext.Payload($$"""{"action":"{{action}}","target":"{{target}}","member":0,"spell":"{{spell}}"}""")));
        }
        public void Dispose() => Session.Dispose();
    }

    private static (string Path, string Text)[] Content(int[]? professions = null)
    {
        const string root = RulesetTestContext.ContentDirectory + "/content-packs/world/";
        var files = SpellEffectPolicyTests.Content(places: (root + "places.json", """
            {"documentId":"places","definitionKind":"place","entries":[
              {"id":"1","kind":"region","name":"Yard","respawnDays":1,"entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}],"placements":[
                {"id":"probe","kind":"monster","x":9000,"y":0,"z":0,"monster":"7"},
                {"id":"people","kind":"person","x":100,"y":0,"z":0,"people":["npc-1","npc-2","npc-3"]},
                {"id":"story","kind":"fixture","x":100,"y":1000,"z":0,"eventId":1},
                {"id":"reward","kind":"fixture","x":100,"y":2000,"z":0,"eventId":2},
                {"id":"refused","kind":"fixture","x":100,"y":3000,"z":0,"eventId":3},
                {"id":"guard1","kind":"container","x":100,"y":4000,"z":0,"flags":1,"trapDifficulty":2,"trapDamageDice":0,"contents":[]},
                {"id":"guard2","kind":"container","x":100,"y":5000,"z":0,"flags":1,"trapDifficulty":2,"trapDamageDice":0,"contents":[]}]},
              {"id":"2","kind":"interior","name":"Away","respawnDays":1,"entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}]}]}
            """), extra: [
            (root + "people.json", """
                {"documentId":"people","definitionKind":"person","entries":[
                  {"id":"npc-1","npcId":1,"name":"Guide","portrait":"701","profession":31,"hirePrice":100,"canJoin":true,"joinText":"I know the road.","dismissText":"Farewell."},
                  {"id":"npc-2","npcId":2,"name":"Banker","portrait":"702","profession":32,"hirePrice":200,"canJoin":true},
                  {"id":"npc-3","npcId":3,"name":"Third","portrait":"703","profession":1,"hirePrice":100,"canJoin":true},
                  {"id":"npc-4","npcId":4,"name":"Witness","portrait":"704","profession":0,"hirePrice":0,"canJoin":false}]}
                """, """{"path":"people.json","documentId":"people","definitionKind":"person"}"""),
            (root + "events.json", """
                {"documentId":"events","definitionKind":"place-event","entries":[
                  {"id":"1.1","place":"1","event":1,"label":"Witness joins","raised":true,"steps":[
                    {"step":0,"op":"add","variable":"hireling","value":4},
                    {"step":1,"op":"compare","variable":"hireling","value":4,"target":3},
                    {"step":2,"op":"unknown-instruction"},{"step":3,"op":"exit"}]},
                  {"id":"1.2","place":"1","event":2,"label":"Reward","raised":true,"steps":[
                    {"step":0,"op":"add","variable":"gold","value":1000},{"step":1,"op":"exit"}]},
                  {"id":"1.3","place":"1","event":3,"label":"Refused","raised":true,"steps":[
                    {"step":0,"op":"add","variable":"hireling","value":3},{"step":1,"op":"unknown-instruction"}]}]}
                """, """{"path":"events.json","documentId":"events","definitionKind":"place-event"}""")]);
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Path == root + "spells.json")
            {
                JsonNode spells = JsonNode.Parse(files[i].Text)!;
                spells["entries"]!.AsArray().Add(JsonNode.Parse("""{"id":"96","school":"Dark","level":8,"name":"Sacrifice","resist":"0"}"""));
                files[i] = (files[i].Path, spells.ToJsonString());
            }
            if (files[i].Path == root + "party.json")
            {
                JsonNode party = JsonNode.Parse(files[i].Text)!;
                JsonNode caster = party["entries"]![0]!["members"]![0]!;
                caster["class"] = "Lich";
                caster["skills"]!.AsArray().Add(JsonNode.Parse("""{"id":"Dark","level":4,"tier":3,"pointsSpent":1}"""));
                caster["spells"]!.AsArray().Add("96");
                files[i] = (files[i].Path, party.ToJsonString());
            }
        }
        if (professions is not null)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (!files[i].Path.EndsWith("people.json", StringComparison.Ordinal)) continue;
                JsonNode people = JsonNode.Parse(files[i].Text)!;
                for (int j = 0; j < professions.Length; j++) people["entries"]![j]!["profession"] = professions[j];
                files[i] = (files[i].Path, people.ToJsonString());
            }
        }
        return files;
    }
}
