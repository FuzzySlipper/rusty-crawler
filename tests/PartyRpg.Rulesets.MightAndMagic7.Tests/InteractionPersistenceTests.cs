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
        world.Died(new CreatureDeath(world.Place, beast.Placement, "A beast"));
        Assert.True(world.Interactions.IsDefeated(new("1"), beast.Content));
        MightAndMagic7Ruleset.Instance.Save(session);
        var (resumedContext, resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1));
        var again = ((MightAndMagic7Session)resumed).World!;
        Assert.Empty(again.Population.Entities);
        Assert.True(Assert.Single(MightAndMagic7Fixtures.ActorsOf(again, new("1"))!).Down);
        again.Party.Enter(new("2"), PlacePose.Origin);
        again.Populate();
        again.Party.Enter(new("1"), PlacePose.Origin);
        Assert.Empty(again.Populate());
        ((IInteractionWorld)again).Clock!.Advance(GameDuration.FromHours(24));
        Assert.False(again.Interactions.IsDefeated(new("1"), beast.Content));
        Assert.Single(again.Populate());
    }

    [Fact]
    public void Two_used_targets_resume_in_their_played_states_and_cannot_yield_a_second_use()
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
        var content = SpellEffectPolicyTests.Content(places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", places));
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
        Assert.Equal("searched", live.World.Interactions.StateOf(new("1"), new("container", "chest")).State);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Equal(2, Assert.Single(saved.World.Interaction.Places).Targets.Count);

        var (resumedContext, resumedUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumedContext, resumedUi) with { Start = SessionStart.Resume, Use = controls });
        resumed.Start();
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var again = (MightAndMagic7Session)resumed;
        Assert.Equal("container-emptied", again.World!.LastInteraction!.Refusal!.Code);
        Assert.Equal(live.World.Interactions.StateOf(new("1"), new("container", "chest")), again.World.Interactions.StateOf(new("1"), new("container", "chest")));
        again.World.Party.Move(0, -1000, 0);
        resumed.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.Equal(MightAndMagic7Codes.DoorAlreadyOpen, again.World.LastInteraction!.Refusal!.Code);
        Assert.Equal(live.World.Interactions.StateOf(new("1"), new("door", "gate")), again.World.Interactions.StateOf(new("1"), new("door", "gate")));
    }
}
