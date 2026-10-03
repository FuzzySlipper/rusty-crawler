using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Scene;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's scene reading: a place's mesh and materials from its render entry and the media pack's images beside it,
/// its doors by the collision's names, its sky outdoors only, and its light by the clock's hour.
/// </summary>
public sealed class ScenePolicyTests
{
    [Fact]
    public void A_place_is_read_with_its_mesh_beside_its_pack_its_materials_images_its_doors_and_its_sky()
    {
        MightAndMagic7Scene scene = MightAndMagic7Scene.Read(Content(), () => null, MightAndMagic7Time.Compose(), MightAndMagic7Tuning.Read(null))!;

        PlaceScene region = scene.For(new PlaceId("1"))!;
        Assert.Equal("packs/world/render/1.mesh", region.MeshPath);
        Assert.Equal("packs/media/skies/sky-plansky3.png", region.Sky);
        Assert.Equal(
            [
                new SceneMaterial("packs/media/textures/grastyl.png", SceneAlpha.Opaque),
                new SceneMaterial("packs/media/textures/cfb1.png", SceneAlpha.Cutout),
                new SceneMaterial(null, SceneAlpha.Cutout),
            ],
            region.Materials);

        PlaceScene interior = scene.For(new PlaceId("2"))!;
        Assert.Null(interior.Sky);
        Assert.Equal("door-3", interior.Doors[3]);
        Assert.True(interior.Materials.Single().Emissive);
        Assert.Null(scene.For(new PlaceId("9")));
    }

    [Fact]
    public void The_sun_lights_a_region_by_day_and_leaves_it_to_a_dim_night_and_the_carried_light()
    {
        GameClock clock = MightAndMagic7Time.Compose();
        MightAndMagic7Scene scene = MightAndMagic7Scene.Read(Content(), () => null, clock, MightAndMagic7Tuning.Read(null))!;

        // The session begins in the morning: the sun is up and the sky shows.
        SceneLighting morning = scene.Lighting(new PlaceId("1"), outdoors: true);
        Assert.NotNull(morning.SunDirection);
        Assert.True(morning.SkyVisible);
        Assert.Null(morning.Carried);
        Assert.Equal((24_000f, 90_000f), morning.Fog);

        clock.Advance(GameDuration.FromHours(17));
        SceneLighting night = scene.Lighting(new PlaceId("1"), outdoors: true);
        Assert.True(clock.IsNight);
        Assert.Null(night.SunDirection);
        Assert.False(night.SkyVisible);
        Assert.NotNull(night.Carried);
        Assert.True(night.AmbientIntensity < morning.AmbientIntensity);

        // Indoors there is no sun at any hour, and the party's light carries the room.
        SceneLighting indoors = scene.Lighting(new PlaceId("2"), outdoors: false);
        Assert.Null(indoors.SunDirection);
        Assert.NotNull(indoors.Carried);
        Assert.Equal((3_000f, 14_000f), indoors.Fog);
    }

    [Fact]
    public void A_sprite_group_is_read_with_its_atlas_beside_the_media_pack()
    {
        MightAndMagic7Scene scene = MightAndMagic7Scene.Read(Content(), () => null, MightAndMagic7Time.Compose(), MightAndMagic7Tuning.Read(null))!;

        SceneSprite walk = scene.Sprite("frame-12")!;
        Assert.Equal("packs/media/sprites/frame-12.png", walk.Texture);
        Assert.Equal((8, 32, 48, 2d, false), (walk.Views, walk.CellWidth, walk.CellHeight, walk.Scale, walk.Centred));
        Assert.Equal([0.5, 0.25], walk.Seconds);
        Assert.Null(scene.Sprite("frame-99"));
    }

