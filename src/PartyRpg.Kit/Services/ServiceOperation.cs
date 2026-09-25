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
/// policy the ruleset supplies for it: whether the party may do it here, what it costs, and what it offers.
/// </para>
/// <para>
/// <b>An operation is here when the mechanism has to move something it owns.</b> It has to know what to
/// move: a purchase takes a line off a shelf and puts items in the party's pack, a sale does the opposite,
/// identification and repair change one instance's own state, a lesson raises a member's skill or puts a
/// party-wide effect on the band, a cure ends conditions a member suffers, training turns banked experience
/// into a level, a provision fills the party's larder, a stay spends the party's time and rests it, a
/// deposit and a withdrawal move coin between the purse and what a counter keeps, and a fare records a
/// passage the party has bought. Each of those is one step of this one workflow, and a game's kind of
/// building either composes them or adds one — never a class per building.
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

    /// <summary>Pay to end the conditions a member suffers, which is what a temple's healing is.</summary>
    Cure,

    /// <summary>Pay to turn a member's banked experience into a level, up to the counter's ceiling.</summary>
    Train,

    /// <summary>Pay to fill the party's larder, which is what a tavern's food and drink is.</summary>
    Provision,

    /// <summary>Pay for a night's stay, which spends game time and rests the party.</summary>
    Stay,

    /// <summary>Leave coins with the counter, which it holds for the party.</summary>
    Deposit,

    /// <summary>Take back the coins the counter holds.</summary>
    Withdraw,

    /// <summary>Pay for a passage to a place, which the party then holds as a ticket.</summary>
    Fare,
}
