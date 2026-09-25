namespace PartyRpg.Kit.Time;

/// <summary>Identifies one deadline the clock is holding.</summary>
/// <remarks>
/// The clock hands back a handle rather than a callback, a task, or a subscription, so what fires is
/// decided by the caller that reads the advance report and not by the clock reaching into its owners.
/// The handle is opaque: an owner keeps the one it registered beside whatever the deadline is about — a
/// service, a training, a spell — and looks itself up when the deadline comes due. Handles are never
/// reused, so a stale one matches nothing instead of matching a later registration.
/// </remarks>
/// <param name="Value">The handle's own number, which only the clock that issued it can interpret.</param>
public readonly record struct DeadlineId(long Value);
