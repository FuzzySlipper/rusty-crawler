using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;

namespace MightAndMagic7.Import.Collision;

/// <summary>A whole place's authored collision, separating mutable faces from its static ground.</summary>
/// <param name="Static">Every solid triangle not controlled by a door or a face group.</param>
/// <param name="Faces">Door-controlled and event-addressable faces, including initially passable ones.</param>
public sealed record PlaceCollisionLayout(CollisionMesh Static, IReadOnlyList<CollisionFace> Faces)
{
    /// <summary>Builds the partition offline from decoded source geometry.</summary>
    public static PlaceCollisionLayout From(DecodedMap map)
    {
        Dictionary<int, CollisionCorner> moved = DoorCorners(map);
        CollisionMesh ground = new();
        List<CollisionFace> faces = [];
        if (map is OutdoorMap outdoor)
            PlaceCollisionEmitter.AddTerrain(ground, [], outdoor, null, new CollisionMesh());
        foreach (var (faceIndex, face, _, _) in MapFaceList.Flatten(map))
        {
            // Portals are never collision, even when an event addresses their group.
            if (face.BackSectorId > 0 || (face.Attributes & 1) != 0) continue;
            bool moves = map is IndoorMap && face.VertexIds.Any(moved.ContainsKey);
            if (!moves && face.CogNumber == 0 && face.EventId == 0)
            {
                if (PlaceCollisionEmitter.IsSolid(face)) ground.AddPolygon(face.Vertices);
                continue;
            }
            List<CollisionCorner> corners = [];
            for (int index = 0; index < face.Vertices.Count; index++)
            {
                MapPoint point = face.Vertices[index];
                corners.Add(map is IndoorMap && moved.TryGetValue(face.VertexIds[index], out CollisionCorner? bound)
                    ? bound : new CollisionCorner([point.X, point.Z, -point.Y], null, [0, 0, 0]));
            }
            // Preserve the whole authored fan: a triangle collapsed at rest may gain area after a corner moves.
            List<int[]> triangles = [];
            for (int index = 2; index < corners.Count; index++) triangles.Add([0, index - 1, index]);
            if (triangles.Count > 0)
                faces.Add(new CollisionFace(face.CogNumber, (face.Attributes & 0x20000000) != 0, corners, triangles, face.EventId, faceIndex));
        }
        return new PlaceCollisionLayout(ground, faces);
    }

    /// <summary>
    /// Every vertex a level's doors move, by the level's vertex id: its rest position and its door's full travel, both
    /// in Engine axes. The one reading of a door's corners, which the collision and the render geometry both take.
    /// </summary>
    public static Dictionary<int, CollisionCorner> DoorCorners(DecodedMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Dictionary<int, CollisionCorner> moved = [];
        if (map is IndoorMap indoor)
        {
            foreach (MapDoor door in indoor.Doors.Where(door => door.InUse))
            {
                if (door.VertexIds.Count != door.XOffsets.Count || door.VertexIds.Count != door.YOffsets.Count || door.VertexIds.Count != door.ZOffsets.Count)
                    throw new InvalidOperationException($"Door {door.Index} has {door.VertexIds.Count} moved vertices but incomplete rest coordinates; its collision cannot be reconstructed.");
                // Source direction is signed 16.16. Normalize it here, not in the runtime kit.
                double[] travel = [door.Direction.X * door.MoveLength / 65536d,
                    door.Direction.Z * door.MoveLength / 65536d, -door.Direction.Y * door.MoveLength / 65536d];
                for (int index = 0; index < door.VertexIds.Count; index++)
                {
                    double[] rest = [door.XOffsets[index], door.ZOffsets[index], -door.YOffsets[index]];
                    if (!moved.TryAdd(door.VertexIds[index], new CollisionCorner(rest, door.Index, travel)))
                        throw new InvalidOperationException($"Door {door.Index} shares moved vertex {door.VertexIds[index]} with another door; their travel cannot be assigned silently.");
                }
            }
        }

        return moved;
    }
}

/// <summary>One face's immutable fan and the corners whose positions its doors control.</summary>
public sealed record CollisionFace(int Group, bool Passable, IReadOnlyList<CollisionCorner> Corners, IReadOnlyList<int[]> Triangles, int Event = 0, int FaceIndex = -1);

/// <summary>An authored corner, with its door's full travel in Engine axes when a door owns it.</summary>
public sealed record CollisionCorner(double[] Rest, int? Door, double[] Travel);
