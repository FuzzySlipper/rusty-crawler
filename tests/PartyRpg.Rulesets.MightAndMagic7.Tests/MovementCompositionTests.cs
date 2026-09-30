using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's movers, composed over an engine that supplies a spatial service: one scene per world, shared
/// by the party and the place's creatures, and released with the world that made it.
/// </summary>
/// <remarks>
/// The engine here is the scripted one, which records rather than collides. It is enough to see what the
/// composition does with the scene: how many are made, which one a place's geometry, the party's steps and a
/// creature's steps go to, and when each is released — including when a world is replaced, as a restart
/// replaces it, which must not leave the old one's scene behind.
/// </remarks>
public sealed class MovementCompositionTests
{
    [Fact]
    public void A_composed_world_makes_one_scene_and_its_creatures_walk_in_it()
    {
        ScriptedSpatialService spatial = new();
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Content());
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));

        SpatialSession scene = Assert.Single(spatial.Sessions).Session;
        SessionWorld world = ((MightAndMagic7Session)session).World!;
        EnginePartyMover mover = Assert.IsType<EnginePartyMover>(world.Mover);
        Assert.Same(scene, mover.Session);

        // The place the party starts in carries geometry, and it was admitted to that scene.
        Assert.Same(scene, Assert.Single(spatial.Admissions).Session);

        // A creature's step is proposed in the same scene the place's collision was admitted to.
        ICreatureMover creatures = Assert.IsType<EngineCreatureMotion>(world.Creatures);
        int before = spatial.Steps.Count;
        creatures.Move(new CreatureMoveRequest(
            CombatantId.Of(new EntityId(7)),
            new PlacePose(0, 0, 0, 0, 0),
            CombatantId.Of(new EntityId(8)),
            new PlacePose(0, 100, 0, 0, 0),
            CreatureMovePurpose.Toward,
            Speed: 1,
            ElapsedSeconds: 1.0 / 60));
        Assert.Same(scene, spatial.Steps[before].Session);

        session.Dispose();
        Assert.Equal([scene.Handle.Value], spatial.Released);
    }

    [Fact]
    public void A_world_replaced_by_another_releases_its_own_scene_and_only_its_own()
    {
        // A restart builds the replacement before it releases what it replaces, so two worlds stand for a
        // moment: each made exactly one scene, and releasing the first leaves the second's alone.
        ScriptedSpatialService spatial = new();
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Content());
        IGameSession first = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        first.Start();
        IGameSession replacement = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        Assert.Equal(2, spatial.Sessions.Count);

        first.Dispose();
        replacement.Start();
        replacement.Update(RulesetTestContext.Update(1, 1));
        Assert.Equal([spatial.Sessions[0].Session.Handle.Value], spatial.Released);
        Assert.Same(spatial.Sessions[1].Session, Assert.IsType<EnginePartyMover>(((MightAndMagic7Session)replacement).World!.Mover).Session);

        replacement.Dispose();
        Assert.Equal(spatial.Sessions.Select(created => created.Session.Handle.Value), spatial.Released);
    }

    /// <summary>One region with an arrival, its collision artifact, a start, and a party to play there.</summary>
    private static (string Path, string Text)[] Content() =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "place-geometry.json", "documentId": "place-geometry", "definitionKind": "place-geometry" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "Erathia", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/place-geometry.json",
            """
            {
              "documentId": "place-geometry",
              "definitionKind": "place-geometry",
              "entries": [ { "id": "1", "artifact": { "stated": "by the scripted engine, which reads nothing" } } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 5, "hitPoints": 30,
                      "spellPoints": 40,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 40 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 20 },
                                      { "id": "Accuracy", "value": 20 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
    ];
}
