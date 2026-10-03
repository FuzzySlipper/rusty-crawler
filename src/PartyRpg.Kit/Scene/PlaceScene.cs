using System.Numerics;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Scene;

/// <summary>How a material lets light and the ground show through it.</summary>
public enum SceneAlpha
{
    /// <summary>Every texel is drawn.</summary>
    Opaque,

    /// <summary>Texels whose alpha is below half are cut away: foliage, grates, a sign's cut-out edge.</summary>
    Cutout,
}

/// <summary>One material a place's mesh binds.</summary>
/// <param name="Texture">The content path of its image, or null when content names no image the material can draw.</param>
/// <param name="Alpha">How its image's transparency is drawn.</param>
/// <param name="Emissive">Whether it lights itself rather than taking the scene's light, as a sky face does.</param>
public sealed record SceneMaterial(string? Texture, SceneAlpha Alpha, bool Emissive = false);

/// <summary>Everything a place is drawn from: its mesh, its materials, its doors' identities and its sky.</summary>
/// <param name="Place">The place.</param>
/// <param name="MeshPath">The content path of the place's render geometry, in <see cref="RenderMesh"/>'s binary form.</param>
/// <param name="Materials">Its materials, by the index the mesh's groups name.</param>
/// <param name="Doors">The identity of each door a mesh part's door index names.</param>
/// <param name="Sky">The content path of the place's sky panorama, or null for a place under a roof.</param>
public sealed record PlaceScene(
    PlaceId Place,
    string MeshPath,
    IReadOnlyList<SceneMaterial> Materials,
    IReadOnlyDictionary<int, string> Doors,
    string? Sky);

/// <summary>Where a place's scene comes from, when the loaded content carries one.</summary>
public interface IPlaceSceneSource
{
    /// <summary>The place's scene, or null when content carries none for it.</summary>
    /// <param name="place">The place the party is in.</param>
    PlaceScene? For(PlaceId place);

    /// <summary>A sprite group, or null when content carries none of that identity.</summary>
    /// <param name="sprite">The group's identity, as a <see cref="SceneObject"/> names it.</param>
    SceneSprite? Sprite(string sprite);
}

/// <summary>The light a place is seen under at one moment.</summary>
/// <param name="Ambient">The light that reaches every surface, as a linear colour.</param>
/// <param name="AmbientIntensity">How strong it is.</param>
/// <param name="SunDirection">The direction the sun's light travels, in the Engine's axes, or null under a roof or at night.</param>
/// <param name="Sun">The sun's colour.</param>
/// <param name="SunIntensity">How strong it is.</param>
/// <param name="Background">The colour behind everything where no sky is drawn, and the colour distance fades into.</param>
/// <param name="Carried">The light the party carries, as a colour, with its strength and reach, or null for none.</param>
/// <param name="SkyVisible">Whether a place's sky panorama shows now; when it does not, the background colour does.</param>
/// <param name="Fog">How far from the eye distance starts to fade into the background colour and where it has wholly
/// faded, in place units, or null for no fade.</param>
public sealed record SceneLighting(
    Vector3 Ambient,
    float AmbientIntensity,
    Vector3? SunDirection,
    Vector3 Sun,
    float SunIntensity,
    Vector3 Background,
    (Vector3 Colour, float Intensity, float Range)? Carried = null,
    bool SkyVisible = true,
    (float Start, float End)? Fog = null);

/// <summary>A game's answers about how its world is seen: where the eye is, how a place is lit, and which doors stand closed.</summary>
public interface ISceneRule
{
    /// <summary>The camera's vertical field of view in degrees.</summary>
    double FieldOfViewDegrees { get; }

    /// <summary>How far the camera sees, in place units.</summary>
    double ViewDistance { get; }

    /// <summary>The light a place is seen under now.</summary>
    /// <param name="place">The place.</param>
    /// <param name="outdoors">Whether the place's scene has a sky.</param>
    SceneLighting Lighting(PlaceId place, bool outdoors);

    /// <summary>Whether a door currently stands closed, as the place's own interaction state says.</summary>
    /// <param name="place">The place.</param>
    /// <param name="door">The door's identity in the scene.</param>
    bool IsClosed(PlaceId place, string door);

    /// <summary>What stands in the place now and is drawn as a sprite, as the place's owners hold it.</summary>
    /// <param name="place">The place.</param>
    /// <param name="seconds">The session's admitted time, which an animation is read against.</param>
    IReadOnlyList<SceneObject> Objects(PlaceId place, double seconds);

    /// <summary>
    /// What a game's events have made of one switch's faces: whether they are hidden and which of the place's materials
    /// they draw with. Null fields leave the mesh's own answer: hidden as it starts, drawn with its own materials.
    /// </summary>
    /// <param name="place">The place.</param>
    /// <param name="cog">The switch, as the mesh names it.</param>
    SceneSwitch Switch(PlaceId place, int cog) => default;

