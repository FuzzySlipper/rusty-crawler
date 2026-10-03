using System.Numerics;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
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
/// <b>The eye</b> stands 160 units above the party's feet, the donor's default <c>eyeLevel</c> (OpenEnroth
/// <c>src/Engine/Party.h:281</c>, read from <c>PartyEyeLevel</c> at <c>src/Engine/Party.cpp:64</c>).
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

    /// <summary>The donor's default eye level above the party's feet.</summary>
    private const double EyeLevel = 160;

    private readonly Dictionary<PlaceId, PlaceScene> _scenes = [];
    private readonly Func<SessionWorld?> _world;
    private readonly GameClock _clock;

    private MightAndMagic7Scene(Func<SessionWorld?> world, GameClock clock)
    {
        _world = world;
        _clock = clock;
    }

    /// <inheritdoc />
    public PartyViewOffsets Eye { get; } = new(lookPitch: 0, eyeHeight: EyeLevel, bobOffset: 0);

    /// <inheritdoc />
    public double FieldOfViewDegrees => 60;

    /// <inheritdoc />
    /// <remarks>A region is 127 squares of 512 units on a side; the camera sees across all of it.</remarks>
    public double ViewDistance => 96_000;

    /// <summary>Reads every place's scene from content, or null when content carries none.</summary>
    /// <param name="catalog">The selected content.</param>
    /// <param name="world">The session's live world, read when a door is asked about.</param>
    /// <param name="clock">The session's one clock, read when the light is asked about.</param>
    internal static MightAndMagic7Scene? Read(ContentCatalog? catalog, Func<SessionWorld?> world, GameClock clock)
    {
        if (catalog is null) return null;
        Dictionary<string, string> textures = new(StringComparer.Ordinal);
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(TextureDefinitionKind))
        {
            textures[entry.Id] = $"{pack.Directory}/{entry.Payload.GetProperty("path").GetString()}";
        }

        MightAndMagic7Scene scene = new(world, clock);
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
            return new SceneLighting(new Vector3(1f, 0.95f, 0.85f), 0.45f, null, Vector3.Zero, 0f, new Vector3(0f, 0f, 0f),
                (new Vector3(1f, 0.85f, 0.6f), 1.6f, 2400f));
        }

        // Outdoors the sun climbs from the calendar's dawn to its noon and sets at its dusk, in the east-west plane;
        // the quarter-minute it is read at is the light's whole resolution, so the Engine's light changes at most
        // that often.
        GameDate now = _clock.Now;
        double minutes = (now.Hour * 60) + now.Minute;
        double dawn = (_clock.Daylight.Dawn.Hour * 60) + _clock.Daylight.Dawn.Minute;
        double dusk = (_clock.Daylight.Dusk.Hour * 60) + _clock.Daylight.Dusk.Minute;
        double day = Math.Clamp((minutes - dawn) / (dusk - dawn), 0, 1);
        double height = minutes < dawn || minutes >= dusk ? 0 : Math.Sin(day * Math.PI);
        float level = (float)Math.Round(height * 20) / 20f;
        Vector3? direction = level <= 0 ? null : Vector3.Normalize(new Vector3((float)Math.Cos(day * Math.PI), -Math.Max(level, 0.15f), 0.35f));
        Vector3 sky = Vector3.Lerp(night, new Vector3(0.62f, 0.72f, 0.86f), level);
        return new SceneLighting(
            Vector3.Lerp(new Vector3(0.5f, 0.55f, 0.8f), Vector3.One, level),
            0.18f + (0.42f * level),
            direction,
            new Vector3(1f, 0.96f, 0.88f),
            1.4f * level,
            sky,
            level <= 0 ? (new Vector3(1f, 0.85f, 0.6f), 1.2f, 2400f) : null,
            SkyVisible: level > 0);
    }
}
