using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Presentation;
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
public sealed class CrawlerProduct : IEngineProduct
{
    private readonly ProductCreateContext _context;
    private readonly IGameRuleset _ruleset;
    private readonly SessionInputRouter _input;
    private readonly MovementIntentNames _movement;
    private readonly CreationIntentNames _creation;
    private readonly BundleSelection _selection;
    private readonly ContentCatalog? _content;
    private IGameSession _session;
    private bool _started;
    private bool _shutdown;

    /// <summary>Creates the product with the host's default compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context)
        : this(context, BuiltInRulesets.Default)
    {
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset and bundle.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset, string? bundleId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ruleset);
        _context = context;
        _ruleset = ruleset;
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
        (_selection, _content) = SelectBundle(context, bundleId ?? BuiltInBundles.Default);
        _session = CreateSession();
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset)
        : this(context, ruleset, BuiltInBundles.Default)
    {
    }

    /// <summary>The bundle and content the product is running with.</summary>
    public BundleSelection Selection => _selection;

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
            // reach for. The movement and creation controls are the host's declaration, so their names go
            // with them: a name the product never declared to the engine is a control nobody can press.
            return _ruleset.CreateSession(new RulesetSessionContext(
                channel,
                _selection,
                _content,
                Engine: _context.Engine,
                Movement: _movement,
                Creation: _creation));
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
