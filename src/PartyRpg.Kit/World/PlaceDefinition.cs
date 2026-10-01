using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// One place: its identity, its kind, where the party can arrive in it, and the content entry it came
/// from.
/// </summary>
/// <remarks>
/// The content entry is kept so a ruleset can read the fields this layer has no opinion about —
/// encounter settings, music, a map file reference — without the kit growing a vocabulary for them.
/// </remarks>
/// <param name="Id">The place's identity.</param>
/// <param name="Kind">Whether the place is a region or an interior.</param>
/// <param name="Name">The place's display name.</param>
/// <param name="EntryPoints">The spots the party can arrive at, in content order.</param>
/// <param name="Source">The content entry the place was read from.</param>
public sealed record PlaceDefinition(
    PlaceId Id,
    PlaceKind Kind,
    string Name,
    IReadOnlyList<PlaceEntryPoint> EntryPoints,
    ContentEntry Source)
{
    /// <summary>How two arrival-point ids are compared: as one name when they differ only in case.</summary>
    /// <remarks>
    /// Content spells one point's name inconsistently — a start and a transition naming it may disagree on
    /// case — so a lookup ignores case, and for the same reason two points of one place whose ids differ only
    /// in case are one name declared twice: the graph's load refuses that (<c>entry-point-id-reused</c>).
    /// </remarks>
    public static StringComparer EntryPointIds { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>Finds an arrival point by id, ignoring case, or null when the place has none.</summary>
    /// <remarks>
    /// A loaded place holds each id once, under <see cref="EntryPointIds"/>, so at most one point matches. A
    /// definition built some other way that holds two is refused here by name rather than answered with the
    /// first: a party sent to a name two points share would land at whichever came first, and the author's
    /// other point would be dead.
    /// </remarks>
    /// <param name="entryPointId">The arrival point's id.</param>
    /// <returns>The one point of that id, or null when the place has none.</returns>
    /// <exception cref="ContentValidationException">More than one point of this place answers to the id.</exception>
    public PlaceEntryPoint? FindEntryPoint(string entryPointId)
    {
        PlaceEntryPoint? found = null;
        foreach (PlaceEntryPoint point in EntryPoints)
        {
            if (!EntryPointIds.Equals(point.Id, entryPointId)) continue;
            if (found is not null)
            {
                string message = $"Place '{Id}' holds more than one arrival point answering to '{entryPointId}' ('{found.Id}' and '{point.Id}'), so which one a party arriving there lands at cannot be told.";
                throw new ContentValidationException(message, [new ContentValidationIssue("entry-point-ambiguous", message, Id.Value)]);
            }

            found = point;
        }

        return found;
    }
}
