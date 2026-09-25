namespace PartyRpg.Kit.Presentation;

/// <summary>How a session stands with its save slot, as the panel shows it.</summary>
public enum SaveState
{
    /// <summary>No save has been asked for in this session, so there is no outcome to show.</summary>
    Never,

    /// <summary>The last save request was written to the slot.</summary>
    Saved,

    /// <summary>The last save request could not be written, and <see cref="SaveSnapshot.Message"/> says why.</summary>
    Failed,
}

/// <summary>
/// A session's save state: where a save would go, whether this session was composed from one, and what
/// happened the last time the player asked for one.
/// </summary>
/// <remarks>
/// <para>
/// Saving is an explicit act with an observable outcome, so the outcome is session state rather than
/// something a caller keeps beside the session: the panel a player reads is built from this value, and a
/// save that did not land is as visible as one that did. <see cref="State"/> is a state rather than a
/// boolean because "nothing was ever asked" and "the last request failed" are different facts about a
/// session, and a panel that showed them the same way would leave a player unable to tell a save that has
/// not happened yet from one that just went wrong.
/// </para>
/// <para>
/// <see cref="At"/> is the game moment a save landed, read from the session's one clock when it was
/// written, so a player reading the panel can see which point in the expedition the slot now holds. It is
/// empty until a save lands, and the panel shows that emptiness rather than the first day of a calendar.
/// </para>
/// <para>
/// <see cref="Resumed"/> is the host's composition decision made visible: this session was built from the
/// save in this slot rather than started fresh. It is reported here because it is the one fact that makes
/// the two sessions a player could be looking at distinguishable, and a resumed expedition that looked
/// exactly like a new one would leave the operator unable to tell whether the switch took effect.
/// </para>
/// </remarks>
/// <param name="Available">
/// Whether this session has a save store at all. A session composed without one still plays, and asking
/// it to save fails by name; the panel can then show that saving is not possible here instead of offering
/// a control that cannot work.
/// </param>
/// <param name="Resumed">Whether this session was composed from the save in this slot.</param>
/// <param name="Slot">The slot a save is written to and a resume reads.</param>
/// <param name="State">Whether a save has been asked for, and how the last request ended.</param>
/// <param name="At">The game date and time the last save landed, empty when none has.</param>
/// <param name="Code">
/// Which failure this was, empty unless <see cref="State"/> is <see cref="SaveState.Failed"/>:
/// <c>save-unavailable</c> when the session has no store, <c>save-refused</c> when the session held
/// nothing a load could rebuild, and <c>save-failed</c> when the write itself did not land.
/// </param>
/// <param name="Message">What happened, in the terms of the save that could not be used.</param>
public readonly record struct SaveSnapshot(
    bool Available,
    bool Resumed,
    string Slot,
    SaveState State,
    string At,
    string Code,
    string Message)
{
    /// <summary>Whether the last save request was written.</summary>
    public bool IsSaved => State == SaveState.Saved;

    /// <summary>Whether the last save request failed, whatever the reason.</summary>
    public bool IsFailed => State == SaveState.Failed;

    /// <summary>
    /// The save state of a session no save has been asked of yet.
    /// </summary>
    /// <param name="available">Whether the session has a save store.</param>
    /// <param name="resumed">Whether the session was composed from the save in its slot.</param>
    /// <param name="slot">The slot a save would be written to.</param>
    /// <returns>The state a session that has saved nothing publishes.</returns>
    public static SaveSnapshot None(bool available, bool resumed, string slot) =>
        new(available, resumed, slot, SaveState.Never, string.Empty, string.Empty, string.Empty);
}
