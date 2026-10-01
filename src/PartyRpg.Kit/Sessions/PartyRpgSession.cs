using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The ordinary session shell: it owns the session's mode, measures the admitted simulation it has consumed,
/// steps the one game clock with that same admitted interval, and publishes one presentation through its
/// projection channel.
/// </summary>
/// <remarks>
/// <para>
/// The shell owns no gameplay. The mechanisms are composed by <see cref="SessionOwners"/> over the party the
/// session plays, the fight is driven by <see cref="CombatDriver"/>, a player's acts are applied by
/// <see cref="SessionActs"/>, and saves go through <see cref="SaveRequests"/>; what exists here is the lifecycle
/// they step inside, the one place that decides what a mode means for stepping, and the order one admitted
/// update applies them in.
/// </para>
/// <para>
/// <b>A session either creates its party or plays one</b> (<see cref="SessionParty"/>). A session composed to
/// create steps nothing while it does and takes the party the factory built — and the world composed for it —
/// the moment creation is accepted; the owners are then composed by the same sequence a session handed its
/// party runs, so the two end with the same mechanisms.
/// </para>
/// <para>
/// <b>A save happens when a player asks for one and at no other time.</b> The request is read from the admitted
/// input and reaches the save boundary before that update steps anything; nothing else here writes.
/// </para>
/// </remarks>
public sealed class PartyRpgSession : IGameSession
{
    private readonly SessionComposition _composition;
    private readonly IUiProjectionChannel _projection;
    private readonly SessionOwners _owners;
    private readonly MovementInput? _movement;
    private readonly SessionActs _acts;
    private readonly CombatDriver _fight;
    private readonly SaveRequests _saves;
    private readonly bool _resumed;
    private readonly HashSet<string> _contracts;
    private readonly ControlKeys _keys;
    private CreationDriver? _creation;
    private bool _accepted;
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

    // What the panel was last sent, and whether an update is under way: an update publishes once, at its end,
    // and only a value that differs from the last one sent.
    private UiValue? _published;
    private bool _updating;

    // The blocks the projection keeps between readings, each beside the owner stamps it was read under.
    private readonly ProjectionReadings _readings = new();

    /// <summary>Creates a session for a compiled ruleset over the mechanisms the kit supplies.</summary>
    /// <param name="composition">The identity the session presents.</param>
    /// <param name="projection">Where it publishes its presentation.</param>
    /// <param name="owners">
    /// The owners the session composes, over its one clock. They are created before the session so a game's
    /// answers can read them when an act arrives, and they belong to this session alone.
    /// </param>
    /// <param name="party">Whether the session plays a party it was handed or creates one.</param>
    /// <param name="rules">The game's answers, grouped by the mechanism each is composed into.</param>
    /// <param name="controls">The controls the host declared.</param>
    /// <param name="saving">Where saves go, when the product has somewhere to keep them.</param>
    /// <exception cref="ArgumentException">The session creates its party and the host declared no creation controls.</exception>
    /// <exception cref="InvalidOperationException">The owners already belong to another session.</exception>
    public PartyRpgSession(
        SessionComposition composition,
        IUiProjectionChannel projection,
        SessionOwners owners,
        SessionParty party,
        SessionRules? rules = null,
        SessionControls? controls = null,
        SessionSaving? saving = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composition.Title);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(party);
        controls ??= SessionControls.None;
        if (party is SessionParty.Creating && controls.Creation is null)
        {
            throw new ArgumentException(
                "A session that creates its party needs the controls its commands arrive on; without them nothing could ever choose a portrait, a class, a skill, or a name.",
                nameof(controls));
        }

        _composition = composition;
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        _owners = owners;
        _movement = controls.Movement;
        SessionRecords? records = (party as SessionParty.Playing)?.Resumed;
        _resumed = records is not null;
        _saves = new SaveRequests(composition.Title, saving, controls.Save, _resumed);
        owners.Bind(rules ?? SessionRules.None, records);
        _acts = new SessionActs(owners, controls);
        _fight = new CombatDriver(owners, controls.Movement, controls.Combat);
        _contracts = Contracts(controls);
        _keys = controls.Keys ?? ControlKeys.None;

