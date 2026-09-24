using PartyRpg.Kit.Presentation;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The ordinary session shell: it owns the session's mode, measures the admitted simulation it has
/// consumed, and publishes one presentation through its projection channel.
/// </summary>
/// <remarks>
/// The shell deliberately owns no gameplay. Later stones attach world, party, combat, and content
/// mechanisms to this session; what exists here is the lifecycle those mechanisms will step inside,
/// and the one place that decides what a mode means for stepping.
/// </remarks>
public sealed class PartyRpgSession : IGameSession
{
    private readonly SessionComposition _composition;
    private readonly IUiProjectionChannel _projection;
    private SessionMode _mode = SessionMode.Starting;
    private double _simulationSeconds;
    private ulong _admittedSteps;
    private ulong _updates;
    private ulong _lastSimulationStep;
    private bool _hasSimulationBaseline;
    private bool _disposed;

    /// <summary>Creates a session for a compiled ruleset over the mechanisms the kit supplies.</summary>
    public PartyRpgSession(SessionComposition composition, IUiProjectionChannel projection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(composition.Title);
        _composition = composition;
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
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

    /// <summary>Admitted updates this session has consumed, whether or not it was running.</summary>
    public ulong Updates => _updates;

    /// <inheritdoc />
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mode != SessionMode.Starting) return;
        _mode = SessionMode.Running;
        // A session that has not run yet has no baseline; the first admitted tick establishes one so
        // the engine's runtime-wide step counter never reads as this session's own elapsed time.
        _hasSimulationBaseline = false;
        Publish();
    }

    /// <inheritdoc />
    public void Pause()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mode != SessionMode.Running) return;
        _mode = SessionMode.Paused;
        _hasSimulationBaseline = false;
        Publish();
    }

    /// <inheritdoc />
    public void Resume()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_mode != SessionMode.Paused) return;
        _mode = SessionMode.Running;
        Publish();
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
        Advance(SessionTick.From(update.Facts));
        return ProductUpdateResult.None;
    }

    /// <summary>
    /// Consumes one admitted tick. Only a running session advances: a paused session keeps receiving
    /// admitted updates while the player is held, and must publish the frozen state it holds rather
    /// than the engine's advancing step counter.
    /// </summary>
    public void Advance(SessionTick tick)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _updates++;
        if (_mode == SessionMode.Running)
        {
            if (_hasSimulationBaseline)
            {
                // The baseline is re-established after every pause so the held interval is not
                // credited to the session when it resumes.
                _simulationSeconds += (tick.SimulationStep - _lastSimulationStep) * tick.FixedDeltaSeconds;
            }

            _lastSimulationStep = tick.SimulationStep;
            _hasSimulationBaseline = true;
            _admittedSteps += tick.AdmittedStepCount;
        }

        Publish();
    }

    /// <summary>Stops the session and releases the projection channel.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _mode = SessionMode.Stopped;
        _projection.Dispose();
    }

    private void Publish() => _projection.Publish(SessionProjection.Build(Snapshot()));

    private SessionSnapshot Snapshot() => new(_composition, _mode, _simulationSeconds, _admittedSteps, _updates);
}
