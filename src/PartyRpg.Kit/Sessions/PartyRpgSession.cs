using PartyRpg.Kit.Input;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Time;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

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
/// </remarks>
public sealed class PartyRpgSession : IGameSession
{
    private readonly SessionComposition _composition;
    private readonly IUiProjectionChannel _projection;
    private readonly MovementInput? _movementInput;
    private readonly CreationInput? _creationInput;
    private readonly GameClock? _clock;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly ISessionSaveStore? _saveStore;
    private readonly SessionSaveBoundary? _saves;
    private SessionCreation? _creation;
    private PartyRefusal? _creationRefusal;
    private bool _accepted;
    private SessionMode _mode = SessionMode.Starting;
    private double _simulationSeconds;
    private ulong _admittedSteps;
    private ulong _updates;
    private ulong? _accountedThroughStep;
    private SessionWorld? _liveWorld;
    private PartyEntity? _party;
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
        SessionCreation? creation = null)
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
        _creationInput = creationInput;
        _creation = creation;
        _clock = clock;
        _party = party;
        _diagnostics = diagnostics;
        _saveStore = saveStore;
        _saves = saveStore is null ? null : new SessionSaveBoundary(saveStore, saveSlot);
        _liveWorld = world;
        _world = world?.Snapshot ?? WorldSnapshot.Empty;
        world?.Populate();
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
        StepParty(update.Input, seconds);
        StepClock(seconds);

        Advance(tick);

        // The world advances with the same admitted time the session measures: one clock, one update. The
        // day boundary the clock just crossed is what its places are restored against, so a crossing
        // reaches respawn here rather than through a schedule of the world's own.
        if (_liveWorld is { } world)
        {
            if (world.AdvanceTime().Count > 0) Publish();
            else if (world.Snapshot != _world) Publish();
            _world = world.Snapshot;
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
    /// Advances the one clock by the interval this update admitted, and hands on what the advance crossed
    /// and brought due.
    /// </summary>
    /// <remarks>
    /// The clock returns its effects rather than publishing them, so this is where they reach their owners:
    /// the boundaries it crossed are measured in whole game days, and the world's places are brought up to
    /// the day the clock now stands on in this same update. A deadline is the one effect with no owner yet
    /// — nothing in the product schedules an effect, a rest, a restock, or a quest limit — so it is
    /// reported by name instead of being applied or dropped.
    /// </remarks>
    private void StepClock(double admittedSeconds)
    {
        if (_clock is not { } clock || admittedSeconds <= 0) return;
        ClockAdvance advance = clock.AdvanceAdmittedSeconds(admittedSeconds);
        foreach (DeadlineDue due in advance.Due)
        {
            _diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "clock",
                Code: "deadline-due",
                Message: $"Game time reached {due.Fired}, which a deadline of {due.Deadline} was set for; no owner schedules deadlines yet, so nothing acted on it.",
                Correlation: string.Empty));
        }
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
        // it, so the accepted state is a fact about what is being played. A session that did neither — a
        // resumed one — publishes that it is doing neither, rather than an empty creation screen.
        _creation is { } creation
            ? CreationSnapshot.From(creation.Flow, _creationRefusal)
            : _accepted ? CreationSnapshot.OfParty(_party) : CreationSnapshot.None);

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
        return SessionSave.Capture(this);
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

        return _saves.Save(this);
    }
}
