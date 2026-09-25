using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The save controls a host declares, by the names a player's request to save arrives on.
/// </summary>
/// <remarks>
/// <para>
/// The names are data rather than vocabulary, exactly as the movement and creation controls are: the kit
/// claims what a product declares and invents no key of its own, so a product that maps its save control
/// to another key, or names the action differently on its own payload contract, is served by the same
/// reader. A save request is a request and nothing more: what a save contains, where it goes, and what
/// makes it loadable are the persistence owner's rules, and this record only says how the player asks.
/// </para>
/// <para>
/// Two names are needed because a product offers two ways to ask: a digital intent for a key, and one
/// action name on the payload contract the interface already claims its semantic actions on. Both are
/// read inside the one admitted update, so a key and a button ask for exactly the same save.
/// </para>
/// </remarks>
public sealed record SaveIntentNames
{
    /// <summary>Names the controls a save request arrives on.</summary>
    /// <param name="intent">The digital intent that asks the session to save.</param>
    /// <param name="action">The payload action name that asks the session to save.</param>
    /// <param name="actionContract">The payload contract that action arrives on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public SaveIntentNames(string intent, string action, string actionContract)
    {
        Intent = Require(intent, nameof(intent));
        Action = Require(action, nameof(action));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The digital intent that asks the session to save.</summary>
    public string Intent { get; }

    /// <summary>The payload action name that asks the session to save.</summary>
    public string Action { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The save control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// The ordinary session shell: it owns the session's mode, measures the admitted simulation it has
/// consumed, steps the one game clock with that same admitted interval, and publishes one presentation
/// through its projection channel.
/// </summary>
/// <remarks>
/// <para>
/// The shell deliberately owns no gameplay. The world, the party, and the clock are composed elsewhere and
/// handed here; what exists here is the lifecycle those mechanisms step inside, and the one place that
/// decides what a mode means for stepping. It holds the party and the clock for as long as it lives, and
/// releases both with itself.
/// </para>
/// <para>
/// <b>A session either creates its party or holds one.</b> A session composed to create holds the flow and
/// the two factories that end it, steps nothing while it does, and takes the party the factory built — and
/// the world composed for that party — the moment creation is accepted. A session composed with a party
/// plays it, which is what a resumed session and a session whose content fixes the party are. Neither shape
/// is a second party: the session holds at most one, and the one it holds is the one it plays.
/// </para>
/// <para>
/// <b>A save happens when a player asks for one and at no other time.</b> The request arrives on the save
/// controls the host declared, is read from the admitted input the update already carries, and reaches the
/// explicit save boundary before that update steps anything, so the document a save holds describes the
/// session the player was looking at when they asked. Nothing else in this shell writes: not the update,
/// not a mode change, and not the release of the session.
/// </para>
/// </remarks>
public sealed class PartyRpgSession : IGameSession
{
    private readonly SessionComposition _composition;
    private readonly IUiProjectionChannel _projection;
    private readonly MovementInput? _movementInput;
    private readonly InteractionUseInput? _useInput;
    private readonly CreationInput? _creationInput;
    private readonly ServiceInput? _serviceInput;
    private readonly IServiceRule? _serviceRule;
    private readonly RestInput? _restInput;
    private readonly IRestRule? _restRule;
    private readonly GameClock? _clock;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly ISessionSaveStore? _saveStore;
    private readonly SessionSaveBoundary? _saves;
    private readonly byte[]? _saveIntent;
    private readonly string? _saveAction;
    private readonly byte[]? _saveActionContract;
    private SessionCreation? _creation;
    private PartyRefusal? _creationRefusal;
    private SaveSnapshot _save;
    private bool _accepted;
    private readonly bool _resumed;
    private SessionMode _mode = SessionMode.Starting;
    private double _simulationSeconds;
    private ulong _admittedSteps;
    private ulong _updates;
    private ulong? _accountedThroughStep;
    private SessionWorld? _liveWorld;
    private PartyEntity? _party;
    private PartyResourceLedger? _accounts;
    private PartyServices? _services;
    private PartyRest? _rest;
    private readonly TimeOwners _timeOwners = new();
    private WorldSnapshot _world = WorldSnapshot.Empty;
    private bool _started;
    private bool _enginePaused;
    private bool _held;
    private bool _disposed;

    /// <summary>Creates a session for a compiled ruleset over the mechanisms the kit supplies.</summary>
    /// <param name="composition">The identity the session presents.</param>
    /// <param name="projection">Where it publishes its presentation.</param>
    /// <param name="world">
    /// The live world it steps, when content supplied one and the session holds the party that walks in it.
    /// A session that creates its party is composed without one: its world is composed when the party is
    /// accepted, so the accounts a journey charges are the created party's own.
    /// </param>
    /// <param name="movementInput">
    /// What reads the player's movement controls out of each admitted update, when the ruleset composed
    /// movement and declared where its intents arrive. Without one the session never asks the world to
    /// move the party, which is what a session whose ruleset has no movement does.
    /// </param>
    /// <param name="clock">
    /// The session's one game clock, when its ruleset composed one. It is advanced by the same admitted
    /// interval the movement step covers, and it is the clock the world charges a journey's time to, so
    /// there is one clock in the session and not a second one for travel.
    /// </param>
    /// <param name="party">
    /// The party the session holds, when content supplied what creation needed. The session owns its
    /// lifetime: it publishes the party's facts and disposes it with itself, while the world borrows the
    /// party's accounts to charge a road.
    /// </param>
    /// <param name="diagnostics">
    /// Where the session reports what the clock brought due and nobody acted on, when the engine's
    /// diagnostics are reachable. Nothing schedules a deadline yet, so a report is the only honest thing
    /// that can happen to one.
    /// </param>
    /// <param name="saveStore">
    /// Where this session's saves are written and read, when the product has somewhere to keep them. The
    /// session owns the store and releases it with itself, and a session composed without one still plays:
    /// it simply cannot save, and asking it to says so rather than writing nowhere.
    /// </param>
    /// <param name="saveSlot">The slot this session saves under, when the product names one.</param>
    /// <param name="creationInput">
    /// What reads the creation commands out of each admitted update, when the session is creating a party.
    /// A session that creates without one has no way for a player to choose anything, which is refused
    /// below rather than composed as a screen nobody can drive.
    /// </param>
    /// <param name="creation">
    /// The creation this session holds while a party is being made, when its ruleset offers one. It owns
    /// the flow, the factory that builds the accepted party, and the world that party walks into.
    /// </param>
    /// <param name="saveInput">
    /// The save controls the host declared, when it declared any: the intent and the payload action a
    /// player's request to save arrives on. Without them the session never saves by itself — which is what
    /// a product that offers no save control gets — and the boundary stays reachable only through
    /// <see cref="Save"/>.
    /// </param>
    /// <param name="resumed">
    /// Whether this session was composed from the save already in its slot, which is the host's composition
    /// decision reported to the player rather than a fact this shell could work out for itself: an empty
    /// slot is indistinguishable from a session playing on from one, and a resumed expedition that looked
    /// like a new one would leave the operator unable to tell whether the switch took effect.
    /// </param>
    /// <param name="useInput">
    /// The use controls the host declared, when it declared any: the intent and the payload action a
    /// player's request to use what the party faces arrives on. Without them the session never uses anything
    /// by itself, which is what a product that offers no use control gets; the mechanism is still stepped,
    /// so what the party faces is still published.
    /// </param>
    /// <param name="service">
    /// This game's answers about services, when its ruleset has any. A service target the party talks to
    /// hands off into the mechanism this composes, which browses, transacts, and leaves over the party's own
    /// accounts. Without one the session holds no service mechanism at all, and a use that lands on somebody
    /// keeping a counter quietly opens nothing — which is what a ruleset that has not answered yet gets.
    /// </param>
    /// <param name="accounts">
    /// The party's own accounts as the one settlement path, when the caller composed them. A service charges
    /// and pays through this same ledger, so a shop and a road move one purse; a session that holds a world
    /// borrows the world's own ledger when none is handed here, so the two can never be two ledgers. Without
    /// either, a transaction that moves coin is refused by name rather than settling nowhere.
    /// </param>
    /// <param name="serviceInput">
    /// The service controls the host declared, when it declared any: the intent a request to leave a counter
    /// arrives on, and the payload contract a service screen's commands arrive on. Without them the
    /// mechanism is still composed and a counter can still be entered and browsed, and no command ever
    /// reaches it — which is what a product that declares no service controls gets.
    /// </param>
    /// <param name="rest">
    /// This game's answers about sleeping, camping, waiting, and going without sleep, when its ruleset has
    /// any. The mechanism is composed over the party the session plays, the ledger a journey charges, the
    /// session's one clock, and the world the party stands in, so a night is judged against the place it is
    /// taken in. Without one the session holds no rest mechanism at all, and a stop control quietly does
    /// nothing — which is what a ruleset that has not answered yet gets.
    /// </param>
    /// <param name="restInput">
    /// The stop controls the host declared, when it declared any: the intents a rest, a camp, and each wait
    /// arrive on, and the payload contract a screen's own stop buttons arrive on. Without them the mechanism
    /// is still composed and its schedule still runs, and no stop ever reaches it.
    /// </param>
    /// <exception cref="ArgumentException">
    /// The session is composed both to create a party and to hold one, or to create one without the controls
    /// its commands arrive on.
    /// </exception>
    public PartyRpgSession(
        SessionComposition composition,
        IUiProjectionChannel projection,
        SessionWorld? world = null,
        MovementInput? movementInput = null,
        GameClock? clock = null,
        PartyEntity? party = null,
        IDiagnosticsService? diagnostics = null,
        ISessionSaveStore? saveStore = null,
        string saveSlot = SessionSaveBoundary.DefaultSlot,
        CreationInput? creationInput = null,
        SessionCreation? creation = null,
        SaveIntentNames? saveInput = null,
        bool resumed = false,
        InteractionUseInput? useInput = null,
        IServiceRule? service = null,
        PartyResourceLedger? accounts = null,
        ServiceIntentNames? serviceInput = null,
        IRestRule? rest = null,
        RestIntentNames? restInput = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composition.Title);
        if (creation is not null && (world is not null || party is not null))
        {
            throw new ArgumentException(
                "A session that creates its party owns no other one: the party and the world it walks in are composed when creation is accepted, so handing this session either as well would leave two of them.",
                nameof(creation));
        }

        if (creation is not null && creationInput is null)
        {
            throw new ArgumentException(
                "A session that creates its party needs the controls its commands arrive on; without them nothing could ever choose a portrait, a class, a skill, or a name.",
                nameof(creationInput));
        }

        _composition = composition;
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _movementInput = movementInput;
        _useInput = useInput;
        _creationInput = creationInput;
        _serviceInput = serviceInput is null ? null : new ServiceInput(serviceInput);
        _serviceRule = service;
        _restInput = restInput is null ? null : new RestInput(restInput);
        _restRule = rest;
        _creation = creation;
        _clock = clock;
        _party = party;
        _accounts = accounts ?? world?.Accounts;
        _diagnostics = diagnostics;
        _saveStore = saveStore;
        _saves = saveStore is null ? null : new SessionSaveBoundary(saveStore, saveSlot);
        // The declared save controls are read once, into the exact bytes an admitted event carries, so the
        // reader compares bytes rather than decoding a name on every event of every update.
        _saveIntent = saveInput is null ? null : Encoding.UTF8.GetBytes(saveInput.Intent);
        _saveAction = saveInput?.Action;
        _saveActionContract = saveInput is null ? null : Encoding.UTF8.GetBytes(saveInput.ActionContract);
        _resumed = resumed;
        _save = SaveSnapshot.None(available: _saves is not null, resumed: resumed, slot: saveSlot);
        _liveWorld = world;
        _world = world?.Snapshot ?? WorldSnapshot.Empty;
        world?.Populate();
        // The service mechanism is composed over the party the session plays and the ledger a journey already
        // charges, so a shop and a road settle one purse through one path. A session that creates its party
        // composes it when creation is accepted, which is the moment that party exists. The rest mechanism is
        // composed beside it, over the same party, clock, and world, so a night is judged where it is taken.
        ComposeServices();
        ComposeRest();
        // A session publishes as soon as it exists: the engine expects a create-time projection, and a
        // client that attaches before the first update should see the session it has attached to.
        Publish();
    }

    /// <inheritdoc />
    public SessionMode Mode => _mode;

    /// <summary>Admitted simulation time accumulated while the session was running.</summary>
    public double SimulationSeconds => _simulationSeconds;

    /// <summary>Admitted fixed steps accumulated while the session was running.</summary>
    public ulong AdmittedSteps => _admittedSteps;

    /// <summary>
    /// The live world this session steps, or null while it is creating its party or when content supplied
    /// no places.
    /// </summary>
    public SessionWorld? LiveWorld => _liveWorld;

    /// <summary>The session's one game clock, or null when its ruleset composed none.</summary>
    public GameClock? Clock => _clock;

    /// <summary>
    /// The service mechanism this session serves counters through, or null when its ruleset answered no
    /// service policy or the session holds no party yet. It is the one mechanism every kind of service is
    /// served by, and what the projection publishes about a counter is read from it.
    /// </summary>
    public PartyServices? Services => _services;

    /// <summary>
    /// The rest mechanism this session stops through, or null when its ruleset answered no rest policy or
    /// the session holds no party yet. It is the one mechanism every stop is applied by, and what the
    /// projection publishes about a night is read from it.
    /// </summary>
    public PartyRest? Rest => _rest;

    /// <summary>
    /// The party the session holds, or null while it is creating one and when no content or creation
    /// supplied one. The party a session accepted is the party it plays: nothing else is ever assigned here.
    /// </summary>
    public PartyEntity? Party => _party;

    /// <summary>The creation this session holds while a party is being made, or null once it is playing.</summary>
    public PartyCreationFlow? Creation => _creation?.Flow;

    /// <summary>
    /// The last creation choice the flow refused, or null when creation has refused nothing or the last
    /// choice was accepted.
    /// </summary>
    /// <remarks>
    /// The refusal is kept here rather than thrown: a player who picks a portrait the game does not offer, a
    /// skill their class may not learn, or an attribute past its ceiling gets an answer the screen can show,
    /// and creation stays exactly where it was.
    /// </remarks>
    public PartyRefusal? CreationRefusal => _creationRefusal;

    /// <summary>
    /// The explicit save boundary this session writes and reads through, or null when it was composed
    /// without a save store.
    /// </summary>
    /// <remarks>
    /// Nothing else saves the session: the admitted update, the mode changes, and the release of the session
    /// all write nothing, and a save happens only when a caller asks this boundary for one. A session
    /// composed without a store reports null here, which is what a product with nowhere to persist says
    /// about itself rather than pretending a save landed.
    /// </remarks>
    public SessionSaveBoundary? Saves => _saves;

    /// <summary>Where the party is, or an empty world while the session has no places.</summary>
    public WorldSnapshot World => _world;

    /// <summary>
    /// What the party's movement has done so far, or none while the session has no world to move in.
    /// </summary>
    /// <remarks>
    /// This is where a fall is observable today. It goes to the party's health owner when that owner
    /// exists, which is why it is reported here and applied nowhere.
    /// </remarks>
    public MovementDiagnostics Movement => LiveWorld?.Movement ?? MovementDiagnostics.None;

    /// <summary>Admitted updates this session has consumed, whether or not it was running.</summary>
    public ulong Updates => _updates;

    /// <summary>Whether the player has held the session, which is not the engine's lifecycle pause.</summary>
    public bool IsHeld => _held;

    /// <summary>Whether the engine's lifecycle has paused the session.</summary>
    public bool IsEnginePaused => _enginePaused;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _started = true;
        // A session that has not run yet has no baseline; the first admitted tick establishes one so
        // the engine's runtime-wide step counter never reads as this session's own elapsed time.
        _accountedThroughStep = null;
        ResolveMode();
    }

    /// <inheritdoc />
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_enginePaused) return;
        _enginePaused = true;
        ResolveMode();
    }

    /// <inheritdoc />
    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_enginePaused) return;
        // The engine releasing its pause must not release a hold the player asked for: they are two
        // authorities, and the mode is paused while either of them holds the session.
        _enginePaused = false;
        ResolveMode();
    }

    /// <summary>Holds the session at the player's request.</summary>
    public void Hold()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_held) return;
        _held = true;
        ResolveMode();
    }

    /// <summary>Releases the player's hold.</summary>
    public void ReleaseHold()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_held) return;
        _held = false;
        ResolveMode();
    }

    /// <inheritdoc />
    public void PublishInitial()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Publish();
    }

    /// <inheritdoc />
    public ProductUpdateResult Update(ProductUpdate update)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SessionTick tick = SessionTick.From(update.Facts);

        // A save request is settled before the step it accompanies, exactly as the movement controls are:
        // the player asked at the moment whose projection they were reading, so the document the slot holds
        // describes that moment rather than one tick later. This is the only place an admitted update
        // writes anything, and it writes because a request arrived — never because time passed.
        if (ReadsSaveRequest(update.Input)) _save = AttemptSave();

        // Creation owns no world stepping. While a party is being made, this one admitted update drives the
        // flow and nothing else: the movement reader is not consulted, the world is not stepped, the clock
        // is not advanced, and no admitted interval is measured. Creation is turn-taking, not a second loop
        // — every command it applies arrived in the input of this same update. The update that accepts the
        // party measures nothing either, because it stepped no world: the first interval the world is
        // credited with is the one the update after it steps.
        if (_creation is not null)
        {
            DriveCreation(update.Input);
            _updates++;
            Publish();
            return ProductUpdateResult.None;
        }

        // Input settles before the step it governs, and both happen inside this one admitted update: the
        // intent is read from the events this update carries, the step it produces covers exactly the
        // admitted interval the same update measures, and the clock is advanced by that same interval. The
        // party's motion and the passage of game time are therefore one interval, not two loops, and a
        // session that is not running admits no interval for either of them.
        double seconds = AdmittedSeconds(tick);

        // A service visit owns the player's controls: the counter's screen is what they act on, so the party
        // does not step and nothing is faced while a visit is open, and a party cannot walk away from a shop
        // by holding a key at its menu. The world itself keeps advancing — one clock, one update, and a
        // screen does not pause a real-time world — so the shops that close and the places that respawn do
        // so behind the counter exactly as they do in the street.
        if (_services is not { IsOpen: true })
        {
            StepParty(update.Input, seconds);
            // Using something follows the step that carried the party to it, in the same update: the reticle
            // is refreshed from where the party now stands, and a use the player asked for is applied to what
            // that step put in front of it rather than to what the previous one did.
            Interact(update.Input);
            // A stop is an instant like a use, and the whole period is applied here: the clock the step above
            // moved is moved on by the hours the party slept or waited for, inside this same admitted update.
            DriveRest(update.Input);
        }

        DriveServices(update.Input);
        StepClock(seconds);

        Advance(tick);

        // The world advances with the same admitted time the session measures: one clock, one update. The
        // day boundary the clock just crossed is what its places are restored against, so a crossing
        // reaches respawn here rather than through a schedule of the world's own.
        if (_liveWorld is { } world)
        {
            // What the world stands on now is read once, before anything is published: a snapshot taken after
            // a publish would leave the world's own facts — the place, the pose, and the hours its doors keep
            // — one update behind the clock published beside them, which is exactly how a shop that shut at
            // six would still read open in the projection that shows the clock striking six.
            bool restored = world.AdvanceTime().Count > 0;
            WorldSnapshot live = world.Snapshot;
            bool changed = live != _world;
            _world = live;
            if (restored || changed) Publish();
        }

        return ProductUpdateResult.None;
    }

    /// <summary>
    /// Applies the creation commands this update carried to the flow, in the order they arrived.
    /// </summary>
    /// <remarks>
    /// Every command goes through the flow's own operation and its refusal is recorded rather than thrown,
    /// so an illegal choice is an answer the screen shows and creation stays where it was. An acceptance is
    /// the last command this update acts on: once the party exists there is no flow left to drive, and the
    /// commands behind it in the same update belonged to a screen that has just gone away.
    /// </remarks>
    private void DriveCreation(ReadOnlySpan<ProductInputEvent> input)
    {
        if (_creation is not { } creation || _creationInput is null) return;
        foreach (CreationCommand command in _creationInput.Read(input))
        {
            if (command.Kind == CreationCommandKind.Accept)
            {
                Accept(creation);
                if (_creation is null) return;
                continue;
            }

            _creationRefusal = Apply(creation.Flow, command);
        }
    }

    /// <summary>Makes one creation command on the flow and returns the rule it broke, when it broke one.</summary>
    private static PartyRefusal? Apply(PartyCreationFlow flow, CreationCommand command) => command.Kind switch
    {
        CreationCommandKind.SelectMember => flow.SelectMember(command.Member),
        CreationCommandKind.SelectPortrait => Missing(command, "portrait") ?? flow.SelectPortrait(new PortraitId(command.Value)),
        CreationCommandKind.SelectClass => Missing(command, "class") ?? flow.SelectClass(new ClassId(command.Value)),
        CreationCommandKind.SetName => flow.SetName(command.Value),
        CreationCommandKind.RaiseAttribute => Missing(command, "attribute") ?? flow.RaiseAttribute(new AttributeId(command.Value)),
        CreationCommandKind.LowerAttribute => Missing(command, "attribute") ?? flow.LowerAttribute(new AttributeId(command.Value)),
        CreationCommandKind.ChooseSkill => Missing(command, "skill") ?? flow.ChooseSkill(new SkillId(command.Value)),
        CreationCommandKind.RemoveSkill => Missing(command, "skill") ?? flow.RemoveSkill(new SkillId(command.Value)),
        CreationCommandKind.Advance => flow.Advance(),
        CreationCommandKind.Accept => null,
        _ => null,
    };

    /// <summary>
    /// Refuses a choice command that arrived without the choice it names.
    /// </summary>
    /// <remarks>
    /// The alternative is dropping the command, which is the failure this kit refuses everywhere else: a
    /// control that silently does nothing looks exactly like a control that worked and changed nothing.
    /// </remarks>
    private static PartyRefusal? Missing(CreationCommand command, string choice) =>
        string.IsNullOrWhiteSpace(command.Value)
            ? new PartyRefusal(
                "creation-choice-missing",
                $"A {choice} choice arrived naming no {choice}, so there was nothing to choose; a {choice} is named by the id creation offers it under.")
            : null;

    /// <summary>
    /// Accepts the finished creation: builds the party, composes the world it walks into, and plays it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one moment a session takes a party. The party comes from the factory the ruleset
    /// supplied, over exactly what the flow finished, so a party created here is the same durable shape as
    /// one restored from a save; the world comes from the ruleset too, composed over that same party,
    /// because a road's provisions come out of the party's larder and a world built before its party would
    /// have none to charge.
    /// </para>
    /// <para>
    /// A refusal here leaves creation exactly as it was: an unfinished member is named, and a factory that
    /// refuses what the flow produced is reported as what it is — a defect in the choices the ruleset
    /// offered — rather than leaving a half-created party or an exception inside an admitted update.
    /// </para>
    /// </remarks>
    private void Accept(SessionCreation creation)
    {
        if (!creation.Flow.IsComplete)
        {
            _creationRefusal = new PartyRefusal(
                "creation-incomplete",
                $"The party cannot be accepted while creation is unfinished: {Unfinished(creation.Flow)}.");
            return;
        }

        PartyEntity? party = null;
        SessionWorld? world = null;
        try
        {
            party = creation.BuildParty(creation.Flow.ToCreation());
            world = creation.ComposeWorld(party);
        }
        catch (ArgumentException error)
        {
            // The factory or the world refused what the flow produced. Both are released here: an accepted
            // party that is not played would be a second party, and the session stays in creation so the
            // player can change the choice that produced it.
            world?.Dispose();
            party?.Dispose();
            _creationRefusal = new PartyRefusal(
                "creation-refused",
                $"The finished party was refused: {error.Message}");
            _diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Error,
                DiagnosticsDisposition.RejectedRecoverable,
                Source: "creation",
                Code: "creation-refused",
                Message: _creationRefusal.Message,
                Correlation: string.Empty));
            return;
        }

        _creation = null;
        _creationRefusal = null;
        _accepted = true;
        _party = party;
        _liveWorld = world;
        _world = world?.Snapshot ?? WorldSnapshot.Empty;
        // The place the accepted party starts in is populated here for the same reason the world's own
        // composition does it: the party plays in a place that is already furnished, in the first update
        // that follows rather than one late.
        _liveWorld?.Populate();
        // The ledger the world charges a journey to is the one a shop charges the created party's purse
        // through, and the service mechanism is composed over exactly that pair, so a party that just came
        // into being is served by the same path as one restored from a save.
        _accounts ??= _liveWorld?.Accounts;
        ComposeServices();
        ComposeRest();
        // Leaving creation is a mode change like any other, so it resolves and publishes through the one
        // path that decides what a mode means: the next admitted update steps the world the party is in.
        ResolveMode();
    }

    /// <summary>Names every member still unfinished, which is why a party cannot be accepted yet.</summary>
    private static string Unfinished(PartyCreationFlow flow)
    {
        List<string> pending = [];
        for (int index = 0; index < flow.MemberCount; index++)
        {
            CreationMember member = flow.Member(index);
            if (member.IsComplete) continue;
            pending.Add($"member {index + 1} is at the {member.Step} step");
        }

        return pending.Count > 0 ? string.Join("; ", pending) : "no member is finished";
    }

    /// <summary>The interval this admitted update covers, which is zero for a session that is not running.</summary>
    /// <remarks>
    /// One derivation of the interval, so the movement step and the clock cannot be advanced by different
    /// amounts: the engine reports how many fixed steps it admitted and how long one is, and a batch this
    /// session cannot measure advances nothing at all.
    /// </remarks>
    private double AdmittedSeconds(SessionTick tick)
    {
        if (_mode != SessionMode.Running) return 0;
        double seconds = tick.AdmittedStepCount * tick.FixedDeltaSeconds;
        return double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
    }

    /// <summary>
    /// Reads the player's movement controls and asks the world to move the party by the admitted interval.
    /// </summary>
    /// <remarks>
    /// A paused session still reads what the player holds — a key released while the session was held must
    /// not keep walking after it resumes — but it never steps, because no admitted time passes for a
    /// session that is not running.
    /// </remarks>
    private void StepParty(ReadOnlySpan<ProductInputEvent> input, double seconds)
    {
        if (_movementInput is null) return;
        MovementIntent intent = _movementInput.Read(input);
        if (seconds <= 0 || LiveWorld is not { } world) return;
        world.Step(intent, seconds);
    }

    /// <summary>
    /// Steps the world's interaction mechanism and applies a use the player asked for, in this same update.
    /// </summary>
    /// <remarks>
    /// The reticle is refreshed whenever the session holds a world, so what the panel shows as faced is what
    /// the last step actually put in front of the party; the use itself happens only when the player asked
    /// for one, on the controls the host declared. A held session steps this too — a use is an instant rather
    /// than an interval, and a lever pulled while the world is held is an act — which is deliberately not how
    /// movement works. The mechanism is the world's, and this is where the one admitted update reaches it.
    /// A use that lands on somebody keeping a counter hands off into the service mechanism, which is the one
    /// way a service is entered: the interaction mechanism reached the person, and what stands behind them is
    /// the service's business rather than a second way to reach a person.
    /// </remarks>
    private void Interact(ReadOnlySpan<ProductInputEvent> input)
    {
        if (LiveWorld is not { } world) return;
        InteractionResult? result = world.Interact(_useInput is not null && _useInput.Read(input));
        if (result is { IsApplied: true, Target: { } target } && _services is { } services)
        {
            services.OpenTarget(target.Id.Place, target.Placement);
        }
    }

    /// <summary>
    /// Applies the stops this update carried, in the order they arrived, while no counter owns the controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The reader is consulted only while the party is walking, exactly as the movement controls are: a visit's
    /// screen owns the player's controls while it is open, and a stop asked for behind a counter is the
    /// service's business rather than a second way into the same night's sleep.
    /// </para>
    /// <para>
    /// Every stop is applied whole inside this update — a rest is not a state the session holds between
    /// updates — so there is nothing to resume, nothing to interrupt from a later update, and no screen that
    /// could be drawn while the clock is halfway through the night. The report is published with the rest of
    /// the projection and reported to the engine's diagnostics, so what a night cost is visible both on the
    /// panel and in the product's own account of itself.
    /// </para>
    /// </remarks>
    private void DriveRest(ReadOnlySpan<ProductInputEvent> input)
    {
        if (_rest is not { Available: true } rest || _restInput is null) return;
        foreach (RestKind kind in _restInput.Read(input))
        {
            RestResult result = rest.Perform(kind);
            Report(result);
        }
    }

    /// <summary>Reports what one stop did, whether it applied or was refused.</summary>
    private void Report(RestResult result)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "rest",
            Code: result.IsApplied ? "rest-applied" : "rest-refused",
            Message: result.IsApplied
                ? $"The party stopped ({result.Kind}) from {result.From} to {result.To} in place '{_liveWorld?.Place}': {result.Message}"
                : $"The party's stop ({result.Kind}) in place '{_liveWorld?.Place}' was refused ({result.Code}): {result.Message}",
            Correlation: string.Empty));
    }

    /// <summary>
    /// Applies the service commands this update carried, in the order they arrived, while a visit is open.
    /// </summary>
    /// <remarks>
    /// The reader is consulted only while a counter is open, exactly as the creation reader is consulted only
    /// while a party is being made: the commands belong to a screen, and one that arrives with no counter
    /// open names nothing this session is doing. Each command goes through the mechanism's own operation and
    /// its refusal is recorded rather than thrown, so a purchase the purse cannot cover is an answer the
    /// screen shows and the visit stays exactly where it was.
    /// </remarks>
    private void DriveServices(ReadOnlySpan<ProductInputEvent> input)
    {
        if (_services is not { IsOpen: true } services || _serviceInput is null) return;
        foreach (ServiceCommand command in _serviceInput.Read(input)) services.Transact(command);
    }

    /// <summary>
    /// Advances the one clock by the interval this update admitted, and hands on what the advance crossed
    /// and brought due.
    /// </summary>
    /// <remarks>
    /// The clock returns its effects rather than publishing them, so this is where they reach their owners:
    /// the boundaries it crossed are measured in whole game days, and the world's places are brought up to
    /// the day the clock now stands on in this same update. The advance reaches the service mechanism before
    /// anything is reported, so a shelf whose refresh deadline came due is filled in the update that reached
    /// it. A deadline is still reported by name, because the report is how a deadline nothing owns is
    /// visible: one a schedule acted on says so, and one no owner holds says that too rather than being
    /// dropped.
    /// </remarks>
    private void StepClock(double admittedSeconds)
    {
        if (_clock is not { } clock || admittedSeconds <= 0) return;
        ClockAdvance advance = clock.AdvanceAdmittedSeconds(admittedSeconds);
        // Every owner that keeps something against game time hears the same advance, in one place: a shelf
        // whose refresh came due is filled by the service mechanism, and a debt of sleep that the interval
        // ran past lands on the party, in the update that moved the clock.
        _timeOwners.Observe(advance);
        foreach (DeadlineDue due in advance.Due)
        {
            bool owned = (_services?.Holds(due.Deadline) ?? false) || (_rest?.Holds(due.Deadline) ?? false);
            string message = owned
                ? $"Game time reached {due.Fired}, which a deadline of {due.Deadline} was set for; the owner that scheduled it acted on it."
                : $"Game time reached {due.Fired}, which a deadline of {due.Deadline} was set for; no owner schedules deadlines yet, so nothing acted on it.";
            _diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "clock",
                Code: "deadline-due",
                Message: message,
                Correlation: string.Empty));
        }
    }

    /// <summary>
    /// Composes the service mechanism over the party the session plays, when the ruleset answered for one.
    /// </summary>
    /// <remarks>
    /// It is composed once, when the party exists: a session that creates its party has none until creation
    /// is accepted, and the ledger a journey charges is the one a shop charges because both are composed over
    /// that same party. A session with no ledger still gets the mechanism — it can browse and be refused by
    /// name — which is better than a counter that is not there because nobody handed over the accounts.
    /// </remarks>
    private void ComposeServices()
    {
        if (_services is not null || _serviceRule is null || _party is not { } party) return;
        _services = new PartyServices(_serviceRule, party, _accounts, _clock);
        // The world hands its journey advances to the same owners the session's admitted intervals reach, so a
        // shelf's deadline is driven by the one clock wherever the advance happened. The world is told here
        // because this is the moment the mechanism exists, which for a session that creates its party is when
        // creation is accepted.
        ObserveTimeWith(_services);
    }

    /// <summary>
    /// Composes the rest mechanism over the party the session plays, when the ruleset answered for one.
    /// </summary>
    /// <remarks>
    /// It is composed once, when the party exists, and over the world the party stands in — so whether a
    /// night may be taken here is read from the live place rather than from a place the session remembered.
    /// The mechanism joins the owners the one clock reports to, which is what makes a debt of sleep fall due
    /// on a journey exactly as it does in an update; the service mechanism is its own onward owner, so a
    /// rest's hours refresh a shelf without either mechanism hearing the advance twice.
    /// </remarks>
    private void ComposeRest()
    {
        if (_rest is not null || _restRule is null || _party is not { } party) return;
        _rest = new PartyRest(_restRule, party, _clock, _liveWorld, _accounts, onward: _services);
        ObserveTimeWith(_rest);
    }

    /// <summary>Tells the world and this session about one more owner of the session's game time.</summary>
    private void ObserveTimeWith(IGameTimeObserver owner)
    {
        _timeOwners.Add(owner);
        _liveWorld?.ObserveTimeWith(_timeOwners);
    }

    /// <summary>
    /// Consumes one admitted tick. Only a running session advances: a held or engine-paused session
    /// keeps receiving admitted updates, and must publish the frozen state it holds rather than the
    /// engine's advancing step counter.
    /// </summary>
    public void Advance(SessionTick tick)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _updates++;
        if (_mode == SessionMode.Running)
        {
            // The engine reports a batch as its first step plus the number of steps it admitted, so a
            // batch covers [SimulationStep, SimulationStep + AdmittedStepCount). Accounting by batch
            // end is what makes the published seconds and the published step count describe the same
            // simulation; accounting by batch starts credits the previous batch and never the one in
            // flight.
            ulong batchStart = tick.SimulationStep;
            ulong batchEnd = batchStart + tick.AdmittedStepCount;
            // After a hold, the first running tick re-establishes the baseline, so the interval the
            // session was held is never credited to it.
            _accountedThroughStep ??= batchStart;
            _simulationSeconds += (batchEnd - _accountedThroughStep.Value) * tick.FixedDeltaSeconds;
            _accountedThroughStep = batchEnd;
            _admittedSteps += tick.AdmittedStepCount;
        }

        Publish();
    }

    /// <summary>Stops the session, publishes the stop, and releases the projection channel.</summary>
    /// <remarks>
    /// The party and the world are released here because the session is what holds them: a party that
    /// outlived its session would be a second live party, and the world owns the engine scene it walks in.
    /// The stop is published before either is released, because the projection reads the party's accounts
    /// and the party's store is what disposing it takes away. Nothing is saved at this point: releasing a
    /// session is not a save, and a caller that wanted one asked for it while the session was live.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mode = SessionMode.Stopped;
        // Publish before releasing the channel: a client attached at shutdown should learn that the
        // session stopped instead of keeping the last running projection forever.
        Publish();
        LiveWorld?.Dispose();
        _party?.Dispose();
        _saveStore?.Dispose();
        _projection.Dispose();
    }

    private void ResolveMode()
    {
        SessionMode next = _disposed
            ? SessionMode.Stopped
            : !_started
                ? SessionMode.Starting
                // A session with a party to make is creating it: neither an engine pause nor a player's hold
                // takes it out of creation, because creation steps nothing that either of them would stop,
                // and the flow must keep taking the player's choices while the world is held.
                : _creation is not null
                    ? SessionMode.Creating
                    : _enginePaused || _held ? SessionMode.Paused : SessionMode.Running;

        if (next == _mode) return;
        _mode = next;
        // Stepping stops and resumes with the mode, so the held interval is never measured.
        if (_mode == SessionMode.Paused) _accountedThroughStep = null;
        Publish();
    }

    private void Publish() => _projection.Publish(SessionProjection.Build(Snapshot()));

    private SessionSnapshot Snapshot() => new(
        _composition,
        _mode,
        _simulationSeconds,
        _admittedSteps,
        _updates,
        _world,
        MovementSnapshot.From(LiveWorld?.Movement.Last),
        ClockSnapshot.From(_clock),
        PartySnapshot.From(_party),
        // While a party is being made the flow is the screen's whole subject; once one has been accepted the
        // members shown are the party's own, read from the party rather than from the flow that described
        // it, so the accepted state is a fact about what is being played. A resumed session plays a party it
        // did not create in this run and publishes the same list: the roster a player reads after resuming
        // is the party they led, read from the party rather than recalled from a flow that no longer exists.
        // A session that holds no party at all publishes that it is doing neither.
        _creation is { } creation
            ? CreationSnapshot.From(creation.Flow, _creationRefusal)
            : _accepted || _resumed ? CreationSnapshot.OfParty(_party) : CreationSnapshot.None,
        _save,
        // What the party faces and what using it did, read from the world's own mechanism: a session with no
        // world, or one whose ruleset answered no interaction policy, publishes that it holds none rather
        // than an empty reticle that looks like an empty room.
        InteractionSnapshot.From(LiveWorld?.Interaction),
        // What the party is doing at a service, read from the service mechanism the ruleset's answers
        // composed: a session with no mechanism, one that stands at no counter, and one whose counter is
        // shut are three different facts the panel must be able to tell apart.
        ServiceSnapshot.From(_services),
        // What the party's last stop did and what it cost, read from the rest mechanism beside it: a session
        // with no mechanism, one that has not stopped yet, and one whose night was refused are three
        // different facts, and the fatigue debt the clock is holding is published with them.
        RestSnapshot.Read(_rest, _clock));

    /// <summary>Publishes the world as it stands now, after a caller moved the party.</summary>
    public void PublishWorld()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (LiveWorld is not { } world) return;
        _world = world.Snapshot;
        Publish();
    }

    /// <summary>
    /// Reads this session into the product's one current save schema, without writing anything.
    /// </summary>
    /// <remarks>
    /// This is a read of the party, the clock, and the world, and it changes none of them: a capture taken at
    /// any point describes the session as it stood then, and playing on cannot alter the document it
    /// produced. A session with nothing to save — no party, no clock, or no world — is refused by name here
    /// rather than captured as an empty shell.
    /// </remarks>
    /// <returns>The session as a save records it.</returns>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or the clock is holding scheduled work.</exception>
    public SessionSave Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return InCapture(() => SessionSave.Capture(this));
    }

    /// <summary>
    /// Writes this session at its explicit save boundary, and returns the document written.
    /// </summary>
    /// <remarks>
    /// A save happens here and only here: this is the call a product makes at the point it decides is worth
    /// remembering, and no update, mode change, or shutdown writes one by itself.
    /// </remarks>
    /// <returns>The document that was written.</returns>
    /// <exception cref="InvalidOperationException">The session was composed without a save store.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, the clock is holding scheduled work, or the write failed.</exception>
    public SessionSave Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_saves is null)
        {
            throw new InvalidOperationException(
                $"The session in '{_composition.Title}' was composed without a save store, so there is nowhere to write a save; a session is composed with one when the product has a place to keep them.");
        }

        return InCapture(() => _saves.Save(this));
    }

    /// <summary>
    /// Saves this session because the player asked for one, and reports what happened.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the entry point a save request reaches, whether it arrived on the declared save controls
    /// inside an admitted update or from a caller that decided this moment is worth remembering. Unlike
    /// <see cref="Save"/>, which fails loudly because a caller that asked for a write must not mistake a
    /// refusal for one, this records the outcome in the session's own save state and publishes it: a
    /// refusal is an answer the panel shows, and a save request that could not land must not look like one
    /// that did.
    /// </para>
    /// <para>
    /// Every failure keeps its own name. A session composed without a store says so; a session holding
    /// nothing a load could rebuild reports the boundary's own list of what is missing; and a write the
    /// store refused reports the store's reason. None of them is thrown out of an admitted update.
    /// </para>
    /// </remarks>
    /// <returns>The save state after the request, which is what the projection now publishes.</returns>
    public SaveSnapshot RequestSave()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _save = AttemptSave();
        Publish();
        return _save;
    }

    /// <summary>
    /// Saves this session if it can, and states what happened without publishing it.
    /// </summary>
    /// <remarks>
    /// A save request read from an admitted update comes through here rather than through
    /// <see cref="RequestSave"/>, because that update publishes its own projection once at the end and the
    /// outcome is part of it. The failure of a save is not an exception: the request arrived from a player
    /// rather than from a caller that demanded a write, so every way it can fail is an outcome to report.
    /// </remarks>
    private SaveSnapshot AttemptSave()
    {
        if (_saves is null)
        {
            // The host selects the persistence root before the product is created, so a session composed
            // without a store is a product that plays but cannot write. That is named here rather than
            // written nowhere, and it is a failure like any other so the panel can show it.
            return _save with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = "save-unavailable",
                Message = $"The session in '{_composition.Title}' cannot be saved: it was composed without a save store, so there is nowhere to write one. The host selects the persistence root before the product is created.",
            };
        }

        try
        {
            InCapture(() => _saves.Save(this));
        }
        catch (EngineCallException error)
        {
            // The engine's own service refused the write after its store was open — a root that went away,
            // bytes the storage would not take. That is the same loss as any other failed write, and it is
            // reported here rather than thrown out of the admitted update that carried the request, which
            // would stop the session over a save the player could simply try again.
            return _save with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = "save-failed",
                Message = $"The session could not be written to slot '{_save.Slot}': {error.Message}",
            };
        }
        catch (SessionSaveException error)
        {
            // Two different losses behind one exception type: the session held nothing a load could rebuild,
            // or the store could not hold what it was handed. The first carries the boundary's own list of
            // what is missing; the second carries the store's reason. A player acts on them differently, so
            // they are named differently.
            return _save with
            {
                State = SaveState.Failed,
                At = string.Empty,
                Code = error.Problems.Count > 0 ? "save-refused" : "save-failed",
                Message = error.Message,
            };
        }

        ClockSnapshot clock = ClockSnapshot.From(_clock);
        string at = clock.Present ? $"{clock.Date} {clock.Time}" : string.Empty;
        return _save with
        {
            State = SaveState.Saved,
            At = at,
            Code = string.Empty,
            Message = at.Length > 0
                ? $"Saved the session to slot '{_save.Slot}' at {at}."
                : $"Saved the session to slot '{_save.Slot}'.",
        };
    }

    /// <summary>
    /// Reads the session at its save boundary with every schedule the session owns taken off the one clock.
    /// </summary>
    /// <remarks>
    /// The save schema records the game time a clock has lived through and refuses a clock that is holding a
    /// deadline, because a deadline's number means nothing without the owner that scheduled it. This session
    /// has such an owner — the debt of sleep the rest mechanism keeps — so it takes its own deadline off the
    /// clock for the length of the read and puts it back at the point it was due, whatever the read did. A
    /// save therefore changes nothing about the running session, and the debt the document cannot carry is a
    /// stated loss rather than one hidden by the capture.
    /// </remarks>
    private T InCapture<T>(Func<T> capture)
    {
        _rest?.Suspend();
        try
        {
            return capture();
        }
        finally
        {
            _rest?.Resume();
        }
    }

    /// <summary>
    /// Whether this update's admitted input carries a request to save, on either declared control.
    /// </summary>
    /// <remarks>
    /// A digital event on the declared intent asks, whether it arrived as a physical press or as a direct
    /// interface claim, which carries no edge; a payload on the declared contract asks when it names the
    /// declared action. Anything else, including a malformed payload, carries no request: an input channel
    /// must not throw on hostile bytes, and a caller that receives nothing simply has nothing to apply.
    /// Several requests in one update are one save, because they are one moment.
    /// </remarks>
    private bool ReadsSaveRequest(ReadOnlySpan<ProductInputEvent> input)
    {
        if (_saveIntent is null || _saveActionContract is null || _saveAction is null) return false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (inputEvent.Intent.Span.SequenceEqual(_saveIntent) && IsActivation(inputEvent)) return true;
                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_saveActionContract)) continue;
            if (string.Equals(UiActionPayload.Parse(inputEvent.PayloadData.Span)?.Name, _saveAction, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge; a direct interface
    /// claim is admitted with no edge at all, so its own phase and provenance are what identify it.
    /// </summary>
    private static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;
}