        switch (party)
        {
            case SessionParty.Playing playing:
                owners.Take(playing.Party, playing.World, playing.Accounts);
                break;
            case SessionParty.Creating creating:
                _creation = new CreationDriver(creating.Creation, controls.Creation!, owners.Diagnostics);
                owners.Compose();
                break;
        }

        _world = owners.World?.Snapshot ?? WorldSnapshot.Empty;

        // A session publishes as soon as it exists: the engine expects a create-time projection, and a client
        // that attaches before the first update should see the session it has attached to.
        Publish();
    }

    /// <inheritdoc />
    public SessionMode Mode => _mode;

    /// <summary>Admitted simulation time accumulated while the session was running.</summary>
    public double SimulationSeconds => _simulationSeconds;

    /// <summary>Admitted fixed steps accumulated while the session was running.</summary>
    public ulong AdmittedSteps => _admittedSteps;

    /// <summary>Admitted updates this session has consumed, whether or not it was running.</summary>
    public ulong Updates => _updates;

    /// <summary>The live world this session steps, or null while it creates its party or content supplied no places.</summary>
    public SessionWorld? LiveWorld => _owners.World;

    /// <summary>The session's one game clock, or null when its ruleset composed none.</summary>
    public GameClock? Clock => _owners.Clock;

    /// <summary>The party the session plays, or null while it creates one or when content supplied none.</summary>
    public PartyEntity? Party => _owners.Party;

    /// <summary>The service mechanism, or null when the ruleset answered none or no party is held yet.</summary>
    public PartyServices? Services => _owners.Services;

    /// <summary>The progression owner, or null when the ruleset answered none or no party is held yet.</summary>
    public PartyProgression? Progression => _owners.Progression;

    /// <summary>The conversation mechanism, composed with or without a party, or null when the ruleset answered none.</summary>
    public PartyConversations? Conversations => _owners.Conversations;

    /// <summary>The quest owner, or null when the ruleset stated no quests or no party is held yet.</summary>
    public PartyQuests? Quests => _owners.Quests;

    /// <summary>The journal, or null when the ruleset stated none or the session holds no clock.</summary>
    public PartyJournal? Journal => _owners.Journal;

    /// <summary>What the party knows, or null when the ruleset stated no discoveries or there is no clock.</summary>
    public PartyKnowledge? Knowledge => _owners.Knowledge;

    /// <summary>What the party has mapped, or null when the ruleset stated no automap or content carries no maps.</summary>
    public PartyMaps? Maps => _owners.Maps;

    /// <summary>The rest mechanism, or null when the ruleset answered none or no party is held yet.</summary>
    public PartyRest? Rest => _owners.Rest;

    /// <summary>The fight, or null when the ruleset answered no combat policy or no party is held yet.</summary>
    public CombatState? Combat => _owners.Combat;

    /// <summary>The owners the session composed that set deadlines on its clock, which a save asks about each.</summary>
    internal IReadOnlyList<IDeadlineOwner> DeadlineOwners => _owners.DeadlineOwners;

    /// <summary>The creation this session holds while a party is being made, or null once it is playing.</summary>
    public PartyCreationFlow? Creation => _creation?.Flow;

    /// <summary>The last creation choice the flow refused, or null when the last choice was accepted.</summary>
    public Refusal? CreationRefusal => _creation?.Refusal;

    /// <summary>The explicit save boundary, or null when the session was composed without a save store.</summary>
    public SessionSaveBoundary? Saves => _saves.Boundary;

    /// <summary>Where the party is, or an empty world while the session has no places.</summary>
    public WorldSnapshot World => _world;

    /// <summary>What the party's movement has done so far, or none while the session has no world to move in.</summary>
    public MovementDiagnostics Movement => LiveWorld?.Movement ?? MovementDiagnostics.None;

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
        // A session that has not run yet has no baseline; the first admitted tick establishes one so the
        // engine's runtime-wide step counter never reads as this session's own elapsed time.
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
    public ProductUpdateResult Update(ProductUpdate update)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SessionMode before = _mode;
        bool heard;
        _updating = true;
        try
        {
            heard = Consume(update);
        }
        finally
        {
            _updating = false;
        }

        // A session that was held throughout and heard nothing moved nothing any block reads, so it is not even
        // read: the panel already shows what it holds.
        if (heard || before != SessionMode.Paused || _mode != SessionMode.Paused) Publish();
        return ProductUpdateResult.None;
    }

    /// <inheritdoc />
    public SessionSnapshot Inspect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // The world is read live: a look between updates turns the party, and the update's own copy would still
        // show the facing it had before.
        return Snapshot() with { World = LiveWorld?.Snapshot ?? _world };
    }

    /// <inheritdoc />
    /// <remarks>
    /// A look turns the party exactly as its turn controls do — through the pose owner's facing rule — and only
    /// when those controls would: a session that would not step the party for a held turn key does not turn it
    /// for a look either. The party does not look up or down in play, because no control pitches it, so a look
    /// with any pitch is refused rather than leaving the party at a pitch play could never reach. What the party
    /// faces is re-aimed by the next admitted update, as it is after a turn key.
    /// </remarks>
    public Refusal? Look(double yawDegrees, double pitchDegrees)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!double.IsFinite(yawDegrees) || !double.IsFinite(pitchDegrees))
        {
            return new Refusal(PlaytestCodes.LookNotANumber, "A look must turn by a number of degrees.");
        }

        if (pitchDegrees != 0)
        {
            return new Refusal(
                PlaytestCodes.LookNoPitch,
                "The party turns but does not look up or down: no control pitches it in play, so a look is a yaw alone.");
        }

        if (PlaytestReadout.Steering(Inspect()) is { } refused) return refused;
        if (LiveWorld is not { } world) return new Refusal(PlaytestCodes.SteerNoWorld, "The session holds no places, so there is nowhere to turn.");

        // A look turns right for a positive yaw; the party's facing grows as it turns left, which is the sign its
        // own turn-left control drives it by.
        PartyPoseOwner party = world.Party;
        party.Turn(-yawDegrees / 360.0 * party.Facing.UnitsPerTurn, 0);
        return null;
    }

    /// <summary>Applies one admitted update to the session, without publishing anything.</summary>
    /// <returns>Whether the update carried any input at all.</returns>
    private bool Consume(ProductUpdate update)
    {
        SessionTick tick = SessionTick.From(update.Facts);

        // Every payload the update carried is parsed once, here, and each reader takes the actions it acts on;
        // what nobody took is reported when the update is done.
        ActionInbox input = new(update.Input);

        // A save request is settled before the step it accompanies: the player asked at the moment whose
        // projection they were reading, so the document describes that moment rather than one tick later.
        if (_saves.Asked(input)) _saves.Attempt(this, Clock);

        // Creation owns no world stepping: while a party is being made this update drives the flow and nothing
        // else, and measures no interval. The update that accepts the party measures nothing either, because it
        // stepped no world: the first interval credited is the one the update after it steps.
        if (_creation is { } creation)
        {
            if (creation.Drive(input) is { } accepted)
            {
                _creation = null;
                _accepted = true;
                _owners.Take(accepted.Party, accepted.World, accounts: null);
                _world = _owners.World?.Snapshot ?? WorldSnapshot.Empty;
                ResolveMode();
            }

            _updates++;
            ReportUnclaimed(input);
            return true;
        }

        // The pacing control is settled first of all: a press that switches the mode changes what this very
        // update does with the world, so it cannot wait for the update after the one it arrived in.
        TurnControls turn = _fight.ReadTurns(input);
        if (turn.Toggle) _fight.TogglePacing();

        // Input settles before the step it governs: the step covers exactly the admitted interval this update
        // measures, and the clock is advanced by that same interval, so motion and game time are one interval.
        double seconds = AdmittedSeconds(tick);

        // A held session is quiescent: no game time passes and no act is applied. It still reads the keys the
        // player holds, so a key released while it was held does not keep walking when it resumes.
        bool quiescent = _mode == SessionMode.Paused;

        ControlHolder controls = HeldBy();
        bool screenOwnsControls = controls == ControlHolder.Screen;

        // What the player holds is read on every update, whatever owns the controls, so a release is never
        // missed; it is applied only when the party may act.
        MovementIntent intent = _movement?.Read(update.Input) ?? default;
        if (!screenOwnsControls)
        {
            if (controls == ControlHolder.Party && _movement is not null && seconds > 0) LiveWorld?.Step(intent, seconds);

            // A use and a stop are instants rather than intervals, so they follow the step that carried the
            // party to what it faces, in the same update, and still apply while a turn is being taken.
            if (!quiescent)
            {
                _acts.Interact(input);
                _acts.Stop(input);
            }
        }

        FightOrders orders = _fight.Read(input, turn, screenOwnsControls);
        if (!quiescent)
        {
            _acts.Serve(input);
            _acts.Raise(input);
            _acts.Converse(input);
        }

        // Being in somebody's company is a state rather than an edge, so the journal decides that the first
        // report was news.
        if (Conversations is { IsOpen: true, Speaker: { } speaker } conversations)
        {
            Journal?.Record(new JournalEvent(
                JournalEntryKind.Meeting,
                Source: "conversation",
                Subject: speaker.Id,
                Name: speaker.Name.Length > 0 ? speaker.Name : speaker.Id,
                Place: conversations.Place.Value));
        }

        if (Clock is { } clock && seconds > 0) clock.AdvanceAdmittedSeconds(seconds);

        if (LiveWorld is { } world)
        {
            // What the party can see is added to its map once per update, before anything is read from the
            // world: a step, a road taken, and a scripted arrival all put the party somewhere.
            Maps?.Observe(world.Place, world.Party.PlacePose);

            // What the world stands on now is read before anything is published, so the place, the pose, and
            // the hours its doors keep stand beside the clock this update moved.
            world.AdvanceTime();
            WorldSnapshot live = world.Snapshot;
            _world = live;

            // Standing somewhere is a state the world reports, so the quest owner judges an errand that asks for
            // a place, and the journal decides whether arriving there is news.
            if (live.Place.Length > 0)
            {
                Quests?.Observe(new PlaceId(live.Place));
                Journal?.Record(new JournalEvent(
                    JournalEntryKind.Place,
                    Source: "world",
                    Subject: live.Place,
                    Name: live.Name.Length > 0 ? live.Name : live.Place,
                    Place: live.Place));
            }
        }

        // The fight is stepped last of all the world's readers, once everything it reads has moved, so
        // hostility is a fact about where the party is now. Casting and mixing are read in the same breath: a
        // screen that owns the controls owns them too, and what arrived anyway is refused by name.
        if (!quiescent)
        {
            CombatantId? caster = _acts.Cast(input, allowed: !screenOwnsControls);
            _acts.Mix(input, allowed: !screenOwnsControls);
            _acts.Equip(input, allowed: !screenOwnsControls);
            _fight.Step(orders, caster, seconds);
        }

        // The mode is resolved after the fight has had its say, because the fight decides whether the next
        // update waits for a committed turn; the update is then consumed and its one projection published.
        ResolveMode();
        ReportUnclaimed(input);
        Advance(tick);
        return input.Actions.Count > 0 || !input.Digital.IsEmpty;
    }

    /// <summary>
    /// Reports every action on this session's own contracts that nothing took, so a control the panel offered and
    /// the session could not act on says so rather than looking like a control that did nothing.
    /// </summary>
    /// <remarks>
    /// An action goes untaken when the mechanism it belongs to is not composed, or is not open to take it at that
    /// moment — a purchase with no counter open, a casting while the session is held — or when its name is one no
    /// mechanism knows. The host's own pause and resume reach the lifecycle through its router before the update,
    /// so they are the host's rather than the session's to report.
    /// </remarks>
    private void ReportUnclaimed(ActionInbox input)
    {
        foreach (UiAction action in input.Unclaimed(_contracts))
        {
            if (action.Name is UiActionPayload.PauseSession or UiActionPayload.ResumeSession) continue;
            _owners.Diagnostics.Refused(
                "input",
                "action-unclaimed",
                $"'{action.Name}' arrived on '{action.Contract}' and nothing in this session took it: the mechanism it belongs to is not composed, was not open to take it in {_mode} mode, or does not know the name.");
        }
    }

    /// <summary>The contracts the host declared this session's semantic actions on.</summary>
    private static HashSet<string> Contracts(SessionControls controls) =>
        new(
            new[]
            {
                controls.Creation?.ActionContract, controls.Save?.ActionContract, controls.Use?.ActionContract,
                controls.Service?.ActionContract, controls.Rest?.ActionContract, controls.Conversation?.ActionContract,
                controls.Combat?.ActionContract, controls.Combat?.Turn?.ActionContract, controls.Skills?.ActionContract,
                controls.Cast?.ActionContract, controls.Mix?.ActionContract, controls.Equip?.ActionContract,
            }.OfType<string>(),
            StringComparer.Ordinal);

    /// <summary>What the player's controls act on in this update.</summary>
    /// <remarks>
    /// This is not a <see cref="SessionMode"/>, deliberately: the mode says how the world is paced, and a screen
    /// does not pace it — the world keeps advancing behind a counter or a conversation, one clock and one update.
    /// What a screen changes is what the player's keys reach, which is this.
    /// </remarks>
    private enum ControlHolder
    {
        /// <summary>The party: a step walks it, and a use, a stop, a cast, and a mixture apply.</summary>
        Party,

        /// <summary>A counter or a conversation: the screen is what the player acts on, and the party stands still.</summary>
        Screen,

        /// <summary>A paced fight waiting for a committed turn: the party does not walk, and an instant still applies.</summary>
        Turn,
    }

    /// <summary>Who holds the player's controls this update: an open screen first, then a turn the fight awaits.</summary>
    private ControlHolder HeldBy() =>
        Services is { IsOpen: true } || Conversations is { IsOpen: true }
            ? ControlHolder.Screen
            : Combat is { Turns.WaitsForPlayer: true } ? ControlHolder.Turn : ControlHolder.Party;

    /// <summary>The interval this admitted update covers, which is zero for a session that is not running.</summary>
    /// <remarks>
    /// One derivation of the interval, so the movement step and the clock cannot be advanced by different
    /// amounts, and a batch this session cannot measure advances nothing at all.
    /// </remarks>
    private double AdmittedSeconds(SessionTick tick)
    {
        if (_mode != SessionMode.Running) return 0;
        double seconds = tick.AdmittedStepCount * tick.FixedDeltaSeconds;
        return double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
    }

    /// <summary>
    /// Consumes one admitted tick. Only a running session advances: a held or engine-paused session keeps
    /// receiving admitted updates, and must publish the frozen state it holds rather than the engine's
    /// advancing step counter.
    /// </summary>
    private void Advance(SessionTick tick)
    {
        _updates++;
        if (_mode == SessionMode.Running)
        {
            // A batch covers [SimulationStep, SimulationStep + AdmittedStepCount), so accounting by batch end is
            // what makes the published seconds and the published step count describe the same simulation.
            ulong batchStart = tick.SimulationStep;
            ulong batchEnd = batchStart + tick.AdmittedStepCount;
            // After a hold, the first running tick re-establishes the baseline, so the held interval is never
            // credited to the session.
            _accountedThroughStep ??= batchStart;
            _simulationSeconds += (batchEnd - _accountedThroughStep.Value) * tick.FixedDeltaSeconds;
            _accountedThroughStep = batchEnd;
            _admittedSteps += tick.AdmittedStepCount;
        }
    }

    /// <summary>Stops the session, publishes the stop, and releases the projection channel.</summary>
    /// <remarks>
    /// The party and the world are released here because the session holds them: a party that outlived its
    /// session would be a second live party. The stop is published before either is released, because the
    /// projection reads the party's accounts. Releasing a session is not a save.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mode = SessionMode.Stopped;
        Publish();
        LiveWorld?.Dispose();
        Party?.Dispose();
        _saves.Store?.Dispose();
        _projection.Dispose();
    }

    private void ResolveMode()
    {
        SessionMode next = _disposed
            ? SessionMode.Stopped
            : !_started
                ? SessionMode.Starting
                // Neither an engine pause nor a player's hold takes a session out of creation, because creation
                // steps nothing either would stop and must keep taking choices while the world is held.
                : _creation is not null
                    ? SessionMode.Creating
                    : _enginePaused || _held
                        ? SessionMode.Paused
                        // A paced fight waiting for one of the party's turns owns the update as a screen does; a
                        // hold still outranks it, because a player who asked the session to stop asked for
                        // nothing to move.
                        : Combat is { Turns.WaitsForPlayer: true } ? SessionMode.TurnBased : SessionMode.Running;

        if (next == _mode) return;
        _mode = next;
        // Stepping stops and resumes with the mode, so the held interval is never measured.
        if (_mode is SessionMode.Paused or SessionMode.TurnBased) _accountedThroughStep = null;
        Publish();
    }

    /// <summary>
    /// Sends the panel the session as it stands, unless an update is under way — that update sends it once, when
    /// it is done — or nothing has changed since the last value sent.
    /// </summary>
    /// <remarks>
    /// A session that is held and hears nothing changes nothing, so it sends nothing: the panel already shows
    /// what it holds. The comparison is of the whole value written from the snapshot, whose expensive blocks are
    /// read again only when an owner they read has changed (<see cref="Snapshot"/>), so a running update that only
    /// moved the clock reads again the blocks that show the clock and writes the rest as they were last read.
    /// </remarks>
    private void Publish()
    {
        if (_updating) return;
        UiValue value = SessionProjection.Build(Snapshot());
        if (_published is { } last && Same(last, value)) return;
        _projection.Publish(value);
        _published = value;
    }

    /// <summary>Whether two projection values say exactly the same thing.</summary>
    private static bool Same(UiValue one, UiValue other) =>
        one.Root == other.Root &&
        one.Nodes.Span.SequenceEqual(other.Nodes.Span) &&
        one.Edges.Span.SequenceEqual(other.Edges.Span) &&
        one.Utf8.Span.SequenceEqual(other.Utf8.Span);

    /// <summary>
    /// Reads every block the projection publishes from the owner that holds its facts: a session with no
    /// mechanism, one that has not used it yet, and one whose last act was refused are different facts each
    /// block can tell apart, and nothing here works a number out.
    /// </summary>
    /// <remarks>
    /// The blocks whose owners change when somebody acts or the clock delivers something, rather than on every
    /// update — the party, the members a played party was made with, skills, promotion, magic, alchemy, quests,
    /// journal, automap and equipment — are handed back as they were last read while every owner they read carries
    /// the same change stamp and the few live facts each shows are unchanged (<see cref="ProjectionReadings"/>); the
    /// party's stamp is read once for all of them. The rest move with the update, or are a handful of fields, and are
    /// read every time. The progression block is one of those: the fee each row shows is the open counter's, which
    /// opens and closes with the clock.
    /// </remarks>
    private SessionSnapshot Snapshot()
    {
        long party = Party?.Stamp ?? 0;
        return new(
            _composition,
            _mode,
            _simulationSeconds,
            _admittedSteps,
            _world,
            MovementSnapshot.From(LiveWorld?.Movement.Last, LiveWorld?.Mover?.MayFly == true),
            ClockSnapshot.From(Clock),
            _readings.Party(Party, party, _owners.Rules.Standing),
            // While a party is being made the flow is the screen's subject; once one is played, the members shown
            // are the party's own, whether it was created in this run or resumed.
            _creation is { } creation
                ? CreationSnapshot.From(creation.Flow, creation.Refusal)
                : _accepted || _resumed ? _readings.Members(Party, party) : CreationSnapshot.None,
            _saves.State,
            InteractionSnapshot.From(LiveWorld?.Interaction),
            ServiceSnapshot.From(Services),
            RestSnapshot.Read(Rest, Clock),
            ConversationSnapshot.From(Conversations),
            CombatSnapshot.From(Combat, _owners.Director),
            ProgressionSnapshot.From(Progression, Services),
            _readings.Promotion(Progression, party),
            _readings.Skills(Progression, party, _owners.Rules.Names),
            _readings.Magic(_owners.Casting, party, _world),
            _readings.Alchemy(_owners.Mixing, party, _owners.Rules.Alchemy?.Kinds),
            _readings.Quests(Quests, party, Clock),
            _readings.Journal(Journal, Quests, LiveWorld, Clock, Knowledge, Maps),
            _readings.Map(Maps, LiveWorld, _owners.Rules.Magic?.Running),
            _keys,
            _readings.Equipment(_owners.Outfitting, party));
    }

    /// <summary>Reads this session into the product's one current save schema, without writing anything.</summary>
    /// <returns>The session as a save records it.</returns>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or something a save cannot carry.</exception>
    public SessionSave Capture()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return SessionSave.Capture(this);
    }

    /// <summary>Writes this session at its explicit save boundary, and returns the document written.</summary>
    /// <returns>The document that was written.</returns>
    /// <exception cref="InvalidOperationException">The session was composed without a save store.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or the write failed.</exception>
    public SessionSave Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (Saves is not { } boundary)
        {
            throw new InvalidOperationException(
                $"The session in '{_composition.Title}' was composed without a save store, so there is nowhere to write a save; a session is composed with one when the product has a place to keep them.");
        }

        return boundary.Save(this);
    }
}
