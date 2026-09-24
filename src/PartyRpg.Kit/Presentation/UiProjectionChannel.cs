using Rusty.Engine;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// Where a session publishes its presentation. The engine owns the transport and the browser shell;
/// this seam exists so a session can be exercised without an engine runtime, and so the exchange
/// between the session and the engine's UI service stays in one place.
/// </summary>
public interface IUiProjectionChannel : IDisposable
{
    /// <summary>Publishes one complete projection value for the declared stream.</summary>
    void Publish(UiValue value);
}

/// <summary>
/// The ordinary channel: one engine UI stream, one monotonically increasing sequence.
/// </summary>
public sealed class EngineUiProjectionChannel : IUiProjectionChannel
{
    private readonly IUiService _ui;
    private readonly UiStream _stream;
    private ulong _sequence;
    private bool _disposed;

    /// <summary>Opens the stream named by <paramref name="request"/> on the engine's UI service.</summary>
    public EngineUiProjectionChannel(IUiService ui, UiStreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        _stream = ui.OpenStream(request);
    }

    /// <inheritdoc />
    public void Publish(UiValue value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _ui.PublishProjection(new UiProjection(_stream, ++_sequence, value));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stream.Dispose();
    }
}
