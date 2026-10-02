using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class InteractionHeightTests
{
    [Theory]
    [InlineData(88, 59, true)]
    [InlineData(88, 195, true)]
    [InlineData(88, -195, true)]
    [InlineData(0, 512, true)]
    [InlineData(0, -512, true)]
    [InlineData(88, 513, false)]
    public void The_composed_ruleset_uses_reachable_elevated_containers_and_names_reach_refusals(int ground, int height, bool usable)
    {
        string places = $$"""
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "region", "name": "Yard", "respawnDays": 1,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [ { "id": "chest", "kind": "container", "x": {{ground}}, "y": 0, "z": {{height}} } ] },
              { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
            ] }
            """;
        var content = SpellEffectPolicyTests.Content(
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", places));
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = new UseIntentNames(Declared.UseIntent, Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var world = ((MightAndMagic7Session)session).World!;
        Assert.Equal(usable, world.LastInteraction!.IsApplied);
        if (usable) Assert.Equal("searched", world.Interactions.StateOf(new("1"), new("container", "chest")).State);
        else Assert.Equal(InteractionCodes.InteractionOutOfReach, world.LastInteraction.Code);
    }
}