    [Fact]
    public void A_blow_the_fight_resolved_shows_on_the_creature_it_struck_and_marks_it_once()
    {
        MonsterAiPolicyTests.Fixture fixture = MonsterAiPolicyTests.Fixture.Of(engineRoll: 0);
        CombatState fight = fixture.Fight();
        MightAndMagic7Session session = fixture.Session;
        MightAndMagic7Scene scene = MightAndMagic7Scene.Read(Content(), () => session.World, MightAndMagic7Time.Compose(), MightAndMagic7Tuning.Read(null),
            combat: () => fixture.Combat, fight: () => session.Combat, director: () => session.Owners.Director)!;
        PlaceId place = session.World!.Place;
        string beast = fixture.Combatant(fight, "beast").Subject.Placement!.Content.ToString();

        // Before any blow the creature stands, and there is nothing to mark.
        Assert.Equal("stand", scene.Objects(place, 0).Single(drawn => drawn.Id == beast).Sprite);
        Assert.Empty(scene.Bursts(place, 0));

        CombatantId member = fight.Combatants.First(actor => actor.Side == CombatSide.Party).Id;
        CombatResult blow = fight.Order(new AttackOrder(member, AttackKind.Melee, fixture.Combatant(fight, "beast").Id));
        Assert.True(blow.IsApplied, blow.Code);
        bool hit = blow.Resolution!.Hit;

        // The next frame shows what the blow did: a creature struck flinches, from the start of its flinch, and one
        // burst marks where it stood; the mark is given once.
        SceneObject struck = scene.Objects(place, 1).Single(drawn => drawn.Id == beast);
        Assert.Equal(hit ? "hit" : "stand", struck.Sprite);
        if (hit) Assert.Equal((0d, false), (struck.Seconds, struck.Loop));
        SceneBurst mark = Assert.Single(scene.Bursts(place, 1));
        Assert.Equal(hit ? "blow-struck" : "blow-missed", mark.Label);
        Assert.True(mark.At.Z > 0);
        Assert.Empty(scene.Bursts(place, 1.1));

        // Once its flinch has run, it stands again.
        Assert.Equal("stand", scene.Objects(place, 5).Single(drawn => drawn.Id == beast).Sprite);
    }

    [Fact]
    public void Content_without_render_entries_draws_nothing()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/world/pack.json", TestPacks.Manifest("world", ("places", "place")))
            .Add("packs/world/places.json", TestPacks.Document("places", "place", """{"id":"1","kind":"region"}"""));

        Assert.Null(MightAndMagic7Scene.Read(ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid(), () => null, MightAndMagic7Time.Compose(), MightAndMagic7Tuning.Read(null)));
        Assert.Null(MightAndMagic7Scene.Read(null, () => null, MightAndMagic7Time.Compose(), MightAndMagic7Tuning.Read(null)));
    }

    private static ContentCatalog Content()
    {
        InMemoryContentSource source = new InMemoryContentSource()
            .Add("packs/world/pack.json", TestPacks.Manifest("world", ("place-render", "place-render")))
            .Add("packs/world/place-render.json", TestPacks.Document("place-render", "place-render",
                """
                {"id":"1","mesh":"render/1.mesh","sky":"sky-plansky3","doors":[],
                 "materials":[{"texture":"grastyl","surface":"terrain"},{"texture":"cfb1","surface":"face"},{"texture":"","surface":"face"}]}
                """,
                """
                {"id":"2","mesh":"render/2.mesh","sky":"","doors":["door-3"],
                 "materials":[{"texture":"cfb1","surface":"sky"}]}
                """))
            .Add("packs/media/pack.json", TestPacks.Manifest("media", ("textures", "texture"), ("sprites", "sprite"), ("looks", "look")))
            .Add("packs/media/sprites.json", TestPacks.Document("sprites", "sprite",
                """
                {"id":"frame-12","path":"sprites/frame-12.png","width":256,"height":96,"columns":8,"cellWidth":32,"cellHeight":48,
                 "octants":8,"scale":2,"centred":false,"lit":false,"seconds":[0.5,0.25]}
                """,
                """{"id":"stand","path":"sprites/stand.png","width":32,"height":48,"columns":1,"cellWidth":32,"cellHeight":48,"octants":1,"scale":1,"centred":false,"lit":false,"seconds":[1]}""",
                """{"id":"hit","path":"sprites/hit.png","width":64,"height":48,"columns":2,"cellWidth":32,"cellHeight":48,"octants":1,"scale":1,"centred":false,"lit":false,"seconds":[0.25,0.25]}"""))
            .Add("packs/media/looks.json", TestPacks.Document("looks", "look",
                """{"id":"monster-151","height":160,"radius":40,"actions":["frame-12","frame-12","","","","","",""]}""",
                """{"id":"monster-7","height":120,"radius":30,"actions":["stand","stand","","","hit","","",""]}""",
                """{"id":"decoration-2","sprite":"","hidden":true}"""))
            .Add("packs/media/textures.json", TestPacks.Document("textures", "texture",
                """{"id":"grastyl","path":"textures/grastyl.png"}""",
                """{"id":"cfb1","path":"textures/cfb1.png"}""",
                """{"id":"sky-plansky3","path":"skies/sky-plansky3.png"}"""));
        return ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid();
    }
}
