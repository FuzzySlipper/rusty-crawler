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
public sealed record SceneLighting(
    Vector3 Ambient,
    float AmbientIntensity,
    Vector3? SunDirection,
    Vector3 Sun,
    float SunIntensity,
    Vector3 Background,
    (Vector3 Colour, float Intensity, float Range)? Carried = null,
    bool SkyVisible = true);

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
}

/// <summary>
/// What draws the live world: the session hands it the party after every admitted update, and releases it with itself.
/// </summary>
public interface IWorldPresenter : IDisposable
{
    /// <summary>Draws the world as it stands after this update, from the party's own eye.</summary>
    /// <param name="party">The party's pose owner.</param>
    void Present(PartyPoseOwner party);

    /// <summary>What it drew for the current place, or null before it drew one.</summary>
    WorldViewReport? Report { get; }
}
