using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// How a session reports what it did and what it refused to the engine's diagnostics, in one shape.
/// </summary>
/// <remarks>
/// Two kinds of report and no others: something that happened is information that was accepted, and something
/// that was refused is a warning the player can recover from. A refusal is a refusal whichever mechanism it came
/// from, so a night that could not be taken and a spell that could not be cast read the same way in the
/// product's own account of itself. A session with no diagnostics reports nothing and loses nothing it acts on.
/// </remarks>
internal readonly struct SessionDiagnostics(IDiagnosticsService? service)
{
    /// <summary>The engine's diagnostics, or null when none are reachable.</summary>
    public IDiagnosticsService? Service => service;

    /// <summary>Reports something that happened.</summary>
    public void Applied(string source, string code, string message) =>
        service?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            source,
            code,
            message,
            Correlation: string.Empty));

    /// <summary>Reports something that was refused, which leaves the session exactly as it was.</summary>
    public void Refused(string source, string code, string message) =>
        service?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Warning,
            DiagnosticsDisposition.RejectedRecoverable,
            source,
            code,
            message,
            Correlation: string.Empty));

    /// <summary>Reports what happened or what was refused, by whether it applied.</summary>
    public void Report(bool applied, string source, string code, string message)
    {
        if (applied) Applied(source, code, message);
        else Refused(source, code, message);
    }
}
