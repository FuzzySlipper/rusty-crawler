using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>The record a physical surface opens for this use. Its own ledger identity keeps its trap and searched state.</summary>
public sealed record InteractionUse(PlacementDefinition Placement, InteractionTargetDefinition Target)
{
    /// <summary>Why this surface cannot open anything; no guard or contents are applied.</summary>
    public Refusal? Refusal { get; init; }

    /// <summary>Finishes the surface's event after a successful application. A trap-only use never finishes it.</summary>
    public Func<InteractionOutcome, InteractionOutcome>? AfterApply { get; init; }
}
