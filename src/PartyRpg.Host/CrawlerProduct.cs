using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Rusty.Engine.Debugging;

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
/// <para>
/// The product registers the Engine's playtest and interaction inspection in the generated debug catalog, which
/// the Engine creates once for the product's whole life. A session is replaced on restart and a world when
/// creation is accepted, so every playtest query resolves the session held at the moment it is asked, and the
/// one interaction selection the inspection reads is the host's, handed to every session it composes.
/// </para>
/// </remarks>
public sealed class CrawlerProduct : IEngineProduct, IDebugCommandModuleSource
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
    private readonly EquipIntentNames _equip;
    private readonly RestIntentNames _rest;
    private readonly ConversationIntentNames _conversation;
    private readonly CombatIntentNames _combat;
    private readonly ControlKeys _keys;
    private readonly BundleSelection _selection;
    private readonly ContentCatalog? _content;
    private readonly bool _showLaunchTitle;
    private readonly EngineUiProjectionChannel _projection;
    private readonly SessionMenuState _menu;
    private readonly InteractionSelection _interaction = new();
    private readonly IReadOnlyList<ProductPlaytest.Binding> _bindings;
    private IGameSession _session;
    private bool _started;
    private bool _shutdown;
    private bool _menuHeldSession;

    /// <summary>Creates the product with the host's default compiled ruleset, started as the process environment says.</summary>
    public CrawlerProduct(ProductCreateContext context)
        : this(context, Environment.GetEnvironmentVariable, showLaunchTitle: true)
    {
    }

    /// <summary>Creates the product with the host's default compiled ruleset, started as the given variables say.</summary>
    /// <param name="context">The Engine's creation context.</param>
    /// <param name="variables">Reads one named variable, or null when it is unset.</param>
    internal CrawlerProduct(ProductCreateContext context, Func<string, string?> variables, bool showLaunchTitle = false)
        : this(context, BuiltInRulesets.Default, BuiltInBundles.Parse(variables(ProductIdentity.BundleVariable)), ProductStart.From(variables), showLaunchTitle)
    {
    }

    /// <summary>Creates the product over an explicitly selected compiled ruleset and bundle.</summary>
    public CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset, string? bundleId, SessionStart start = SessionStart.Fresh)
        : this(context, ruleset, bundleId, start, showLaunchTitle: false)
    {
    }

    private CrawlerProduct(ProductCreateContext context, IGameRuleset ruleset, string? bundleId, SessionStart start, bool showLaunchTitle)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(ruleset);
        _context = context;
        _ruleset = ruleset;
        StartMode = start;
        _showLaunchTitle = showLaunchTitle;
        _input = new SessionInputRouter(ProductIdentity.PauseToggleIntent, ProductIdentity.UiActionContract);
        _movement = new MovementIntentNames(
            ProductIdentity.MoveForwardIntent,
            ProductIdentity.MoveBackIntent,
            ProductIdentity.StrafeLeftIntent,
            ProductIdentity.StrafeRightIntent,
            ProductIdentity.TurnLeftIntent,
            ProductIdentity.TurnRightIntent,
            ProductIdentity.JumpIntent,
            ProductIdentity.AscendIntent,
            ProductIdentity.DescendIntent);
        _creation = new CreationIntentNames(
            ProductIdentity.CreationAdvanceIntent,
            ProductIdentity.CreationAcceptIntent,
            ProductIdentity.UiActionContract);
        _save = new SaveIntentNames(
            ProductIdentity.SaveIntent,
            ProductIdentity.UiActionContract);
        _use = new UseIntentNames(
            ProductIdentity.UseIntent,
            ProductIdentity.UiActionContract,
            ProductIdentity.NextTargetIntent);
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
        _skills = new SkillRaiseIntentNames(ProductIdentity.UiActionContract);
        // Casting is two payload actions and no key, for the same reason: a spell, a caster, and a target are
        // what a screen's own rows name, and one press of a key could say none of them. The quick slot is the
        // second action, because which spell a character keeps there is a choice the spellbook screen makes.
        _cast = new CastIntentNames(ProductIdentity.UiActionContract);
        // Mixing is one payload action and no key, for the same reason casting is two: which two of the things
        // the party carries a player put together is what the pack screen's own rows name.
        _mix = new MixIntentNames(ProductIdentity.UiActionContract);
        // Putting something on and taking it off are payload actions and no key, for the same reason: which member
        // and which of the things the party carries are what the figure screen's own rows name.
        _equip = new EquipIntentNames(ProductIdentity.UiActionContract);
        // The act control is one intent and one action, because the act is one act: what a member does with
        // it is the ruleset's answer about that member, and a player presses the same control for a spell, a
        // shot, or a swing. The pace controls travel with it, because a paced fight is the same fight: one
        // control switches the pacing, and skipping and waiting each have their own because their
        // consequences differ.
        _combat = new CombatIntentNames(
            ProductIdentity.AttackIntent,
            ProductIdentity.UiActionContract,
            new TurnIntentNames(
                ProductIdentity.TurnBasedToggleIntent,
                ProductIdentity.TurnSkipIntent,
                ProductIdentity.TurnWaitIntent,
                ProductIdentity.UiActionContract),
            nextMember: ProductIdentity.NextMemberIntent);
        // Which key each control is bound to is the project file's declaration, handed back by the engine: the
        // panel names those keys and no others.
        _keys = ProductControlKeys.Read(context.Input);
        _bindings = ProductPlaytest.Bindings(context.Input);
        (_selection, _content) = SelectBundle(context, bundleId ?? BuiltInBundles.Default, ruleset.Id);
        _menu = new SessionMenuState();
        if (_showLaunchTitle)
        {
            _menu.ShowTitle(
                canNewGame: _selection.Unavailable is null,
                canContinue: context.Engine is not null,
                message: _selection.Unavailable is { } missing
                    ? _selection.Setup.Length > 0
                        ? _selection.Setup
                        : $"The selected bundle '{missing}' is missing {_selection.MissingPacks.Count} content pack(s)."
                    : string.Empty,
                code: _selection.Unavailable is null ? string.Empty : "content-missing",
                state: _selection.Unavailable is null ? "none" : "setup");
        }
        // One product owns one stream for its whole lifetime. Replacement sessions publish through this same
        // channel so the Engine's sequence remains monotonic for the browser binding.
        _projection = new EngineUiProjectionChannel(
            context.Engine!.Ui,
            new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract));
        try
        {
            _session = CreateSession();
        }
        catch
        {
            _projection.Dispose();
            throw;
        }
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
    /// no selection instead: a checkout whose packs have not been generated yet still runs, and when the bundle
    /// it asked for names packs that are absent the selection carries which ones and the bundle's own setup
    /// guidance, which the session shows in place of a world.
    /// </remarks>
    private static (BundleSelection Selection, ContentCatalog? Content) SelectBundle(ProductCreateContext context, string bundleId, RulesetId ruleset)
    {
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductIdentity.ContentDirectory),
            bundleId,
            ruleset);
        if (!bootstrap.IsValid)
        {
            throw new ContentValidationException(
                $"{ProductIdentity.ProductTitle} cannot start: {bootstrap.Issues[0]}",
                bootstrap.Issues);
        }

        if (bootstrap.Selection is { } selection)
            return (new BundleSelection(selection.Bundle.BundleId, selection.Packs.Count), bootstrap.Catalog);
        return bootstrap.Missing is { } missing
            ? (BundleSelection.Missing(missing), null)
            : (BundleSelection.None, null);
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
        // A process-launched fresh run opens on the title menu. The creation/session owners are already composed
        // behind it, so selecting New Game reveals that same flow without a second session or update loop. An
        // environment-requested resume keeps its established direct-start behavior for operator automation.
        if (StartMode == SessionStart.Resume || !_showLaunchTitle) _menu.ShowAdventure();
        _session.Start();
        if (_showLaunchTitle && StartMode == SessionStart.Fresh && _session.Mode == SessionMode.Running) _session.Hold();
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
        previous.DisposeForReplacement();
        if (_started)
        {
            _session.Start();
            if (_showLaunchTitle && StartMode == SessionStart.Fresh &&
                _menu.Snapshot.Screen == SessionMenuScreen.Title && _session.Mode == SessionMode.Running)
                _session.Hold();
        }
    }

    /// <summary>Shuts the session down and stops responding to the engine.</summary>
    public void Shutdown()
    {
        if (_shutdown) return;
        _shutdown = true;
        _session.Dispose();
        _projection.Dispose();
    }

    /// <inheritdoc />
    public void Dispose() => Shutdown();

    /// <summary>
    /// Registers the Engine's playtest and interaction inspection over this product's live state.
    /// </summary>
    /// <remarks>
    /// The playtest module answers <c>playtest.observe</c>, <c>playtest.action</c>, and <c>playtest.look</c> from the
    /// session held when each is asked. The interaction module reads the host's one selection, which the live
    /// world aims through, so <c>interaction.inspect</c> lists what the party could face and which one it does;
    /// targeted use is off, because a use reaches the world only through the party's own use control. A module
    /// the catalog refuses stops the product here by name: a debug surface that silently lacks a command would
    /// send a live check back to scraping the panel without saying why.
    /// </remarks>
    /// <param name="registrar">The generated catalog's registration surface.</param>
    /// <exception cref="InvalidOperationException">The catalog refused a module.</exception>
    public void RegisterDebugCommands(IDebugCommandModuleRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        Require(registrar.Register(ProductPlaytest.Module(_bindings, () => _session)));
        Require(registrar.Register(new InteractionDebugModule(_interaction.Interaction)));

        static void Require(DebugCommandRegistrationResult registration)
        {
            if (!registration.Succeeded) throw new InvalidOperationException(registration.Message);
        }
    }

    /// <summary>
    /// Applies this update's input to the session and then advances it. Input is settled first so a
    /// hold issued in this update is already in force when the session steps, and the projection it
    /// publishes describes the session the player just put it in.
    /// </summary>
    public ProductUpdateResult Update(ProductUpdate update)
    {
        if (!_started || _shutdown) return ProductUpdateResult.None;
        bool menuOwnedAtStart = _menu.Snapshot.Screen != SessionMenuScreen.Adventure;
        _menu.SetControlsOwnedForUpdate(menuOwnedAtStart);
        try
        {
            // A lifecycle action owns the update even when it closes the screen it arrived on. This keeps the
            // activating Enter/click from also reaching the creation flow or the world underneath it.
            if (HandleMenu(update)) _menu.SetControlsOwnedForUpdate(true);
            if (!_menu.ControlsOwnedForUpdate) _input.Apply(_session, update.Input);
            return _session.Update(update);
        }
        finally
        {
            _menu.SetControlsOwnedForUpdate(false);
        }
    }

    private IGameSession CreateSession() => CreateSession(StartMode);

    private IGameSession CreateSession(SessionStart start)
    {
        // The engine's own services go to the ruleset whole: composing movement needs the spatial
        // service to walk in and the content owner to retain a place's collision artifact, and the
        // product is the only place that holds the engine context the ruleset would otherwise have to
        // reach for. The movement, creation, save, use, and service controls are the host's declaration,
        // so their names go with them: a name the product never declared to the engine is a control
        // nobody can press, and a service screen whose commands arrive on an undeclared contract is a
        // screen that cannot be driven.
        RulesetSessionContext context = new(
            _projection,
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
            Mix: _mix,
            Keys: _keys,
            Interaction: _interaction,
            Equip: _equip,
            Menu: _menu,
            OwnProjection: false);

        // The start switch travels with the context and is answered at the ruleset's one composition
        // entry: a resumed run reads the save the slot holds and composes a session from it, and a slot
        // that holds nothing fails by name rather than starting a new expedition in place of the one
        // that was asked for.
        return _ruleset.CreateSession(context with { Start = start });
    }

    /// <summary>Handles the visible title/menu lifecycle actions at the Host seam.</summary>
    private bool HandleMenu(ProductUpdate update)
    {
        ActionInbox input = new(update.Input);
        IReadOnlyList<UiAction> actions = input.Take(ProductIdentity.UiActionContract, SessionMenuActions.IsMenuAction);
        foreach (UiAction action in actions)
        {
            switch (action.Name)
            {
                case SessionMenuActions.NewGame when _menu.Snapshot.Screen == SessionMenuScreen.Title:
                    BeginNewGame();
                    break;
                case SessionMenuActions.Continue when _menu.Snapshot.Screen == SessionMenuScreen.Title:
                    Continue();
                    break;
                case SessionMenuActions.ReturnTitle when _menu.Snapshot.Screen is SessionMenuScreen.Adventure or SessionMenuScreen.SaveLoad:
                    RequestReturnToTitle();
                    break;
                case SessionMenuActions.ConfirmReturnTitle when _menu.Snapshot.Screen == SessionMenuScreen.ConfirmReturn:
                    ConfirmReturnToTitle();
                    break;
                case SessionMenuActions.CancelReturnTitle when _menu.Snapshot.Screen == SessionMenuScreen.ConfirmReturn:
                    CancelReturnToTitle();
                    break;
                case SessionMenuActions.OpenSaveLoad when _menu.Snapshot.Screen == SessionMenuScreen.Adventure:
                    OpenSaveLoad();
                    break;
                case SessionMenuActions.CloseSaveLoad when _menu.Snapshot.Screen == SessionMenuScreen.SaveLoad:
                    CloseSaveLoad();
                    break;
                case SessionMenuActions.Load when _menu.Snapshot.Screen == SessionMenuScreen.SaveLoad:
                    RequestLoad();
                    break;
                case SessionMenuActions.ConfirmLoad when _menu.Snapshot.Screen == SessionMenuScreen.ConfirmLoad:
                    Continue(fromSaveMenu: true);
                    break;
                case SessionMenuActions.CancelLoad when _menu.Snapshot.Screen == SessionMenuScreen.ConfirmLoad:
                    ShowSaveLoad();
                    break;
                case SessionMenuActions.CancelOverwrite when _menu.Snapshot.Screen == SessionMenuScreen.ConfirmOverwrite:
                    ShowSaveLoad();
                    break;
            }
        }

        // A save key or the save action while the explicit screen is open follows the same overwrite decision
        // as the screen's own button. The action is deliberately left for SaveRequests after this state change:
        // on the first press the session sees ConfirmOverwrite and does not write, while the confirming press
        // sees SaveLoad and reaches the same canonical save boundary.
        if (_menu.Snapshot.Save.Present &&
            (_menu.Snapshot.Screen is SessionMenuScreen.SaveLoad or SessionMenuScreen.ConfirmOverwrite))
        {
            bool requested = input.Activated(System.Text.Encoding.UTF8.GetBytes(_save.Intent)) || input.Take(
                ProductIdentity.UiActionContract,
                name => name is SaveActions.Save or SaveActions.MenuSave).Count > 0;
            if (requested)
            {
                if (_menu.Snapshot.Screen == SessionMenuScreen.SaveLoad)
                    _menu.ShowOverwriteConfirmation();
                else
                    _menu.ShowSaveLoad(_session.Inspect().Save.Dirty);
            }
        }

        // A menu action was claimed even when its current screen did not accept it. The update still belongs to
        // the visible menu, so callers cannot pair a stale lifecycle action with a gameplay or creation shortcut.
        return actions.Count > 0 || _menu.Snapshot.Screen != SessionMenuScreen.Adventure;
    }

    private void BeginNewGame()
    {
        _menu.ShowAdventure();
        if (_session.Mode == SessionMode.Stopped)
        {
            ReplaceStopped(SessionStart.Fresh);
            return;
        }

        if (!_started || _session.Mode == SessionMode.Starting) _session.Start();
        if (_session.Mode == SessionMode.Paused) _session.ReleaseHold();
    }

    private void Continue()
        => Continue(fromSaveMenu: false);

    private void Continue(bool fromSaveMenu)
    {
        IGameSession? replacement = null;
        try
        {
            replacement = CreateSession(SessionStart.Resume);
            IGameSession previous = _session;
            _menuHeldSession = false;
            _menu.ShowAdventure();
            _session = replacement;
            replacement = null;
            previous.DisposeForReplacement();
            if (_started) _session.Start();
        }
        catch (SessionSaveException refused)
        {
            replacement?.Dispose();
            if (fromSaveMenu)
            {
                // A failed load never disposes the live expedition. It remains held behind the load screen,
                // with the current save refusal named so the player can cancel, save, or return safely.
                RecordLoadFailure(_session.Inspect(), refused);
                ShowSaveLoad();
                return;
            }

            // The old session remains the live owner until the replacement composes successfully. A failed
            // resume must leave a usable title and session behind even when rebuilding the fresh composition
            // meets the same missing-content or unavailable-resource defect; disposing first would strand the
            // product in Stopped before the title could answer New Game.
            _menu.ShowTitle(
                canNewGame: _selection.Unavailable is null,
                canContinue: _context.Engine is not null,
                state: "failed",
                code: refused.Problems.FirstOrDefault()?.Code ?? "continue-failed",
                message: refused.Message);
            ReplaceStopped(SessionStart.Fresh);
        }
    }

    /// <summary>Opens the save/load screen over the current expedition and reads its slot context once.</summary>
    private void OpenSaveLoad()
    {
        SessionSnapshot current = _session.Inspect();
        try
        {
            SessionSave? saved = _session.ReadSave();
            if (saved is { } document)
                _menu.RecordSaved(document, _session.DescribeSave(document), current.Save.At);
            else
                _menu.RecordEmptySlot(
                    current.Save.Available,
                    current.Save.Slot,
                    current.Save.Available
                        ? $"No saved expedition exists in slot '{current.Save.Slot}'."
                        : "Saving is unavailable: this product has no persistence store.");
        }
        catch (SessionSaveException refused)
        {
            RecordLoadFailure(current, refused);
        }
        catch (InvalidOperationException unavailable)
        {
            _menu.RecordLoadFailure(
                available: false,
                slot: current.Save.Slot,
                code: "save-unavailable",
                message: unavailable.Message);
        }

        ShowSaveLoad();
    }

    /// <summary>Projects a load refusal without claiming that an unavailable or empty slot contains a document.</summary>
    private void RecordLoadFailure(SessionSnapshot current, SessionSaveException refused)
    {
        bool unavailable = refused.Kind == SessionSaveFailure.Unavailable || refused.Problems.Any(problem =>
            problem.Code is SaveCodes.SaveStoreAbsent or SaveCodes.SaveStoreUnopened);
        bool empty = refused.Problems.Any(problem => problem.Code == SaveCodes.SaveSlotEmpty);
        _menu.RecordLoadFailure(
            available: !unavailable && current.Save.Available,
            slot: current.Save.Slot,
            code: refused.Problems.FirstOrDefault()?.Code ?? "load-failed",
            message: refused.Message,
            present: !unavailable && !empty);
    }

    /// <summary>Shows the save/load screen and keeps the Host-applied hold in force.</summary>
    private void ShowSaveLoad()
    {
        _menu.ShowSaveLoad(_session.Inspect().Save.Dirty);
        if (_session.Mode is SessionMode.Running or SessionMode.TurnBased)
        {
            _menuHeldSession = true;
            _session.Hold();
        }
    }

    /// <summary>Closes the save/load screen, releasing only a hold this menu applied.</summary>
    private void CloseSaveLoad()
    {
        _menu.ShowAdventure();
        if (_menuHeldSession)
        {
            _menuHeldSession = false;
            _session.ReleaseHold();
        }
    }

    /// <summary>Requests a load, asking before discarding live changes.</summary>
    private void RequestLoad()
    {
        if (!_menu.Snapshot.Save.Present)
        {
            _menu.RecordLoadFailure(
                _menu.Snapshot.Save.Available,
                _menu.Snapshot.Save.Slot,
                "save-slot-empty",
                $"No saved expedition exists in slot '{_menu.Snapshot.Save.Slot}'.");
            ShowSaveLoad();
            return;
        }

        if (_session.Inspect().Save.Dirty)
        {
            _menu.ShowLoadConfirmation();
            return;
        }

        Continue(fromSaveMenu: true);
    }

    private void RequestReturnToTitle()
    {
        bool unsaved = _session.Inspect().Save.Dirty;
        _menu.ShowReturnConfirmation(unsaved);
        _menuHeldSession |= _session.Mode is SessionMode.Running or SessionMode.TurnBased;
        if (_menuHeldSession) _session.Hold();
    }

    private void CancelReturnToTitle()
    {
        _menu.ShowAdventure();
        if (_menuHeldSession)
        {
            _menuHeldSession = false;
            _session.ReleaseHold();
        }
    }

    private void ConfirmReturnToTitle()
    {
        _menuHeldSession = false;
        _menu.ShowTitle(
            canNewGame: _selection.Unavailable is null,
            canContinue: _context.Engine is not null);
        ReplaceStopped(SessionStart.Fresh);
    }

    private void ReplaceStopped(SessionStart start)
    {
        // Compose before releasing the current owner. A title/continue failure is recoverable when the old
        // session remains available for the next ordinary menu action; disposing it before this call made a
        // second composition error turn a visible refusal into a stopped product.
        IGameSession replacement = CreateSession(start);
        IGameSession previous = _session;
        _session = replacement;
        if (previous.Mode != SessionMode.Stopped) previous.DisposeForReplacement();
        if (start == SessionStart.Resume || (_started && _menu.Snapshot.Screen == SessionMenuScreen.Adventure)) _session.Start();
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

    /// <summary>The start mode the declared switch selects.</summary>
    /// <param name="variables">Reads one named variable, or null when it is unset.</param>
    internal static SessionStart From(Func<string, string?> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        return Parse(variables(ProductIdentity.StartVariable));
    }
}
