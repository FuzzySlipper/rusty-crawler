using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This ruleset's session: it composes the kit's session shell with this game's identity, this game's one
/// clock, and the party its content describes or its creation flow builds.
/// </summary>
/// <remarks>
/// <para>
/// Composition happens in one order because the pieces depend on each other in exactly that order: the
/// clock is this game's policy and comes first, the party comes from content or from creation, the ledger is
/// the one path into the party's accounts, and the world is composed over the clock and the ledger so a
/// journey can charge both. The session then holds the party and the clock, publishes their facts, and
/// releases them with itself.
/// </para>
/// <para>
/// <b>A new session is created when the host declares a creation screen; otherwise it plays what its
/// scenario fixes.</b> This game's creation screen is the player-facing path a new game takes, so a host that
/// declared its controls gets a session holding this game's creation flow, starting in creation: its party
/// and the world that party walks into are composed when the player accepts them, which is what makes the
/// ledger the world charges the created party's own accounts. A host that declared none offers no creation
/// screen, and its session plays the party its content fixes — the scripted path a live check, a test, or a
/// product without creation takes, composed through the same factory creation ends at. A session resuming
/// from a save already holds both and creates nothing.
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

        // This game's services are read once, here, and the same answers are handed to the world — which
        // needs them to describe a counter the party talks to — and to the session, which serves it. One
        // reading of one placement is what keeps what a use offers and what a transaction does in step.
        MightAndMagic7Services? services = MightAndMagic7Services.Read(Declared(context.Content));

        PartyEntity? party = null;
        SessionWorld? world = null;
        EngineSessionSaveStore? store = MightAndMagic7Persistence.Store(context.Engine);
        try
        {
            SessionComposition composition = SessionComposition.From(ruleset) with
            {
                Bundle = context.Selection.BundleId,
                ContentPacks = context.Selection.PackCount,
            };
            MovementInput? movement = Movement(context);
            InteractionUseInput? use = Use(context);
            if (resume is { } save)
            {
                party = MightAndMagic7Party.Restore(save.Party);
                PartyResourceLedger ledger = Ledger(party);
                world = MightAndMagic7World.Compose(context.Content, context, clock, ledger, party, save, services);
                _session = new PartyRpgSession(
                    composition,
                    context.Projection,
                    world,
                    movement,
                    clock,
                    party,
                    context.Engine?.Diagnostics,
                    store,
                    MightAndMagic7Persistence.SaveSlot,
                    saveInput: context.Save,
                    resumed: true,
                    useInput: use,
                    service: services,
                    accounts: ledger,
                    serviceInput: context.Service);
                return;
            }

            if (Creation(context) is { } creation)
            {
                // The host declared a creation screen, so a new session creates its party. The flow is this
                // game's, the factory is the one every party comes from, and the world is composed with the
                // created party so the provisions a road costs come out of the larder the player's own
                // characters filled.
                ContentCatalog? declared = Declared(context.Content);
                _session = new PartyRpgSession(
                    composition,
                    context.Projection,
                    movementInput: movement,
                    clock: clock,
                    diagnostics: context.Engine?.Diagnostics,
                    saveStore: store,
                    saveSlot: MightAndMagic7Persistence.SaveSlot,
                    creationInput: creation,
                    creation: new SessionCreation(
                        MightAndMagic7Creation.Start(declared),
                        description => MightAndMagic7Party.Factory().Create(description),
                        created => MightAndMagic7World.Compose(declared, context, clock, Ledger(created), created, services: services)),
                    saveInput: context.Save,
                    useInput: use,
                    service: services,
                    serviceInput: context.Service);
                return;
            }

            // No creation screen was declared, so this session plays the party its scenario fixes: the
            // scripted path — a live check, a test, or a product that offers no creation. Handing that party
            // to the world here is the same composition order the created path takes, one accept earlier.
            party = MightAndMagic7Party.Compose(context.Content);
            PartyResourceLedger? accounts = party is null ? null : Ledger(party);
            world = MightAndMagic7World.Compose(context.Content, context, clock, accounts, party, services: services);
            _session = new PartyRpgSession(
                composition,
                context.Projection,
                world,
                movement,
                clock,
                party,
                context.Engine?.Diagnostics,
                store,
                MightAndMagic7Persistence.SaveSlot,
                saveInput: context.Save,
                useInput: use,
                service: services,
                accounts: accounts,
                serviceInput: context.Service);
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
    /// The party's own accounts as the one settlement path, so a journey charges what the party carries.
    /// </summary>
    /// <remarks>
    /// The larder's policy is this game's rule over the party it feeds: what a day costs and what hunger does
    /// are answered here, and the party is what the rule weakens and feeds again. The ledger is composed over
    /// whichever party the session holds — a restored one or the one creation just built — so both paths
    /// charge the same accounts through the same rule.
    /// </remarks>
    private static PartyResourceLedger Ledger(PartyEntity party) =>
        new(party, provisioning: new MightAndMagic7Provisions(party));

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

    /// <summary>
    /// The reader for the use controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// A host that declares no use control gets a session that never uses anything by itself, exactly as it
    /// gets no creation screen and no save key: the mechanism is still composed and still publishes what the
    /// party faces, and only the player's way of asking for a use is missing.
    /// </remarks>
    private static InteractionUseInput? Use(RulesetSessionContext context) =>
        context.Use is { } controls ? new InteractionUseInput(controls) : null;

    /// <summary>
    /// The reader for the creation controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// A host that declares no creation controls offers no creation screen, and a session without one plays
    /// the party its scenario fixes instead — so the reader is also the declaration that this product creates
    /// its parties. The flow and the reader arrive together: a session creating a party with no way to choose
    /// anything is refused where it is composed rather than composed as a screen nobody can drive.
    /// </remarks>
    private static CreationInput? Creation(RulesetSessionContext context) =>
        context.Creation is { } controls ? new CreationInput(controls) : null;

    /// <summary>
    /// The content creation is offered over: the packs that loaded, or nothing when none did.
    /// </summary>
    /// <remarks>
    /// An empty catalog is the absence of content rather than content that contradicts creation. The
    /// product's shipped bundle names no packs until the operator generates them, and creation's own
    /// contract is to be composed without content then — from the compiled tables, because a game that
    /// cannot create a party is not a game. The world and the scenario's party already read an empty catalog
    /// as nothing; this is where creation is told the same thing instead of being refused by a catalog that
    /// declares no classes because it declares nothing at all.
    /// </remarks>
    private static ContentCatalog? Declared(ContentCatalog? content) =>
        content is { Packs.Count: > 0 } ? content : null;

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
