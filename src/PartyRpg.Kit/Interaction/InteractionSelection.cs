using System.Numerics;
using Rusty.Engine.Interaction;
using TargetIdentity = Rusty.Engine.Interaction.InteractionTarget;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// The one engine selection a product's interaction mechanisms aim through, kept for as long as the product
/// lives rather than for as long as one world does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it outlives a world.</b> A session replaces its world when creation is accepted, and a product
/// replaces its session when it restarts, but whatever inspects the reticle from outside — the engine's
/// interaction inspection, registered once when the product is created — holds one
/// <see cref="WorldInteraction"/> for the product's whole life. The live world's mechanism therefore aims
/// through this one selection instead of a private one, so what an inspection reports as selected is what the
/// party actually faces, and there is never a second focus to disagree with it.
/// </para>
/// <para>
/// <b>Whoever steps owns it.</b> The mechanism that last refreshed or used the selection is the scene it reads:
/// a world claims the selection every time it aims, which clears the focus when the claimant changes, and a
/// world that is released gives it up only if it still holds it. A replacement that is built and then thrown
/// away cannot leave the running world without its selection, because the running world claims it back on its
/// next step.
/// </para>
/// <para>
/// With no world holding it, the selection reads an empty scene: nothing to face and nothing to use. Targeted
/// use is off: a use reaches the world only through the party's own use control, which is the one path that
/// also opens the conversation a use lands in and records what it taught.
/// </para>
/// </remarks>
public sealed class InteractionSelection : IWorldInteractionScene
{
    /// <summary>The stamp an empty scene reports, so an inspection can tell "no world" from "nothing here".</summary>
    private const string NoWorld = "no-world";

    private IWorldInteractionScene? _scene;

    /// <summary>Creates a selection no world holds yet.</summary>
    public InteractionSelection()
    {
        Interaction = new WorldInteraction(this, targetedUseEnabled: false);
    }

    /// <summary>The engine's selection, which the live world aims through and an inspection reads.</summary>
    public WorldInteraction Interaction { get; }

    /// <summary>Makes a world's mechanism the scene this selection reads, clearing the focus when that changes.</summary>
    /// <param name="scene">The mechanism about to aim or use.</param>
    /// <returns>The selection to aim through.</returns>
    internal WorldInteraction Claim(IWorldInteractionScene scene)
    {
        if (!ReferenceEquals(_scene, scene))
        {
            _scene = scene;
            Interaction.Focus.Clear();
        }

        return Interaction;
    }

    /// <summary>Gives the selection up for a mechanism that is being released, if it still holds it.</summary>
    /// <param name="scene">The mechanism being released.</param>
    internal void Release(IWorldInteractionScene scene)
    {
        if (!ReferenceEquals(_scene, scene)) return;
        _scene = null;
        Interaction.Focus.Clear();
    }

    /// <inheritdoc />
    InteractionSceneSnapshot IWorldInteractionScene.ReadInteraction() =>
        _scene?.ReadInteraction() ?? new InteractionSceneSnapshot(
            new InteractionQuery(Vector3.Zero, -Vector3.UnitZ, 0, 0, 0, 0),
            ReadOnlyMemory<InteractionCandidate>.Empty,
            NoWorld);

    /// <inheritdoc />
    InteractionActionResult IWorldInteractionScene.UseInteraction(TargetIdentity target) =>
        _scene?.UseInteraction(target)
        ?? new InteractionActionResult(false, "There is no world to use anything in.");
}
