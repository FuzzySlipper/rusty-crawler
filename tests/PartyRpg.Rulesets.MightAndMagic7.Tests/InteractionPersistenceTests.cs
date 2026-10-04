using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class InteractionPersistenceTests
{
    [Fact]
    public void A_content_creatures_death_survives_a_resume_and_a_visit_until_the_clock_restores_its_place()
    {
        var content = SpellEffectPolicyTests.Content();
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        var world = ((MightAndMagic7Session)session).World!;
        var beast = Assert.Single(world.Population.Entities);
        CreatureHealth.Find(beast.Actor)!.Wound(CreatureHealth.Find(beast.Actor)!.Maximum);
        world.Died(new CreatureDeath(world.Place, beast.Placement, "A beast"));
        Assert.True(world.Interactions.IsDefeated(new("1"), beast.Content));
        MightAndMagic7Ruleset.Instance.Save(session);
        var (resumedContext, resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1));
        var again = ((MightAndMagic7Session)resumed).World!;
        Assert.True(CreatureHealth.Find(Assert.Single(again.Population.Entities).Actor)!.IsDown);
        Assert.True(Assert.Single(MightAndMagic7Fixtures.ActorsOf(again, new("1"))!).Down);
        again.Party.Enter(new("2"), PlacePose.Origin);
        again.Populate();
        again.Party.Enter(new("1"), PlacePose.Origin);
        Assert.Empty(again.Populate());
        ((IInteractionWorld)again).Clock!.Advance(GameDuration.FromHours(24));
        Assert.False(again.Interactions.IsDefeated(new("1"), beast.Content));
        Assert.Single(again.Populate());
    }

    [Theory]
    [InlineData("container")]
    [InlineData("sprite")]
    public void Two_used_targets_resume_in_their_played_states_and_cannot_yield_a_second_use(string kind)
    {
        const string places = """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "region", "name": "Yard", "respawnDays": 1,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [
                  { "id": "gate", "kind": "door", "x": 100, "y": 0, "z": 0, "state": 2 },
                  { "id": "chest", "kind": "container", "x": 100, "y": 1000, "z": 0 }
                ] },
              { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
            ] }
            """;
        var content = SpellEffectPolicyTests.Content(
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", kind == "container" ? places : places.Replace("\"kind\": \"container\"", "\"kind\": \"sprite\", \"containingItem\": 7", StringComparison.Ordinal)),
            extra: [($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json", """{ "documentId": "items", "definitionKind": "item", "entries": [ { "id": "7", "name": "A token", "value": 1 } ] }""", """{ "path": "items.json", "documentId": "items", "definitionKind": "item" }""")]);
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        UseIntentNames controls = new(Declared.UseIntent, Declared.UiActionContract);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui) with { Use = controls });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var live = (MightAndMagic7Session)session;
        Assert.Equal("open", live.World!.Interactions.StateOf(new("1"), new("door", "gate")).State);
        live.World.Party.Move(0, 1000, 0);
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal("searched", live.World.Interactions.StateOf(new("1"), new(kind, "chest")).State);
        if (kind == "sprite")
        {
            Assert.Contains("The party takes 1 × A token.", live.World.Interaction!.LastResult!.Message);
            Assert.Equal(1, live.Party!.Inventory.TotalOf(new("7")));
        }
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Equal(2, Assert.Single(saved.World.Interaction.Places).Targets.Count);

        var (resumedContext, resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume, Use = controls });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var again = (MightAndMagic7Session)resumed;
        Assert.Equal("container-emptied", again.World!.LastInteraction!.Refusal!.Code);
        Assert.Equal(live.World.Interactions.StateOf(new("1"), new(kind, "chest")), again.World.Interactions.StateOf(new("1"), new(kind, "chest")));
        again.World.Party.Move(0, -1000, 0);
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal(MightAndMagic7Codes.DoorAlreadyOpen, again.World.LastInteraction!.Refusal!.Code);
        Assert.Equal(live.World.Interactions.StateOf(new("1"), new("door", "gate")), again.World.Interactions.StateOf(new("1"), new("door", "gate")));
    }
}
