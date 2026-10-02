using PartyRpg.Kit;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's own answers to the kit's interaction seam: what the imported content offers the party, what a
/// requirement means here, and what a granted use makes of a door or a fixture.
/// </summary>
/// <remarks>
/// The ruleset's policy types are internal because nothing outside the product composes them, so this suite
/// reaches them through the ruleset's own friend declaration. What it proves is what no kit test can: the
/// readings of the imported data this game states — a door's delta state, a decoration's event, and a
/// requirement's meaning against the party and the clock the ruleset composes.
/// </remarks>
public sealed class InteractionPolicyTests
{
    private static readonly PlaceId Hall = new("7");

    [Fact]
    public void A_door_offers_the_use_its_delta_state_calls_for()
    {
        MightAndMagic7Interaction rule = new();

        // Imported doorways are open at rest and close at full travel; no animation is inferred.
        InteractionTargetDefinition closed = Describe(rule, Door(state: 2));
        Assert.Equal("door", closed.Kind.Value);
        Assert.Equal("closed", closed.State);
        InteractionOutcome opened = rule.Apply(closed, Context(closed, Door(state: 2)));
        Assert.True(opened.IsApplied);
        Assert.Equal("open", opened.State);
        Assert.Empty(opened.Residue);
        InteractionTargetDefinition moved = Describe(rule, Door(state: 0));
        Assert.Equal("open", moved.State);
        Assert.Equal("door-already-open", rule.Apply(moved, Context(moved, Door(state: 0))).Refusal!.Code);
        InteractionTargetDefinition recorded = Describe(rule, Door(state: 2), recorded: "open");
        Assert.Equal("open", recorded.State);
    }

