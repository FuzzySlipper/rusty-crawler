using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Packs;

/// <summary>What kind of travel walking into an imported entrance is.</summary>
public enum PlaceEntranceKind
{
    /// <summary>Across the edge between two regions, which is the world's own ground.</summary>
    Walking,

    /// <summary>Through an entrance between a region and an interior, or between two interiors.</summary>
    Entrance,
}

/// <summary>
/// One reach a party can walk into to take a transition: where in the source place the trigger geometry
/// is, how far its reach extends, and which map face it came from.
/// </summary>
/// <remarks>
/// The position and the radius are both derived from one trigger face rather than authored: the position
/// is the face's own centroid and the radius is how far the face's own corners reach from it. That keeps
/// the whole reach a fact of the map data — it is the smallest ball holding the face the donor hangs the
/// event on — and it says where it came from, so a report can follow the reach back to a model and a face
/// index rather than to a number this importer chose.
/// </remarks>
/// <param name="LinkIndex">The travel link's own entry id in the pack, which the entrance takes.</param>
/// <param name="FromPlace">The place the trigger stands in, which is the place the link leaves.</param>
/// <param name="ToPlace">The place the link arrives at.</param>
/// <param name="Kind">What kind of travel walking into this entrance is.</param>
/// <param name="X">The trigger face's centroid along the place's first axis.</param>
/// <param name="Y">The trigger face's centroid along the place's second axis.</param>
/// <param name="Z">The trigger face's centroid in height.</param>
/// <param name="Radius">How far the trigger face's corners reach from its centroid.</param>
/// <param name="EventId">The event the face raises, which is the event the link's move belongs to.</param>
/// <param name="SourceFaceIndex">The face's index in the decoded map's own face list.</param>
/// <param name="SourceModelIndex">The owning model's index, or -1 for an interior face, which has none.</param>
/// <param name="SourceModelName">The owning model's name, empty for an interior face.</param>
/// <param name="Attributes">The face's attribute word, kept raw so a report can state what the donor does with it.</param>
public sealed record PlaceEntrancePlacement(
    int LinkIndex,
    int FromPlace,
    int ToPlace,
    PlaceEntranceKind Kind,
    double X,
    double Y,
    double Z,
    double Radius,
    int EventId,
    int SourceFaceIndex,
    int SourceModelIndex,
    string SourceModelName,
    uint Attributes)
{
    /// <summary>
    /// Whether the donor raises this event by the party stepping on the face
    /// (<c>FACE_PRESSURE_PLATE</c>, OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:973</c> and
    /// <c>Indoor.cpp:1491</c>).
    /// </summary>
    public bool IsPressurePlate => (Attributes & PlaceEntranceEmitter.PressurePlateAttribute) != 0;

    /// <summary>
    /// Whether the donor raises this event by the party clicking the face
    /// (<c>FACE_CLICKABLE</c>, OpenEnroth <c>src/Engine/Graphics/Indoor.cpp:1397-1416</c>).
    /// </summary>
    public bool IsClickable => (Attributes & PlaceEntranceEmitter.ClickableAttribute) != 0;
}

/// <summary>One travel link no walking party can take, with the reason it cannot.</summary>
/// <param name="LinkIndex">The travel link's own entry id in the pack.</param>
/// <param name="FromPlace">The place the link leaves, absent for a link the world issues.</param>
/// <param name="ToPlace">The place the link arrives at.</param>
/// <param name="EventId">The event the link's move belongs to.</param>
/// <param name="Step">The instruction's step inside that event.</param>
/// <param name="Code">A short stable code for the kind of untriggerable link.</param>
/// <param name="Reason">Why no walking party can take it, in terms a person can act on.</param>
public sealed record PlaceEntranceRefusal(
    int LinkIndex,
    int? FromPlace,
    int ToPlace,
    int EventId,
    int Step,
    string Code,
    string Reason);

