namespace MightAndMagic7.Import.Maps;

/// <summary>The decoration names that mean "the party arrives here".</summary>
/// <remarks>
/// Arrival is a decoration lookup rather than a header field, and the game matches the name without
/// regard to case because the shipped maps spell them inconsistently — <c>"Party Start"</c> and
/// <c>"north start"</c> both occur. Only these five names count; any other decoration is scenery.
/// </remarks>
internal static class EntryPointNames
{
    private static readonly string[] Names =
    [
        "Party Start",
        "North Start",
        "South Start",
        "East Start",
        "West Start",
    ];

    /// <summary>Matches a decoration name against the start-point names, ignoring case.</summary>
    internal static bool TryCanonical(string decorationName, out string name)
    {
        foreach (string candidate in Names)
        {
            if (string.Equals(decorationName, candidate, StringComparison.OrdinalIgnoreCase))
            {
                name = candidate;
                return true;
            }
        }

        name = string.Empty;
        return false;
    }
}
