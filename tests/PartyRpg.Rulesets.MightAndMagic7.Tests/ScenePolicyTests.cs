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
            .Add("packs/media/pack.json", TestPacks.Manifest("media", ("textures", "texture")))
            .Add("packs/media/textures.json", TestPacks.Document("textures", "texture",
                """{"id":"grastyl","path":"textures/grastyl.png"}""",
                """{"id":"cfb1","path":"textures/cfb1.png"}""",
                """{"id":"sky-plansky3","path":"skies/sky-plansky3.png"}"""));
        return ContentCatalogLoader.Load(source, new ContentLayout("packs", "imports", "bundles")).RequireValid();
    }
}
