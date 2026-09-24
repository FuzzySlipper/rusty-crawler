using System.Text.Json;
using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// What a placement is, in content's terms: the kind of thing it is and which one of that kind it is.
/// </summary>
/// <remarks>
/// This is content identity, not runtime identity. Two visits to one place create entities with the
/// same content identity and different runtime ones, so this is the half of an entity's identity that
/// could ever be written down; the other half belongs to the visit that made it.
/// </remarks>
/// <param name="Kind">The kind of thing placed, as content names it.</param>
/// <param name="Id">Which one of that kind it is, unique among its place's placements.</param>
public readonly record struct PlacementContentId(string Kind, string Id)
{
    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Id}";
}

/// <summary>
/// One thing content places in a place: its identity, where it stands, and the entry it came from.
/// </summary>
/// <remarks>
/// The raw entry is kept so a ruleset can read the fields this layer has no opinion about — what a
/// placement's kind means, how it behaves, what it carries — without the kit growing a vocabulary for
/// them. The kit reads identity, position, and provenance; everything else stays content.
/// </remarks>
/// <param name="Content">The placement's identity in content.</param>
/// <param name="SourceField">
/// The source field the placement was read from, when content records one, so a rule can follow it back
/// to the data that produced it. Empty when content records none.
/// </param>
/// <param name="SourceIndex">The index within that source field, or null when content records none.</param>
/// <param name="Pose">Where the placement stands in its place.</param>
/// <param name="Source">The content entry the placement was read from.</param>
public sealed record PlacementDefinition(
    PlacementContentId Content,
    string SourceField,
    int? SourceIndex,
    PlacePose Pose,
    ContentEntry Source);

/// <summary>
/// The placements every place declares, read from the places a world is built from.
/// </summary>
/// <remarks>
/// <para>
/// A place's entry carries its placements, which is why this reads places rather than a document of its
/// own: a placement has no meaning apart from the place it stands in, and splitting the two would make
/// every reader join them again.
/// </para>
/// <para>
/// Reading is all-or-nothing. A placement without an identity, or two placements of one place claiming
/// the same one, is a content defect that fails while the world is being built rather than a population
/// that quietly comes up short. Every defective placement is reported, not only the first.
/// </para>
/// </remarks>
public sealed class PlacePopulationContent
{
    /// <summary>The place-content field that carries a place's placements.</summary>
    public const string PlacementsField = "placements";

    /// <summary>The placement field that names what kind of thing it is.</summary>
    public const string KindField = "kind";

    /// <summary>The placement field that names which one of that kind it is.</summary>
    public const string IdField = "id";

    private readonly PlaceGraph _places;
    private readonly Dictionary<PlaceId, IReadOnlyList<PlacementDefinition>> _placements;

    private PlacePopulationContent(PlaceGraph places, Dictionary<PlaceId, IReadOnlyList<PlacementDefinition>> placements)
    {
        _places = places;
        _placements = placements;
    }

    /// <summary>Reads every place's placements, failing with every problem found.</summary>
    /// <param name="places">The world whose places carry the placements.</param>
    public static PlacePopulationContent Read(PlaceGraph places)
    {
        ArgumentNullException.ThrowIfNull(places);
        List<ContentValidationIssue> issues = [];
        Dictionary<PlaceId, IReadOnlyList<PlacementDefinition>> byPlace = [];
        foreach (PlaceDefinition place in places.Places)
        {
            byPlace[place.Id] = ReadPlace(place, issues);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world's placements cannot be read: {issues[0].Message}",
                issues);
        }

        return new PlacePopulationContent(places, byPlace);
    }

    /// <summary>The placements of a place, in the order content declared them.</summary>
    /// <remarks>
    /// An id the world does not have fails by name rather than answering with an empty list: a mistyped
    /// place would otherwise populate nobody, which looks exactly like a place content left empty.
    /// </remarks>
    /// <param name="place">The place whose placements to read.</param>
    public IReadOnlyList<PlacementDefinition> PlacementsOf(PlaceId place)
    {
        _places.Require(place);
        return _placements.TryGetValue(place, out IReadOnlyList<PlacementDefinition>? placements) ? placements : [];
    }

    private static IReadOnlyList<PlacementDefinition> ReadPlace(PlaceDefinition place, List<ContentValidationIssue> issues)
    {
        List<PlacementDefinition> placements = [];
        HashSet<PlacementContentId> seen = [];
        foreach (JsonElement element in place.Source.GetArray(PlacementsField))
        {
            string kind = ContentEntry.ReadString(element, KindField);
            string id = ContentEntry.ReadId(element, IdField);
            if (kind.Length == 0 || id.Length == 0)
            {
                issues.Add(new ContentValidationIssue(
                    "placement-identity-missing",
                    $"place '{place.Id}' declares a placement without a '{KindField}' or without an '{IdField}', so there is nothing for a runtime entity to be created from.",
                    place.Id.Value));
                continue;
            }

            PlacementContentId content = new(kind, id);
            if (!seen.Add(content))
            {
                issues.Add(new ContentValidationIssue(
                    "placement-identity-reused",
                    $"place '{place.Id}' declares the placement '{content}' more than once.",
                    place.Id.Value));
                continue;
            }

            placements.Add(new PlacementDefinition(
                content,
                ContentEntry.ReadString(element, "sourceField"),
                ReadSourceIndex(element),
                new PlacePose(
                    ContentEntry.ReadDouble(element, "x") ?? 0,
                    ContentEntry.ReadDouble(element, "y") ?? 0,
                    ContentEntry.ReadDouble(element, "z") ?? 0,
                    ContentEntry.ReadDouble(element, "yaw") ?? 0,
                    ContentEntry.ReadDouble(element, "pitch") ?? 0),
                new ContentEntry(id, element)));
        }

        return placements;
    }

    /// <summary>Reads where in its source field a placement came from, or null when content records no index.</summary>
    /// <remarks>
    /// An index is either one or nothing: a fractional or out-of-range value is not an index, and
    /// rounding it would name a source entry the placement did not come from.
    /// </remarks>
    private static int? ReadSourceIndex(JsonElement element)
    {
        if (ContentEntry.ReadDouble(element, "sourceIndex") is not { } value) return null;
        return value >= int.MinValue && value <= int.MaxValue && Math.Truncate(value) == value ? (int)value : null;
    }
}
