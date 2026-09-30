using Rusty.Engine;

namespace PartyRpg.Kit.Input;

/// <summary>What every reader of the admitted input agrees an event means.</summary>
internal static class InputEvents
{
    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge; a direct interface claim is
    /// admitted with no edge at all, so its own phase and provenance are what identify it.
    /// </summary>
    public static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;
}
