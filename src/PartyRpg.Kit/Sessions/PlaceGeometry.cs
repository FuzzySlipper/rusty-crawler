using System.Numerics;
using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// One place's collision geometry, in the engine's own canonical artifact document.
/// </summary>
/// <remarks>
/// The bytes are the engine's document, not a kit format: the engine parses them itself, and everything
/// between content and that parse copies them unchanged. A kit that re-wrote the document would own a
/// second copy of the engine's schema and would be the first thing to disagree with it.
/// </remarks>
public sealed record PlaceGeometry
{
    /// <summary>Creates a place's geometry.</summary>
    /// <param name="path">The artifact's own path, which identifies it to the engine's content owner.</param>
    /// <param name="artifact">The artifact document's bytes, exactly as content wrote them.</param>
    /// <param name="surfaces">The place's named ground, or null when content names none.</param>
    /// <exception cref="ArgumentException">The artifact has no path or no bytes.</exception>
    public PlaceGeometry(string path, ReadOnlyMemory<byte> artifact, PlaceSurfaces? surfaces = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (artifact.Length == 0)
        {
            throw new ArgumentException(
                $"The collision artifact '{path}' carries no bytes, so the place that declares it would be admitted as geometry nobody can walk on.",
                nameof(artifact));
        }

        Path = path;
        Artifact = artifact;
        Surfaces = surfaces ?? PlaceSurfaces.None;
    }

    /// <summary>The artifact's own path, which identifies it to the engine's content owner.</summary>
    public string Path { get; }

    /// <summary>The artifact document's bytes, exactly as content wrote them.</summary>
    public ReadOnlyMemory<byte> Artifact { get; }

    /// <summary>
    /// The place's named ground — water, road, whatever content names — over the same triangles the artifact collides
    /// with. It travels beside the artifact rather than in it, because the engine refuses a field its document does
    /// not define.
    /// </summary>
    public PlaceSurfaces Surfaces { get; }
}

/// <summary>Where a place's collision geometry comes from, when the loaded content carries any.</summary>
public interface IPlaceGeometrySource
{
    /// <summary>The place's collision geometry, or null when content provides none for it.</summary>
    /// <param name="place">The place the party is entering.</param>
    PlaceGeometry? For(PlaceId place);
}

/// <summary>
/// Reads a place's collision geometry out of the content the product loaded.
/// </summary>
/// <remarks>
/// <para>
/// The definition kind and the artifact property are supplied by whoever owns the content vocabulary, so
/// this reader stays general while the ruleset keeps naming its own documents. The bytes handed on are
/// the artifact document exactly as content wrote it — read out of the envelope it arrived in and never
/// re-serialized, so the engine parses the document the author wrote rather than a copy of it.
/// </para>
/// <para>
/// An entry that exists for a place but carries no artifact document stops the read instead of yielding
/// nothing: a place whose geometry silently went missing is a party walking through the floor, and that
/// is worse than a named failure at the moment the party tries to enter.
/// </para>
/// </remarks>
public sealed class ContentPlaceGeometry : IPlaceGeometrySource
{
    private readonly ContentCatalog _catalog;
    private readonly string _definitionKind;
    private readonly string _artifactProperty;
    private readonly string? _surfacesProperty;

    /// <summary>Creates the reader.</summary>
    /// <param name="catalog">The validated content the world was built from.</param>
    /// <param name="definitionKind">The definition kind whose entries carry places' collision artifacts.</param>
    /// <param name="artifactProperty">The property of such an entry that holds the engine's artifact document.</param>
    /// <param name="surfacesProperty">
    /// The property of such an entry that names the place's ground: a list of objects each with a <c>surface</c> name,
    /// <c>positions</c> as three-number arrays in the engine's axes, and <c>triangles</c> as three-index arrays. Null
    /// for content whose places name no ground; an entry without the property names none either.
    /// </param>
    /// <exception cref="ArgumentNullException">No content catalog was supplied.</exception>
    /// <exception cref="ArgumentException">A name is missing, so no entry could ever be found.</exception>
    public ContentPlaceGeometry(ContentCatalog catalog, string definitionKind, string artifactProperty, string? surfacesProperty = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactProperty);
        _catalog = catalog;
        _definitionKind = definitionKind;
        _artifactProperty = artifactProperty;
        _surfacesProperty = surfacesProperty;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The place's entry declares no artifact document, so the geometry content promised cannot be handed on.
    /// </exception>
    public PlaceGeometry? For(PlaceId place)
    {
        // An entry id is unique among the entries of its definition kind across the whole root — the
        // catalog refuses a second entry claiming one — so the first entry whose id is the place is the
        // only entry that carries this place's geometry.
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in _catalog.Entries(_definitionKind))
        {
            if (!string.Equals(entry.Id, place.Value, StringComparison.Ordinal)) continue;
            if (!entry.Payload.TryGetProperty(_artifactProperty, out JsonElement artifact) ||
                artifact.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Place '{place}' declares collision geometry in '{pack.PackId}/{document.DocumentId}', but its '{_artifactProperty}' is not the engine's artifact document, so the place's collision cannot be admitted.");
            }

            // The entry's own identity is the artifact's path, so the engine's content owner reports
            // which pack, document, and entry a retained artifact came from rather than an opaque handle.
            return new PlaceGeometry(
                $"{pack.PackId}/{document.DocumentId}/{entry.Id}.json",
                Encoding.UTF8.GetBytes(artifact.GetRawText()),
                Surfaces(place, pack, document, entry));
        }

