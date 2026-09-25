namespace PartyRpg.Kit.Interaction;

/// <summary>What has already happened to one target, and how many times its incarnation has changed.</summary>
/// <remarks>
/// <para>
/// The word is the ruleset's, not the kit's: a door's <c>open</c>, a container's <c>searched</c>, a lever's
/// <c>pulled</c> are answers a ruleset gives, and the kit carries them without knowing what any of them
/// means. That is what lets one mechanism serve kinds that do not exist yet, and it is why the state of a
/// door is a word rather than a door-shaped type here.
/// </para>
/// <para>
/// The revision counts the changes a target has been through, starting at zero for a target nothing has
/// happened to. It exists because a selection made before a use must not authorize one after it: a caller
/// that inspected a closed door and then opens it holds an answer about an incarnation that is gone, and the
/// revision is what says so.
/// </para>
/// </remarks>
/// <param name="State">What the party has done to the target, as the ruleset's own word; empty when nothing has.</param>
/// <param name="Revision">How many times the target's state has changed, which starts at zero.</param>
public readonly record struct InteractionTargetState(string State, int Revision)
{
    /// <summary>A target nothing has happened to: no state word, and no change to its incarnation.</summary>
    /// <remarks>
    /// This is what a target reads as before the ruleset has recorded anything for it, which is also when
    /// content's own state — a door's stored open or closed position — is what the target is.
    /// </remarks>
    public static InteractionTargetState None => new(string.Empty, 0);
}