/// <summary>What one import's entrance derivation produced, over every travel link it read.</summary>
/// <param name="Entrances">Every reach a party can walk into, in link and face order.</param>
/// <param name="Refusals">Every link no walking party can take, with its reason.</param>
public sealed record PlaceEntranceSummary(
    IReadOnlyList<PlaceEntrancePlacement> Entrances,
    IReadOnlyList<PlaceEntranceRefusal> Refusals)
{
    /// <summary>An import that derived no entrances at all, such as one that decoded no maps.</summary>
    public static PlaceEntranceSummary Empty { get; } = new([], []);

    /// <summary>How many places carry a reach a party can walk into.</summary>
    public int PlaceCount => Entrances.Select(entrance => entrance.FromPlace).Distinct().Count();

    /// <summary>How many travel links a walking party can take.</summary>
    public int LinkCount => Entrances.Select(entrance => entrance.LinkIndex).Distinct().Count();

    /// <summary>How many reaches were derived; one link can be reachable through several faces.</summary>
    public int ReachCount => Entrances.Count;

    /// <summary>How many reaches the donor raises by the party stepping on the face.</summary>
    public int PressurePlateCount => Entrances.Count(entrance => entrance.IsPressurePlate);

    /// <summary>How many reaches the donor raises by the party clicking the face.</summary>
    public int ClickableCount => Entrances.Count(entrance => entrance.IsClickable);

    /// <summary>How many travel links nothing about walking can take.</summary>
    public int UntriggerableCount => Refusals.Count;
}

/// <summary>
/// Derives the reaches a walking party can take a place's transitions through, from the event faces the
/// decoded maps already carry.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where the trigger comes from.</b> A map hands an event to a face: the donor fires the event when
/// the party steps on a face that carries one as a pressure plate, and when the party clicks a face that
/// carries one as clickable (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:966-980</c>,
/// <c>src/Engine/Graphics/Indoor.cpp:1397-1416,1491</c>). A travel link comes from an event program's
/// map move, and the program is the map's own, so a face in the source place whose event is the link's
/// event is the source-side trigger the data holds — there is no other. Nothing here reads a
/// destination: a link's position says where the party arrives, and a party standing at an arrival point
/// is not standing at an entrance.
/// </para>
/// <para>
/// <b>What this adapts.</b> The donor takes these events by stepping on them or clicking them; the
/// product takes them by walking into them, so the reach is the smallest ball holding the trigger face —
/// its centroid and its own extent — and the product fires when a step carries the party from outside
/// that ball to inside it. The adaptation is recorded here rather than hidden: the pack states the face
/// the reach came from, and the trigger word, so what the donor does with the face stays readable.
/// </para>
/// <para>
/// <b>What cannot be taken by walking.</b> Three kinds of link get no reach, and each is recorded per
/// link rather than rounded to a position: a link the world itself issues, which no place can be walked
/// into; a link whose event's first move is a different link, which an event's later steps reach only
/// after its earlier ones have run, so nothing about walking selects it; and a link whose source map
/// carries no face for its event at all. The last two leave the requirement with the event interpreter
/// that does not exist yet, and the summary names every one of them.
/// </para>
/// </remarks>
public static class PlaceEntranceEmitter
{
    /// <summary>The donor's attribute for an event the party triggers by stepping on the face.</summary>
    public const uint PressurePlateAttribute = 0x04000000;

    /// <summary>The donor's attribute for an event the party triggers by clicking the face.</summary>
    public const uint ClickableAttribute = 0x02000000;