        return null;
    }

    /// <summary>
    /// Reads a place's named ground, refusing a list that is not one rather than reading part of it: a place whose
    /// water silently went missing is a party that never drowns there.
    /// </summary>
    private PlaceSurfaces? Surfaces(PlaceId place, LoadedPack pack, ContentDocument document, ContentEntry entry)
    {
        if (_surfacesProperty is null || !entry.Payload.TryGetProperty(_surfacesProperty, out JsonElement list)) return null;
        string where = $"'{pack.PackId}/{document.DocumentId}'";
        if (list.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"Place '{place}' names its ground in {where}, but its '{_surfacesProperty}' is not a list.");
        }

        List<SurfaceMesh> meshes = [];
        foreach (JsonElement surface in list.EnumerateArray())
        {
            if (surface.ValueKind != JsonValueKind.Object ||
                !surface.TryGetProperty("surface", out JsonElement name) || name.ValueKind != JsonValueKind.String ||
                !surface.TryGetProperty("positions", out JsonElement positions) || positions.ValueKind != JsonValueKind.Array ||
                !surface.TryGetProperty("triangles", out JsonElement triangles) || triangles.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"Place '{place}' names its ground in {where}, and one entry of '{_surfacesProperty}' is not a surface name with positions and triangles.");
            }

            List<Vector3> corners = [];
            foreach (JsonElement corner in positions.EnumerateArray())
            {
                if (Triple(corner) is not { } xyz)
                {
                    throw new InvalidOperationException($"Place '{place}' names ground '{name.GetString()}' in {where} with a corner that is not three numbers.");
                }

                corners.Add(new Vector3((float)xyz.A, (float)xyz.B, (float)xyz.C));
            }

            List<int> indices = [];
            foreach (JsonElement triangle in triangles.EnumerateArray())
            {
                if (Triple(triangle) is not { } abc || abc.A % 1 != 0 || abc.B % 1 != 0 || abc.C % 1 != 0)
                {
                    throw new InvalidOperationException($"Place '{place}' names ground '{name.GetString()}' in {where} with a triangle that is not three corner indices.");
                }

                indices.Add((int)abc.A);
                indices.Add((int)abc.B);
                indices.Add((int)abc.C);
            }

            meshes.Add(new SurfaceMesh(name.GetString() ?? string.Empty, corners, indices));
        }

        try
        {
            return new PlaceSurfaces(meshes);
        }
        catch (ArgumentException refused)
        {
            throw new InvalidOperationException($"Place '{place}' names its ground in {where}, and it cannot be read: {refused.Message}", refused);
        }
    }

    /// <summary>Three numbers in an array, or null when the element is not exactly that.</summary>
    private static (double A, double B, double C)? Triple(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != 3) return null;
        double[] values = new double[3];
        int index = 0;
        foreach (JsonElement value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number) return null;
            values[index++] = value.GetDouble();
        }

        return (values[0], values[1], values[2]);
    }
}

/// <summary>What entering a place did to the movement's collision scene.</summary>
/// <remarks>
/// A place that provides no geometry is not an error and not a fallback: it is a place the party can
/// stand nowhere in, and saying so is what keeps the emptiness visible instead of implied.
/// </remarks>
/// <param name="Place">The place whose geometry was asked for.</param>
/// <param name="Admitted">Whether the engine admitted geometry for it.</param>
/// <param name="CollisionVertices">How many collision vertices the admitted artifact carried.</param>
/// <param name="CollisionTriangles">How many collision triangles the admitted artifact carried.</param>
/// <param name="NavigationCells">How many walkable navigation cells the admitted artifact carried.</param>
public sealed record PlaceGeometryAdmission(
    PlaceId Place,
    bool Admitted,
    ulong CollisionVertices,
    ulong CollisionTriangles,
    ulong NavigationCells)
{
    /// <summary>The scene holds nothing for this place: the party stands on nothing in it.</summary>
    /// <param name="place">The place whose geometry was asked for.</param>
    public static PlaceGeometryAdmission Empty(PlaceId place) => new(place, false, 0, 0, 0);
}

/// <summary>
/// The navigation policy a place's artifact cells are admitted under.
/// </summary>
/// <remarks>
/// The artifact states its own cell size and its cells; the grid identity, the chunking, and how far a
/// navigation step may climb are the product's policy, and they are stated here in the engine's own
/// terms rather than being worked out from the artifact.
/// </remarks>
/// <param name="GridId">The grid identity the artifact's cells are projected into.</param>
/// <param name="ChunkSize">How many cells a navigation chunk spans; the engine requires a cubic chunk.</param>
/// <param name="MaxStepCells">How many cells a navigation step may climb.</param>
/// <param name="SteeringStep">
/// How far ahead, in place units, a creature asks the navigation for its next walkable waypoint: the scale of a
/// doorway or a corridor bend in the game's places, not a claim about a stride.
/// </param>
/// <param name="SteeringBudget">
/// How many navigation cells one steering query may visit before it gives up and the creature walks straight at
/// its target.
/// </param>
public sealed record PlaceNavigationPolicy(ulong GridId, uint ChunkSize, uint MaxStepCells, float SteeringStep, uint SteeringBudget);
