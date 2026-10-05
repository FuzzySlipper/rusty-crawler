using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class ConversationDefeatTests
{
    [Fact]
    public void A_moved_person_is_talked_to_at_the_saved_live_position_not_the_abandoned_spawn()
    {
        var content = PeopleContent();
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        UseIntentNames use = new(Declared.UseIntent, Declared.UiActionContract);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = use });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        var live = (MightAndMagic7Session)session;
        var world = live.World!;
        var person = Assert.Single(world.Population.Entities, entity => entity.Content.Id == "fallen");
        PlacePose moved = new(100, 2000, 0, 0, 0);
        person.MoveTo(moved);
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.False(live.Party!.Records.Has("met:first"));
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        MightAndMagic7Ruleset.Instance.Save(session);

        var (resumeContext, resumeUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumeContext, resumeUi) with { Start = SessionStart.Resume, Use = use });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.False(again.Party!.Records.Has("met:first"));
        Assert.Equal(moved, Assert.Single(again.World!.Population.Entities, entity => entity.Content.Id == "fallen").Pose);

        again.World.Party.Move(0, 2000, 0);
        resumed.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal("fallen", again.World.Interaction!.FocusedTarget?.Content.Id);
        resumed.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(ProjectedNode.Of(resumeUi.Latest().Value).Field("conversation").Field("open").AsBoolean());
        Assert.True(again.Party.Records.Has("met:first"));
        Assert.False(again.Party.Records.Has("met:second"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_defeated_person_cannot_greet_until_the_place_restores_them(bool recordDeathBeforeUse)
    {
        var content = PeopleContent();
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        UseIntentNames use = new(Declared.UseIntent, Declared.UiActionContract);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = use });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        var live = (MightAndMagic7Session)session;
        var world = live.World!;
        var fallen = Assert.Single(world.Population.Entities, entity => entity.Content.Id == "fallen");
        CreatureHealth health = CreatureHealth.Find(fallen.Actor)!;
        Assert.True(health.Wound(health.Maximum));
        if (recordDeathBeforeUse) world.Died(new(world.Place, fallen.Placement, "First neighbour"));

        // The first frame of death and its durable record must both keep Use from greeting the body.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("conversation").Field("open").AsBoolean());
        Assert.False(live.Party!.Records.Has("met:first"));
        world.Died(new(world.Place, fallen.Placement, "First neighbour"));
        MightAndMagic7Ruleset.Instance.Save(session);

        var (resumeContext, resumeUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(
            RulesetTestContext.RulesetContext(resumeContext, resumeUi) with { Start = SessionStart.Resume, Use = use });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var again = (MightAndMagic7Session)resumed;
        Assert.False(ProjectedNode.Of(resumeUi.Latest().Value).Field("conversation").Field("open").AsBoolean());
        Assert.False(again.Party!.Records.Has("met:first"));

        // A revisit omits the defeated entity, so absence must not lose the durable death reading.
        again.World!.Party.Enter(new("2"), PlacePose.Origin);
        again.World.Populate();
        again.World.Party.Enter(new("1"), PlacePose.Origin);
        again.World.Populate();
        Assert.DoesNotContain(again.World.Population.Entities, entity => entity.Content.Id == "fallen");
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.False(again.Party.Records.Has("met:first"));

        // Another placement of the same creature row is still a living, distinct speaker.
        again.World.Party.Move(0, 1000, 0);
        resumed.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(again.Party.Records.Has("met:second"));

        ((IInteractionWorld)world).Clock!.Advance(GameDuration.FromHours(24));
        world.Populate();
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(live.Party.Records.Has("met:first"));
    }
    private static (string Path, string Text)[] PeopleContent() =>
        SpellEffectPolicyTests.Content(
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", """
                { "documentId": "places", "definitionKind": "place", "entries": [
                  { "id": "1", "kind": "region", "name": "Yard", "respawnDays": 1,
                    "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                    "placements": [
                      { "id": "fallen", "kind": "person", "monster": "7", "people": ["first"], "x": 100, "y": 0, "z": 0 },
                      { "id": "standing", "kind": "person", "monster": "7", "people": ["second"], "x": 100, "y": 1000, "z": 0 }
                    ] },
                  { "id": "2", "kind": "interior", "name": "Cave",
                    "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
                ] }
                """),
            extra: [($"{RulesetTestContext.ContentDirectory}/content-packs/world/people.json", """
                { "documentId": "people", "definitionKind": "person", "entries": [
                  { "id": "first", "name": "First neighbour", "greeting": "Hello." },
                  { "id": "second", "name": "Second neighbour", "greeting": "Good day." }
                ] }
                """, """{ "path": "people.json", "documentId": "people", "definitionKind": "person" }""")]);

}
