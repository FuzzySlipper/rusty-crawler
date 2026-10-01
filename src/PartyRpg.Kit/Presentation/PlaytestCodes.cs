namespace PartyRpg.Kit.Presentation;

/// <summary>The codes a playtest query refuses a step or a look with.</summary>
/// <remarks>
/// A code is what a harness and a test branch on and the message beside it is what a person reads, so every
/// reason the session's movement controls would not step the party now is named here once.
/// </remarks>
public static class PlaytestCodes
{
    /// <summary>The refusal code <c>steer-not-running</c>: the session has not started, or has stopped.</summary>
    public const string SteerNotRunning = "steer-not-running";

    /// <summary>The refusal code <c>steer-creating</c>: a party is still being made.</summary>
    public const string SteerCreating = "steer-creating";

    /// <summary>The refusal code <c>steer-no-world</c>: the session holds no places to walk in.</summary>
    public const string SteerNoWorld = "steer-no-world";

    /// <summary>The refusal code <c>steer-held</c>: the session is held, by the player or by the engine.</summary>
    public const string SteerHeld = "steer-held";

    /// <summary>The refusal code <c>steer-turn</c>: a paced fight is waiting for one of the party's turns.</summary>
    public const string SteerTurn = "steer-turn";

    /// <summary>The refusal code <c>steer-screen</c>: a counter or a conversation holds the controls.</summary>
    public const string SteerScreen = "steer-screen";

    /// <summary>The refusal code <c>look-not-a-number</c>: a look delta is not a finite number.</summary>
    public const string LookNotANumber = "look-not-a-number";

    /// <summary>The refusal code <c>look-no-pitch</c>: the party does not look up or down in play.</summary>
    public const string LookNoPitch = "look-no-pitch";
}
