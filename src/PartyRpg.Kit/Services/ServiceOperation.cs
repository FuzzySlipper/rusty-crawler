namespace PartyRpg.Kit.Services;

/// <summary>
/// What a party asks a service to do: the mechanism's own vocabulary of operations, not a game's.
/// </summary>
/// <remarks>
/// <para>
/// <b>An operation is a step of this mechanism's workflow; a service kind is content's word.</b> Every kind
/// of building is served by the same mechanism and the same operations, so adding a weapon shop, a temple,
/// or a guild means adding content — a definition naming its kind, the operations it offers, its shelves,
/// its lessons, its hours, and its access requirement — and never a class. What an operation means is the
/// policy the ruleset supplies for it: whether the party may do it here, what it costs, and what the
/// shelves hold.
/// </para>
/// <para>
/// An operation is listed here because the mechanism has to know what to move: a purchase takes a line off
/// a shelf and puts items in the party's pack, a sale does the opposite, identification and repair change
/// one instance's own state, and a lesson raises a member's skill or puts a party-wide effect on the band.
/// The operations still to come — a temple's cures, a tavern's rest, a bank's deposit, a hall's training, a
/// stable's fare — are additions to this list and to the workflow that applies them, which is the one place
/// a new kind of service can ever require code; they belong to the task that implements those kinds.
/// </para>
/// </remarks>
public enum ServiceOperationKind
{
    /// <summary>Take what the shelves hold, paying the service's price for it.</summary>
    Buy,

    /// <summary>Give the party's own item to the service, taking what it pays for it.</summary>
    Sell,

    /// <summary>Pay to learn what an item is, which is item state rather than a shop-only effect.</summary>
    Identify,

    /// <summary>Pay to have an item's damage repaired, which is item state as well.</summary>
    Repair,

    /// <summary>Pay for a lesson: a skill, or the membership a guild requires.</summary>
    Teach,
}
