namespace PartyRpg.Kit.World;

/// <summary>The unit an amount of elapsed travel time is stated in.</summary>
/// <remarks>
/// The unit travels with the amount instead of being implied by the field, because the kit owns no
/// calendar. Turning "three days" into the clock's own duration needs a hours-per-day ratio, and that
/// ratio is the calendar owner's policy; a cost that arrived as a bare number would force this layer to
/// guess it, and a guess here would quietly disagree with the clock every other owner reads.
/// </remarks>
public enum TravelTimeUnit
{
    /// <summary>Elapsed minutes.</summary>
    Minutes,

    /// <summary>Elapsed hours.</summary>
    Hours,

    /// <summary>Elapsed days.</summary>
    Days,
}
