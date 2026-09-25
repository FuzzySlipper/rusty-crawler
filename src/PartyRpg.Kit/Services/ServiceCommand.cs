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

    /// <summary>Leave the counter, ending the visit.</summary>
    Leave,
}

/// <summary>One command a screen sent, with what it named.</summary>
/// <remarks>
/// <para>
/// The target is a string because the three things a command can name are named differently: a lot of the
/// shelves by its lot identity, an item the party holds by its durable instance identity, and a lesson by
/// its subject. Resolving it is the mechanism's work, and a target the visit cannot resolve is a refusal
/// with its own code — "no such item" and "out of stock" are different answers to a player.
/// </para>
/// <para>
/// A count is carried for purchases, where a party may take more than one of a line. It is deliberately
/// not the whole stack rule: what may be bought is what the shelves hold, which the mechanism judges before
/// anything is settled.
/// </para>
/// </remarks>
/// <param name="Kind">Which command this is.</param>
/// <param name="Target">
/// What the command names: a lot, an item instance, or a lesson. Empty for a command that names nothing,
/// such as leaving.
/// </param>
/// <param name="Member">Which member a lesson is for, counted from zero; unused by the other commands.</param>
/// <param name="Count">How many of a purchased line to take, at least one.</param>
public sealed record ServiceCommand(ServiceCommandKind Kind, string Target = "", int Member = 0, int Count = 1)
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
        _ => null,
    };
}
