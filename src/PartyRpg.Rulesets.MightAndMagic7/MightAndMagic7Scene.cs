using System.Numerics;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Scene;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's meaning for the drawn world: which content a place is drawn from, which of a region's surfaces are
/// water or sky, where the party's eye is, how a place is lit at an hour, and which doors the interaction state holds
/// closed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Content.</b> A place is drawn from its <c>place-render</c> entry: the binary mesh beside it and one material per
/// entry of its list, each naming a <c>texture</c> entry whose image lies beside that entry's own pack. A material
/// whose texture the import could not resolve names none and is drawn flat, and the view says which.
/// </para>
/// <para>
/// <b>Doors</b> are read exactly as the collision reads them (<see cref="MightAndMagic7Geometry"/>): the place's door
/// placement, its recorded state in the interaction ledger, and <see cref="MightAndMagic7Interaction.DoorState"/>. A
/// drawn door and a walked door can therefore never disagree.
/// </para>
/// <para>
/// <b>The eye</b> is the movement's (<see cref="MightAndMagic7Movement.EyeLevel"/>); the reticle shares its heading.
/// </para>
/// <para>
/// <b>Light is ours.</b> The original shades terrain by a sun that crosses the sky with the hour and dims the world
/// at night, and lights interiors by sector levels and the party's torch (OpenEnroth <c>src/Engine/Graphics/Weather.cpp</c>,
/// <c>src/Engine/Graphics/Outdoor.cpp</c>). This keeps that shape — a sun that rises at the calendar's dawn and sets at
/// its dusk, a dim blue night, and a dim interior lit by the light the party carries — with values chosen here rather
/// than taken from the donor's lighting tables.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Scene : IPlaceSceneSource, ISceneRule
{
    internal const string RenderDefinitionKind = "place-render";
    internal const string TextureDefinitionKind = "texture";

    private readonly Dictionary<PlaceId, PlaceScene> _scenes = [];
    private readonly Func<SessionWorld?> _world;
    private readonly GameClock _clock;
    private readonly TuningProfile _tuning;

    private MightAndMagic7Scene(Func<SessionWorld?> world, GameClock clock, TuningProfile tuning)
    {
        _world = world;
        _clock = clock;
        _tuning = tuning;
    }

    /// <inheritdoc />
    public double FieldOfViewDegrees => _tuning[MightAndMagic7Tuning.ViewFieldOfView];

    /// <inheritdoc />
    public double ViewDistance => _tuning[MightAndMagic7Tuning.ViewDistance];

    /// <summary>Reads every place's scene from content, or null when content carries none.</summary>
    /// <param name="catalog">The selected content.</param>
    /// <param name="world">The session's live world, read when a door is asked about.</param>
    /// <param name="clock">The session's one clock, read when the light is asked about.</param>
    /// <param name="tuning">The selected tuning, which holds the view's and the light's adjustable values.</param>
    internal static MightAndMagic7Scene? Read(ContentCatalog? catalog, Func<SessionWorld?> world, GameClock clock, TuningProfile tuning)
    {
        if (catalog is null) return null;
        Dictionary<string, string> textures = new(StringComparer.Ordinal);
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(TextureDefinitionKind))
        {
            textures[entry.Id] = $"{pack.Directory}/{entry.Payload.GetProperty("path").GetString()}";
        }

        MightAndMagic7Scene scene = new(world, clock, tuning);
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(RenderDefinitionKind))
        {
            JsonElement payload = entry.Payload;
            List<SceneMaterial> materials = [];
            foreach (JsonElement material in payload.GetProperty("materials").EnumerateArray())
            {
                string texture = material.GetProperty("texture").GetString() ?? string.Empty;
                string surface = material.GetProperty("surface").GetString() ?? "face";
                materials.Add(new SceneMaterial(
                    textures.TryGetValue(texture, out string? path) ? path : null,

                    // Terrain and water fill every texel; a face's palette entry zero is cut out, which is how the
                    // original draws a grate or a cut-out edge.
                    surface is "terrain" or "water" ? SceneAlpha.Opaque : SceneAlpha.Cutout,
                    Emissive: surface == "sky"));
            }

            Dictionary<int, string> doors = [];
            foreach (JsonElement door in payload.GetProperty("doors").EnumerateArray())
            {
                string id = door.GetString() ?? string.Empty;
                if (id.StartsWith("door-", StringComparison.Ordinal) && int.TryParse(id.AsSpan(5), out int index)) doors[index] = id;
            }

            string sky = payload.GetProperty("sky").GetString() ?? string.Empty;
            PlaceId place = new(entry.Id);
            scene._scenes[place] = new PlaceScene(
                place,
                $"{pack.Directory}/{payload.GetProperty("mesh").GetString()}",
                materials,
                doors,
                sky.Length > 0 && textures.TryGetValue(sky, out string? panorama) ? panorama : null);
        }

        return scene._scenes.Count == 0 ? null : scene;
    }

    /// <summary>The view over this game's scenes, through the Engine's graphics, cameras and content.</summary>
    internal static WorldView? View(MightAndMagic7Scene? scene, IEngineContext? engine) =>
        scene is null || engine is null ? null : new WorldView(engine, scene, scene, MightAndMagic7Movement.Space);

    /// <inheritdoc />
    public PlaceScene? For(PlaceId place) => _scenes.GetValueOrDefault(place);

    /// <inheritdoc />
    public bool IsClosed(PlaceId place, string door)
    {
        if (_world() is not { } world || world.Place != place) return false;
        PlacementDefinition? placement = ((IInteractionWorld)world).Placements.FirstOrDefault(candidate =>
            string.Equals(candidate.Content.Kind, MightAndMagic7Interaction.DoorPlacementKind, StringComparison.Ordinal)
            && string.Equals(candidate.Content.Id, door, StringComparison.Ordinal));
        if (placement is null) return false;
        return MightAndMagic7Interaction.DoorState(placement, world.Interactions.StateOf(place, placement.Content).State)
            != MightAndMagic7Interaction.OpenState;
    }

    /// <inheritdoc />
    public SceneLighting Lighting(PlaceId place, bool outdoors)
    {
        Vector3 night = new(0.03f, 0.04f, 0.09f);
        if (!outdoors)
        {
            // Interiors: a dim even fill and the light the party carries, which reaches a room but not a long hall.
            return new SceneLighting(new Vector3(1f, 0.95f, 0.85f), Light(MightAndMagic7Tuning.LightInteriorAmbient), null, Vector3.Zero, 0f,
                new Vector3(0f, 0f, 0f), Carried());
        }

        // Outdoors the sun climbs from the calendar's dawn to its noon and sets at its dusk, in the east-west plane. Its
        // height and its arc are read in coarse steps, so the answer — and the Engine's lights — change a few dozen
        // times a day rather than every game minute.
        GameDate now = _clock.Now;
        double minutes = (now.Hour * 60) + now.Minute;
        double dawn = (_clock.Daylight.Dawn.Hour * 60) + _clock.Daylight.Dawn.Minute;
        double dusk = (_clock.Daylight.Dusk.Hour * 60) + _clock.Daylight.Dusk.Minute;
        double day = Math.Clamp((minutes - dawn) / (dusk - dawn), 0, 1);
        double height = minutes < dawn || minutes >= dusk ? 0 : Math.Sin(day * Math.PI);
        float level = (float)Math.Round(height * 20) / 20f;
        double arc = Math.Round(day * 40) / 40;
        Vector3? direction = level <= 0 ? null : Vector3.Normalize(new Vector3((float)Math.Cos(arc * Math.PI), -Math.Max(level, 0.15f), 0.35f));
        Vector3 sky = Vector3.Lerp(night, new Vector3(0.62f, 0.72f, 0.86f), level);
        return new SceneLighting(
            Vector3.Lerp(new Vector3(0.5f, 0.55f, 0.8f), Vector3.One, level),
            float.Lerp(Light(MightAndMagic7Tuning.LightNightAmbient), Light(MightAndMagic7Tuning.LightDayAmbient), Math.Min(1f, level * 1.5f)),
            direction,
            new Vector3(1f, 0.96f, 0.88f),
            Light(MightAndMagic7Tuning.LightSun) * level,
            sky,
            level <= 0 ? Carried() : null,
            SkyVisible: level > 0);
    }

    private float Light(TuningHandle handle) => (float)_tuning[handle];

    /// <summary>The light the party carries: a warm light of the tuned strength and reach.</summary>
    private (Vector3 Colour, float Intensity, float Range) Carried() =>
        (new Vector3(1f, 0.85f, 0.6f), Light(MightAndMagic7Tuning.LightCarried), Light(MightAndMagic7Tuning.LightCarriedRange));
}
