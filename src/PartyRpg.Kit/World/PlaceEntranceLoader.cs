using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// Reads the entrances a place issues out of the content the product loaded: for every entry, the
/// transition it takes, where in the place its reach is, and how big that reach is.
/// </summary>
/// <remarks>
/// <para>
/// The definition kind is named here as a string because that is what content declares, exactly as the
/// place graph loader does for places and transitions. What this reads is the half of an entrance that
/// belongs to the world — which transition, which place, and where the party has to walk — and what it
/// leaves in the entry is the provenance of that reach, which a report or a person can follow back to
/// the map data that produced it.
/// </para>
/// <para>
/// Reading is all-or-nothing, like the graph's own load: an entrance naming a transition no pack
/// declares, one standing in a place the transition does not leave, one whose reach is missing or not
/// a number, or one whose travel is not a walk-in is a content defect, and every defect is reported
/// rather than the first. An entrance the world silently dropped would be a door that only works
/// sometimes, which is far harder to diagnose than a load that refuses.
/// </para>
/// <para>
/// Several entrances may take one transition, and that is not a duplicate: a doorway is often more than one
/// face, each its own reach onto the same crossing. Nothing finds an entrance by its transition or by its
/// own id — a step finds the one it walked into by where it is — and an entrance's own id is unique among
/// entrances because the catalog refuses two entries of one kind sharing one.
/// </para>
/// </remarks>
public static class PlaceEntranceLoader
{
    /// <summary>The definition kind that carries the transitions a party can walk into.</summary>
    public const string DefinitionKind = "place-entrance";

    /// <summary>The field naming the transition an entrance takes.</summary>
    public const string TransitionField = "link";

    /// <summary>The field naming the place the entrance stands in.</summary>
    public const string PlaceField = "fromPlace";

    /// <summary>The field naming what kind of travel the walk-in is.</summary>
    public const string KindField = "kind";

    /// <summary>The field naming the placement an entrance that raises a use raises, by its id.</summary>
    public const string RaisesField = "raises";

    /// <summary>The field naming the kind of the placement an entrance raises.</summary>
    public const string RaisesKindField = "raisesKind";

    /// <summary>Loads every entrance, failing with every problem found rather than the first.</summary>
    /// <param name="catalog">The validated content catalog to read.</param>
    /// <param name="graph">The graph the entrances' transitions must belong to.</param>
    /// <exception cref="ArgumentNullException">The catalog or the graph is null.</exception>
    /// <exception cref="ContentValidationException">An entrance cannot be read, or names something the world does not hold.</exception>
    public static IReadOnlyList<PlaceEntrance> Load(ContentCatalog catalog, PlaceGraph graph)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(graph);

        Dictionary<string, PlaceTransition> transitions = [];
        foreach (PlaceTransition transition in graph.Transitions)
        {
            // A loaded graph holds each transition id once: two authored links sharing one are refused by the
            // catalog (entry-id-reused, per kind), and a fare-network crossing under an id another transition
            // holds by the graph's load (transition-id-reused). A graph a caller assembled from parts is not
            // trusted to have done the same, so a second transition under an id is refused here by name too.
            if (!transitions.TryAdd(transition.Source, transition))
            {
                throw new ContentValidationException(
                    $"The world's entrances cannot be read: transition '{transition.Source}' is held by the world more than once, so an entrance naming it could take either.",
                    [new ContentValidationIssue(
                        "transition-id-reused",
                        $"transition '{transition.Source}' is held by the world more than once, so an entrance naming it could take either.",
                        transition.Source)]);
            }
        }

