namespace PartyRpg.Kit.Services;

/// <summary>What one command asks a service to do, in the mechanism's own vocabulary.</summary>
/// <remarks>
/// A command is a value rather than a call, so what a screen asked for can be read, applied, and reported
/// in that order inside one admitted update — the same shape the creation commands have. Leaving is one of
/// them because entering and leaving a service are both explicit acts: a visit ends when the party asks to
/// leave, and never because a screen was drawn again.
/// </remarks>
public enum ServiceCommandKind
{
    /// <summary>Take a line of the shelves, paying for it.</summary>
    Buy,

    /// <summary>Give the service one of the party's items, taking what it pays.</summary>
    Sell,

    /// <summary>Pay to learn what an item is.</summary>
    Identify,

    /// <summary>Pay to have an item repaired.</summary>
    Repair,

    /// <summary>Pay for a lesson.</summary>
    Teach,

    /// <summary>Pay for a cure the counter offers.</summary>
    Cure,

    /// <summary>Pay for a training step the counter offers.</summary>
    Train,

    /// <summary>Pay for provisions the counter offers.</summary>
    Provision,

    /// <summary>Pay for a night's stay the counter offers.</summary>
    Stay,

    /// <summary>Leave coins with the counter.</summary>
    Deposit,

    /// <summary>Take back the coins the counter holds.</summary>
    Withdraw,

    /// <summary>Pay for a passage the counter sells.</summary>
    Fare,

    /// <summary>Leave the counter, ending the visit.</summary>
    Leave,
}

/// <summary>One command a screen sent, with what it named.</summary>
/// <remarks>
/// <para>
/// The target is a string because the things a command can name are named differently: a lot of the shelves
/// by its lot identity, an item the party holds by its durable instance identity, a lesson by its subject,
/// and an offer — a cure's condition, a fare's destination place, a holding's name — by the subject content
/// gave it. Resolving it is the mechanism's work, and a target the visit cannot resolve is a refusal with
/// its own code: "no such item", "out of stock", and "this counter offers no such thing" are different
/// answers to a player.
/// </para>
/// <para>
/// A count is carried for the commands that act on more than one thing: how many of a line the party takes,
/// or how many coins a deposit or a withdrawal moves. What may be bought is what the shelves hold and what
/// may be deposited is what the purse holds, both of which the mechanism judges before anything is settled.
/// </para>
/// </remarks>
/// <param name="Kind">Which command this is.</param>
/// <param name="Target">
/// What the command names: a lot, an item instance, a lesson, or an offer. Empty for a command that names
/// nothing, such as leaving or a counter's single training step.
/// </param>
/// <param name="Member">Which member a lesson, a cure, or a training step is for, counted from zero.</param>
/// <param name="Count">How many of the thing named to act on, at least one.</param>
/// <param name="Tier">
/// Which rung of a skill a lesson leaves a member at, where one is the first rung. It is part of what names
/// a lesson rather than a property of the command: a counter can teach the same skill at more than one rung,
/// and the screen sends the rung of the row the player pressed.
/// </param>
public sealed record ServiceCommand(
    ServiceCommandKind Kind,
    string Target = "",
    int Member = 0,
    int Count = 1,
    int Tier = 1)
{
    /// <summary>Creates a command that names nothing.</summary>
    /// <param name="kind">Which command to state.</param>
    /// <returns>The command.</returns>
    public static ServiceCommand Of(ServiceCommandKind Kind) => new(Kind);

    /// <summary>Which operation the command asks for, or null when it asks to leave.</summary>
    public ServiceOperationKind? Operation => Kind switch
    {
        ServiceCommandKind.Buy => ServiceOperationKind.Buy,
        ServiceCommandKind.Sell => ServiceOperationKind.Sell,
        ServiceCommandKind.Identify => ServiceOperationKind.Identify,
        ServiceCommandKind.Repair => ServiceOperationKind.Repair,
        ServiceCommandKind.Teach => ServiceOperationKind.Teach,
        ServiceCommandKind.Cure => ServiceOperationKind.Cure,
        ServiceCommandKind.Train => ServiceOperationKind.Train,
        ServiceCommandKind.Provision => ServiceOperationKind.Provision,
        ServiceCommandKind.Stay => ServiceOperationKind.Stay,
        ServiceCommandKind.Deposit => ServiceOperationKind.Deposit,
        ServiceCommandKind.Withdraw => ServiceOperationKind.Withdraw,
        ServiceCommandKind.Fare => ServiceOperationKind.Fare,
        _ => null,
    };
}
