using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;

namespace PartyRpg.Host;

/// <summary>
/// The one ordinary product entry. It selects a compiled ruleset at its explicit composition seam,
/// builds that ruleset's session over an engine UI projection channel, reads admitted input through
/// the kit's session input router, and forwards the engine lifecycle to the session. It owns no rules
/// and no gameplay state.
/// </summary>
/// <remarks>
/// <para>
/// The host also owns the two decisions that belong to whoever launched it: which bundle to load, and how a
/// run begins. A run either plays a new session or resumes the save in the product's slot, and that choice
/// is explicit — passed in, or read once from the declared environment variable — and never inferred from
/// whether a save happens to exist. A resume that finds nothing fails by name where the product is created,
/// because starting a new game in place of the one an operator asked to continue is the one thing a resume
/// switch must never do quietly.
/// </para>
/// <para>
/// The product declares the save control to the engine — a key on the digital intent, and the payload action
/// the DOM companion's save button sends — and hands those names to the ruleset, which composes the session
/// that reads them. Nothing here saves: the request travels with the admitted input into the session's one
/// update and reaches the explicit save boundary there.
/// </para>
/// </remarks>
public sealed class CrawlerProduct : IEngineProduct
{
    private readonly ProductCreateContext _context;
    private readonly IGameRuleset _ruleset;
    private readonly SessionInputRouter _input;
    private readonly MovementIntentNames _movement;
    private readonly CreationIntentNames _creation;
    private readonly SaveIntentNames _save;
    private readonly UseIntentNames _use;
    private readonly ServiceIntentNames _service;
    private readonly SkillRaiseIntentNames _skills;
    private readonly CastIntentNames _cast;
    private readonly MixIntentNames _mix;
    private readonly RestIntentNames _rest;
    private readonly ConversationIntentNames _conversation;
    private readonly CombatIntentNames _combat;
    private readonly BundleSelection _selection;
    private readonly ContentCatalog? _content;
    private IGameSession _session;
    private bool _started;
    private bool _shutdown;

