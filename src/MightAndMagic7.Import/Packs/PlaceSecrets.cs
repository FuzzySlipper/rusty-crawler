using MightAndMagic7.Import.Maps;

namespace MightAndMagic7.Import.Packs;

/// <summary>Secret faces belonging to one imported target, in flattened source order.</summary>
public static class PlaceSecrets
{
    /// <summary>OpenEnroth src/Engine/Graphics/FaceEnums.h:9, FACE_IsSecret.</summary>
    public const uint SecretFaceAttribute = 0x00000002;

    /// <summary>Reads the source flag without interpreting whether a party can discover the surface.</summary>
    public static IReadOnlyList<int> Faces(DecodedMap map, IReadOnlyList<int> targetFaces)
    {
        HashSet<int> held = [.. targetFaces];
        return [.. MapFaceList.Flatten(map)
            .Where(entry => held.Contains(entry.FaceIndex) && (entry.Face.Attributes & SecretFaceAttribute) != 0)
            .Select(entry => entry.FaceIndex)];
    }
}
