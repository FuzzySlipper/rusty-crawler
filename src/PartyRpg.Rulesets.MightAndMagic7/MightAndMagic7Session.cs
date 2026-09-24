using PartyRpg.Kit.Content;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This ruleset's session: it composes the kit's session shell with this game's identity. Gameplay
/// mechanisms are attached here as their stones land; the delegation below is the seam they arrive
/// through, not a placeholder for one.
/// </summary>
internal sealed class MightAndMagic7Session : IGameSession
{
    private readonly PartyRpgSession _session;

    internal MightAndMagic7Session(IGameRuleset ruleset, RulesetSessionContext context)
    {
        _session = new PartyRpgSession(
            SessionComposition.From(ruleset) with
            {
                Bundle = context.Selection.BundleId,
                ContentPacks = context.Selection.PackCount,
            },
            context.Projection,
            MightAndMagic7World.Compose(context.Content, context.Time));
    }

    /// <inheritdoc />
    public SessionMode Mode => _session.Mode;

    /// <inheritdoc />
    public void Start() => _session.Start();

    /// <inheritdoc />
    public void Pause() => _session.Pause();

    /// <inheritdoc />
    public void Resume() => _session.Resume();

    /// <inheritdoc />
    public void Hold() => _session.Hold();

    /// <inheritdoc />
    public void ReleaseHold() => _session.ReleaseHold();

    /// <inheritdoc />
    public void PublishInitial() => _session.PublishInitial();

    /// <inheritdoc />
    public ProductUpdateResult Update(ProductUpdate update) => _session.Update(update);

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();
}