    /// <summary>Creates the product with the host's default compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context)
        : this(context, BuiltInRulesets.Default, bundleId: null, start: ProductStart.FromEnvironment())
    {
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset and bundle.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset, string? bundleId, SessionStart start = SessionStart.Fresh)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ruleset);
        _context = context;
        _ruleset = ruleset;
        StartMode = start;
        _input = new SessionInputRouter(ProductIdentity.PauseToggleIntent, ProductIdentity.UiActionContract);
        _movement = new MovementIntentNames(
            ProductIdentity.MoveForwardIntent,
            ProductIdentity.MoveBackIntent,
            ProductIdentity.StrafeLeftIntent,
            ProductIdentity.StrafeRightIntent,
            ProductIdentity.TurnLeftIntent,
            ProductIdentity.TurnRightIntent,
            ProductIdentity.JumpIntent);
        _creation = new CreationIntentNames(
            ProductIdentity.CreationAdvanceIntent,
            ProductIdentity.CreationAcceptIntent,
            ProductIdentity.UiActionContract);
        _save = new SaveIntentNames(
            ProductIdentity.SaveIntent,
            ProductIdentity.SaveAction,
            ProductIdentity.UiActionContract);
        _use = new UseIntentNames(
            ProductIdentity.UseIntent,
            ProductIdentity.UseAction,
            ProductIdentity.UiActionContract);
        _service = new ServiceIntentNames(
            ProductIdentity.ServiceLeaveIntent,
            ProductIdentity.UiActionContract);
        // The conversation's controls are declared the same way the counter's are: one key that leaves, and
        // the choices on the payload contract the companion's own buttons claim.
        _conversation = new ConversationIntentNames(
            ProductIdentity.ConversationLeaveIntent,
            ProductIdentity.UiActionContract);
        // Every stop is declared with its own control, so a key and a screen button ask for exactly the same
        // night's sleep or wait: one name per act, on the digital intents above and on the payload contract
        // the companion's own buttons claim.
        _rest = new RestIntentNames(
            ProductIdentity.RestIntent,
            ProductIdentity.CampIntent,
            ProductIdentity.WaitUntilDawnIntent,
            ProductIdentity.WaitAnHourIntent,
            ProductIdentity.WaitFiveMinutesIntent,
            ProductIdentity.UiActionContract);
        // The skill-spend control is one payload action and no key: spending a point is the character
        // screen's own act, and the screen names the member and the skill it drew.
        _skills = new SkillRaiseIntentNames(
            ProductIdentity.SkillRaiseAction,
            ProductIdentity.UiActionContract);
        // Casting is two payload actions and no key, for the same reason: a spell, a caster, and a target are
        // what a screen's own rows name, and one press of a key could say none of them. The quick slot is the
        // second action, because which spell a character keeps there is a choice the spellbook screen makes.
        _cast = new CastIntentNames(
            ProductIdentity.CastAction,
            ProductIdentity.QuickSpellAction,
            ProductIdentity.UiActionContract);
        // Mixing is one payload action and no key, for the same reason casting is two: which two of the things
        // the party carries a player put together is what the pack screen's own rows name.
        _mix = new MixIntentNames(
            ProductIdentity.MixAction,
            ProductIdentity.UiActionContract);
        // The act control is one intent and one action, because the act is one act: what a member does with
        // it is the ruleset's answer about that member, and a player presses the same control for a spell, a
        // shot, or a swing. The pace controls travel with it, because a paced fight is the same fight: one
        // control switches the pacing, and skipping and waiting each have their own because their
        // consequences differ.
        _combat = new CombatIntentNames(
            ProductIdentity.AttackIntent,
            ProductIdentity.AttackAction,
            ProductIdentity.UiActionContract,
            new TurnIntentNames(
                ProductIdentity.TurnBasedToggleIntent,
                ProductIdentity.TurnSkipIntent,
                ProductIdentity.TurnWaitIntent,
                ProductIdentity.UiActionContract));
        (_selection, _content) = SelectBundle(context, bundleId ?? BuiltInBundles.Default);
        _session = CreateSession();
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset)
        : this(context, ruleset, bundleId: null, start: ProductStart.FromEnvironment())
    {
    }

    /// <summary>The bundle and content the product is running with.</summary>
    public BundleSelection Selection => _selection;

    /// <summary>How this run began: a new session, or the one the save slot held.</summary>
    public SessionStart StartMode { get; }

    /// <summary>
    /// Loads the staged content and resolves the bundle to start from.
    /// </summary>
    /// <remarks>
    /// Content that is present and wrong stops the product here, naming every problem at once, rather
    /// than starting and meeting the defect later as a missing monster. Content that is absent yields
    /// no selection instead: a checkout whose packs have not been generated yet still runs.
    /// </remarks>
    private static (BundleSelection Selection, ContentCatalog? Content) SelectBundle(ProductCreateContext context, string bundleId)
    {
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductIdentity.ContentDirectory),
            bundleId);
        if (!bootstrap.IsValid)
        {
            throw new ContentValidationException(
                $"{ProductIdentity.ProductTitle} cannot start: {bootstrap.Issues[0]}",
                bootstrap.Issues);
        }

        return bootstrap.Selection is { } selection
            ? (new BundleSelection(selection.Bundle.BundleId, selection.Packs.Count), bootstrap.Catalog)
            : (BundleSelection.None, (ContentCatalog?)null);
    }

    /// <summary>The mode the session is in.</summary>
    public SessionMode Mode => _session.Mode;

    /// <summary>The compiled ruleset this product selected.</summary>
    public RulesetId RulesetId => _ruleset.Id;

    /// <summary>Starts the session.</summary>
    public void Start()
    {
        if (_shutdown) return;
        _started = true;
        _session.Start();
    }

    /// <summary>
    /// Republishes the current presentation. A fresh browser attachment reconstructs presentation from
    /// committed engine state, so the current projection is published again rather than rebuilt here.
    /// </summary>
    public void Attach()
    {
        if (_shutdown) return;
        _session.PublishInitial();
    }

    /// <summary>Holds the session because the engine paused it.</summary>
    public void Pause()
    {
        if (!_started || _shutdown) return;
        _session.Pause();
    }

    /// <summary>Releases the engine's pause. A hold the player asked for stays in force.</summary>
    public void Resume()
    {
        if (!_started || _shutdown) return;
        _session.Resume();
    }

    /// <summary>Replaces the session with a freshly built one, keeping the same selected ruleset.</summary>
    public void Restart()
    {
        if (_shutdown) return;
        // The replacement is built before the old session is released, so a failure here leaves the
        // running session in place rather than the product without one. The selected bundle is reused
        // rather than re-read: a restart repeats the same run, it does not silently load other content.
        IGameSession replacement = CreateSession();
        IGameSession previous = _session;
        _session = replacement;
        previous.Dispose();
        if (_started) _session.Start();
    }

    /// <summary>Shuts the session down and stops responding to the engine.</summary>
    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _session.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => Shutdown();

    /// <summary>
    /// Applies this update's input to the session and then advances it. Input is settled first so a
    /// hold issued in this update is already in force when the session steps, and the projection it
    /// publishes describes the session the player just put it in.
    /// </summary>
    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (!_started || _shutdown) return ProductUpdateResult.None;
        _input.Apply(_session, update.Input);
        return _session.Update(update);
    }

    private IGameSession CreateSession()
    {
        EngineUiProjectionChannel channel = new(
            _context.Engine.Ui,
            new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract));
        try
        {
            // The engine's own services go to the ruleset whole: composing movement needs the spatial
            // service to walk in and the content owner to retain a place's collision artifact, and the
            // product is the only place that holds the engine context the ruleset would otherwise have to
            // reach for. The movement, creation, save, use, and service controls are the host's declaration,
            // so their names go with them: a name the product never declared to the engine is a control
            // nobody can press, and a service screen whose commands arrive on an undeclared contract is a
            // screen that cannot be driven.
            RulesetSessionContext context = new(
                channel,
                _selection,
                _content,
                Engine: _context.Engine,
                Movement: _movement,
                Creation: _creation,
                Save: _save,
                Use: _use,
                Service: _service,
                Rest: _rest,
                Conversation: _conversation,
                Combat: _combat,
                Skills: _skills,
                Cast: _cast,
                Mix: _mix);

            // The start switch travels with the context and is answered at the ruleset's one composition
            // entry: a resumed run reads the save the slot holds and composes a session from it, and a slot
            // that holds nothing fails by name rather than starting a new expedition in place of the one
            // that was asked for.
            return _ruleset.CreateSession(context with { Start = StartMode });
        }
        catch
        {
            // The stream is opened before the ruleset composes its session, so a failed composition
            // must release it here; otherwise the stream outlives the product that asked for it.
            channel.Dispose();
            throw;
        }
    }
}

