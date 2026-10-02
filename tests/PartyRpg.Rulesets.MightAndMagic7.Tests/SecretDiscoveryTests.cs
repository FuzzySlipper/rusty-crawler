using System.Text.Json.Nodes;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class SecretDiscoveryTests
{
    // The actual importer contract for Manor door 0; the imported case below reads it from the operator's pack.
    private const string ManorDoor = """
        {"id":"door-0","kind":"door","sourceField":"doors","sourceIndex":0,"x":1026,"y":-476,"z":92,
         "positionSource":"vertexIds","doorId":6,"state":2,"attributes":1,"moveLength":8,"openSpeed":50,
         "closeSpeed":50,"secret":true,"perceptionDifficulty":10,"secretFaces":[914]}
        """;

    [Theory]
    [InlineData(1, 1, false, false)]
    [InlineData(10, 2, false, true)]
    [InlineData(7, 3, false, true)]
    [InlineData(1, 4, false, true)]
    [InlineData(10, 2, true, false)]
    public void A_real_skill_read_discovers_without_opening_or_changes_nothing_on_failure(int level, int tier, bool unconscious, bool found) =>
        Check(ManorDoor, level, tier, unconscious, found);

    [ImportedFact("places.json")]
    public void The_actual_imported_secret_door_reaches_discovery_and_ordinary_use()
    {
        ContentEntry manor = ImportedContent.Load().RequireValid().Entries("place").Single(row => row.Entry.Id == "17").Entry;
        var door = manor.GetArray("placements").Single(p => ContentEntry.ReadString(p, "id") == "door-0");
        Assert.True(new ContentEntry("door", door).GetBoolean("secret"));
        Assert.Equal(10, new ContentEntry("door", door).GetInt32("perceptionDifficulty"));
        Check(door.GetRawText(), 1, 1, false, false);
        Check(door.GetRawText(), 10, 2, false, true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_forged_discovery_cannot_unlock_a_target_when_a_save_resumes(bool unknown)
    {
        InMemoryPersistenceService persistence = new();
        var content = Content(ManorDoor, 10, 2, false);
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = Session(context, ui);
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        MightAndMagic7Ruleset.Instance.Save(session);
        var bytes = JsonNode.Parse(persistence.Payload("sessions", "session")!)!;
        var discovery = bytes["world"]!["interaction"]!["places"]![0]!["values"]![0]!;
        if (unknown) discovery["key"] = "secret-discovered:door:missing";
        else discovery["value"] = 2;
        persistence.Seed("sessions", "session", System.Text.Encoding.UTF8.GetBytes(bytes.ToJsonString()));
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        var refused = Assert.Throws<SessionSaveException>(() => Session(againContext, againUi, resume: true));
        Assert.Contains(refused.Problems, problem => problem.Text.Contains(unknown ? "no secret surface" : "must carry 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Discovering_a_secret_fixture_does_not_run_its_door_event_until_the_next_use_even_after_resume()
    {
        const string fixture = """
            {"id":"fixture-6","kind":"fixture","sourceField":"events","eventId":6,"x":1026,"y":-476,"z":92,
             "secret":true,"perceptionDifficulty":10,"secretFaces":[914]}
            """;
        var content = Content(fixture, 10, 2, false).ToList();
        int manifest = content.FindIndex(file => file.Path.EndsWith("/pack.json", StringComparison.Ordinal));
        var pack = JsonNode.Parse(content[manifest].Text)!;
        pack["documents"]!.AsArray().Add(new JsonObject { ["path"] = "events.json", ["documentId"] = "events", ["definitionKind"] = "place-event" });
        content[manifest] = (content[manifest].Path, pack.ToJsonString());
        content.Add(($"{RulesetTestContext.ContentDirectory}/content-packs/world/events.json", """
            {"documentId":"events","definitionKind":"place-event","entries":[
              {"id":"1.6","place":1,"event":6,"label":"Secret lever","fixture":true,"steps":[
                {"step":0,"op":"change-door-state","door":6,"action":"open"},{"step":1,"op":"exit"}]}]}
            """));
        int places = content.FindIndex(file => file.Path.EndsWith("/places.json", StringComparison.Ordinal));
        var world = JsonNode.Parse(content[places].Text)!;
        world["entries"]![0]!["placements"]!.AsArray().Add(JsonNode.Parse(ManorDoor.Replace("1026", "-900")));
        content[places] = (content[places].Path, world.ToJsonString());
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content.ToArray());
        using IGameSession session = Session(context, ui);
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var live = (MightAndMagic7Session)session;
        Assert.Equal("discovered", live.World!.Interactions.StateOf(new("1"), new("fixture", "fixture-6")).State);
        Assert.Empty(live.World.Interactions.StateOf(new("1"), new("door", "door-0")).State);
        MightAndMagic7Ruleset.Instance.Save(session);
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content.ToArray());
        using IGameSession resumed = Session(againContext, againUi, resume: true);
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var again = (MightAndMagic7Session)resumed;
        Assert.True(again.World!.LastInteraction!.IsApplied);
        Assert.Equal("open", again.World.Interactions.StateOf(new("1"), new("door", "door-0")).State);
        Assert.Single(again.Knowledge!.Notes);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("\"ten\"")]
    public void A_secret_target_cannot_silently_lose_its_threshold(string difficulty)
    {
        var content = Content(ManorDoor.Replace("\"perceptionDifficulty\":10", $"\"perceptionDifficulty\":{difficulty}"), 1, 1, false);
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), content);
        var refused = Assert.Throws<ContentValidationException>(() => MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui)));
        Assert.Contains(refused.Issues, issue => issue.Code == "secret-surface-invalid");
    }

    private static void Check(string door, int level, int tier, bool unconscious, bool found)
    {
        InMemoryPersistenceService persistence = new();
        var content = Content(door, level, tier, unconscious);
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = Session(context, ui);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var world = live.World!;
        Assert.Equal(found, world.LastInteraction!.IsApplied);
        var place = new PartyRpg.Kit.World.PlaceId("1");
        var target = new PartyRpg.Kit.World.PlacementContentId("door", "door-0");
        const string key = "secret-discovered:door:door-0";
        if (!found)
        {
            Assert.Equal(MightAndMagic7Codes.SecretNotDiscovered, world.LastInteraction.Code);
            Assert.Contains("Perception 20", world.LastInteraction.Message);
            Assert.Empty(world.Interactions.ValuesOf(place));
            Assert.Empty(world.Interactions.StateOf(place, target).State);
            Assert.Empty(live.Knowledge!.Notes);
            return;
        }
        Assert.Contains("discovered a secret surface", world.LastInteraction.Message);
        Assert.Equal("closed", world.Interactions.StateOf(place, target).State);
        Assert.Equal(1, world.Interactions.ValuesOf(place)[key]);
        Assert.Single(live.Knowledge!.Notes);
        MightAndMagic7Ruleset.Instance.Save(session);
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = Session(againContext, againUi, resume: true);
        var again = (MightAndMagic7Session)resumed;
        Assert.Equal(1, again.World!.Interactions.ValuesOf(place)[key]);
        Assert.Single(again.Knowledge!.Notes);
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(again.World.LastInteraction!.IsApplied);
        Assert.Equal("open", again.World.Interactions.StateOf(place, target).State);
        Assert.Single(again.Knowledge.Notes);
    }

    private static IGameSession Session(ProductCreateContext context, RecordingUiService ui, bool resume = false)
    {
        var composition = RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) };
        var session = resume ? MightAndMagic7Ruleset.Instance.ResumeSession(composition) : MightAndMagic7Ruleset.Instance.CreateSession(composition);
        session.Start();
        return session;
    }

    private static (string Path, string Text)[] Content(string door, int level, int tier, bool unconscious)
    {
        string places = $$"""
            {"documentId":"places","definitionKind":"place","entries":[
              {"id":"1","kind":"interior","name":"Hall","respawnDays":1,
               "entryPoints":[{"id":"Party Start","x":926,"y":-476,"z":0,"yaw":0}],"placements":[{{door}}]},
              {"id":"2","kind":"interior","name":"Cave","respawnDays":1,
               "entryPoints":[{"id":"Party Start","x":0,"y":0,"z":0,"yaw":0}]}]}
            """;
        var content = SpellEffectPolicyTests.Content(places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", places));
        return content.Select(file =>
        {
            if (file.Path.EndsWith("/skills.json", StringComparison.Ordinal))
            {
                var json = JsonNode.Parse(file.Text)!;
                json["entries"]!.AsArray().Add(new JsonObject { ["id"] = "Perception" });
                return (file.Path, json.ToJsonString());
            }
            if (file.Path.EndsWith("/party.json", StringComparison.Ordinal))
            {
                var json = JsonNode.Parse(file.Text)!;
                var members = json["entries"]![0]!["members"]!.AsArray();
                var first = members[0]!.DeepClone();
                members.Clear(); members.Add(first);
                first["skills"]!.AsArray().Add(new JsonObject { ["id"] = "Perception", ["level"] = level, ["tier"] = tier });
                if (unconscious) first["conditions"]!.AsArray().Add(new JsonObject { ["id"] = "Unconscious", ["severity"] = 1 });
                return (file.Path, json.ToJsonString());
            }
            return file;
        }).ToArray();
    }
}
