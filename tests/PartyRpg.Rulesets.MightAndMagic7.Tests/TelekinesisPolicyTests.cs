using System.Text.Json.Nodes;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class TelekinesisPolicyTests
{
    [Theory]
    [InlineData("door", "open")]
    [InlineData("container", "searched")]
    public void The_projected_distant_aim_casts_through_use_and_current_save(string kind, string state)
    {
        InMemoryPersistenceService persistence = new();
        var content = Content(kind);
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        string aim = Aim(ui);
        Assert.NotEmpty(aim);
        Assert.Equal(InteractionCodes.InteractionOutOfReach, live.World!.Interact(true)!.Code);
        int points = live.Party!.Members[0].Resources.SpellPoints.Current;
        SpellEffectPolicyTests.Cast(session, ui, 2, "42", aim);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(points - 20, live.Party.Members[0].Resources.SpellPoints.Current);
        Assert.True(live.World.LastInteraction!.IsApplied);
        Assert.Equal(state, live.World.Interactions.StateOf(new("1"), new(kind, "target")).State);
        Assert.Equal(InteractionCodes.InteractionOutOfReach, live.World.Interact(true)!.Code);

        MightAndMagic7Ruleset.Instance.Save(session);
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = Session(againContext, againUi, true);
        Assert.Equal(state, ((MightAndMagic7Session)resumed).World!.Interactions.StateOf(new("1"), new(kind, "target")).State);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("pose")]
    [InlineData("place")]
    [InlineData("reach")]
    public void A_stale_or_unavailable_aim_cannot_spend_or_use(string change)
    {
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), Content("door"));
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        string aim = Aim(ui);
        Assert.NotEmpty(aim);
        if (change == "revision") live.World!.Interactions.Record(new("1"), new("door", "target"), "closed");
        if (change == "pose") live.World!.ArriveAt(new("1"), new(0, 0, 0, 1024, 0));
        if (change == "place") live.World!.ArriveAt(new("2"), PlacePose.Origin);
        if (change == "reach") live.World!.ArriveAt(new("1"), new(-4000, 0, 0, 0, 0));
        int points = live.Party!.Members[0].Resources.SpellPoints.Current;
        SpellEffectPolicyTests.Cast(session, ui, 2, "42", aim);
        Assert.Equal("spell-world-target-unavailable", Magic(ui).Field("code").AsString());
        Assert.Equal(points, live.Party.Members[0].Resources.SpellPoints.Current);
        Assert.NotEqual("open", live.World!.Interactions.StateOf(new("1"), new("door", "target")).State);
        Assert.Empty(live.Knowledge!.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Distant_use_retains_canonical_requirement_or_trap_behavior(bool trapped)
    {
        var content = Content(trapped ? "container" : "door");
        content = content.Select(file =>
        {
            if (!file.Path.EndsWith("/places.json", StringComparison.Ordinal)) return file;
            var json = JsonNode.Parse(file.Text)!;
            var target = json["entries"]![0]!["placements"]![0]!;
            if (trapped)
            {
                target["flags"] = 1; target["trapDifficulty"] = 10; target["trapDamageDice"] = 1;
            }
            else target["requires"] = JsonNode.Parse("""[{"kind":"flag","id":"gate-key","label":"the gate key"}]""");
            return (file.Path, json.ToJsonString());
        }).ToArray();
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), content);
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        string aim = Aim(ui);
        int health = live.Party!.Members.Sum(member => member.Resources.HitPoints.Current);
        SpellEffectPolicyTests.Cast(session, ui, 2, "42", aim);
        if (trapped)
        {
            Assert.True(live.Party.Members.Sum(member => member.Resources.HitPoints.Current) < health);
            Assert.NotEqual("searched", live.World!.LastInteraction!.State);
        }
        else
        {
            Assert.Equal(InteractionCodes.InteractionRequirementUnmet, live.World!.LastInteraction!.Code);
            Assert.Contains("the gate key", live.World.LastInteraction.Message);
            Assert.Empty(live.World.Interactions.StateOf(new("1"), new("door", "target")).State);
            Assert.Equal(health, live.Party.Members.Sum(member => member.Resources.HitPoints.Current));
        }
    }

    private static ProjectedNode Magic(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("magic");
    private static string Aim(RecordingUiService ui)
    {
        var spells = Magic(ui).Field("members").Item(0).Field("spells");
        for (int i = 0; i < spells.Length(); i++)
            if (spells.Item(i).Field("spell").AsString() == "42")
            {
                var aims = spells.Item(i).Field("aims");
                return aims.Length() == 0 ? "" : aims.Item(0).Field("aim").AsString();
            }
        return "";
    }
    private static IGameSession Session(ProductCreateContext context, RecordingUiService ui, bool resume = false)
    {
        var composition = RulesetTestContext.RulesetContext(context, ui) with
        {
            Cast = new(Declared.UiActionContract),
            Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract),
        };
        var session = resume ? MightAndMagic7Ruleset.Instance.ResumeSession(composition) : MightAndMagic7Ruleset.Instance.CreateSession(composition);
        session.Start();
        return session;
    }
    private static (string Path, string Text)[] Content(string kind)
    {
        string places = $$"""
            {"documentId":"places","definitionKind":"place","entries":[
              {"id":"1","kind":"interior","name":"Hall","respawnDays":1,
               "entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}],
               "placements":[{"id":"target","kind":"{{kind}}","doorId":6,"state":2,"x":2000,"y":0,"z":0}]},
              {"id":"2","kind":"interior","name":"Cave","respawnDays":1,
               "entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}]}]}
            """;
        return SpellEffectPolicyTests.Content(places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", places));
    }
}
