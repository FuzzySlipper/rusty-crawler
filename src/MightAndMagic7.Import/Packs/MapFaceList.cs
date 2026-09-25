using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Packs;

/// <summary>
/// Every face of a decoded map, with its index in the map's own flattened face list and the model it
/// belongs to.
/// </summary>
/// <remarks>
/// An interior's faces are the level's face array, so the index is the face's own. An outdoor map's faces
/// are its models' face arrays concatenated, so the index is the running one and the owner is the model
/// the face was read from. Two emitters need this same flattening — the reaches a party walks into a
/// transition by, and the faces a container is placed by — so it is written once, because two answers to
/// "which face is this" would be the first thing to disagree about where a derived position came from.
/// </remarks>
internal static class MapFaceList
{
    internal static IEnumerable<(int FaceIndex, MapFace Face, int ModelIndex, string ModelName)> Flatten(DecodedMap map)
    {
        if (map is not OutdoorMap outdoor)
        {
            foreach (MapFace face in map.Faces) yield return (face.Index, face, -1, string.Empty);
            yield break;
        }

        int index = 0;
        foreach (OutdoorModel model in outdoor.Models)
        {
            foreach (MapFace face in model.Faces)
            {
                yield return (index, face, model.Index, model.Name);
                index++;
            }
        }
    }

    /// <summary>The middle of a face's own bounding box, which is where the donor hangs an event's effect.</summary>
    /// <remarks>
    /// The donor takes a face's bounding-box centre when it needs a point on the face
    /// (OpenEnroth <c>src/Engine/Objects/Chest.cpp:396-402</c>, the face and model branches, and
    /// <c>src/Engine/Graphics/Outdoor.cpp:1768</c> for a model face), so the same point is used here rather
    /// than the mean of the corners: a chest's position is then the donor's own reading of the geometry.
    /// </remarks>
    internal static (double X, double Y, double Z) BoxCentre(MapFace face)
    {
        double minimumX = double.MaxValue;
        double minimumY = double.MaxValue;
        double minimumZ = double.MaxValue;
        double maximumX = double.MinValue;
        double maximumY = double.MinValue;
        double maximumZ = double.MinValue;
        foreach (MapPoint vertex in face.Vertices)
        {
            minimumX = Math.Min(minimumX, vertex.X);
            minimumY = Math.Min(minimumY, vertex.Y);
            minimumZ = Math.Min(minimumZ, vertex.Z);
            maximumX = Math.Max(maximumX, vertex.X);
            maximumY = Math.Max(maximumY, vertex.Y);
            maximumZ = Math.Max(maximumZ, vertex.Z);
        }

        return ((minimumX + maximumX) / 2, (minimumY + maximumY) / 2, (minimumZ + maximumZ) / 2);
    }
}