    /// <summary>The bursts to emit now: what the place's owners resolved since the last time the view asked.</summary>
    /// <param name="place">The place.</param>
    /// <param name="seconds">The session's admitted time.</param>
    IReadOnlyList<SceneBurst> Bursts(PlaceId place, double seconds) => [];
}

/// <summary>
/// What draws the live world: the session hands it the party after every admitted update, and releases it with itself.
/// </summary>
public interface IWorldPresenter : IDisposable
{
    /// <summary>Draws the world as it stands after this update, from the party's own eye.</summary>
    /// <param name="party">The party's pose owner.</param>
    /// <param name="seconds">The session's admitted time, which the world's animations are read against.</param>
    void Present(PartyPoseOwner party, double seconds);

    /// <summary>What it drew for the current place, or null before it drew one.</summary>
    WorldViewReport? Report { get; }
}

/// <summary>
/// One sprite group as content packs it: an atlas image of equal cells, each cell one view of one frame, standing on its
/// cell's bottom edge (or centred in it).
/// </summary>
/// <param name="Texture">The content path of the atlas image.</param>
/// <param name="Width">The atlas's width in texels.</param>
/// <param name="Height">The atlas's height in texels.</param>
/// <param name="Columns">How many cells a row holds.</param>
/// <param name="CellWidth">One cell's width in texels.</param>
/// <param name="CellHeight">One cell's height in texels.</param>
/// <param name="Views">How many views a frame has around its facing: one, or eight.</param>
/// <param name="Scale">How many place units one texel is.</param>
/// <param name="Centred">Whether the group is centred on its point rather than standing on it.</param>
/// <param name="SelfLit">Whether it lights itself rather than taking the scene's light.</param>
/// <param name="Seconds">How long each frame shows; their count is the group's frame count.</param>
public sealed record SceneSprite(
    string Texture,
    int Width,
    int Height,
    int Columns,
    int CellWidth,
    int CellHeight,
    int Views,
    double Scale,
    bool Centred,
    bool SelfLit,
    IReadOnlyList<double> Seconds)
{
    /// <summary>How long the whole animation runs.</summary>
    public double TotalSeconds { get; } = Seconds.Sum();

    /// <summary>The frame shown a given time into the group's animation: looping, or held at the last frame.</summary>
    /// <param name="seconds">How far into the animation it is.</param>
    /// <param name="loop">Whether the animation repeats.</param>
    public int FrameAt(double seconds, bool loop)
    {
        double total = TotalSeconds;
        if (Seconds.Count <= 1 || total <= 0) return 0;
        double t = loop ? ((seconds % total) + total) % total : Math.Min(Math.Max(seconds, 0), total);
        for (int frame = 0; frame < Seconds.Count; frame++)
        {
            if (t < Seconds[frame]) return frame;
            t -= Seconds[frame];
        }

        return Seconds.Count - 1;
    }
}

/// <summary>Something that stands in a place and is drawn as a sprite: a creature, a person, a decoration, a body.</summary>
/// <param name="Id">Its canonical identity — the placement or entity it is — which keeps its drawing across updates.</param>
/// <param name="Sprite">The sprite group it shows now.</param>
/// <param name="Feet">Where it stands and faces, in the place's own coordinates and facing units.</param>
/// <param name="Seconds">How far into the group's animation it is.</param>
/// <param name="Loop">Whether that animation repeats; one that does not holds its last frame.</param>
public sealed record SceneObject(string Id, string Sprite, PlacePose Feet, double Seconds, bool Loop = true);

/// <summary>
/// A brief burst at a point in the world that marks what a resolved act did there: a blow that landed, one that missed,
/// a spell that struck. It is emitted once and the Engine retires it; nothing keeps it.
/// </summary>
/// <param name="At">Where, in the place's own coordinates; its height is the burst's centre.</param>
/// <param name="Colour">Its colour, linear RGB.</param>
/// <param name="Count">How many particles it throws.</param>
/// <param name="Size">How large each particle starts, in place units.</param>
/// <param name="Seconds">How long the burst lasts.</param>
/// <param name="Speed">How fast its particles fly out from the point, in place units a second, before they fall.</param>
/// <param name="Label">What it marks, for the Engine's report of the emission.</param>
public sealed record SceneBurst(PlacePose At, Vector3 Colour, int Count, float Size, float Seconds, float Speed, string Label);

/// <summary>What a game's events have made of one switch's faces.</summary>
/// <param name="Hidden">Whether they are hidden, or null for the mesh's own starting state.</param>
/// <param name="Material">The place material every face of the switch draws with, or null for each face's own.</param>
public readonly record struct SceneSwitch(bool? Hidden, int? Material);