    /// <summary>Derives every reach, and records every link that has none.</summary>
    /// <param name="graph">The links the places were read from.</param>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <exception cref="ArgumentNullException">The graph or the map lookup is null.</exception>
    public static PlaceEntranceSummary Emit(PlaceGraph graph, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(maps);

        IReadOnlyList<PlaceLink> links = graph.Links;
        Dictionary<(int Place, int EventId), int> firstMove = FirstMoves(links);
        List<PlaceEntrancePlacement> entrances = [];
        List<PlaceEntranceRefusal> refusals = [];
        for (int index = 0; index < links.Count; index++)
        {
            PlaceLink link = links[index];
            if (link.SourceMapId is not int from)
            {
                refusals.Add(Refuse(
                    index,
                    link,
                    "world-issued",
                    "The world issues this move from no map, so it is an arrival with no place a party could walk into it from."));
                continue;
            }

            if (firstMove[(from, link.EventId)] != index)
            {
                int first = firstMove[(from, link.EventId)];
                refusals.Add(Refuse(
                    index,
                    link,
                    "conditional-step",
                    $"Event {link.EventId} moves the party from link {first} (step {links[first].Step}) first, and this instruction is step {link.Step}: an event's later steps run only after its earlier ones, so walking into the event's face takes the first move and nothing selects this one."));
                continue;
            }

            if (!maps.TryGetValue(from, out DecodedMap? map) || !maps.TryGetValue(link.DestinationMapId!.Value, out DecodedMap? destination))
            {
                refusals.Add(Refuse(
                    index,
                    link,
                    "map-not-decoded",
                    $"Place {from} or {link.DestinationMapId} was not decoded by this import, so the faces that raise event {link.EventId} were never read."));
                continue;
            }

            PlaceEntranceKind kind = map.Kind == MapKind.Outdoor && destination.Kind == MapKind.Outdoor
                ? PlaceEntranceKind.Walking
                : PlaceEntranceKind.Entrance;

            int found = 0;
            foreach ((int faceIndex, MapFace face, int modelIndex, string modelName) in MapFaceList.Flatten(map))
            {
                if (face.EventId != link.EventId) continue;
                found++;
                entrances.Add(Place(
                    index,
                    link,
                    kind,
                    faceIndex,
                    face,
                    modelIndex,
                    modelName));
            }

            if (found == 0)
            {
                refusals.Add(Refuse(
                    index,
                    link,
                    "no-event-face",
                    $"Place {from} carries no face raising event {link.EventId}, so nothing in the place tells the party it is standing in this transition; whatever raised the event in the original game is not in the map data."));
            }
        }

        return new PlaceEntranceSummary(entrances, refusals);
    }

    /// <summary>
    /// The first move of every event a place raises, which is the one walking into its face takes.
    /// </summary>
    /// <remarks>
    /// An event's steps run in order, so the instruction with the lowest step is the one the event
    /// reaches first; where two instructions share the lowest step the earlier link wins, which is the
    /// order the program stores them in.
    /// </remarks>
    private static Dictionary<(int Place, int EventId), int> FirstMoves(IReadOnlyList<PlaceLink> links)
    {
        Dictionary<(int, int), int> first = [];
        for (int index = 0; index < links.Count; index++)
        {
            PlaceLink link = links[index];
            if (link.SourceMapId is not int from) continue;
            (int, int) key = (from, link.EventId);
            if (!first.TryGetValue(key, out int existing) || link.Step < links[existing].Step) first[key] = index;
        }

        return first;
    }

    /// <summary>Derives one reach from one trigger face.</summary>
    private static PlaceEntrancePlacement Place(
        int linkIndex,
        PlaceLink link,
        PlaceEntranceKind kind,
        int faceIndex,
        MapFace face,
        int modelIndex,
        string modelName)
    {
        // Integer division is avoided: the centroid is the mean of the face's corners, and the radius is
        // the farthest corner from it, so both are the face's own geometry rather than a rounding of it.
        double x = 0;
        double y = 0;
        double z = 0;
        foreach (MapPoint vertex in face.Vertices)
        {
            x += vertex.X;
            y += vertex.Y;
            z += vertex.Z;
        }

        x /= face.Vertices.Count;
        y /= face.Vertices.Count;
        z /= face.Vertices.Count;

        double radius = 0;
        foreach (MapPoint vertex in face.Vertices)
        {
            double dx = vertex.X - x;
            double dy = vertex.Y - y;
            double dz = vertex.Z - z;
            double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (distance > radius) radius = distance;
        }

        return new PlaceEntrancePlacement(
            linkIndex,
            link.SourceMapId!.Value,
            link.DestinationMapId!.Value,
            kind,
            x,
            y,
            z,
            radius,
            face.EventId,
            faceIndex,
            modelIndex,
            modelName,
            face.Attributes);
    }

    private static PlaceEntranceRefusal Refuse(int linkIndex, PlaceLink link, string code, string reason) =>
        new(linkIndex, link.SourceMapId, link.DestinationMapId!.Value, link.EventId, link.Step, code, reason);
}