    [Fact]
    public void A_decoration_that_raises_an_event_is_a_fixture_whose_use_names_the_event_when_none_is_loaded()
    {
        MightAndMagic7Interaction rule = new();

        // The original reaches decorations that raise an event and ignores the rest, so only those are
        // targets here; a fixture's use runs its event, and a rule composed over no content carries no event
        // to run, so the use is refused with the event and the place named rather than doing nothing.
        InteractionTargetDefinition fixture = Describe(rule, Decoration(name: "dec32", eventId: 150))!;
        Assert.Equal("fixture", fixture.Kind.Value);
        Assert.Equal("A fixture (dec32)", fixture.Name);
        Assert.Equal(InteractionVerb.Pull, fixture.Verb);

        InteractionOutcome refused = rule.Apply(fixture, Context(fixture, Decoration(name: "dec32", eventId: 150)));
        Assert.False(refused.IsApplied);
        Assert.Equal(MightAndMagic7Codes.FixtureEventMissing, refused.Refusal!.Code);
        Assert.Contains("150", refused.Refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'7'", refused.Refusal.Message, StringComparison.Ordinal);

        Assert.Null(rule.Describe(new InteractionTargetRequest(Hall, Decoration(name: "torch01", eventId: 0), string.Empty)));
    }

    [Fact]
    public void A_spawn_point_and_a_light_are_not_things_anybody_uses()
    {
        MightAndMagic7Interaction rule = new();

        Assert.Null(rule.Describe(new InteractionTargetRequest(Hall, Spawn(), string.Empty)));
        Assert.Null(rule.Describe(new InteractionTargetRequest(Hall, Light(), string.Empty)));
    }

    [Fact]
    public void What_using_something_requires_is_content_and_each_kind_of_requirement_is_answered_here()
    {
        MightAndMagic7Interaction rule = new();
        using PartyEntity party = Party(coins: 0);
        GameClock clock = MightAndMagic7Time.Compose();

        // A door whose placement states a key offers the use that turns the lock, and the party that carries
        // no such item is told exactly that.
        InteractionTargetDefinition locked = Describe(rule, DoorWithKey(), recorded: string.Empty);
        Assert.Equal(InteractionVerb.Unlock, locked.Verb);
        Assert.Equal("closed", locked.State);
        Assert.Single(locked.Requires);

        InteractionContext context = new(Hall, DoorWithKey(), locked, party, clock);
        Verdict missing = rule.Judge(locked.Requires[0], context);
        Assert.False(missing.IsMet);
        Assert.Contains("the Barrow Key", missing.Explanation, StringComparison.Ordinal);

        // With the item in the party's own shared pack the same requirement is met, and the door offers the
        // lock-turning use until what it required has been turned.
        party.AcquireItem(new ItemDefinitionId("655"), 1);
        Assert.True(rule.Judge(locked.Requires[0], context).IsMet);
        InteractionOutcome turned = rule.Apply(locked, context);
        Assert.True(turned.IsApplied);
        Assert.Equal("unlocked", turned.State);
        Assert.Equal(InteractionVerb.Open, Describe(rule, DoorWithKey(), recorded: "unlocked").Verb);

        // A skill requirement is answered by the best member, a part of the day by the clock this ruleset
        // composes, and a flag by the fact that nothing records flags yet — each with the sentence a refusal
        // shows, which is what makes a lock say what it needs.
        InteractionRequirement skill = new(InteractionRequirementKind.Skill, "perception", 4, "Perception");
        Assert.False(rule.Judge(skill, context).IsMet);
        Assert.Contains("Perception 4", rule.Judge(skill, context).Explanation, StringComparison.Ordinal);

        Assert.True(rule.Judge(new InteractionRequirement(InteractionRequirementKind.TimeOfDay, "day"), context).IsMet);
        Assert.False(rule.Judge(new InteractionRequirement(InteractionRequirementKind.TimeOfDay, "night"), context).IsMet);

        Verdict flag = rule.Judge(new InteractionRequirement(InteractionRequirementKind.Flag, "cellar-opened"), context);
        Assert.False(flag.IsMet);
        Assert.Contains("cellar-opened", flag.Explanation, StringComparison.Ordinal);

        // A session with no clock cannot answer a time of day either, and says so rather than assuming one.
        Verdict noClock = rule.Judge(
            new InteractionRequirement(InteractionRequirementKind.TimeOfDay, "day"),
            context with { Clock = null });
        Assert.False(noClock.IsMet);
        Assert.NotEmpty(noClock.Explanation);
    }

    [Fact]
    public void Two_doors_of_one_place_under_one_number_stop_the_world_being_built()
    {
        // A map event moves a door by its number, so two doors sharing one would leave a lever moving only the
        // first: the place is refused by name with the number, and doors numbered apart load.
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Interaction.Validate(Catalog(
                """
                { "id": "7", "kind": "interior", "name": "Hall", "respawnDays": 7,
                  "placements": [
                    { "id": "door-0", "kind": "door", "doorId": 3, "x": 0, "y": 0, "z": 0, "state": 2 },
                    { "id": "door-1", "kind": "door", "doorId": 3, "x": 10, "y": 0, "z": 0, "state": 2 } ] }
                """)));

        ContentValidationIssue issue = Assert.Single(error.Issues);
        Assert.Equal("interaction-door-number-reused", issue.Code);
        Assert.Contains("place '7'", issue.Message, StringComparison.Ordinal);
        Assert.Contains("numbered 3", issue.Message, StringComparison.Ordinal);

        MightAndMagic7Interaction.Validate(Catalog(
            """
            { "id": "7", "kind": "interior", "name": "Hall", "respawnDays": 7,
              "placements": [
                { "id": "door-0", "kind": "door", "doorId": 3, "x": 0, "y": 0, "z": 0, "state": 2 },
                { "id": "door-1", "kind": "door", "doorId": 4, "x": 10, "y": 0, "z": 0, "state": 2 } ] }
            """));
    }

    [Fact]
    public void A_requirement_this_game_has_no_kind_for_stops_the_world_being_built()
    {
        // Requirements are judged inside an admitted update, where a content defect cannot be reported
        // without stopping the session, so a pack that states one this game cannot read is refused while the
        // world is built, with the place named.
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Interaction.Validate(Catalog(
                """
                { "id": "7", "kind": "interior", "name": "Hall", "respawnDays": 7,
                  "placements": [
                    { "id": "door-0", "kind": "door", "x": 0, "y": 0, "z": 0, "state": 2,
                      "requires": [ { "kind": "incantation", "id": "open sesame" } ] } ] }
                """)));

