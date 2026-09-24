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
    private IGameSession _session;
    private bool _started;
    private bool _shutdown;

    /// <summary>Creates the product with the host's default compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context)
        : this(context, BuiltInRulesets.Default)
    {
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ruleset);
        _context = context;
        _ruleset = ruleset;
        _input = new SessionInputRouter(ProductIdentity.PauseToggleIntent, ProductIdentity.UiActionContract);
        _session = CreateSession();
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

    /// <summary>Holds the session.</summary>
    public void Pause()
    {
        if (!_started || _shutdown) return;
        _session.Pause();
    }

    /// <summary>Releases a held session.</summary>
    public void Resume()
    {
        if (!_started || _shutdown) return;
        _session.Resume();
    }

    /// <summary>Replaces the session with a freshly built one, keeping the same selected ruleset.</summary>
    public void Restart()
    {
        if (_shutdown) return;
        IGameSession previous = _session;
        _session = CreateSession();
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

    private IGameSession CreateSession() => _ruleset.CreateSession(
        new RulesetSessionContext(
            new EngineUiProjectionChannel(
                _context.Engine.Ui,
                new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract))));
}
