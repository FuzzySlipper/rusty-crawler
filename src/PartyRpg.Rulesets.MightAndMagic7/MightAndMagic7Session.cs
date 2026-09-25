using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This ruleset's session: it composes the kit's session shell with this game's identity, this game's one
/// clock, and the party its content describes.
/// </summary>
/// <remarks>
/// <para>
/// Composition happens in one order because the pieces depend on each other in exactly that order: the
/// clock is this game's policy and comes first, the party comes from content, the ledger is the one path
/// into the party's accounts, and the world is composed over the clock and the ledger so a journey can
/// charge both. The session then holds the party and the clock, publishes their facts, and releases them
/// with itself.
/// </para>
/// <para>
/// Anything this composition creates and then fails to hand over is released here, so a session that
/// cannot be composed leaves no party and no engine scene behind it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Session : IGameSession
{
    private readonly PartyRpgSession _session;

    internal MightAndMagic7Session(IGameRuleset ruleset, RulesetSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GameClock clock = MightAndMagic7Time.Compose();
        PartyEntity? party = null;
        SessionWorld? world = null;
        try
        {
            party = MightAndMagic7Party.Compose(context.Content);
            // The larder's policy is this game's rule over the party it feeds: what a day costs and what
            // hunger does are answered here, and the party is what the rule weakens and feeds again.
            PartyResourceLedger? resources = party is null
                ? null
                : new PartyResourceLedger(party, provisioning: new MightAndMagic7Provisions(party));

            world = MightAndMagic7World.Compose(context.Content, context, clock, resources);
            _session = new PartyRpgSession(
                SessionComposition.From(ruleset) with
                {
                    Bundle = context.Selection.BundleId,
                    ContentPacks = context.Selection.PackCount,
                },
                context.Projection,
                world,
                Movement(context),
                clock,
                party,
                context.Engine?.Diagnostics);
        }
        catch
        {
            // The world owns the engine's spatial session and the population's entities, and the party owns
            // the store it was created in: a composition that failed after building either must release it
            // rather than leaving a party nobody plays and a scene nobody walks.
            world?.Dispose();
            party?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The reader for the movement controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// The host owns the intent names and the ruleset owns how fast a held turn control turns the party,
    /// which is why the reader is composed here from both: a product that declares no movement controls
    /// gets no reader, and the session then never asks the world to move anything.
    /// </remarks>
    private static MovementInput? Movement(RulesetSessionContext context) =>
        context.Movement is { } controls
            ? new MovementInput(controls, MightAndMagic7Movement.TurnRatePerSecond)
            : null;

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