        Assert.Contains(error.Issues, issue => issue.Code == "interaction-requirement-kind-unknown");

        // What this game does read passes the same check.
        MightAndMagic7Interaction.Validate(Catalog(
            """
            { "id": "7", "kind": "interior", "name": "Hall", "respawnDays": 7,
              "placements": [
                { "id": "door-0", "kind": "door", "x": 0, "y": 0, "z": 0, "state": 2,
                  "requires": [ { "kind": "item", "id": "655", "label": "the Barrow Key" } ] } ] }
            """));
    }

    private static InteractionTargetDefinition Describe(MightAndMagic7Interaction rule, PlacementDefinition placement, string recorded = "") =>
        rule.Describe(new InteractionTargetRequest(Hall, placement, recorded))!;

    private static InteractionContext Context(InteractionTargetDefinition target, PlacementDefinition placement) =>
        new(Hall, placement, target, Party(), MightAndMagic7Time.Compose());

    /// <summary>A door placement as the importer writes one, with the delta state a case states.</summary>
    private static PlacementDefinition Door(int state) => Placement(
        "door",
        "door-0",
        $$"""{ "id": "door-0", "kind": "door", "sourceField": "doors", "sourceIndex": 0, "x": 100, "y": 0, "z": 0, "positionSource": "vertexIds", "doorId": 1, "state": {{state}}, "attributes": 1 }""");

    /// <summary>A door whose placement states what using it requires.</summary>
    private static PlacementDefinition DoorWithKey() => Placement(
        "door",
        "door-0",
        """
        { "id": "door-0", "kind": "door", "sourceField": "doors", "sourceIndex": 0, "x": 100, "y": 0, "z": 0,
          "positionSource": "vertexIds", "doorId": 1, "state": 2, "attributes": 1,
          "requires": [ { "kind": "item", "id": "655", "label": "the Barrow Key" } ] }
        """);

    /// <summary>A decoration placement as the importer writes one, with the event a case states.</summary>
    private static PlacementDefinition Decoration(string name, int eventId) => Placement(
        "decoration",
        "decoration-0",
        $$"""{ "id": "decoration-0", "kind": "decoration", "sourceField": "decorations", "sourceIndex": 0, "x": 0, "y": 100, "z": 0, "yaw": 0, "name": "{{name}}", "descriptionId": 1, "flags": 0, "cog": 51, "eventId": {{eventId}}, "triggerRange": 0, "eventVarId": 0 }""");

    private static PlacementDefinition Spawn() => Placement(
        "spawn",
        "spawn-0",
        """{ "id": "spawn-0", "kind": "spawn", "sourceField": "spawnPoints", "sourceIndex": 0, "x": 0, "y": 0, "z": 0, "radius": 32, "type": 3, "treasureLevelOrMonsterIndex": 1, "attributes": 0, "group": 0 }""");

    private static PlacementDefinition Light() => Placement(
        "light",
        "light-0",
        """{ "id": "light-0", "kind": "light", "sourceField": "lights", "sourceIndex": 0, "x": 0, "y": 0, "z": 0, "radius": 400, "red": 255, "green": 255, "blue": 255, "type": 0, "attributes": 0, "brightness": 8 }""");

    private static PlacementDefinition Placement(string kind, string id, string json) =>
        new(new PlacementContentId(kind, id), "placements", 0, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));

    /// <summary>A party the ruleset's requirements are judged against, holding nothing until a case gives it something.</summary>
    private static PartyEntity Party(int coins = 0) =>
        new PartyEntityFactory().Create(new PartyCreation(
            [new MemberCreation(new PartyMemberSeed(
                "Tester",
                new RaceId("Human"),
                new ClassId("Knight"),
                [new AttributeScore(new AttributeId("Might"), 13)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0)))],
            coins,
            foodPortions: 2,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));

    /// <summary>One place, as content declares it, for the requirement check to read.</summary>
    private static ContentCatalog Catalog(string place) =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                    }
                    """)
                .Add(
                    "packs/world/places.json",
                    $$"""{ "documentId": "places", "definitionKind": "place", "entries": [ {{place}} ] }"""),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();
}