/// <summary>
/// Reads how a product run should begin from the declared environment variable.
/// </summary>
/// <remarks>
/// The dev runner owns its command line and hands the product no arguments, so the operator's switch is an
/// environment variable read once, where the product is created. Absent or empty means a new session, the
/// two words the product states mean what they say, and anything else stops the run with the value named
/// rather than being rounded to one of them: an operator who typed a switch wrong must not be handed a new
/// game when they asked to continue one.
/// </remarks>
internal static class ProductStart
{
    /// <summary>The start a switch value names.</summary>
    /// <param name="value">The value read from the environment, or null when it is unset.</param>
    /// <returns>The start the value names.</returns>
    /// <exception cref="InvalidOperationException">The value names neither a fresh session nor a resume.</exception>
    internal static SessionStart Parse(string? value) =>
        (value?.Trim().ToLowerInvariant() ?? string.Empty) switch
        {
            "" or "fresh" => SessionStart.Fresh,
            "resume" => SessionStart.Resume,
            _ => throw new InvalidOperationException(
                $"{ProductIdentity.StartVariable} is '{value}', which names neither a fresh session nor a resume. Set it to 'fresh' to start a new expedition or 'resume' to continue the one the save slot holds, or leave it unset for a fresh session."),
        };

    /// <summary>The start mode the environment selects.</summary>
    internal static SessionStart FromEnvironment() =>
        Parse(Environment.GetEnvironmentVariable(ProductIdentity.StartVariable));
}
