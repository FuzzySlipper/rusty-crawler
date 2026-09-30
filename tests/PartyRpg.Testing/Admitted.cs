using System.Text;
using Rusty.Engine;

namespace PartyRpg.Testing;

/// <summary>
/// Admitted updates and admitted input in the shape the engine admits them, so a suite states what arrived rather
/// than the twenty-three fields an input event carries.
/// </summary>
public static class Admitted
{
    /// <summary>The fixed step the suites admit at.</summary>
    public const double StepSeconds = 1.0 / 60.0;

    /// <summary>One admitted update of a number of fixed steps, carrying the input given.</summary>
    public static ProductUpdate Update(ulong step, uint admitted, params ProductInputEvent[] input) =>
        Update(step, admitted, StepSeconds, input);

    /// <summary>One admitted update of a number of fixed steps of a stated length, carrying the input given.</summary>
    public static ProductUpdate Update(ulong step, uint admitted, double stepSeconds, params ProductInputEvent[] input) =>
        new(
            new ProductUpdateFacts(
                ProductUpdateMode.Realtime,
                ProductLifecycleState.Running,
                Generation: 1,
                ControlRevision: 1,
                ObservedHostTimeNanoseconds: 0,
                SimulationStep: step,
                FixedStepHz: 60,
                AdmittedStepCount: admitted,
                DroppedStepCount: 0,
                FixedDeltaSeconds: stepSeconds),
            input);

    /// <summary>
    /// An update that admits no time and carries nothing: the session steps nothing, and publishes what the world
    /// now stands on — which is how a suite that moved the party by hand reads it back.
    /// </summary>
    public static ProductUpdate Nothing(ulong step) => Update(step, 0);

    /// <summary>One digital event on an intent, as the engine admits a mapped key.</summary>
    /// <remarks>
    /// The engine copies five byte blocks per event in constructor order: label, mapping id, intent, payload
    /// contract, payload data. A digital event carries only the intent.
    /// </remarks>
    public static ProductInputEvent Digital(
        string intent,
        InputEdge edge = InputEdge.Pressed,
        InputPhase phase = InputPhase.Pressed,
        InputProvenance provenance = InputProvenance.Physical,
        InputValueKind valueKind = InputValueKind.Digital) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        valueKind, phase, provenance, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>A key coming back up, which is what ends a held control.</summary>
    public static ProductInputEvent Released(string intent) => Digital(intent, InputEdge.Released, InputPhase.Released);

    /// <summary>A digital intent claimed by a screen's own button rather than pressed on a key.</summary>
    public static ProductInputEvent Claimed(string intent) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>One semantic action on a payload contract, as the DOM companion sends it.</summary>
    public static ProductInputEvent Payload(string contract, string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(contract), Encoding.UTF8.GetBytes(json));

    /// <summary>One payload action naming only its action, on a contract.</summary>
    public static ProductInputEvent Action(string contract, string action) =>
        Payload(contract, $$"""{ "action": "{{action}}" }""");
}
