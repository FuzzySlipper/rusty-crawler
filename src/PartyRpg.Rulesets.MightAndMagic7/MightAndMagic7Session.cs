using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
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
/// <b>A resumed session is composed the same way, from the same content, and then handed the save.</b> The
/// clock takes the game time the save recorded, the party is rebuilt by the same factory a created party is
/// built by, and the world takes its place, pose, and per-place state from the save — so what a load
/// rebuilds is the durable half of a session, and everything transient is composed fresh exactly as it is
/// for a new game.
/// </para>
/// <para>
/// Anything this composition creates and then fails to hand over is released here, so a session that
/// cannot be composed leaves no party, no engine scene, and no store behind it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Session : IGameSession
{
    private readonly PartyRpgSession _session;

    internal MightAndMagic7Session(IGameRuleset ruleset, RulesetSessionContext context, SessionSave? resume = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        GameClock clock = MightAndMagic7Time.Compose();

        // The clock takes the recorded game time before the world is composed, because the world's places
        // are read against the day the session stands on: a resumed session that restored its ledger and
        // then moved its clock would spend its first update crossing a boundary it had already crossed.
        resume?.Clock.ApplyTo(clock);

        PartyEntity? party = null;
        SessionWorld? world = null;
        EngineSessionSaveStore? store = MightAndMagic7Persistence.Store(context.Engine);
        try
        {
            party = resume is { } save
                ? MightAndMagic7Party.Restore(save.Party)
                : MightAndMagic7Party.Compose(context.Content);
            // The larder's policy is this game's rule over the party it feeds: what a day costs and what
            // hunger does are answered here, and the party is what the rule weakens and feeds again.
            PartyResourceLedger? resources = party is null
                ? null
                : new PartyResourceLedger(party, provisioning: new MightAndMagic7Provisions(party));

            world = MightAndMagic7World.Compose(context.Content, context, clock, resources, resume);
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
                context.Engine?.Diagnostics,
                store);
        }
        catch
        {
            // The world owns the engine's spatial session and the population's entities, and the party owns
            // the store it was created in: a composition that failed after building either must release it
            // rather than leaving a party nobody plays and a scene nobody walks.
            world?.Dispose();
            party?.Dispose();
            store?.Dispose();
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

    /// <summary>
    /// Writes the live session at this game's explicit save boundary, and returns the document written.
    /// </summary>
    /// <remarks>
    /// The session owns where a save goes: the player's request to save reaches this call, and no update,
    /// mode change, or shutdown reaches it by itself.
    /// </remarks>
    /// <returns>The document that was written.</returns>
    /// <exception cref="InvalidOperationException">The session was composed without a save store.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or the write failed.</exception>
    internal SessionSave Save() => _session.Save();
}