        List<ContentValidationIssue> issues = [];
        List<PlaceEntrance> entrances = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DefinitionKind))
        {
            PlaceEntrance? entrance = Read(pack, document, entry, graph, transitions, issues);
            if (entrance is not null) entrances.Add(entrance);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world's entrances cannot be read: {issues[0].Message}",
                issues);
        }

        return entrances;
    }

    private static PlaceEntrance? Read(
        LoadedPack pack,
        ContentDocument document,
        ContentEntry entry,
        PlaceGraph graph,
        Dictionary<string, PlaceTransition> transitions,
        List<ContentValidationIssue> issues)
    {
        void Defect(string code, string message) =>
            issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

        string transitionId = entry.GetId(TransitionField);
        string raises = entry.GetId(RaisesField);
        if (transitionId.Length > 0 && raises.Length > 0)
        {
            Defect(
                "entrance-form-ambiguous",
                $"entrance '{entry.Id}' names both a transition and a placement to raise, so whether walking into it travels or sets the placement off cannot be told.");
            return null;
        }

        if (raises.Length > 0) return ReadRaising(entry, graph, raises, Defect);

        if (transitionId.Length == 0)
        {
            Defect(
                "entrance-transition-missing",
                $"entrance '{entry.Id}' names no transition and no placement to raise, so there is nothing for walking into it to do.");
            return null;
        }

        if (!transitions.TryGetValue(transitionId, out PlaceTransition? transition))
        {
            Defect(
                "entrance-transition-unknown",
                $"entrance '{entry.Id}' takes transition '{transitionId}', which no pack declares.");
            return null;
        }

        if (ReadKind(entry, Defect) is not { } kind) return null;

        // The place is checked against the transition rather than trusted: an entrance indexed under one
        // place while its transition leaves another would be a door in the wrong wall, and the party
        // would walk into it in a place that does not issue the transition at all.
        if (!string.Equals(entry.GetId(PlaceField), transition.From?.Value, StringComparison.Ordinal))
        {
            Defect(
                "entrance-place-mismatch",
                $"entrance '{entry.Id}' stands in place '{entry.GetId(PlaceField)}' but its transition '{transitionId}' leaves place '{transition.From}'.");
            return null;
        }

        if (entry.GetString("shape") == "boundary")
        {
            string axis = entry.GetString("axis"), side = entry.GetString("side");
            if (kind != TransitionKind.Walking || transition.IsFare || transition.From is null ||
                axis is not ("x" or "y") || side is not ("above" or "below") ||
                entry.GetDouble("coordinate") is not { } coordinate || !double.IsFinite(coordinate))
            {
                Defect("entrance-boundary-invalid", $"Boundary '{entry.Id}' needs walking travel, axis x/y, side above/below and a finite coordinate.");
                return null;
            }
            return new PlaceEntrance(transition, new PlaceBoundary(axis == "x", coordinate, side == "above"), entry.Id);
        }

        if (entry.GetString("shape") is not ("" or "sphere"))
        {
            Defect("entrance-shape-unknown", $"Entrance '{entry.Id}' has unknown shape '{entry.GetString("shape")}'.");
            return null;
        }

        if (entry.GetDouble("x") is not { } x || entry.GetDouble("y") is not { } y || entry.GetDouble("z") is not { } z)
        {
            Defect(
                "entrance-reach-missing",
                $"entrance '{entry.Id}' does not state a position for its reach, so no step could be inside it.");
            return null;
        }

        if (entry.GetDouble("radius") is not { } radius || !double.IsFinite(radius) || radius <= 0)
        {
            Defect(
                "entrance-reach-invalid",
                $"entrance '{entry.Id}' does not state a positive, finite radius for its reach, so an entrance nobody can be inside is not one.");
            return null;
        }

        if (transition.From is not { } place) return null;
        return new PlaceEntrance(transition, kind, x, y, z, radius, entry.Id);
    }

    /// <summary>Reads an entrance whose step raises the use of a placement of its place.</summary>
    /// <remarks>
    /// The place must be one the world holds; whether the place holds the placement is the world's to judge when
    /// it is built over the place's population, because placements are read with the places rather than here.
    /// </remarks>
    private static PlaceEntrance? ReadRaising(ContentEntry entry, PlaceGraph graph, string raises, Action<string, string> defect)
    {
        string place = entry.GetId(PlaceField);
        if (place.Length == 0 || graph.Find(new PlaceId(place)) is null)
        {
            defect(
                "entrance-place-unknown",
                $"entrance '{entry.Id}' raises '{raises}' in place '{place}', which the world does not hold.");
            return null;
        }

        string kind = entry.GetString(RaisesKindField);
        if (kind.Length == 0)
        {
            defect(
                "entrance-raises-kind-missing",
                $"entrance '{entry.Id}' raises '{raises}' without naming the kind of placement it is, so which placement of the place it means cannot be told.");
            return null;
        }

        if (entry.GetDouble("x") is not { } x || entry.GetDouble("y") is not { } y || entry.GetDouble("z") is not { } z)
        {
            defect(
                "entrance-reach-missing",
                $"entrance '{entry.Id}' does not state a position for its reach, so no step could be inside it.");
            return null;
        }

        if (entry.GetDouble("radius") is not { } radius || !double.IsFinite(radius) || radius <= 0)
        {
            defect(
                "entrance-reach-invalid",
                $"entrance '{entry.Id}' does not state a positive, finite radius for its reach, so an entrance nobody can be inside is not one.");
            return null;
        }

        return new PlaceEntrance(new PlaceId(place), new PlacementContentId(kind, raises), x, y, z, radius, entry.Id);
    }

    /// <summary>Reads what kind of travel a walk-in takes, naming the two kinds walking can be.</summary>
    private static TransitionKind? ReadKind(ContentEntry entry, Action<string, string> defect)
    {
        string kind = entry.GetString(KindField);
        if (string.Equals(kind, "walking", StringComparison.OrdinalIgnoreCase)) return TransitionKind.Walking;
        if (string.Equals(kind, "entrance", StringComparison.OrdinalIgnoreCase)) return TransitionKind.Entrance;

        defect(
            "entrance-kind-unknown",
            $"entrance '{entry.Id}' declares travel '{kind}', which is not walking across an edge ('walking') or passing through an entrance ('entrance'); a fare or a portal cannot be entered on foot.");
        return null;
    }
}
