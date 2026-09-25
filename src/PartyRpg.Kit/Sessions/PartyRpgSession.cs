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
/// The shell deliberately owns no gameplay. The world, the party, and the clock are composed elsewhere and
/// handed here; what exists here is the lifecycle those mechanisms step inside, and the one place that
/// decides what a mode means for stepping. It holds the party and the clock for as long as it lives, and
/// releases both with itself.
/// </remarks>
public sealed class PartyRpgSession : IGameSession
{
    private readonly SessionComposition _composition;
    private readonly IUiProjectionChannel _projection;
    private readonly MovementInput? _movementInput;
    private readonly GameClock? _clock;
    private readonly PartyEntity? _party;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly ISessionSaveStore? _saveStore;
    private readonly SessionSaveBoundary? _saves;
    private SessionMode _mode = SessionMode.Starting;
    private double _simulationSeconds;
    private ulong _admittedSteps;
    private ulong _updates;
    private ulong? _accountedThroughStep;
    private WorldSnapshot _world = WorldSnapshot.Empty;
    private bool _started;
    private bool _enginePaused;
    private bool _held;
    private bool _disposed;

    /// <summary>Creates a session for a compiled ruleset over the mechanisms the kit supplies.</summary>
    /// <param name="composition">The identity the session presents.</param>
    /// <param name="projection">Where it publishes its presentation.</param>
    /// <param name="world">The live world it steps, when content supplied one.</param>
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
    public PartyRpgSession(
        SessionComposition composition,
        IUiProjectionChannel projection,
        SessionWorld? world = null,
        MovementInput? movementInput = null,
        GameClock? clock = null,
        PartyEntity? party = null,
        IDiagnosticsService? diagnostics = null,
        ISessionSaveStore? saveStore = null,
        string saveSlot = SessionSaveBoundary.DefaultSlot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composition.Title);
        _composition = composition;
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _movementInput = movementInput;
        _clock = clock;
        _party = party;
        _diagnostics = diagnostics;
        _saveStore = saveStore;
        _saves = saveStore is null ? null : new SessionSaveBoundary(saveStore, saveSlot);
        LiveWorld = world;
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

    /// <summary>The live world this session steps, or null when content supplied none.</summary>
    public SessionWorld? LiveWorld { get; }

    /// <summary>The session's one game clock, or null when its ruleset composed none.</summary>
    public GameClock? Clock => _clock;

    /// <summary>The party the session holds, or null when content supplied nothing to create one from.</summary>
    public PartyEntity? Party => _party;

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
        if (LiveWorld is { } world)
        {
            if (world.AdvanceTime().Count > 0) Publish();
            else if (world.Snapshot != _world) Publish();
            _world = world.Snapshot;
        }

        return ProductUpdateResult.None;
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
        PartySnapshot.From(_party));

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
