using System.Numerics;
using System.Text.Json;
using PartyRpg.Kit.Combat;
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
    internal const string SpriteDefinitionKind = "sprite";
    internal const string LookDefinitionKind = "look";

    /// <summary>A monster look's animations, in the donor's <c>ActorAnimation</c> order (OpenEnroth <c>ActorEnums.h:80-88</c>).</summary>
    private const int Standing = 0, Walking = 1, Dying = 5, Dead = 6;

    private readonly Dictionary<PlaceId, PlaceScene> _scenes = [];
    private readonly Dictionary<string, SceneSprite> _sprites = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Sprite, bool Hidden)> _looks = new(StringComparer.Ordinal);
    private readonly Dictionary<int, IReadOnlyList<string>> _monsters = [];
    private CorpseGround? _corpses;

    // Presentation memory, not world state: when each body first lay, so a fresh body falls before it lies still, and
    // the current place's decorations. Forgotten with the place.
    private readonly Dictionary<long, double> _fell = [];
    private readonly List<(string Id, string Sprite, PlacePose Pose)> _decorations = [];
    private PlaceId? _remembered;
    private Func<MightAndMagic7Combat?> _combat = () => null;

    /// <summary>A map decoration's row in the decoration list, as the importer writes it.</summary>
    private const string DecorationListField = "descriptionId";

    /// <summary>A map decoration's level flags, as the importer writes them.</summary>
    private const string DecorationFlagsField = "flags";

    /// <summary>A map sprite object's row in the object list, as the importer writes it.</summary>
    private const string ObjectListField = "objectDescId";

    /// <summary><c>LEVEL_DECORATION_INVISIBLE</c> (OpenEnroth <c>src/Engine/Objects/Decoration.h:18</c>).</summary>
    private const int InvisibleDecoration = 0x20;
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
    internal static MightAndMagic7Scene? Read(ContentCatalog? catalog, Func<SessionWorld?> world, GameClock clock, TuningProfile tuning,
        CorpseGround? corpses = null, Func<MightAndMagic7Combat?>? combat = null)
    {
        if (catalog is null) return null;
        Dictionary<string, string> textures = new(StringComparer.Ordinal);
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(TextureDefinitionKind))
        {
            textures[entry.Id] = $"{pack.Directory}/{entry.Payload.GetProperty("path").GetString()}";
        }

        MightAndMagic7Scene scene = new(world, clock, tuning) { _corpses = corpses, _combat = combat ?? (() => null) };
        foreach ((LoadedPack pack, _, ContentEntry entry) in catalog.Entries(SpriteDefinitionKind))
        {
            JsonElement sprite = entry.Payload;
            scene._sprites[entry.Id] = new SceneSprite(
                $"{pack.Directory}/{sprite.GetProperty("path").GetString()}",
                sprite.GetProperty("width").GetInt32(),
                sprite.GetProperty("height").GetInt32(),
                sprite.GetProperty("columns").GetInt32(),
                sprite.GetProperty("cellWidth").GetInt32(),
                sprite.GetProperty("cellHeight").GetInt32(),
                sprite.GetProperty("octants").GetInt32(),
                sprite.GetProperty("scale").GetDouble(),
                sprite.GetProperty("centred").GetBoolean(),
                sprite.GetProperty("lit").GetBoolean(),
                [.. sprite.GetProperty("seconds").EnumerateArray().Select(value => value.GetDouble())]);
        }

        foreach ((_, _, ContentEntry entry) in catalog.Entries(LookDefinitionKind))
        {
            JsonElement look = entry.Payload;
            if (look.TryGetProperty("actions", out JsonElement actions))
            {
                if (int.TryParse(entry.Id.AsSpan("monster-".Length), out int monster))
                    scene._monsters[monster] = [.. actions.EnumerateArray().Select(action => action.GetString() ?? string.Empty)];
                continue;
            }

            scene._looks[entry.Id] = (look.GetProperty("sprite").GetString() ?? string.Empty,
                look.TryGetProperty("hidden", out JsonElement hidden) && hidden.GetBoolean());
        }

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
    public SceneSprite? Sprite(string sprite) => _sprites.GetValueOrDefault(sprite);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// What stands in a place is read from its owners each update: a decoration and a pile of items from the place's
    /// placements (a pile once searched is gone, a decoration the level marks invisible — <c>LEVEL_DECORATION_INVISIBLE</c>,
    /// OpenEnroth <c>src/Engine/Objects/Decoration.h:18</c> — is not drawn); a creature or a person from the population,
    /// at its live feet and facing, walking while it moves and standing otherwise; and a body from the corpse ground, at
    /// where the creature fell, falling once and then lying still. A creature a body answers for is drawn as the body.
    /// </para>
    /// <para>
    /// Every object is named by its owner's identity — a placement's content identity, which the reticle and the save
    /// use, or a body's corpse serial — so what is drawn is what is aimed at; the view draws it under that name.
    /// </para>
    /// </remarks>
    public IReadOnlyList<SceneObject> Objects(PlaceId place, double seconds)
    {
        if (_world() is not { } world || world.Place != place) return [];
        if (_remembered != place)
        {
            // A place entered or re-entered starts with no memory: a body already lying there lies still, and the place's
            // decorations — which only an event changes — are read once.
            _remembered = place;
            _fell.Clear();
            foreach (Corpse body in _corpses?.In(place) ?? []) _fell[body.Serial] = double.NegativeInfinity;
            _decorations.Clear();
            foreach (PlacementDefinition placement in ((IInteractionWorld)world).Placements)
            {
                if (placement.Content.Kind != MightAndMagic7Interaction.DecorationPlacementKind) continue;
                if (((placement.Source.GetInt32(DecorationFlagsField) ?? 0) & InvisibleDecoration) != 0) continue;
                if (Look($"decoration-{placement.Source.GetInt32(DecorationListField)}") is { } sprite)
                    _decorations.Add((placement.Content.ToString(), sprite, placement.Pose));
            }
        }

        List<SceneObject> objects = [.. _decorations.Select(decoration => new SceneObject(decoration.Id, decoration.Sprite, decoration.Pose, seconds))];
        foreach (PlacementDefinition placement in ((IInteractionWorld)world).Placements)
        {
            if (placement.Content.Kind != MightAndMagic7Containers.PilePlacementKind) continue;
            if (!MightAndMagic7Containers.PileLies(placement, world.Interactions.StateOf(place, placement.Content).State)) continue;
            if (Look($"object-{placement.Source.GetInt32(ObjectListField)}") is { } sprite)
                objects.Add(new SceneObject(placement.Content.ToString(), sprite, placement.Pose, seconds));
        }

        HashSet<PlacementContentId> bodies = [];
        foreach (Corpse body in _corpses?.In(place) ?? [])
        {
            bodies.Add(body.Content);
            if (Actions(body.Body) is not { } actions) continue;
            if (!_fell.TryGetValue(body.Serial, out double fell)) _fell[body.Serial] = fell = seconds;
            SceneSprite? dying = actions[Dying].Length > 0 ? Sprite(actions[Dying]) : null;
            bool falling = dying is not null && seconds - fell < dying.TotalSeconds;
            string sprite = falling ? actions[Dying] : actions[Dead].Length > 0 ? actions[Dead] : actions[Dying];

            // A body is the corpse ground's own record, named by its serial: the creature it was shares its content
            // identity, and the two must never be drawn as one object.
            if (sprite.Length > 0)
                objects.Add(new SceneObject($"corpse:{body.Serial}", sprite, body.Body.Pose, falling ? seconds - fell : double.MaxValue, Loop: false));
        }

        foreach (PlacePopulationEntity entity in world.Population.Entities)
        {
            if (!entity.IsAlive || bodies.Contains(entity.Content) || Actions(entity.Placement) is not { } actions) continue;
            bool walking = world.Creatures?.IsWalking(CombatantId.Of(entity.Id)) == true;
            string sprite = walking && actions[Walking].Length > 0 ? actions[Walking] : actions[Standing];
            if (sprite.Length > 0) objects.Add(new SceneObject(entity.Content.ToString(), sprite, entity.Pose, seconds));
        }

        return objects;
    }

    /// <summary>A decoration's or object's sprite, or null when its look is hidden or content carries none.</summary>
    private string? Look(string look) => _looks.TryGetValue(look, out var found) && !found.Hidden && found.Sprite.Length > 0 ? found.Sprite : null;

    /// <summary>A creature's or person's eight animations, by the row the fight reads it as, or null when it has no look.</summary>
    private IReadOnlyList<string>? Actions(PlacementDefinition placement) =>
        _combat()?.RowOf(placement) is { } monster ? _monsters.GetValueOrDefault(monster) : null;


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
