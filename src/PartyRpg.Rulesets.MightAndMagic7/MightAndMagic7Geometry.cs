using System.Globalization;
using System.Numerics;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Projects the canonical interaction ledger onto the importer's authored collision partition.</summary>
internal sealed class MightAndMagic7Geometry : IPlaceGeometrySource
{
    internal const string LayoutProperty = "collisionLayout";
    internal const string PassablePrefix = "face-passable:";
    private readonly InteractionLedger _ledger;
    private readonly Dictionary<PlaceId, Authored> _places = [];

    internal MightAndMagic7Geometry(ContentCatalog catalog, PlaceGraph graph, InteractionLedger ledger)
    {
        _ledger = ledger;
        ContentPlaceGeometry artifacts = new(catalog, MightAndMagic7World.GeometryDefinitionKind,
            MightAndMagic7World.GeometryArtifactProperty, MightAndMagic7World.GeometrySurfacesProperty,
            MightAndMagic7World.GeometryNavigationProperty);
        PlacePopulationContent placements = PlacePopulationContent.Read(graph);
        foreach (var (_, _, entry) in catalog.Entries(MightAndMagic7World.GeometryDefinitionKind))
        {
            PlaceId place = new(entry.Id);
            PlaceGeometry geometry = artifacts.For(place)!;
            PlacementDefinition[] doors = [.. placements.PlacementsOf(place).Where(placement => placement.Content.Kind == MightAndMagic7Interaction.DoorPlacementKind)];
            if (!entry.Payload.TryGetProperty(LayoutProperty, out JsonElement layout))
            {
                if (doors.Length > 0) throw new InvalidOperationException($"Place '{place}' has doors but no authored collision layout; rewrite its imported pack before walking it.");
                _places.Add(place, new Authored(geometry, [], [], [], []));
                continue;
            }
            Vector3[] positions = Vectors(layout.GetProperty("positions"));
            Triangle[] triangles = Triangles(layout.GetProperty("triangles"), positions.Length);
            Face[] faces = [.. layout.GetProperty("faces").EnumerateArray().Select(face =>
            {
                Corner[] corners = [.. face.GetProperty("corners").EnumerateArray().Select(corner => new Corner(
                    Vector(corner.GetProperty("rest")), corner.TryGetProperty("door", out var door) ? door.GetString() : null,
                    Vector(corner.GetProperty("travel"))))];
                return new Face(face.GetProperty("group").GetInt32(), face.GetProperty("passable").GetBoolean(), corners,
                    Triangles(face.GetProperty("triangles"), corners.Length));
            })];
            HashSet<string> known = [.. doors.Select(door => door.Content.Id)];
            foreach (Corner corner in faces.SelectMany(face => face.Corners))
                if (corner.Door is { } door && !known.Contains(door))
                    throw new InvalidOperationException($"Place '{place}' collision names absent door '{door}'.");
            foreach (PlacementDefinition door in doors)
                if (!faces.Any(face => face.Corners.Any(corner => corner.Door == door.Content.Id)))
                    throw new InvalidOperationException($"Place '{place}' door '{door.Content.Id}' has no collision corners to move.");
            _places.Add(place, new Authored(geometry, positions, triangles, faces, doors));
        }
    }

    public PlaceGeometry? For(PlaceId place)
    {
        if (!_places.TryGetValue(place, out Authored? authored)) return null;
        if (authored.Faces.Length == 0) return authored.Geometry;
        Dictionary<string, bool> open = authored.Doors.ToDictionary(door => door.Content.Id,
            door => MightAndMagic7Interaction.DoorState(door, _ledger.StateOf(place, door.Content).State) == MightAndMagic7Interaction.OpenState);
        var values = _ledger.ValuesOf(place);
        bool[] passable = [.. authored.Faces.Select(face => face.Group != 0 && values.TryGetValue(PassableKey(face.Group), out long value) ? value != 0 : face.Passable)];
        string key = string.Join(',', open.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value)) + "|" + string.Join(',', passable);
        if (authored.Projected is { } cached && key == authored.Key) return cached;
        List<Vector3> positions = [.. authored.Positions];
        List<Triangle> triangles = [.. authored.Triangles];
        for (int index = 0; index < authored.Faces.Length; index++)
        {
            Face face = authored.Faces[index];
            if (passable[index]) continue;
            uint start = checked((uint)positions.Count);
            foreach (Corner corner in face.Corners)
                positions.Add(corner.Rest + (corner.Door is { } door && open[door] ? corner.Travel : Vector3.Zero));
            foreach (Triangle triangle in face.Triangles)
                triangles.Add(new Triangle(start + triangle.A, start + triangle.B, start + triangle.C));
        }
        authored.Key = key;
        authored.Projected = authored.Geometry with { Collision = new PlaceCollisionGeometry(positions.ToArray(), triangles.ToArray()) };
        return authored.Projected;
    }

    internal static string PassableKey(int group) => PassablePrefix + group.ToString(CultureInfo.InvariantCulture);

    private static Vector3 Vector(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 3)
            throw new InvalidOperationException("An authored collision corner must have three coordinates.");
        float[] xyz = [.. value.EnumerateArray().Select(coordinate => coordinate.GetSingle())];
        if (xyz.Any(coordinate => !float.IsFinite(coordinate))) throw new InvalidOperationException("An authored collision corner must be finite.");
        return new Vector3(xyz[0], xyz[1], xyz[2]);
    }
    private static Vector3[] Vectors(JsonElement value) => [.. value.EnumerateArray().Select(Vector)];
    private static Triangle[] Triangles(JsonElement value, int count) => [.. value.EnumerateArray().Select(row =>
    {
        if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 3) throw new InvalidOperationException("An authored triangle needs three corner indices.");
        uint[] indices = [.. row.EnumerateArray().Select(index => index.GetUInt32())];
        if (indices.Any(index => index >= count)) throw new InvalidOperationException("An authored collision triangle names an absent corner.");
        return new Triangle(indices[0], indices[1], indices[2]);
    })];
    private sealed record Corner(Vector3 Rest, string? Door, Vector3 Travel);
    private sealed record Face(int Group, bool Passable, Corner[] Corners, Triangle[] Triangles);
    private sealed record Authored(PlaceGeometry Geometry, Vector3[] Positions, Triangle[] Triangles, Face[] Faces, PlacementDefinition[] Doors)
    {
        internal string? Key;
        internal PlaceGeometry? Projected;
    }
}
